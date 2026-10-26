using HyperJav.Core.Models;
using HyperJav.Core.Scraping;
using HyperJav.Core.Scraping.Scrapers;
using Xunit;

namespace HyperJav.Core.Tests.Scraping;

public sealed class JavlibraryScraperTests
{
    private const string Base = "https://www.example-lib.com";
    private const string Code = "ABP-123";

    [Fact]
    public async Task Parses_detail_with_hot_comments_when_enabled()
    {
        var fetcher = new FakePageFetcher(new Dictionary<string, string>
        {
            [$"{Base}/search.php?q={Code}&type=0&parent=ce"] = await TestFixtures.ScrapingAsync("javlibrary-search.html"),
            [$"{Base}/?v=javme0"] = await TestFixtures.ScrapingAsync("javlibrary-detail.html"),
        });

        var metadata = await new JavlibraryScraper(fetcher, Base, collectHotComments: true).ScrapeAsync(Code);

        Assert.NotNull(metadata);
        Assert.Equal(ScrapeSource.Javlibrary, metadata!.SourceSite);
        Assert.Equal(Code, metadata.CarCode);
        Assert.Equal("示例标题", metadata.Title);
        Assert.Equal(new DateOnly(2020, 1, 1), metadata.ReleaseDate);
        Assert.Equal(120, metadata.RuntimeMinutes);
        Assert.Equal("导演一", metadata.Director);
        Assert.Equal("片商一", metadata.Studio);
        Assert.Equal("系列一", metadata.Series);
        Assert.Equal(["特征甲", "特征乙"], metadata.RawFeatures);
        Assert.Equal(["演员甲", "演员乙"], metadata.Actors);
        Assert.Equal(4.56, metadata.Rating);
        Assert.Equal("https://pics.example/abp123pl.jpg", metadata.PosterUrl);
        // 网友热评（开关打开时收集）。
        Assert.Equal(["热评一：示例评语。", "热评二：另一条示例。"], metadata.HotComments);
    }

    [Fact]
    public async Task Skips_hot_comments_when_disabled()
    {
        var fetcher = new FakePageFetcher(new Dictionary<string, string>
        {
            [$"{Base}/search.php?q={Code}&type=0&parent=ce"] = await TestFixtures.ScrapingAsync("javlibrary-search.html"),
            [$"{Base}/?v=javme0"] = await TestFixtures.ScrapingAsync("javlibrary-detail.html"),
        });

        var metadata = await new JavlibraryScraper(fetcher, Base, collectHotComments: false).ScrapeAsync(Code);

        Assert.NotNull(metadata);
        Assert.Empty(metadata!.HotComments);
    }

    [Fact]
    public async Task No_search_results_returns_null()
    {
        var fetcher = new FakePageFetcher(new Dictionary<string, string>
        {
            [$"{Base}/search.php?q={Code}&type=0&parent=ce"] = "<html><body><h1>无结果</h1></body></html>",
        });

        var metadata = await new JavlibraryScraper(fetcher, Base, collectHotComments: true).ScrapeAsync(Code);
        Assert.Null(metadata);
    }
}
