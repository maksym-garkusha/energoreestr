using System.Text;

namespace Energoreestr.Check;

/// <summary>Звіт для людини: у терміналі (п'ять прикладів на вид) і у файлі, який зберігає сторінка.</summary>
public static class ReportText
{
    // скільки пунктів показувати під кожним видом зауважень у терміналі, як у звіті про якість
    public const int Examples = 5;

    private static readonly (Level Level, string Title)[] Sections =
    [
        (Level.Fix, "Поправити: без цього програми прочитають файл не так, як задумано"),
        (Level.Verify, "Варто звірити: може бути помилкою, вирішуєте ви"),
        (Level.Info, "До відома: не помилка, але корисно знати")
    ];

    public static string Write(CheckReport report, IReadOnlyDictionary<string, CodeInfo> codes, int examples = Examples)
    {
        var text = new StringBuilder();
        if (report.Problem is not null)
            return text.AppendLine(report.Problem).ToString();

        if (report.File is { } file) WriteProfile(text, file, report.Rows, report.Findings.Any(f => f.Code == CheckCodes.SingleColumn));
        else text.AppendLine($"Рядків з даними: {report.Rows}.");

        text.AppendLine($"Поправити: {ReportGroups.Count(report, codes, Level.Fix)}, " +
            $"варто звірити: {ReportGroups.Count(report, codes, Level.Verify)}, " +
            $"до відома: {ReportGroups.Count(report, codes, Level.Info)}.");
        if (report.PersonalDataRows > 0)
            text.AppendLine($"Рядків з ознаками персональних даних: {report.PersonalDataRows}. Значення з них не показано.");

        var groups = ReportGroups.Build(report, codes);
        if (groups.Count == 0)
            text.AppendLine().AppendLine("Зауважень немає.");

        foreach (var (level, title) in Sections)
        {
            var inSection = groups.Where(g => g.Info.Level == level).ToList();
            if (inSection.Count == 0) continue;
            text.AppendLine().AppendLine(title);
            foreach (var group in inSection)
                foreach (var line in Lines(group, report.File, examples))
                    text.AppendLine(line);
        }
        return text.ToString();
    }

    private static void WriteProfile(StringBuilder text, FileProfile file, int rows, bool singleColumn)
    {
        // коли колонка одна, знака між колонками немає, і кома тут була б вигадкою
        text.AppendLine(singleColumn
            ? $"Кодування: {EncodingName(file.Encoding)}."
            : $"Кодування: {EncodingName(file.Encoding)}. Знак між колонками: {DelimiterName(file.Delimiter)}.");
        text.AppendLine(file.HasHeader
            ? $"Рядків з даними: {rows}, колонок: {file.Columns.Count}."
            : $"Рядків з даними: {rows}, колонок: {file.Columns.Count}, рядка з назвами колонок немає.");
    }

    /// <summary>Назва виду з кількістю, пояснення і пункти: «рядок 5, колонка 2: «3361661»» або проміжки рядків.</summary>
    public static List<string> Lines(ReportGroup group, FileProfile? file, int examples)
    {
        var lines = new List<string> { "  " + Heading(group) };
        if (group.Info.Title is not null) lines.Add("    " + group.Info.Text);

        var wholeFile = group.Findings.Where(f => f.Line == 0 && f.Column is not null).Select(f => f.Column!.Value).Distinct().ToList();
        if (wholeFile.Count > 0)
            lines.Add($"    {(wholeFile.Count == 1 ? "колонка" : "колонки")} {ReportGroups.Ranges(wholeFile)}");

        var places = group.Findings.Where(f => f.Line > 0).ToList();
        if (CheckCodes.CountedByColumn.Contains(group.Code))
        {
            foreach (var column in places.GroupBy(f => f.Column).OrderBy(g => g.Key))
                lines.Add($"    {(column.Key is int c ? ColumnName(c, file) : "колонку не визначено")}: {Count(column.Count(), Unit.Cell)}");
            return lines;
        }

        var shown = places.Take(examples).ToList();
        // без значень сусідні рядки зводимо в проміжки: «рядки 2-31» читається легше за тридцять рядків
        if (shown.All(f => f.Value is null && f.Note is null))
            lines.AddRange(Spans(shown, file));
        else
            lines.AddRange(shown.Select(f => "    " + Describe(f, file)));

        int rest = group.Total - shown.Count - (group.Findings.Count - places.Count);
        if (rest > 0) lines.Add($"    і ще {rest}");
        return lines;
    }

