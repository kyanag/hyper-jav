using HyperJav.Core.Models;

namespace HyperJav.Core.Configuration;

/// <summary>影片类型显示标签（用于命名模板"影片类型"元素与归类目录，来自 [原影片文件的性质]）。</summary>
public sealed class CategoryLabels
{
    public string Youma { get; set; } = "有码";
    public string Wuma { get; set; } = "无码";
    public string Suren { get; set; } = "素人";
    public string Fc2 { get; set; } = "FC2";

    public string Get(CarCategory category) => category switch
    {
        CarCategory.Youma => Youma,
        CarCategory.Wuma => Wuma,
        CarCategory.Suren => Suren,
        CarCategory.Fc2 => Fc2,
        _ => "",
    };
}

/// <summary>代理协议（由 [局部代理]"代理服务器" 的前缀解析；无前缀默认 Socks5）。</summary>
public enum ProxyKind
{
    Http = 0,
    Socks5 = 1,
}

/// <summary>简繁（[其他设置]"简繁中文？"，决定特征字典取 simplified 还是 traditional）。</summary>
public enum ScriptMode
{
    Simplified = 0,
    Traditional = 1,
}

/// <summary>归类目标（[归类影片]"针对文件还是文件夹？"）。</summary>
public enum ClassifyTarget
{
    Files = 0,
    Folders = 1,
}

/// <summary>
/// 强类型配置（一一对应原版 【点我设置整理规则】.ini 的全部节与键）。
/// 默认值与原版 defaults 对齐；页面/键值映射见 <see cref="AppConfigMapper"/>。
/// </summary>
public sealed class AppConfig
{
    /// <summary>[收集nfo]</summary>
    public NfoCollectOptions Nfo { get; set; } = new();

    /// <summary>[重命名影片]</summary>
    public RenameMovieOptions Rename { get; set; } = new();

    /// <summary>[修改文件夹]</summary>
    public FolderOptions Folder { get; set; } = new();

    /// <summary>[归类影片]</summary>
    public ClassifyOptions Classify { get; set; } = new();

    /// <summary>[下载封面]</summary>
    public CoverOptions Cover { get; set; } = new();

    /// <summary>[字幕文件]</summary>
    public SubtitleFileOptions Subtitle { get; set; } = new();

    /// <summary>[kodi专用]</summary>
    public KodiOptions Kodi { get; set; } = new();

    /// <summary>[emby/jellyfin]</summary>
    public MediaServerOptions MediaServer { get; set; } = new();

    /// <summary>[局部代理]</summary>
    public ProxyOptions Proxy { get; set; } = new();

    /// <summary>[原影片文件的性质]</summary>
    public MovieFileNatureOptions MovieNature { get; set; } = new();

    /// <summary>[信息来源]</summary>
    public InfoSourceOptions InfoSource { get; set; } = new();

    /// <summary>[其他设置]</summary>
    public MiscOptions Misc { get; set; } = new();

    /// <summary>[百度翻译API]</summary>
    public TranslateApiOptions Translate { get; set; } = new();

    /// <summary>[百度人体分析]</summary>
    public BodyAnalysisOptions BodyAnalysis { get; set; } = new();
}

public sealed class NfoCollectOptions
{
    /// <summary>是否跳过已存在nfo的文件夹？</summary>
    public bool SkipFoldersWithNfo { get; set; }

    /// <summary>是否收集nfo？</summary>
    public bool CollectNfo { get; set; } = true;

    /// <summary>nfo中title的格式。</summary>
    public string TitleTemplate { get; set; } = "车牌+空格+标题";

    /// <summary>是否去除标题末尾的演员姓名？</summary>
    public bool RemoveTrailingActorNames { get; set; }

    /// <summary>额外将以下元素添加到特征中。</summary>
    public IReadOnlyList<ExtraFeatureElement> ExtraFeatureElements { get; set; } =
        [ExtraFeatureElement.Series, ExtraFeatureElement.Studio];

