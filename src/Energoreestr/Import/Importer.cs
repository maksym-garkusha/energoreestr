using System.Security.Cryptography;
using System.Text;
using Energoreestr.Data;
using Energoreestr.Registers;
using Energoreestr.Text;
using Microsoft.EntityFrameworkCore;

namespace Energoreestr.Import;

public record ImportOutcome(string Dataset, bool Imported, string Message);

/// <summary>
/// Забирає файл реєстру, розбирає його і приводить таблицю в базі до стану цього файлу.
/// Усе, що при цьому з'явилося, зникло чи змінилося, записує в журнал змін.
/// </summary>
public class Importer(RegistryDb db, DataGovClient portal, ILogger<Importer> log)
{
    // поля, зміну яких у журнал не пишемо: порядковий номер зсувається сам,
    // а SearchName рахується з назви
    private static readonly string[] NotTracked = [nameof(Participant.Number), nameof(Participant.SearchName)];

    public async Task<List<ImportOutcome>> ImportAllAsync(CancellationToken ct = default)
    {
        var outcomes = new List<ImportOutcome>();
        foreach (var def in Datasets.All)
        {
            try
            {
                outcomes.Add(await ImportFromPortalAsync(def, ct));
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                // один недоступний набір не має зупиняти решту
                log.LogWarning(ex, "Не вдалося оновити {Dataset}", def.Key);
                outcomes.Add(new ImportOutcome(def.Key, false, "помилка: " + ex.Message));
            }
        }
        return outcomes;
    }

    public async Task<ImportOutcome> ImportFromPortalAsync(DatasetDef def, CancellationToken ct = default)
    {
        var dataset = await portal.GetDatasetAsync(def.PackageId, ct);

        var resource = dataset.Resources.FirstOrDefault(r =>
            r.Format.Equals("CSV", StringComparison.OrdinalIgnoreCase)
            && (def.FileHint is null || r.Name.Contains(def.FileHint)));

        var state = await db.DatasetStates.FindAsync([def.Key], ct);
        if (state is null)
        {
            state = new DatasetState { Dataset = def.Key };
            db.DatasetStates.Add(state);
        }
        state.Title = dataset.Title;
        state.UpdateFrequency = dataset.UpdateFrequency;
        state.ResourceName = resource?.Name;
        state.ResourceModifiedAt = resource?.ModifiedAt;
        state.CheckedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        if (resource is null)
            return new ImportOutcome(def.Key, false, "у наборі немає CSV-файлу");

        // якщо цей файл уже брали, не качаємо його знову
        bool known = await db.Snapshots.AnyAsync(s =>
            s.Dataset == def.Key && s.FileName == resource.Name && s.PublishedAt == resource.ModifiedAt, ct);
        if (known)
            return new ImportOutcome(def.Key, false, $"без змін, останній файл {resource.Name}");

        var content = await portal.DownloadAsync(resource.Url, ct);
        return await ImportFileAsync(def, resource.Name, resource.ModifiedAt, content, ct);
    }

    public async Task<ImportOutcome> ImportFileAsync(
        DatasetDef def, string fileName, DateTime publishedAt, byte[] content, CancellationToken ct = default)
    {
        string sha = Convert.ToHexStringLower(SHA256.HashData(content));
        if (await db.Snapshots.AnyAsync(s => s.Dataset == def.Key && s.Sha256 == sha, ct))
            return new ImportOutcome(def.Key, false, $"файл {fileName} з таким самим вмістом уже імпортовано");

        // перший файл набору в журнал змін не пишемо, інакше там буде кілька тисяч "додано"
        bool first = !await db.Snapshots.AnyAsync(s => s.Dataset == def.Key, ct);
        string text = Encoding.UTF8.GetString(content);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var snapshot = new Snapshot
        {
            Dataset = def.Key,
            FileName = fileName,
            PublishedAt = publishedAt,
            ImportedAt = DateTime.UtcNow,
            Sha256 = sha
        };
        db.Snapshots.Add(snapshot);
        await db.SaveChangesAsync(ct);   // потрібен Id знімка

        List<RowIssue> issues;
        if (def.Kind == RegisterKind.Wholesale)
        {
            var file = WholesaleParser.Parse(text);
            issues = file.Issues;
            snapshot.TotalRows = file.TotalRows;
            snapshot.ImportedRows = file.Rows.Count;
            snapshot.SoleProprietors = file.SoleProprietors;

            var existing = await db.Participants.ToDictionaryAsync(p => p.EcrbCode, ct);
            var incoming = file.Rows.ToDictionary(r => r.EcrbCode, ToEntity);
            Sync(def.Key, existing, incoming, snapshot, first);
        }
        else
        {
            var file = MonopolyParser.Parse(text);
            issues = file.Issues;
            snapshot.TotalRows = file.TotalRows;
            snapshot.ImportedRows = file.Rows.Count;
            snapshot.SoleProprietors = file.SoleProprietors;

            var existing = await db.Monopolies.Where(m => m.Dataset == def.Key).ToDictionaryAsync(m => m.Key, ct);
            var incoming = file.Rows.ToDictionary(r => r.Key, r => ToEntity(r, def.Key));
            Sync(def.Key, existing, incoming, snapshot, first);
        }

        await db.Issues.Where(i => i.Dataset == def.Key).ExecuteDeleteAsync(ct);
        db.Issues.AddRange(issues.Select(i => new Issue
        {
            Dataset = def.Key,
            Line = i.Line,
            Key = i.Key,
            Code = i.Code,
            Value = i.Value
        }));
        snapshot.IssueCount = issues.Count;

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        log.LogInformation(
            "{Dataset}: {File}, записів {Rows}, зауважень {Issues}, +{Added} -{Removed} ~{Changed}",
            def.Key, fileName, snapshot.ImportedRows, snapshot.IssueCount, snapshot.Added, snapshot.Removed, snapshot.Changed);

        return new ImportOutcome(def.Key, true,
            $"{fileName}: записів {snapshot.ImportedRows}, зауважень {snapshot.IssueCount}");
    }