    private static string Heading(ReportGroup group)
    {
        string title = group.Info.Title ?? group.Info.Text;
        return group.Info.Unit == Unit.File ? title : $"{title}: {Count(group.Total, group.Info.Unit)}";
    }

    private static IEnumerable<string> Spans(List<Finding> findings, FileProfile? file) =>
        findings.GroupBy(f => f.Column).OrderBy(g => g.Key).Select(g =>
        {
            var rows = g.Select(f => f.Line).Distinct().ToList();
            string what = $"{(rows.Count == 1 ? "рядок" : "рядки")} {Ranges(rows)}";
            return g.Key is int c ? $"    {ColumnName(c, file)}: {what}" : $"    {what}";
        });

    private static string Ranges(List<int> rows) => ReportGroups.Ranges(rows.Select(r => r - 1));

    public static string Describe(Finding finding, FileProfile? file)
    {
        var where = new StringBuilder($"рядок {finding.Line}");
        if (finding.Column is int column) where.Append(", ").Append(ColumnName(column, file));
        // у лапках, щоб було видно, де значення закінчується: крапка в «35546472.» теж його частина
        if (finding.Value is not null) where.Append($": «{Visible(finding.Value)}»");
        if (finding.Note is not null) where.Append($" ({Visible(finding.Note)})");
        return where.ToString();
    }

    private static string ColumnName(int column, FileProfile? file)
    {
        string? name = file?.HasHeader == true && column < file.Columns.Count ? file.Columns[column].Name : null;
        return string.IsNullOrEmpty(name) ? $"колонка {column + 1}" : $"колонка {column + 1} «{Visible(name)}»";
    }

    // 1 рядок, 2 рядки, 5 рядків, 11 рядків, 21 рядок
    public static string Count(int count, Unit unit)
    {
        int lastTwo = count % 100, last = count % 10;
        int form = lastTwo is >= 11 and <= 14 ? 2 : last == 1 ? 0 : last is >= 2 and <= 4 ? 1 : 2;
        string[] words = unit switch
        {
            Unit.Cell => ["клітинка", "клітинки", "клітинок"],
            Unit.Column => ["колонка", "колонки", "колонок"],
            _ => ["рядок", "рядки", "рядків"]
        };
        return $"{count} {words[form]}";
    }

    public static string EncodingName(string encoding) => encoding switch
    {
        "utf-8-bom" => "UTF-8 з позначкою BOM",
        "utf-8" => "UTF-8",
        "windows-1251" => "Windows-1251",
        "utf-16le" or "utf-16be" => "UTF-16",
        _ => encoding
    };

    public static string DelimiterName(char delimiter) => delimiter switch
    {
        ',' => "кома",
        ';' => "крапка з комою",
        '\t' => "табуляція",
        '|' => "вертикальна риска",
        _ => $"«{delimiter}»"
    };

    /// <summary>
    /// Текст з файлу в одному рядку і без керівних знаків: перенесення ламали б звіт, а знаки
    /// на кшталт ESC чи U+202E (зміна напрямку письма) могли б підмінити те, що бачить людина.
    /// Такі знаки показуємо записом \uXXXX.
    /// </summary>
    public static string Visible(string value)
    {
        var text = new StringBuilder(value.Length);
        foreach (char c in value.ReplaceLineEndings(" "))
        {
            var category = char.GetUnicodeCategory(c);
            bool hidden = c != '\t' && category is System.Globalization.UnicodeCategory.Control or System.Globalization.UnicodeCategory.Format;
            if (hidden) text.Append($"\\u{(int)c:X4}");
            else text.Append(c);
        }
        return text.ToString();
    }
}
