using System.Diagnostics;
using System.Text.Json;
using Energoreestr.Commands;
using Energoreestr.Registers;

namespace Energoreestr.Tests;

public class CheckCommandTests
{
    private static async Task<(int Code, string Output, string Error)> Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        int code = await CheckCommand.RunAsync(args, output, error);
        return (code, output.ToString(), error.ToString());
    }

    [Theory]
    [InlineData("wholesale", "wholesale-sample.csv")]
    [InlineData("monopolies-energy", "energy-sample.csv")]
    [InlineData("monopolies-heat-water", "heat-water-sample.csv")]
    public async Task Register_mode_reports_the_same_issues_as_the_parser(string register, string fixture)
    {
        string text = Fixture.Text(fixture);
        var issues = register == "wholesale" ? WholesaleParser.Parse(text).Issues : MonopolyParser.Parse(text).Issues;

        var (_, output, _) = await Run("--json", "--register", register, Fixture.Path(fixture));

        var groups = JsonDocument.Parse(output).RootElement.GetProperty("groups").EnumerateArray()
            .ToDictionary(g => g.GetProperty("code").GetString()!, g => g.GetProperty("count").GetInt32());
        Assert.Equal(issues.GroupBy(i => i.Code).ToDictionary(g => g.Key, g => g.Count()), groups);
    }

    [Fact]
    public async Task Text_report_names_each_kind_of_issue_with_line_numbers()
    {
        var (code, output, _) = await Run("--register", "monopolies-energy", Fixture.Path("energy-sample.csv"));

        // чотири коди без нулів на початку: це треба поправити
        Assert.Equal(1, code);
        Assert.Contains("Поправити", output);
        Assert.Contains("У коді ЄДРПОУ загублено нулі на початку: 4 рядки", output);
        Assert.Contains("рядок 3: «3346331»", output);
    }

    [Fact]
    public async Task Sole_proprietor_rows_appear_without_values_in_every_format()
    {
        var (_, json, _) = await Run("--json", "--register", "monopolies-heat-water", Fixture.Path("heat-water-sample.csv"));
        var (_, text, _) = await Run("--register", "monopolies-heat-water", Fixture.Path("heat-water-sample.csv"));

        Assert.Contains("\"personalDataRows\": 3", json);
        Assert.Contains("Рядків з ознаками персональних даних: 3", text);
        foreach (var trace in Fixture.SoleProprietorTraces)
        {
            Assert.DoesNotContain(trace, json);
            Assert.DoesNotContain(trace, text);
        }
    }

    [Theory]
    [InlineData("check/personal-data.csv")]
    [InlineData("heat-water-sample.csv")]
    [InlineData("wholesale-sample.csv")]
    public async Task General_check_shows_no_trace_of_invented_sole_proprietors_in_text_or_json(string fixture)
    {
        var (_, text, _) = await Run(Fixture.Path(fixture));
        var (_, json, _) = await Run("--json", Fixture.Path(fixture));

        Assert.Contains("Рядків з ознаками персональних даних", text);
        Assert.DoesNotContain("\"personalDataRows\": 0", json);
        foreach (var trace in Fixture.SoleProprietorTraces)
        {
            Assert.DoesNotContain(trace, text);
            Assert.DoesNotContain(trace, json);
        }
    }

    [Theory]
    [InlineData("check/clean.csv", false, 0)]
    [InlineData("check/utf8-no-bom.csv", false, 0)]   // лише «до відома»
    [InlineData("check/no-header.csv", false, 0)]     // лише «варто звірити»
    [InlineData("check/no-header.csv", true, 1)]
    [InlineData("check/excel-traces.csv", false, 1)]
    public async Task General_check_exit_code_follows_the_most_serious_finding(string fixture, bool strict, int expected)
    {
        string[] args = strict ? ["--strict", Fixture.Path(fixture)] : [Fixture.Path(fixture)];

        var (code, _, _) = await Run(args);

        Assert.Equal(expected, code);
    }

    [Fact]
    public async Task Excel_workbook_is_refused_with_a_hint_and_exit_code_2()
    {
        string path = Path.Combine(Path.GetTempPath(), $"check-{Guid.NewGuid():N}.xlsx");
        await File.WriteAllBytesAsync(path, [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00]);
        try
        {
            var (code, text, _) = await Run(path);
            var (_, json, _) = await Run("--json", path);

            Assert.Equal(2, code);
            Assert.Contains("CSV UTF-8", text);
            Assert.Contains("\"problem\"", json);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static async Task<(int Code, string Output, string Error)> RunProgram(params string[] args)
    {
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Energoreestr.dll"));
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        string output = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, output, error);
    }

    [Fact]
    public async Task Check_is_found_wherever_it_stands_and_in_any_case()
    {
        var (code, output, _) = await RunProgram("--json", "CHECK", Fixture.Path("check/clean.csv"));

        Assert.Equal(0, code);
        Assert.Contains("\"schema\": 1", output);
    }

    [Fact]
    public async Task Unknown_command_prints_a_hint_instead_of_starting_the_server()
    {
        var (code, _, error) = await RunProgram(Fixture.Path("check/clean.csv"));

        Assert.Equal(2, code);
        Assert.Contains("Невідома команда", error);
    }

    [Theory]
    [InlineData]
    [InlineData("--register", "wholesale")]
    [InlineData("--register", "no-such-register", "file.csv")]
    [InlineData("--unknown", "--register", "wholesale", "file.csv")]
    [InlineData("--register", "wholesale", "one.csv", "two.csv")]
    public async Task Wrong_arguments_print_usage_and_return_2(params string[] args)
    {
        var (code, output, error) = await Run(args);

        Assert.Equal(2, code);
        Assert.Empty(output);
        Assert.Contains("Використання", error);
    }

    [Fact]
    public async Task Missing_file_returns_2()
    {
        var (code, _, error) = await Run("--register", "wholesale", "/no/such/file.csv");

        Assert.Equal(2, code);
        Assert.Contains("не знайдено", error);
    }

    // Запускаємо справжню програму в порожній теці: команда не повинна створити
    // ні базу, ні будь-який інший файл, бо обробляється раніше за застосунок
    [Fact]
    public async Task Check_command_writes_nothing_to_the_working_directory()
    {
        var dir = Directory.CreateTempSubdirectory("check-");
        try
        {
            var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
            {
                WorkingDirectory = dir.FullName,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var arg in new[] { Path.Combine(AppContext.BaseDirectory, "Energoreestr.dll"),
                         "check", "--register", "monopolies-energy", Fixture.Path("energy-sample.csv") })
                start.ArgumentList.Add(arg);

            using var process = Process.Start(start)!;
            string output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            Assert.Equal(1, process.ExitCode);
            Assert.Contains("нулі на початку", output);
            Assert.Empty(dir.EnumerateFileSystemInfos());
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
