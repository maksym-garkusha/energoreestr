namespace Energoreestr.Check;

/// <summary>
/// Види зауважень загальної перевірки. Тексти про файл, а не про того, хто його готував:
/// їх читає людина без технічної освіти, тож кожне незвичне слово пояснено на місці.
/// </summary>
public static class CheckCodes
{
    // кодування
    public const string Encoding1251 = "encoding-1251";
    public const string EncodingUtf16 = "encoding-utf16";
    public const string EncodingMojibake = "encoding-mojibake";
    public const string Utf8NoBom = "utf8-no-bom";

    // будова таблиці
    public const string SingleColumn = "single-column";
    public const string UnquotedDelimiter = "unquoted-delimiter";
    public const string MissingFields = "missing-fields";
    public const string UnclosedQuote = "unclosed-quote";
    public const string EmptyColumns = "empty-columns";
    public const string SectionRows = "section-rows";
    public const string HeaderMissing = "header-missing";
    public const string HeaderEmpty = "header-empty";
    public const string HeaderDuplicate = "header-duplicate";
    public const string DuplicateRow = "duplicate-row";

    // коди
    public const string EdrpouLeadingZeros = "edrpou-leading-zeros";
    public const string EdrpouChecksum = "edrpou-checksum";
    public const string EdrpouFormat = "edrpou-format";
    public const string EicFormat = "eic-format";
    public const string EicChecksum = "eic-checksum";
    public const string IbanChecksum = "iban-checksum";

    // сліди Excel
    public const string ExcelExponent = "excel-exponent";
    public const string ExcelTrailingZero = "excel-trailing-zero";
    public const string ExcelDateSerial = "excel-date-serial";
    public const string ExcelError = "excel-error";

    // значення
    public const string DateInvalid = "date-invalid";
    public const string DateMixedFormat = "date-mixed-format";
    public const string DateNotIso = "date-not-iso";
    public const string NumberMixedSeparator = "number-mixed-separator";
    public const string MixedAlphabet = "mixed-alphabet";
    public const string PlaceholderValue = "placeholder-value";
    public const string Whitespace = "whitespace";
    public const string LineBreakInValue = "line-break-in-value";

    // персональні дані
    public const string PersonalTaxNumber = "personal-tax-number";
    public const string PersonalSoleProprietor = "personal-sole-proprietor";

    public static readonly IReadOnlyDictionary<string, CodeInfo> All = new Dictionary<string, CodeInfo>
    {
        [Encoding1251] = new(Level.Fix, "Файл збережено в старому кодуванні Windows-1251: збережіть його в UTF-8, щоб літери однаково читалися в усіх програмах"),
        [EncodingUtf16] = new(Level.Fix, "Файл збережено в кодуванні UTF-16 (так Excel зберігає «текст Юнікод»): збережіть його як CSV UTF-8"),
        [EncodingMojibake] = new(Level.Fix, "Замість літер стоять знаки на кшталт «РўРћР’»: схоже, файл двічі перекодовано, і текст варто взяти з першоджерела"),
        [Utf8NoBom] = new(Level.Info, "Файл у кодуванні UTF-8, але без службової позначки на початку (BOM): сам файл правильний, проте Excel при відкритті подвійним кліком може показати літери неправильно"),

        [SingleColumn] = new(Level.Verify, "У файлі знайдено лише одну колонку: можливо, колонки розділено незвичним знаком, а не комою чи крапкою з комою"),
        [UnquotedDelimiter] = new(Level.Fix, "У рядку більше значень, ніж назв колонок у першому рядку: схоже, значення з комою (чи іншим знаком, яким розділено колонки) не взято в лапки"),
        [MissingFields] = new(Level.Fix, "У рядку менше значень, ніж назв колонок у першому рядку"),
        [UnclosedQuote] = new(Level.Fix, "Лапки, відкриті в цьому рядку, ніде не закрито, тому решту файлу прочитано як одне значення"),
        [EmptyColumns] = new(Level.Fix, "Колонки без назви і без жодного значення: схоже, аркуш збережено разом з порожніми клітинками праворуч"),
        [SectionRows] = new(Level.Fix, "Посеред таблиці стоїть рядок з однією заповненою клітинкою (наприклад, назвою області): таке значення краще винести в окрему колонку"),
        [HeaderMissing] = new(Level.Verify, "Перший рядок схожий на дані, а не на назви колонок"),
        [HeaderEmpty] = new(Level.Fix, "Колонка має дані, але не має назви в першому рядку"),
        [HeaderDuplicate] = new(Level.Fix, "Дві колонки мають однакову назву"),
        [DuplicateRow] = new(Level.Verify, "Рядок повністю повторює інший рядок файлу"),

        [EdrpouLeadingZeros] = new(Level.Fix, "Код ЄДРПОУ коротший за 8 цифр: схоже, нулі на початку загубилися, бо колонку збережено як число"),
        [EdrpouChecksum] = new(Level.Verify, "Код ЄДРПОУ не проходить перевірку контрольної цифри, варто звірити з ЄДР"),
        [EdrpouFormat] = new(Level.Verify, "Значення в колонці кодів ЄДРПОУ не схоже на код"),
        [EicFormat] = new(Level.Fix, "EIC-код не відповідає формату: має бути 16 символів, лише цифри, великі латинські літери і дефіс"),
        [EicChecksum] = new(Level.Verify, "EIC-код не проходить перевірку контрольного символу, варто звірити"),
        [IbanChecksum] = new(Level.Verify, "Номер рахунку IBAN не проходить перевірку контрольних цифр, варто звірити"),

        [ExcelExponent] = new(Level.Fix, "Число збережено у скороченому вигляді на кшталт 1,23E+11: частину цифр із файлу вже не відновити"),
        [ExcelTrailingZero] = new(Level.Fix, "До коду дописано «.0»: колонку збережено як дробове число"),
        [ExcelDateSerial] = new(Level.Fix, "Замість дати записано її порядковий номер у Excel (наприклад, 45231)"),
        [ExcelError] = new(Level.Fix, "У клітинці лишилася помилка формули Excel (#N/A, #REF! тощо)"),

        [DateInvalid] = new(Level.Fix, "Такої дати не існує"),
        [DateMixedFormat] = new(Level.Fix, "Дату записано не в тому вигляді, що більшість дат у цій колонці"),
        [DateNotIso] = new(Level.Info, "Дати записано як ДД.ММ.РРРР; у вигляді РРРР-ММ-ДД програми читають їх без здогадок"),
        [NumberMixedSeparator] = new(Level.Verify, "Дробову частину відокремлено не тим знаком, що в більшості чисел колонки (крапка чи кома)"),
        [MixedAlphabet] = new(Level.Fix, "В одному слові змішано українські й латинські літери: на око цього не видно, але пошук такий запис не знайде"),
        [PlaceholderValue] = new(Level.Info, "Замість порожньої клітинки стоїть «-», «null» чи подібна заглушка"),
        [Whitespace] = new(Level.Info, "Зайві пробіли на початку чи в кінці значення або два пробіли поспіль"),
        [LineBreakInValue] = new(Level.Info, "Усередині клітинки є перенесення рядка: частина програм прочитає такий запис як кілька рядків"),

        [PersonalTaxNumber] = new(Level.Verify, "Десять цифр, схожих на РНОКПП (податковий номер людини): перед публікацією варто переконатися, що ці дані можна оприлюднювати. Значення не показано"),
        [PersonalSoleProprietor] = new(Level.Verify, "Згадано фізичну особу-підприємця: перед публікацією варто переконатися, що ці дані можна оприлюднювати. Значення не показано")
    };
}
