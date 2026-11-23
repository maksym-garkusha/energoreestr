namespace Energoreestr.Check;

/// <summary>
/// Що зберігається в колонці, за назвою і за самими значеннями. Пороги навмисно високі:
/// краще не впізнати колонку і нічого не сказати, ніж налякати зауваженнями до чисел,
/// які лише випадково схожі на коди.
/// </summary>
public static class ColumnKinds
{
    // менше значень замало, щоб судити про колонку
    private const int MinValues = 5;

    public static ColumnKind Guess(string name, IReadOnlyList<string> values)
    {
        if (values.Count < MinValues) return ColumnKind.Unknown;
        string header = name.ToLowerInvariant();
        double Share(Func<string, bool> fits) => values.Count(fits) / (double)values.Count;

        if (Share(v => Cells.EicShape().IsMatch(v)) >= (header.Contains("eic") ? 0.5 : 0.8))
            return ColumnKind.Eic;
        if (Share(v => Cells.IbanShape().IsMatch(Cells.Compact(v))) >= (IsIbanHeader(header) ? 0.5 : 0.8))
            return ColumnKind.Iban;
        if (IsEdrpou(header, values, Share))
            return ColumnKind.Edrpou;
        bool dateHeader = IsDateHeader(name);
        if (Share(v => Dates.PatternOf(v) is not null || (dateHeader && Cells.IsExcelDateSerial(v))) >= 0.8)
            return ColumnKind.Date;
        if (Share(v => Cells.NumberShape().IsMatch(v) || Cells.Exponent().IsMatch(v)) >= 0.9)
            return ColumnKind.Number;
        return ColumnKind.Text;
    }

    /// <summary>Колонка, де за назвою мають бути коди: у ній десять цифр означають РНОКПП.</summary>
    public static bool IsCodeHeader(string name)
    {
        string header = name.ToLowerInvariant();
        return IsEdrpouHeader(header) || header.Contains("код") || header.Contains("іпн")
            || header.Contains("рнокпп") || header.Contains("податков");
    }

    /// <summary>Номер дня Excel (45231) вважаємо датою лише в колонці, яка так і названа.</summary>
    public static bool IsDateHeader(string name)
    {
        string header = name.ToLowerInvariant();
        return header.Contains("дата") || header.Contains("date");
    }

    public static bool IsPhoneHeader(string name)
    {
        string header = name.ToLowerInvariant();
        return header.Contains("телефон") || header.Contains("тел.") || header.Contains("phone");
    }

    // Випадкове ціле проходить контрольний розряд ЄДРПОУ приблизно в одному випадку з десяти,
    // а справжні коди майже завжди. Тому колонка сум чи номерів ліцензій кодом не стане.
    private static bool IsEdrpou(string header, IReadOnlyList<string> values, Func<Func<string, bool>, double> share)
    {
        if (IsEdrpouHeader(header))
            return share(v => Cells.IsEdrpouShaped(v) || IsTenDigits(v)) >= 0.5;
        if (share(Cells.IsEdrpouShaped) < 0.8) return false;

        var plain = values.Where(v => Cells.Digits().IsMatch(v) && v.Length is >= 5 and <= 8).ToList();
        if (plain.Count == 0) return false;
        bool mostlyLong = plain.Count(v => v.Length >= 7) * 2 >= plain.Count;
        bool checksums = plain.Count(v => Codes.Edrpou.IsValid(v.PadLeft(8, '0'))) >= 0.6 * plain.Count;
        return mostlyLong && checksums;
    }

    private static bool IsTenDigits(string value) => value.Count(char.IsAsciiDigit) == 10 && !value.Any(char.IsLetter);

    private static bool IsEdrpouHeader(string header) =>
        header.Contains("єдрпоу") || header.Contains("edrpou") || header.Contains("ідентифікаційний код")
        || header.Contains("код організації");

    private static bool IsIbanHeader(string header) => header.Contains("iban") || header.Contains("рахун");
}
