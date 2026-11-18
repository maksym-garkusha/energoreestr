using Energoreestr.Codes;

namespace Energoreestr.Tests;

public class CodesTests
{
    [Theory]
    [InlineData("42750837")]   // код до 30000000..60000000, зсунуті ваги
    [InlineData("03346331")]   // Київгаз, код менший за 30000000
    [InlineData("00100227")]   // Укренерго
    [InlineData("20077720")]   // Нафтогаз
    public void Edrpou_accepts_real_codes(string code)
    {
        Assert.True(Edrpou.IsValid(code));
    }

    [Theory]
    [InlineData("42750838")]   // остання цифра змінена
    [InlineData("03346313")]   // дві цифри переставлені місцями
    [InlineData("3346331")]    // сім цифр
    [InlineData("0334633A")]
    public void Edrpou_rejects_broken_codes(string code)
    {
        Assert.False(Edrpou.IsValid(code));
    }

    [Theory]
    [InlineData("3346331", "03346331")]
    [InlineData(" 135390 ", "00135390")]
    [InlineData("42750837", "42750837")]
    public void Edrpou_normalize_restores_leading_zeros(string raw, string expected)
    {
        Assert.Equal(expected, Edrpou.Normalize(raw));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1234567890")]   // 10 цифр це вже РНОКПП
    [InlineData("35546472.")]
    public void Edrpou_normalize_returns_null_for_other_things(string raw)
    {
        Assert.Null(Edrpou.Normalize(raw));
    }

    [Theory]
    [InlineData("10X1001C--00001X")]   // Укренерго
    [InlineData("62X529473093477D")]
    [InlineData("21X0000000013279")]   // Укртрансгаз
    [InlineData("24X-PJSC-DOENE-K")]   // з дефісами
    public void Eic_accepts_real_codes(string code)
    {
        Assert.True(Eic.IsValid(code));
    }

    [Theory]
    [InlineData("62X1580751879965")]   // з реєстру, контрольний символ не сходиться
    [InlineData("56X93000011558OL")]   // літера O замість нуля
    public void Eic_rejects_wrong_check_character(string code)
    {
        Assert.True(Eic.HasValidFormat(code));
        Assert.False(Eic.IsValid(code));
    }

    [Theory]
    [InlineData("62z000000000123Y")]   // мала літера, синтетичний код
    [InlineData("62X863954335972")]    // 15 символів
    [InlineData("56XO000172D5700U, 56XO000172D4R00T")]
    public void Eic_rejects_wrong_format(string code)
    {
        Assert.False(Eic.HasValidFormat(code));
        Assert.False(Eic.IsValid(code));
    }
}
