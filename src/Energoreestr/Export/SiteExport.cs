using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Energoreestr.Api;
using Energoreestr.Codes;
using Energoreestr.Data;
using Energoreestr.Text;
using Microsoft.EntityFrameworkCore;

namespace Energoreestr.Export;

/// <param name="Key">Назва у вигляді для пошуку, сторінка порівнює з нею запит.</param>
/// <param name="CodeValid">null, якщо коду немає або це не ЄДРПОУ.</param>
/// <param name="Foreign">true, якщо всі записи компанії з іншої країни.</param>
public record SiteCompany(
    string? Code, bool? CodeValid, bool Foreign, string Name, string Key,
    List<SiteParticipant> Wholesale, List<SiteMonopoly> Monopolies, List<SiteIssue> Issues);

public record SiteParticipant(
    string Ecrb, string LegalForm, string Country, string? Settlement, string? Eic,
    DateOnly RegisteredOn, DateOnly? ExcludedOn, string? Website);

public record SiteMonopoly(string Dataset, string Activity, string Territory, string Location);

public record SiteIssue(string Dataset, int Line, string Code, string? Value);

/// <param name="SoleProprietors">Скільки записів про підприємців є в останніх файлах. Самих записів у базі немає.</param>
public record SiteData(
    DateTime GeneratedAt,
    List<DatasetStatus> Datasets,
    List<DatasetQuality> Quality,
    int SoleProprietors,
    List<SiteCompany> Companies);

public record ExportSummary(int Companies, int SoleProprietors);

/// <summary>
/// Складає все, що потрібно сторінці пошуку, в один JSON. Сторінка статична,
/// сервера за нею немає, тому пошук працює в браузері по цьому файлу.
/// </summary>
public static class SiteExport
{
    public const string FileName = "data.json";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<ExportSummary> WriteAsync(RegistryDb db, string directory, CancellationToken ct = default)
    {
        var data = await BuildAsync(db, ct);
        Directory.CreateDirectory(directory);
        await using var file = File.Create(Path.Combine(directory, FileName));
        await JsonSerializer.SerializeAsync(file, data, Json, ct);
        return new ExportSummary(data.Companies.Count, data.SoleProprietors);
    }

    public static async Task<SiteData> BuildAsync(RegistryDb db, CancellationToken ct = default)
    {
        var participants = await db.Participants.AsNoTracking().ToListAsync(ct);
        var monopolies = await db.Monopolies.AsNoTracking().ToListAsync(ct);
        var issues = await db.Issues.AsNoTracking().ToListAsync(ct);

        var groups = new Dictionary<string, Draft>();

        foreach (var p in participants)
        {
            // українські компанії зводимо за ЄДРПОУ, решту лишаємо окремими записами
            // у кількох записах замість ЄДРПОУ стоїть дев'ятизначний податковий номер
            // договору про спільну діяльність, за ним зводити не можна
            bool ukrainian = p.Country == "Україна" && p.IdCode is not null && Edrpou.Normalize(p.IdCode) == p.IdCode;
            string groupKey = ukrainian ? p.IdCode! : "ecrb:" + p.EcrbCode;
            var draft = Get(groups, groupKey, ukrainian ? p.IdCode : null, p.Name);
            draft.Wholesale.Add(new SiteParticipant(
                p.EcrbCode, p.LegalForm, p.Country, p.Settlement, p.Eic,
                p.RegisteredOn, p.ExcludedOn, p.Website));
            draft.IssueKeys.Add(p.EcrbCode);
        }

        foreach (var m in monopolies)
        {
            string groupKey = m.Edrpou ?? "name:" + m.Dataset + ":" + m.SearchName;
            var draft = Get(groups, groupKey, m.Edrpou, m.Name);
            draft.Monopolies.Add(new SiteMonopoly(m.Dataset, m.Activity, m.Territory, m.Location));
            // якщо коду у файлі немає, зауваження записане під назвою
            if (m.Edrpou is null) draft.IssueKeys.Add(m.Name);
        }

        // зауваження прив'язані або до коду ECRB, або до ЄДРПОУ в тому вигляді,
        // як він записаний у файлі, тобто, можливо, без нулів на початку
        var issuesByKey = issues.ToLookup(i => i.Key);

        var companies = new List<SiteCompany>(groups.Count);
        foreach (var draft in groups.Values)
        {
            if (draft.Code is not null)
            {
                draft.IssueKeys.Add(draft.Code);
                draft.IssueKeys.Add(draft.Code.TrimStart('0'));
            }

            var found = draft.IssueKeys
                .SelectMany(key => issuesByKey[key])
                .OrderBy(i => i.Dataset).ThenBy(i => i.Line)
                .Select(i => new SiteIssue(i.Dataset, i.Line, i.Code, i.Value))
                .ToList();

            companies.Add(new SiteCompany(
                draft.Code,
                draft.Code is null ? null : Edrpou.IsValid(draft.Code),
                draft.Monopolies.Count == 0 && draft.Wholesale.All(w => w.Country != "Україна"),
                draft.Name, Names.SearchKey(draft.Name),
                draft.Wholesale,
                draft.Monopolies.OrderBy(m => m.Dataset).ThenBy(m => m.Activity).ToList(),
                found));
        }

        var quality = await QualityApi.Summary(db, ct);
        return new SiteData(
            DateTime.UtcNow,
            await DatasetsApi.List(db, ct),
            quality,
            quality.Sum(q => q.SoleProprietors),
            companies.OrderBy(c => c.Name, StringComparer.Ordinal).ToList());
    }

    private static Draft Get(Dictionary<string, Draft> groups, string key, string? code, string name)
    {
        if (!groups.TryGetValue(key, out var draft))
            groups[key] = draft = new Draft(code, name);
        return draft;
    }

    private sealed class Draft(string? code, string name)
    {
        public string? Code { get; } = code;
        public string Name { get; } = name;
        public List<SiteParticipant> Wholesale { get; } = [];
        public List<SiteMonopoly> Monopolies { get; } = [];
        public HashSet<string> IssueKeys { get; } = [];
    }
}
