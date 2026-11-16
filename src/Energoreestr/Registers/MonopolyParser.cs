using Energoreestr.Codes;
using Energoreestr.Csv;
using Energoreestr.Text;

namespace Energoreestr.Registers;

/// <summary>Запис реєстру суб'єктів природних монополій.</summary>
public record MonopolyRow(
    int? Number,
    string? Edrpou,
    bool IsIndividual,
    string Name,
    string Location,
    string Activity,
    string Territory)
{
    /// <summary>За чим відрізняємо один запис від іншого, коли порівнюємо два файли.</summary>
    public string Key => $"{Edrpou ?? Name}|{Activity}|{Territory}";
}

public record MonopolyFile(List<MonopolyRow> Rows, List<RowIssue> Issues, int TotalRows);

/// <summary>
/// Розбирає реєстри природних монополій (енергетика, тепло і вода). Формат у них
/// спільний: крапка з комою, колонки "номер; ЄДРПОУ; назва; місцезнаходження;
/// вид діяльності; територія".
///
/// Файл по теплу і воді збережено з Excel як є: там є сьома колонка з назвою
/// органу, після неї ще 67 порожніх, а між записами стоять рядки-заголовки
/// з назвою області.
/// </summary>
public static class MonopolyParser
{
    public static MonopolyFile Parse(string text)
    {
        var records = CsvReader.Read(text, ';');
        var rows = new List<MonopolyRow>();
        var issues = new List<RowIssue>();
        var seen = new HashSet<string>();
        int total = 0;

        foreach (var record in records.Skip(1))
        {
            var f = record.Fields;

            // рядок-заголовок області: заповнена тільки перша клітинка
            if (f.Skip(1).All(string.IsNullOrWhiteSpace)) continue;

            total++;

            if (f.Length < 6 || string.IsNullOrWhiteSpace(f[2]))
            {
                issues.Add(new RowIssue(record.Line, f[0].Trim(), IssueCodes.RowUnreadable, null));
                continue;
            }

            // номер у кількох записах пропущено, і він однаково не унікальний:
            // у файлі по теплу і воді нумерація починається заново в кожній області
            int? number = int.TryParse(f[0].Trim(), out int n) ? n : null;

            string name = OneLine(f[2]);
            string rawCode = f[1].Trim();
            string key = rawCode.Length > 0 ? rawCode : name;

            string? edrpou = null;
            bool isIndividual = false;

            if (rawCode.Length == 10 && rawCode.All(char.IsAsciiDigit) && LooksLikePersonName(name))
            {
                // РНОКПП підприємця не зберігаємо
                isIndividual = true;
                key = name;
            }
            else if (Edrpou.Normalize(rawCode) is { } code)
            {
                if (code != rawCode)
                    issues.Add(new RowIssue(record.Line, key, IssueCodes.EdrpouLeadingZeros, rawCode));
                if (!Edrpou.IsValid(code))
                    issues.Add(new RowIssue(record.Line, key, IssueCodes.EdrpouChecksum, rawCode));
                edrpou = code;
            }
            else
            {
                // сюди потрапляє і порожня клітинка, тоді значення в зауваженні немає
                issues.Add(new RowIssue(record.Line, key, IssueCodes.IdCodeFormat, rawCode.Length > 0 ? rawCode : null));
            }

            foreach (var word in Names.MixedAlphabetWords(name))
                issues.Add(new RowIssue(record.Line, key, IssueCodes.MixedAlphabet, word));

            var row = new MonopolyRow(
                number, edrpou, isIndividual, name,
                OneLine(f[3]), OneLine(f[4]), OneLine(f[5]));

            // одна компанія може мати кілька записів (різні види діяльності чи області),
            // але повністю однакові рядки це вже повтор
            if (!seen.Add(row.Key))
            {
                issues.Add(new RowIssue(record.Line, key, IssueCodes.DuplicateKey, name));
                continue;
            }

            rows.Add(row);
        }

        return new MonopolyFile(rows, issues, total);
    }

    // Десять цифр у колонці коду бувають і в організацій, коли код набрали з помилкою.
    // Підприємець записаний або з позначкою "фізична особа-підприємець", або просто
    // як прізвище, ім'я, по батькові: два-чотири слова без лапок.
    private static bool LooksLikePersonName(string name)
    {
        if (name.StartsWith("ФІЗИЧНА ОСОБА", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("ФОП ", StringComparison.OrdinalIgnoreCase))
            return true;

        var words = name.Split(' ');
        return words.Length is >= 2 and <= 4
            && words.All(w => w.All(c => char.IsLetter(c) || c is '-' or '\'' or '’' or 'ʼ'));
    }

    // прибирає подвійні пробіли й перенесення всередині клітинки
    private static string OneLine(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
