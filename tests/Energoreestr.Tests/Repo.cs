namespace Energoreestr.Tests;

public static class Repo
{
    public static readonly string Root = FindRoot();

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Energoreestr.slnx")))
                return dir.FullName;
        throw new FileNotFoundException("Не знайдено корінь репозиторію з Energoreestr.slnx");
    }
}
