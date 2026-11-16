using Energoreestr.Codes;
using Energoreestr.Data;
using Energoreestr.Text;
using Microsoft.EntityFrameworkCore;

namespace Energoreestr.Api;

/// <summary>Усе, що є про компанію в різних реєстрах, зібране за кодом ЄДРПОУ.</summary>
public record CompanyCard(
    string Edrpou,
    bool ChecksumValid,
    string? Name,
    List<Participant> WholesaleMarket,
    List<Monopoly> Monopolies,
    List<Issue> Issues);

public static class CompaniesApi
{
    public static void MapCompanies(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/companies/{edrpou}", GetCard)
            .WithTags("Компанії")
            .WithSummary("Картка компанії за ЄДРПОУ: записи з усіх реєстрів і зауваження до них");

        app.MapGet("/api/monopolies", SearchMonopolies)
            .WithTags("Природні монополії")
            .WithSummary("Пошук у реєстрах природних монополій");
    }

    private static async Task<IResult> GetCard(RegistryDb db, string edrpou, CancellationToken ct)
    {
        string? code = Edrpou.Normalize(edrpou);
        if (code is null)
            return Results.BadRequest(new { error = "ЄДРПОУ це до 8 цифр" });

        var participants = await db.Participants.AsNoTracking()
            .Where(p => p.IdCode == code && p.Country == "Україна")
            .ToListAsync(ct);
        var monopolies = await db.Monopolies.AsNoTracking()
            .Where(m => m.Edrpou == code)
            .OrderBy(m => m.Dataset).ThenBy(m => m.Activity)
            .ToListAsync(ct);

        if (participants.Count == 0 && monopolies.Count == 0)
            return Results.NotFound();

        // зауваження в одних реєстрах прив'язані до коду ECRB, в інших до ЄДРПОУ,
        // причому до ЄДРПОУ в тому вигляді, як він у файлі (може бути без нулів)
        var keys = participants.Select(p => p.EcrbCode)
            .Append(code)
            .Append(code.TrimStart('0'))
            .ToList();
        var issues = await db.Issues.AsNoTracking()
            .Where(i => keys.Contains(i.Key))
            .ToListAsync(ct);

        string? name = participants.FirstOrDefault()?.Name ?? monopolies.FirstOrDefault()?.Name;

        return Results.Ok(new CompanyCard(code, Edrpou.IsValid(code), name, participants, monopolies, issues));
    }

    private static async Task<Page<Monopoly>> SearchMonopolies(
        RegistryDb db, string? q, string? dataset, string? activity, int? page, int? pageSize, CancellationToken ct)
    {
        var query = db.Monopolies.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(dataset))
            query = query.Where(m => m.Dataset == dataset);

        if (!string.IsNullOrWhiteSpace(q))
        {
            string term = q.Trim();
            if (term.All(char.IsAsciiDigit))
            {
                string? padded = Edrpou.Normalize(term);
                query = query.Where(m => m.Edrpou == padded);
            }
            else
            {
                string key = Names.SearchKey(term);
                query = query.Where(m => m.SearchName.Contains(key));
            }
        }

        if (!string.IsNullOrWhiteSpace(activity))
        {
            // види діяльності у файлах великими літерами
            string upper = activity.Trim().ToUpperInvariant();
            query = query.Where(m => m.Activity.Contains(upper));
        }

        return await query
            .OrderBy(m => m.Name).ThenBy(m => m.Activity)
            .ToPageAsync(page, pageSize, ct);
    }
}
