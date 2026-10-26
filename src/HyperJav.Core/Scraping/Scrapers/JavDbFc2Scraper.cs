using HyperJav.Core.Models;

namespace HyperJav.Core.Scraping.Scrapers;

/// <summary>JavDb（FC2 专用侧）。流程：搜索页 → 首个结果 → 详情解析。</summary>
public sealed class JavDbFc2Scraper : IScraper
{
    private readonly IPageFetcher _fetcher;
    private readonly string _baseRoot;

    public JavDbFc2Scraper(IPageFetcher fetcher, string baseUrl)
    {
        _fetcher = fetcher;
        _baseRoot = baseUrl.TrimEnd('/');
    }

    public ScrapeSource Source => ScrapeSource.JavDbFc2;

    /// <summary>能力：FC2 流程（对标准车牌同样可查，先以 FC2 为主入册）。</summary>
    public IReadOnlyList<CarCategory> Categories { get; } = [CarCategory.Fc2];

    public IReadOnlyList<string> Requests => _fetcher.Requests;

    public async Task<MovieMetadata?> ScrapeAsync(string carCode, CancellationToken cancellationToken = default)
    {
        // 1. 搜索页。
        var searchUrl = $"{_baseRoot}/search?q={Uri.EscapeDataString(carCode)}&f=all";
        var searchHtml = await _fetcher.GetStringAsync(searchUrl, cancellationToken);
        var search = await HtmlPage.ParseAsync(searchHtml, cancellationToken);
        var detailHref = search.Attr(".movie-list .item a.box", "href")
            ?? search.Attr("a.box", "href");
        if (string.IsNullOrWhiteSpace(detailHref))
        {
            return null;
        }

        // 2. 详情页。
        var detailHtml = await _fetcher.GetStringAsync(
            ScrapeSites.Absolutize(detailHref, _baseRoot + "/"), cancellationToken);
        var page = await HtmlPage.ParseAsync(detailHtml, cancellationToken);
        return JavDbDetailParser.Parse(page, carCode, _baseRoot + "/");
    }
}

/// <summary>JavDb 详情页解析（选择器集中在常量区，P5 验收可修正）。</summary>
public static class JavDbDetailParser
{
    private static readonly string[] TitleSelectors = ["strong.current-title", ".video-title strong", "h2", "meta[property='og:title'][content]"];

    private const string PanelRowSelector = ".movie-panel-info .panel-block";
    private const string PanelLabelSelector = "strong";
    private const string ActorSelector = ".value a";
    private const string TagSelector = ".value .tag";

    private static readonly string[] IdMarkers = ["識別碼", "识别码", "id"];
    private static readonly string[] DateMarkers = ["發行日期", "发行日期", "released"];
    private static readonly string[] LengthMarkers = ["時長", "时长", "长度", "duration"];
    private static readonly string[] DirectorMarkers = ["導演", "导演", "director"];
    private static readonly string[] StudioMarkers = ["製作", "制作", "studio"];
    private static readonly string[] PublisherMarkers = ["發行", "发行", "publish"];
    private static readonly string[] SeriesMarkers = ["系列", "series"];
    private static readonly string[] ActorMarkers = ["演員", "演员", "actress", "actor"];
    private static readonly string[] TagMarkers = ["類別", "类别", "tags", "genre"];

    public static MovieMetadata? Parse(HtmlPage page, string carCode, string baseRoot)
    {
        var title = FirstText(page, TitleSelectors) is { } rawTitle
            ? ScrapeHelpers.CollapseWhitespace(rawTitle)
            : null;
        if (title is null)
        {
            return null;
        }
        var originTitle = ScrapeHelpers.CollapseWhitespace(page.Text("strong.origin-title"))
            ?? ScrapeHelpers.CollapseWhitespace(page.Text(".origin-title"));

        string? dateText = null;
        string? lengthText = null;
        string? director = null;
        string? studio = null;
        string? series = null;
        var actors = new List<string>();
        var features = new List<string>();
        foreach (var row in page.All(PanelRowSelector))
        {
            var label = page.Text(PanelLabelSelector, row);
            if (string.IsNullOrWhiteSpace(label))
            {
                continue;
            }
            var labelText = label.Trim().ToLowerInvariant();
            // 面板行内含链接的行（演员/类别）按锚点取，避免把"更多"等杂词带入。
            if (ActorMarkers.Any(m => labelText.Contains(m, StringComparison.Ordinal)))
            {
                actors.AddRange(page.TextList(ActorSelector, row));
            }
            else if (TagMarkers.Any(m => labelText.Contains(m, StringComparison.Ordinal)))
            {
                features.AddRange(page.TextList(TagSelector, row));
            }
            else
            {
                var value = row.TextContent.Trim();
                var start = value.IndexOf(label.Trim(), StringComparison.Ordinal);
                var text = ScrapeHelpers.CollapseWhitespace(start < 0 ? "" : value[(start + label.Trim().Length)..].Trim(':', '：', ' ')) ?? "";
                if (IdMarkers.Any(m => labelText.Contains(m, StringComparison.Ordinal)))
                {
                    // 识别码行跳过（以传入车牌为准）。
                }
                else if (DateMarkers.Any(m => labelText.Contains(m, StringComparison.Ordinal)))
                {
                    dateText = text;
                }
                else if (LengthMarkers.Any(m => labelText.Contains(m, StringComparison.Ordinal)))
                {
                    lengthText = text;
                }
                else if (DirectorMarkers.Any(m => labelText.Contains(m, StringComparison.Ordinal)))
                {
                    director = text.Length > 0 ? text : null;
                }
                else if (StudioMarkers.Any(m => labelText.Contains(m, StringComparison.Ordinal))
                    && !PublisherMarkers.Any(m => labelText.Contains(m, StringComparison.Ordinal)))
                {
                    studio = text.Length > 0 ? text : null;
                }
                else if (SeriesMarkers.Any(m => labelText.Contains(m, StringComparison.Ordinal)))
                {
                    series = text.Length > 0 ? text : null;
                }
            }
        }

        var cover = page.Absolutize(page.Attr("img.video-cover", "src"), baseRoot);

        return new MovieMetadata
        {
            CarCode = carCode,
            Title = ScrapeHelpers.TrimCarCode(title, carCode),
            OriginalTitle = originTitle,
            ReleaseDate = ScrapeHelpers.Date(dateText),
            Director = director,
            Studio = studio,
            Series = series,
            RuntimeMinutes = ScrapeHelpers.Int(lengthText),
            RawFeatures = features,
            Actors = [.. actors.Distinct()],
            PosterUrl = cover,
            FanartUrl = cover,
            SourceSite = ScrapeSource.JavDbFc2,
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
