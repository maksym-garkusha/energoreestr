using Energoreestr.Registers;

namespace Energoreestr.Tests;

public class SoleProprietorTests
{
    [Theory]
    [InlineData("ФОП Вигаданий Іван Іванович")]
    [InlineData("фоп Вигаданий І. І.")]
    [InlineData("ВИГАДАНИЙ ІВАН (ФОП)")]
    [InlineData("Фізична особа-підприємець")]
    [InlineData("ФIЗИЧНА ОСОБА-ПІДПРИЄМЕЦЬ ВИГАДАНИЙ")]     // латинська I
    [InlineData("фізична  особа - підприємець Вигаданий")]
    public void Recognises_a_person_by_the_marker_in_name_or_legal_form(string text)
    {
        Assert.True(SoleProprietor.IsNamedAsOne(text));
    }

    [Theory]
    [InlineData("ТОВАРИСТВО З ОБМЕЖЕНОЮ ВІДПОВІДАЛЬНІСТЮ \"ФОПТЕХ\"")]   // ФОП усередині слова
    [InlineData("ПП \"ПРОМЕТЕЙ 2000\"")]                                 // ПП це приватне підприємство
    [InlineData("Товариство з обмеженою відповідальністю")]
    [InlineData("ГРОМАДСЬКА ОРГАНІЗАЦІЯ ФІЗИЧНИХ ОСІБ")]
    [InlineData("")]
    public void Organisation_is_not_taken_for_a_person(string text)
    {
        Assert.False(SoleProprietor.IsNamedAsOne(text));
    }

    [Theory]
    [InlineData("1234567899", true)]
    [InlineData(" 1234567899 ", true)]
    [InlineData("123 456 7899", true)]
    [InlineData("123-456-7899", true)]
    [InlineData("1234567899.", true)]
    [InlineData("12345678", false)]
    [InlineData("35546472.", false)]
    [InlineData("123456789", false)]
    [InlineData("12345678901", false)]
    [InlineData("UA1234567899", false)]
    [InlineData("HE 276280", false)]
    public void Ten_digits_in_any_spelling_are_a_tax_number(string code, bool expected)
    {
        Assert.Equal(expected, SoleProprietor.IsTaxNumber(code));
    }
}
