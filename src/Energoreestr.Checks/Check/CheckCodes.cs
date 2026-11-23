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
    public const string ValueTooLong = "value-too-long";
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

    /// <summary>
    /// Зауваження, яких буває багато і які не потребують переліку рядків: у тексті звіту
    /// вони зведені до кількості клітинок у кожній колонці, номери рядків лишаються в JSON.
    /// </summary>
    public static readonly IReadOnlySet<string> CountedByColumn = new HashSet<string> { Whitespace };

    /// <summary>
    /// Усі види зауважень у тому порядку, в якому їх показуємо всередині рівня: кодування,
    /// знак між колонками і лапки, будова таблиці, значення.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, CodeInfo> All = Numbered(
    [
        (Encoding1251, Level.Fix, Unit.File, "Старе кодування Windows-1251",
            "Файл збережено в старому кодуванні Windows-1251, тож в інших програмах замість літер можуть бути незрозумілі знаки. Збережіть його в кодуванні UTF-8."),
        (EncodingUtf16, Level.Fix, Unit.File, "Кодування UTF-16",
            "Файл збережено в кодуванні UTF-16 (так Excel зберігає «текст Юнікод»). Збережіть його як «CSV UTF-8»."),
        (EncodingMojibake, Level.Fix, Unit.Cell, "Текст перекодовано двічі",
            "Замість літер стоять знаки на кшталт «РўРћР’»: файл, найімовірніше, двічі збережено в різних кодуваннях. Виправити такий текст у файлі вже не вийде, візьміть дані з першоджерела."),
        (Utf8NoBom, Level.Info, Unit.File, "UTF-8 без позначки BOM",
            "Файл у кодуванні UTF-8 без службової позначки BOM на початку. Для програм це не вада, але Excel, якщо відкрити файл подвійним клацанням, може показати замість літер незрозумілі знаки."),
        (SingleColumn, Level.Verify, Unit.File, "Лише одна колонка",
            "У файлі знайдено лише одну колонку. Можливо, колонки розділено незвичним знаком, а не комою чи крапкою з комою: збережіть файл як «CSV UTF-8» ще раз."),
        (UnquotedDelimiter, Level.Fix, Unit.Row, "Зайві значення в рядку",
            "У рядку більше значень, ніж назв колонок у першому рядку: значення з комою (чи іншим знаком, яким розділено колонки) не взято в лапки. Візьміть такі значення в лапки або збережіть файл з Excel ще раз."),
        (MissingFields, Level.Fix, Unit.Row, "Бракує значень у рядку",
            "У рядку менше значень, ніж назв колонок у першому рядку. Перевірте, чи не загубилася частина рядка."),
        (UnclosedQuote, Level.Fix, Unit.Row, "Незакриті лапки",
            "Лапки, відкриті в зазначеному рядку, далі ніде не закрито, тому все після них прочитано як одне значення. Знайдіть у цьому рядку клітинку з непарною кількістю лапок."),
        (ValueTooLong, Level.Fix, Unit.Row, "Надто довге значення",
            "У рядку є значення довше за 100 тисяч знаків, тому перевірку цього рядка пропущено. Найчастіше це зламаний рядок: перевірте лапки в ньому."),
        (HeaderMissing, Level.Verify, Unit.Row, "Немає назв колонок",
            "Перший рядок схожий на дані, а не на назви колонок. Додайте зверху рядок з назвами."),
        (HeaderEmpty, Level.Fix, Unit.Column, "Колонка без назви",
            "Колонка має дані, але не має назви в першому рядку. Допишіть назву."),
        (HeaderDuplicate, Level.Fix, Unit.Column, "Однакові назви колонок",
            "Дві колонки мають однакову назву. Дайте кожній колонці свою назву."),
        (EmptyColumns, Level.Fix, Unit.Column, "Порожні колонки",
            "Колонки без назви і без жодного значення: аркуш збережено разом з порожніми клітинками праворуч. Виділіть ці колонки в Excel, видаліть їх і збережіть файл знову."),
        (SectionRows, Level.Fix, Unit.Row, "Рядки-розділи посеред таблиці",
            "Посеред таблиці стоїть рядок з однією заповненою клітинкою (наприклад, назвою області). Перенесіть це значення в окрему колонку для кожного рядка нижче, а сам рядок видаліть."),
        (DuplicateRow, Level.Verify, Unit.Row, "Повтори рядків",
            "Рядок повністю повторює інший рядок файлу. Якщо це не два різні записи, видаліть повтор."),
        (PersonalTaxNumber, Level.Verify, Unit.Row, "Номери, схожі на РНОКПП",
            "Десять цифр, схожих на РНОКПП (податковий номер людини). Перед публікацією переконайтеся, що ці дані можна оприлюднювати. Значення не показано."),
        (PersonalSoleProprietor, Level.Verify, Unit.Row, "Згадано підприємця",
            "Згадано фізичну особу-підприємця. Перед публікацією переконайтеся, що ці дані можна оприлюднювати. Значення не показано."),
        (EdrpouLeadingZeros, Level.Fix, Unit.Cell, "Загублені нулі в ЄДРПОУ",
            "Код ЄДРПОУ коротший за 8 цифр: нулі на початку загубилися, бо Excel вважав колонку числами. Допишіть нулі зліва до 8 цифр і перед збереженням задайте колонці формат «Текстовий»."),
        (EdrpouChecksum, Level.Verify, Unit.Cell, "Контрольна цифра ЄДРПОУ",
            "Остання цифра коду ЄДРПОУ не збігається з тією, що виходить з перших семи (так ловлять описки). Звірте код з ЄДР."),
        (EdrpouFormat, Level.Verify, Unit.Cell, "Не схоже на код ЄДРПОУ",
            "Значення в колонці кодів ЄДРПОУ не схоже на код. Звірте його з ЄДР."),
        (EicFormat, Level.Fix, Unit.Cell, "Неправильний вигляд EIC",
            "EIC-код не відповідає формату: має бути 16 символів, лише цифри, великі латинські літери і дефіс. Звірте код з першоджерелом."),
        (EicChecksum, Level.Verify, Unit.Cell, "Контрольна цифра EIC",
            "Останній символ EIC-коду не збігається з тим, що виходить з перших п’ятнадцяти (так ловлять описки). Звірте код з першоджерелом."),
        (IbanChecksum, Level.Verify, Unit.Cell, "Контрольні цифри IBAN",
            "Контрольні цифри IBAN (третя і четверта) не збігаються з рештою номера (так ловлять описки). Звірте рахунок з першоджерелом."),
        (ExcelExponent, Level.Fix, Unit.Cell, "Скорочені числа Excel",
            "Excel скоротив довге число до вигляду 1,23E+11, і частину цифр утрачено. Візьміть ці значення з першоджерела, задайте колонці формат «Текстовий» і вставте їх знову."),
        (ExcelTrailingZero, Level.Fix, Unit.Cell, "«.0» в кінці коду",
            "До коду дописано «.0», бо колонка мала формат дробового числа. Задайте колонці формат «Текстовий» і приберіть «.0» в кінці значень."),
        (ExcelDateSerial, Level.Fix, Unit.Cell, "Номер дня замість дати",
            "Замість дати записано її порядковий номер у Excel (наприклад, 45231). Задайте колонці формат «Дата» і збережіть файл знову."),
        (ExcelError, Level.Fix, Unit.Cell, "Помилки формул Excel",
            "У клітинці лишилася помилка формули Excel (#N/A, #REF! тощо). Виправте формулу або замініть її значенням."),
        (DateInvalid, Level.Fix, Unit.Cell, "Неіснуючі дати",
            "Такої дати не існує або рік поза 1900-2100. Звірте дату з першоджерелом."),
        (DateMixedFormat, Level.Fix, Unit.Cell, "Різний вигляд дат",
            "Дату записано не в тому вигляді, що більшість дат у цій колонці. Запишіть усі дати однаково, найкраще як РРРР-ММ-ДД."),
        (DateNotIso, Level.Info, Unit.Column, "Дати як ДД.ММ.РРРР",
            "Дати записано як ДД.ММ.РРРР. У вигляді РРРР-ММ-ДД програми читають їх без здогадок про порядок дня й місяця."),
        (NumberMixedSeparator, Level.Verify, Unit.Cell, "Різні дробові знаки",
            "Дробову частину відокремлено не тим знаком, що в більшості чисел колонки (крапка чи кома). Запишіть усі числа однаково."),
        (MixedAlphabet, Level.Fix, Unit.Cell, "Латинські літери в словах",
            "В одному слові є і українські, і латинські літери (наприклад, латинська «A» замість української «А»). На око різниці не видно, але пошук такий запис не знайде. Наберіть слово заново українською розкладкою."),
        (PlaceholderValue, Level.Info, Unit.Cell, "Заглушки замість порожнього",
            "Замість порожньої клітинки стоїть «-», «null» чи подібна заглушка. Якщо значення немає, залиште клітинку порожньою."),
        (Whitespace, Level.Info, Unit.Cell, "Зайві пробіли",
            "Зайві пробіли на початку чи в кінці значення або два пробіли поспіль. Пошук і порівняння можуть через них не знайти запис."),
        (LineBreakInValue, Level.Info, Unit.Cell, "Перенесення в клітинках",
            "Усередині клітинки є перенесення рядка: частина програм прочитає такий запис як кілька рядків. Замініть перенесення пробілом.")
    ]);

    private static Dictionary<string, CodeInfo> Numbered((string Code, Level Level, Unit Unit, string Title, string Text)[] entries) =>
        entries.Select((e, order) => (e, order)).ToDictionary(x => x.e.Code, x => new CodeInfo(x.e.Level, x.e.Text, x.e.Title, x.e.Unit, x.order));
}
