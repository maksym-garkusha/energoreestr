using Energoreestr.Codes;
using Energoreestr.Csv;
using Energoreestr.Registers;
using Energoreestr.Text;

namespace Energoreestr.Check;

/// <summary>Один прохід перевірки одного файлу. Стан тримаємо в полях, щоб методи лишалися малими.</summary>
internal sealed class FileChecker(Decoded decoded, DateOnly today)
{
    // довших значень не буває навіть в адресах; таке поле означає незакриті лапки
    private const int MaxField = 100_000;
    // скільки рядків з однаковою кількістю полів досить, щоб судити про колонки лише за ними
    private const int MinAlignedRows = 5;

    private readonly string text = decoded.Text!;
    private readonly List<Finding> findings = [.. decoded.Findings];
    private readonly HashSet<string> seen = [];

    private char delimiter;
    private List<CsvRecord> data = [];
    private int width;
    private bool hasHeader;
    private int headerLine;
    private string[] names = [];
    private ColumnKind[] kinds = [];
    private int[] filled = [];
    private bool[] empty = [];
    private bool[] codeLike = [];
    private bool[] phone = [];
    private DatePattern?[] usualDate = [];
    private char?[] usualSeparator = [];
    private int personalRows;

    public CheckReport Run()
    {
        var records = Read();
        if (records.Count == 0) return CheckReport.Unreadable(FileCheck.Empty);

        ReadHeader(records);
        DescribeColumns();
        foreach (var row in data) CheckRow(row);
        CheckColumns();

        var columns = Enumerable.Range(0, width).Select(c => new ColumnProfile(c, names[c], kinds[c], filled[c])).ToList();
        var profile = new FileProfile(decoded.Encoding, delimiter, hasHeader, columns);
        return new CheckReport(data.Count, findings, personalRows, profile);
    }

    private List<CsvRecord> Read()
    {
        char? detected = Delimiters.Detect(text);
        if (detected is null) findings.Add(new(CheckCodes.SingleColumn, 0));
        delimiter = detected ?? ',';

        var records = CsvReader.Read(text, delimiter, out int? unclosed);
        int? swallowed = unclosed ?? records.FirstOrDefault(r => r.Fields.Any(f => f.Length > MaxField))?.Line;
        if (swallowed is int line) findings.Add(new(CheckCodes.UnclosedQuote, line));
        return records;
    }

    private void ReadHeader(List<CsvRecord> records)
    {
        var first = records[0];
        width = first.Fields.Length;
        headerLine = first.Line;
        names = [.. first.Fields.Select(f => f.Trim())];
        data = records.Skip(1).ToList();
        hasHeader = true;
        kinds = GuessKinds();

        if (!FirstRowLooksLikeData(first)) return;

        findings.Add(new(CheckCodes.HeaderMissing, first.Line));
        hasHeader = false;
        names = new string[width];
        Array.Fill(names, "");
        data = records;
        kinds = GuessKinds();
    }

    // Перший рядок це дані, якщо в половині колонок, де ми впізнали коди, дати чи числа,
    // і в ньому стоїть код, дата чи число
    private bool FirstRowLooksLikeData(CsvRecord first)
    {
        int typed = 0, fits = 0;
        for (int c = 0; c < width; c++)
        {
            if (kinds[c] is ColumnKind.Unknown or ColumnKind.Text) continue;
            typed++;
            if (FitsKind(first.Fields[c].Trim(), kinds[c])) fits++;
        }
        return typed > 0 && fits * 2 >= typed;
    }

    private static bool FitsKind(string value, ColumnKind kind) => kind switch
    {
        ColumnKind.Edrpou => Cells.IsEdrpouShaped(value),
        ColumnKind.Eic => Cells.EicShape().IsMatch(value),
        ColumnKind.Iban => Cells.IbanShape().IsMatch(Cells.Compact(value)),
        ColumnKind.Date => Dates.PatternOf(value) is not null,
        ColumnKind.Number => Cells.NumberShape().IsMatch(value),
        _ => false
    };

    private ColumnKind[] GuessKinds()
    {
        var rows = SampleRows();
        filled = new int[width];
        var guessed = new ColumnKind[width];
        for (int c = 0; c < width; c++)
        {
            var values = Values(rows, c);
            filled[c] = values.Count;
            guessed[c] = ColumnKinds.Guess(names[c], values);
        }
        return guessed;
    }

