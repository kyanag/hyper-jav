namespace HyperJav.Core.Configuration;

using HyperJav.Core.Models;

/// <summary>
/// 原版 ini（【点我设置整理规则】.ini，中文键名）与 <see cref="AppConfig"/> 的双向映射。
/// 键名以原版 defaults 为准；未在表中的键在 ini 中保持原样（回写时不丢失）。
/// </summary>
public static class AppConfigMapper
{
    /// <summary>读取：应用 ini 中出现的键到强类型配置（缺省键保留 AppConfig 默认值）。</summary>
    public static AppConfig FromIni(IniFile ini)
    {
        var config = new AppConfig();
        ApplyIni(config, ini);
        return config;
    }

    /// <summary>读取：同 <see cref="FromIni"/>，对既有 config 应用。</summary>
    public static void ApplyIni(AppConfig config, IniFile ini)
    {
        foreach (var binding in IniKeyTable.Create())
        {
            if (ini.TryGet(binding.Section, binding.Key, out var value))
            {
                binding.Apply(config, value);
            }
        }
        LegacyProxy(config, ini);
        LegacyCacheLevel(config, ini);
    }

    /// <summary>旧格式兼容：早期的布尔键"是否启用刮削缓存？"→ 等级（是=完全 / 否=不使用）；新键优先。</summary>
    private static void LegacyCacheLevel(AppConfig config, IniFile ini)
    {
        if (ini.TryGet("其他设置", "缓存使用等级", out _))
        {
            return;
        }
        if (ini.TryGet("其他设置", "是否启用刮削缓存？", out var text))
        {
            config.Misc.CacheLevel = ValueParsers.Bool(text, true)
                ? ScrapeCacheLevel.Full
                : ScrapeCacheLevel.Disabled;
        }
    }

    /// <summary>旧格式兼容：没有"代理服务器"键时，由历史的"协议 + 代理端口"两键合成（P5 键合并前的形态）。</summary>
    private static void LegacyProxy(AppConfig config, IniFile ini)
    {
        if (ini.TryGet("局部代理", "代理服务器", out _))
        {
            return;
        }
        if (!ini.TryGet("局部代理", "代理端口", out var address) || address.Trim().Length == 0)
        {
            return;
        }
        var kind = ini.TryGet("局部代理", "http还是socks5？", out var text)
            ? ValueParsers.ParseProxyKind(text, ProxyKind.Socks5) : ProxyKind.Socks5;
        config.Proxy.Server = $"{ValueParsers.ProxyKindText(kind)}://{address.Trim()}";
    }

    /// <summary>回写：把强类型配置的值写回 ini（键不存在则追加，节序/键序基本保持）。</summary>
    public static void ToIni(AppConfig config, IniFile ini)
    {
        foreach (var binding in IniKeyTable.Create())
        {
            ini.SetValue(binding.Section, binding.Key, binding.Render(config));
        }
        // 历史键清理：协议已并入"代理服务器"值的前缀，旧两键移除避免残留旧值混读。
        ini.Find("局部代理")?.Remove("http还是socks5？");
        ini.Find("局部代理")?.Remove("代理端口");
        // 缓存等级替代早期布尔键。
        ini.Find("其他设置")?.Remove("是否启用刮削缓存？");
    }
}

/// <summary>单项键值绑定。</summary>
public sealed record IniBinding(string Section, string Key, Action<AppConfig, string> Apply, Func<AppConfig, string> Render);

