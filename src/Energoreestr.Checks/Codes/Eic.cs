namespace Energoreestr.Codes;

/// <summary>
/// EIC, код учасника енергоринку за схемою ENTSO-E: 16 символів (цифри, латинські
/// літери, дефіс), останній контрольний.
/// </summary>
public static class Eic
{
    public const int Length = 16;

    public static bool HasValidFormat(string code)
    {
        if (code.Length != Length) return false;

        foreach (char c in code)
        {
            bool allowed = char.IsAsciiDigit(c) || (c >= 'A' && c <= 'Z') || c == '-';
            if (!allowed) return false;
        }
        return true;
    }

    public static bool IsValid(string code)
    {
        if (!HasValidFormat(code)) return false;

        // перші 15 символів множаться на ваги 16, 15, ... 2
        int sum = 0;
        for (int i = 0; i < Length - 1; i++)
            sum += Value(code[i]) * (Length - i);

        int check = 36 - (sum - 1) % 37;
        return check == Value(code[Length - 1]);
    }

    // 0-9 це 0..9, A-Z це 10..35, дефіс 36
    private static int Value(char c)
    {
        if (char.IsAsciiDigit(c)) return c - '0';
        if (c == '-') return 36;
        return c - 'A' + 10;
    }
}
