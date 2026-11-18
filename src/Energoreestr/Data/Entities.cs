using System.Text.Json.Serialization;

namespace Energoreestr.Data;

/// <summary>Один завантажений файл реєстру.</summary>
public class Snapshot
{
    public int Id { get; set; }
    public string Dataset { get; set; } = "";
    public string FileName { get; set; } = "";
    /// <summary>Коли файл оновили на порталі (UTC).</summary>
    public DateTime PublishedAt { get; set; }
    public DateTime ImportedAt { get; set; }
    public string Sha256 { get; set; } = "";

    public int TotalRows { get; set; }
    public int ImportedRows { get; set; }
    public int IssueCount { get; set; }
    /// <summary>Записи про фізичних осіб-підприємців. Їх тільки рахуємо, у базі їх немає.</summary>
    public int SoleProprietors { get; set; }

    // скільки записів з'явилося, зникло і змінилося проти попереднього файлу
    public int Added { get; set; }
    public int Removed { get; set; }
    public int Changed { get; set; }
}

/// <summary>Спільне для записів обох реєстрів: те, що потрібно для порівняння файлів.</summary>
public interface IRegisterRecord
{
    int Id { get; set; }
    string Name { get; }
    /// <summary>Файл, у якому запис з'явився вперше.</summary>
    int FirstSnapshotId { get; set; }
}

/// <summary>Учасник оптового енергетичного ринку, стан за останнім файлом.</summary>
public class Participant : IRegisterRecord
{
    [JsonIgnore] public int Id { get; set; }
    public string EcrbCode { get; set; } = "";
    public int Number { get; set; }

    public string Name { get; set; } = "";
    /// <summary>Назва, приведена до вигляду для пошуку, див. Names.SearchKey.</summary>
    [JsonIgnore] public string SearchName { get; set; } = "";
    public string LegalForm { get; set; } = "";

    public string? Address { get; set; }
    public string? Settlement { get; set; }
    public string? Zip { get; set; }
    public string Country { get; set; } = "";

    public string? Eic { get; set; }
    public string? Bic { get; set; }
    public string? Lei { get; set; }
    public string? Vat { get; set; }
    /// <summary>Для українських компаній це ЄДРПОУ, для іноземних код їхньої країни.</summary>
    public string? IdCode { get; set; }

    public DateOnly SubmittedOn { get; set; }
    public DateOnly RegisteredOn { get; set; }
    public DateOnly? ExcludedOn { get; set; }

    public string? Website { get; set; }
    public string? InsideInfoUrl { get; set; }

    public int FirstSnapshotId { get; set; }
}

/// <summary>Запис реєстру природних монополій (енергетика або тепло і вода).</summary>
public class Monopoly : IRegisterRecord
{
    [JsonIgnore] public int Id { get; set; }
    public string Dataset { get; set; } = "";
    [JsonIgnore] public string Key { get; set; } = "";

    public string? Edrpou { get; set; }
    public string Name { get; set; } = "";
    [JsonIgnore] public string SearchName { get; set; } = "";
    public string Location { get; set; } = "";
    public string Activity { get; set; } = "";
    public string Territory { get; set; } = "";

    public int FirstSnapshotId { get; set; }
}

public enum ChangeKind { Added, Removed, Changed }

/// <summary>Що змінилося в записі між двома файлами.</summary>
public class Change
{
    public int Id { get; set; }
    public int SnapshotId { get; set; }
    public Snapshot? Snapshot { get; set; }

    public string Dataset { get; set; } = "";
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public ChangeKind Kind { get; set; }

    // заповнені тільки для Changed
    public string? Field { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
}

/// <summary>Зауваження до рядка останнього файлу. Старі при новому імпорті замінюються.</summary>
public class Issue
{
    public int Id { get; set; }
    public string Dataset { get; set; } = "";
    public int Line { get; set; }
    public string Key { get; set; } = "";
    public string Code { get; set; } = "";
    public string? Value { get; set; }
}

/// <summary>Що портал каже про набір: як часто його обіцяють оновлювати і коли оновили востаннє.</summary>
public class DatasetState
{
    public string Dataset { get; set; } = "";
    public string Title { get; set; } = "";
    public string? UpdateFrequency { get; set; }
    public string? ResourceName { get; set; }
    public DateTime? ResourceModifiedAt { get; set; }
    public DateTime CheckedAt { get; set; }
}