/// <summary>全部键绑定与值解析/序列化辅助。</summary>
internal static class IniKeyTable
{
    public static IEnumerable<IniBinding> Create()
    {
        var bindings = new List<IniBinding>();
        void Add(string section, string key, Action<AppConfig, string> apply, Func<AppConfig, string> render)
            => bindings.Add(new IniBinding(section, key, apply, render));

        // ---- [收集nfo] ----
        string sec = "收集nfo";
        Add(sec, "是否跳过已存在nfo的文件夹？",
            (c, v) => c.Nfo.SkipFoldersWithNfo = ValueParsers.Bool(v, c.Nfo.SkipFoldersWithNfo),
            c => ValueParsers.BoolText(c.Nfo.SkipFoldersWithNfo));
        Add(sec, "是否收集nfo？",
            (c, v) => c.Nfo.CollectNfo = ValueParsers.Bool(v, c.Nfo.CollectNfo),
            c => ValueParsers.BoolText(c.Nfo.CollectNfo));
        Add(sec, "nfo中title的格式",
            (c, v) => c.Nfo.TitleTemplate = v,
            c => c.Nfo.TitleTemplate);
        Add(sec, "是否去除标题末尾的演员姓名？",
            (c, v) => c.Nfo.RemoveTrailingActorNames = ValueParsers.Bool(v, c.Nfo.RemoveTrailingActorNames),
            c => ValueParsers.BoolText(c.Nfo.RemoveTrailingActorNames));
        Add(sec, "额外将以下元素添加到特征中",
            (c, v) => c.Nfo.ExtraFeatureElements = ExtraFeatureElementExtensions.Parse(v),
            c => ExtraFeatureElementExtensions.Format(c.Nfo.ExtraFeatureElements));
        Add(sec, "是否将特征保存到genre？",
            (c, v) => c.Nfo.SaveFeaturesToGenre = ValueParsers.Bool(v, c.Nfo.SaveFeaturesToGenre),
            c => ValueParsers.BoolText(c.Nfo.SaveFeaturesToGenre));
        Add(sec, "是否将特征保存到tag？",
            (c, v) => c.Nfo.SaveFeaturesToTag = ValueParsers.Bool(v, c.Nfo.SaveFeaturesToTag),
            c => ValueParsers.BoolText(c.Nfo.SaveFeaturesToTag));

        // ---- [重命名影片] ----
        sec = "重命名影片";
        Add(sec, "是否重命名影片？",
            (c, v) => c.Rename.Enabled = ValueParsers.Bool(v, c.Rename.Enabled),
            c => ValueParsers.BoolText(c.Rename.Enabled));
        Add(sec, "重命名影片的格式",
            (c, v) => c.Rename.Template = v,
            c => c.Rename.Template);

        // ---- [修改文件夹] ----
        sec = "修改文件夹";
        Add(sec, "是否重命名或创建独立文件夹？",
            (c, v) => c.Folder.Enabled = ValueParsers.Bool(v, c.Folder.Enabled),
            c => ValueParsers.BoolText(c.Folder.Enabled));
        Add(sec, "新文件夹的格式",
            (c, v) => c.Folder.NewFolderTemplate = v,
            c => c.Folder.NewFolderTemplate);

        // ---- [归类影片] ----
        sec = "归类影片";
        Add(sec, "是否归类影片？",
            (c, v) => c.Classify.Enabled = ValueParsers.Bool(v, c.Classify.Enabled),
            c => ValueParsers.BoolText(c.Classify.Enabled));
        Add(sec, "针对文件还是文件夹？",
            (c, v) => c.Classify.Target = ValueParsers.ParseClassifyTarget(v, c.Classify.Target),
            c => ValueParsers.ClassifyTargetText(c.Classify.Target));
        Add(sec, "归类的根目录",
            (c, v) => c.Classify.RootDirectory = v,
            c => c.Classify.RootDirectory);
        Add(sec, "归类的标准",
            (c, v) => c.Classify.Standard = v,
            c => c.Classify.Standard);

        // ---- [下载封面] ----
        sec = "下载封面";
        Add(sec, "是否下载封面海报？",
            (c, v) => c.Cover.Enabled = ValueParsers.Bool(v, c.Cover.Enabled),
            c => ValueParsers.BoolText(c.Cover.Enabled));
        Add(sec, "dvd封面的格式",
            (c, v) => c.Cover.FanartTemplate = v,
            c => c.Cover.FanartTemplate);
        Add(sec, "海报的格式",
            (c, v) => c.Cover.PosterTemplate = v,
            c => c.Cover.PosterTemplate);
        Add(sec, "是否为海报加上中文字幕条幅？",
            (c, v) => c.Cover.SubtitleBanner = ValueParsers.Bool(v, c.Cover.SubtitleBanner),
            c => ValueParsers.BoolText(c.Cover.SubtitleBanner));
        Add(sec, "是否为海报加上无码流出条幅？",
            (c, v) => c.Cover.LeakBanner = ValueParsers.Bool(v, c.Cover.LeakBanner),
            c => ValueParsers.BoolText(c.Cover.LeakBanner));

        // ---- [字幕文件] ----
        sec = "字幕文件";
        Add(sec, "是否重命名已有的字幕文件？",
            (c, v) => c.Subtitle.Enabled = ValueParsers.Bool(v, c.Subtitle.Enabled),
            c => ValueParsers.BoolText(c.Subtitle.Enabled));
        Add(sec, "是否跳过已有字幕的影片？",
            (c, v) => c.Subtitle.SkipMoviesWithSubtitles = ValueParsers.Bool(v, c.Subtitle.SkipMoviesWithSubtitles),
            c => ValueParsers.BoolText(c.Subtitle.SkipMoviesWithSubtitles));

        // ---- [kodi专用] ----
        sec = "kodi专用";
        Add(sec, "是否收集演员头像？",
            (c, v) => c.Kodi.CollectActorAvatars = ValueParsers.Bool(v, c.Kodi.CollectActorAvatars),
            c => ValueParsers.BoolText(c.Kodi.CollectActorAvatars));
        Add(sec, "是否对多cd只收集一份图片和nfo？",
            (c, v) => c.Kodi.MultiCdSingleNfo = ValueParsers.Bool(v, c.Kodi.MultiCdSingleNfo),
            c => ValueParsers.BoolText(c.Kodi.MultiCdSingleNfo));

        // ---- [emby/jellyfin] ----
        sec = "emby/jellyfin";
        Add(sec, "网址",
            (c, v) => c.MediaServer.Url = v,
            c => c.MediaServer.Url);
        Add(sec, "api id",
            (c, v) => c.MediaServer.ApiId = v,
            c => c.MediaServer.ApiId);
        Add(sec, "是否覆盖以前上传的头像？",
            (c, v) => c.MediaServer.OverwriteExistingAvatars = ValueParsers.Bool(v, c.MediaServer.OverwriteExistingAvatars),
            c => ValueParsers.BoolText(c.MediaServer.OverwriteExistingAvatars));

        // ---- [局部代理] ----
        sec = "局部代理";
        Add(sec, "是否使用局部代理？",
            (c, v) => c.Proxy.Enabled = ValueParsers.Bool(v, c.Proxy.Enabled),
            c => ValueParsers.BoolText(c.Proxy.Enabled));
        Add(sec, "代理服务器",
            (c, v) => c.Proxy.Server = v,
            c => c.Proxy.Server);
        Add(sec, "是否代理javlibrary（有问题）？",
            (c, v) => c.Proxy.ProxyJavlibrary = ValueParsers.Bool(v, c.Proxy.ProxyJavlibrary),
            c => ValueParsers.BoolText(c.Proxy.ProxyJavlibrary));
        Add(sec, "是否代理javbus？",
            (c, v) => c.Proxy.ProxyJavbus = ValueParsers.Bool(v, c.Proxy.ProxyJavbus),
            c => ValueParsers.BoolText(c.Proxy.ProxyJavbus));
        Add(sec, "是否代理jav321？",
            (c, v) => c.Proxy.ProxyJav321 = ValueParsers.Bool(v, c.Proxy.ProxyJav321),
            c => ValueParsers.BoolText(c.Proxy.ProxyJav321));
        Add(sec, "是否代理javdb？",
            (c, v) => c.Proxy.ProxyJavDb = ValueParsers.Bool(v, c.Proxy.ProxyJavDb),
            c => ValueParsers.BoolText(c.Proxy.ProxyJavDb));
        Add(sec, "是否代理arzon？",
            (c, v) => c.Proxy.ProxyArzon = ValueParsers.Bool(v, c.Proxy.ProxyArzon),
            c => ValueParsers.BoolText(c.Proxy.ProxyArzon));
        Add(sec, "是否代理dmm图片？",
            (c, v) => c.Proxy.ProxyDmmImages = ValueParsers.Bool(v, c.Proxy.ProxyDmmImages),
            c => ValueParsers.BoolText(c.Proxy.ProxyDmmImages));

        // ---- [原影片文件的性质] ----
        sec = "原影片文件的性质";
        Add(sec, "有码素人无视多余的字母数字",
            (c, v) => c.MovieNature.YoumaSurenNoiseTokens = ValueParsers.List(v),
            c => ValueParsers.ListText(c.MovieNature.YoumaSurenNoiseTokens));
        Add(sec, "无码无视多余的字母数字",
            (c, v) => c.MovieNature.WumaNoiseTokens = ValueParsers.List(v),
            c => ValueParsers.ListText(c.MovieNature.WumaNoiseTokens));
        Add(sec, "是否中字即文件名包含",
            (c, v) => c.MovieNature.SubtitleMarkers = ValueParsers.List(v),
            c => ValueParsers.ListText(c.MovieNature.SubtitleMarkers));
        Add(sec, "是否中字的表现形式",
            (c, v) => c.MovieNature.SubtitleBadge = v,
            c => c.MovieNature.SubtitleBadge);
        Add(sec, "是否流出即文件名包含",
            (c, v) => c.MovieNature.LeakMarkers = ValueParsers.List(v),
            c => ValueParsers.ListText(c.MovieNature.LeakMarkers));
        Add(sec, "是否流出的表现形式",
            (c, v) => c.MovieNature.LeakBadge = v,
            c => c.MovieNature.LeakBadge);
        Add(sec, "有码",
            (c, v) => c.MovieNature.Labels.Youma = v,
            c => c.MovieNature.Labels.Youma);
        Add(sec, "无码",
            (c, v) => c.MovieNature.Labels.Wuma = v,
            c => c.MovieNature.Labels.Wuma);
        Add(sec, "素人",
            (c, v) => c.MovieNature.Labels.Suren = v,
            c => c.MovieNature.Labels.Suren);
        Add(sec, "fc2",
            (c, v) => c.MovieNature.Labels.Fc2 = v,
            c => c.MovieNature.Labels.Fc2);

        // ---- [信息来源] ----
        sec = "信息来源";
        Add(sec, "是否用javlibrary整理影片时收集网友的热评？",
            (c, v) => c.InfoSource.CollectHotComments = ValueParsers.Bool(v, c.InfoSource.CollectHotComments),
            c => ValueParsers.BoolText(c.InfoSource.CollectHotComments));
        Add(sec, "是否用javlibrary整理影片时优先从javbus下载图片？",
            (c, v) => c.InfoSource.PreferJavbusImages = ValueParsers.Bool(v, c.InfoSource.PreferJavbusImages),
            c => ValueParsers.BoolText(c.InfoSource.PreferJavbusImages));
        Add(sec, "刮削源",
            (c, v) => c.InfoSource.ScrapeSource = SourceChoice.NormalizeKey(v),
            c => c.InfoSource.ScrapeSource);

        // ---- [其他设置] ----
        sec = "其他设置";
        Add(sec, "简繁中文？",
            (c, v) => c.Misc.Script = ValueParsers.ParseScriptMode(v, c.Misc.Script),
            c => ValueParsers.ScriptModeText(c.Misc.Script));
        Add(sec, "javlibrary网址",
            (c, v) => c.Misc.JavlibraryUrl = v,
            c => c.Misc.JavlibraryUrl);
        Add(sec, "javbus网址",
            (c, v) => c.Misc.JavbusUrl = v,
            c => c.Misc.JavbusUrl);
        Add(sec, "javdb网址",
            (c, v) => c.Misc.JavDbUrl = v,
            c => c.Misc.JavDbUrl);
        Add(sec, "jav321网址",
            (c, v) => c.Misc.Jav321Url = v,
            c => c.Misc.Jav321Url);
        Add(sec, "是否忽略ssl证书？",
            (c, v) => c.Misc.IgnoreSsl = ValueParsers.Bool(v, c.Misc.IgnoreSsl),
            c => ValueParsers.BoolText(c.Misc.IgnoreSsl));
        Add(sec, "缓存使用等级",
            (c, v) => c.Misc.CacheLevel = ScrapeCacheLevels.Parse(v),
            c => ScrapeCacheLevels.TextOf(c.Misc.CacheLevel));
        Add(sec, "清理文件大小上限（MB）",
            (c, v) => c.Misc.CleanupSizeLimitMb = ValueParsers.Int(v, c.Misc.CleanupSizeLimitMb, 1, 102400),
            c => c.Misc.CleanupSizeLimitMb.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Add(sec, "扫描文件类型",
            (c, v) => c.Misc.ScanExtensions = ValueParsers.List(v),
            c => ValueParsers.ListText(c.Misc.ScanExtensions));
        Add(sec, "重命名中的标题长度（50~150）",
            (c, v) => c.Misc.TitleTruncateLength = ValueParsers.Int(v, c.Misc.TitleTruncateLength, 50, 150),
            c => c.Misc.TitleTruncateLength.ToString(System.Globalization.CultureInfo.InvariantCulture));

        // ---- [百度翻译API] ----
        sec = "百度翻译API";
        Add(sec, "是否需要日语简介？",
            (c, v) => c.Translate.NeedJapanesePlot = ValueParsers.Bool(v, c.Translate.NeedJapanesePlot),
            c => ValueParsers.BoolText(c.Translate.NeedJapanesePlot));
        Add(sec, "是否翻译为中文？",
            (c, v) => c.Translate.TranslateToChinese = ValueParsers.Bool(v, c.Translate.TranslateToChinese),
            c => ValueParsers.BoolText(c.Translate.TranslateToChinese));
        Add(sec, "app id",
            (c, v) => c.Translate.AppId = v,
            c => c.Translate.AppId);
        Add(sec, "密钥",
            (c, v) => c.Translate.SecretKey = v,
            c => c.Translate.SecretKey);

        // ---- [百度人体分析] ----
        sec = "百度人体分析";
        Add(sec, "是否需要准确定位人脸的poster？",
            (c, v) => c.BodyAnalysis.NeedAccurateFacePoster = ValueParsers.Bool(v, c.BodyAnalysis.NeedAccurateFacePoster),
            c => ValueParsers.BoolText(c.BodyAnalysis.NeedAccurateFacePoster));
        Add(sec, "appid",
            (c, v) => c.BodyAnalysis.AppId = v,
            c => c.BodyAnalysis.AppId);
        Add(sec, "api key",
            (c, v) => c.BodyAnalysis.ApiKey = v,
            c => c.BodyAnalysis.ApiKey);
        Add(sec, "secret key",
            (c, v) => c.BodyAnalysis.SecretKey = v,
            c => c.BodyAnalysis.SecretKey);

        return bindings;
    }
}

/// <summary>ini 值解析/序列化（是=否 / 、分隔列表 / 枚举名）。</summary>
internal static class ValueParsers
{
    public static bool Bool(string value, bool fallback) => value.Trim() switch
    {
        "是" or "true" or "1" or "yes" or "on" or "开" => true,
        "否" or "false" or "0" or "no" or "off" or "关" => false,
        _ => fallback,
    };

