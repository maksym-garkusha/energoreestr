namespace Energoreestr.Registers;

/// <summary>Зауваження до одного рядка файлу.</summary>
/// <param name="Line">Номер рядка у файлі.</param>
/// <param name="Key">За чим цей запис можна знайти в реєстрі (код реєстрації, ЄДРПОУ або назва).</param>
/// <param name="Code">Вид зауваження, одна з констант <see cref="IssueCodes"/>.</param>
/// <param name="Value">Значення з файлу, яке викликало зауваження.</param>
public record RowIssue(int Line, string Key, string Code, string? Value);

public static class IssueCodes
{
    // будова рядка
    public const string RowRearranged = "row-rearranged";
    public const string RowAmbiguous = "row-ambiguous";
    public const string RowUnreadable = "row-unreadable";
    public const string DuplicateKey = "duplicate-key";

    // коди
    public const string EdrpouChecksum = "edrpou-checksum";
    public const string EdrpouLeadingZeros = "edrpou-leading-zeros";
    public const string IdCodeFormat = "id-code-format";
    public const string EicFormat = "eic-format";
    public const string EicChecksum = "eic-checksum";
    public const string BicFormat = "bic-format";
    public const string LeiFormat = "lei-format";

    // значення
    public const string DateFormat = "date-format";
    public const string UrlFormat = "url-format";
    public const string DashPlaceholder = "dash-placeholder";
    public const string MixedAlphabet = "mixed-alphabet";

    public static readonly IReadOnlyDictionary<string, string> Descriptions = new Dictionary<string, string>
    {
        [RowRearranged] = "Коми всередині значень не взяті в лапки, межі колонок довелося підбирати",
        [RowAmbiguous] = "Межі колонок підібрано, але підходило кілька варіантів",
        [RowUnreadable] = "Рядок не вдалося розкласти на колонки, запис пропущено",
        [DuplicateKey] = "Запис повторює інший рядок файлу, взято перший",
        [EdrpouChecksum] = "Код ЄДРПОУ не проходить перевірку контрольного розряду, варто звірити з ЄДР",
        [EdrpouLeadingZeros] = "У коді ЄДРПОУ загублено нулі на початку",
        [IdCodeFormat] = "Ідентифікаційний код не схожий ні на ЄДРПОУ, ні на РНОКПП",
        [EicFormat] = "EIC-код не відповідає формату (16 символів: цифри, великі латинські літери, дефіс)",
        [EicChecksum] = "EIC-код не проходить перевірку контрольного символу",
        [BicFormat] = "BIC не відповідає формату (8 або 11 символів)",
        [LeiFormat] = "LEI не відповідає формату (20 символів)",
        [DateFormat] = "Дата записана не у форматі РРРР-ММ-ДД",
        [UrlFormat] = "Адреса сайту з пробілами, без домену або це взагалі не адреса",
        [DashPlaceholder] = "Замість порожнього значення стоїть дефіс",
        [MixedAlphabet] = "В одному слові змішано кирилицю і латиницю"
    };
}
