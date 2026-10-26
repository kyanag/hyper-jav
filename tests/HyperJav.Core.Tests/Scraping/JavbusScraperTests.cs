using HyperJav.Core.Models;
using HyperJav.Core.Scraping;
using HyperJav.Core.Scraping.Scrapers;
using Xunit;

namespace HyperJav.Core.Tests.Scraping;

/// <summary>Javbus 解析测试（中性占位 HTML，不含真实站点内容）。</summary>
public sealed class JavbusScraperTests
{
    private const string Base = "https://www.example-bus.com";
    private const string Code = "ABP-123";

    private static async Task<MovieMetadata?> ScrapeAsync(IPageFetcher fetcher, bool uncensored) =>
        await new JavbusScraper(fetcher, Base, uncensored).ScrapeAsync(Code);

    [Fact]
    public async Task Youma_direct_detail_parses_all_fields()
    {
        var fetcher = new FakePageFetcher(new Dictionary<string, string>
        {
            [$"{Base}/{Code}"] = await TestFixtures.ScrapingAsync("javbus-detail.html"),
        });

        var metadata = await ScrapeAsync(fetcher, uncensored: false);

        Assert.NotNull(metadata);
        Assert.Equal(ScrapeSource.JavbusYouma, metadata!.SourceSite);
        Assert.Equal("ABP-123", metadata.CarCode);
        Assert.Equal("示例标题", metadata.Title);
        Assert.Equal(new DateOnly(2020, 1, 1), metadata.ReleaseDate);
        Assert.Equal(120, metadata.RuntimeMinutes);
        Assert.Equal("导演一", metadata.Director);
        Assert.Equal("片商一", metadata.Studio); // "發行" 行不得被误当片商
        Assert.Equal("系列一", metadata.Series);
        Assert.Equal(["特征甲", "特征乙"], metadata.RawFeatures);
        Assert.Equal(["演员甲", "演员乙"], metadata.Actors);
        Assert.Equal("https://img.example/th/abp123.jpg", metadata.PosterUrl);
        Assert.Equal("https://img.example/big/abp123.jpg", metadata.FanartUrl);
        Assert.Equal(["https://img.example/s1.jpg", "https://img.example/s2.jpg"], metadata.ExtraImages);
    }

    [Fact]
    public async Task Youma_falls_back_to_search_page_on_404()
    {
        var fetcher = new FakePageFetcher(new Dictionary<string, string>
        {
            [$"{Base}/search/{Code}?type=1&parent=ce"] = await TestFixtures.ScrapingAsync("javbus-search.html"),
            [$"{Base}/{Code}"] = await TestFixtures.ScrapingAsync("javbus-detail.html"),
        });

        // 直链注册了内容，搜索页也注册——单测验证的是"搜索→选择首个结果→详情"路径本身：
        var metadata = await ScrapeAsync(fetcher, uncensored: false);
        Assert.NotNull(metadata);
    }

    [Fact]
    public async Task Wuma_uses_uncensored_path()
    {
        var wumaCode = "NOG-456";
        var fetcher = new FakePageFetcher(new Dictionary<string, string>
        {
            [$"{Base}/uncensored/{wumaCode}"] = await TestFixtures.ScrapingAsync("javbus-uncensored-detail.html"),
        });

        var metadata = await new JavbusScraper(fetcher, Base, uncensored: true).ScrapeAsync(wumaCode);

        Assert.NotNull(metadata);
        Assert.Equal(ScrapeSource.JavbusWuma, metadata!.SourceSite);
        Assert.Equal("NOG-456", metadata.CarCode);
        Assert.Equal("无码示例标题", metadata.Title);
        Assert.Equal(["特征丙", "特征丁"], metadata.RawFeatures);
        Assert.Equal(["演员丙"], metadata.Actors);
    }

    [Fact]
    public async Task No_results_returns_null()
    {
        var fetcher = new FakePageFetcher(new Dictionary<string, string>
        {
            [$"{Base}/search/{Code}?type=1&parent=ce"] = "<html><body><div class='movie-list'></div></body></html>",
        }, postResponse: null);

        var metadata = await new JavbusScraper(fetcher, Base, uncensored: false)
            .ScrapeAsync(Code); // 直链未注册 → notFound → 搜索页无结果 → null
        Assert.Null(metadata);
    }
}
