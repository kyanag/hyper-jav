using System.Text.Json;
using HyperJav.Core.Models;

namespace HyperJav.Core.Scraping.Scrapers;

/// <summary>
/// Jav321（素人）。流程：POST /search/data（isbn=车牌）→ 响应 JSON 取 path → GET 详情页。
/// 注意：素人源没有特征对照表 sheet，特征原样透传（docs/specs/field-mapping.md）。
/// </summary>
public sealed class Jav321Scraper : IScraper
{
    private readonly IPageFetcher _fetcher;
    private readonly string _baseRoot;

    public Jav321Scraper(IPageFetcher fetcher, string? baseUrl = null)
    {
        _fetcher = fetcher;
        _baseRoot = (baseUrl ?? ScrapeSites.Jav321Base).TrimEnd('/');
    }

    public ScrapeSource Source => ScrapeSource.Jav321;

    /// <summary>能力：素人（没有特征对照表 sheet，特征原样透传；对 FC2 的覆盖待真站核对后再声明）。</summary>
    public IReadOnlyList<CarCategory> Categories { get; } = [CarCategory.Suren];

    public IReadOnlyList<string> Requests => _fetcher.Requests;

    public async Task<MovieMetadata?> ScrapeAsync(string carCode, CancellationToken cancellationToken = default)
    {
        // 1. 搜索（表单 POST，返回 JSON：{"error":"", "path":"/av/..."}）。
        var searchResponse = await _fetcher.PostFormAsync(
            $"{_baseRoot}/search/data",
            new Dictionary<string, string> { ["isbn"] = carCode },
            cancellationToken);
        var detailPath = ExtractSearchPath(searchResponse);
        if (detailPath is null)
        {
            return null; // 未收录。
        }

        // 2. 详情页。
        var detailHtml = await _fetcher.GetStringAsync($"{_baseRoot}{EnsureLeadingSlash(detailPath)}", cancellationToken);
        var page = await HtmlPage.ParseAsync(detailHtml, cancellationToken);
        return Jav321DetailParser.Parse(page, carCode, _baseRoot + "/");
    }

    private static string? ExtractSearchPath(string searchResponse)
    {
        try
        {
            using var document = JsonDocument.Parse(searchResponse);
            if (document.RootElement.TryGetProperty("path", out var path)
                && path.ValueKind == JsonValueKind.String
                && path.GetString() is { Length: > 0 } value)
            {
                return value;
            }
        }
        catch (JsonException)
        {
            // 非 JSON（未收录 / 站点改版）按"未找到"处理。
        }
        return null;
    }

    private static string EnsureLeadingSlash(string path) => path.StartsWith('/') ? path : '/' + path;
}

/// <summary>Jav321 详情页解析（选择器集中在常量区，P5 验收可修正）。</summary>
public static class Jav321DetailParser
{
    private static readonly string[] TitleSelectors = [".panel-body h3", ".col-md-9 h3", "h3", "meta[property='og:title'][content]"];

    private const string InfoRowSelector = ".panel-body p";
    private const string InfoLabelSelector = "strong";

    private static readonly string[] IdMarkers = ["识别码", "car", "id"];
    private static readonly string[] DateMarkers = ["日期", "date", "発売"];
    private static readonly string[] LengthMarkers = ["时长", "长度", "時間", "分钟", "時間"];
    private static readonly string[] DirectorMarkers = ["导演", "監督", "director"];
    private static readonly string[] StudioMarkers = ["制作", "メーカー", "studio", "maker"];
    private static readonly string[] SeriesMarkers = ["系列", "シリーズ", "series"];
    private static readonly string[] ActorMarkers = ["女优", "女優", "演员", "actor", "actress"];

    public static MovieMetadata? Parse(HtmlPage page, string carCode, string baseRoot)
    {
        var title = FirstText(page, TitleSelectors) is { } rawTitle
            ? ScrapeHelpers.CollapseWhitespace(rawTitle)
            : null;
        if (title is null)
        {
            return null;
        }

        string? dateText = null;
        string? lengthText = null;
        string? director = null;
        string? studio = null;
        string? series = null;
        IReadOnlyList<string> actors = [];
        foreach (var (label, value) in page.PanelRows(InfoRowSelector, InfoLabelSelector))
        {
            var text = ScrapeHelpers.CollapseWhitespace(value) ?? "";
            var labelText = label.ToLowerInvariant();
            if (DateMarkers.Any(m => labelText.Contains(m, StringComparison.Ordinal)))
            {
                dateText = text;
            }
            else if (LengthMarkers.Any(m => labelText.Contains(m, StringComparison.Ordinal)))
            {
                lengthText = text;
            }
            else if (ActorMarkers.Any(m => labelText.Contains(m, StringComparison.Ordinal)))
            {
                actors = ScrapeHelpers.SplitNames(text);
            }
            else if (StudioMarkers.Any(m => labelText.Contains(m, StringComparison.Ordinal)))
            {
                studio = text.Length > 0 ? text : null;
            }
            else if (DirectorMarkers.Any(m => labelText.Contains(m, StringComparison.Ordinal)))
            {
                director = text.Length > 0 ? text : null;
            }
            else if (SeriesMarkers.Any(m => labelText.Contains(m, StringComparison.Ordinal)))
            {
                series = text.Length > 0 ? text : null;
            }
        }

        var cover = page.Absolutize(page.Attr("img.img-responsive", "src"), baseRoot);

        return new MovieMetadata
        {
            CarCode = carCode,
            Title = ScrapeHelpers.TrimCarCode(title, carCode),
            ReleaseDate = ScrapeHelpers.Date(dateText),
            Director = director,
            Studio = studio,
            Series = series,
            RuntimeMinutes = ScrapeHelpers.Int(lengthText),
            // 素人源无特征 sheet → 特征透传（页面上若有分类则原样取）。
            RawFeatures = page.TextList(".panel-body .genre a"),
            Actors = actors,
            PosterUrl = cover,
            FanartUrl = cover,
            SourceSite = ScrapeSource.Jav321,
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
