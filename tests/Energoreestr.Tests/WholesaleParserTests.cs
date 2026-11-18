using Energoreestr.Registers;

namespace Energoreestr.Tests;

public class WholesaleParserTests
{
    private static readonly WholesaleFile File = WholesaleParser.Parse(Fixture.Text("wholesale-sample.csv"));

    private static ParticipantRow Row(int number) => File.Rows.Single(r => r.Number == number);

    private static List<string> IssuesOf(string ecrbCode) =>
        File.Issues.Where(i => i.Key == ecrbCode).Select(i => i.Code).ToList();

    private static readonly string[] Lines = Fixture.Text("wholesale-sample.csv").Split("\r\n");

    private static string Header => Lines[0];

    private static string Line(int number) => Lines.Single(l => l.StartsWith(number + ","));

    [Fact]
    public void Reads_every_row_of_the_sample()
    {
        // 19 записів, з них три про вигаданих підприємців
        Assert.Equal(19, File.TotalRows);
        Assert.Equal(16, File.Rows.Count);
        Assert.Equal(3, File.SoleProprietors);
        Assert.DoesNotContain(File.Issues, i => i.Code == IssueCodes.RowUnreadable);
    }

    [Fact]
    public void Ordinary_row()
    {
        var row = Row(1);

        Assert.Equal("ECRB-UA-20231019-001", row.EcrbCode);
        Assert.Equal("ТОВАРИСТВО З ОБМЕЖЕНОЮ ВІДПОВІДАЛЬНІСТЮ \"МГ БІЗНЕС ГРУП\"", row.Name);
        Assert.Equal("Товариство з обмеженою відповідальністю", row.LegalForm);
        Assert.Equal(new DateOnly(2023, 10, 19), row.SubmittedOn);
        Assert.Equal(new DateOnly(2023, 10, 27), row.RegisteredOn);
        Assert.Equal("місто Київ", row.Address);
        Assert.Equal("Україна", row.Country);
        Assert.Equal("62X529473093477D", row.Eic);
        Assert.Equal("42750837", row.IdCode);
        Assert.Equal("www.mgbg.com.ua", row.Website);

        // слово null у файлі означає порожнє значення
        Assert.Null(row.Settlement);
        Assert.Null(row.Vat);
        Assert.Null(row.ExcludedOn);
        Assert.Empty(IssuesOf(row.EcrbCode));
    }

    [Fact]
    public void Two_urls_in_one_column_stay_together()
    {
        var row = Row(10);

        Assert.Equal("https://utg.ua/", row.Website);
        Assert.Equal("https://utg.ua/, https://inside.oree.com.ua/", row.InsideInfoUrl);
        Assert.Empty(IssuesOf(row.EcrbCode));
    }

    [Fact]
    public void Website_written_after_comma_and_space_does_not_swallow_the_id_code()
    {
        // у файлі: ...,44524171, https://solipowertrading.com.ua/  ,https://inside.oree.com.ua/,null
        var row = Row(63);

        Assert.Equal("44524171", row.IdCode);
        Assert.Equal("https://solipowertrading.com.ua/", row.Website);
        Assert.Equal("https://inside.oree.com.ua/", row.InsideInfoUrl);
        Assert.Contains(IssueCodes.RowRearranged, IssuesOf(row.EcrbCode));
    }

    [Fact]
    public void Quoted_cells_with_line_breaks_inside()
    {
        var row = Row(736);

        Assert.Equal("43202392", row.IdCode);
        Assert.Equal("https://zahid-ez.com.ua", row.Website);
        Assert.Equal("https://zahid-ez.com.ua", row.InsideInfoUrl);
    }

    [Fact]
    public void Foreign_address_with_many_commas()
    {
        var row = Row(1168);

        Assert.Equal("НЕЙЧИРАЛ ГАЗ Д,С.", row.Name);
        Assert.Equal("вул. Олексія Щюсєва, 55", row.Address);
        Assert.Equal("м. Кишинів", row.Settlement);
        Assert.Equal("MD2012", row.Zip);
        Assert.Equal("68X3NATURAL-GAZN", row.Eic);
    }

    [Fact]
    public void Name_is_not_confused_with_legal_form()
    {
        Assert.Equal("ГНГ ІНТЕРНЕШИНАЛ ЛІМІТЕД", Row(1825).Name);
        Assert.Equal("Товариство з обмеженою відповідальністю", Row(1825).LegalForm);
        Assert.Equal("м. Нікосія", Row(1825).Settlement);

        // назва записана як ,,НАГІРЯНСЬКЕ'' з двома комами замість лапок
        Assert.Equal("ТОВАРИСТВО З ОБМЕЖЕНОЮ ВІДПОВІДАЛЬНІСТЮ ,,НАГІРЯНСЬКЕ''", Row(3079).Name);
        Assert.Equal("Тернопільська область", Row(3079).Address);
    }

