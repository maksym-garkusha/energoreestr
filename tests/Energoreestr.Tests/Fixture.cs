namespace Energoreestr.Tests;

/// <summary>
/// Файли з папки Fixtures. Це справжні рядки з реєстрів НКРЕКП (відкриті дані, CC BY),
/// відібрані так, щоб у них були всі відомі вади. Записи про підприємців вигадані.
/// </summary>
public static class Fixture
{
    public static string Path(string name) =>
        System.IO.Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    public static string Text(string name) => File.ReadAllText(Path(name));

    public static byte[] Bytes(string name) => File.ReadAllBytes(Path(name));

    /// <summary>
    /// Імена, коди, адреси й сайти вигаданих підприємців зі зразків. Жоден із цих рядків
    /// не має з'явитися ні в базі, ні в API, ні в data.json, ні у звіті.
    /// </summary>
    public static readonly string[] SoleProprietorTraces =
    [
        "ТЕСТОВИЙ", "Тестовий", "ВИГАДАН", "Вигадан", "вигадан", "ОКСAНА",
        "1234567890", "1234567899", "2345678901", "123 456 7899", "123-456-7899",
        "39999", "62x0000000000017", "vyhadanyi",
        "ECRB-UA-20250203-099", "ECRB-UA-20250303-098", "ECRB-UA-20250401-097"
    ];
}
