using System.Text.RegularExpressions;
using HyperJav.Core.Models;

namespace HyperJav.Core.Parsing;

/// <summary>
/// 车牌识别（有码 / 无码 / 素人 / FC2）。规则见 docs/specs/carcode-rules.md。
/// 流程：去扩展名 → 中字/流出标记判定 → 标记/噪音剥离 → 分隔符归一 → 分类（FC2 → 素人 → 无码 → 有码）→ 车牌规范化。
/// </summary>
public static partial class CarCodeParser
{
    private const string Separators = " \t._-";

    // 有码——分隔形态：前缀（字母开头，可含数字，如 ABP / SSIS / T28 / GENS）+ 分隔符 + 2~5 位号码。
    private static readonly Regex YoumaSeparated = new(
        @"(?<prefix>[A-Za-z][A-Za-z0-9]{0,9})[-_ ]{1,3}(?<num>\d{2,5})(?![0-9])",
        RegexOptions.Compiled);

    // 有码——紧凑形态：纯字母前缀(≥2)紧邻数字，如 abp123 → ABP-123、ssis00123 → SSIS-123。
    private static readonly Regex YoumaCompact = new(
        @"(?<![0-9A-Za-z])(?<prefix>[A-Za-z]{2,10})(?<num>\d{2,5})(?![0-9])",
        RegexOptions.Compiled);

    // 素人：前缀 + 可选分隔 + 2~7 位号码（如 siro-2222、ore-456）。
    private static readonly Regex SurenCode = new(
        @"(?<prefix>[A-Za-z][A-Za-z0-9]{0,15})[-_ ]{0,3}(?<num>\d{2,7})(?![0-9])",
        RegexOptions.Compiled);

    // FC2：FC2[- ]PPV[- ]号码 / FC2-号码。
    private static readonly Regex Fc2Ppv = new(
        @"(?i)FC2[-_ ]?PPV[-_ ]?(?<num>\d{4,7})(?![0-9])", RegexOptions.Compiled);
    private static readonly Regex Fc2Direct = new(
        @"(?i)FC2[-_ ](?<num>\d{4,7})(?![0-9])", RegexOptions.Compiled);

    // 无码 Tokyo Hot（关键形态）。
    private static readonly Regex TokyoHot = new(
        @"(?i)tokyo[-_ ]?hot[-_ ]?n?[-_ ]?(?<num>\d{3,5})(?![0-9])", RegexOptions.Compiled);
    private static readonly Regex Heyzo = new(
        @"(?i)(?<![0-9A-Za-z])heyzo[-_ ]?(?<num>\d{3,4})(?![0-9])", RegexOptions.Compiled);
    private static readonly Regex ThreeXPlanet = new(
        @"(?i)3xplanet", RegexOptions.Compiled);
    private static readonly Regex DateCode = new(
        @"(?<![0-9])(?<date>\d{6})[-_ ](?<serial>\d{2,3})(?![0-9])", RegexOptions.Compiled);
    private static readonly Regex NakedNumberN = new(
        @"(?i)(?<![0-9A-Za-z])(?<prefix>n)[-_ ]?(?<num>\d{3,4})(?![0-9])", RegexOptions.Compiled);

    // 文件名首段（用于素人前缀比对）。
    private static readonly Regex LeadingSegment = new(@"^[A-Za-z0-9]+", RegexOptions.Compiled);

    public static CarCodeResult Parse(string fileName, CarCodeOptions options)
    {
        var raw = fileName.Trim();
        var stripped = StripVideoExtension(raw, options.VideoExtensions);

        // 1. 标记判定（在含扩展名去除后的名字上进行）。
        var subtitle = ContainsAny(stripped, options.SubtitleMarkers);
        var leak = ContainsAny(stripped, options.LeakMarkers);

        // 2. 预处理（剥离标记与噪音 token，分隔符归一）。
        var cleaned = Clean(stripped, options);

        // 3. 分类（命中即停）。
        return Classify(raw, cleaned, subtitle, leak, options);
    }

    private static string StripVideoExtension(string name, IReadOnlyList<string> extensions)
    {
        var dot = name.LastIndexOf('.');
        if (dot < 0)
        {
            return name;
        }
        var ext = name[(dot + 1)..];
        return extensions.Contains(ext, StringComparer.OrdinalIgnoreCase) ? name[..dot] : name;
    }

    private static bool ContainsAny(string text, IReadOnlyList<string> markers) =>
        markers.Any(m => m.Length > 0 && Regex.IsMatch(text, BoundaryPattern(m)));

    /// <summary>
    /// 边界感知 token 模式（标记判定与标记剥离共用同一套语义）：
    /// token 前须是字母数字/分隔符（或开头），后须非字母数字——
    /// 因此 "ABP-123-C" 命中 "-C"、"ABP-123_cd2" 不命中 "_C"（后面跟着字母 d）。
    /// </summary>
    private static string BoundaryPattern(string token) =>
        $"(?i)(?:^|(?<=[0-9A-Za-z{SeparatorsRegexClass}])){Regex.Escape(token)}(?![0-9A-Za-z])";

