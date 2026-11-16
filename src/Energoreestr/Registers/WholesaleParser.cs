using System.Globalization;
using System.Text.RegularExpressions;
using Energoreestr.Codes;
using Energoreestr.Csv;
using Energoreestr.Text;

namespace Energoreestr.Registers;

/// <summary>Запис Реєстру учасників оптового енергетичного ринку.</summary>
public record ParticipantRow(
    int Number,
    string EcrbCode,
    DateOnly SubmittedOn,
    DateOnly RegisteredOn,
    DateOnly? ExcludedOn,
    string Name,
    string LegalForm,
    string? Address,
    string? Settlement,
    string? Zip,
    string Country,
    string? Eic,
    string? Bic,
    string? Lei,
    string? Vat,
    string? IdCode,
    bool IsIndividual,
    string? Website,
    string? InsideInfoUrl);

public record WholesaleFile(List<ParticipantRow> Rows, List<RowIssue> Issues, int TotalRows);

/// <summary>
/// Розбирає файл registry-opt-ua-*.csv.
///
/// Файл роздільником має кому, але значення з комами в лапки не взяті. Назва органу
/// реєстрації ("Національна комісія, що здійснює...") є в кожному рядку, тому жоден
/// рядок не має заявлених 20 полів. Колонки відновлює <see cref="RowRepair"/>.
/// </summary>
public static partial class WholesaleParser
{
    private const int Columns = 20;
    private const string Ukraine = "Україна";

    // номери колонок у файлі
    private const int ColNumber = 0, ColSubmitted = 3, ColRegistered = 4, ColEcrb = 5, ColName = 6,
        ColLegalForm = 7, ColAddress = 8, ColSettlement = 9, ColZip = 10, ColCountry = 11, ColEic = 12,
        ColBic = 13, ColLei = 14, ColVat = 15, ColIdCode = 16, ColWebsite = 17, ColInsideInfo = 18,
        ColExcluded = 19;

    [GeneratedRegex(@"^ECRB-UA-\d{8}-\d{3}$")]
    private static partial Regex EcrbPattern();

    // організаційно-правова форма пишеться з великої літери звичайним реченням,
    // а назва компанії великими літерами. Це допомагає не сплутати ці дві колонки
    [GeneratedRegex(@"^[А-ЯІЇЄҐA-Z][а-яіїєґa-z'’ ()/-]+$")]
    private static partial Regex LegalFormPattern();

    [GeneratedRegex(@"^[A-Z0-9]{8}([A-Z0-9]{3})?$")]
    private static partial Regex BicPattern();

    [GeneratedRegex(@"^[A-Z0-9]{20}$")]
    private static partial Regex LeiPattern();

    private static readonly RowRepair Repair = new(BuildChecks());

    public static WholesaleFile Parse(string text)
    {
        var records = CsvReader.Read(text, ',');
        var rows = new List<ParticipantRow>();
        var issues = new List<RowIssue>();
        var seen = new HashSet<string>();

        // перший запис це заголовок
        foreach (var record in records.Skip(1))
        {
            // коми могли лишитися і всередині полів у лапках, тому ріжемо все до кінця
            var pieces = record.Fields.SelectMany(f => f.Split(',')).ToList();
            var repaired = Repair.Repair(pieces);

            if (repaired.Kind == RepairKind.Failed)
            {
                issues.Add(new RowIssue(record.Line, record.Fields[0], IssueCodes.RowUnreadable, null));
                continue;
            }

            var f = repaired.Fields;
            string key = f[ColEcrb];

            if (!int.TryParse(f[ColNumber], out int number)
                || !EcrbPattern().IsMatch(key)
                || !TryDate(f[ColSubmitted], out var submitted, out _)
                || !TryDate(f[ColRegistered], out var registered, out _))
            {
                issues.Add(new RowIssue(record.Line, f[ColNumber], IssueCodes.RowUnreadable, null));
                continue;
            }

            if (repaired.Kind == RepairKind.Rearranged)
                issues.Add(new RowIssue(record.Line, key, IssueCodes.RowRearranged, null));
            if (repaired.Kind == RepairKind.Ambiguous)
                issues.Add(new RowIssue(record.Line, key, IssueCodes.RowAmbiguous, null));

            if (!seen.Add(key))
            {
                issues.Add(new RowIssue(record.Line, key, IssueCodes.DuplicateKey, key));
                continue;
            }

            // у цьому файлі порожнє значення записане словом null, інколи дефісом
            string? Value(int column)
            {
                string value = f[column];
                if (value == "null" || value.Length == 0) return null;
                if (value == "-")
                {
                    issues.Add(new RowIssue(record.Line, key, IssueCodes.DashPlaceholder, null));
                    return null;
                }
                return value;
            }

            DateOnly? excluded = null;
            if (Value(ColExcluded) is { } excludedText)
            {
                if (TryDate(excludedText, out var date, out bool hasTime))
                {
                    excluded = date;
                    if (hasTime) issues.Add(new RowIssue(record.Line, key, IssueCodes.DateFormat, excludedText));
                }
                else
                {
                    issues.Add(new RowIssue(record.Line, key, IssueCodes.DateFormat, excludedText));
                }
            }

            string name = f[ColName];
            string country = f[ColCountry];
            foreach (var word in Names.MixedAlphabetWords(name).Concat(Names.MixedAlphabetWords(country)))
                issues.Add(new RowIssue(record.Line, key, IssueCodes.MixedAlphabet, word));

            string? eic = Value(ColEic);
            if (eic is not null)
            {
                if (!Eic.HasValidFormat(eic))
                    issues.Add(new RowIssue(record.Line, key, IssueCodes.EicFormat, eic));
                else if (!Eic.IsValid(eic))
                    issues.Add(new RowIssue(record.Line, key, IssueCodes.EicChecksum, eic));
            }

            string? bic = Value(ColBic);
            if (bic is not null && !BicPattern().IsMatch(bic.Replace(" ", "")))
                issues.Add(new RowIssue(record.Line, key, IssueCodes.BicFormat, bic));

            string? lei = Value(ColLei);
            if (lei is not null && !LeiPattern().IsMatch(lei))
                issues.Add(new RowIssue(record.Line, key, IssueCodes.LeiFormat, lei));

            string? idCode = Value(ColIdCode);
            bool isIndividual = false;
            if (country == Ukraine && idCode is not null)
            {
                if (idCode.Length == 10 && idCode.All(char.IsAsciiDigit))
                {
                    // 10 цифр це РНОКПП фізичної особи-підприємця. Податковий номер
                    // людини в базу не кладемо, лишаємо тільки позначку
                    isIndividual = true;
                    idCode = null;
                }
                else if (Edrpou.Normalize(idCode) is { } edrpou)
                {
                    if (edrpou != idCode)
                        issues.Add(new RowIssue(record.Line, key, IssueCodes.EdrpouLeadingZeros, idCode));
                    if (!Edrpou.IsValid(edrpou))
                        issues.Add(new RowIssue(record.Line, key, IssueCodes.EdrpouChecksum, idCode));
                    idCode = edrpou;
                }
                else
                {
                    issues.Add(new RowIssue(record.Line, key, IssueCodes.IdCodeFormat, idCode));
                }
            }

            string? website = Value(ColWebsite);
            string? insideInfo = Value(ColInsideInfo);
            if (website is not null && !LooksLikeUrl(website))
                issues.Add(new RowIssue(record.Line, key, IssueCodes.UrlFormat, website));
            // часто це та сама адреса, що й сайт: двічі про одне не пишемо
            if (insideInfo is not null && insideInfo != website && !LooksLikeUrl(insideInfo))
                issues.Add(new RowIssue(record.Line, key, IssueCodes.UrlFormat, insideInfo));

            // у підприємця адреса це зазвичай домашня адреса, а номер платника ПДВ
            // збігається з РНОКПП, тому їх теж не зберігаємо
            string? address = isIndividual ? null : Value(ColAddress);
            string? zip = isIndividual ? null : Value(ColZip);
            string? vat = isIndividual ? null : Value(ColVat);

            rows.Add(new ParticipantRow(
                number, key, submitted, registered, excluded,
                name, f[ColLegalForm],
                address, Value(ColSettlement), zip, country,
                eic, bic, lei, vat, idCode, isIndividual,
                website, insideInfo));
        }

        return new WholesaleFile(rows, issues, records.Count - 1);
    }

