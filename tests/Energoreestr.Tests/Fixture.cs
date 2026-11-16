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
}
