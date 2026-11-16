using Energoreestr.Text;

namespace Energoreestr.Tests;

public class NamesTests
{
    [Fact]
    public void Search_key_ignores_case_quotes_style_and_extra_spaces()
    {
        string a = Names.SearchKey("Акціонерне  товариство «Київгаз»");
        string b = Names.SearchKey("АКЦІОНЕРНЕ ТОВАРИСТВО \"КИЇВГАЗ\"");

        Assert.Equal(b, a);
    }

    [Fact]
    public void Search_key_replaces_latin_lookalikes_inside_cyrillic_words()
    {
        // у першому рядку I, A та X латинські
        string dirty = Names.SearchKey("ПУБЛIЧНЕ АКЦIОНЕРНЕ ТОВАРИСТВО ШAXTA");
        string clean = Names.SearchKey("ПУБЛІЧНЕ АКЦІОНЕРНЕ ТОВАРИСТВО ШАХТА");

        Assert.Equal(clean, dirty);
    }

    [Fact]
    public void Search_key_leaves_real_latin_names_alone()
    {
        Assert.Equal("ТОВ \"TEPLA ENERGY GROUP\"", Names.SearchKey("ТОВ \"TEPLA ENERGY GROUP\""));
    }

    [Fact]
    public void Finds_words_with_mixed_alphabets()
    {
        var words = Names.MixedAlphabetWords("ПУБЛIЧНЕ АКЦІОНЕРНЕ ТОВАРИСТВО \"УКPНAФТА\" GROUP");

        Assert.Equal(["ПУБЛIЧНЕ", "УКPНAФТА"], words);
    }
}