    // колонки впізнаємо за рядками, де значення стоять на своїх місцях, якщо таких досить
    private List<CsvRecord> SampleRows()
    {
        var rows = data.Where(r => !IsSection(r)).ToList();
        var aligned = rows.Where(IsAligned).ToList();
        return aligned.Count >= MinAlignedRows ? aligned : rows;
    }

    private static List<string> Values(IEnumerable<CsvRecord> rows, int column) =>
        rows.Where(r => column < r.Fields.Length)
            .Select(r => r.Fields[column].Trim())
            .Where(v => v.Length > 0 && !Cells.IsPlaceholder(v))
            .ToList();

    private void DescribeColumns()
    {
        var rows = data.Where(r => !IsSection(r) && IsAligned(r)).ToList();
        empty = new bool[width];
        codeLike = new bool[width];
        phone = new bool[width];
        usualDate = new DatePattern?[width];
        usualSeparator = new char?[width];

        for (int c = 0; c < width; c++)
        {
            var values = Values(rows, c);
            empty[c] = names[c].Length == 0 && rows.All(r => c >= r.Fields.Length || r.Fields[c].Trim().Length == 0);
            codeLike[c] = kinds[c] == ColumnKind.Edrpou || ColumnKinds.IsCodeHeader(names[c]) || TaxNumbersColumn(values);
            phone[c] = ColumnKinds.IsPhoneHeader(names[c]);
            usualDate[c] = Usual(values.Select(Dates.PatternOf));
            usualSeparator[c] = Usual(values.Select(Cells.DecimalSeparator));
        }
    }

    // колонка десятизначних чисел без назви, більшість яких проходить контрольну цифру РНОКПП
    private bool TaxNumbersColumn(List<string> values)
    {
        var tenDigits = values.Where(v => v.Length == 10 && Cells.Digits().IsMatch(v)).ToList();
        return tenDigits.Count >= MinAlignedRows && tenDigits.Count(v => Cells.IsPlausibleTaxNumber(v, today)) >= 0.6 * tenDigits.Count;
    }

    private static T? Usual<T>(IEnumerable<T?> values) where T : struct =>
        values.Where(v => v is not null).GroupBy(v => v!.Value).OrderByDescending(g => g.Count()).Select(g => (T?)g.Key).FirstOrDefault();

    private bool IsAligned(CsvRecord row) =>
        row.Fields.Length <= width || row.Fields.Skip(width).All(f => f.Trim().Length == 0);

    // рядок-заголовок посеред таблиці: заповнена одна клітинка з трьох і більше
    private bool IsSection(CsvRecord row) =>
        width >= 3 && row.Fields.Count(f => f.Trim().Length > 0) == 1;

    private void CheckRow(CsvRecord row)
    {
        if (IsSection(row))
        {
            findings.Add(new(CheckCodes.SectionRows, row.Line));
            return;
        }

        bool aligned = IsAligned(row);
        if (!aligned) findings.Add(new(CheckCodes.UnquotedDelimiter, row.Line));
        else if (row.Fields.Length < width) findings.Add(new(CheckCodes.MissingFields, row.Line));

        if (!seen.Add(string.Join('\u001f', row.Fields.Select(f => f.Trim()))))
            findings.Add(new(CheckCodes.DuplicateRow, row.Line));

        bool personal = MarkPersonal(row, aligned);
        bool garbled = false;
        int count = aligned ? Math.Min(row.Fields.Length, width) : row.Fields.Length;
        for (int c = 0; c < count; c++)
        {
            if (aligned && empty[c]) continue;
            var cell = new Cell(row.Line, aligned ? c : null, row.Fields[c], personal);
            if (aligned) CheckAlignedCell(cell, kinds[c]);
            else CheckLooseCell(cell);
            garbled |= CheckText(cell, aligned ? kinds[c] : ColumnKind.Unknown, garbled);
        }
    }

    /// <param name="Column">null, якщо значення не на своєму місці і колонку не визначити.</param>
    private record Cell(int Line, int? Column, string Raw, bool Personal)
    {
        public string Value { get; } = Raw.Trim();
    }

