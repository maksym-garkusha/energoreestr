using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Energoreestr.Data;

public class RegistryDb(DbContextOptions<RegistryDb> options) : DbContext(options)
{
    public DbSet<Snapshot> Snapshots => Set<Snapshot>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<Monopoly> Monopolies => Set<Monopoly>();
    public DbSet<Change> Changes => Set<Change>();
    public DbSet<Issue> Issues => Set<Issue>();
    public DbSet<DatasetState> DatasetStates => Set<DatasetState>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configuration)
    {
        // SQLite зберігає час текстом без зони. Ми завжди пишемо UTC,
        // тож при читанні повертаємо цю позначку, інакше в JSON час виходить без "Z"
        configuration.Properties<DateTime>().HaveConversion<UtcConverter>();
    }

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Snapshot>().HasIndex(s => new { s.Dataset, s.PublishedAt });

        model.Entity<Participant>(e =>
        {
            e.HasIndex(p => p.EcrbCode).IsUnique();
            e.HasIndex(p => p.IdCode);
            e.HasIndex(p => p.Eic);
        });

        model.Entity<Monopoly>(e =>
        {
            e.HasIndex(m => new { m.Dataset, m.Key }).IsUnique();
            e.HasIndex(m => m.Edrpou);
        });

        model.Entity<Change>(e =>
        {
            e.HasIndex(c => c.Key);
            e.Property(c => c.Kind).HasConversion<string>();
        });

        model.Entity<Issue>().HasIndex(i => new { i.Dataset, i.Code });

        model.Entity<DatasetState>().HasKey(d => d.Dataset);
    }
}

public class UtcConverter() : ValueConverter<DateTime, DateTime>(
    value => value,
    value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
