using AngleSharp;
using AngleSharp.Dom;

namespace HyperJav.Core.Scraping;

/// <summary>HTML 解析封装（AngleSharp；仅解析不加载远程资源）。CSS 选择器为字符串假设，P5 验收可修正。</summary>
public sealed class HtmlPage
{
    private readonly IDocument _document;

    private HtmlPage(IDocument document) => _document = document;

    public static async Task<HtmlPage> ParseAsync(string html, CancellationToken cancellationToken = default)
    {
        var context = BrowsingContext.New(AngleSharp.Configuration.Default);
        return new HtmlPage(await context.OpenAsync(request => request.Content(html), cancellationToken));
    }

    public IElement? First(string selector) => _document.QuerySelector(selector);

    public string? Text(string selector)
    {
        return First(selector) is { } element ? element.TextContent.Trim() : null;
    }

    public string? Text(string selector, IElement ancestor)
    {
        var element = ancestor.QuerySelector(selector);
        return element is null ? null : element.TextContent.Trim();
    }

    public string? Attr(string selector, string attribute)
        => First(selector)?.GetAttribute(attribute) is { Length: > 0 } value ? value.Trim() : null;

    public string? Attr(string selector, string attribute, IElement ancestor)
        => ancestor.QuerySelector(selector)?.GetAttribute(attribute) is { Length: > 0 } value ? value.Trim() : null;

    public IReadOnlyList<IElement> All(string selector) => [.. _document.QuerySelectorAll(selector)];

    public IReadOnlyList<IElement> All(string selector, IElement ancestor) => [.. ancestor.QuerySelectorAll(selector)];

    public IReadOnlyList<string> TextList(string selector) =>
        All(selector)
            .Select(e => e.TextContent.Trim())
            .Where(t => t.Length > 0)
            .ToArray();

    public IReadOnlyList<string> TextList(string selector, IElement ancestor) =>
        All(selector, ancestor)
            .Select(e => e.TextContent.Trim())
            .Where(t => t.Length > 0)
            .ToArray();

    public IReadOnlyList<string> AttrList(string selector, string attribute) =>
        All(selector)
            .Select(e => e.GetAttribute(attribute)?.Trim())
            .Where(t => !string.IsNullOrEmpty(t))
            .Select(t => t!)
            .ToArray();

    public string? Absolutize(string? href, string baseUrl)
        => ScrapeSites.Absolutize(href, baseUrl);

    /// <summary>
    /// "标签行"提取：选择器命中的行元素（如 &lt;p&gt; / .panel-block）里
    /// 标签元素（如 span.header / strong）在前、取值在其余文本中。
    /// 返回 (label 去冒号, value)。
    /// </summary>
    public IReadOnlyList<(string Label, string Value)> PanelRows(string rowSelector, string labelSelector)
    {
        var rows = new List<(string Label, string Value)>();
        foreach (var row in All(rowSelector))
        {
            var label = Text(labelSelector, row);
            if (string.IsNullOrWhiteSpace(label))
            {
                continue;
            }
            var labelText = label.Trim();
            var blockText = row.TextContent.Trim();
            var start = blockText.IndexOf(labelText, StringComparison.Ordinal);
            var value = start < 0 ? "" : blockText[(start + labelText.Length)..].Trim(':', '：', ' ', '　');
            rows.Add((labelText.TrimEnd(':', '：', ' '), value));
        }
        return rows;
    }
}