    private void Add(string code, Cell cell, string? value = null, string? note = null) =>
        findings.Add(new(code, cell.Line, cell.Column, cell.Personal ? null : value, note));

    // У рядку з ознаками персональних даних значень не показуємо взагалі: ні кодів, ні назв
    private bool MarkPersonal(CsvRecord row, bool aligned)
    {
        int? soleProprietor = null, taxNumber = null;
        for (int c = 0; c < row.Fields.Length; c++)
        {
            string value = row.Fields[c].Trim();
            if (value.Length == 0) continue;
            soleProprietor ??= SoleProprietor.IsNamedAsOne(value) ? c : null;
            taxNumber ??= LooksLikeTaxNumber(value, aligned && c < width ? c : null) ? c : null;
        }

        if (soleProprietor is int s) findings.Add(new(CheckCodes.PersonalSoleProprietor, row.Line, aligned ? s : null));
        if (taxNumber is int t) findings.Add(new(CheckCodes.PersonalTaxNumber, row.Line, aligned ? t : null));
        bool personal = soleProprietor is not null || taxNumber is not null;
        if (personal) personalRows++;
        return personal;
    }

    // У колонці кодів десять цифр означають РНОКПП, хоч би як їх записали: це правило імпорту.
    // Деінде вимагаємо ще й контрольну цифру і правдоподібну дату народження, бо там
    // десять цифр бувають і в номерах документів
    private bool LooksLikeTaxNumber(string value, int? column)
    {
        if (column is int c && codeLike[c]) return SoleProprietor.IsTaxNumber(value);
        if (column is int other && (phone[other] || kinds[other] is not (ColumnKind.Text or ColumnKind.Unknown))) return false;
        return Cells.IsPlausibleTaxNumber(value, today);
    }

    private void CheckAlignedCell(Cell cell, ColumnKind kind)
    {
        if (cell.Value.Length == 0) return;
        if (cell.Raw.Contains('\n') || cell.Raw.Contains('\r')) Add(CheckCodes.LineBreakInValue, cell);
        else if (kind != ColumnKind.Number && HasExtraSpaces(cell)) Add(CheckCodes.Whitespace, cell);

        if (Cells.IsPlaceholder(cell.Value))
        {
            Add(CheckCodes.PlaceholderValue, cell);
            return;
        }
        if (Cells.ExcelError().IsMatch(cell.Value))
        {
            Add(CheckCodes.ExcelError, cell, cell.Value);
            return;
        }

        switch (kind)
        {
            case ColumnKind.Edrpou: CheckEdrpou(cell); break;
            case ColumnKind.Eic: CheckEic(cell); break;
            case ColumnKind.Iban: CheckIban(cell); break;
            case ColumnKind.Date: CheckDate(cell); break;
            case ColumnKind.Number: CheckNumber(cell); break;
        }
    }

    // значення не на своєму місці: перевіряємо лише те, що не залежить від колонки
    private void CheckLooseCell(Cell cell)
    {
        if (Cells.ExcelError().IsMatch(cell.Value)) Add(CheckCodes.ExcelError, cell, cell.Value);
    }

    private static bool HasExtraSpaces(Cell cell) =>
        cell.Raw.Length != cell.Value.Length || cell.Value.Contains("  ") || cell.Value.Contains(' ');

    private void CheckEdrpou(Cell cell)
    {
        string value = cell.Value;
        if (Cells.Exponent().IsMatch(value)) Add(CheckCodes.ExcelExponent, cell, value);
        else if (Cells.TrailingZero().IsMatch(value)) Add(CheckCodes.ExcelTrailingZero, cell, value);
        else if (SoleProprietor.IsTaxNumber(value)) return;   // уже позначено як персональні дані
        else if (Cells.Digits().IsMatch(value) && value.Length is >= 5 and <= 8)
        {
            if (value.Length < 8) Add(CheckCodes.EdrpouLeadingZeros, cell, value);
            if (!Edrpou.IsValid(value.PadLeft(8, '0'))) Add(CheckCodes.EdrpouChecksum, cell, value);
        }
        else Add(CheckCodes.EdrpouFormat, cell, value);
    }

