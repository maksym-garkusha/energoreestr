using System.Text;

namespace Energoreestr.Check;

/// <param name="Text">null, якщо кодування визначити не вдалося.</param>
/// <param name="Garbled">Чи схожий текст на двічі перекодований.</param>
public record Decoded(string? Text, string Encoding, IReadOnlyList<Finding> Findings, bool Garbled);

/// <summary>
/// Визначає кодування. Для файлів з України реально бувають три: UTF-8 з позначкою BOM
/// чи без неї, Windows-1251 і UTF-16 (Excel, «текст Юнікод»). Інші не пробуємо:
/// кожне зайве кодування дає хибні спрацювання на правильних файлах.
/// </summary>
public static class Decoding
{
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, throwOnInvalidBytes: true);
    private static readonly Encoding Windows1251 = CodePagesEncodingProvider.Instance.GetEncoding(1251)!;

    public static Decoded Decode(byte[] bytes)
    {
        if (StartsWith(bytes, 0xEF, 0xBB, 0xBF))
            return Utf8(Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3), "utf-8-bom", []);
        if (StartsWith(bytes, 0xFF, 0xFE))
            return Utf8(Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2), "utf-16le", [new(CheckCodes.EncodingUtf16, 0)]);
        if (StartsWith(bytes, 0xFE, 0xFF))
            return Utf8(Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2), "utf-16be", [new(CheckCodes.EncodingUtf16, 0)]);

        try
        {
            string text = StrictUtf8.GetString(bytes);
            // для файлу з самих латинських літер і цифр позначка нічого не змінює
            List<Finding> findings = bytes.Any(b => b > 0x7F) ? [new(CheckCodes.Utf8NoBom, 0)] : [];
            return Utf8(text, "utf-8", findings);
        }
        catch (DecoderFallbackException)
        {
            string text = Windows1251.GetString(bytes);
            return MostlyCyrillic(text)
                ? new Decoded(text, "windows-1251", [new(CheckCodes.Encoding1251, 0)], false)
                : new Decoded(null, "unknown", [], false);
        }
    }

    private static Decoded Utf8(string text, string encoding, List<Finding> findings) =>
        new(text, encoding, findings, Mojibake.LooksGarbled(text));

    private static bool StartsWith(byte[] bytes, params byte[] prefix) =>
        bytes.Length >= prefix.Length && bytes.AsSpan(0, prefix.Length).SequenceEqual(prefix);

    // Якщо файл не в UTF-8 і не в Windows-1251, у 1251 він однаково «прочитається»,
    // але замість кирилиці будуть розділові знаки й рідкісні літери
    private static bool MostlyCyrillic(string text)
    {
        int nonAscii = 0, cyrillic = 0;
        foreach (char c in text)
        {
            if (c <= 0x7F) continue;
            nonAscii++;
            if (c is >= 'А' and <= 'я' or 'І' or 'і' or 'Ї' or 'ї' or 'Є' or 'є' or 'Ґ' or 'ґ') cyrillic++;
        }
        return nonAscii == 0 || cyrillic * 2 >= nonAscii;
    }
}

/// <summary>
/// Текст у UTF-8, який хтось прочитав як Windows-1251 і знову зберіг: «ТОВ» стає «РўРћР’».
/// Кожна кирилична літера перетворюється на «Р» або «С» і знак з верхньої половини таблиці 1251.
/// </summary>
public static class Mojibake
{
    // Знаки, які в такому тексті стоять після «Р» чи «С». Українські літери І, Ї, Є, Ґ
    // і закривні лапки сюди не входять: «РІВНЕ» чи «СЕРВІС»» це звичайний текст.
    private const string Suspicious =
        "ЂЃ‚ѓ„…†‡€‰Љ‹ЊЌЋЏ" +
        "ђ‘“•–—™љ›њќћџ" +
        " ЎўЈ¤¦§Ё©«¬­®" +
        "°±µ¶·ё№јЅѕ";

    private static readonly char[] Separators = [' ', '\t', '\r', '\n', ';', ',', '"'];

    public static bool LooksGarbled(string text)
    {
        int cyrillicWords = 0, garbled = 0;
        foreach (var word in text.Split(Separators, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!word.Any(c => c is >= 'Ѐ' and <= 'ӿ')) continue;
            cyrillicWords++;
            if (IsGarbled(word)) garbled++;
        }
        // кілька таких слів можуть бути випадковістю, тож потрібна і кількість, і частка
        return garbled >= 3 && garbled * 10 >= cyrillicWords;
    }

    public static bool HasGarbledWord(string value) =>
        value.Split(Separators, StringSplitOptions.RemoveEmptyEntries).Any(IsGarbled);

    private static bool IsGarbled(string word)
    {
        int pairs = 0;
        for (int i = 0; i + 1 < word.Length; i++)
        {
            if (word[i] is 'Р' or 'С' && Suspicious.Contains(word[i + 1])) pairs++;
        }
        return pairs >= 2;
    }
}
