using System.Text.RegularExpressions;
using Energoreestr.Text;

namespace Energoreestr.Registers;

/// <summary>
/// Як упізнати запис про фізичну особу-підприємця. У такому записі ім'я людини,
/// її РНОКПП і часто домашня адреса, тому розбір файлу його пропускає і рахує
/// тільки кількість. Зауваження до рядка лишаються, але без значень.
/// </summary>
public static partial class SoleProprietor
{
    /// <summary>Що стоїть у зауваженні замість коду чи назви запису.</summary>
    public const string Key = "фізична особа-підприємець";

    /// <summary>
    /// Десять цифр, як у РНОКПП. Код набирають руками: «123 456 7890», «123-456-7890»
    /// і «1234567890.» це той самий номер, тому рахуємо тільки цифри.
    /// </summary>
    public static bool IsTaxNumber(string? code) =>
        code is not null && code.Count(char.IsAsciiDigit) == 10 && !code.Any(char.IsLetter);

    /// <summary>
    /// «ФОП» окремим словом або «фізична особа» в будь-якому регістрі. Назву спершу
    /// приводимо до ключа пошуку, бо в реєстрах трапляється «ФIЗИЧНА» з латинською I.
    /// </summary>
    public static bool IsNamedAsOne(string? text) =>
        !string.IsNullOrWhiteSpace(text) && Marker().IsMatch(Names.SearchKey(text));

    // кількісники обмежені: на довгому рядку з «ФІЗИЧН» без «ОСОБ» необмежені давали квадратичний час
    [GeneratedRegex(@"(?:^|[^\p{L}])ФОП(?:[^\p{L}]|$)|ФІЗИЧН\p{L}{0,4}[ -]{0,3}ОСОБ")]
    private static partial Regex Marker();
}
