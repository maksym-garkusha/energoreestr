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

        // у зразку 10 записів і 2 рядки з назвою області; один запис повторюється
        Assert.Equal(10, file.TotalRows);
        Assert.Equal(9, file.Rows.Count);
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

        var duplicate = file.Issues.Single(i => i.Code == IssueCodes.DuplicateKey);
        Assert.Contains("ПОЛТАВАВОДОКАНАЛ", duplicate.Value);

        var badCode = file.Issues.Single(i => i.Code == IssueCodes.IdCodeFormat);
        Assert.Equal("35546472.", badCode.Value);

        Assert.Contains(file.Issues, i => i.Code == IssueCodes.MixedAlphabet && i.Value == "УКPНAФТА");
    }

    [Fact]
    public void Tax_number_of_a_sole_proprietor_is_not_kept()
    {
        var file = MonopolyParser.Parse(Fixture.Text("heat-water-sample.csv"));

        var row = file.Rows.Single(r => r.IsIndividual);
        Assert.Null(row.Edrpou);
        Assert.DoesNotContain("1234567890", row.Key);
    }
}
