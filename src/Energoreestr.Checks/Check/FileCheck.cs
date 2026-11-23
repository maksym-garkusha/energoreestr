namespace Energoreestr.Check;

/// <summary>
/// Перевірка довільного файлу CSV без налаштувань: кодування, знак між колонками, заголовок
/// і вміст колонок визначаються з самого файлу. Однакова для команди і для сторінки.
/// </summary>
public static class FileCheck
{
    public const string ExcelFile = "Це файл Excel, а не CSV. Збережіть аркуш як «CSV UTF-8» і перевірте ще раз.";
    public const string NotText = "Це не текстовий файл CSV.";
    public const string Empty = "Файл порожній.";
    public const string UnknownEncoding = "Не вдалося визначити кодування файлу. Збережіть його як «CSV UTF-8» і перевірте ще раз.";

    public static CheckReport Run(byte[] content) => Run(content, DateOnly.FromDateTime(DateTime.Today));

    /// <param name="today">Від нього рахується вік для правила про РНОКПП; у тестах фіксований.</param>
    public static CheckReport Run(byte[] content, DateOnly today)
    {
        if (NotCsv(content) is { } problem) return CheckReport.Unreadable(problem);

        var decoded = Decoding.Decode(content);
        if (decoded.Text is null) return CheckReport.Unreadable(UnknownEncoding);
        if (string.IsNullOrWhiteSpace(decoded.Text)) return CheckReport.Unreadable(Empty);

        return new FileChecker(decoded, today).Run();
    }

    private static string? NotCsv(byte[] content)
    {
        if (content.Length == 0) return Empty;
        // xlsx це zip-архів, старий xls це складений документ OLE
        if (content is [0x50, 0x4B, 0x03, 0x04, ..] || content is [0xD0, 0xCF, 0x11, 0xE0, ..]) return ExcelFile;
        bool utf16 = content is [0xFF, 0xFE, ..] or [0xFE, 0xFF, ..];
        return !utf16 && content.AsSpan().Contains((byte)0) ? NotText : null;
    }
}