    // Якою має бути кожна колонка. Перевірки навмисно м'які: вони потрібні,
    // щоб відрізнити одну колонку від сусідньої, а не щоб знайти помилку в даних.
    private static Func<string, bool>?[] BuildChecks()
    {
        var checks = new Func<string, bool>?[Columns];

        checks[ColNumber] = s => s.Length > 0 && s.All(char.IsAsciiDigit);
        checks[2] = s => !s.Any(char.IsDigit);                       // країна органу реєстрації
        checks[ColSubmitted] = s => TryDate(s, out _, out _);
        checks[ColRegistered] = s => TryDate(s, out _, out _);
        checks[ColEcrb] = s => EcrbPattern().IsMatch(s);
        checks[ColName] = s => s.Length > 1;
        checks[ColLegalForm] = s => LegalFormPattern().IsMatch(s);
        checks[ColZip] = s => IsBlank(s) || (s.Length <= 10 && s.Any(char.IsDigit));
        checks[ColCountry] = s => !IsBlank(s) && !s.Any(char.IsDigit);
        checks[ColEic] = s => IsBlank(s) || (s.Length >= 3 && char.IsDigit(s[0]) && char.IsDigit(s[1]) && char.IsLetter(s[2]));
        checks[ColBic] = s => IsBlank(s) || (s.Length is >= 8 and <= 12 && s.All(c => char.IsAsciiLetterOrDigit(c) || c == ' '));
        checks[ColLei] = s => IsBlank(s) || (s.Length is >= 16 and <= 20 && s.All(char.IsAsciiLetterOrDigit));
        checks[ColVat] = s => IsBlank(s) || (s.Any(char.IsDigit) && !MentionsSite(s));
        checks[ColIdCode] = s => IsBlank(s) || (s.Length <= 20 && s.Any(char.IsDigit) && !MentionsSite(s));
        checks[ColWebsite] = s => IsBlank(s) || s.Contains('.');
        checks[ColInsideInfo] = s => IsBlank(s) || s.Contains('.');
        checks[ColExcluded] = s => s == "null" || TryDate(s, out _, out _);

        return checks;
    }

    private static bool IsBlank(string s) => s is "null" or "-";

    private static bool MentionsSite(string s) => s.Contains("http") || s.Contains("www");

    // у полі може бути кілька адрес через кому, перевіряємо кожну
    private static bool LooksLikeUrl(string s)
    {
        foreach (var part in s.Split(',', StringSplitOptions.TrimEntries))
        {
            if (!part.Contains('.') || part.Contains('@') || part.Any(char.IsWhiteSpace))
                return false;
        }
        return true;
    }

    private static bool TryDate(string s, out DateOnly date, out bool hasTime)
    {
        hasTime = false;
        if (DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return true;

        // в одному записі дата виключення має хвіст " 00:00:00"
        if (DateTime.TryParseExact(s, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var withTime))
        {
            date = DateOnly.FromDateTime(withTime);
            hasTime = true;
            return true;
        }
        return false;
    }
}
