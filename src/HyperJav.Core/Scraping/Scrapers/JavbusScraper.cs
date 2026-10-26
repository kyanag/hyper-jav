using HyperJav.Core.Models;

namespace HyperJav.Core.Scraping.Scrapers;

/// <summary>
/// Javbus（有码 / 无码共用一个站点类）。流程：详情直链 → 404 时回退搜索页 → 详情解析。
/// 选择器为常见页面结构假设，全部集中在 <see cref="JavbusDetailParser"/>，P5 验收可修正。
/// </summary>
public sealed class JavbusScraper : IScraper
{
    private readonly IPageFetcher _fetcher;
    private readonly string _baseRoot;   // 无尾斜杠
    private readonly bool _uncensored;

    public JavbusScraper(IPageFetcher fetcher, string baseUrl, bool uncensored)
    {
        _fetcher = fetcher;
        _baseRoot = baseUrl.TrimEnd('/');
        _uncensored = uncensored;
        Categories = uncensored ? [CarCategory.Wuma] : [CarCategory.Youma];
    }

    public ScrapeSource Source => _uncensored ? ScrapeSource.JavbusWuma : ScrapeSource.JavbusYouma;

    /// <summary>能力：与构造时的无码开关联动（同一个站，两个侧）。</summary>
    public IReadOnlyList<CarCategory> Categories { get; }

    public IReadOnlyList<string> Requests => _fetcher.Requests;

    public async Task<MovieMetadata?> ScrapeAsync(string carCode, CancellationToken cancellationToken = default)
    {
        // 1. 详情直链（无码在 /uncensored/ 路径下）。
        var detailUrl = _uncensored
            ? $"{_baseRoot}/uncensored/{Uri.EscapeDataString(carCode)}"
            : $"{_baseRoot}/{Uri.EscapeDataString(carCode)}";

        var page = await GetDetailPageAsync(detailUrl, carCode, cancellationToken);
        if (page is null)
        {
            return null;
        }
        return JavbusDetailParser.Parse(page, carCode, Source, _baseRoot + "/");
    }

    /// <summary>详情直链 404 → 搜索页（type=1 有码 / type=2 无码）首个结果 → 详情页。</summary>
    private async Task<HtmlPage?> GetDetailPageAsync(string detailUrl, string carCode, CancellationToken cancellationToken)
    {
        string detailHtml;
        try
        {
            detailHtml = await _fetcher.GetStringAsync(detailUrl, cancellationToken);
        }
        catch (ScrapeException error) when (error.IsNotFound)
        {
            var searchUrl = $"{_baseRoot}/search/{Uri.EscapeDataString(carCode)}?type={(_uncensored ? 2 : 1)}&parent=ce";
            var searchHtml = await _fetcher.GetStringAsync(searchUrl, cancellationToken);
            var search = await HtmlPage.ParseAsync(searchHtml, cancellationToken);
            var href = SearchFirst(search);
            if (string.IsNullOrWhiteSpace(href))
            {
                return null;
            }
            detailHtml = await _fetcher.GetStringAsync(ScrapeSites.Absolutize(href, _baseRoot + "/"), cancellationToken);
        }
        return await HtmlPage.ParseAsync(detailHtml, cancellationToken);
    }

    private static string? SearchFirst(HtmlPage page)
    {
        foreach (var selector in JavbusDetailParser.SearchResultSelectors)
        {
            var href = page.Attr(selector, "href");
            if (!string.IsNullOrWhiteSpace(href))
            {
                return href;
            }
        }
        return null;
    }
}

/// <summary>Javbus 详情页解析（选择器集中在常量区，遗漏字段记 null 而非失败）。</summary>
public static class JavbusDetailParser
{
    // --- 假定选择器（docs/specs/p2-notes.md "待样本核对"清单） ---
    public static readonly string[] SearchResultSelectors =
    [
        "div.movie-list a.movie-box",
        "a.movie-box",
        "div.movie-list a.box",
        "a.box",
    ];

    private static readonly string[] TitleSelectors =
    [
        "#video-title h2",
        "h2.title",
        "h3.title",
        "h2",                                 // 真站现行结构兜底（直挂车牌+标题）
        "head title",                         // 页 title 元素（含"车牌 标题"）
        "meta[property='og:title'][content]", // content 属性兜底
    ];

