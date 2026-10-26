namespace HyperJav.Core.Models;

/// <summary>影片类型：原版 JAVSDT 的"有码/无码/素人/FC2"四分类（见 docs/specs/carcode-rules.md）。</summary>
public enum CarCategory
{
    /// <summary>无法识别。</summary>
    Unknown = 0,

    /// <summary>有码。</summary>
    Youma = 1,

    /// <summary>无码。</summary>
    Wuma = 2,

    /// <summary>素人。</summary>
    Suren = 3,

    /// <summary>FC2。</summary>
    Fc2 = 4,
}
