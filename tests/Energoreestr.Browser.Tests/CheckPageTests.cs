using System.Text.Json;
using Microsoft.Playwright;

namespace Energoreestr.Browser.Tests;

public class CheckPageTests(BrowserFixture browser) : IClassFixture<BrowserFixture>
{
    // Імена, коди й адреси вигаданих підприємців з фікстур. Жоден із цих рядків
    // не має з'явитися ні на сторінці, ні в збереженому звіті
    private static readonly string[] Traces =
    [
        "ТЕСТОВИЙ", "Тестовий", "ВИГАДАН", "Вигадан", "вигадан", "ОКСAНА",
        "1234567890", "1234567899", "2345678901", "123 456 7899", "123-456-7899", "vyhadanyi"
    ];

    private async Task<(IBrowserContext Context, IPage Page)> OpenAsync()
    {
        var context = await browser.Browser.NewContextAsync(new() { AcceptDownloads = true });
        var page = await context.NewPageAsync();
        await page.GotoAsync(browser.Site.BaseUrl + "check/");
        return (context, page);
    }

    private static async Task CheckAsync(IPage page, string fixture)
    {
        await page.Locator("#file").SetInputFilesAsync(BrowserFixture.Fixture(fixture));
        await page.Locator("#status").Filter(new() { HasTextRegex = new("завершено|не перевірено") })
            .WaitForAsync(new() { Timeout = 15000 });
    }

    [Fact]
    public async Task Page_loads_nothing_but_itself_and_sends_nothing_while_checking()
    {
        var (context, page) = await OpenAsync();
        var requests = new List<string>();
        context.Request += (_, request) => requests.Add(request.Url);
        int served = browser.Site.Requests.Count;

        foreach (var fixture in new[] { "check/personal-data.csv", "heat-water-sample.csv", "check/windows-1251.csv" })
            await CheckAsync(page, fixture);

        // worker з blob: це не мережевий запит, а більше нічого бути не повинно
        Assert.All(requests, url => Assert.StartsWith("blob:", url));
        Assert.Equal(served, browser.Site.Requests.Count);
        await context.DisposeAsync();
    }

    [Fact]
    public async Task Page_itself_is_one_file_without_fonts_or_scripts_from_elsewhere()
    {
        var context = await browser.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        var requests = new List<string>();
        context.Request += (_, request) => requests.Add(request.Url);

        await page.GotoAsync(browser.Site.BaseUrl + "check/");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        Assert.Equal([browser.Site.BaseUrl + "check/"], requests.Where(u => !u.StartsWith("data:", StringComparison.Ordinal)));
        await context.DisposeAsync();
    }

    // Головна обіцянка сторінки: worker з blob: успадковує заборону на з'єднання. Перевіряємо не код
    // сторінки, а сам браузер: worker, який навмисно пробує fetch, importScripts і WebSocket
    [Fact]
    public async Task Worker_made_from_a_blob_cannot_reach_the_network()
    {
        var (context, page) = await OpenAsync();
        int served = browser.Site.Requests.Count;

        var results = await page.EvaluateAsync<string[]>(@"async url => {
          const code = `
            const results = [];
            const attempt = async (name, run) => { try { await run(); results.push(name + ' дійшов'); } catch { results.push(name + ' заблоковано'); } };
            (async () => {
              await attempt('fetch', () => fetch('${url}probe-fetch'));
              await attempt('importScripts', async () => importScripts('${url}probe-import.js'));
              await attempt('WebSocket', () => new Promise((ok, fail) => {
                const socket = new WebSocket('${url.replace('http', 'ws')}probe-socket');
                socket.onopen = ok; socket.onerror = fail;
              }));
              postMessage(results);
            })();`;
          const worker = new Worker(URL.createObjectURL(new Blob([code], { type: 'text/javascript' })));
          return await new Promise(done => {
            worker.onmessage = event => done(event.data);
            setTimeout(() => done(['немає відповіді']), 10000);
          });
        }", browser.Site.BaseUrl);

        Assert.Equal(["fetch заблоковано", "importScripts заблоковано", "WebSocket заблоковано"], results);
        Assert.Equal(served, browser.Site.Requests.Count);
        await context.DisposeAsync();
    }

    [Fact]
    public async Task Second_file_chosen_quickly_wins_over_the_first()
    {
        var (context, page) = await OpenAsync();

        await page.Locator("#file").SetInputFilesAsync(BrowserFixture.Fixture("heat-water-sample.csv"));
        await page.Locator("#file").SetInputFilesAsync(BrowserFixture.Fixture("check/clean.csv"));
        await Assertions.Expect(page.Locator("#status")).ToContainTextAsync("завершено");
        await page.WaitForTimeoutAsync(1000);

        await Assertions.Expect(page.Locator("#report")).ToContainTextAsync("clean.csv");
        await Assertions.Expect(page.Locator("#report")).Not.ToContainTextAsync("heat-water-sample.csv");
        await context.DisposeAsync();
    }

