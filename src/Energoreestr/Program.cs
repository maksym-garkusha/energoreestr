using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Energoreestr.Api;
using Energoreestr.Data;
using Energoreestr.Import;
using Energoreestr.Report;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<RegistryDb>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Registry")));

builder.Services.AddHttpClient<DataGovClient>(client =>
{
    client.BaseAddress = new Uri("https://data.gov.ua/");
    client.Timeout = TimeSpan.FromMinutes(2);
    // портал відповідає 403 на запити без User-Agent або без Accept,
    // а HttpClient сам ні того, ні іншого не надсилає
    client.DefaultRequestHeaders.UserAgent.ParseAdd("energoreestr/1.0 (+https://github.com/maksym-garkusha/energoreestr)");
    client.DefaultRequestHeaders.Accept.ParseAdd("*/*");
});
builder.Services.AddScoped<Importer>();
builder.Services.AddHostedService<ImportWorker>();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    // без цього кирилиця у відповідях перетворюється на ТОВ...
    options.SerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddOpenApi();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RegistryDb>();
    await db.Database.MigrateAsync();

    // dotnet run -- import                  забрати свіжі файли з порталу
    // dotnet run -- import <набір> <файл>   імпортувати файл з диска
    // dotnet run -- report [куди]           зібрати звіт про якість даних
    if (args is ["import", ..])
    {
        var importer = scope.ServiceProvider.GetRequiredService<Importer>();
        return await RunImport(importer, args);
    }
    if (args is ["report", ..])
    {
        string path = args.Length > 1 ? args[1] : "quality-report.md";
        await File.WriteAllTextAsync(path, await QualityReport.BuildAsync(db));
        Console.WriteLine($"Звіт записано в {path}");
        return 0;
    }
}

app.MapOpenApi();
app.MapScalarApiReference();
app.MapGet("/", () => Results.Redirect("/scalar")).ExcludeFromDescription();

app.MapParticipants();
app.MapCompanies();
app.MapChanges();
app.MapQuality();
app.MapDatasets();

await app.RunAsync();
return 0;

static async Task<int> RunImport(Importer importer, string[] args)
{
    if (args.Length == 1)
    {
        foreach (var outcome in await importer.ImportAllAsync())
            Console.WriteLine($"{outcome.Dataset}: {outcome.Message}");
        return 0;
    }

    if (args.Length != 3 || Datasets.Find(args[1]) is not { } def || !File.Exists(args[2]))
    {
        Console.Error.WriteLine("Використання: import <набір> <файл.csv>");
        Console.Error.WriteLine("Набори: " + string.Join(", ", Datasets.All.Select(d => d.Key)));
        return 1;
    }

    var content = await File.ReadAllBytesAsync(args[2]);
    var published = File.GetLastWriteTimeUtc(args[2]);
    var result = await importer.ImportFileAsync(def, Path.GetFileName(args[2]), published, content);
    Console.WriteLine($"{result.Dataset}: {result.Message}");
    return 0;
}

// потрібно тестам, щоб підняти застосунок у пам'яті
public partial class Program;
