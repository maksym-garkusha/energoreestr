using Energoreestr.Codes;

namespace Energoreestr.Tests;

public class ChecksLibraryTests
{
    // Перевірка файлу живе в окремій збірці, щоб було видно без читання коду:
    // вона не може ні піти в мережу через наш клієнт порталу, ні записати щось у базу
    [Fact]
    public void Checks_library_references_only_the_standard_library()
    {
        var references = typeof(Edrpou).Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();

        Assert.NotEmpty(references);
        Assert.All(references, name => Assert.StartsWith("System", name));
        Assert.DoesNotContain(references, name => name.StartsWith("System.Net", StringComparison.Ordinal));
    }
}
