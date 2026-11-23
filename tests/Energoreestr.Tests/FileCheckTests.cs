using System.Text;
using Energoreestr.Check;

namespace Energoreestr.Tests;

public class FileCheckTests
{
    private static readonly DateOnly Today = new(2026, 11, 23);

    private static CheckReport Check(string csv, bool bom = true) =>
        FileCheck.Run((bom ? "﻿" : "") is var prefix ? Encoding.UTF8.GetBytes(prefix + csv) : [], Today);

    private static List<string> Codes(CheckReport report) => report.Findings.Select(f => f.Code).Distinct().Order().ToList();

    // шість рядків організацій з вигаданими кодами, що проходять контрольний розряд
    private const string Header = "Номер;Код ЄДРПОУ;Найменування\n";
    private const string Rows =
        "1;35209058;ТОВ \"ЗРАЗОК\"\n2;36066930;ТОВ \"МОДЕЛЬ\"\n3;33343774;КП \"ПРИКЛАД\"\n" +
        "4;27641862;ТОВ \"ВЗІРЕЦЬ\"\n5;42857064;КП \"ШАБЛОН\"\n6;34015271;ТОВ \"МАКЕТ\"\n";

    [Fact]
    public void Well_formed_file_has_no_findings()
    {
        var report = Check(Header + Rows);

        Assert.Empty(report.Findings);
        Assert.Equal(6, report.Rows);
        Assert.Equal(ColumnKind.Edrpou, report.File!.Columns[1].Kind);
    }