    private void CheckEic(Cell cell)
    {
        if (!Eic.HasValidFormat(cell.Value)) Add(CheckCodes.EicFormat, cell, cell.Value);
        else if (!Eic.IsValid(cell.Value)) Add(CheckCodes.EicChecksum, cell, cell.Value);
    }

    private void CheckIban(Cell cell)
    {
        string compact = Cells.Compact(cell.Value);
        if (Cells.IbanShape().IsMatch(compact) && !Cells.IsIbanValid(compact)) Add(CheckCodes.IbanChecksum, cell, cell.Value);
    }

    private void CheckDate(Cell cell)
    {
        if (Cells.IsExcelDateSerial(cell.Value) && ColumnKinds.IsDateHeader(names[cell.Column!.Value]))
        {
            Add(CheckCodes.ExcelDateSerial, cell, cell.Value);
            return;
        }
        if (Dates.PatternOf(cell.Value) is not DatePattern pattern) return;
        if (!Dates.IsReal(cell.Value, pattern)) Add(CheckCodes.DateInvalid, cell, cell.Value);
        else if (pattern != usualDate[cell.Column!.Value]) Add(CheckCodes.DateMixedFormat, cell, cell.Value);
    }

    private void CheckNumber(Cell cell)
    {
        if (Cells.Exponent().IsMatch(cell.Value))
        {
            Add(CheckCodes.ExcelExponent, cell, cell.Value);
            return;
        }
        var separator = Cells.DecimalSeparator(cell.Value);
        if (separator is not null && separator != usualSeparator[cell.Column!.Value])
            Add(CheckCodes.NumberMixedSeparator, cell, cell.Value);
    }

    /// <returns>Чи знайдено в рядку двічі перекодований текст, щоб не писати про нього двічі.</returns>
    private bool CheckText(Cell cell, ColumnKind kind, bool garbledAlready)
    {
        if (kind is not (ColumnKind.Text or ColumnKind.Unknown) || cell.Value.Length == 0) return false;

        if (MixedWords(cell.Value) is { } note) Add(CheckCodes.MixedAlphabet, cell, note: note);

        if (garbledAlready || !decoded.Garbled || !Mojibake.HasGarbledWord(cell.Value)) return false;
        Add(CheckCodes.EncodingMojibake, cell);
        return true;
    }

    // Самого слова не показуємо, бо це текст з файлу: лише яке воно за ліком і які в ньому латинські літери.
    // Словом вважаємо будь-який ряд літер, тож «<b>жирний</b>» це три слова, а не одне змішане
    private static string? MixedWords(string value)
    {
        var words = Words(value);
        var mixed = Enumerable.Range(0, words.Count)
            .Where(i => words[i].Any(c => c is >= 'Ѐ' and <= 'ӿ') && words[i].Any(Names.IsLatin))
            .ToList();
        if (mixed.Count == 0) return null;

        var latin = mixed.SelectMany(i => words[i].Where(Names.IsLatin)).Distinct().Order();
        string which = mixed.Count == 1 ? "слово" : "слова";
        return $"{which} {string.Join(", ", mixed.Select(i => i + 1))}, латинські літери: {string.Join(" ", latin)}";
    }

    private static List<string> Words(string value)
    {
        var words = new List<string>();
        int start = -1;
        for (int i = 0; i <= value.Length; i++)
        {
            bool letter = i < value.Length && char.IsLetter(value[i]);
            if (letter && start < 0) start = i;
            if (!letter && start >= 0)
            {
                words.Add(value[start..i]);
                start = -1;
            }
        }
        return words;
    }

    private void CheckColumns()
    {
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int c = 0; c < width; c++)
        {
            if (empty[c]) findings.Add(new(CheckCodes.EmptyColumns, 0, c));
            if (hasHeader && names[c].Length == 0 && !empty[c]) findings.Add(new(CheckCodes.HeaderEmpty, headerLine, c));
            if (hasHeader && names[c].Length > 0 && !seenNames.Add(names[c]))
                findings.Add(new(CheckCodes.HeaderDuplicate, headerLine, c));
            if (kinds[c] == ColumnKind.Date && usualDate[c] is DatePattern.Dmy or DatePattern.DmyWithTime)
                findings.Add(new(CheckCodes.DateNotIso, 0, c));
        }
    }
}
