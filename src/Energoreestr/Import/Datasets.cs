namespace Energoreestr.Import;

public enum RegisterKind { Wholesale, Monopolies }

/// <param name="Key">Коротка назва набору в нашій базі та в адресах API.</param>
/// <param name="PackageId">Ідентифікатор набору на data.gov.ua.</param>
/// <param name="FileHint">Частина імені файлу, якщо в наборі кілька CSV і потрібен конкретний.</param>
public record DatasetDef(string Key, string PackageId, RegisterKind Kind, string? FileHint = null);

public static class Datasets
{
    public const string Wholesale = "wholesale";
    public const string MonopoliesEnergy = "monopolies-energy";
    public const string MonopoliesHeatWater = "monopolies-heat-water";

    public static readonly DatasetDef[] All =
    [
        // у наборі два файли, український та англійський; беремо український
        new(Wholesale, "3ec102b6-7725-4b37-9a72-f502da845990", RegisterKind.Wholesale, "-ua-"),
        new(MonopoliesEnergy, "ed63c78a-761f-4cc7-8e85-29d311135c5e", RegisterKind.Monopolies),
        new(MonopoliesHeatWater, "7b662002-f5f3-4a1d-af43-2f68dc8acaf0", RegisterKind.Monopolies)
    ];

    public static DatasetDef? Find(string key) => All.FirstOrDefault(d => d.Key == key);

    /// <summary>
    /// Скільки днів може минути між оновленнями за заявленою на порталі періодичністю.
    /// null, якщо періодичність не вказана або незнайома.
    /// </summary>
    public static int? ExpectedDays(string? frequency) => frequency switch
    {
        "once a day" => 1,
        "once a week" => 7,
        "once a month" => 31,
        "once a quarter" => 92,
        "once a half year" => 183,
        "once a year" => 366,
        _ => null
    };
}
