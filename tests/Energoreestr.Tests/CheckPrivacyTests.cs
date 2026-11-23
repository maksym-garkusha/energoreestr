using System.Text;
using System.Text.Json.Nodes;
using Energoreestr.Check;
using Energoreestr.Commands;

namespace Energoreestr.Tests;

/// <summary>
/// Файли, де дані людини стоять не там, де їх чекають: у колонці кодів, у першому рядку,
/// в рядку-розділі, серед тексту. Ні текстовий звіт, ні JSON не мають показати з них нічого.
/// </summary>
public class CheckPrivacyTests
{
    private static async Task<(int Code, string Text, string Json)> Run(string fixture)
    {
        var text = new StringWriter();
        var json = new StringWriter();
        int code = await CheckCommand.RunAsync([Fixture.Path(fixture)], text, new StringWriter());
        await CheckCommand.RunAsync(["--json", Fixture.Path(fixture)], json, new StringWriter());
        return (code, text.ToString(), json.ToString());
    }

    [Theory]
    [InlineData("check/text-in-code.csv")]
    [InlineData("check/person-as-header.csv")]
    [InlineData("check/person-header-tax.csv")]
    [InlineData("check/person-in-text.csv")]
    public async Task Nothing_about_a_person_reaches_the_report(string fixture)
    {
        var (_, text, json) = await Run(fixture);

        foreach (var trace in Fixture.SoleProprietorTraces)
        {
            Assert.DoesNotContain(trace, text);
            Assert.DoesNotContain(trace, json);
        }
    }

    [Fact]
    public async Task Name_typed_into_a_code_column_is_not_shown_even_in_latin_letters()
    {
        var (_, _, json) = await Run("check/text-in-code.csv");

        var group = JsonNode.Parse(json)!["groups"]!.AsArray().Single(g => (string)g!["code"]! == CheckCodes.EdrpouFormat)!;
        Assert.Equal(2, (int)group["count"]!);
        Assert.Empty(group["examples"]!.AsArray());
    }

    // рядок, у якому лапки так і не закрилися, вбирає решту файлу: його не перевіряємо зовсім
    [Fact]
    public void Record_swallowed_by_an_open_quote_gets_only_the_quote_finding()
    {
        var report = FileCheck.Run(Fixture.Bytes("check/text-in-code.csv"), new DateOnly(2026, 11, 23));

        var line = Assert.Single(report.Findings, f => f.Code == CheckCodes.UnclosedQuote).Line;
        Assert.Equal([CheckCodes.UnclosedQuote], report.Findings.Where(f => f.Line == line).Select(f => f.Code));
    }

    [Theory]
    [InlineData("check/person-as-header.csv")]
    [InlineData("check/person-header-tax.csv")]
    public async Task First_row_about_a_person_is_not_used_as_column_names(string fixture)
    {
        var (_, text, json) = await Run(fixture);

        Assert.Contains("Перший рядок схожий на дані", text);
        var columns = JsonNode.Parse(json)!["file"]!["columns"]!.AsArray();
        Assert.All(columns, c => Assert.Equal("", (string)c!["name"]!));
    }

    [Fact]
    public async Task Sole_proprietor_alone_in_a_row_and_tax_numbers_inside_text_mark_their_rows()
    {
        var (_, text, json) = await Run("check/person-in-text.csv");

        Assert.Contains("\"personalDataRows\": 3", json);
        Assert.Contains("Рядків з ознаками персональних даних: 3", text);
    }

    [Fact]
    public void Header_cell_longer_than_200_characters_is_not_a_column_name()
    {
        string header = "Номер;" + new string('Ж', 201) + ";Код ЄДРПОУ\n";
        string rows = string.Concat(Enumerable.Range(1, 6).Select(i => $"{i};ТОВ;35209058\n"));

        var report = FileCheck.Run(Encoding.UTF8.GetBytes(header + rows), new DateOnly(2026, 11, 23));

        Assert.False(report.File!.HasHeader);
    }

    [Fact]
    public void Long_column_names_are_cut_to_60_characters()
    {
        string name = string.Concat(Enumerable.Repeat("Найменування ", 10));
        string csv = $"Номер;{name};Код ЄДРПОУ\n" + string.Concat(Enumerable.Range(1, 6).Select(i => $"{i};ТОВ;35209058\n"));

        var report = FileCheck.Run(Encoding.UTF8.GetBytes(csv), new DateOnly(2026, 11, 23));

        Assert.Equal(60, report.File!.Columns[1].Name.Length);
        Assert.EndsWith("…", report.File.Columns[1].Name);
    }

    [Theory]
    [InlineData("3346331", true)]
    [InlineData("2025-02-30", true)]
    [InlineData("1,23457E+11", true)]
    [InlineData("Tarasenko Vyhadanyi", false)]   // ім'я латиницею без цифр
    [InlineData("ТОВ 123", false)]
    [InlineData("UA213223130000026007233566001", false)]   // довше за 24
    [InlineData("12\u001b34", false)]
    public void Only_short_code_like_values_can_be_shown(string value, bool shown)
    {
        Assert.Equal(shown, Cells.IsDisplayable(value));
    }

    [Fact]
    public async Task Control_characters_in_names_are_shown_as_escapes()
    {
        var (_, text, _) = await Run("check/unusual-characters.csv");

        Assert.Contains("\\u202E\\u001B[2J", text);
        Assert.DoesNotContain('‮', text);
        Assert.DoesNotContain('\u001b', text);
    }

    [Fact]
    public void Visible_text_keeps_tabs_and_letters_and_escapes_the_rest()
    {
        Assert.Equal("а\tb \\u202E c", ReportText.Visible("а\tb\n‮ c"));
    }
}
