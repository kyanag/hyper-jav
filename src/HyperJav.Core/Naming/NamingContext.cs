namespace HyperJav.Core.Naming;

/// <summary>命名模板的取值上下文（元素值 + 标记）。</summary>
public sealed record NamingContext
{
    /// <summary>车牌（"车牌"）。</summary>
    public string? CarCode { get; init; }

    /// <summary>标题（"标题"）。</summary>
    public string? Title { get; init; }

    /// <summary>全部演员（"全部演员"，用 <see cref="ActorSeparator"/> 连接）。</summary>
    public IReadOnlyList<string> Actors { get; init; } = [];

    /// <summary>原文件名（"原文件名"）。</summary>
    public string? OriginalFileName { get; init; }

    /// <summary>原文件夹名（"原文件夹名"）。</summary>
    public string? OriginalFolderName { get; init; }

    /// <summary>系列（"系列"）。</summary>
    public string? Series { get; init; }

    /// <summary>片商（"片商"）。</summary>
    public string? Studio { get; init; }

    /// <summary>发行日期（"发行日期"，yyyy-MM-dd）。</summary>
    public DateOnly? ReleaseDate { get; init; }

    /// <summary>影片类型标签（"类型"/"影片类型"，如 有码/无码/素人/FC2）。</summary>
    public string CategoryLabel { get; init; } = "";

    /// <summary>视频名（"视频"，通常为整理后的视频文件名，不含扩展名）。</summary>
    public string VideoName { get; init; } = "";

    /// <summary>是否中字（输出中字标记）。</summary>
    public bool Subtitle { get; init; }

    /// <summary>是否流出（输出发掘标记）。</summary>
    public bool Leak { get; init; }

    /// <summary>演员连接符。</summary>
    public string ActorSeparator { get; init; } = "、";
}
