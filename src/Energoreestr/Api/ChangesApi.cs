using Energoreestr.Data;
using Microsoft.EntityFrameworkCore;

namespace Energoreestr.Api;

public record ChangeItem(
    string Dataset, string FileName, DateTime PublishedAt,
    string Key, string Name, ChangeKind Kind,
    string? Field, string? OldValue, string? NewValue);

public static class ChangesApi
{
    public static void MapChanges(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/changes", List)
            .WithTags("Зміни")
            .WithSummary("Що з'явилося, зникло або змінилося в реєстрах від файлу до файлу");
    }

    private static async Task<Page<ChangeItem>> List(
        RegistryDb db, string? dataset, string? key, ChangeKind? kind, DateOnly? since,
        int? page, int? pageSize, CancellationToken ct)
    {
        var query = db.Changes.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(dataset))
            query = query.Where(c => c.Dataset == dataset);
        if (!string.IsNullOrWhiteSpace(key))
            query = query.Where(c => c.Key == key);
        if (kind is not null)
            query = query.Where(c => c.Kind == kind);
        if (since is not null)
        {
            var from = since.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(c => c.Snapshot!.PublishedAt >= from);
        }

        return await query
            .OrderByDescending(c => c.Snapshot!.PublishedAt).ThenBy(c => c.Id)
            .Select(c => new ChangeItem(
                c.Dataset, c.Snapshot!.FileName, c.Snapshot.PublishedAt,
                c.Key, c.Name, c.Kind, c.Field, c.OldValue, c.NewValue))
            .ToPageAsync(page, pageSize, ct);
    }
}
