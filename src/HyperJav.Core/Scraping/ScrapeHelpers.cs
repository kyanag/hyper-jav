using System.Globalization;
using System.Text.RegularExpressions;

namespace HyperJav.Core.Scraping;

/// <summary>URL 构造与绝对化。</summary>
public static class ScrapeSites
{
    /// <summary>Jav321 基础地址（原版无配置项，固定域名；代理仍按 [局部代理] 开关）。</summary>
    public const string Jav321Base = "https://www.jav321.com";

    /// <summary>相对/协议相对 href → 绝对 URL（baseUrl 须以 "/" 结尾）。</summary>
    public static string Absolutize(string? href, string baseUrl) => href switch
    {
        null or "" => "",
        _ when href.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || href.StartsWith("http://", StringComparison.OrdinalIgnoreCase) => href,
        _ when href.StartsWith("//", StringComparison.Ordinal) => "https:" + href,
        _ => new Uri(new Uri(baseUrl), href).AbsoluteUri,
    };
}

/// <summary>站点页面通用字段提取辅助（类型解析、脏值清理、标题车牌剥离）。</summary>
public static partial class ScrapeHelpers
{
    /// <summary>站点标题常以车牌开头/内嵌车牌；去掉车牌字样与首尾残留分隔符。</summary>
    public static string? TrimCarCode(string? title, string? carCode)
    {
        var text = CollapseWhitespace(title);
        if (text is null || string.IsNullOrWhiteSpace(carCode))
        {
            return text;
        }
        var stripped = text.Replace(carCode, " ").Trim('-', '－', '　', ' ');
        return stripped.Length > 0 ? stripped : text;
    }

    /// <summary>把 "120 分鐘" / "ABP-123" 等 → 整数（首个数字串）。</summary>
    public static int? Int(string? text)
    {
        var match = DigitRegex().Match(text ?? "");
        return match.Success
            && int.TryParse(match.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    /// <summary>评分如 "4.56"（含全角句点宽容）。</summary>
    public static double? Double(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        var normalized = text.Trim().Replace('。', '.').Replace('．', '.');
        var match = ScoreRegex().Match(normalized);
        return match.Success
            && double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    /// <summary>日期如 2019-01-25 / 2019/01/25 / 2019.01.25。</summary>
    public static DateOnly? Date(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        var match = DateRegex().Match(text);
        while (match.Success)
        {
            if (DateOnly.TryParseExact(
                    match.Value.Replace('/', '-').Replace('.', '-'),
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var value))
            {
                return value;
            }
            match = match.NextMatch();
        }
        return null;
    }

    /// <summary>javlibrary 用 "-" 表示缺失（可连续多个）。</summary>
    public static string? DashToNull(string? text)
    {
        var trimmed = CollapseWhitespace(text);
        return string.IsNullOrEmpty(trimmed) || DashesOnly(trimmed) ? null : trimmed;
    }

    private static bool DashesOnly(string text) =>
        text.All(c => c is '-' or '－') && text.Length > 0;

    /// <summary>压缩全部空白为单空格。</summary>
    public static string? CollapseWhitespace(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        return WhitespaceRegex().Replace(text, " ").Trim();
    }

    /// <summary>按常见分隔符拆演员名。</summary>
    public static IReadOnlyList<string> SplitNames(string? text) => string.IsNullOrWhiteSpace(text)
        ? []
        : CollapseWhitespace(text)!
            .Split(['、', ',', '，', '/'])
            .Select(p => p.Trim(' ', '　', '，', ','))
            .Where(p => p.Length > 0)
            .ToArray();

    [GeneratedRegex(@"\d+")]
    private static partial Regex DigitRegex();

    [GeneratedRegex(@"\d+(?:\.\d+)?")]
    private static partial Regex ScoreRegex();

    [GeneratedRegex(@"\d{4}[-/.]\d{1,2}[-/.]\d{1,2}")]
    private static partial Regex DateRegex();

    [GeneratedRegex(@"[\t\n\r\u00A0\u3000]+")]
    private static partial Regex WhitespaceRegex();
}
