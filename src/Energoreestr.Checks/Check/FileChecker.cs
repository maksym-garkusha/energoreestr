using Energoreestr.Codes;
using Energoreestr.Csv;
using Energoreestr.Registers;
using Energoreestr.Text;

namespace Energoreestr.Check;

/// <summary>Один прохід перевірки одного файлу. Стан тримаємо в полях, щоб методи лишалися малими.</summary>
internal sealed class FileChecker(Decoded decoded, DateOnly today)
{
    // довших значень не буває навіть в адресах; таке поле означає зламаний рядок
    private const int MaxField = 100_000;
    // скільки рядків з однаковою кількістю полів досить, щоб судити про колонки лише за ними
    private const int MinAlignedRows = 5;
    // більше колонок буває лише тоді, коли знак між колонками визначено неправильно
    public const int MaxColumns = 1000;
    // на кожен вид зауважень зберігаємо стільки, далі лише рахуємо: інакше великий файл з'їсть пам'ять
    public const int MaxPerCode = 1000;
    // назва колонки довша за це вже не назва, а дані
    private const int MaxHeaderCell = 200;
    // у звіті назви колонок обрізаємо, щоб довга назва не забила екран
    private const int MaxName = 60;

    private readonly string text = decoded.Text!;
    private readonly List<Finding> findings = [];
    private readonly Dictionary<string, int> totals = [];
    private readonly HashSet<string> seen = [];
    // записи зі зламаними лапками чи надто довгим полем: їх не перевіряємо зовсім
    private readonly HashSet<CsvRecord> broken = [];

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
        foreach (var finding in decoded.Findings) Note(finding);
        var records = Read();
        if (records.Count == 0) return CheckReport.Unreadable(FileCheck.Empty);
        if (records.Any(r => r.Fields.Length > MaxColumns)) return CheckReport.Unreadable(FileCheck.TooManyColumns);

        ReadHeader(records);
        if (data.Count == 0) return CheckReport.Unreadable(FileCheck.HeaderOnly);
        DescribeColumns();
        foreach (var row in data) CheckRow(row);
        CheckColumns();

