using Energoreestr.Check;

namespace Energoreestr.Tests;

public class ReportTests
{
    private static readonly IReadOnlyDictionary<string, CodeInfo> Codes = new Dictionary<string, CodeInfo>
    {
        ["fix"] = new(Level.Fix, "поправити"),
        ["verify"] = new(Level.Verify, "звірити"),
        ["info"] = new(Level.Info, "до відома")
    };

    private static CheckReport With(params string[] codes) =>
        new(10, codes.Select((c, i) => new Finding(c, i + 2)).ToList(), 0);

    [Fact]
    public void Exit_code_is_0_when_nothing_needs_fixing()
    {
        Assert.Equal(0, ReportGroups.ExitCode(With(), Codes, strict: false));
        Assert.Equal(0, ReportGroups.ExitCode(With("info", "verify"), Codes, strict: false));
    }

    [Fact]
    public void Exit_code_is_1_when_something_needs_fixing()
    {
        Assert.Equal(1, ReportGroups.ExitCode(With("info", "fix"), Codes, strict: false));
    }

    [Fact]
    public void Strict_mode_also_fails_on_things_to_verify()
    {
        Assert.Equal(1, ReportGroups.ExitCode(With("verify"), Codes, strict: true));
        Assert.Equal(0, ReportGroups.ExitCode(With("info"), Codes, strict: true));
    }

    [Fact]
    public void Exit_code_is_2_when_the_file_could_not_be_read()
    {
        Assert.Equal(2, ReportGroups.ExitCode(CheckReport.Unreadable("порожній файл"), Codes, strict: false));
    }

    [Fact]
    public void Groups_go_from_fixes_to_notes_and_then_by_count()
    {
        var groups = ReportGroups.Build(With("info", "verify", "fix", "verify"), Codes);

        Assert.Equal(["fix", "verify", "info"], groups.Select(g => g.Code));
        Assert.Equal(2, groups[1].Findings.Count);
    }

    [Theory]
    [InlineData(new[] { 7, 8, 9, 10 }, "8-11")]
    [InlineData(new[] { 0, 2, 3, 5 }, "1, 3-4, 6")]
    [InlineData(new[] { 4 }, "5")]
    public void Column_numbers_collapse_into_ranges(int[] zeroBased, string expected)
    {
        Assert.Equal(expected, ReportGroups.Ranges(zeroBased));
    }

    [Fact]
    public void Json_report_has_a_schema_version_and_no_problem_when_readable()
    {
        string json = ReportJson.Write(With("fix"), Codes);

        Assert.Contains("\"schema\": 1", json);
        Assert.DoesNotContain("problem", json);
        Assert.Contains("\"lines\": [", json);
    }
}
