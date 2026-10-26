using HyperJav.Core.Configuration;
using HyperJav.Core.Models;
using Xunit;

namespace HyperJav.Core.Tests.Configuration;

/// <summary>AppConfig ←→ ini 双向映射。数据用原版 defaults（data/defaults.ini.utf8.txt）。</summary>
public sealed class AppConfigMapperTests
{
    private static IniFile DefaultIni() => IniFile.Load(Path.Combine(
        AppContext.BaseDirectory, "TestData", "Configuration", "defaults.ini.utf8.txt"));

    [Fact]
    public void Cache_level_reads_from_ini_with_legacy_bool_migration()
    {
        // 新键：三档直读。
        var ini = new IniFile();
        ini.SetValue("其他设置", "缓存使用等级", "只使用正缓存");
        Assert.Equal(ScrapeCacheLevel.PositiveOnly, AppConfigMapper.FromIni(ini).Misc.CacheLevel);

        // 旧布尔键：是 → 完全；否 → 不使用；回写后旧键清理、新键落盘。
        var legacyOn = new IniFile();
        legacyOn.SetValue("其他设置", "是否启用刮削缓存？", "是");
        var configOn = AppConfigMapper.FromIni(legacyOn);
        Assert.Equal(ScrapeCacheLevel.Full, configOn.Misc.CacheLevel);

        var legacyOff = new IniFile();
        legacyOff.SetValue("其他设置", "是否启用刮削缓存？", "否");
        Assert.Equal(ScrapeCacheLevel.Disabled, AppConfigMapper.FromIni(legacyOff).Misc.CacheLevel);

        AppConfigMapper.ToIni(configOn, legacyOn);
        Assert.False(legacyOn.TryGet("其他设置", "是否启用刮削缓存？", out _));
        Assert.True(legacyOn.TryGet("其他设置", "缓存使用等级", out var written));
        Assert.Equal("完全使用缓存", written);
    }

    [Fact]
    public void Maps_all_sections_to_strong_typed_defaults()
    {
        var config = AppConfigMapper.FromIni(DefaultIni());

        // [收集nfo]
        Assert.False(config.Nfo.SkipFoldersWithNfo);
        Assert.True(config.Nfo.CollectNfo);
        Assert.Equal("车牌+空格+标题", config.Nfo.TitleTemplate);
        Assert.False(config.Nfo.RemoveTrailingActorNames);
        Assert.Equal([ExtraFeatureElement.Series, ExtraFeatureElement.Studio], config.Nfo.ExtraFeatureElements);
        Assert.True(config.Nfo.SaveFeaturesToGenre);
        Assert.True(config.Nfo.SaveFeaturesToTag);

        // [重命名影片] / [修改文件夹]
        Assert.True(config.Rename.Enabled);
        Assert.Equal("车牌+空格+标题", config.Rename.Template);
        Assert.True(config.Folder.Enabled);
        Assert.Equal("车牌+【+全部演员+】", config.Folder.NewFolderTemplate);

        // [归类影片]
        Assert.False(config.Classify.Enabled);
        Assert.Equal(ClassifyTarget.Folders, config.Classify.Target);
        Assert.Equal("影片类型\\全部演员", config.Classify.Standard);

        // [下载封面]
        Assert.Equal("视频+-fanart.jpg", config.Cover.FanartTemplate);
        Assert.Equal("视频+-poster.jpg", config.Cover.PosterTemplate);
        Assert.False(config.Cover.SubtitleBanner);
        Assert.False(config.Cover.LeakBanner);

        // [字幕文件] [kodi专用]
        Assert.True(config.Subtitle.Enabled);
        Assert.True(config.Subtitle.SkipMoviesWithSubtitles);
        Assert.False(config.Kodi.CollectActorAvatars);
        Assert.False(config.Kodi.MultiCdSingleNfo);

        // [emby/jellyfin]
        Assert.Equal("http://localhost:8096/", config.MediaServer.Url);
        Assert.Equal("0123456789abcdef0123456789abcdef", config.MediaServer.ApiId);

        // [局部代理]
        Assert.True(config.Proxy.Enabled);
        // 原版默认 ini 仍是旧两键 → 由兼容路径合成。
        Assert.Equal("socks5://127.0.0.1:10808", config.Proxy.Server);
        Assert.True(config.Proxy.ProxyJavDb);

        // [原影片文件的性质]
        Assert.Equal(3, config.MovieNature.YoumaSurenNoiseTokens.Count);
        Assert.Equal(11, config.MovieNature.WumaNoiseTokens.Count);
        Assert.Contains("中文字幕", config.MovieNature.SubtitleMarkers);
        Assert.Equal("㊥", config.MovieNature.SubtitleBadge);
        Assert.Equal("无码流出", config.MovieNature.LeakBadge);
        Assert.Equal("有码", config.MovieNature.Labels.Youma);
        Assert.Equal("FC2", config.MovieNature.Labels.Fc2);

        // [其他设置]
        Assert.True(config.InfoSource.CollectHotComments);
        Assert.Equal(ScriptMode.Simplified, config.Misc.Script);
        Assert.Equal(8, config.Misc.ScanExtensions.Count);
        Assert.Equal(50, config.Misc.TitleTruncateLength);
        Assert.Equal("https://www.jav321.com", config.Misc.Jav321Url); // 原版无此键 → 仅默认值生效
        Assert.True(config.Misc.IgnoreSsl);                            // 是否忽略ssl证书？（原版无此键）
        Assert.Equal(100, config.Misc.CleanupSizeLimitMb);             // 清理阈值默认 100 MB

        // [百度翻译API]
        Assert.True(config.Translate.NeedJapanesePlot);
        Assert.False(config.Translate.TranslateToChinese);
    }