        var columns = Enumerable.Range(0, width).Select(c => new ColumnProfile(c, Shorten(names[c]), kinds[c], filled[c])).ToList();
        var profile = new FileProfile(decoded.Encoding, delimiter, hasHeader, columns);
        return new CheckReport(data.Count, findings, personalRows, profile, Totals: totals);
    }

    private static string Shorten(string name) => name.Length <= MaxName ? name : name[..(MaxName - 1)] + "…";

    // кожне зауваження проходить тут: перші MaxPerCode зберігаємо, решту лише рахуємо
    private void Note(Finding finding)
    {
        int count = totals.GetValueOrDefault(finding.Code) + 1;
        totals[finding.Code] = count;
        if (count <= MaxPerCode) findings.Add(finding);
    }

    private List<CsvRecord> Read()
    {
        char? detected = Delimiters.Detect(text);
        if (detected is null) Note(new(CheckCodes.SingleColumn, 0));
        delimiter = detected ?? ',';

        var records = CsvReader.Read(text, delimiter, out int? unclosed);
        foreach (var record in records)
        {
            if (record.Line == unclosed && record == records[^1])
            {
                Note(new(CheckCodes.UnclosedQuote, record.Row));
                broken.Add(record);
            }
            else if (record.Fields.Any(f => f.Length > MaxField))
            {
                Note(new(CheckCodes.ValueTooLong, record.Row));
                broken.Add(record);
            }
        }
        return records;
    }

    private void ReadHeader(List<CsvRecord> records)
    {
        var first = records[0];
        width = first.Fields.Length;
        headerLine = first.Row;
        names = [.. first.Fields.Select(f => Cells.Clean(f))];
        data = records.Skip(1).ToList();
        hasHeader = true;
        kinds = GuessKinds();

        if (!FirstRowLooksLikeData(first) && !HeaderLooksPersonal(first) && first.Fields.All(f => f.Length <= MaxHeaderCell)
            && !(NoTypedColumns() && FirstRowLooksLikeTheRest(first)))
            return;

        // назв колонок тоді не показуємо ніде, а перший рядок перевіряємо як дані
        Note(new(CheckCodes.HeaderMissing, first.Row));
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
            if (FitsKind(Cells.Clean(first.Fields[c]), kinds[c])) fits++;
        }
        return typed > 0 && fits * 2 >= typed;
    }

    private bool NoTypedColumns() => kinds.All(k => k is ColumnKind.Unknown or ColumnKind.Text);

    // ті самі ознаки персональних даних, що й для рядків з даними
    private bool HeaderLooksPersonal(CsvRecord first) =>
        first.Fields.Select(f => Cells.Clean(f)).Where(v => v.Length > 0).Any(v =>
            SoleProprietor.IsNamedAsOne(v) || Cells.IsPlausibleTaxNumber(v, today) || Cells.ContainsTaxNumber(v, today))
        || Enumerable.Range(0, width).Any(c => kinds[c] == ColumnKind.Edrpou && SoleProprietor.IsTaxNumber(first.Fields[c]));

    // Для таблиці з самого тексту: перший рядок схожий на решту, якщо в половині колонок,
    // де значення мають «@», кому чи крапку, ці самі знаки є і в першому рядку. Справжні
    // назви колонок («Адреса», «Email») таких знаків майже не мають
    private bool FirstRowLooksLikeTheRest(CsvRecord first)
    {
        var rows = SampleRows();
        int marked = 0, same = 0;
        for (int c = 0; c < width; c++)
        {
            var values = Values(rows, c);
            if (values.Count == 0) continue;
            string usual = values.GroupBy(Marks).OrderByDescending(g => g.Count()).First().Key;
            if (usual.Length == 0) continue;
            marked++;
            if (Marks(Cells.Clean(first.Fields[c])) == usual) same++;
        }
        return marked > 0 && same * 2 >= marked;
    }

    private static string Marks(string value) =>
        (value.Contains('@') ? "@" : "") + (value.Contains(',') ? "," : "") + (value.Contains('.') ? "." : "");

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
        var rows = data.Where(r => !IsSection(r) && !broken.Contains(r)).ToList();
        var aligned = rows.Where(IsAligned).ToList();
        return aligned.Count >= MinAlignedRows ? aligned : rows;
    }

    private static List<string> Values(IEnumerable<CsvRecord> rows, int column) =>
        rows.Where(r => column < r.Fields.Length)
            .Select(r => Cells.Clean(r.Fields[column]))
            // заглушки й помилки формул не кажуть, що в колонці, тож у визначенні вигляду їх не рахуємо
            .Where(v => v.Length > 0 && !Cells.IsPlaceholder(v) && !Cells.ExcelError().IsMatch(v))
            .ToList();

    private void DescribeColumns()
    {
        var rows = data.Where(r => !IsSection(r) && IsAligned(r) && !broken.Contains(r)).ToList();
        empty = new bool[width];
        codeLike = new bool[width];
        phone = new bool[width];
        usualDate = new DatePattern?[width];
        usualSeparator = new char?[width];

        for (int c = 0; c < width; c++)
        {
            var values = Values(rows, c);
            empty[c] = names[c].Length == 0 && rows.All(r => c >= r.Fields.Length || Cells.Clean(r.Fields[c]).Length == 0);
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
        row.Fields.Length <= width || row.Fields.Skip(width).All(f => Cells.Clean(f).Length == 0);

    // рядок-заголовок посеред таблиці: заповнена одна клітинка з трьох і більше
    private bool IsSection(CsvRecord row) =>
        width >= 3 && row.Fields.Count(f => Cells.Clean(f).Length > 0) == 1;

    private void CheckRow(CsvRecord row)
    {
        if (broken.Contains(row)) return;
        if (IsSection(row))
        {
            Note(new(CheckCodes.SectionRows, row.Row));
            // у такому рядку теж буває ім'я підприємця
            MarkPersonal(row, aligned: true);
            return;
        }

        bool aligned = IsAligned(row);
        if (!aligned) Note(new(CheckCodes.UnquotedDelimiter, row.Row));
        else if (row.Fields.Length < width) Note(new(CheckCodes.MissingFields, row.Row));

        if (!seen.Add(string.Join('\u001f', row.Fields.Select(f => Cells.Clean(f)))))
            Note(new(CheckCodes.DuplicateRow, row.Row));

        bool personal = MarkPersonal(row, aligned);
        bool garbled = false;
        int count = aligned ? Math.Min(row.Fields.Length, width) : row.Fields.Length;
        for (int c = 0; c < count; c++)
        {
            if (aligned && empty[c]) continue;
            var cell = new Cell(row.Row, aligned ? c : null, row.Fields[c], personal);
            if (aligned) CheckAlignedCell(cell, kinds[c]);
            else CheckLooseCell(cell);
            garbled |= CheckText(cell, aligned ? kinds[c] : ColumnKind.Unknown, garbled);
        }
    }

    /// <param name="Column">null, якщо значення не на своєму місці і колонку не визначити.</param>
    private record Cell(int Line, int? Column, string Raw, bool Personal)
    {
        public string Value { get; } = Cells.Clean(Raw);
    }

    private void Add(string code, Cell cell, string? value = null, string? note = null) =>
        Note(new(code, cell.Line, cell.Column, cell.Personal || value is null || !Cells.IsDisplayable(value) ? null : value, note));

    // У рядку з ознаками персональних даних значень не показуємо взагалі: ні кодів, ні назв
    private bool MarkPersonal(CsvRecord row, bool aligned)
    {
        int? soleProprietor = null, taxNumber = null;
        for (int c = 0; c < row.Fields.Length; c++)
        {
            string value = Cells.Clean(row.Fields[c]);
            if (value.Length == 0) continue;
            soleProprietor ??= SoleProprietor.IsNamedAsOne(value) ? c : null;
            taxNumber ??= LooksLikeTaxNumber(value, aligned && c < width ? c : null) ? c : null;
        }

        if (soleProprietor is int s) Note(new(CheckCodes.PersonalSoleProprietor, row.Row, aligned ? s : null));
        if (taxNumber is int t) Note(new(CheckCodes.PersonalTaxNumber, row.Row, aligned ? t : null));
        bool personal = soleProprietor is not null || taxNumber is not null;
        if (personal) personalRows++;
        return personal;
    }

    // У колонці кодів десять цифр означають РНОКПП, хоч би як їх записали: це правило імпорту.
    // Деінде вимагаємо ще й контрольну цифру і правдоподібну дату народження, бо там
    // десять цифр бувають і в номерах документів
    private bool LooksLikeTaxNumber(string value, int? column)
    {
        if (column is int c && codeLike[c]) return SoleProprietor.IsTaxNumber(value) || Cells.ContainsTaxNumber(value, today);
        if (column is int other && (phone[other] || kinds[other] is not (ColumnKind.Text or ColumnKind.Unknown))) return false;
        // і окреме число, і номер серед тексту: «РНОКПП 3012345678», «3012345678 (Петренко)»
        return Cells.IsPlausibleTaxNumber(value, today) || Cells.ContainsTaxNumber(value, today);
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
            if (empty[c]) Note(new(CheckCodes.EmptyColumns, 0, c));
            if (hasHeader && names[c].Length == 0 && !empty[c]) Note(new(CheckCodes.HeaderEmpty, headerLine, c));
            if (hasHeader && names[c].Length > 0 && !seenNames.Add(names[c]))
                Note(new(CheckCodes.HeaderDuplicate, headerLine, c));
            if (kinds[c] == ColumnKind.Date && usualDate[c] is DatePattern.Dmy or DatePattern.DmyWithTime)
                Note(new(CheckCodes.DateNotIso, 0, c));
        }
    }
}
