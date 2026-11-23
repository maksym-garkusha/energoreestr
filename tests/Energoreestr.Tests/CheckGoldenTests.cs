using Energoreestr.Check;

namespace Energoreestr.Tests;

/// <summary>
/// Еталони загальної перевірки: для кожного файлу з Fixtures/check очікуваний JSON лежить поруч.
/// Ті самі файли згодом перевірятиме й сторінка, тому це і є договір між двома реалізаціями.
/// Три витяги реєстрів лежать рівнем вище, а їхні еталони тут же.
/// </summary>
public class CheckGoldenTests
{
    private static readonly DateOnly Today = new(2026, 11, 23);

    public static TheoryData<string> Fixtures() =>
        [.. Directory.GetFiles(Fixture.Path("check"), "*.expected.json")
            .Select(f => Path.GetFileName(f).Replace(".expected.json", ""))
            .Order()];

    public static string CsvFor(string name) =>
        File.Exists(Fixture.Path($"check/{name}.csv")) ? $"check/{name}.csv" : $"{name}.csv";

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Report_matches_the_expected_file(string name)
    {
        var report = FileCheck.Run(Fixture.Bytes(CsvFor(name)), Today);

        string expected = Fixture.Text($"check/{name}.expected.json").ReplaceLineEndings("\n").TrimEnd();
        string actual = ReportJson.Write(report, CheckCodes.All).ReplaceLineEndings("\n").TrimEnd();
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void There_are_enough_expected_files()
    {
        Assert.True(Fixtures().Count >= 12);
    }
}
