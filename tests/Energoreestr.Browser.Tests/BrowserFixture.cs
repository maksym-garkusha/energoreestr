using Microsoft.Playwright;

namespace Energoreestr.Browser.Tests;

/// <summary>
/// Системний Chrome і сайт на локальному сервері, спільні для всіх тестів класу.
/// Браузер не завантажуємо: на runner'і GitHub Actions Chrome уже встановлено.
/// </summary>
public sealed class BrowserFixture : IAsyncLifetime
{
    public static readonly string Repo = FindRepo();

    private IPlaywright? playwright;

    public IBrowser Browser { get; private set; } = null!;
    public SiteServer Site { get; } = new(Path.Combine(Repo, "site") + Path.DirectorySeparatorChar);

    public async Task InitializeAsync()
    {
        playwright = await Playwright.CreateAsync();
        string chrome = Environment.GetEnvironmentVariable("CHROME_PATH") ?? "/usr/bin/google-chrome";
        Browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = chrome, Headless = true });
    }

    public async Task DisposeAsync()
    {
        await Browser.DisposeAsync();
        playwright?.Dispose();
        Site.Dispose();
    }

    public static string Fixture(string name) =>
        Path.Combine(Repo, "tests", "Energoreestr.Tests", "Fixtures", name);

    private static string FindRepo()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Energoreestr.slnx")))
                return dir.FullName;
        throw new FileNotFoundException("Не знайдено корінь репозиторію з Energoreestr.slnx");
    }
}
