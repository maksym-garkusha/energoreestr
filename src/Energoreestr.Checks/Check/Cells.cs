using System.Globalization;
using System.Text.RegularExpressions;

namespace Energoreestr.Check;

/// <summary>Як виглядають окремі значення: коди, дати, числа, сліди Excel.</summary>
public static partial class Cells
{
    public static bool IsPlaceholder(string value) =>
        value is "-" or "null" or "NULL" or "Null" or "N/A" or "n/a" or "н/д" or "Н/Д";

    // англійські, українські й російські назви помилок, бо Excel пише їх мовою інтерфейсу
    [GeneratedRegex(@"^#(N/A|REF!|VALUE!|DIV/0!|NAME\?|NUM!|NULL!|Н/Д|ЗНАЧ!|ПОСИЛАННЯ!|ДІЛ/0!|ІМ'Я\?|ЧИСЛО!|ССЫЛКА!|ДЕЛ/0!|ИМЯ\?|ПУСТО!)$")]
    public static partial Regex ExcelError();

    // 1,23457E+11: так Excel зберігає довге число, і цифри після шостої вже втрачено
    [GeneratedRegex(@"^-?\d+([.,]\d+)?[Ee]\+\d+$")]
    public static partial Regex Exponent();

    [GeneratedRegex(@"^\d+\.0$")]
    public static partial Regex TrailingZero();

    [GeneratedRegex(@"^\d+$")]
    public static partial Regex Digits();

    [GeneratedRegex(@"^\d{2}[A-Za-z][0-9A-Za-z-]{13}$")]
    public static partial Regex EicShape();

    [GeneratedRegex(@"^UA\d{27}$", RegexOptions.IgnoreCase)]
    public static partial Regex IbanShape();

    [GeneratedRegex(@"^[-+]?(\d+|\d{1,3}([  ]\d{3})+)([.,]\d+)?$")]
    public static partial Regex NumberShape();

    public static string Compact(string value) => value.Replace(" ", "").Replace(" ", "");

    public static bool IsEdrpouShaped(string value) =>
        (Digits().IsMatch(value) && value.Length is >= 5 and <= 8) || TrailingZero().IsMatch(value) || Exponent().IsMatch(value);

    // Excel рахує дні від 1900 року: 20000 це 1954 рік, 73050 це 2099
    public static bool IsExcelDateSerial(string value) =>
        value.Length == 5 && Digits().IsMatch(value) && int.Parse(value, CultureInfo.InvariantCulture) is >= 20000 and <= 73050;

    public static bool IsIbanValid(string compact)
    {
        string moved = compact[4..] + compact[..4];
        int remainder = 0;
        foreach (char c in moved.ToUpperInvariant())
        {
            // літера A це 10, B це 11 і так далі
            int value = char.IsAsciiDigit(c) ? c - '0' : c - 'A' + 10;
            remainder = (value > 9 ? remainder * 100 + value : remainder * 10 + value) % 97;
        }
        return remainder == 1;
    }

    /// <summary>Знак дробової частини: крапка, кома або null, якщо число ціле.</summary>
    public static char? DecimalSeparator(string value)
    {
        int at = value.LastIndexOfAny(['.', ',']);
        return at < 0 ? null : value[at];
    }

    /// <summary>
    /// Десять цифр, що проходять контрольну цифру РНОКПП, не починаються з нуля (так
    /// відсіюються телефони 0441234567) і за першими п'ятьма цифрами дають вік від 14 до 100 років.
    /// </summary>
    public static bool IsPlausibleTaxNumber(string value, DateOnly today)
    {
        string digits = new(value.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length != 10 || digits[0] == '0' || value.Any(char.IsLetter)) return false;

        int[] weights = [-1, 5, 7, 9, 4, 6, 10, 5, 7];
        int sum = 0;
        for (int i = 0; i < 9; i++) sum += (digits[i] - '0') * weights[i];
        if (sum % 11 % 10 != digits[9] - '0') return false;

        // перші п'ять цифр це кількість днів від 31.12.1899 до дня народження
        var born = new DateOnly(1899, 12, 31).AddDays(int.Parse(digits[..5], CultureInfo.InvariantCulture));
        return born <= today.AddYears(-14) && born >= today.AddYears(-100);
    }
}

/// <summary>Вигляд дати. Різні вигляди в одній колонці це зауваження.</summary>
public enum DatePattern { Iso, IsoWithTime, Dmy, DmyWithTime }

public static partial class Dates
{
    [GeneratedRegex(@"^(\d{4}-\d{2}-\d{2})$")]
    private static partial Regex Iso();

    [GeneratedRegex(@"^(\d{4}-\d{2}-\d{2})[ T](\d{2}:\d{2}(:\d{2})?)$")]
    private static partial Regex IsoWithTime();

    [GeneratedRegex(@"^(\d{2}\.\d{2}\.\d{4})$")]
    private static partial Regex Dmy();

    [GeneratedRegex(@"^(\d{2}\.\d{2}\.\d{4}) (\d{2}:\d{2}(:\d{2})?)$")]
    private static partial Regex DmyWithTime();

    public static DatePattern? PatternOf(string value)
    {
        if (Iso().IsMatch(value)) return DatePattern.Iso;
        if (IsoWithTime().IsMatch(value)) return DatePattern.IsoWithTime;
        if (Dmy().IsMatch(value)) return DatePattern.Dmy;
        if (DmyWithTime().IsMatch(value)) return DatePattern.DmyWithTime;
        return null;
    }

    /// <summary>Чи існує така дата (і час, якщо є).</summary>
    public static bool IsReal(string value, DatePattern pattern)
    {
        var (regex, format) = pattern switch
        {
            DatePattern.Iso => (Iso(), "yyyy-MM-dd"),
            DatePattern.IsoWithTime => (IsoWithTime(), "yyyy-MM-dd"),
            DatePattern.Dmy => (Dmy(), "dd.MM.yyyy"),
            _ => (DmyWithTime(), "dd.MM.yyyy")
        };
        var match = regex.Match(value);
        if (!DateOnly.TryParseExact(match.Groups[1].Value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            return false;
        return match.Groups[2].Value is not { Length: > 0 } time
            || TimeOnly.TryParseExact(time, ["HH:mm", "HH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    }
}
