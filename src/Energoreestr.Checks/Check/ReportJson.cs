using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Unicode;

namespace Energoreestr.Check;

/// <summary>
/// Звіт у JSON для автоматизації. Ім'я файлу не пишемо: воно буває з прізвищем.
/// Номери рядків і колонок від одиниці, як їх бачить людина.
/// </summary>
public static class ReportJson
{
    public const int Schema = 1;
    private const int Examples = 5;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        // без цього кирилиця перетворилася б на Т...
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    public static string Write(CheckReport report, IReadOnlyDictionary<string, CodeInfo> codes)
    {
        var root = new JsonObject { ["schema"] = Schema };
        if (report.Problem is not null)
        {
            root["problem"] = report.Problem;
            return root.ToJsonString(Options);
        }

        if (report.File is { } file) root["file"] = File(file);
        root["rows"] = report.Rows;
        root["summary"] = new JsonObject
        {
            ["fix"] = ReportGroups.Count(report, codes, Level.Fix),
            ["verify"] = ReportGroups.Count(report, codes, Level.Verify),
            ["info"] = ReportGroups.Count(report, codes, Level.Info),
            // підсумок рахує види зауважень, а тут кількість окремих знахідок
            ["findings"] = new JsonObject
            {
                ["fix"] = ReportGroups.Findings(report, codes, Level.Fix),
                ["verify"] = ReportGroups.Findings(report, codes, Level.Verify),
                ["info"] = ReportGroups.Findings(report, codes, Level.Info)
            },
            ["personalDataRows"] = report.PersonalDataRows
        };
        root["groups"] = new JsonArray([.. ReportGroups.Build(report, codes).Select(Group)]);
        return root.ToJsonString(Options);
    }

    private static JsonObject File(FileProfile file) => new()
    {
        ["encoding"] = file.Encoding,
        ["delimiter"] = file.Delimiter == '\t' ? "tab" : file.Delimiter.ToString(),
        ["hasHeader"] = file.HasHeader,
        ["columns"] = new JsonArray([.. file.Columns.Select(c => (JsonNode)new JsonObject
        {
            ["index"] = c.Index + 1,
            ["name"] = c.Name,
            ["kind"] = c.Kind.ToString().ToLowerInvariant(),
            ["filled"] = c.Filled
        })])
    };

    private static JsonObject Group(ReportGroup group)
    {
        var json = Details(group);
        // перелік рядків обрізано, але count повний
        if (group.Truncated) json["truncated"] = true;
        return json;
    }

    private static JsonObject Details(ReportGroup group) => new()
    {
        ["code"] = group.Code,
        ["level"] = group.Info.Level.ToString().ToLowerInvariant(),
        ["title"] = group.Info.Title,
        ["unit"] = group.Info.Unit.ToString().ToLowerInvariant(),
        ["text"] = group.Info.Text,
        ["count"] = group.Total,
        ["lines"] = Numbers(group.Findings.Where(f => f.Line > 0).Select(f => f.Line)),
        ["columns"] = Numbers(group.Findings.Where(f => f.Column is not null).Select(f => f.Column!.Value + 1)),
        ["examples"] = new JsonArray([.. group.Findings
            .Where(f => f.Value is not null || f.Note is not null)
            .Take(Examples)
            .Select(Example)])
    };

    private static JsonNode Example(Finding finding)
    {
        var example = new JsonObject { ["line"] = finding.Line };
        if (finding.Column is int column) example["column"] = column + 1;
        if (finding.Value is not null) example["value"] = finding.Value;
        if (finding.Note is not null) example["note"] = finding.Note;
        return example;
    }

    private static JsonArray Numbers(IEnumerable<int> numbers) =>
        new([.. numbers.Distinct().Order().Select(n => (JsonNode)n)]);
}
