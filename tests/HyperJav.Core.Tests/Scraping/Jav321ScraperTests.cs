using HyperJav.Core.Models;
using HyperJav.Core.Scraping;
using HyperJav.Core.Scraping.Scrapers;
using Xunit;

namespace HyperJav.Core.Tests.Scraping;

public sealed class Jav321ScraperTests
{
    private const string Base = "https://www.example-321.com";
    private const string Code = "GANA-1865";

    [Fact]
    public async Task Posts_search_then_parses_detail_from_json_path()
    {
        var fetcher = new FakePageFetcher(new Dictionary<string, string>
        {
            [$"{Base}/video/suren-999"] = await TestFixtures.ScrapingAsync("jav321-detail.html"),
        }, postResponse: """{"error": "", "path": "/video/suren-999"}""");

        var metadata = await new Jav321Scraper(fetcher, Base).ScrapeAsync(Code);

        Assert.NotNull(metadata);
        Assert.Equal(ScrapeSource.Jav321, metadata!.SourceSite);
        Assert.Equal(Code, metadata.CarCode);
        Assert.Equal("示例标题（素人样本）", metadata.Title);
        Assert.Equal(new DateOnly(2018, 6, 1), metadata.ReleaseDate);
        Assert.Equal("片商一", metadata.Studio);
        Assert.Equal("系列一", metadata.Series);
        Assert.Equal(["演员甲", "演员乙"], metadata.Actors);
        // 原始 cover 相对地址被绝对化。
        Assert.Equal($"{Base}/pic/suren-999.jpg", metadata.PosterUrl);
        // 请求顺序：先 POST 搜索再 GET 详情。
        Assert.Equal(
            [$"POST {Base}/search/data isbn={Code}", $"{Base}/video/suren-999"],
            fetcher.Requests);
    }

    [Fact]
    public async Task Empty_path_returns_null()
    {
        var fetcher = new FakePageFetcher(new Dictionary<string, string>(), postResponse: """{"error": "", "path": ""}""");
        var metadata = await new Jav321Scraper(fetcher, Base).ScrapeAsync(Code);
        Assert.Null(metadata);
    }

    [Fact]
    public async Task Non_json_response_returns_null()
    {
        var fetcher = new FakePageFetcher(new Dictionary<string, string>(), postResponse: "<html><body>旧版页面</body></html>");
        var metadata = await new Jav321Scraper(fetcher, Base).ScrapeAsync(Code);
        Assert.Null(metadata);
    }
}
