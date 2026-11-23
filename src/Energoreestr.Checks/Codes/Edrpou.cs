namespace Energoreestr.Codes;

/// <summary>Код ЄДРПОУ: 8 цифр, остання контрольна.</summary>
public static class Edrpou
{
    /// <summary>
    /// Повертає код до 8 цифр. Excel зберігає код як число і з'їдає нулі на початку,
    /// тому "3346331" у файлі насправді "03346331".
    /// </summary>
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var digits = raw.Trim();
        if (digits.Length > 8 || !digits.All(char.IsAsciiDigit)) return null;

        return digits.PadLeft(8, '0');
    }

    public static bool IsValid(string code)
    {
        if (code.Length != 8 || !code.All(char.IsAsciiDigit)) return false;

        int number = int.Parse(code);

        // для кодів від 30000000 до 60000000 ваги зсунуті
        int[] weights = number < 30000000 || number > 60000000
            ? [1, 2, 3, 4, 5, 6, 7]
            : [7, 1, 2, 3, 4, 5, 6];

        int check = WeightedSum(code, weights, 0) % 11;
        if (check == 10)
        {
            // друга спроба з вагами, більшими на 2
            check = WeightedSum(code, weights, 2) % 11;
            if (check == 10) check = 0;
        }

        return check == code[7] - '0';
    }

    private static int WeightedSum(string code, int[] weights, int shift)
    {
        int sum = 0;
        for (int i = 0; i < 7; i++)
            sum += (code[i] - '0') * (weights[i] + shift);
        return sum;
    }
}
