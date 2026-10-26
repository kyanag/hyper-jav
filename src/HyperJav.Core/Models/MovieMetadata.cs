namespace HyperJav.Core.Models;

/// <summary>
/// 统一元数据模型（各刮削源的公共契约，见 docs/specs/field-mapping.md 第 1 节）。
/// </summary>
public sealed record MovieMetadata
{
    /// <summary>规范化车牌。</summary>
    public required string CarCode { get; init; }

    /// <summary>标题（站点原文，未拼装）。</summary>
    public string? Title { get; init; }

    /// <summary>原标题（javlibrary / javbus 提供）。</summary>
    public string? OriginalTitle { get; init; }

    /// <summary>简介。</summary>
    public string? Plot { get; init; }

    /// <summary>概述（短简介，部分站点提供）。</summary>
    public string? Outline { get; init; }

    /// <summary>发行日期。</summary>
    public DateOnly? ReleaseDate { get; init; }

    /// <summary>导演（javbus / javdb 提供）。</summary>
    public string? Director { get; init; }

    /// <summary>片商。</summary>
    public string? Studio { get; init; }

    /// <summary>系列。</summary>
    public string? Series { get; init; }

    /// <summary>时长（分钟）。</summary>
    public int? RuntimeMinutes { get; init; }

    /// <summary>原始特征（规范化前，来自站点 source 值）。</summary>
    public IReadOnlyList<string> RawFeatures { get; init; } = [];

    /// <summary>演员。</summary>
    public IReadOnlyList<string> Actors { get; init; } = [];

    /// <summary>封面/海报下载源 URL（P2/P3 使用）。</summary>
    public string? PosterUrl { get; init; }

    /// <summary>远图（fanart）下载源 URL（P2/P3 使用）。</summary>
    public string? FanartUrl { get; init; }

    /// <summary>花絮图（javbus / javlibrary 提供，可选）。</summary>
    public IReadOnlyList<string> ExtraImages { get; init; } = [];

    /// <summary>评分（javlibrary / javdb 提供）。</summary>
    public double? Rating { get; init; }

    /// <summary>评票数。</summary>
    public int? Votes { get; init; }

    /// <summary>（javlibrary 开关：收集网友热评）热评列表。</summary>
    public IReadOnlyList<string> HotComments { get; init; } = [];

    /// <summary>来源站点。</summary>
    public ScrapeSource SourceSite { get; init; } = ScrapeSource.None;

    /// <summary>是否中字（继承自车牌识别结果）。</summary>
    public bool Subtitle { get; init; }

    /// <summary>是否流出（继承自车牌识别结果）。</summary>
    public bool Leak { get; init; }
}
