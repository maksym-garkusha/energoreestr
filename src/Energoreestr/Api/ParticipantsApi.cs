using Energoreestr.Codes;
using Energoreestr.Data;
using Energoreestr.Text;
using Microsoft.EntityFrameworkCore;

namespace Energoreestr.Api;

public record ParticipantSummary(
    string EcrbCode, string Name, string Country, string? IdCode, string? Eic,
    DateOnly RegisteredOn, DateOnly? ExcludedOn);

public static class ParticipantsApi
{
    public static void MapParticipants(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/participants").WithTags("Учасники оптового енергоринку");

        group.MapGet("/", Search)
            .WithSummary("Пошук учасників за назвою, ЄДРПОУ, EIC або кодом реєстрації");
        group.MapGet("/{ecrbCode}", Get)
            .WithSummary("Повний запис учасника за кодом реєстрації ECRB");
    }

    private static async Task<Page<ParticipantSummary>> Search(
        RegistryDb db, string? q, string? country, string? status, int? page, int? pageSize, CancellationToken ct)
    {
        var query = db.Participants.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
        {
            string term = q.Trim();
            string upper = term.ToUpperInvariant();

            if (term.All(char.IsAsciiDigit))
            {
                // 3346331 і 03346331 мають знаходити одну компанію
                string? padded = Edrpou.Normalize(term);
                query = query.Where(p => p.IdCode != null && (p.IdCode == padded || p.IdCode.StartsWith(term)));
            }
            else if (upper.StartsWith("ECRB-"))
            {
                query = query.Where(p => p.EcrbCode == upper);
            }
            else if (Eic.HasValidFormat(upper))
            {
                query = query.Where(p => p.Eic == upper);
            }
            else
            {
                // LIKE у SQLite не вміє ігнорувати регістр кирилиці, тому шукаємо
                // по заздалегідь нормалізованій назві
                string key = Names.SearchKey(term);
                query = query.Where(p => p.SearchName.Contains(key));
            }
        }

        if (!string.IsNullOrWhiteSpace(country))
            query = query.Where(p => p.Country == country);

        if (status == "active")
            query = query.Where(p => p.ExcludedOn == null);
        else if (status == "excluded")
            query = query.Where(p => p.ExcludedOn != null);

        return await query
            .OrderBy(p => p.Number)
            .Select(p => new ParticipantSummary(
                p.EcrbCode, p.Name, p.Country, p.IdCode, p.Eic, p.RegisteredOn, p.ExcludedOn))
            .ToPageAsync(page, pageSize, ct);
    }

    private static async Task<IResult> Get(RegistryDb db, string ecrbCode, CancellationToken ct)
    {
        string code = ecrbCode.ToUpperInvariant();
        var participant = await db.Participants.AsNoTracking()
            .FirstOrDefaultAsync(p => p.EcrbCode == code, ct);

        return participant is null ? Results.NotFound() : Results.Ok(participant);
    }
}
