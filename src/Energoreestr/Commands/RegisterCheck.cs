using System.Text;
using Energoreestr.Check;
using Energoreestr.Import;
using Energoreestr.Registers;

namespace Energoreestr.Commands;

/// <summary>
/// Перевірка файлу одного з підключених реєстрів наявними парсерами: ті самі зауваження,
/// що потрапляють у звіт про якість, але без бази.
/// </summary>
public static class RegisterCheck
{
    public static readonly IReadOnlyDictionary<string, CodeInfo> Codes =
        IssueCodes.Descriptions.ToDictionary(d => d.Key, d => new CodeInfo(LevelOf(d.Key), d.Value));

    public static CheckReport Run(DatasetDef def, byte[] content)
    {
        // так само, як при імпорті
        string text = Encoding.UTF8.GetString(content);

        var (issues, total, soleProprietors) = def.Kind == RegisterKind.Wholesale
            ? Parsed(WholesaleParser.Parse(text))
            : Parsed(MonopolyParser.Parse(text));

        var findings = issues.Select(i => new Finding(i.Code, i.Line, Value: i.Value)).ToList();
        return new CheckReport(total, findings, soleProprietors);
    }

    private static (List<RowIssue>, int, int) Parsed(WholesaleFile file) => (file.Issues, file.TotalRows, file.SoleProprietors);

    private static (List<RowIssue>, int, int) Parsed(MonopolyFile file) => (file.Issues, file.TotalRows, file.SoleProprietors);

    private static Level LevelOf(string code) => code switch
    {
        IssueCodes.RowRearranged or IssueCodes.RowUnreadable or IssueCodes.EdrpouLeadingZeros
            or IssueCodes.EicFormat or IssueCodes.DateFormat or IssueCodes.MixedAlphabet => Level.Fix,
        IssueCodes.DashPlaceholder => Level.Info,
        _ => Level.Verify
    };
}
