using Energoreestr.Registers;

namespace Energoreestr.Tests;

public class MonopolyParserTests
{
    [Fact]
    public void Energy_register_restores_leading_zeros()
    {
        var file = MonopolyParser.Parse(Fixture.Text("energy-sample.csv"));

        Assert.Equal(5, file.Rows.Count);

        var kyivgaz = file.Rows.Single(r => r.Name.Contains("КИЇВГАЗ"));
        Assert.Equal("03346331", kyivgaz.Edrpou);
        Assert.Equal("М. КИЇВ", kyivgaz.Location);
        Assert.Equal("РОЗПОДІЛ ПРИРОДНОГО ГАЗУ", kyivgaz.Activity);

        // чотири з п'яти кодів у файлі семизначні
        Assert.Equal(4, file.Issues.Count(i => i.Code == IssueCodes.EdrpouLeadingZeros));
        Assert.DoesNotContain(file.Issues, i => i.Code == IssueCodes.EdrpouChecksum);
    }

    [Fact]
    public void Heat_and_water_register_skips_region_headings_and_empty_columns()
    {
        var file = MonopolyParser.Parse(Fixture.Text("heat-water-sample.csv"));

        // у зразку 13 записів і 2 рядки з назвою області. Чотири записи про вигаданих
        // підприємців, один з них повтор, і ще один повтор серед організацій
        Assert.Equal(13, file.TotalRows);
        Assert.Equal(8, file.Rows.Count);
        Assert.Equal(3, file.SoleProprietors);
        Assert.DoesNotContain(file.Rows, r => r.Name == "ПОЛТАВСЬКА ОБЛАСТЬ");
    }

    [Fact]
    public void Collapses_spaces_inside_cells()
    {
        var file = MonopolyParser.Parse(Fixture.Text("heat-water-sample.csv"));

        var row = file.Rows.Single(r => r.Edrpou == "37609523");
        Assert.Equal("ЦЕНТРАЛІЗОВАНЕ ВОДОПОСТАЧАННЯ ТА ЦЕНТРАЛІЗОВАНЕ ВОДОВІДВЕДЕННЯ", row.Activity);
    }

    [Fact]
    public void Row_without_number_is_still_a_record()
    {
        var file = MonopolyParser.Parse(Fixture.Text("heat-water-sample.csv"));

        var row = file.Rows.Single(r => r.Edrpou == "38401390");
        Assert.Null(row.Number);
    }

    [Fact]
    public void Reports_duplicates_bad_codes_and_mixed_alphabet()
    {
        var file = MonopolyParser.Parse(Fixture.Text("heat-water-sample.csv"));

        var duplicate = file.Issues.Single(i => i.Code == IssueCodes.DuplicateKey && i.Key != SoleProprietor.Key);
        Assert.Contains("ПОЛТАВАВОДОКАНАЛ", duplicate.Value);

        var badCode = file.Issues.Single(i => i.Code == IssueCodes.IdCodeFormat);
        Assert.Equal("35546472.", badCode.Value);

        Assert.Contains(file.Issues, i => i.Code == IssueCodes.MixedAlphabet && i.Value == "УКPНAФТА");
    }

    [Fact]
    public void Sole_proprietors_are_counted_but_not_kept()
    {
        var file = MonopolyParser.Parse(Fixture.Text("heat-water-sample.csv"));

        string kept = string.Join('\n', file.Rows.Select(r => r.ToString() + r.Key).Concat(file.Issues.Select(i => i.ToString())));
        foreach (var trace in Fixture.SoleProprietorTraces)
            Assert.DoesNotContain(trace, kept);

        var repeated = file.Issues.Single(i => i.Code == IssueCodes.DuplicateKey && i.Key == SoleProprietor.Key);
        Assert.Null(repeated.Value);
    }

    [Theory]
    [InlineData("1234567899")]
    [InlineData("123 456 7899")]
    [InlineData("123-456-7899")]
    [InlineData("1234567899.")]
    public void Person_name_with_ten_digits_is_a_sole_proprietor(string code)
    {
        var file = MonopolyParser.Parse(Csv(code, "ВИГАДАНИЙ ІВАН ІВАНОВИЧ"));

        Assert.Empty(file.Rows);
        Assert.Equal(1, file.SoleProprietors);
        Assert.Empty(file.Issues);
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("")]
    public void Marker_in_the_name_is_enough_whatever_the_code(string code)
    {
        var file = MonopolyParser.Parse(Csv(code, "фоп Вигаданий І. І."));

        Assert.Empty(file.Rows);
        Assert.Equal(1, file.SoleProprietors);
        Assert.Empty(file.Issues);
    }

    [Theory]
    [InlineData("1234567899")]
    [InlineData("123 456 7899")]
    [InlineData("1234567899.")]
    public void Organisation_with_a_ten_digit_code_keeps_its_name_but_not_the_code(string code)
    {
        const string name = "ТОВАРИСТВО З ОБМЕЖЕНОЮ ВІДПОВІДАЛЬНІСТЮ \"ТЕПЛО\"";

        var file = MonopolyParser.Parse(Csv(code, name));

        var row = Assert.Single(file.Rows);
        Assert.Equal(name, row.Name);
        Assert.Null(row.Edrpou);
        Assert.DoesNotContain("456", row.Key);

        var issue = Assert.Single(file.Issues);
        Assert.Equal(IssueCodes.TaxNumberInOrganisation, issue.Code);
        Assert.Equal(name, issue.Key);
        Assert.Null(issue.Value);
    }

    private static string Csv(string code, string name) =>
        "Number;Код;Назва;Місцезнаходження;Діяльність;Територія\n" +
        $"1;{code};\"{name.Replace("\"", "\"\"")}\";КИЇВСЬКА ОБЛАСТЬ;ТЕПЛО;КИЇВСЬКА ОБЛАСТЬ\n";
}
