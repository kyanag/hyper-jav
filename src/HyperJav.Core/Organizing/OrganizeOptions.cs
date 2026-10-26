using HyperJav.Core.Configuration;
using HyperJav.Core.Models;
using HyperJav.Core.Nfo;
using HyperJav.Core.Normalization;
using HyperJav.Core.Parsing;
using HyperJav.Core.Scraping;

namespace HyperJav.Core.Organizing;

/// <summary>整理状态。</summary>
public enum OrganizeStatus
{
    /// <summary>完成（全部目标动作成功）。</summary>
    Success = 1,

    /// <summary>部分完成（刮削成功但个别文件操作有注意点；详情看 Message）。</summary>
    Partial = 2,

    /// <summary>整体跳过（已有 nfo / 未识别车牌 / 无刮削数据）。</summary>
    Skipped = 3,

    /// <summary>出错（详见 Message）。</summary>
    Error = 4,
}

/// <summary>单个视频的整理结果（清单输出内容）。</summary>
public sealed record OrganizeOutcome(
    string OriginalPath,
    OrganizeStatus Status,
    string? CarCode,
    CarCategory Category,
    string Message)
{
    public string? NewPath { get; init; }

    public string? NewFolder { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = [];
}

/// <summary>整批整理报告。</summary>
public sealed record OrganizeReport(IReadOnlyList<OrganizeOutcome> Outcomes, string? ChecklistPath)
{
    public int Count(OrganizeStatus status) => Outcomes.Count(o => o.Status == status);
}

/// <summary>
/// 整理配置（一眼对应原版 ini 各节； truncation 只作用于重命名/文件夹/封面名，nfo title 不截断，见 p1-notes 决策 9）。
/// record：测试/UI 可用 with 派生变体。
/// </summary>
public sealed record OrganizeOptions
{
    public CarCodeOptions Codes { get; init; } = CarCodeOptions.Default(new HashSet<string>(StringComparer.OrdinalIgnoreCase));

    public bool CollectNfo { get; init; } = true;
    public bool SkipFoldersWithNfo { get; init; }
    public string NfoTitleTemplate { get; init; } = "车牌+空格+标题";
    public bool RemoveTrailingActorNames { get; init; }
    public bool SaveFeaturesToGenre { get; init; } = true;
    public bool SaveFeaturesToTag { get; init; } = true;
    public IReadOnlyList<ExtraFeatureElement> ExtraFeatureElements { get; init; } =
        [ExtraFeatureElement.Series, ExtraFeatureElement.Studio];
    public ScriptMode Script { get; init; } = ScriptMode.Simplified;
    public FeatureDictionary? Dictionary { get; init; }

    public bool RenameEnabled { get; init; } = true;
    public string RenameTemplate { get; init; } = "车牌+空格+标题";

    public bool FolderEnabled { get; init; } = true;
    public string FolderTemplate { get; init; } = "车牌+【+全部演员+】";

    public bool ClassifyEnabled { get; init; }
    public ClassifyTarget ClassifyTarget { get; init; } = ClassifyTarget.Folders;
    public string ClassifyRoot { get; init; } = "所选文件夹";
    public string ClassifyStandard { get; init; } = "影片类型\\全部演员";

    public bool CoverEnabled { get; init; } = true;
    public string PosterTemplate { get; init; } = "视频+-poster.jpg";
    public string FanartTemplate { get; init; } = "视频+-fanart.jpg";
    public bool SubtitleBanner { get; init; }
    public bool LeakBanner { get; init; }

    public bool SubtitleRenameEnabled { get; init; } = true;
    public bool SkipMoviesWithSubtitles { get; init; } = true;

    public bool MultiCdSingleNfo { get; init; }

    public string SubtitleBadge { get; init; } = "㊥";
    public string LeakBadge { get; init; } = "无码流出";
    public int? MaxTitleLength { get; init; } = 50;
    public CategoryLabels Labels { get; init; } = new();

    /// <summary>刮削缓存使用等级（完全 / 只正缓存 / 不使用）。</summary>
    public ScrapeCacheLevel CacheLevel { get; init; } = ScrapeCacheLevel.Full;

    /// <summary>强制使用的刮削源；null = 按类型自动（auto，网络失败/未收录时回退）。</summary>
    public ScrapeSource? ForcedSource { get; init; }

    public IReadOnlyList<string> VideoExtensions { get; init; } = ["mp4", "mkv", "avi", "wmv", "iso", "rmvb", "flv", "ts"];

    /// <summary>从 AppConfig 组装（需额外的素人前缀表 / 特征字典）。</summary>
    public static OrganizeOptions From(AppConfig config, SurenPrefixTable surenPrefixes, FeatureDictionary? dictionary = null)
    {
        var options = new OrganizeOptions
        {
            Codes = CarCodeOptions.FromConfig(config, surenPrefixes.Prefixes),
            CollectNfo = config.Nfo.CollectNfo,
            SkipFoldersWithNfo = config.Nfo.SkipFoldersWithNfo,
            NfoTitleTemplate = config.Nfo.TitleTemplate,
            RemoveTrailingActorNames = config.Nfo.RemoveTrailingActorNames,
            SaveFeaturesToGenre = config.Nfo.SaveFeaturesToGenre,
            SaveFeaturesToTag = config.Nfo.SaveFeaturesToTag,
            ExtraFeatureElements = config.Nfo.ExtraFeatureElements,
            Script = config.Misc.Script,
            Dictionary = dictionary,
            RenameEnabled = config.Rename.Enabled,
            RenameTemplate = config.Rename.Template,
            FolderEnabled = config.Folder.Enabled,
            FolderTemplate = config.Folder.NewFolderTemplate,
            ClassifyEnabled = config.Classify.Enabled,
            ClassifyTarget = config.Classify.Target,
            ClassifyRoot = config.Classify.RootDirectory,
            ClassifyStandard = config.Classify.Standard,
            CoverEnabled = config.Cover.Enabled,
            PosterTemplate = config.Cover.PosterTemplate,
            FanartTemplate = config.Cover.FanartTemplate,
            SubtitleBanner = config.Cover.SubtitleBanner,
            LeakBanner = config.Cover.LeakBanner,
            SubtitleRenameEnabled = config.Subtitle.Enabled,
            SkipMoviesWithSubtitles = config.Subtitle.SkipMoviesWithSubtitles,
            MultiCdSingleNfo = config.Kodi.MultiCdSingleNfo,
            SubtitleBadge = config.MovieNature.SubtitleBadge,
            LeakBadge = config.MovieNature.LeakBadge,
            MaxTitleLength = config.Misc.TitleTruncateLength,
            Labels = config.MovieNature.Labels,
            VideoExtensions = config.Misc.ScanExtensions,
            ForcedSource = SourceChoice.Parse(config.InfoSource.ScrapeSource),
            CacheLevel = config.Misc.CacheLevel,
        };
        return options;
    }
}

/// <summary>
/// 整理环境：选项 + 卡车刮削器供给 + 封面下载器 + 条幅渲染器（全部可注入；测试用假实现）。
/// </summary>
public sealed record OrganizeEnvironment(
    OrganizeOptions Options,
    Func<ScrapeSource, IScraper> Scrapers,
    ICoverDownloader CoverDownloader,
    IPosterBannerRenderer BannerRenderer,
    ScrapeCache? ScrapeCache = null,
    ScrapeCacheLog? CacheLog = null,
    string? ChecklistDirectory = null) : IDisposable
{
    public void Dispose()
    {
        CacheLog?.Dispose();
        ScrapeCache?.Dispose(); // 仅缓存连接/日志句柄需要释放。
    }
}