    /// <summary>
    /// Порівнює те, що лежить у базі, з новим файлом. Ключ словників це те,
    /// за чим запис впізнається між файлами.
    /// </summary>
    private void Sync<T>(
        string dataset, Dictionary<string, T> existing, Dictionary<string, T> incoming, Snapshot snapshot, bool first)
        where T : class, IRegisterRecord
    {
        foreach (var (key, fresh) in incoming)
        {
            if (!existing.Remove(key, out var current))
            {
                fresh.FirstSnapshotId = snapshot.Id;
                db.Add(fresh);
                if (!first)
                {
                    snapshot.Added++;
                    db.Changes.Add(NewChange(snapshot, dataset, key, fresh.Name, ChangeKind.Added));
                }
                continue;
            }

            // переносимо значення з файлу в запис із бази. EF сам бачить,
            // які поля після цього відрізняються від збережених
            fresh.Id = current.Id;
            fresh.FirstSnapshotId = current.FirstSnapshotId;
            var entry = db.Entry(current);
            entry.CurrentValues.SetValues(fresh);

            var modified = entry.Properties
                .Where(p => p.IsModified && !NotTracked.Contains(p.Metadata.Name))
                .ToList();
            if (modified.Count == 0) continue;

            snapshot.Changed++;
            foreach (var property in modified)
            {
                var change = NewChange(snapshot, dataset, key, current.Name, ChangeKind.Changed);
                change.Field = property.Metadata.Name;
                change.OldValue = AsText(property.OriginalValue);
                change.NewValue = AsText(property.CurrentValue);
                db.Changes.Add(change);
            }
        }

        // хто лишився в existing, того в новому файлі немає
        foreach (var (key, gone) in existing)
        {
            snapshot.Removed++;
            db.Changes.Add(NewChange(snapshot, dataset, key, gone.Name, ChangeKind.Removed));
            db.Remove(gone);
        }
    }

    private static Change NewChange(Snapshot snapshot, string dataset, string key, string name, ChangeKind kind) =>
        new() { Snapshot = snapshot, Dataset = dataset, Key = key, Name = name, Kind = kind };

    private static string? AsText(object? value) => value switch
    {
        null => null,
        DateOnly date => date.ToString("yyyy-MM-dd"),
        bool flag => flag ? "так" : "ні",
        _ => value.ToString()
    };

    private static Participant ToEntity(ParticipantRow row) => new()
    {
        EcrbCode = row.EcrbCode,
        Number = row.Number,
        Name = row.Name,
        SearchName = Names.SearchKey(row.Name),
        LegalForm = row.LegalForm,
        Address = row.Address,
        Settlement = row.Settlement,
        Zip = row.Zip,
        Country = row.Country,
        Eic = row.Eic,
        Bic = row.Bic,
        Lei = row.Lei,
        Vat = row.Vat,
        IdCode = row.IdCode,
        SubmittedOn = row.SubmittedOn,
        RegisteredOn = row.RegisteredOn,
        ExcludedOn = row.ExcludedOn,
        Website = row.Website,
        InsideInfoUrl = row.InsideInfoUrl
    };

    private static Monopoly ToEntity(MonopolyRow row, string dataset) => new()
    {
        Dataset = dataset,
        Key = row.Key,
        Edrpou = row.Edrpou,
        Name = row.Name,
        SearchName = Names.SearchKey(row.Name),
        Location = row.Location,
        Activity = row.Activity,
        Territory = row.Territory
    };
}
