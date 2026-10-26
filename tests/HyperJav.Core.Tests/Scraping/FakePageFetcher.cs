using HyperJav.Core.Scraping;

namespace HyperJav.Core.Tests.Scraping;

/// <summary>
/// 假抓取器：URL(区分 GET ) → 预置响应（中性占位 HTML，不含真实站点内容）。
/// 未注册的 URL 抛 notFound 异常，模拟 404。
/// </summary>
public sealed class FakePageFetcher : IPageFetcher
{
    private readonly Dictionary<string, string> _pages;
    private readonly string? _postResponse;

    public FakePageFetcher(IReadOnlyDictionary<string, string> pages, string? postResponse = null)
    {
        _pages = new Dictionary<string, string>(pages, StringComparer.Ordinal);
        _postResponse = postResponse;
    }

    public IReadOnlyList<string> Requests => _requests;

    private readonly List<string> _requests = [];

    public Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default)
    {
        _requests.Add(url);
        return _pages.TryGetValue(url, out var content)
            ? Task.FromResult(content)
            : throw new Core.Scraping.ScrapeException($"未模拟的页面：{url}", notFound: true);
    }

    public Task<string> PostFormAsync(string url, IReadOnlyDictionary<string, string> form, CancellationToken cancellationToken = default)
    {
        _requests.Add($"POST {url} isbn={form.GetValueOrDefault("isbn")}");
        return _postResponse is not null
            ? Task.FromResult(_postResponse)
            : throw new Core.Scraping.ScrapeException($"未模拟的 POST：{url}", notFound: true);
    }
}
