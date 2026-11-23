namespace Energoreestr.Check;

/// <summary>Зауваження одного виду, як їх показують людині.</summary>
/// <param name="Total">Скільки всього таких зауважень; збережено може бути менше.</param>
public record ReportGroup(string Code, CodeInfo Info, IReadOnlyList<Finding> Findings, int Total)
{
    public bool Truncated => Total > Findings.Count;
}

public static class ReportGroups
{
    /// <summary>Спершу те, що треба поправити; всередині рівня за причиною, як у таблиці кодів.</summary>
    public static List<ReportGroup> Build(CheckReport report, IReadOnlyDictionary<string, CodeInfo> codes) =>
        report.Findings
            .GroupBy(f => f.Code)
            .Select(g => new ReportGroup(g.Key, codes[g.Key], g.OrderBy(f => f.Line).ThenBy(f => f.Column).ToList(), report.Total(g.Key)))
            .OrderBy(g => g.Info.Level)
            .ThenBy(g => g.Info.Order)
            .ThenBy(g => g.Code, StringComparer.Ordinal)
            .ToList();

    /// <summary>Скільки видів зауважень цього рівня: саме це людина бачить у підсумку.</summary>
    public static int Count(CheckReport report, IReadOnlyDictionary<string, CodeInfo> codes, Level level) =>
        report.Findings.Select(f => f.Code).Distinct().Count(code => codes[code].Level == level);

    /// <summary>Скільки окремих знахідок цього рівня, разом з тими, що не збережено.</summary>
    public static int Findings(CheckReport report, IReadOnlyDictionary<string, CodeInfo> codes, Level level) =>
        report.Findings.Select(f => f.Code).Distinct().Where(code => codes[code].Level == level).Sum(report.Total);

    /// <summary>
    /// 0, якщо поправляти нічого; 1, якщо є що поправити (зі strict також що звірити);
    /// 2, якщо файл не вдалося прочитати. Як у grep, щоб зручно було в автоматизації.
    /// </summary>
    public static int ExitCode(CheckReport report, IReadOnlyDictionary<string, CodeInfo> codes, bool strict)
    {
        if (report.Problem is not null) return 2;
        bool fail = report.Findings.Any(f => codes[f.Code].Level == Level.Fix
            || (strict && codes[f.Code].Level == Level.Verify));
        return fail ? 1 : 0;
    }

    /// <summary>Номери колонок від одиниці, сусідні злиті в проміжки: «8-74».</summary>
    public static string Ranges(IEnumerable<int> zeroBased)
    {
        var parts = new List<string>();
        int? start = null, prev = null;
        foreach (int n in zeroBased.Select(c => c + 1).Distinct().Order())
        {
            if (prev == n - 1)
            {
                prev = n;
                continue;
            }
            if (start is not null) parts.Add(Range(start.Value, prev!.Value));
            start = prev = n;
        }
        if (start is not null) parts.Add(Range(start.Value, prev!.Value));
        return string.Join(", ", parts);
    }

    private static string Range(int from, int to) => from == to ? $"{from}" : $"{from}-{to}";
}
