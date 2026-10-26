namespace HyperJav.Core.Scraping;

/// <summary>页面抓取客户端选项（UA、超时、重试、限速、代理）。</summary>
public sealed record ScrapeClientOptions
{
    public const string DefaultUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0 Safari/537.36";

    /// <summary>浏览器 UA（原版行为：伪装浏览器请求）。</summary>
    public string UserAgent { get; init; } = DefaultUserAgent;

    /// <summary>单请求超时。</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>失败重试次数（含首次；400/404 不重试，429/5xx/网络错误重试）。</summary>
    public int MaxAttempts { get; init; } = 3;

    /// <summary>是否忽略 ssl 证书问题（站点证书过期/自签、代理节点替换；默认忽略）。</summary>
    public bool AllowBadSsl { get; init; } = true;

    /// <summary>重试基础退避毫秒（第 n 次重试前延迟 base × n）。</summary>
    public int RetryDelayMs { get; init; } = 1000;

    /// <summary>同站两请求的最小间隔毫秒（限速，礼貌抓取）。</summary>
    public int MinRequestIntervalMs { get; init; } = 1200;

    /// <summary>代理设置；null 直连。</summary>
    public ScrapeProxySettings? Proxy { get; init; }
}