    [Fact]
    public void Round_trips_write_back_without_losing_entries()
    {
        var original = DefaultIni();
        var config = AppConfigMapper.FromIni(original);
        config.Cover.Enabled = false;
        config.Proxy.Server = "http://127.0.0.1:10808";
        config.Misc.TitleTruncateLength = 150;
        config.Misc.CleanupSizeLimitMb = 250;

        AppConfigMapper.ToIni(config, original);

        Assert.Equal("否", original.Find("下载封面")!.Entries[0].Value);
        Assert.Equal("150", original.Find("其他设置")!.Entries[5].Value);

        // 代理已有合一写法；旧两键清理。
        Assert.True(original.TryGet("局部代理", "代理服务器", out var serverKey));
        Assert.Equal("http://127.0.0.1:10808", serverKey);
        Assert.False(original.TryGet("局部代理", "http还是socks5？", out _));
        Assert.False(original.TryGet("局部代理", "代理端口", out _));

        // 未修改键保持原值。
        Assert.True(original.TryGet("下载封面", "是否为海报加上中文字幕条幅？", out var untouched));
        Assert.Equal("否", untouched);

        // 计数；默认 −2（旧两键）+6（代理服务器/刮削源/jav321网址/是否忽略ssl证书/缓存使用等级/清理阈值）= +4。
        Assert.Equal(DefaultIni().Sections.Sum(s => s.Entries.Count) + 4, original.Sections.Sum(s => s.Entries.Count));
        Assert.True(original.TryGet("信息来源", "刮削源", out var sourceKey));
        Assert.Equal("auto", sourceKey);

        // 再读回，强类型值一致。
        var reloaded = AppConfigMapper.FromIni(original);
        Assert.False(reloaded.Cover.Enabled);
        Assert.Equal("http://127.0.0.1:10808", reloaded.Proxy.Server);
        Assert.Equal(150, reloaded.Misc.TitleTruncateLength);
        Assert.Equal(250, reloaded.Misc.CleanupSizeLimitMb);
    }

    [Fact]
    public void Unknown_keys_survive_round_trip()
    {
        var ini = IniFile.Parse("[自定义节]\n自定义键 = 自定义值\n");
        var config = AppConfigMapper.FromIni(ini); // 无已知键 → 保持默认
        AppConfigMapper.ToIni(config, ini);
        Assert.True(ini.TryGet("自定义节", "自定义键", out var value));
        Assert.Equal("自定义值", value);
    }
}
