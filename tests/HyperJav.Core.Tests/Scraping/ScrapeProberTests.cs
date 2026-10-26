using HyperJav.Core.Configuration;
using HyperJav.Core.Models;
using HyperJav.Core.Scraping;
using HyperJav.Core.Scraping.Scrapers;
using HyperJav.Core.Tests.Scraping;

namespace HyperJav.Core.Tests.Scraping;

/// <summary>站点连通性探测（假 fetcher，不联网）；错误信息翻译层的可处置文案。</summary>
public sealed class ScrapeProberTests
{
    [Fact]
    public async Task Reachable_page_counts_as_connected()
    {
        var fetcher = new FakePageFetcher(new Dictionary<string, string> { ["https://www.example-lib.com/"] = "<html></html>" });
        var result = await ScrapeProber.ProbeCoreAsync(ScrapeSource.Javlibrary, "https://www.example-lib.com/", fetcher);
        Assert.True(result.Ok);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task Http_404_still_counts_as_connected()
    {
        // 站点在线但入口路径变了（404）→ 连通性成立。
        var fetcher = new FakePageFetcher(new Dictionary<string, string>());
        var result = await ScrapeProber.ProbeCoreAsync(ScrapeSource.Javlibrary, "https://www.example-lib.com/", fetcher);
        Assert.True(result.Ok);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task Network_error_reports_one_line_reason()
    {
        var result = await ScrapeProber.ProbeCoreAsync(
            ScrapeSource.JavbusYouma, "https://www.example-lib.com/", new ThrowingFetcher("由于目标计算机积极拒绝，无法连接。"));
        Assert.False(result.Ok);
        Assert.NotNull(result.Error);
        Assert.Contains("积极拒绝", result.Error);
    }

    [Fact]
    public void Refused_connection_message_suggests_proxy()
    {
        // 典型：没开代理直连被墙站，Windows 的 Socket 原话。
        var message = ScrapeFriendly.Message(new HttpRequestException(
            "抓取重试 3 次后放弃", new HttpRequestException("由于目标计算机积极拒绝，无法连接。")));
        Assert.Contains("可能需要代理", message);
        Assert.Contains("积极拒绝", message);
    }

    [Fact]
    public void Ssl_handshake_message_is_actionable()
    {
        var message = ScrapeFriendly.Message(new HttpRequestException(
            "The SSL connection could not be established, see inner exception.",
            new HttpRequestException("SSL 连接无法建立")));
        Assert.Contains("SSL/TLS", message);
        Assert.Contains("换一个节点", message);
    }

    [Fact]
    public void Corrupted_tls_frame_message_is_actionable()
    {
        var message = ScrapeFriendly.Message(new HttpRequestException(
            "Cannot determine the frame size or a corrupted frame was received."));
        Assert.Contains("协议与端口不匹配", message);
        Assert.Contains("全局模式", message);
    }

    [Fact]
    public void Ssl_option_follows_config()
    {
        // 默认（是）→ 忽略配置；否 → 严格校验。
        var lenient = new AppConfig();
        Assert.True(ScrapeProber.ClientOptions(lenient, ScrapeSource.Jav321).AllowBadSsl);

        var strict = new AppConfig();
        strict.Misc.IgnoreSsl = false;
        Assert.False(ScrapeProber.ClientOptions(strict, ScrapeSource.Jav321).AllowBadSsl);
    }

    [Fact]
    public void Adapter_capabilities_match_candidate_registry()
    {
        // 适配器模型一致性：工厂装配的每个适配器自我声明的能力，
        //都必须出现在 CapableSources 登记表里（新站漏登记即刻暴露）。
        var config = new AppConfig();
        Assert.All(new (IScraper Scraper, ScrapeSource Source)[]
        {
            (ScraperFactory.CreateJavlibrary(config), ScrapeSource.Javlibrary),
            (ScraperFactory.CreateJavbus(config, uncensored: false), ScrapeSource.JavbusYouma),
            (ScraperFactory.CreateJavbus(config, uncensored: true), ScrapeSource.JavbusWuma),
            (ScraperFactory.CreateJav321(config), ScrapeSource.Jav321),
            (ScraperFactory.CreateJavDbFc2(config), ScrapeSource.JavDbFc2),
        }, entry =>
        {
            Assert.Equal(entry.Source, entry.Scraper.Source);
            Assert.NotEmpty(entry.Scraper.Categories);
            Assert.All(entry.Scraper.Categories, category =>
                Assert.Contains(entry.Source, ScraperFactory.CapableSources(category)));
        });
    }

    [Fact]
    public void Youma_candidates_prefer_censored_site_then_fall_back()
    {
        // 有码候选：Javlibrary 打头（热评等附加能力），Javbus 有码兜底；
        // 其余类别（当前能力表）各站唯一。
        Assert.Equal([ScrapeSource.Javlibrary, ScrapeSource.JavbusYouma], ScraperFactory.CapableSources(CarCategory.Youma));
        Assert.Equal([ScrapeSource.JavbusWuma], ScraperFactory.CapableSources(CarCategory.Wuma));
        Assert.Equal([ScrapeSource.Jav321], ScraperFactory.CapableSources(CarCategory.Suren));
        Assert.Equal([ScrapeSource.JavDbFc2], ScraperFactory.CapableSources(CarCategory.Fc2));
        Assert.Empty(ScraperFactory.CapableSources(CarCategory.Unknown));
    }

    private sealed class ThrowingFetcher(string message) : IPageFetcher
    {
        public IReadOnlyList<string> Requests { get; } = [];
        public Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default) =>
            throw new HttpRequestException(message);

        public Task<string> PostFormAsync(string url, IReadOnlyDictionary<string, string> form, CancellationToken cancellationToken = default) =>
            throw new HttpRequestException(message);
    }
}
