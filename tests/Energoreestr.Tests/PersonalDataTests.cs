using System.Text;
using Energoreestr.Data;
using Energoreestr.Export;
using Energoreestr.Registers;
using Energoreestr.Report;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace Energoreestr.Tests;

/// <summary>
/// Вигадані підприємці зі зразків не мають з'явитися ніде, куди програма щось пише
/// або що віддає: у файлі бази, відповідях API, data.json і звіті.
/// </summary>
public class PersonalDataTests(ApiFixture app) : IClassFixture<ApiFixture>
{
    private static void AssertNoTraces(string text)
    {
        foreach (var trace in Fixture.SoleProprietorTraces)
            Assert.DoesNotContain(trace, text);
    }

    [Fact]
    public void Database_file_has_no_trace_of_sole_proprietors()
    {
        // читаємо сам файл, а не таблиці: так видно і те, що лишилося поза ними
        AssertNoTraces(Encoding.UTF8.GetString(File.ReadAllBytes(app.DbPath)));
    }

    [Theory]
    [InlineData("/api/participants?pageSize=100")]
    [InlineData("/api/monopolies?pageSize=100")]
    [InlineData("/api/quality")]
    [InlineData("/api/quality/issues?pageSize=100")]
    [InlineData("/api/changes?pageSize=100")]
    [InlineData("/api/datasets")]
    public async Task Api_returns_no_trace_of_sole_proprietors(string url)
    {
        AssertNoTraces(await app.CreateClient().GetStringAsync(url));
    }

    [Theory]
    [InlineData("/api/participants?q=вигаданий")]
    [InlineData("/api/participants?q=1234567890")]
    [InlineData("/api/monopolies?q=тестовий")]
    [InlineData("/api/monopolies?q=1234567899")]
    public async Task Search_does_not_find_sole_proprietors(string url)
    {
        string body = await app.CreateClient().GetStringAsync(url);

        Assert.Contains("\"total\":0", body);
    }

    [Fact]
    public async Task Page_data_and_report_have_no_trace_of_sole_proprietors()
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RegistryDb>();
        string dir = Path.Combine(Path.GetTempPath(), $"energoreestr-site-{Guid.NewGuid():N}");

        try
        {
            await SiteExport.WriteAsync(db, dir);
            AssertNoTraces(await File.ReadAllTextAsync(Path.Combine(dir, SiteExport.FileName)));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }

