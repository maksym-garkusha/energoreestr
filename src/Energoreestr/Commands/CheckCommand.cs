using Energoreestr.Check;
using Energoreestr.Import;

namespace Energoreestr.Commands;

/// <summary>
/// dotnet run -- check [--json] [--strict] [--register набір] файл.csv
///
/// Нічого не записує і нікуди не ходить: читає файл і друкує зауваження.
/// Код повернення 0, 1 або 2, див. <see cref="ReportGroups.ExitCode"/>.
/// </summary>
public static class CheckCommand
{
    public const string Usage = "Використання: check [--json] [--strict] [--register набір] файл.csv";

    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error)
    {
        if (!TryParse(args, out var options))
        {
            await error.WriteLineAsync(Usage);
            await error.WriteLineAsync("Набори для --register: " + string.Join(", ", Datasets.All.Select(d => d.Key)));
            return 2;
        }
        if (!File.Exists(options.Path))
        {
            await error.WriteLineAsync($"Файл не знайдено: {options.Path}");
            return 2;
        }

        if (new FileInfo(options.Path).Length > MaxBytes)
        {
            await error.WriteLineAsync("Файл більший за 1 ГБ, такий команда не перевіряє: розділіть його на кілька менших.");
            return 2;
        }

        try
        {
            var content = await File.ReadAllBytesAsync(options.Path);
            var (report, codes) = options.Register is { } def
                ? (RegisterCheck.Run(def, content), RegisterCheck.Codes)
                : (FileCheck.Run(content), CheckCodes.All);

            await output.WriteAsync(options.Json ? ReportJson.Write(report, codes) + Environment.NewLine : ReportText.Write(report, codes));
            return ReportGroups.ExitCode(report, codes, options.Strict);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            await error.WriteLineAsync("Не вдалося прочитати файл: немає доступу або його відкрито в іншій програмі.");
            return 2;
        }
        catch (Exception)
        {
            // Текст винятку не друкуємо: у ньому буває шматок файлу, а стек людині нічого не скаже
            await error.WriteLineAsync("Не вдалося перевірити файл через помилку в програмі. Якщо можете, повідомте про це в issues репозиторію.");
            return 2;
        }
    }

    // увесь файл читається в пам'ять, тож понад це команда його не бере
    private const long MaxBytes = 1L << 30;

    private record Options(string Path, bool Json, bool Strict, DatasetDef? Register);

    private static bool TryParse(string[] args, out Options options)
    {
        options = new Options("", false, false, null);
        string? path = null;
        bool json = false, strict = false;
        DatasetDef? register = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--json": json = true; break;
                case "--strict": strict = true; break;
                case "--register" when i + 1 < args.Length && Datasets.Find(args[i + 1]) is { } def:
                    register = def;
                    i++;
                    break;
                case var arg when !arg.StartsWith("--", StringComparison.Ordinal) && path is null:
                    path = arg;
                    break;
                default:
                    return false;
            }
        }

        if (path is null) return false;
        options = new Options(path, json, strict, register);
        return true;
    }
}