    [Theory]
    [InlineData(new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00 }, FileCheck.ExcelFile)]
    [InlineData(new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1 }, FileCheck.ExcelFile)]
    [InlineData(new byte[] { 0x41, 0x00, 0x42 }, FileCheck.NotText)]
    [InlineData(new byte[0], FileCheck.Empty)]
    [InlineData(new byte[] { 0xEF, 0xBB, 0xBF, 0x0D, 0x0A }, FileCheck.Empty)]
    public void Files_that_are_not_csv_are_not_checked(byte[] content, string problem)
    {
        var report = FileCheck.Run(content, Today);

        Assert.Equal(problem, report.Problem);
        Assert.Equal(2, ReportGroups.ExitCode(report, CheckCodes.All, strict: false));
    }

    [Fact]
    public void Windows_1251_file_is_read_and_reported()
    {
        var bytes = CodePagesEncodingProvider.Instance.GetEncoding(1251)!.GetBytes(Header + Rows);
        var report = FileCheck.Run(bytes, Today);

        Assert.Equal("windows-1251", report.File!.Encoding);
        Assert.Equal([CheckCodes.Encoding1251], Codes(report));
        Assert.Equal("Найменування", report.File.Columns[2].Name);
    }

    [Fact]
    public void Utf8_without_mark_is_only_a_note_and_only_when_there_is_non_latin_text()
    {
        Assert.Equal([CheckCodes.Utf8NoBom], Codes(Check(Header + Rows, bom: false)));
        Assert.Empty(Check("number;code\n1;a\n2;b\n", bom: false).Findings);
    }

    [Theory]
    [InlineData(';')]
    [InlineData(',')]
    [InlineData('\t')]
    [InlineData('|')]
    public void Column_separator_is_detected(char delimiter)
    {
        var report = Check((Header + Rows).Replace(';', delimiter));

        Assert.Equal(delimiter, report.File!.Delimiter);
        Assert.Equal(3, report.File.Columns.Count);
    }

    [Fact]
    public void Comma_inside_unquoted_value_is_reported_and_the_separator_is_still_found()
    {
        var report = Check((Header + Rows).Replace(';', ',') + "7,35209058,Товариство \"Приклад\", м. Київ\n");

        Assert.Equal(',', report.File!.Delimiter);
        // перший рядок файлу заголовок, тож сьомий запис стоїть у восьмому рядку
        Assert.Equal(8, Assert.Single(report.Findings, f => f.Code == CheckCodes.UnquotedDelimiter).Line);
    }

    [Fact]
    public void File_with_one_column_is_worth_a_look()
    {
        Assert.Contains(CheckCodes.SingleColumn, Codes(Check("назва\nперша\nдруга\n")));
    }

    [Fact]
    public void Seven_digit_code_is_reported_as_lost_zeros_with_its_value()
    {
        var finding = Assert.Single(Check(Header + Rows + "7;2249063;ТОВ \"КОПІЯ\"\n").Findings);

        Assert.Equal(CheckCodes.EdrpouLeadingZeros, finding.Code);
        Assert.Equal((8, 1, "2249063"), (finding.Line, finding.Column!.Value, finding.Value));
    }

    [Fact]
    public void Numbers_that_only_look_like_codes_are_not_treated_as_codes()
    {
        // восьмицифрові номери, які майже ніколи не проходять контрольний розряд ЄДРПОУ
        var report = Check("Номер ліцензії;Найменування\n10000001;А\n10000002;Б\n10000003;В\n10000004;Г\n10000005;Ґ\n10000006;Д\n");

        Assert.Equal(ColumnKind.Number, report.File!.Columns[0].Kind);
        Assert.Empty(report.Findings);
    }

    [Theory]
    [InlineData("1234567899", true)]     // вигаданий номер з правильною контрольною цифрою
    [InlineData("123 456 7899", true)]
    [InlineData("1234567890", false)]    // контрольна цифра не та
    [InlineData("0441234567", false)]    // телефон
    [InlineData("4600000000", false)]    // день народження в майбутньому
    [InlineData("12345678ab", false)]
    public void Tax_number_outside_code_columns_needs_checksum_and_plausible_birth_date(string value, bool expected)
    {
        Assert.Equal(expected, Cells.IsPlausibleTaxNumber(value, Today));
    }

    [Fact]
    public void Ten_digits_in_a_code_column_mark_the_row_as_personal_and_hide_its_values()
    {
        var report = Check(Header + Rows + "7;1234567890;ПЕТРО ВИГАДАНИЙ\n8;123456;ФОП ЗРАЗКОВИЙ\n");

        Assert.Equal(2, report.PersonalDataRows);
        Assert.Contains(report.Findings, f => f.Code == CheckCodes.PersonalTaxNumber && f.Line == 8);
        Assert.Contains(report.Findings, f => f.Code == CheckCodes.PersonalSoleProprietor && f.Line == 9);
        // короткий код у рядку підприємця теж зауваження, але без значення
        var zeros = Assert.Single(report.Findings, f => f.Code == CheckCodes.EdrpouLeadingZeros);
        Assert.Null(zeros.Value);
    }

    [Fact]
    public void Phone_numbers_are_not_mistaken_for_tax_numbers()
    {
        var report = Check("Найменування;Телефон\nА;0441234567\nБ;1234567899\nВ;0501112233\nГ;0672223344\nҐ;0443334455\n");

        Assert.Equal(0, report.PersonalDataRows);
    }

    [Fact]
    public void Text_values_are_never_shown_even_outside_personal_rows()
    {
        var report = Check(Header + Rows + "7;34015271;ТОВ \"МАКEТ\"\n");

        var mixed = Assert.Single(report.Findings, f => f.Code == CheckCodes.MixedAlphabet);
        Assert.Null(mixed.Value);
        Assert.Equal("слово 2, латинські літери: E", mixed.Note);
    }

    [Fact]
    public void Doubly_encoded_text_is_recognised_but_ordinary_text_is_not()
    {
        // так виходить, коли текст у UTF-8 прочитали як Windows-1251
        string garbled = CodePagesEncodingProvider.Instance.GetEncoding(1251)!
            .GetString(Encoding.UTF8.GetBytes("ТОВАРИСТВО ЗРАЗОК ТЕПЛОВІ МЕРЕЖІ"));

        Assert.True(Mojibake.LooksGarbled(garbled + " " + garbled));
        Assert.False(Mojibake.LooksGarbled("РІВНЕНСЬКЕ ТОВ \"ГАЗ-СЕРВІС\" СРІБНЕ РІЧКОВЕ СЕРВІС» ПРОГРЕС’"));
    }

    [Fact]
    public void Dates_are_checked_for_existence_and_for_one_format_per_column()
    {
        var report = Check("Найменування;Дата\nА;2025-01-01\nБ;2025-02-30\nВ;03.02.2025\nГ;2025-01-04\nҐ;2025-01-05\nД;2025-01-06\n");

        Assert.Equal([CheckCodes.DateInvalid, CheckCodes.DateMixedFormat], Codes(report));
    }

    [Theory]
    [InlineData("0021-05-01")]
    [InlineData("2101-01-01")]
    [InlineData("1899-12-31")]
    public void Years_outside_1900_to_2100_are_not_real_dates(string date)
    {
        var report = Check($"Найменування;Дата\nА;2025-01-01\nБ;{date}\nВ;2025-01-03\nГ;2025-01-04\nҐ;2025-01-05\nД;2025-01-06\n");

        Assert.Equal([CheckCodes.DateInvalid], Codes(report));
    }

    [Fact]
    public void Iban_check_digits_00_01_and_99_are_never_valid()
    {
        // 01 і 98 дають ту саму остачу за модулем 97, але стандарт видає лише 02-98
        Assert.False(Cells.IsIbanValid("UA01" + "3052990000026007233566001"));
        Assert.False(Cells.IsIbanValid("UA00" + "3052990000026007233566001"));
    }

    [Fact]
    public void Formula_errors_do_not_decide_what_a_column_holds()
    {
        var report = Check("Найменування;Сума\nА;10.50\nБ;#N/A\nВ;#REF!\nГ;12.00\nҐ;13.25\nД;14.00\nЕ;15.75\n");

        Assert.Equal(ColumnKind.Number, report.File!.Columns[1].Kind);
    }

    [Fact]
    public void File_with_only_column_names_is_reported_as_having_no_rows()
    {
        Assert.Equal(FileCheck.HeaderOnly, Check("Номер;Код ЄДРПОУ;Найменування\n").Problem);
    }

    [Fact]
    public void Row_numbers_follow_the_table_not_the_lines_of_the_file()
    {
        // перенесення в клітинці займає зайвий рядок файлу, але рядок таблиці один
        var report = Check(Header + "1;35209058;\"ТОВ\nЗРАЗОК\"\n2;2249063;ТОВ\n" + Rows);

        Assert.Equal(3, Assert.Single(report.Findings, f => f.Code == CheckCodes.EdrpouLeadingZeros).Line);
    }

    [Fact]
    public void One_column_file_does_not_claim_a_separator()
    {
        var text = ReportText.Write(Check("назва\nперша\nдруга\n"), CheckCodes.All);

        Assert.DoesNotContain("Знак між колонками", text);
    }

    [Fact]
    public void Excel_day_number_counts_as_a_date_only_in_a_column_named_as_date()
    {
        string rows = "А;2025-01-01\nБ;45231\nВ;2025-01-03\nГ;2025-01-04\nҐ;2025-01-05\nД;2025-01-06\n";

        Assert.Equal([CheckCodes.ExcelDateSerial], Codes(Check("Найменування;Дата рішення\n" + rows)));
        Assert.DoesNotContain(Check("Найменування;Значення\n" + rows).Findings, f => f.Code == CheckCodes.ExcelDateSerial);
    }

    [Fact]
    public void Region_rows_and_empty_columns_of_a_saved_sheet_are_reported()
    {
        var report = Check("Номер;Код ЄДРПОУ;Найменування;;\nПЕРША ОБЛАСТЬ;;;;\n" + Rows.Replace("\n", ";;\n"));

        Assert.Equal([CheckCodes.EmptyColumns, CheckCodes.SectionRows], Codes(report));
        Assert.Equal([3, 4], report.Findings.Where(f => f.Code == CheckCodes.EmptyColumns).Select(f => f.Column!.Value));
    }

    [Fact]
    public void First_row_with_data_instead_of_names_is_reported()
    {
        var report = Check(Rows);

        Assert.Equal([CheckCodes.HeaderMissing], Codes(report));
        Assert.False(report.File!.HasHeader);
        Assert.Equal(6, report.Rows);
    }

    [Fact]
    public void Quote_left_open_until_the_end_is_reported_at_its_line()
    {
        var report = Check(Header + Rows + "7;35209058;\"ТОВ НЕЗАКРИТО\n8;36066930;ТОВ\n");

        Assert.Equal(8, Assert.Single(report.Findings, f => f.Code == CheckCodes.UnclosedQuote).Line);
    }

    [Fact]
    public void Every_code_has_a_plain_text_without_long_dashes()
    {
        var constants = typeof(CheckCodes).GetFields()
            .Where(f => f.IsLiteral)
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        Assert.Equal(constants.Order(), CheckCodes.All.Keys.Order());
        Assert.All(CheckCodes.All.Values, info =>
        {
            Assert.DoesNotContain("—", info.Text);
            Assert.DoesNotContain("–", info.Text);
            Assert.False(info.Text.EndsWith('.'));
        });
    }
}
