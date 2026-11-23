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
    public const string Usage = "Використання: check [--json] [--strict] --register набір файл.csv";

    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, CancellationToken ct = default)
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

        var content = await File.ReadAllBytesAsync(options.Path, ct);
        var report = RegisterCheck.Run(options.Register!, content);
        var codes = RegisterCheck.Codes;

        await output.WriteAsync(options.Json ? ReportJson.Write(report, codes) + Environment.NewLine : ReportText.Write(report, codes));
        return ReportGroups.ExitCode(report, codes, options.Strict);
    }

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

        if (path is null || register is null) return false;
        options = new Options(path, json, strict, register);
        return true;
    }
}
