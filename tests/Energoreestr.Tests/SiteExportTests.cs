using Energoreestr.Data;
using Energoreestr.Export;
using Microsoft.Extensions.DependencyInjection;

namespace Energoreestr.Tests;

public class SiteExportTests(ApiFixture app) : IClassFixture<ApiFixture>
{
    private async Task<SiteData> Build()
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RegistryDb>();
        return await SiteExport.BuildAsync(db);
    }

    [Fact]
    public async Task Each_code_gives_exactly_one_entry()
    {
        var data = await Build();

        var kyivgaz = Assert.Single(data.Companies, c => c.Code == "03346331");
        Assert.NotEmpty(kyivgaz.Monopolies);
        Assert.True(kyivgaz.CodeValid);

        var codes = data.Companies.Where(c => c.Code is not null).Select(c => c.Code).ToList();
        Assert.Equal(codes.Count, codes.Distinct().Count());
    }

    [Fact]
    public async Task Issue_recorded_under_code_without_zeros_is_attached()
    {
        var data = await Build();

        var kyivgaz = data.Companies.Single(c => c.Code == "03346331");
        Assert.Contains(kyivgaz.Issues, i => i.Code == "edrpou-leading-zeros");
    }

    [Fact]
    public async Task Individuals_are_left_out()
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RegistryDb>();
        int individuals = db.Participants.Count(p => p.IsIndividual) + db.Monopolies.Count(m => m.IsIndividual);

        var data = await SiteExport.BuildAsync(db);

        Assert.True(individuals > 0, "у зразках має бути хоча б один підприємець");
        Assert.Equal(individuals, data.IndividualsSkipped);
        Assert.Equal(
            db.Participants.Count(p => !p.IsIndividual),
            data.Companies.Sum(c => c.Wholesale.Count));
    }

    [Fact]
    public async Task Search_key_matches_what_the_api_uses()
    {
        var data = await Build();

        Assert.All(data.Companies, c => Assert.Equal(Energoreestr.Text.Names.SearchKey(c.Name), c.Key));
    }
}
