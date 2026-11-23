using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Energoreestr.Api;
using Energoreestr.Commands;
using Energoreestr.Data;
using Energoreestr.Export;
using Energoreestr.Import;
using Energoreestr.Report;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

// Перевірці файлу не потрібні ні база, ні вебсервер: виходимо раніше, ніж їх створити,
// щоб команда нічого не записала на диск. Слово check шукаємо серед усіх аргументів,
// бо прапорці люди ставлять і перед ним
int checkAt = Array.FindIndex(args, a => a.Equals("check", StringComparison.OrdinalIgnoreCase));
if (checkAt >= 0)
{
    // інакше в консолі Windows кирилиця звіту перетвориться на знаки питання
    Console.OutputEncoding = Encoding.UTF8;
    return await CheckCommand.RunAsync([.. args[..checkAt], .. args[(checkAt + 1)..]], Console.Out, Console.Error);
}

// опечатка в назві команди не має мовчки запускати вебсервер
if (args is [var command, ..] && !command.StartsWith('-') && command is not ("import" or "report" or "export"))
{
    Console.Error.WriteLine($"Невідома команда «{command}». Є check, import, report і export; без команди запускається API.");
    return 2;
}

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
    // dotnet run -- export [папка]         зібрати дані для сторінки пошуку
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
    if (args is ["export", ..])
    {
        string dir = args.Length > 1 ? args[1] : "site";
        var summary = await SiteExport.WriteAsync(db, dir);
        Console.WriteLine($"{summary.Companies} компаній записано в {Path.Combine(dir, SiteExport.FileName)}, записів про підприємців у файлах: {summary.SoleProprietors}, у базу вони не потрапляють");
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
        var outcomes = await importer.ImportAllAsync();
        foreach (var outcome in outcomes)
            Console.WriteLine($"{outcome.Dataset}: {outcome.Message}");
        // якщо хоч один набір не вдалося забрати, код повернення має це показати:
        // інакше щотижнева збірка викладе сторінку з неповними даними
        return outcomes.Any(o => o.Message.StartsWith("помилка")) ? 1 : 0;
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