    public static string BoolText(bool value) => value ? "是" : "否";

    public static IReadOnlyList<string> List(string value) => value
        .Split('、')
        .Select(part => part.Trim())
        .Where(part => part.Length > 0)
        .ToArray();

    public static string ListText(IReadOnlyList<string> values) => string.Join("、", values);

    public static ProxyKind ParseProxyKind(string value, ProxyKind fallback)
        => value.Contains("socks5", StringComparison.OrdinalIgnoreCase) ? ProxyKind.Socks5
            : value.Contains("http", StringComparison.OrdinalIgnoreCase) ? ProxyKind.Http
            : fallback;

    public static string ProxyKindText(ProxyKind kind) => kind switch
    {
        ProxyKind.Socks5 => "socks5",
        _ => "http",
    };

    public static ScriptMode ParseScriptMode(string value, ScriptMode fallback)
        => value.Contains('繁') ? ScriptMode.Traditional
            : value.Contains('简') ? ScriptMode.Simplified
            : fallback;

    public static string ScriptModeText(ScriptMode mode) => mode switch
    {
        ScriptMode.Traditional => "繁",
        _ => "简",
    };

    public static ClassifyTarget ParseClassifyTarget(string value, ClassifyTarget fallback)
        => value.Contains("文件", StringComparison.Ordinal) && !value.Contains("文件夹", StringComparison.Ordinal)
            ? ClassifyTarget.Files
            : value.Contains("文件夹", StringComparison.Ordinal) ? ClassifyTarget.Folders
            : fallback;

    public static string ClassifyTargetText(ClassifyTarget target) => target switch
    {
        ClassifyTarget.Files => "文件",
        _ => "文件夹",
    };

    public static int Int(string value, int fallback, int min, int max)
    {
        if (!int.TryParse(value.Trim(), out var parsed))
        {
            return fallback;
        }
        return Math.Clamp(parsed, min, max);
    }
}