    private const string InfoRowSelector = ".info p, .movie-info p";
    private const string InfoLabelSelector = "span.header";
    private const string GenreSelector = "span.genre a, .genre label a";
    private const string AvatarBoxSelector = ".avatar-box";
    private const string BigImageSelector = "a.bigImage";
    private const string JacketImageSelector = "#video_jacket_img";
    private const string SampleImageSelector = "#sample-waterfall .photo-frame img";

    // 行标签（繁/简/英 兼容；匹配为 contains）。
    private static readonly string[] IdMarkers = ["識別碼", "识别码", "id:"];
    private static readonly string[] DateMarkers = ["發行日期", "发行日期", "released"];
    private static readonly string[] LengthMarkers = ["長度", "长度", "length", "時長", "时长"];
    private static readonly string[] DirectorMarkers = ["導演", "导演", "director"];
    private static readonly string[] StudioMarkers = ["製作", "制作", "studio", "maker"];
    private static readonly string[] ProducerMarkers = ["發行", "发行", "publish"];
    private static readonly string[] SeriesMarkers = ["系列", "series"];

    public static MovieMetadata? Parse(HtmlPage page, string carCode, ScrapeSource source, string baseRoot)
    {
        var title = FirstText(page, TitleSelectors) is { } rawTitle
            ? ScrapeHelpers.CollapseWhitespace(rawTitle)
            : null;
        if (title is null)
        {
            return null; // 无标题视为页面异常（非本片详情 / 改版）。
        }

        string? dateText = null;        string? lengthText = null;
        string? director = null;
        string? studio = null;
        string? series = null;
        foreach (var (label, value) in page.PanelRows(InfoRowSelector, InfoLabelSelector))
        {
            var text = ScrapeHelpers.CollapseWhitespace(value) ?? "";
            var labelText = label.ToLowerInvariant();
            if (IdMarkers.Any(m => labelText.Contains(m, StringComparison.Ordinal)))
            {
                // 识别码行跳过（以传入的规范化车牌为准）。
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
                director = ScrapeHelpers.DashToNull(text);
            }
            else if (StudioMarkers.Any(m => labelText.Contains(m, StringComparison.Ordinal))
                && !ProducerMarkers.Any(m => labelText.Contains(m, StringComparison.Ordinal)))
            {
                studio = ScrapeHelpers.DashToNull(text);
            }
            else if (SeriesMarkers.Any(m => labelText.Contains(m, StringComparison.Ordinal)))
            {
                series = ScrapeHelpers.DashToNull(text);
            }
        }

        var bigImage = page.Absolutize(page.Attr(BigImageSelector, "href"), baseRoot);
        var jacket = page.Absolutize(page.Attr(JacketImageSelector, "src"), baseRoot);
        var samples = page.AttrList(SampleImageSelector, "src")
            .Select(src => page.Absolutize(src, baseRoot))
            .Where(url => !string.IsNullOrEmpty(url))
            .Select(url => url!)
            .ToArray();

        var actors = new List<string>();
        foreach (var box in page.All(AvatarBoxSelector))
        {
            foreach (var span in box.QuerySelectorAll("span"))
            {
                var name = span.TextContent.Trim();
                if (name.Length > 0 && !name.StartsWith("(", StringComparison.Ordinal))
                {
                    actors.Add(name);
                }
            }
        }

        return new MovieMetadata
        {
            CarCode = carCode,
            Title = ScrapeHelpers.TrimCarCode(title, carCode),
            ReleaseDate = ScrapeHelpers.Date(dateText),
            Director = director,
            Studio = studio,
            Series = series,
            RuntimeMinutes = ScrapeHelpers.Int(lengthText),
            RawFeatures = page.TextList(GenreSelector),
            Actors = [.. actors.Distinct()],
            // 小图为海报位（缩略）、bigImage 大图为 dvd 封面（远图）。
            PosterUrl = string.IsNullOrEmpty(jacket) ? bigImage : jacket,
            FanartUrl = string.IsNullOrEmpty(bigImage) ? jacket : bigImage,
            ExtraImages = samples,
            SourceSite = source,
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
