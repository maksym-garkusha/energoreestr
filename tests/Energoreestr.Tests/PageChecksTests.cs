using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Energoreestr.Check;
using Jint;
using Jint.Native;

namespace Energoreestr.Tests;

/// <summary>
/// Сторінка перевірки має власну копію правил на JavaScript. Тут цей самий код, узятий
/// просто з site/check/index.html, проганяється на тих самих еталонах, що й C#: якщо
/// дві реалізації розійдуться, тест покаже, де саме.
/// </summary>
public class PageChecksTests
{
    private const string Today = "2026-11-23";
    private static readonly Encoding Windows1251 = CodePagesEncodingProvider.Instance.GetEncoding(1251)!;
    private static readonly string Code = ChecksBlock();

    private static string ChecksBlock()
    {
        string page = File.ReadAllText(Path.Combine(Repo.Root, "site", "check", "index.html"));
        var block = Regex.Match(page, "<script type=\"text/plain\" id=\"checks\">(.*?)</script>", RegexOptions.Singleline);
        Assert.True(block.Success, "На сторінці перевірки немає блоку з кодом перевірок");
        return block.Groups[1].Value;
    }

    private static Engine Load()
    {
        var engine = new Engine(o => o.TimeoutInterval(TimeSpan.FromSeconds(30)));
        engine.Execute(Code);
        return engine;
    }

    // у браузері це TextDecoder; тут той самий результат дає .NET
    private static Func<string, int, bool, string?> Decoder(byte[] bytes) => (label, offset, strict) =>
    {
        try
        {
            var encoding = label switch
            {
                "utf-8" => strict ? new UTF8Encoding(false, true) : Encoding.UTF8,
                "utf-16le" => Encoding.Unicode,
                "utf-16be" => Encoding.BigEndianUnicode,
                _ => Windows1251
            };
            return encoding.GetString(bytes, offset, bytes.Length - offset);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    };

    private static (string Json, string Text) RunPage(byte[] bytes)
    {
        var engine = Load();
        engine.SetValue("hostBytes", bytes.Select(b => (int)b).ToArray());
        engine.SetValue("hostDecode", Decoder(bytes));
        var result = engine.Evaluate($$"""
            (() => {
              const report = checkFile(Array.from(hostBytes), '{{Today}}', hostDecode);
              return [JSON.stringify(reportJson(report)), reportText(report)];
            })()
            """).AsArray();
        return (result[0].AsString(), result[1].AsString());
    }

    [Theory]
    [MemberData(nameof(CheckGoldenTests.Fixtures), MemberType = typeof(CheckGoldenTests))]
    public void Page_gives_the_same_json_as_the_expected_file(string name)
    {
        var (json, _) = RunPage(Fixture.Bytes(CheckGoldenTests.CsvFor(name)));

        var expected = JsonNode.Parse(Fixture.Text($"check/{name}.expected.json"));
        var actual = JsonNode.Parse(json);
        Assert.True(JsonNode.DeepEquals(expected, actual), $"Сторінка дала інший результат для {name}:\n{actual}");
    }

    [Theory]
    [MemberData(nameof(CheckGoldenTests.Fixtures), MemberType = typeof(CheckGoldenTests))]
    public void Page_report_text_is_the_same_as_the_command(string name)
    {
        var bytes = Fixture.Bytes(CheckGoldenTests.CsvFor(name));
        var (_, text) = RunPage(bytes);

        string command = ReportText.Write(FileCheck.Run(bytes, DateOnly.Parse(Today)), CheckCodes.All);
        Assert.Equal(command.ReplaceLineEndings("\n"), text);
    }

    [Theory]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 0x50, 0x4B, 0x03, 0x04 })]
    [InlineData(new byte[] { 0x41, 0x00, 0x42 })]
    [InlineData(new byte[] { 0xC0, 0x81, 0x82, 0x83, 0x84 })]
    public void Page_refuses_the_same_files_as_the_command(byte[] bytes)
    {
        var (json, _) = RunPage(bytes);

        string expected = ReportJson.Write(FileCheck.Run(bytes, DateOnly.Parse(Today)), CheckCodes.All);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), JsonNode.Parse(json)), json);
    }

    [Fact]
    public void Page_and_command_share_the_same_codes_levels_and_texts()
    {
        var codes = Load().Evaluate("JSON.stringify(CODES)").AsString();
        var page = JsonNode.Parse(codes)!.AsObject()
            .ToDictionary(p => p.Key, p => (p.Value![0]!.GetValue<string>(), p.Value![1]!.GetValue<string>()));
        var command = CheckCodes.All.ToDictionary(p => p.Key, p => (p.Value.Level.ToString().ToLowerInvariant(), p.Value.Text));

        Assert.Equal(command.OrderBy(p => p.Key, StringComparer.Ordinal), page.OrderBy(p => p.Key, StringComparer.Ordinal));
    }

    [Fact]
    public void Page_summarises_the_same_codes_by_column()
    {
        var page = Load().Evaluate("JSON.stringify([...COUNTED_BY_COLUMN])").AsString();

        Assert.Equal(CheckCodes.CountedByColumn.Order(), JsonNode.Parse(page)!.AsArray().Select(n => n!.GetValue<string>()).Order());
    }

    [Theory]
    [InlineData("1234567899", true)]
    [InlineData("1234567890", false)]
    [InlineData("0441234567", false)]
    [InlineData("4600000000", false)]
    public void Tax_number_rule_is_the_same_on_the_page(string value, bool expected)
    {
        JsValue result = Load().Evaluate($"plausibleTaxNumber('{value}', '{Today}')");

        Assert.Equal(expected, result.AsBoolean());
        Assert.Equal(expected, Cells.IsPlausibleTaxNumber(value, DateOnly.Parse(Today)));
    }
}