    /// <summary>是否将特征保存到genre？</summary>
    public bool SaveFeaturesToGenre { get; set; } = true;

    /// <summary>是否将特征保存到tag？</summary>
    public bool SaveFeaturesToTag { get; set; } = true;
}

public sealed class RenameMovieOptions
{
    /// <summary>是否重命名影片？</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>重命名影片的格式。</summary>
    public string Template { get; set; } = "车牌+空格+标题";
}

public sealed class FolderOptions
{
    /// <summary>是否重命名或创建独立文件夹？</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>新文件夹的格式。</summary>
    public string NewFolderTemplate { get; set; } = "车牌+【+全部演员+】";
}

public sealed class ClassifyOptions
{
    /// <summary>是否归类影片？</summary>
    public bool Enabled { get; set; }

    /// <summary>针对文件还是文件夹？</summary>
    public ClassifyTarget Target { get; set; } = ClassifyTarget.Folders;

    /// <summary>归类的根目录（"所选文件夹" 为占位字面量）。</summary>
    public string RootDirectory { get; set; } = "所选文件夹";

    /// <summary>归类的标准（"\" 为目录层级分隔）。</summary>
    public string Standard { get; set; } = "影片类型\\全部演员";
}

public sealed class CoverOptions
{
    /// <summary>是否下载封面海报？</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>dvd封面的格式。</summary>
    public string FanartTemplate { get; set; } = "视频+-fanart.jpg";

    /// <summary>海报的格式。</summary>
    public string PosterTemplate { get; set; } = "视频+-poster.jpg";

    /// <summary>是否为海报加上中文字幕条幅？</summary>
    public bool SubtitleBanner { get; set; }

    /// <summary>是否为海报加上无码流出条幅？</summary>
    public bool LeakBanner { get; set; }
}

public sealed class SubtitleFileOptions
{
    /// <summary>是否重命名已有的字幕文件？</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>是否跳过已有字幕的影片？</summary>
    public bool SkipMoviesWithSubtitles { get; set; } = true;
}

public sealed class KodiOptions
{
    /// <summary>是否收集演员头像？</summary>
    public bool CollectActorAvatars { get; set; }

    /// <summary>是否对多cd只收集一份图片和nfo？</summary>
    public bool MultiCdSingleNfo { get; set; }
}

public sealed class MediaServerOptions
{
    /// <summary>网址。</summary>
    public string Url { get; set; } = "http://localhost:8096/";

    /// <summary>api id。</summary>
    public string ApiId { get; set; } = "";

    /// <summary>是否覆盖以前上传的头像？</summary>
    public bool OverwriteExistingAvatars { get; set; }
}

public sealed class ProxyOptions
{
    /// <summary>是否使用局部代理？</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>代理服务器（[局部代理]"代理服务器"：[socks5|http]://host:port；协议可省略，默认 socks5）。</summary>
    public string Server { get; set; } = "socks5://127.0.0.1:10809";

    /// <summary>是否代理javlibrary（有问题）？</summary>
    public bool ProxyJavlibrary { get; set; } = true;

    /// <summary>是否代理javbus？</summary>
    public bool ProxyJavbus { get; set; } = true;

    /// <summary>是否代理jav321？</summary>
    public bool ProxyJav321 { get; set; } = true;

    /// <summary>是否代理javdb？</summary>
    public bool ProxyJavDb { get; set; } = true;

    /// <summary>是否代理arzon？</summary>
    public bool ProxyArzon { get; set; } = true;

    /// <summary>是否代理dmm图片？</summary>
    public bool ProxyDmmImages { get; set; } = true;
}

public sealed class MovieFileNatureOptions
{
    /// <summary>有码素人无视多余的字母数字。</summary>
    public IReadOnlyList<string> YoumaSurenNoiseTokens { get; set; } = ["xhd1080", "mm616", "fhd-1080"];

