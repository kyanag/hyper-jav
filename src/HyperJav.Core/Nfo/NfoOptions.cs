using HyperJav.Core.Models;

namespace HyperJav.Core.Nfo;

/// <summary>NFO 输出选项（对齐原版 [收集nfo] 与 全局开关，见 docs/specs/field-mapping.md）。</summary>
public sealed class NfoOptions
{
    /// <summary>nfo中title的格式。</summary>
    public string TitleTemplate { get; init; } = "车牌+空格+标题";

    /// <summary>是否去除标题末尾的演员姓名？</summary>
    public bool RemoveTrailingActorNames { get; init; }

    /// <summary>是否将特征保存到genre？</summary>
    public bool SaveFeaturesToGenre { get; init; } = true;

    /// <summary>是否将特征保存到tag？</summary>
    public bool SaveFeaturesToTag { get; init; } = true;

    /// <summary>额外将以下元素添加到特征中。</summary>
    public IReadOnlyList<ExtraFeatureElement> ExtraFeatureElements { get; init; } =
        [ExtraFeatureElement.Series, ExtraFeatureElement.Studio];

    /// <summary>是否中字的表现形式（㊥）。</summary>
    public string SubtitleBadge { get; init; } = "㊥";

    /// <summary>是否流出的表现形式（无码流出）。</summary>
    public string LeakBadge { get; init; } = "无码流出";

    /// <summary>简繁中文？（决定特征取 simplified / traditional）。</summary>
    public Configuration.ScriptMode Script { get; init; } = Configuration.ScriptMode.Simplified;

    /// <summary>特征对照表（可选；未提供则特征原样透传）。</summary>
    public Normalization.FeatureDictionary? FeatureDictionary { get; init; }

    /// <summary>海报文件名（写入 &lt;poster&gt;；由 P3 管线按"海报的格式"渲染，可选）。</summary>
    public string? PosterFileName { get; init; }

    /// <summary>远图文件名（写入 &lt;fanart&gt;，由"封面"模板渲染，可选）。</summary>
    public string? FanartFileName { get; init; }

    /// <summary>文件名（视频名，不含扩展名；用于 naming 上下文，可选）。</summary>
    public string? VideoName { get; init; }
}
