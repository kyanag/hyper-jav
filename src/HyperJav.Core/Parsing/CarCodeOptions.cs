using HyperJav.Core.Configuration;
using HyperJav.Core.Models;

namespace HyperJav.Core.Parsing;

/// <summary>车牌识别的全部可配置输入（规则引擎化：字面量一律来自 ini / data/*.json，见 docs/specs/carcode-rules.md）。</summary>
public sealed class CarCodeOptions
{
    public required IReadOnlyList<string> YoumaSurenNoiseTokens { get; init; }

    public required IReadOnlyList<string> WumaNoiseTokens { get; init; }

    public required IReadOnlyList<string> SubtitleMarkers { get; init; }

    public required IReadOnlyList<string> LeakMarkers { get; init; }

    public required IReadOnlyList<string> VideoExtensions { get; init; }

    public required IReadOnlySet<string> SurenPrefixes { get; init; }

    /// <summary>从 AppConfig 默认/已加载配置 + 素人前缀表 组装。</summary>
    public static CarCodeOptions FromConfig(AppConfig config, IReadOnlySet<string> surenPrefixes) => new()
    {
        YoumaSurenNoiseTokens = config.MovieNature.YoumaSurenNoiseTokens,
        WumaNoiseTokens = config.MovieNature.WumaNoiseTokens,
        SubtitleMarkers = config.MovieNature.SubtitleMarkers,
        LeakMarkers = config.MovieNature.LeakMarkers,
        VideoExtensions = config.Misc.ScanExtensions,
        SurenPrefixes = surenPrefixes,
    };

    /// <summary>用 AppConfig 默认值（tests / 无配置场景）。</summary>
    public static CarCodeOptions Default(IReadOnlySet<string> surenPrefixes) =>
        FromConfig(new AppConfig(), surenPrefixes);
}
