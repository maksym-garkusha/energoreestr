using System.Text;

namespace Energoreestr.Check;

/// <summary>Звіт для людини в терміналі.</summary>
public static class ReportText
{
    // скільки прикладів показувати під кожним видом зауважень, як у звіті про якість
    private const int Examples = 5;

    private static readonly (Level Level, string Title)[] Sections =
        [(Level.Fix, "Поправити"), (Level.Verify, "Варто звірити"), (Level.Info, "До відома")];

    public static string Write(CheckReport report, IReadOnlyDictionary<string, CodeInfo> codes)
    {
        var text = new StringBuilder();
        if (report.Problem is not null)
            return text.AppendLine(report.Problem).ToString();

        if (report.File is { } file) WriteProfile(text, file, report.Rows);
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
            foreach (var group in inSection) WriteGroup(text, group, report.File);
        }
        return text.ToString();
    }

    private static void WriteProfile(StringBuilder text, FileProfile file, int rows)
    {
        text.AppendLine($"Кодування: {EncodingName(file.Encoding)}. Знак між колонками: {DelimiterName(file.Delimiter)}.");
        text.AppendLine(file.HasHeader
            ? $"Рядків з даними: {rows}, колонок: {file.Columns.Count}."
            : $"Рядків з даними: {rows}, колонок: {file.Columns.Count}, рядка з назвами колонок немає.");
    }

    private static void WriteGroup(StringBuilder text, ReportGroup group, FileProfile? file)
    {
        text.AppendLine($"  {group.Info.Text} ({group.Total})");

        var wholeFile = group.Findings.Where(f => f.Line == 0).ToList();
        var columns = wholeFile.Where(f => f.Column is not null).Select(f => f.Column!.Value).Distinct().ToList();
        if (columns.Count > 0)
            text.AppendLine($"    {(columns.Count == 1 ? "колонка" : "колонки")} {ReportGroups.Ranges(columns)}");

        var lines = group.Findings.Where(f => f.Line > 0).ToList();
        if (CheckCodes.CountedByColumn.Contains(group.Code))
        {
            foreach (var column in lines.GroupBy(f => f.Column).OrderBy(g => g.Key))
                text.AppendLine($"    {(column.Key is int c ? ColumnName(c, file) : "колонку не визначено")}: {Cells(column.Count())}");
            return;
        }
        foreach (var finding in lines.Take(Examples))
            text.AppendLine($"    {Describe(finding, file)}");
        int shown = Math.Min(lines.Count, Examples);
        int rest = group.Total - shown - (group.Findings.Count - lines.Count);
        if (rest > 0)
            text.AppendLine($"    і ще {rest}");
    }

    public static string Describe(Finding finding, FileProfile? file)
    {
        var where = new StringBuilder($"рядок {finding.Line}");
        if (finding.Column is int column) where.Append(", ").Append(ColumnName(column, file));
        if (finding.Value is not null) where.Append($": {Visible(finding.Value)}");
        if (finding.Note is not null) where.Append($" ({Visible(finding.Note)})");
        return where.ToString();
    }

    private static string ColumnName(int column, FileProfile? file)
    {
        string? name = file?.HasHeader == true && column < file.Columns.Count ? file.Columns[column].Name : null;
        return string.IsNullOrEmpty(name) ? $"колонка {column + 1}" : $"колонка {column + 1} «{Visible(name)}»";
    }

    // 1 клітинка, 2 клітинки, 5 клітинок, 11 клітинок, 21 клітинка
    private static string Cells(int count)
    {
        int lastTwo = count % 100, last = count % 10;
        string word = lastTwo is >= 11 and <= 14 ? "клітинок"
            : last == 1 ? "клітинка"
            : last is >= 2 and <= 4 ? "клітинки"
            : "клітинок";
        return $"{count} {word}";
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
