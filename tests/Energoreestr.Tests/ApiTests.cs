using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Energoreestr.Import;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Energoreestr.Tests;

/// <summary>
/// Піднімає застосунок у пам'яті з окремим файлом бази і наповнює її зразками.
/// Один раз на всі тести класу.
/// </summary>
public class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public string DbPath { get; } = Path.Combine(Path.GetTempPath(), $"energoreestr-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Registry", $"Data Source={DbPath}");
        builder.UseSetting("Import:IntervalHours", "0");
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var importer = scope.ServiceProvider.GetRequiredService<Importer>();
        var published = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        await importer.ImportFileAsync(Datasets.Find(Datasets.Wholesale)!, "wholesale-sample.csv", published,
            Fixture.Bytes("wholesale-sample.csv"));
        await importer.ImportFileAsync(Datasets.Find(Datasets.MonopoliesEnergy)!, "energy-sample.csv", published,
            Fixture.Bytes("energy-sample.csv"));
        await importer.ImportFileAsync(Datasets.Find(Datasets.MonopoliesHeatWater)!, "heat-water-sample.csv", published,
            Fixture.Bytes("heat-water-sample.csv"));
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(DbPath);
    }
}

public class ApiTests(ApiFixture app) : IClassFixture<ApiFixture>
{
    private readonly HttpClient _http = app.CreateClient();

    private async Task<JsonElement> GetJson(string url)
    {
        var response = await _http.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Theory]
    [InlineData("донбасенерго")]            // регістр не має значення
    [InlineData("«Донбасенерго»")]          // вид лапок теж
    [InlineData("23343582")]                // ЄДРПОУ
    [InlineData("24X-PJSC-DOENE-K")]        // EIC
    [InlineData("ecrb-ua-20231115-007")]    // код реєстрації
    public async Task Finds_participant_by_name_or_any_of_its_codes(string q)
    {
        var page = await GetJson("/api/participants?q=" + Uri.EscapeDataString(q));

        Assert.Equal(1, page.GetProperty("total").GetInt32());
        Assert.Equal("ECRB-UA-20231115-007", page.GetProperty("items")[0].GetProperty("ecrbCode").GetString());
    }

    [Fact]
    public async Task Search_finds_name_typed_in_cyrillic_even_if_register_has_latin_letters()
    {
        // у реєстрі "ВIДПОВIДАЛЬНIСТЮ ... ЕЛЕКТРИЧНI ЛIНIЇ" з латинською I
        var page = await GetJson("/api/participants?q=" + Uri.EscapeDataString("електричні лінії"));

        Assert.Equal(1, page.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Filters_by_status_and_pages_the_result()
    {
        var excluded = await GetJson("/api/participants?status=excluded");
        Assert.Equal(1, excluded.GetProperty("total").GetInt32());

        var page = await GetJson("/api/participants?page=2&pageSize=5");
        Assert.Equal(16, page.GetProperty("total").GetInt32());
        Assert.Equal(5, page.GetProperty("items").GetArrayLength());
        Assert.Equal(2, page.GetProperty("pageNumber").GetInt32());
    }

    [Fact]
    public async Task Participant_card_and_404()
    {
        var card = await GetJson("/api/participants/ECRB-UA-20231019-001");
        Assert.Equal("42750837", card.GetProperty("idCode").GetString());
        Assert.Equal("2023-10-27", card.GetProperty("registeredOn").GetString());

        var missing = await _http.GetAsync("/api/participants/ECRB-UA-20000101-001");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Response_keeps_cyrillic_readable()
    {
        string body = await _http.GetStringAsync("/api/participants/ECRB-UA-20231019-001");

        Assert.Contains("МГ БІЗНЕС ГРУП", body);
        Assert.DoesNotContain("\\u04", body);
    }

    [Fact]
    public async Task Company_card_works_with_and_without_leading_zero()
    {
        var full = await GetJson("/api/companies/03346331");
        var shortCode = await GetJson("/api/companies/3346331");

        Assert.Equal("03346331", shortCode.GetProperty("edrpou").GetString());
        Assert.True(full.GetProperty("checksumValid").GetBoolean());
        Assert.Equal(1, full.GetProperty("monopolies").GetArrayLength());
        Assert.Equal("edrpou-leading-zeros", full.GetProperty("issues")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task Company_card_rejects_garbage_and_unknown_codes()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await _http.GetAsync("/api/companies/abc")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync("/api/companies/00000000")).StatusCode);
    }

    [Fact]
    public async Task Monopolies_search_by_activity()
    {
        var page = await GetJson("/api/monopolies?activity=" + Uri.EscapeDataString("розподіл природного газу"));

        Assert.Equal(5, page.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Quality_summary_lists_issue_kinds_with_descriptions()
    {
        var summary = await GetJson("/api/quality");

        var wholesale = summary.EnumerateArray().Single(d => d.GetProperty("dataset").GetString() == "wholesale");
        Assert.Equal(19, wholesale.GetProperty("totalRows").GetInt32());
        Assert.Equal(16, wholesale.GetProperty("importedRows").GetInt32());
        Assert.Equal(3, wholesale.GetProperty("soleProprietors").GetInt32());

        var first = wholesale.GetProperty("issues")[0];
        Assert.True(first.GetProperty("count").GetInt32() > 0);
        Assert.NotEqual("", first.GetProperty("description").GetString());

        var issues = await GetJson("/api/quality/issues?dataset=wholesale&code=eic-checksum");
        Assert.Equal(1, issues.GetProperty("total").GetInt32());
    }

    [Theory]
    [InlineData("/api/check/edrpou/3346331", true)]
    [InlineData("/api/check/edrpou/03346332", false)]
    [InlineData("/api/check/eic/10x1001c--00001x", true)]
    [InlineData("/api/check/eic/62X1580751879965", false)]
    public async Task Code_check_endpoints(string url, bool valid)
    {
        var result = await GetJson(url);

        Assert.Equal(valid, result.GetProperty("valid").GetBoolean());
    }

    [Fact]
    public async Task Openapi_document_is_served()
    {
        var doc = await GetJson("/openapi/v1.json");

        Assert.True(doc.GetProperty("paths").TryGetProperty("/api/participants", out _));
    }
}
