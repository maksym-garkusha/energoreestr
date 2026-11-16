using Energoreestr.Codes;
using Energoreestr.Data;
using Energoreestr.Registers;
using Microsoft.EntityFrameworkCore;

namespace Energoreestr.Api;

public record IssueCount(string Code, string Description, int Count);

public record DatasetQuality(
    string Dataset, string FileName, DateTime PublishedAt,
    int TotalRows, int ImportedRows, List<IssueCount> Issues);

public record CodeCheck(string Code, string Normalized, bool Valid, string? Note);

public static class QualityApi
{
    public static void MapQuality(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/quality").WithTags("Якість даних");

        group.MapGet("/", Summary)
            .WithSummary("Скільки і яких зауважень до останнього файлу кожного реєстру");
        group.MapGet("/issues", Issues)
            .WithSummary("Самі зауваження: рядок файлу, запис і значення");

        var check = app.MapGroup("/api/check").WithTags("Перевірка кодів");
        check.MapGet("/edrpou/{code}", CheckEdrpou)
            .WithSummary("Чи проходить код ЄДРПОУ перевірку контрольного розряду");
        check.MapGet("/eic/{code}", CheckEic)
            .WithSummary("Чи проходить EIC-код перевірку контрольного символу");
    }

    private static async Task<List<DatasetQuality>> Summary(RegistryDb db, CancellationToken ct)
    {
        var counts = await db.Issues.AsNoTracking()
            .GroupBy(i => new { i.Dataset, i.Code })
            .Select(g => new { g.Key.Dataset, g.Key.Code, Count = g.Count() })
            .ToListAsync(ct);

        var result = new List<DatasetQuality>();
        foreach (var snapshot in await LatestSnapshots(db, ct))
        {
            var issues = counts
                .Where(c => c.Dataset == snapshot.Dataset)
                .OrderByDescending(c => c.Count)
                .Select(c => new IssueCount(c.Code, IssueCodes.Descriptions.GetValueOrDefault(c.Code, ""), c.Count))
                .ToList();

            result.Add(new DatasetQuality(
                snapshot.Dataset, snapshot.FileName, snapshot.PublishedAt,
                snapshot.TotalRows, snapshot.ImportedRows, issues));
        }
        return result;
    }

    private static async Task<Page<Issue>> Issues(
        RegistryDb db, string? dataset, string? code, int? page, int? pageSize, CancellationToken ct)
    {
        var query = db.Issues.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(dataset))
            query = query.Where(i => i.Dataset == dataset);
        if (!string.IsNullOrWhiteSpace(code))
            query = query.Where(i => i.Code == code);

        return await query
            .OrderBy(i => i.Dataset).ThenBy(i => i.Line)
            .ToPageAsync(page, pageSize, ct);
    }

    private static IResult CheckEdrpou(string code)
    {
        string? normalized = Edrpou.Normalize(code);
        if (normalized is null)
            return Results.BadRequest(new { error = "ЄДРПОУ це до 8 цифр" });

        string? note = normalized != code ? "доповнено нулями на початку" : null;
        return Results.Ok(new CodeCheck(code, normalized, Edrpou.IsValid(normalized), note));
    }

    private static IResult CheckEic(string code)
    {
        string normalized = code.Trim().ToUpperInvariant();
        if (!Eic.HasValidFormat(normalized))
            return Results.Ok(new CodeCheck(code, normalized, false, "має бути 16 символів: цифри, латинські літери, дефіс"));

        return Results.Ok(new CodeCheck(code, normalized, Eic.IsValid(normalized), null));
    }

    /// <summary>Останній знімок кожного набору.</summary>
    public static async Task<List<Snapshot>> LatestSnapshots(RegistryDb db, CancellationToken ct)
    {
        var all = await db.Snapshots.AsNoTracking().ToListAsync(ct);
        return all
            .GroupBy(s => s.Dataset)
            .Select(g => g.MaxBy(s => s.PublishedAt)!)
            .OrderBy(s => s.Dataset)
            .ToList();
    }
}
