namespace HyperJav.Core.Models;

/// <summary>车牌识别结果（结构见 docs/specs/carcode-rules.md 第 4 节）。</summary>
public sealed record CarCodeResult
{
    /// <summary>原始文件名（去路径后的完整名，含扩展名）。</summary>
    public required string RawName { get; init; }

    /// <summary>预处理后的文件名（去扩展名、去标记/噪音、分隔符归一），供调试与管线复用。</summary>
    public required string CleanedName { get; init; }

    /// <summary>规范化车牌；未能提取时为 null。</summary>
    public string? CarCode { get; init; }

    /// <summary>影片类型分类（判定顺序 FC2 → 素人 → 无码 → 有码，命中即停）。</summary>
    public required CarCategory Category { get; init; }

    /// <summary>是否中字（文件名包含中字标记，如 "-C"、"中文字幕"）。</summary>
    public bool Subtitle { get; init; }

    /// <summary>是否流出（文件名包含流出标记）。</summary>
    public bool Leak { get; init; }

    /// <summary>刮削路由建议（可被用户在 UI 中覆盖），Unknown 时为 None。</summary>
    public ScrapeSource ScraperHint { get; init; } = ScrapeSource.None;
}
