using System.Text.Json;
using HyperJav.Core.Configuration;
using HyperJav.Core.Models;
using HyperJav.Core.Normalization;
using HyperJav.Core.Parsing;
using HyperJav.Core.Scraping;

namespace HyperJav.Core.Organizing;

/// <summary>
/// 真实运行时的默认组装：ScraperFactory（5 站点）+ HttpCoverDownloader（图床 host 路由代理）+
/// Skia 条幅渲染（任一条幅开关打开时）；数据资产（特征字典/素人前缀表）从 data 目录按候选路径加载，缺失则降级。
/// </summary>
public static class OrganizeDefaults
{
    public static OrganizeEnvironment Create(AppConfig config, OrganizeOptions? options = null, string? dataDirectory = null)
    {
        options ??= CreateOptions(config, dataDirectory);
        var bannerEnabled = options.SubtitleBanner || options.LeakBanner;
        var directory = ResolveDataDirectory(dataDirectory);
        // 缓存库在数据目录（GUI 传 data；默认跟随 exe）。等级=完全不使用 → 不构造 = 不读不写。
        ScrapeCache? cache = null;
        ScrapeCacheLog? cacheLog = null;
        if (options.CacheLevel != ScrapeCacheLevel.Disabled)
        {
            cache = TryOpenCache(directory);
            if (cache is not null)
            {
                cacheLog = new ScrapeCacheLog(Path.Combine(directory, "logs"));
            }
        }
        return new OrganizeEnvironment(
            options,
            source => ScraperFactory.Create(source, config),
            new HttpCoverDownloader(config),
            bannerEnabled ? new SkiaPosterBannerRenderer() : NullPosterBannerRenderer.Instance,
            cache,
            cacheLog,
            Path.Combine(directory, TextOutputs.ChecklistDirectoryName));
    }

    /// <summary>数据目录：显式传入优先；否则跟 exe 同级的 data（GUI 与 CLI 共用同一口径）。</summary>
    public static string ResolveDataDirectory(string? dataDirectory) =>
        string.IsNullOrWhiteSpace(dataDirectory)
            ? Path.Combine(AppContext.BaseDirectory, "data")
            : dataDirectory;

    /// <summary>缓存库打不开（锁/权限/损坏）时降级为无缓存运行，不中断整理。</summary>
    private static ScrapeCache? TryOpenCache(string directory)
    {
        try
        {
            return new ScrapeCache(Path.Combine(directory, "scrape-cache.db"));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException
            or Microsoft.Data.Sqlite.SqliteException)
        {
            // 库打不开（锁/权限/损坏）→ 降级为无缓存运行，不中断整理。
            return null;
        }
    }

    /// <summary>从 AppConfig + data 目录组装选项（字典/素人前缀表缺失时为 null / 空表）。</summary>
    public static OrganizeOptions CreateOptions(AppConfig config, string? dataDirectory = null) =>
        OrganizeOptions.From(config, TryLoadSurenPrefixes(dataDirectory), TryLoadFeatureDictionary(dataDirectory));

    /// <summary>加载特征字典（入库报表等外部工具用；缺失/损坏 → null 降级为原始标签）。</summary>
    public static FeatureDictionary? LoadFeatureDictionary(string? dataDirectory = null) =>
        TryLoadFeatureDictionary(dataDirectory);

    private static FeatureDictionary? TryLoadFeatureDictionary(string? dataDirectory)
    {
        foreach (var path in DataCandidates(dataDirectory, "feature-dictionary.json"))
        {
            try
            {
                if (File.Exists(path))
                {
                    return FeatureDictionary.Load(path);
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // 字典读不到 → 降级为不规范化（警告会在 NFO 阶段出现）。
            }
        }
        return null;
    }

    private static SurenPrefixTable TryLoadSurenPrefixes(string? dataDirectory)
    {
        foreach (var path in DataCandidates(dataDirectory, "suren-prefixes.json"))
        {
            try
            {
                if (File.Exists(path))
                {
                    return SurenPrefixTable.Load(path);
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
            {
                // 同上：降级空表。
            }
        }
        return SurenPrefixTable.Empty;
    }

    private static IEnumerable<string> DataCandidates(string? dataDirectory, string fileName)
    {
        if (!string.IsNullOrWhiteSpace(dataDirectory))
        {
            yield return Path.Combine(dataDirectory, fileName);
        }
        yield return Path.Combine(AppContext.BaseDirectory, "data", fileName);
        yield return Path.Combine(Directory.GetCurrentDirectory(), "data", fileName);
        yield return Path.Combine(Directory.GetCurrentDirectory(), fileName);
    }
}
