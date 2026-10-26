using System.Text.Json;
using HyperJav.Core.Configuration;
using HyperJav.Core.Models;
using HyperJav.Core.Parsing;
using Xunit;

namespace HyperJav.Core.Tests.Parsing;

/// <summary>车牌识别——用例集由 data/testcases/carcode-cases.json 驱动（null 字段不做断言）。</summary>
public sealed class CarCodeParserTests
{
    public static TheoryData<string, string, string?, string, string> Cases()
    {
        var theory = new TheoryData<string, string, string?, string, string>();
        var path = Path.Combine(
            AppContext.BaseDirectory, "TestData", "Parsing", "carcode-cases.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var caseElement in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            var fileName = caseElement.GetProperty("fileName").GetString()!;
            var expected = caseElement.GetProperty("expected");
            string? carCode = GetStringOrNull(expected, "carCode");
            var category = expected.GetProperty("category").GetString()!;
            string? subtitle = null;
            string? leak = null;
            if (expected.TryGetProperty("flags", out var flags) && flags.ValueKind == JsonValueKind.Object)
            {
                subtitle = GetNullableBoolean(flags, "subtitle")?.ToString();
                leak = GetNullableBoolean(flags, "leak")?.ToString();
            }
            // 不存在的标记传 "-"（bool.TryParse 失败则不断言）。
            theory.Add(fileName, category, carCode, subtitle ?? "-", leak ?? "-");
        }
        return theory;
    }

    private static string? GetStringOrNull(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool? GetNullableBoolean(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null,
            }
            : null;

    [Theory]
    [MemberData(nameof(Cases))]
    public void Parses_test_case(
        string fileName, string expectedCategory, string? expectedCarCode,
        string subtitleMarker, string leakMarker)
    {
        var result = CarCodeParser.Parse(fileName, DefaultOptions());

        Assert.True(
            Enum.TryParse<CarCategory>(expectedCategory, out var expected),
            $"未知分类 {expectedCategory}");
        Assert.Equal(expected, result.Category);

        if (expectedCarCode is not null)
        {
            Assert.Equal(expectedCarCode, result.CarCode);
        }

        if (bool.TryParse(subtitleMarker, out var expectedSubtitle))
        {
            Assert.Equal(expectedSubtitle, result.Subtitle);
        }
        if (bool.TryParse(leakMarker, out var expectedLeak))
        {
            Assert.Equal(expectedLeak, result.Leak);
        }
    }

    [Fact]
    public void Junk_prefix_does_not_overwhelm_car_code()
    {
        // 覆盖 h_1138-GENS-048 的记分规则：1 字符前缀候选被过滤。
        var options = DefaultOptions();
        var result = CarCodeParser.Parse("h_1138-GENS-048.mkv", options);
        Assert.Equal(CarCategory.Youma, result.Category);
        Assert.Equal("GENS-048", result.CarCode);
    }

    [Theory]
    [InlineData("ABP-123_cd2.mp4", false)]  // 边界规则：_C 后面跟着字母 d，不命中。
    [InlineData("ABP-123-C.mp4", true)]     // 文件名末尾的 -C 命中。
    [InlineData("ABP-123C.mp4", false)]     // 无分隔符的紧凑车牌不误判。
    [InlineData("ABP-123_中字.mp4", true)]  // 中文标记不受影响。
    public void Subtitle_markers_are_boundary_aware(string fileName, bool expectedSubtitle)
    {
        var options = DefaultOptions();
        var result = CarCodeParser.Parse(fileName, options);
        Assert.Equal(expectedSubtitle, result.Subtitle);
        if (expectedSubtitle)
        {
            // 命中标记的车牌本体不受剥离干扰。
            Assert.Equal("ABP-123", result.CarCode);
        }
    }

    [Fact]
    public void Leading_zeros_are_only_trimmed_over_three_digits()
    {
        var options = DefaultOptions();
        // "JUL-001"：3 位保留（不补也不删）；"ssis00123"：5 位去前导零。
        Assert.Equal("JUL-001", CarCodeParser.Parse("JUL-001_中文字幕_mm616.wmv", options).CarCode);
        Assert.Equal("SSIS-123", CarCodeParser.Parse("ssis00123.iso", options).CarCode);
    }

    [Fact]
    public void Suren_prefix_from_json_routes_suren()
    {
        var options = DefaultOptions();
        var result = CarCodeParser.Parse("gana-1865.mkv", options);
        Assert.Equal(CarCategory.Suren, result.Category);
        Assert.Equal(ScrapeSource.Jav321, result.ScraperHint);
    }

    private static CarCodeOptions DefaultOptions() => CarCodeOptions.Default(
        SurenPrefixTable.Load(TestPaths.Data("suren-prefixes.json")).Prefixes);
}
