using System.Text;

namespace Energoreestr.Text;

public static class Names
{
    // латинські літери, які виглядають як кириличні. У реєстрах трапляється
    // "ВIДПОВIДАЛЬНIСТЮ" з латинською I: на око не відрізнити, а пошук не знаходить
    private static readonly Dictionary<char, char> Lookalikes = new()
    {
        ['A'] = 'А',
        ['B'] = 'В',
        ['C'] = 'С',
        ['E'] = 'Е',
        ['H'] = 'Н',
        ['I'] = 'І',
        ['K'] = 'К',
        ['M'] = 'М',
        ['O'] = 'О',
        ['P'] = 'Р',
        ['T'] = 'Т',
        ['X'] = 'Х',
        ['Y'] = 'У'
    };

    /// <summary>
    /// Ключ для пошуку: великі літери, один вид лапок і апострофів, без зайвих пробілів,
    /// латинські двійники в кириличних словах замінені на кириличні.
    /// </summary>
    public static string SearchKey(string name)
    {
        var sb = new StringBuilder(name.Length);
        bool lastWasSpace = true;

        foreach (char raw in name.ToUpperInvariant())
        {
            char c = raw switch
            {
                '«' or '»' or '“' or '”' or '„' => '"',
                '’' or 'ʼ' or '`' or '′' => '\'',
                _ => raw
            };

            if (char.IsWhiteSpace(c))
            {
                if (!lastWasSpace) sb.Append(' ');
                lastWasSpace = true;
            }
            else
            {
                sb.Append(c);
                lastWasSpace = false;
            }
        }

        var words = sb.ToString().TrimEnd().Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            if (HasCyrillic(words[i]))
                words[i] = ReplaceLookalikes(words[i]);
        }
        return string.Join(' ', words);
    }

    /// <summary>Слова, де в одному слові змішані кирилиця і латиниця.</summary>
    public static List<string> MixedAlphabetWords(string text)
    {
        var found = new List<string>();
        foreach (var word in text.Split([' ', '"', '«', '»', '(', ')', ',', '.', '-'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (HasCyrillic(word) && word.Any(IsLatin))
                found.Add(word);
        }
        return found;
    }

    private static string ReplaceLookalikes(string word)
    {
        var chars = word.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (Lookalikes.TryGetValue(chars[i], out char cyrillic))
                chars[i] = cyrillic;
        }
        return new string(chars);
    }

    private static bool HasCyrillic(string word) => word.Any(c => c >= 'Ѐ' && c <= 'ӿ');

    public static bool IsLatin(char c) => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
}
