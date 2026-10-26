namespace HyperJav.Core.Scraping;

/// <summary>抓取失败（网络 / HTTP 状态）。刮削器捕获 <see cref="IsNotFound"/> 的实例决定"未找到"。</summary>
public sealed class ScrapeException : Exception
{
    public ScrapeException(string message, bool notFound = false, Exception? inner = null)
        : base(message, inner)
    {
        IsNotFound = notFound;
    }

    /// <summary>true = 站点明确表示资源不存在（404 等），重试无意义。</summary>
    public bool IsNotFound { get; }
}