        string report = await QualityReport.BuildAsync(db);
        AssertNoTraces(report);
        Assert.Contains("Записів про фізичних осіб-підприємців: 3.", report);
    }

    [Fact]
    public void Migration_clears_sole_proprietors_from_a_database_built_by_the_previous_version()
    {
        string path = Path.Combine(Path.GetTempPath(), $"energoreestr-old-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<RegistryDb>().UseSqlite($"Data Source={path}").Options;

        try
        {
            using (var db = new RegistryDb(options))
            {
                db.GetService<IMigrator>().Migrate("20261116085930_Initial");
                db.Database.ExecuteSqlRaw(OldVersionRows);

                db.Database.Migrate();

                Assert.Equal(["ECRB-UA-20231019-001"], db.Participants.Select(p => p.EcrbCode).ToList());
                Assert.Equal(2, db.Monopolies.Count());

                var wholesale = db.Snapshots.Single(s => s.Dataset == "wholesale");
                Assert.Equal((1, 1, 2), (wholesale.SoleProprietors, wholesale.ImportedRows, wholesale.IssueCount));
                var heatWater = db.Snapshots.Single(s => s.Dataset == "monopolies-heat-water");
                Assert.Equal((1, 2, 2), (heatWater.SoleProprietors, heatWater.ImportedRows, heatWater.IssueCount));

                var anonymous = db.Issues.Where(i => i.Key == SoleProprietor.Key).ToList();
                Assert.Equal(2, anonymous.Count);
                Assert.All(anonymous, i => Assert.Null(i.Value));

                var change = Assert.Single(db.Changes);
                Assert.Equal("ECRB-UA-20231019-001", change.Key);
            }

            SqliteConnection.ClearAllPools();
            AssertNoTraces(Encoding.UTF8.GetString(File.ReadAllBytes(path)));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    // Так попередня версія записувала підприємців: з іменем, EIC, сайтом, адресою,
    // кодом у вигляді РНОКПП у зауваженні і в журналі змін, зокрема про того, хто вже зник
    private const string OldVersionRows = """
        INSERT INTO Snapshots (Id, Dataset, FileName, PublishedAt, ImportedAt, Sha256,
                               TotalRows, ImportedRows, IssueCount, Added, Removed, Changed)
        VALUES (1, 'wholesale', 'w.csv', '2026-10-01 00:00:00', '2026-10-01 00:00:00', 'a', 2, 2, 2, 0, 1, 1),
               (2, 'monopolies-heat-water', 'h.csv', '2026-10-01 00:00:00', '2026-10-01 00:00:00', 'b', 3, 3, 3, 0, 0, 1);

        INSERT INTO Participants (EcrbCode, Number, Name, SearchName, LegalForm, Address, Settlement, Zip, Country,
                                  Eic, Bic, Lei, Vat, IdCode, IsIndividual, SubmittedOn, RegisteredOn, ExcludedOn,
                                  Website, InsideInfoUrl, FirstSnapshotId)
        VALUES ('ECRB-UA-20231019-001', 1, 'ТОВ МГ', 'ТОВ МГ', 'Товариство з обмеженою відповідальністю', 'місто Київ',
                NULL, NULL, 'Україна', NULL, NULL, NULL, NULL, '42750837', 0, '2023-10-19', '2023-10-27', NULL,
                'mgbg.ua', NULL, 1),
               ('ECRB-UA-20250203-099', 9001, 'ФІЗИЧНА ОСОБА-ПІДПРИЄМЕЦЬ ТЕСТОВИЙ ТЕСТ ТЕСТОВИЧ',
                'ФІЗИЧНА ОСОБА-ПІДПРИЄМЕЦЬ ТЕСТОВИЙ ТЕСТ ТЕСТОВИЧ', 'Фізична особа-підприємець', NULL, 'Вигаданівка',
                NULL, 'Україна', '62x0000000000017', NULL, NULL, NULL, NULL, 1, '2025-02-03', '2025-02-05', NULL,
                'vyhadanyi-petro.example', NULL, 1);

        INSERT INTO Monopolies (Dataset, Key, Edrpou, IsIndividual, Name, SearchName, Location, Activity, Territory, FirstSnapshotId)
        VALUES ('monopolies-heat-water', '03361661|ВОДА|ПОЛТАВСЬКА', '03361661', 0, 'КП ВОДОКАНАЛ', 'КП ВОДОКАНАЛ',
                'ПОЛТАВА', 'ВОДА', 'ПОЛТАВСЬКА', 2),
               ('monopolies-heat-water', 'КП ТЕПЛО|ТЕПЛО|КИЇВСЬКА', NULL, 0, 'КП ТЕПЛО', 'КП ТЕПЛО',
                'КИЇВ', 'ТЕПЛО', 'КИЇВСЬКА', 2),
               ('monopolies-heat-water', 'ВИГАДАНА ОКСAНА ПЕТРІВНА|ТЕПЛО|ПОЛТАВСЬКА', NULL, 1, 'ВИГАДАНА ОКСAНА ПЕТРІВНА',
                'ВИГАДАНА ОКСАНА ПЕТРІВНА', 'С. ВИГАДАНЕ', 'ТЕПЛО', 'ПОЛТАВСЬКА', 2);

        INSERT INTO Issues (Dataset, Line, Key, Code, Value)
        VALUES ('wholesale', 2, 'ECRB-UA-20231019-001', 'url-format', 'mgbg ua'),
               ('wholesale', 3, 'ECRB-UA-20250203-099', 'eic-format', '62x0000000000017'),
               ('monopolies-heat-water', 2, '3361661', 'edrpou-leading-zeros', '3361661'),
               ('monopolies-heat-water', 3, '1234567890', 'id-code-format', '1234567890'),
               ('monopolies-heat-water', 4, 'ВИГАДАНА ОКСAНА ПЕТРІВНА', 'mixed-alphabet', 'ОКСAНА');

        INSERT INTO Changes (SnapshotId, Dataset, Key, Name, Kind, Field, OldValue, NewValue)
        VALUES (1, 'wholesale', 'ECRB-UA-20231019-001', 'ТОВ МГ', 'Changed', 'Website', 'www.mgbg.com.ua', 'mgbg.ua'),
               (1, 'wholesale', 'ECRB-UA-20250303-098', 'ФІЗИЧНА ОСОБА-ПІДПРИЄМЕЦЬ ВИГАДАНИЙ ПЕТРО', 'Removed', NULL, NULL, NULL),
               (2, 'monopolies-heat-water', 'ВИГАДАНА ОКСAНА ПЕТРІВНА|ТЕПЛО|ПОЛТАВСЬКА', 'ВИГАДАНА ОКСAНА ПЕТРІВНА',
                'Changed', 'Location', 'С. ВИГАДАНЕ', 'М. ВИГАДАНЕ');
        """;
}
