using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Energoreestr.Tests;

/// <summary>
/// CSP на сторінці пускає вбудовані скрипт і стиль лише за хешем.
/// Після будь-якої правки в них хеш змінюється, і без цього тесту сторінка мовчки перестала б працювати.
/// </summary>
public class PageCspTests
{
    private static readonly string Page = File.ReadAllText(PagePath());

    [Theory]
    [InlineData("script", "script-src")]
    [InlineData("style", "style-src")]
    public void Inline_block_hash_is_allowed_by_the_policy(string tag, string directive)
    {
        var blocks = Regex.Matches(Page, $"<{tag}>(.*?)</{tag}>", RegexOptions.Singleline);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(Assert.Single(blocks).Groups[1].Value));
        string expected = $"'sha256-{Convert.ToBase64String(hash)}'";

        Assert.True(Directive(directive).Contains(expected),
            $"У site/index.html змінився <{tag}>: у CSP-директиві {directive} має стояти {expected}");
    }

    [Fact]
    public void Policy_forbids_everything_not_listed_and_inline_code_without_hash()
    {
        Assert.Equal(["'none'"], Directive("default-src"));
        Assert.DoesNotContain("'unsafe-inline'", Policy());
        Assert.DoesNotContain("'unsafe-eval'", Policy());
    }

    // під такою політикою обробник в атрибуті чи style= просто не спрацює, тож краще впасти тут
    [Fact]
    public void Page_has_no_inline_handlers_or_style_attributes()
    {
        Assert.DoesNotMatch(new Regex(@"\son[a-z]+\s*=", RegexOptions.IgnoreCase), Page);
        Assert.DoesNotMatch(new Regex(@"\sstyle\s*=", RegexOptions.IgnoreCase), Page);
    }

    private static string Policy()
    {
        var meta = Regex.Match(Page, "<meta http-equiv=\"Content-Security-Policy\" content=\"([^\"]+)\">");
        Assert.True(meta.Success, "На сторінці немає CSP");
        return meta.Groups[1].Value;
    }

    private static string[] Directive(string name) =>
        Policy().Split(';', StringSplitOptions.TrimEntries)
            .Single(d => d.StartsWith(name + ' ', StringComparison.Ordinal))
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Skip(1)
            .ToArray();

    private static string PagePath()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Energoreestr.slnx")))
                return Path.Combine(dir.FullName, "site", "index.html");
        throw new FileNotFoundException("Не знайдено корінь репозиторію з Energoreestr.slnx");
    }
}
