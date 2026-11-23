using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Energoreestr.Tests;

/// <summary>
/// CSP на сторінках пускає вбудовані скрипт і стиль лише за хешем.
/// Після будь-якої правки в них хеш змінюється, і без цього тесту сторінка мовчки перестала б працювати.
/// </summary>
public class PageCspTests
{
    public static TheoryData<string> Pages() => ["index.html", "check/index.html"];

    private static string Read(string page) => File.ReadAllText(Path.Combine(Repo.Root, "site", page));

    [Theory]
    [InlineData("index.html", "script", "script-src")]
    [InlineData("index.html", "style", "style-src")]
    [InlineData("check/index.html", "script", "script-src")]
    [InlineData("check/index.html", "style", "style-src")]
    public void Inline_block_hash_is_allowed_by_the_policy(string page, string tag, string directive)
    {
        string html = Read(page);
        var blocks = Regex.Matches(html, $"<{tag}>(.*?)</{tag}>", RegexOptions.Singleline);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(Assert.Single(blocks).Groups[1].Value));
        string expected = $"'sha256-{Convert.ToBase64String(hash)}'";

        Assert.True(Directive(html, directive).Contains(expected),
            $"У site/{page} змінився <{tag}>: у CSP-директиві {directive} має стояти {expected}");
    }

    [Theory]
    [MemberData(nameof(Pages))]
    public void Policy_forbids_everything_not_listed_and_inline_code_without_hash(string page)
    {
        string html = Read(page);

        Assert.Equal(["'none'"], Directive(html, "default-src"));
        Assert.DoesNotContain("'unsafe-inline'", Policy(html));
        Assert.DoesNotContain("'unsafe-eval'", Policy(html));
    }

    // під такою політикою обробник в атрибуті чи style= просто не спрацює, тож краще впасти тут
    [Theory]
    [MemberData(nameof(Pages))]
    public void Page_has_no_inline_handlers_or_style_attributes(string page)
    {
        string html = Read(page);

        Assert.DoesNotMatch(new Regex(@"\son[a-z]+\s*=", RegexOptions.IgnoreCase), html);
        Assert.DoesNotMatch(new Regex(@"\sstyle\s*=", RegexOptions.IgnoreCase), html);
    }

    // Сторінка перевірки працює з чужим файлом: після завантаження вона не має права
    // нікуди з'єднатися, і worker, створений з blob:, успадковує ту саму заборону
    [Fact]
    public void Check_page_may_not_connect_anywhere_or_load_anything_from_other_sites()
    {
        string html = Read("check/index.html");

        Assert.Equal(["'none'"], Directive(html, "connect-src"));
        Assert.Equal(["blob:"], Directive(html, "worker-src"));
        Assert.Equal(["'none'"], Directive(html, "form-action"));
        Assert.DoesNotContain("http", Policy(html));
        Assert.DoesNotContain("'self'", Policy(html));
        Assert.DoesNotMatch(new Regex(@"<(script|link|img|iframe)[^>]+(src|href)=""https?:", RegexOptions.IgnoreCase), html);
    }

    // текст з файлу на сторінку потрапляє лише через textContent і createElement
    [Fact]
    public void Check_page_never_turns_text_into_html()
    {
        string html = Read("check/index.html");

        foreach (var unsafeApi in new[] { "innerHTML", "outerHTML", "insertAdjacentHTML", "document.write", "eval(", "new Function" })
            Assert.DoesNotContain(unsafeApi, html);
    }

    private static string Policy(string html)
    {
        var meta = Regex.Match(html, "<meta http-equiv=\"Content-Security-Policy\" content=\"([^\"]+)\">");
        Assert.True(meta.Success, "На сторінці немає CSP");
        return meta.Groups[1].Value;
    }

    private static string[] Directive(string html, string name) =>
        Policy(html).Split(';', StringSplitOptions.TrimEntries)
            .Single(d => d.StartsWith(name + ' ', StringComparison.Ordinal))
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Skip(1)
            .ToArray();
}
