using Energoreestr.Data;
using Energoreestr.Import;
using Microsoft.EntityFrameworkCore;

namespace Energoreestr.Api;

/// <param name="ExpectedEveryDays">Заявлена на порталі періодичність оновлення, у днях.</param>
/// <param name="DaysSinceUpdate">Скільки днів минуло від останнього оновлення файлу.</param>
/// <param name="Overdue">true, якщо файл не оновлювали довше, ніж обіцяє періодичність.</param>
public record DatasetStatus(
    string Dataset, string Title, string PortalUrl,
    string? UpdateFrequency, int? ExpectedEveryDays,
    string? FileName, DateTime? FileModifiedAt, int? DaysSinceUpdate, bool? Overdue,
    DateTime CheckedAt, int Snapshots, int Records);

public static class DatasetsApi
{
    public static void MapDatasets(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/datasets", List)
            .WithTags("Набори даних")
            .WithSummary("Які реєстри імпортуються і наскільки свіжі файли на порталі");
    }

    private static async Task<List<DatasetStatus>> List(RegistryDb db, CancellationToken ct)
    {
        var states = await db.DatasetStates.AsNoTracking().ToListAsync(ct);
        var snapshotCounts = await db.Snapshots.AsNoTracking()
            .GroupBy(s => s.Dataset)
            .Select(g => new { Dataset = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Dataset, g => g.Count, ct);

        int participants = await db.Participants.CountAsync(ct);
        var monopolies = await db.Monopolies.AsNoTracking()
            .GroupBy(m => m.Dataset)
            .Select(g => new { Dataset = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Dataset, g => g.Count, ct);

        var now = DateTime.UtcNow;
        var result = new List<DatasetStatus>();

        foreach (var def in Datasets.All)
        {
            var state = states.FirstOrDefault(s => s.Dataset == def.Key);
            if (state is null) continue;   // набір ще жодного разу не перевіряли

            int? expected = Datasets.ExpectedDays(state.UpdateFrequency);
            int? age = state.ResourceModifiedAt is { } modified ? (int)(now - modified).TotalDays : null;
            bool? overdue = expected is not null && age is not null ? age > expected : null;

            int records = def.Kind == RegisterKind.Wholesale
                ? participants
                : monopolies.GetValueOrDefault(def.Key);

            result.Add(new DatasetStatus(
                def.Key, state.Title, $"https://data.gov.ua/dataset/{def.PackageId}",
                state.UpdateFrequency, expected,
                state.ResourceName, state.ResourceModifiedAt, age, overdue,
                state.CheckedAt, snapshotCounts.GetValueOrDefault(def.Key), records));
        }
        return result;
    }
}
