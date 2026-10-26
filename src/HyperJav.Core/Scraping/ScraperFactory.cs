using HyperJav.Core.Configuration;
using HyperJav.Core.Models;
using HyperJav.Core.Scraping.Scrapers;

namespace HyperJav.Core.Scraping;

/// <summary>按刮削源组装站点实现（局部代理开关联动 [局部代理]"是否代理某站？"）。</summary>
public static class ScraperFactory
{
    public static IScraper Create(ScrapeSource source, AppConfig config) => source switch
    {
        ScrapeSource.Javlibrary => CreateJavlibrary(config),
        ScrapeSource.JavbusYouma => CreateJavbus(config, uncensored: false),
        ScrapeSource.JavbusWuma => CreateJavbus(config, uncensored: true),
        ScrapeSource.Jav321 => CreateJav321(config),
        ScrapeSource.JavDbFc2 => CreateJavDbFc2(config),
        _ => throw new NotSupportedException($"刮削源 {source} 未实现。"),
    };

    public static IScraper CreateJavlibrary(AppConfig config) => new JavlibraryScraper(
        new HttpPageFetcher(Options(config, config.Proxy.ProxyJavlibrary)),
        config.Misc.JavlibraryUrl,
        config.InfoSource.CollectHotComments);

    public static IScraper CreateJavbus(AppConfig config, bool uncensored) => new JavbusScraper(
        new HttpPageFetcher(Options(config, config.Proxy.ProxyJavbus)),
        config.Misc.JavbusUrl,
        uncensored);

    public static IScraper CreateJav321(AppConfig config) => new Jav321Scraper(
        new HttpPageFetcher(Options(config, config.Proxy.ProxyJav321)),
        config.Misc.Jav321Url);

    public static IScraper CreateJavDbFc2(AppConfig config) => new JavDbFc2Scraper(
        new HttpPageFetcher(Options(config, config.Proxy.ProxyJavDb)),
        config.Misc.JavDbUrl);

    private static ScrapeClientOptions Options(AppConfig config, bool proxyForSite) => new()
    {
        Proxy = ScrapeProxySettings.FromAppConfig(config, proxyForSite),
        AllowBadSsl = config.Misc.IgnoreSsl,
    };

    /// <summary>
    /// 按类别装配候选序列（P5 适配器模型）：登记点在 <see cref="Create"/> 的五个 Create* 组合的适配器能力
    /// （<see cref="IScraper.Categories"/>，注册顺序 = 同类别内优先级）。
    /// 新增站点：加一个 Create* + 在对应类别行登记；候选/回退序列自动变化，管线无需改动。
    /// </summary>
    public static IReadOnlyList<ScrapeSource> CapableSources(CarCategory category) => category switch
    {
        // 有码：Javlibrary（热评/图片偏好）优先，可直连的 Javbus 兜底（套 CF 验证的站连不上时自动转）。
        CarCategory.Youma => [ScrapeSource.Javlibrary, ScrapeSource.JavbusYouma],
        CarCategory.Wuma => [ScrapeSource.JavbusWuma],
        CarCategory.Suren => [ScrapeSource.Jav321],
        CarCategory.Fc2 => [ScrapeSource.JavDbFc2],
        _ => [],
    };

    /// <summary>能力一致性自检：工厂实际装配的适配器声明，与上表登记一致（防新增站漏登记）。</summary>
    public static void AssertRegistryConsistent(AppConfig config)
    {
        var adapters = new (IScraper Scraper, ScrapeSource Source)[]
        {
            (CreateJavlibrary(config), ScrapeSource.Javlibrary),
            (CreateJavbus(config, uncensored: false), ScrapeSource.JavbusYouma),
            (CreateJavbus(config, uncensored: true), ScrapeSource.JavbusWuma),
            (CreateJav321(config), ScrapeSource.Jav321),
            (CreateJavDbFc2(config), ScrapeSource.JavDbFc2),
        };
        foreach (var (scraper, _) in adapters)
        foreach (var category in scraper.Categories)
        {
            var table = CapableSources(category);
            var entry = table.Contains(scraper.Source);
            if (!entry)
            {
                throw new InvalidOperationException(
                    $"适配器 {scraper.Source} 声明了能力 {category}，但 CapableSources 未登记。");
            }
        }
    }
}
