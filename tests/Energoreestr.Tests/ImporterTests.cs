using System.Text;
using Energoreestr.Api;
using Energoreestr.Data;
using Energoreestr.Import;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Energoreestr.Tests;

/// <summary>
/// Імпорт перевіряється на справжньому SQLite в пам'яті. База живе, поки відкрите
/// з'єднання, тому воно одне на весь тест.
/// </summary>
public class ImporterTests : IDisposable
{
    private static readonly DatasetDef Wholesale = Datasets.Find(Datasets.Wholesale)!;
    private static readonly DatasetDef HeatWater = Datasets.Find(Datasets.MonopoliesHeatWater)!;

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly RegistryDb _db;
    private readonly Importer _importer;

    public ImporterTests()
    {
        _connection.Open();
        _db = new RegistryDb(new DbContextOptionsBuilder<RegistryDb>().UseSqlite(_connection).Options);
        _db.Database.Migrate();

        // до порталу ці тести не ходять, клієнт потрібен тільки конструктору
        _importer = new Importer(_db, new DataGovClient(new HttpClient()), NullLogger<Importer>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private Task<ImportOutcome> Import(DatasetDef def, string text, int day) =>
        _importer.ImportFileAsync(def, $"file-{day:00}.csv", new DateTime(2026, 10, day, 0, 0, 0, DateTimeKind.Utc),
            Encoding.UTF8.GetBytes(text));

    [Fact]
    public async Task First_file_fills_tables_and_writes_no_changes()
    {
        var outcome = await Import(Wholesale, Fixture.Text("wholesale-sample.csv"), 1);

        Assert.True(outcome.Imported);
        Assert.Equal(16, await _db.Participants.CountAsync());
        Assert.Empty(await _db.Changes.ToListAsync());

        var snapshot = await _db.Snapshots.SingleAsync();
        Assert.Equal(16, snapshot.ImportedRows);
        Assert.Equal(3, snapshot.SoleProprietors);
        Assert.Equal(await _db.Issues.CountAsync(), snapshot.IssueCount);
        Assert.True(snapshot.IssueCount > 0);
    }

    [Fact]
    public async Task Record_count_of_a_file_includes_sole_proprietors_that_are_not_stored()
    {
        await Import(Wholesale, Fixture.Text("wholesale-sample.csv"), 1);
        _db.DatasetStates.Add(new DatasetState { Dataset = Datasets.Wholesale, Title = "Оптовий ринок" });
        await _db.SaveChangesAsync();

        var status = Assert.Single(await DatasetsApi.List(_db, CancellationToken.None));

        // 16 компаній у базі і 3 підприємці, яких там немає: у реєстрі 19 записів
        Assert.Equal(19, status.Records);
    }

    [Fact]
    public async Task Same_content_is_not_imported_twice()
    {
        string text = Fixture.Text("wholesale-sample.csv");
        await Import(Wholesale, text, 1);

        var again = await Import(Wholesale, text, 2);

        Assert.False(again.Imported);
        Assert.Equal(1, await _db.Snapshots.CountAsync());
    }

    [Fact]
    public async Task Next_file_is_compared_with_the_previous_one()
    {
        string first = Fixture.Text("wholesale-sample.csv");
        await Import(Wholesale, first, 1);

        // у другому файлі: один учасник зник, в одного з'явилася дата виключення
        // і змінився сайт, один новий
        var lines = first.Split("\r\n").ToList();
        lines.RemoveAll(l => l.StartsWith("2,"));
        int i = lines.FindIndex(l => l.StartsWith("1,"));
        lines[i] = lines[i]
            .Replace("www.mgbg.com.ua,www.mgbg.com.ua,null", "mgbg.ua,mgbg.ua,2026-10-01");
        lines.Add(lines[i]
            .Replace("1,Національна", "9002,Національна")
            .Replace("ECRB-UA-20231019-001", "ECRB-UA-20261001-001"));
        string second = string.Join("\r\n", lines);

        var outcome = await Import(Wholesale, second, 8);

        Assert.True(outcome.Imported);
        var snapshot = await _db.Snapshots.OrderBy(s => s.Id).LastAsync();
        Assert.Equal(1, snapshot.Added);
        Assert.Equal(1, snapshot.Removed);
        Assert.Equal(1, snapshot.Changed);

        var changes = await _db.Changes.Where(c => c.SnapshotId == snapshot.Id).ToListAsync();

        var added = changes.Single(c => c.Kind == ChangeKind.Added);
        Assert.Equal("ECRB-UA-20261001-001", added.Key);

        var removed = changes.Single(c => c.Kind == ChangeKind.Removed);
        Assert.Equal("ECRB-UA-20231023-001", removed.Key);
        Assert.Contains("ПІВДЕНЬ ЕНЕРГО ЗБУТ", removed.Name);

        var changed = changes.Where(c => c.Kind == ChangeKind.Changed).ToList();
        Assert.All(changed, c => Assert.Equal("ECRB-UA-20231019-001", c.Key));
        Assert.Equal(
            ["ExcludedOn", "InsideInfoUrl", "Website"],
            changed.Select(c => c.Field!).Order());

        var excluded = changed.Single(c => c.Field == "ExcludedOn");
        Assert.Null(excluded.OldValue);
        Assert.Equal("2026-10-01", excluded.NewValue);

        // таблиця відповідає другому файлу
        Assert.Equal(16, await _db.Participants.CountAsync());
        Assert.False(await _db.Participants.AnyAsync(p => p.EcrbCode == "ECRB-UA-20231023-001"));
        var updated = await _db.Participants.SingleAsync(p => p.EcrbCode == "ECRB-UA-20231019-001");
        Assert.Equal("mgbg.ua", updated.Website);
    }

    [Fact]
    public async Task Record_remembers_the_file_it_first_appeared_in()
    {
        string first = Fixture.Text("wholesale-sample.csv");
        await Import(Wholesale, first, 1);
        await Import(Wholesale, first.Replace("www.mgbg.com.ua", "mgbg.ua"), 8);

        var snapshots = await _db.Snapshots.OrderBy(s => s.Id).ToListAsync();
        var participant = await _db.Participants.SingleAsync(p => p.EcrbCode == "ECRB-UA-20231019-001");

        Assert.Equal(2, snapshots.Count);
        Assert.Equal(snapshots[0].Id, participant.FirstSnapshotId);
    }

    [Fact]
    public async Task Issues_are_replaced_by_those_of_the_newest_file()
    {
        string first = Fixture.Text("wholesale-sample.csv");
        await Import(Wholesale, first, 1);
        int before = await _db.Issues.CountAsync();

        // прибираємо запис, у якого EIC не проходив перевірку
        var lines = first.Split("\r\n").Where(l => !l.StartsWith("829,"));
        await Import(Wholesale, string.Join("\r\n", lines), 8);

        Assert.True(await _db.Issues.CountAsync() < before);
        Assert.False(await _db.Issues.AnyAsync(i => i.Code == "eic-checksum"));
    }

    [Fact]
    public async Task Registers_do_not_touch_each_other()
    {
        await Import(Wholesale, Fixture.Text("wholesale-sample.csv"), 1);
        await Import(HeatWater, Fixture.Text("heat-water-sample.csv"), 2);

        Assert.Equal(16, await _db.Participants.CountAsync());
        Assert.Equal(8, await _db.Monopolies.CountAsync());
        Assert.All(await _db.Monopolies.ToListAsync(), m => Assert.Equal(Datasets.MonopoliesHeatWater, m.Dataset));
        Assert.True(await _db.Issues.AnyAsync(i => i.Dataset == Datasets.Wholesale));
        Assert.True(await _db.Issues.AnyAsync(i => i.Dataset == Datasets.MonopoliesHeatWater));
    }
}
