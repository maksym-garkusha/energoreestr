using Energoreestr.Csv;

namespace Energoreestr.Tests;

public class CsvReaderTests
{
    [Fact]
    public void Splits_fields_and_records()
    {
        var records = CsvReader.Read("a;b;c\r\n1;2;3\r\n", ';');

        Assert.Equal(2, records.Count);
        Assert.Equal(["a", "b", "c"], records[0].Fields);
        Assert.Equal(["1", "2", "3"], records[1].Fields);
    }

    [Fact]
    public void Quoted_field_keeps_delimiter_and_doubled_quotes()
    {
        var records = CsvReader.Read("1;\"АТ \"\"КИЇВГАЗ\"\"; Київ\";x\n", ';');

        Assert.Equal(["1", "АТ \"КИЇВГАЗ\"; Київ", "x"], records[0].Fields);
    }

    [Fact]
    public void Quoted_field_may_span_lines_and_line_numbers_stay_correct()
    {
        var records = CsvReader.Read("a,\"перший\r\nдругий\",c\r\nx,y,z\r\n", ',');

        Assert.Equal(2, records.Count);
        Assert.Equal("перший\r\nдругий", records[0].Fields[1]);
        Assert.Equal(1, records[0].Line);
        Assert.Equal(3, records[1].Line);
    }

    [Fact]
    public void Quote_in_the_middle_of_a_field_is_just_a_character()
    {
        var records = CsvReader.Read("ТОВ \"ЕНЕРГО\",1\n", ',');

        Assert.Equal(["ТОВ \"ЕНЕРГО\"", "1"], records[0].Fields);
    }

    [Fact]
    public void Skips_bom_and_empty_lines_and_reads_last_line_without_newline()
    {
        var records = CsvReader.Read("﻿a,b\n\n\n1,2", ',');

        Assert.Equal(2, records.Count);
        Assert.Equal("a", records[0].Fields[0]);
        Assert.Equal(["1", "2"], records[1].Fields);
        Assert.Equal(4, records[1].Line);
    }

    [Fact]
    public void Keeps_leading_spaces_because_row_repair_depends_on_them()
    {
        var records = CsvReader.Read("Національна комісія, що здійснює\n", ',');

        Assert.Equal(" що здійснює", records[0].Fields[1]);
    }
}