    /// <summary>清洗：标记/噪音剥离（元-边界感知：规整前缀/下缀），置分隔符归一。</summary>
    private static string Clean(string name, CarCodeOptions options)
    {
        // 前后补空格，保证边界规则在首尾可用。
        var text = $" {name} ";

        // 2.1 标记剥离（中字/流出标记，含 "-C"/"_C"）与噪音 token：
        //     边界感知（前一字符须为字母数字空白分隔，后一字符须非字母数字），
        //     避免误伤合法车牌内部的字符。
        var removalTokens = options.SubtitleMarkers
            .Concat(options.LeakMarkers)
            .Concat(options.YoumaSurenNoiseTokens)
            .Concat(options.WumaNoiseTokens)
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(t => t.Length) // 长者优先，如 "中文字幕" 先于 "字幕"
            .ToList();
        foreach (var token in removalTokens)
        {
            text = Regex.Replace(text, BoundaryPattern(token), string.Empty);
        }

        // 2.2 分隔符归一（连续分隔符归一为单个 '-'，去除首尾分隔符）。
        text = Regex.Replace(text, $"[{SeparatorsRegexClass}]{{2,}}", "-");
        return text.Trim(' ', '\t', '_', '-', '.');
    }

    private static string SeparatorsRegexClass => " \t._-";

    private static CarCodeResult Classify(
        string rawName, string cleaned, bool subtitle, bool leak, CarCodeOptions options)
    {
        // FC2。
        var fc2Match = Fc2Ppv.Match(cleaned);
        if (!fc2Match.Success)
        {
            fc2Match = Fc2Direct.Match(cleaned);
        }
        if (fc2Match.Success)
        {
            return Result(rawName, cleaned, $"FC2-{fc2Match.Groups["num"].Value}", CarCategory.Fc2, subtitle, leak);
        }

        // 素人：前缀命中前缀表，或文件名包含"素人"。
        var segment = LeadingSegment.Match(cleaned).Value;
        if ((segment.Length > 0 && options.SurenPrefixes.Contains(segment))
            || cleaned.Contains("素人", StringComparison.Ordinal))
        {
            var surenMatches = SurenCode.Matches(cleaned);
            if (surenMatches.Count > 0)
            {
                // 取最后一个号码段（膜除标签词干扰，如 "ore-800p-456" 类脏名）。
                var last = surenMatches[^1];
                var code = $"{last.Groups["prefix"].Value}-{last.Groups["num"].Value}";
                return Result(rawName, cleaned, code, CarCategory.Suren, subtitle, leak);
            }
            return Result(rawName, cleaned, null, CarCategory.Suren, subtitle, leak);
        }

        // 无码：关键词或无码形态。
        var tky = TokyoHot.Match(cleaned);
        if (tky.Success)
        {
            return Result(rawName, cleaned, $"n-{tky.Groups["num"].Value}", CarCategory.Wuma, subtitle, leak);
        }
        var hey = Heyzo.Match(cleaned);
        if (hey.Success)
        {
            return Result(rawName, cleaned, $"heyzo-{hey.Groups["num"].Value}", CarCategory.Wuma, subtitle, leak);
        }
        var date = DateCode.Match(cleaned);
        if (date.Success)
        {
            return Result(rawName, cleaned, date.Value, CarCategory.Wuma, subtitle, leak);
        }
        var naked = NakedNumberN.Match(cleaned);
        if (naked.Success)
        {
            return Result(rawName, cleaned, $"n-{naked.Groups["num"].Value}", CarCategory.Wuma, subtitle, leak);
        }
        if (ThreeXPlanet.IsMatch(cleaned) || cleaned.Contains("无码", StringComparison.Ordinal))
        {
            return Result(rawName, cleaned, null, CarCategory.Wuma, subtitle, leak);
        }

        // 有码（兜底形态）。
        var youma = ExtractYouma(cleaned);
        if (youma is not null)
        {
            return Result(rawName, cleaned, youma, CarCategory.Youma, subtitle, leak);
        }

        return Result(rawName, cleaned, null, CarCategory.Unknown, subtitle, leak);
    }

    private static CarCodeResult Result(
        string rawName, string cleaned, string? carCode, CarCategory category,
        bool subtitle, bool leak)
        => new()
        {
            RawName = rawName,
            CleanedName = cleaned,
            CarCode = carCode,
            Category = category,
            Subtitle = subtitle,
            Leak = leak,
            ScraperHint = category switch
            {
                CarCategory.Youma => ScrapeSource.Javlibrary,
                CarCategory.Wuma => ScrapeSource.JavbusWuma,
                CarCategory.Suren => ScrapeSource.Jav321,
                CarCategory.Fc2 => ScrapeSource.JavDbFc2,
                _ => ScrapeSource.None,
            },
        };

    /// <summary>有码提取：分隔形态 / 紧凑形态取最佳候选；0 开头号码清洗（>3 位时去前导零）。</summary>
    private static string? ExtractYouma(string cleaned)
    {
        var candidates = new List<(int Score, string Prefix, string Num)>();
        foreach (Match match in YoumaSeparated.Matches(cleaned))
        {
            var prefix = match.Groups["prefix"].Value;
            if (prefix.Length >= 2)
            {
                candidates.Add((prefix.Length, prefix, match.Groups["num"].Value));
            }
        }
        foreach (Match match in YoumaCompact.Matches(cleaned))
        {
            var prefix = match.Groups["prefix"].Value;
            candidates.Add((prefix.Length, prefix, match.Groups["num"].Value));
        }
        if (candidates.Count == 0)
        {
            return null;
        }

        var best = candidates
            .OrderByDescending(c => c.Score)
            .ThenBy(c => 0) // 稳定序（左优先），让结果可复现
            .First();
        return NormalizeYouma(best.Prefix, best.Num);
    }

    private static string NormalizeYouma(string prefix, string num)
    {
        // 数字保留原位数（不补零）；仅当超过常规 3 位时去前导零（ssis00123 → SSIS-123）。
        var digits = num.Length > 3 ? num.TrimStart('0') : num;
        if (digits.Length == 0)
        {
            digits = num;
        }
        return $"{prefix.ToUpperInvariant()}-{digits}";
    }
}