    [Fact]
    public async Task Long_lists_stop_at_a_thousand_items_with_a_count_of_the_rest()
    {
        var (context, page) = await OpenAsync();
        string csv = "Номер;Код ЄДРПОУ\n" + string.Concat(Enumerable.Range(1, 1200).Select(i => $"{i};{1000000 + i}\n"));

        await page.Locator("#file").SetInputFilesAsync(new FilePayload { Name = "codes.csv", MimeType = "text/csv", Buffer = System.Text.Encoding.UTF8.GetBytes(csv) });
        await Assertions.Expect(page.Locator("#status")).ToContainTextAsync("завершено");
        var group = page.Locator("article.group").Filter(new() { HasText = "8 цифр" });
        await group.Locator("button.more").ClickAsync();

        Assert.Equal(1000, await group.Locator("li").CountAsync());
        await Assertions.Expect(group).ToContainTextAsync("і ще 200");
        await context.DisposeAsync();
    }

    [Fact]
    public async Task Hostile_shapes_of_a_file_end_in_a_clear_answer()
    {
        var (context, page) = await OpenAsync();

        var columns = System.Text.Encoding.UTF8.GetBytes(new string(';', 200_000) + "\n");
        await page.Locator("#file").SetInputFilesAsync(new FilePayload { Name = "columns.csv", MimeType = "text/csv", Buffer = columns });
        await Assertions.Expect(page.Locator("#report")).ToContainTextAsync("понад 1000 колонок");

        // довгий рядок з майже-маркером підприємця раніше перевірявся квадратичний час
        var markers = System.Text.Encoding.UTF8.GetBytes("Найменування;Код\n" + string.Concat(Enumerable.Repeat("ФІЗИЧН ", 30_000)) + ";1\n");
        await page.Locator("#file").SetInputFilesAsync(new FilePayload { Name = "markers.csv", MimeType = "text/csv", Buffer = markers });
        await Assertions.Expect(page.Locator("#status")).ToContainTextAsync("завершено", new() { Timeout = 10000 });
        await context.DisposeAsync();
    }

    [Fact]
    public async Task Built_in_example_is_checked_without_any_request()
    {
        var (context, page) = await OpenAsync();
        var requests = new List<string>();
        context.Request += (_, request) => requests.Add(request.Url);

        await page.GetByRole(AriaRole.Button, new() { Name = "Спробувати на прикладі" }).ClickAsync();
        await Assertions.Expect(page.Locator("#status")).ToHaveTextAsync("Перевірку завершено.");

        await Assertions.Expect(page.Locator("#report")).ToContainTextAsync("приклад.csv");
        await Assertions.Expect(page.Locator("#report")).ToContainTextAsync("Загублені нулі в ЄДРПОУ");
        await Assertions.Expect(page.Locator(".personal")).ToContainTextAsync("Рядків з ознаками персональних даних: 1");
        Assert.All(requests, url => Assert.StartsWith("blob:", url));
        await context.DisposeAsync();
    }

    [Fact]
    public async Task Check_works_with_the_network_switched_off()
    {
        var (context, page) = await OpenAsync();
        await context.SetOfflineAsync(true);

        await CheckAsync(page, "check/codes-dates-numbers.csv");

        await Assertions.Expect(page.Locator(".counter.fix b")).ToHaveTextAsync("4");
        await Assertions.Expect(page.Locator(".counter.verify b")).ToHaveTextAsync("4");
        await context.DisposeAsync();
    }

    [Fact]
    public async Task Html_from_the_file_stays_plain_text()
    {
        var (context, page) = await OpenAsync();
        bool dialog = false;
        page.Dialog += (_, d) => { dialog = true; _ = d.DismissAsync(); };
        var errors = new List<string>();
        page.Console += (_, message) => { if (message.Type == "error") errors.Add(message.Text); };

        await CheckAsync(page, "check/html-in-cells.csv");
        await page.Locator("details.columns summary").ClickAsync();

        // <b> із файлу мав би стати елементом, якби текст ішов як розмітка; власні лічильники теж <b>, тож шукаємо в переліках
        Assert.Equal(0, await page.Locator("#report img, #report script, #report [onerror], #report li b, #report td b").CountAsync());
        await Assertions.Expect(page.Locator("details.columns")).ToContainTextAsync("<img src=x onerror=alert(1)>");
        Assert.False(dialog);
        Assert.Empty(errors);
        await context.DisposeAsync();
    }

