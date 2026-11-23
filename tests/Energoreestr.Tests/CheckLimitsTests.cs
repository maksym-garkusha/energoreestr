using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using Energoreestr.Check;
using Energoreestr.Commands;
using Energoreestr.Registers;

namespace Energoreestr.Tests;

/// <summary>Файли, які не мають ні звалити перевірку, ні з'їсти пам'ять чи час.</summary>
public class CheckLimitsTests
{
    private static readonly DateOnly Today = new(2026, 11, 23);

    public static byte[] ManyShortCodes() =>
        Encoding.UTF8.GetBytes("Номер;Код ЄДРПОУ\n" + string.Concat(Enumerable.Range(1, 1200).Select(i => $"{i};{1000000 + i}\n")));

    public static byte[] TooManyColumns() =>
        Encoding.UTF8.GetBytes(string.Join(';', Enumerable.Range(1, 1001)) + "\n1;2\n");

    [Fact]
    public void Arabic_indic_digits_do_not_crash_the_check()
    {
        var report = FileCheck.Run(Fixture.Bytes("check/unusual-characters.csv"), Today);

        Assert.Null(report.Problem);
        Assert.Equal(9, report.Rows);
    }

    [Fact]
    public void Only_the_first_thousand_findings_of_a_kind_are_kept_but_all_are_counted()
    {
        var report = FileCheck.Run(ManyShortCodes(), Today);

        Assert.Equal(1000, report.Findings.Count(f => f.Code == CheckCodes.EdrpouLeadingZeros));
        Assert.Equal(1200, report.Total(CheckCodes.EdrpouLeadingZeros));

        var group = JsonNode.Parse(ReportJson.Write(report, CheckCodes.All))!["groups"]!.AsArray()
            .Single(g => (string)g!["code"]! == CheckCodes.EdrpouLeadingZeros)!;
        Assert.Equal(1200, (int)group["count"]!);
        Assert.Equal(1000, group["lines"]!.AsArray().Count);
        Assert.True((bool)group["truncated"]!);
        Assert.Contains("і ще 1195", ReportText.Write(report, CheckCodes.All));
    }

    [Fact]
    public void File_with_more_than_a_thousand_columns_is_refused()
    {
        Assert.Equal(FileCheck.TooManyColumns, FileCheck.Run(TooManyColumns(), Today).Problem);
    }

    [Fact]
    public void Value_longer_than_the_limit_skips_its_row_with_its_own_finding()
    {
        string csv = "\uFEFFНомер;Код ЄДРПОУ;Найменування\n1;35209058;ТОВ\n2;36066930;" + new string('Ж', 100_001) + "\n";

        var report = FileCheck.Run(Encoding.UTF8.GetBytes(csv), Today);

        var finding = Assert.Single(report.Findings);
        Assert.Equal((CheckCodes.ValueTooLong, 3, null), (finding.Code, finding.Line, finding.Value));
    }

    // обмежені кількісники: рядок з «ФІЗИЧН» без «ОСОБ» більше не дає квадратичного часу
    [Fact]
    public void Long_text_full_of_almost_markers_is_checked_quickly()
    {
        string text = string.Concat(Enumerable.Repeat("ФІЗИЧН ", 40_000));
        var watch = Stopwatch.StartNew();

        Assert.False(SoleProprietor.IsNamedAsOne(text));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2), $"{watch.Elapsed}");
    }

    [Fact]
    public async Task File_over_one_gigabyte_is_refused_without_reading_it()
    {
        string path = Path.Combine(Path.GetTempPath(), $"check-{Guid.NewGuid():N}.csv");
        await using (var stream = File.Create(path)) stream.SetLength((1L << 30) + 1);
        try
        {
            var error = new StringWriter();
            int code = await CheckCommand.RunAsync([path], new StringWriter(), error);

            Assert.Equal(2, code);
            Assert.Contains("1 ГБ", error.ToString());
        }
        finally
        {
            File.Delete(path);
        }
    }
}
