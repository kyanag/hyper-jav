using HyperJav.Core.Models;
using HyperJav.Core.Scraping;
using HyperJav.Core.Scraping.Scrapers;
using Xunit;

namespace HyperJav.Core.Tests.Scraping;

public sealed class JavDbFc2ScraperTests
{
    private const string Base = "https://www.example-db.com";
    private const string Code = "FC2-777111";

    [Fact]
    public async Task Search_then_detail_parses_fc2_fields()
    {
        var fetcher = new FakePageFetcher(new Dictionary<string, string>
        {
            [$"{Base}/search?q={Code}&f=all"] = await TestFixtures.ScrapingAsync("javdb-search.html"),
            [$"{Base}/v/abc123def"] = await TestFixtures.ScrapingAsync("javdb-detail.html"),
        });

        var metadata = await new JavDbFc2Scraper(fetcher, Base).ScrapeAsync(Code);

        Assert.NotNull(metadata);
        Assert.Equal(ScrapeSource.JavDbFc2, metadata!.SourceSite);
        Assert.Equal(Code, metadata.CarCode);
        Assert.Equal("示例标题", metadata.Title);
        Assert.Equal("サンプル・タイトル", metadata.OriginalTitle);
        Assert.Equal(new DateOnly(2014, 8, 13), metadata.ReleaseDate);
        Assert.Equal(120, metadata.RuntimeMinutes);
        Assert.Equal("导演一", metadata.Director);
        Assert.Equal("片商一", metadata.Studio);
        Assert.Equal(["特征甲", "特征乙"], metadata.RawFeatures);
        Assert.Equal(["演员甲", "演员乙"], metadata.Actors);
        Assert.Equal("https://img.example/fc2-cover.jpg", metadata.PosterUrl);
    }

    [Fact]
    public async Task No_results_returns_null()
    {
        var fetcher = new FakePageFetcher(new Dictionary<string, string>
        {
            [$"{Base}/search?q={Code}&f=all"] = "<html><body><div class='movie-list'></div></body></html>",
        });

        var metadata = await new JavDbFc2Scraper(fetcher, Base).ScrapeAsync(Code);
        Assert.Null(metadata);
    }
}