    [Theory]
    [InlineData("check/personal-data.csv")]
    [InlineData("heat-water-sample.csv")]
    [InlineData("wholesale-sample.csv")]
    public async Task Sole_proprietors_leave_no_trace_on_screen_or_in_the_saved_report(string fixture)
    {
        var (context, page) = await OpenAsync();
        await CheckAsync(page, fixture);
        foreach (var more in await page.Locator("button.more").AllAsync()) await more.ClickAsync();

        string screen = await page.Locator("body").InnerTextAsync();
        var download = await page.RunAndWaitForDownloadAsync(() => page.GetByRole(AriaRole.Button, new() { Name = "Зберегти звіт" }).ClickAsync());
        string saved = await File.ReadAllTextAsync((await download.PathAsync())!);

        Assert.Contains("персональних даних", screen);
        Assert.Contains("персональних даних", saved);
        Assert.Matches(@"^perevirka-\d{4}-\d{2}-\d{2}-\d{4}\.txt$", download.SuggestedFilename);
        // назва файлу є і на екрані, і в першому рядку збереженого звіту: це документ людини на її пристрої
        Assert.Contains(Path.GetFileName(fixture), screen);
        Assert.StartsWith($"Файл {Path.GetFileName(fixture)}, ", saved);
        foreach (var trace in Traces)
        {
            Assert.DoesNotContain(trace, screen);
            Assert.DoesNotContain(trace, saved);
        }
        await context.DisposeAsync();
    }

    [Theory]
    [InlineData("check/clean.csv")]
    [InlineData("check/excel-traces.csv")]
    [InlineData("check/windows-1251.csv")]
    [InlineData("check/utf16-tab.csv")]
    public async Task Counters_on_screen_match_the_expected_file(string fixture)
    {
        var (context, page) = await OpenAsync();
        await CheckAsync(page, fixture);

        var expected = JsonDocument.Parse(await File.ReadAllTextAsync(BrowserFixture.Fixture(fixture.Replace(".csv", ".expected.json"))))
            .RootElement.GetProperty("summary");
        foreach (var level in new[] { "fix", "verify", "info" })
            await Assertions.Expect(page.Locator($".counter.{level} b")).ToHaveTextAsync(expected.GetProperty(level).GetInt32().ToString());
        await context.DisposeAsync();
    }

    [Fact]
    public async Task Excel_workbook_and_too_big_file_get_a_clear_message()
    {
        var (context, page) = await OpenAsync();

        await page.Locator("#file").SetInputFilesAsync(new FilePayload { Name = "таблиця.xlsx", MimeType = "application/zip", Buffer = [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00] });
        await Assertions.Expect(page.Locator("#report")).ToContainTextAsync("Це файл Excel");

        string big = Path.Combine(Path.GetTempPath(), $"great-{Guid.NewGuid():N}.csv");
        await using (var stream = File.Create(big)) stream.SetLength(51L * 1024 * 1024);
        try
        {
            await page.Locator("#file").SetInputFilesAsync(big);
            await Assertions.Expect(page.Locator("#report")).ToContainTextAsync("більший за 50 МБ");
        }
        finally
        {
            File.Delete(big);
        }
        await context.DisposeAsync();
    }

    [Fact]
    public async Task File_button_works_from_the_keyboard()
    {
        var (context, page) = await OpenAsync();

        // фокус і натискання однією дією: між ними сторінка ще могла дорендеритися
        var chooser = await page.RunAndWaitForFileChooserAsync(() => page.Locator("#pick").PressAsync("Enter"));
        await chooser.SetFilesAsync(BrowserFixture.Fixture("check/clean.csv"));

        await Assertions.Expect(page.Locator("#status")).ToHaveTextAsync("Перевірку завершено.");
        await Assertions.Expect(page.Locator("#report")).ToContainTextAsync("Зауважень немає");
        // після перевірки фокус стоїть на картці підсумку, тож зчитувач екрана одразу її прочитає
        Assert.True(await page.EvaluateAsync<bool>("document.activeElement.classList.contains('summary')"));
        await context.DisposeAsync();
    }

    [Fact]
    public async Task Pages_link_to_each_other_under_the_site_folder()
    {
        var (context, page) = await OpenAsync();

        await page.GetByRole(AriaRole.Link, new() { Name = "Пошук компанії в реєстрах" }).ClickAsync();
        Assert.Equal(browser.Site.BaseUrl, page.Url);
        await page.GetByRole(AriaRole.Link, new() { Name = "Перевірка CSV-файлу перед публікацією" }).ClickAsync();
        Assert.Equal(browser.Site.BaseUrl + "check/", page.Url);
        await context.DisposeAsync();
    }
}
