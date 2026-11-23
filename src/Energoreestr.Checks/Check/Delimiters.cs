using Energoreestr.Csv;

namespace Energoreestr.Check;

/// <summary>Яким знаком розділено колонки.</summary>
internal static class Delimiters
{
    // порядок важить лише за однакового результату
    private static readonly char[] Candidates = [';', ',', '\t', '|'];
    private const int SampleLines = 200;

    /// <summary>
    /// Читає початок файлу кожним знаком (з урахуванням лапок) і бере той, за яким найбільше
    /// рядків мають однакову кількість полів. null, якщо жоден знак не дає хоча б двох колонок.
    /// </summary>
    public static char? Detect(string text)
    {
        string sample = Sample(text, out bool cut);
        char? best = null;
        double bestShare = 0;
        bool bestFitsFirst = false;

        foreach (char candidate in Candidates)
        {
            var counts = CsvReader.Read(sample, candidate).Select(r => r.Fields.Length).ToList();
            // останній рядок зразка міг обірватися посередині
            if (cut && counts.Count > 1) counts.RemoveAt(counts.Count - 1);
            if (counts.Count == 0) continue;

            int usual = counts.GroupBy(n => n).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).First().Key;
            if (usual < 2) continue;

            double share = counts.Count(n => n == usual) / (double)counts.Count;
            bool fitsFirst = counts[0] == usual;
            if (share > bestShare || (share == bestShare && fitsFirst && !bestFitsFirst))
            {
                (best, bestShare, bestFitsFirst) = (candidate, share, fitsFirst);
            }
        }
        return best;
    }

    private static string Sample(string text, out bool cut)
    {
        int end = 0;
        for (int line = 0; line < SampleLines && end >= 0; line++)
            end = text.IndexOf('\n', end + (line == 0 ? 0 : 1));
        cut = end >= 0 && end < text.Length - 1;
        return cut ? text[..end] : text;
    }
}
