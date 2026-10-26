using HyperJav.Core.Configuration;
using HyperJav.Core.Models;

namespace HyperJav.Core.Scraping;

/// <summary>单站连通性探测结果（掩码口径：布尔 + 一行结构化错误，不落页面文本）。</summary>
public sealed record ScrapeProbeResult(ScrapeSource Source, bool Ok, string? Error)
{
    public static string LabelOf(ScrapeSource source) => source switch
    {
        ScrapeSource.Javlibrary => "Javlibrary",
        ScrapeSource.JavbusYouma => "Javbus有码",
        ScrapeSource.JavbusWuma => "Javbus无码",
        ScrapeSource.Jav321 => "Jav321",
        ScrapeSource.JavDbFc2 => "JavDb FC2",
        _ => source.ToString(),
    };
}

/// <summary>
/// 站点可连接性验证（自动模式整理前可先预估）：
/// 对每个源的入口页发一次 GET；HTTP 404/410 也算"可连"（站点活着，只是路径变了）。
/// 探测不重试、10 秒超时，失败给一行可处置的错误（ScrapeFriendly）。
/// </summary>
public static class ScrapeProber
{
    public static readonly ScrapeSource[] Sources =
    [
        ScrapeSource.Javlibrary,
        ScrapeSource.JavbusYouma,
        ScrapeSource.JavbusWuma,
        ScrapeSource.Jav321,
        ScrapeSource.JavDbFc2,
    ];

    /// <summary>各站入口（baseRoot 与对应 Scraper 同源：config URL / 固定 base）。</summary>
    public static string EntranceUrl(AppConfig config, ScrapeSource source) => source switch
    {
        ScrapeSource.Javlibrary => Root(config.Misc.JavlibraryUrl),
        ScrapeSource.JavbusYouma or ScrapeSource.JavbusWuma => Root(config.Misc.JavbusUrl),
        ScrapeSource.Jav321 => Root(config.Misc.Jav321Url),
        ScrapeSource.JavDbFc2 => Root(config.Misc.JavDbUrl),
        _ => throw new NotSupportedException($"刮削源 {source} 没有入口 URL。"),
    };

    /// <summary>该站是否走代理（与 ScraperFactory 的 per-site 开关同一套判定）。</summary>
    public static bool UsesProxy(AppConfig config, ScrapeSource source) =>
        ScrapeProxySettings.FromAppConfig(config, ProxySwitchFor(config, source)) is not null;

    private static bool ProxySwitchFor(AppConfig config, ScrapeSource source) => source switch
    {
        ScrapeSource.Javlibrary => config.Proxy.ProxyJavlibrary,
        ScrapeSource.JavbusYouma or ScrapeSource.JavbusWuma => config.Proxy.ProxyJavbus,
        ScrapeSource.Jav321 => config.Proxy.ProxyJav321,
        ScrapeSource.JavDbFc2 => config.Proxy.ProxyJavDb,
        _ => false,
    };

    private static string Root(string url) => url.Trim().TrimEnd('/') + "/";

    /// <summary>探测专用客户端参数：快速失败（10s、单次尝试）。</summary>
    public static ScrapeClientOptions ClientOptions(AppConfig config, ScrapeSource source) => new()
    {
        Timeout = TimeSpan.FromSeconds(10),
        MaxAttempts = 1,
        Proxy = ScrapeProxySettings.FromAppConfig(config, ProxySwitchFor(config, source)),
        AllowBadSsl = config.Misc.IgnoreSsl,
    };

    public static async Task<ScrapeProbeResult> ProbeAsync(AppConfig config, ScrapeSource source, CancellationToken cancellationToken = default)
    {
        var url = EntranceUrl(config, source);
        using var fetcher = new HttpPageFetcher(ClientOptions(config, source));
        return await ProbeCoreAsync(source, url, fetcher, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>核心探测（测试 / Check 注入假 fetcher 用）。</summary>
    public static async Task<ScrapeProbeResult> ProbeCoreAsync(
        ScrapeSource source, string url, IPageFetcher fetcher, CancellationToken cancellationToken = default)
    {
        try
        {
            await fetcher.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
            return new ScrapeProbeResult(source, true, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            // 404/410 = 站点在线、路径或权限变了，连通性本身成立。
            if (error is ScrapeException { IsNotFound: true })
            {
                return new ScrapeProbeResult(source, true, null);
            }
            // ScrapeException 的 Message 已是可读成品文案（RetryAsync/Socks5Tunnel 出品）；裸异常走翻译。
            var message = error is ScrapeException ? error.Message : ScrapeFriendly.Message(error);
            return new ScrapeProbeResult(source, false, OneLine(message));
        }
    }

    private static string OneLine(string message)
    {
        var text = message.Replace("\r", " ").Replace("\n", " ");
        return text.Length > 160 ? text[..160] + "…" : text;
    }
}