    [Fact]
    public void Marks_row_when_address_could_be_split_in_more_than_one_way()
    {
        var row = Row(425);

        Assert.Contains(IssueCodes.RowAmbiguous, IssuesOf(row.EcrbCode));
        // усе, крім адреси та населеного пункту, однозначне
        Assert.Equal("DXT COMMODITIES SA", row.Name);
        Assert.Equal("6900", row.Zip);
        Assert.Equal("Швейцарська Конфедерація", row.Country);
        Assert.Equal("12X-0000001848-D", row.Eic);
    }

    [Fact]
    public void Exclusion_date_with_time_is_read_and_reported()
    {
        var row = Row(2352);

        Assert.Equal(new DateOnly(2026, 5, 19), row.ExcludedOn);
        Assert.Contains(IssueCodes.DateFormat, IssuesOf(row.EcrbCode));
    }

    [Fact]
    public void Reports_value_problems()
    {
        Assert.Contains(IssueCodes.EicChecksum, IssuesOf(Row(829).EcrbCode));
        Assert.Contains(IssueCodes.DashPlaceholder, IssuesOf(Row(543).EcrbCode));
        Assert.Contains(IssueCodes.MixedAlphabet, IssuesOf(Row(232).EcrbCode));

        // на місці сторінки з інсайдерською інформацією стоїть e-mail
        var email = File.Issues.Single(i => i.Key == Row(145).EcrbCode && i.Code == IssueCodes.UrlFormat);
        Assert.Equal("national_energo@ukr.net", email.Value);
    }

    [Fact]
    public void Dash_is_treated_as_empty()
    {
        Assert.Null(Row(543).Bic);
    }

    [Fact]
    public void Sole_proprietors_are_counted_but_not_kept()
    {
        Assert.DoesNotContain(File.Rows, r => r.Number is 9001 or 9003 or 9004);

        string rows = string.Join('\n', File.Rows);
        foreach (var trace in Fixture.SoleProprietorTraces)
            Assert.DoesNotContain(trace, rows);
    }

    [Fact]
    public void Issues_on_a_sole_proprietor_row_keep_only_the_line()
    {
        var issues = File.Issues.Where(i => i.Key == SoleProprietor.Key).ToList();

        // у рядку 9003 EIC малими літерами, у 9004 латинська A в імені
        Assert.Contains(issues, i => i.Code == IssueCodes.EicFormat);
        Assert.Contains(issues, i => i.Code == IssueCodes.MixedAlphabet);
        Assert.All(issues, i => Assert.Null(i.Value));

        string all = string.Join('\n', File.Issues);
        foreach (var trace in Fixture.SoleProprietorTraces)
            Assert.DoesNotContain(trace, all);
    }

    [Theory]
    [InlineData("1234567899")]
    [InlineData("123 456 7899")]
    [InlineData("123-456-7899")]
    [InlineData("1234567899.")]
    public void Ten_digits_in_a_ukrainian_record_mark_a_sole_proprietor_whatever_the_name(string code)
    {
        // назва й форма як в організації: підприємця видно тільки з коду
        string line = Line(9001)
            .Replace("ФІЗИЧНА ОСОБА-ПІДПРИЄМЕЦЬ ТЕСТОВИЙ ТЕСТ ТЕСТОВИЧ", "ВИГАДАНЕ ТОВАРИСТВО")
            .Replace("Фізична особа-підприємець", "Товариство з обмеженою відповідальністю")
            .Replace("1234567890", code);

        var file = WholesaleParser.Parse(Header + "\r\n" + line + "\r\n");

        Assert.Empty(file.Rows);
        Assert.Equal(1, file.SoleProprietors);
        Assert.DoesNotContain(file.Issues, i => i.Value is not null);
    }

    [Fact]
    public void Foreign_id_code_is_kept_as_is()
    {
        Assert.Equal("HE 276280", Row(1825).IdCode);
        Assert.DoesNotContain(IssueCodes.IdCodeFormat, IssuesOf(Row(1825).EcrbCode));
    }

    [Fact]
    public void Second_row_with_the_same_registration_code_is_dropped()
    {
        string text = Fixture.Text("wholesale-sample.csv");
        string firstRow = text.Split("\r\n")[1];

        var file = WholesaleParser.Parse(text + firstRow + "\r\n");

        Assert.Equal(16, file.Rows.Count);
        Assert.Contains(file.Issues, i => i.Code == IssueCodes.DuplicateKey && i.Key == "ECRB-UA-20231019-001");
    }
}
