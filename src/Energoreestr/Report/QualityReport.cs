using System.Text;
using Energoreestr.Api;
using Energoreestr.Data;
using Energoreestr.Import;
using Energoreestr.Registers;
using Microsoft.EntityFrameworkCore;

namespace Energoreestr.Report;

/// <summary>Збирає з бази звіт про якість останніх файлів у форматі Markdown.</summary>
public static class QualityReport
{
    // скільки прикладів показувати під кожним видом зауважень
    private const int Examples = 5;

    public static async Task<string> BuildAsync(RegistryDb db, CancellationToken ct = default)
    {
        var snapshots = await QualityApi.LatestSnapshots(db, ct);
        var states = await db.DatasetStates.AsNoTracking().ToDictionaryAsync(s => s.Dataset, ct);
        var issues = await db.Issues.AsNoTracking().OrderBy(i => i.Line).ToListAsync(ct);

        var md = new StringBuilder();
        md.AppendLine("# Якість даних у реєстрах НКРЕКП");
        md.AppendLine();
        md.AppendLine($"Звіт від {DateTime.UtcNow:yyyy-MM-dd}. Зібрано командою `dotnet run -- report` з останніх файлів,");
        md.AppendLine("опублікованих на data.gov.ua. Номери рядків відповідають рядкам у цих файлах.");
        md.AppendLine();

        md.AppendLine("| Реєстр | Файл | Оновлено на порталі | Записів у файлі | Зауважень |");
        md.AppendLine("|---|---|---|---|---|");
        foreach (var s in snapshots)
        {
            string title = states.TryGetValue(s.Dataset, out var state) ? state.Title : s.Dataset;
            md.AppendLine($"| {title} | `{s.FileName}` | {s.PublishedAt:yyyy-MM-dd} | {s.TotalRows} | {s.IssueCount} |");
        }
        md.AppendLine();

        foreach (var s in snapshots)
        {
            string title = states.TryGetValue(s.Dataset, out var state) ? state.Title : s.Dataset;
            md.AppendLine($"## {title}");
            md.AppendLine();

            if (state is not null && Datasets.ExpectedDays(state.UpdateFrequency) is { } expected)
            {
                int age = (int)(DateTime.UtcNow - s.PublishedAt).TotalDays;
                md.AppendLine($"Заявлена періодичність оновлення: раз на {expected} дн. Файлу на день звіту {age} дн.");
                md.AppendLine();
            }

            if (s.ImportedRows != s.TotalRows)
            {
                md.AppendLine($"До бази потрапило {s.ImportedRows} записів із {s.TotalRows}: решта це повтори або рядки, які не вдалося прочитати.");
                md.AppendLine();
            }

            var groups = issues
                .Where(i => i.Dataset == s.Dataset)
                .GroupBy(i => i.Code)
                .OrderByDescending(g => g.Count())
                .ToList();

            if (groups.Count == 0)
            {
                md.AppendLine("Зауважень немає.");
                md.AppendLine();
                continue;
            }

            foreach (var group in groups)
            {
                string description = IssueCodes.Descriptions.GetValueOrDefault(group.Key, group.Key);
                md.AppendLine($"**{description}**: {group.Count()}");
                md.AppendLine();
                foreach (var issue in group.Take(Examples))
                {
                    string value = issue.Value is null ? "" : $": `{issue.Value.ReplaceLineEndings(" ")}`";
                    md.AppendLine($"- рядок {issue.Line}, {issue.Key}{value}");
                }
                if (group.Count() > Examples)
                    md.AppendLine($"- і ще {group.Count() - Examples}");
                md.AppendLine();
            }
        }

        return md.ToString();
    }
}