    /// <summary>无码无视多余的字母数字。</summary>
    public IReadOnlyList<string> WumaNoiseTokens { get; set; } =
    [
        "1080p", "caribbean", "carib", "1pondo", "1pon", "fhd",
        "all", "tokyo-hot", "tokyohot", "3xplanet", "full",
    ];

    /// <summary>是否中字即文件名包含。</summary>
    public IReadOnlyList<string> SubtitleMarkers { get; set; } = ["-C", "_C", "中字", "中文字幕", "字幕"];

    /// <summary>是否中字的表现形式。</summary>
    public string SubtitleBadge { get; set; } = "㊥";

    /// <summary>是否流出即文件名包含。</summary>
    public IReadOnlyList<string> LeakMarkers { get; set; } = ["流出"];

    /// <summary>是否流出的表现形式。</summary>
    public string LeakBadge { get; set; } = "无码流出";

    /// <summary>影片类型显示标签。</summary>
    public CategoryLabels Labels { get; set; } = new();
}

public sealed class InfoSourceOptions
{
    /// <summary>是否用javlibrary整理影片时收集网友的热评？</summary>
    public bool CollectHotComments { get; set; } = true;

    /// <summary>是否用javlibrary整理影片时优先从javbus下载图片？</summary>
    public bool PreferJavbusImages { get; set; }

    /// <summary>刮削源（auto=按类型自动选站+失败回退；具体键=强制单站）。键值定义见 Models/SourceChoice。</summary>
    public string ScrapeSource { get; set; } = "auto";
}

public sealed class MiscOptions
{
    /// <summary>简繁中文？（简/繁）</summary>
    public ScriptMode Script { get; set; } = ScriptMode.Simplified;

    /// <summary>javlibrary网址。</summary>
    public string JavlibraryUrl { get; set; } = "https://www.javlibrary.com/";

    /// <summary>javbus网址。</summary>
    public string JavbusUrl { get; set; } = "https://www.javbus.com";

    /// <summary>javdb网址。</summary>
    public string JavDbUrl { get; set; } = "https://javdb.com/";

    /// <summary>jav321网址（原版 ini 无此键，本实现补齐；默认与原实现常量一致）。</summary>
    public string Jav321Url { get; set; } = "https://www.jav321.com";

    /// <summary>是否忽略ssl证书？（站点证书过期/自签很常见；关闭后严格校验）默认是。</summary>
    public bool IgnoreSsl { get; set; } = true;

    /// <summary>刮削缓存使用等级（完全 / 只正缓存 / 不使用），默认完全。</summary>
    public ScrapeCacheLevel CacheLevel { get; set; } = ScrapeCacheLevel.Full;

    /// <summary>文件清理：列出小于该大小（MB）的文件，默认 100。</summary>
    public int CleanupSizeLimitMb { get; set; } = 100;

    /// <summary>扫描文件类型。</summary>
    public IReadOnlyList<string> ScanExtensions { get; set; } =
        ["mp4", "mkv", "avi", "wmv", "iso", "rmvb", "flv", "ts"];

    /// <summary>重命名中的标题长度（50~150），截断"标题"元素。</summary>
    public int TitleTruncateLength { get; set; } = 50;
}

public sealed class TranslateApiOptions
{
    /// <summary>是否需要日语简介？</summary>
    public bool NeedJapanesePlot { get; set; } = true;

    /// <summary>是否翻译为中文？</summary>
    public bool TranslateToChinese { get; set; }

    /// <summary>app id。</summary>
    public string AppId { get; set; } = "";

    /// <summary>密钥。</summary>
    public string SecretKey { get; set; } = "";
}

public sealed class BodyAnalysisOptions
{
    /// <summary>是否需要准确定位人脸的poster？</summary>
    public bool NeedAccurateFacePoster { get; set; }

    /// <summary>appid。</summary>
    public string AppId { get; set; } = "";

    /// <summary>api key。</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>secret key。</summary>
    public string SecretKey { get; set; } = "";
}
