using HyperJav.Core.Models;

namespace HyperJav.Core.Scraping.Scrapers;

/// <summary>Javlibrary（有码，含网友热评；图片可切换为优先 javbus 来源——该项在 P2 由调用方组合，不在本类内）。</summary>
public sealed class JavlibraryScraper : IScraper
{
    private readonly IPageFetcher _fetcher;
    private readonly string _baseRoot;   // 无尾斜杠
    private readonly bool _collectHotComments;

    public JavlibraryScraper(IPageFetcher fetcher, string baseUrl, bool collectHotComments)
    {
        _fetcher = fetcher;
        _baseRoot = baseUrl.TrimEnd('/');
        _collectHotComments = collectHotComments;
    }

    public ScrapeSource Source => ScrapeSource.Javlibrary;

    /// <summary>能力：有码（网友热评即有码站的附加能力）。</summary>
    public IReadOnlyList<CarCategory> Categories { get; } = [CarCategory.Youma];

    public IReadOnlyList<string> Requests => _fetcher.Requests;

    public async Task<MovieMetadata?> ScrapeAsync(string carCode, CancellationToken cancellationToken = default)
    {
        // 1. 搜索页。
        var searchUrl = $"{_baseRoot}/search.php?q={Uri.EscapeDataString(carCode)}&type=0&parent=ce";
        var searchHtml = await _fetcher.GetStringAsync(searchUrl, cancellationToken);
        var search = await HtmlPage.ParseAsync(searchHtml, cancellationToken);
        var detailHref = search.Attr(".videos .video a", "href") ?? search.Attr(".videos a", "href");
        if (string.IsNullOrWhiteSpace(detailHref))
        {
            return null; // 搜索无结果。
        }

        // 2. 详情页（结果链接形如 ?v=javme……，按站点根绝对化）。
        var detailHtml = await _fetcher.GetStringAsync(
            ScrapeSites.Absolutize(detailHref, _baseRoot + "/"), cancellationToken);
        var page = await HtmlPage.ParseAsync(detailHtml, cancellationToken);
        return JavlibraryDetailParser.Parse(page, carCode, _collectHotComments, _baseRoot + "/");
    }
}

/// <summary>Javlibrary 详情页解析（选择器集中在常量区，P5 验收可修正）。</summary>
public static class JavlibraryDetailParser
{
    private static readonly string[] TitleSelectors =
    [
        "#video_title a",
        "#video_title h2",
        "meta[property='og:title'][content]",
    ];

    public static MovieMetadata? Parse(HtmlPage page, string carCode, bool collectHotComments, string baseRoot)
    {
        var title = FirstText(page, TitleSelectors) is { } rawTitle
            ? ScrapeHelpers.CollapseWhitespace(rawTitle)
            : null;
        if (title is null)
        {
            return null;
        }

        string? CleanMissing(string? cell)
        {
            var text = ScrapeHelpers.DashToNull(cell);
            return text is null or "-" or "----" ? null : text.Trim('-', '－').Trim() is { Length: > 0 } trimmed ? trimmed : null;
        }

        var jacket = page.Absolutize(page.Attr("#video_jacket_img", "src"), baseRoot);

        return new MovieMetadata
        {
            CarCode = carCode,
            Title = ScrapeHelpers.TrimCarCode(title, carCode),
            ReleaseDate = ScrapeHelpers.Date(ScrapeHelpers.DashToNull(page.Text("#video_date .text"))),
            Director = CleanMissing(page.Text("#video_director .text")),
            Studio = CleanMissing(page.Text("#video_maker .text")),
            Series = CleanMissing(page.Text("#video_label .text")),
            RuntimeMinutes = ScrapeHelpers.Int(page.Text("#video_length .text")),
            RawFeatures = page.TextList("#video_genres td.text a"),
            Actors = page.TextList("#video_cast td.text a"),
            Rating = ScrapeHelpers.Double(page.Text("#video_review .score .text")),
            // 封面只有 jav jacket 一张：海报与 dvd 封面同源。
            PosterUrl = jacket,
            FanartUrl = jacket,
            // 网友热评（收集开关来自 [信息来源] 采集网友的热评）。
            HotComments = collectHotComments ? page.TextList("#video_comments table.comment td.t2") : [],
            SourceSite = ScrapeSource.Javlibrary,
        };
    }

    private static string? FirstText(HtmlPage page, string[] selectors)
    {
        foreach (var selector in selectors)
        {
            if (selector.Contains("meta[", StringComparison.Ordinal))
            {
                var content = page.Attr(selector, "content");
                if (!string.IsNullOrWhiteSpace(content))
                {
                    return content;
                }
            }
            else
            {
                var text = page.Text(selector);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }
        return null;
    }
}
