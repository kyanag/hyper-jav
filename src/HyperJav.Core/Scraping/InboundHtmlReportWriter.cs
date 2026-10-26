using System.Globalization;
using System.Net;
using System.Text;
using HyperJav.Core.Configuration;
using HyperJav.Core.Localization;
using HyperJav.Core.Normalization;

namespace HyperJav.Core.Scraping;

/// <summary>报表生成结果（文件路径 + 数据，GUI 可展示摘要）。</summary>
public sealed record InboundReportResult(string Path, InboundReport Report);

/// <summary>
/// 入库明细报表（单文件 HTML，离线可看、零外部依赖）。
/// 三个视角（入库 / 入库+使用 / 仅使用）已预渲染，页面顶部切换；明细支持搜索与排序。
/// </summary>
public static class InboundHtmlReportWriter
{
    public const string FilePrefix = "入库明细报表-";

    public static InboundReportResult Generate(
        string logDirectory,
        string dbPath,
        DateTime from,
        DateTime to,
        string? outputDirectory = null,
        FeatureDictionary? dictionary = null,
        ScriptMode script = ScriptMode.Simplified,
        TranslationCatalog? translations = null)
    {
        if (from > to)
        {
            (from, to) = (to, from);
        }

        var report = InboundReportBuilder.Build(logDirectory, dbPath, from, to, dictionary, script);
        var directory = string.IsNullOrWhiteSpace(outputDirectory)
            ? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(dbPath))!, "reports")
            : outputDirectory;
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{FilePrefix}{DateTime.Now:yyyyMMdd-HHmmss}.html");
        var catalog = translations ?? LoadDefaultTranslations(dbPath);
        File.WriteAllText(path, Render(report, logDirectory, dbPath, catalog), IniTextCodec.Utf8Bom);
        return new InboundReportResult(path, report);
    }

    public static string Render(
        InboundReport report,
        string logDirectory,
        string dbPath,
        TranslationCatalog? translations = null)
    {
        var catalog = translations ?? LoadDefaultTranslations(dbPath);
        var html = new StringBuilder(64 * 1024);
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html lang=\"zh-CN\">");
        html.AppendLine("<head>");
        html.AppendLine("<meta charset=\"utf-8\">");
        html.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        html.AppendLine($"<title>入库明细报表 · {report.From:yyyy-MM-dd} ~ {report.To:yyyy-MM-dd}</title>");
        html.AppendLine("<style>");
        html.AppendLine(Css);
        html.AppendLine("</style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        RenderHeader(html, report, logDirectory, dbPath, catalog);
        RenderSwitcher(html);

        RenderView(html, report, report.View(InboundView.Inbound), "inbound", active: true, catalog);
        RenderView(html, report, report.View(InboundView.InboundAndUsage), "all", active: false, catalog);
        RenderView(html, report, report.View(InboundView.UsageOnly), "usage", active: false, catalog);

        RenderFooter(html, report, catalog);
        html.AppendLine("<script>");
        html.AppendLine(Script);
        html.AppendLine("</script>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");
        return html.ToString();
    }

    private static void RenderHeader(
        StringBuilder html, InboundReport report, string logDirectory, string dbPath,
        TranslationCatalog catalog)
    {
        var coverage = report.LogFrom is { } logFrom && report.LogTo is { } logTo
            ? $"{logFrom:yyyy-MM-dd HH:mm} ~ {logTo:yyyy-MM-dd HH:mm}"
            : "无可用流水";
        var inventory = report.Inventory is { } stats
            ? $"正缓存 {stats.PositiveCount} / 负缓存 {stats.NegativeCount}"
            : "缓存库不可用";

        html.AppendLine("<header>");
        html.AppendLine("<h1>入库明细报表</h1>");
        html.AppendLine("<div class=\"meta\">");
        html.AppendLine($"  <span><b>统计区间</b> {report.From:yyyy-MM-dd HH:mm} ~ {report.To:yyyy-MM-dd HH:mm}</span>");
        html.AppendLine($"  <span><b>生成时间</b> {report.GeneratedAt:yyyy-MM-dd HH:mm:ss}</span>");
        html.AppendLine($"  <span><b>日志覆盖</b> {E(coverage)}（{report.LogFiles} 个文件）</span>");
        html.AppendLine($"  <span><b>库存</b> {E(inventory)}</span>");
        html.AppendLine($"  <span><b>未收录跳过</b> {report.NegativeHits} 次</span>");
        html.AppendLine($"  <span><b>异常</b> {report.Errors} 次</span>");
        if (catalog.Count > 0)
        {
            html.AppendLine($"  <span><b>译名</b> {catalog.Count} 条</span>");
        }
        html.AppendLine("</div>");
        html.AppendLine("<div class=\"source\">");
        html.AppendLine($"  流水：{E(logDirectory)}　｜　元数据：{E(dbPath)}（当前库存快照）");
        html.AppendLine("</div>");
        if (catalog.LoadError is { } loadError)
        {
            html.AppendLine($"<div class=\"warn\">译名文件读取失败：{E(loadError)}（本次全部按原文显示）。</div>");
        }
        if (!report.MetadataAvailable)
        {
            html.AppendLine("<div class=\"warn\">缓存库不可用或为空：片商、系列、演员等只能显示“元数据缺失”。</div>");
        }
        if (report.LogFiles == 0)
        {
            html.AppendLine("<div class=\"warn\">没有找到入库流水文件（data/logs/入库流水-*.log），统计结果为空。</div>");
        }
        else if (report.LogFrom is { } earliest && earliest > report.From)
        {
            html.AppendLine($"<div class=\"warn\">请求区间自 {report.From:yyyy-MM-dd} 开始，但现有流水最早为 {earliest:yyyy-MM-dd HH:mm}；更早的记录不存在。</div>");
        }
        html.AppendLine("</header>");
    }

    private static void RenderSwitcher(StringBuilder html)
    {
        html.AppendLine("<nav class=\"switch\" aria-label=\"统计视角\">");
        html.AppendLine("  <button type=\"button\" class=\"active\" data-view=\"inbound\">入库</button>");
        html.AppendLine("  <button type=\"button\" data-view=\"all\">入库 + 使用</button>");
        html.AppendLine("  <button type=\"button\" data-view=\"usage\">仅缓存使用</button>");
        html.AppendLine("</nav>");
    }

    private static void RenderView(
        StringBuilder html, InboundReport report, InboundViewData view, string key, bool active,
        TranslationCatalog catalog)
    {
        html.AppendLine($"<main class=\"view\" data-view=\"{key}\"{(active ? "" : " hidden")}>");
        RenderCards(html, report, view);
        RenderExplorer(html, view, catalog);
        RenderDetail(html, view, catalog);
        html.AppendLine("</main>");
    }

    private static void RenderCards(StringBuilder html, InboundReport report, InboundViewData view)
    {
        html.AppendLine("<section class=\"cards\">");
        Card(html, view.DistinctCodes, "车牌数（去重）");
        Card(html, view.Total, view.View == InboundView.UsageOnly ? "使用笔数" : "流水笔数");
        if (view.View != InboundView.UsageOnly)
        {
            Card(html, view.AddCount, "新入库");
            Card(html, view.PromoteCount, "转正入库");
            Card(html, view.UpdateCount, "更新");
            if (view.View == InboundView.InboundAndUsage)
            {
                Card(html, view.HitCount, "缓存使用");
            }
        }
        Card(html, view.StudioCount, "片商");
        Card(html, view.SeriesCount, "系列");
        Card(html, view.ActorCount, "演员");
        html.AppendLine("</section>");
        if (report.Inventory is { } inventory && view.View == InboundView.Inbound && inventory.NegativeCount > 0)
        {
            html.AppendLine($"<p class=\"hint\">另有负缓存 {inventory.NegativeCount} 条（未收录），不计入入库车牌。</p>");
        }
    }

    private static void Card(StringBuilder html, int value, string label)
    {
        html.AppendLine($"  <div class=\"card\"><b>{value.ToString("N0", CultureInfo.InvariantCulture)}</b><span>{E(label)}</span></div>");
    }

    /// <summary>
    /// 分类浏览：上方页签切换分类维度，左侧列出该维度的分类项，右侧列出选中项的车牌。
    /// 车牌行带 data-dN（所属各维度分类项下标），切换时只做显隐，不重新请求任何数据。
    /// </summary>
    private static void RenderExplorer(
        StringBuilder html, InboundViewData view, TranslationCatalog catalog)
    {
        var memberships = BuildMemberships(view);
        html.AppendLine("<section class=\"explorer\">");
        html.AppendLine("  <nav class=\"dim-tabs\" role=\"tablist\">");
        for (var index = 0; index < view.Dimensions.Count; index++)
        {
            var dimension = view.Dimensions[index];
            var activeClass = index == 0 ? " active" : "";
            html.AppendLine(
                $"    <button type=\"button\" class=\"dim-tab{activeClass}\" role=\"tab\" data-dim=\"{index}\">" +
                $"{E(dimension.Title)}<span class=\"count\">{dimension.Buckets.Count} 类</span></button>");
        }
        html.AppendLine("  </nav>");
        html.AppendLine("  <div class=\"explorer-body\">");
        html.AppendLine("    <aside class=\"bucket-pane\">");
        html.AppendLine("      <div class=\"bucket-head\">分类项（按车牌去重）</div>");
        for (var index = 0; index < view.Dimensions.Count; index++)
        {
            var dimension = view.Dimensions[index];
            html.AppendLine($"      <div class=\"bucket-list\" data-dim=\"{index}\"{(index == 0 ? "" : " hidden")}>");
            if (!string.IsNullOrWhiteSpace(dimension.Note))
            {
                html.AppendLine($"        <p class=\"bucket-note\">{E(dimension.Note)}</p>");
            }
            html.AppendLine("        <button type=\"button\" class=\"bucket\" data-index=\"-1\" data-label=\"全部\">");
            html.AppendLine("          <span class=\"name\">全部</span>");
            html.AppendLine(
                $"          <span class=\"stat\"><span>{view.DistinctCodes} 个车牌</span><span>100%</span></span>");
            html.AppendLine("          <span class=\"mini\"><i style=\"width:100%\"></i></span>");
            html.AppendLine("        </button>");
            for (var bucketIndex = 0; bucketIndex < dimension.Buckets.Count; bucketIndex++)
            {
                var bucket = dimension.Buckets[bucketIndex];
                var width = bucket.Share.ToString("0.0", CultureInfo.InvariantCulture);
                var activeBucket = index == 0 && bucketIndex == 0 ? " active" : "";
                var bucketName = catalog.Translate(TranslationKindOf(dimension.Kind), bucket.Name);
                html.AppendLine(
                    $"        <button type=\"button\" class=\"bucket{activeBucket}\" data-index=\"{bucketIndex}\" data-label=\"{E(bucketName)}\">");
                html.AppendLine($"          <span class=\"name\">{E(bucketName)}</span>");
                html.AppendLine(
                    $"          <span class=\"stat\"><span>{bucket.CodeCount} 个车牌</span><span>{width}%</span></span>");
                html.AppendLine($"          <span class=\"mini\"><i style=\"width:{width}%\"></i></span>");
                html.AppendLine("        </button>");
            }
            if (dimension.Buckets.Count == 0)
            {
                html.AppendLine("        <p class=\"empty\">本区间没有流水。</p>");
            }
            html.AppendLine("      </div>");
        }
        html.AppendLine("    </aside>");
        html.AppendLine("    <div class=\"code-pane\">");
        html.AppendLine("      <div class=\"code-head\">");
        html.AppendLine($"        <h2>车牌明细<span class=\"sel\">全部・{view.DistinctCodes} 个车牌</span></h2>");
        html.AppendLine("        <input class=\"search\" type=\"search\" placeholder=\"在当前分类中搜索车牌 / 标题 / 片商 / 演员（日文原名也可）…\">");
        html.AppendLine("      </div>");
        html.AppendLine("      <div class=\"table-wrap code-wrap\"><table class=\"bucket-codes\" data-sortable>");
        html.AppendLine("        <thead><tr>");
        foreach (var header in CodeHeaders)
        {
            html.AppendLine($"          <th data-sort=\"text\">{header}</th>");
        }
        html.AppendLine("        </tr></thead>");
        html.AppendLine("        <tbody>");
        foreach (var code in view.Codes)
        {
            var actors = catalog.TranslateList(TranslationKind.Actor, code.Actors);
            var features = catalog.TranslateList(TranslationKind.Feature, code.Features);
            var category = catalog.Translate(TranslationKind.Category, code.Category);
            var studio = catalog.Translate(TranslationKind.Studio, code.Studio);
            var series = catalog.Translate(TranslationKind.Series, code.Series);
            var originals = string.Join(' ', new[] { code.Category, code.Studio, code.Series }
                .Concat(code.Actors)
                .Concat(code.Features));
            var attrs = new StringBuilder();
            for (var index = 0; index < memberships.Length; index++)
            {
                var list = memberships[index].TryGetValue(code.CarCode, out var values) ? values : [];
                attrs.Append($" data-d{index}=\"{string.Join(" ", list)}\"");
            }
            attrs.Append($" data-orig=\"{E(originals)}\"");
            html.AppendLine($"          <tr{attrs}>");
            html.AppendLine($"            <td class=\"code\">{E(code.CarCode)}</td>");
            html.AppendLine($"            <td>{E(code.Prefix)}</td>");
            html.AppendLine($"            <td class=\"title\" title=\"{E(code.Title)}\"><div>{E(code.Title)}</div></td>");
            html.AppendLine($"            <td class=\"nowrap\">{(code.ReleaseDate is { } date ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "—")}</td>");
            html.AppendLine($"            <td>{E(category)}</td>");
            html.AppendLine($"            <td>{E(studio)}</td>");
            html.AppendLine($"            <td>{E(series)}</td>");
            html.AppendLine($"            <td class=\"multi\">{E(actors)}</td>");
            html.AppendLine($"            <td class=\"multi\">{E(features)}</td>");
            html.AppendLine($"            <td class=\"num\">{code.InboundCount}</td>");
            html.AppendLine($"            <td class=\"num\">{code.UsageCount}</td>");
            html.AppendLine($"            <td data-value=\"{code.LastAt:yyyy-MM-ddTHH:mm:ss}\" class=\"nowrap\">{code.LastAt:yyyy-MM-dd HH:mm}</td>");
            html.AppendLine("          </tr>");
        }
        html.AppendLine("        </tbody>");
        html.AppendLine("      </table></div>");
        html.AppendLine("    </div>");
        html.AppendLine("  </div>");
        html.AppendLine("</section>");
    }

    /// <summary>每个维度一张「车牌 → 分类项下标」表，供车牌行写入 data-dN。</summary>
    private static Dictionary<string, List<int>>[] BuildMemberships(InboundViewData view)
    {
        var result = new Dictionary<string, List<int>>[view.Dimensions.Count];
        for (var dimensionIndex = 0; dimensionIndex < view.Dimensions.Count; dimensionIndex++)
        {
            var map = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
            var dimension = view.Dimensions[dimensionIndex];
            for (var bucketIndex = 0; bucketIndex < dimension.Buckets.Count; bucketIndex++)
            {
                foreach (var code in dimension.Buckets[bucketIndex].Codes)
                {
                    if (!map.TryGetValue(code, out var list))
                    {
                        list = [];
                        map[code] = list;
                    }
                    if (!list.Contains(bucketIndex))
                    {
                        list.Add(bucketIndex);
                    }
                }
            }
            result[dimensionIndex] = map;
        }
        return result;
    }

    private static void RenderDetail(StringBuilder html, InboundViewData view, TranslationCatalog catalog)
    {
        html.AppendLine("<section class=\"detail\">");
        html.AppendLine($"  <h2>{(view.View == InboundView.UsageOnly ? "使用明细" : "入库流水明细")}</h2>");
        html.AppendLine("  <div class=\"toolbar\">");
        html.AppendLine("    <input class=\"search\" data-table=\".detail-table\" type=\"search\" placeholder=\"搜索车牌 / 标题 / 片商 / 系列 / 演员 / 目录（日文原名也可）…\">");
        html.AppendLine($"    <span class=\"muted\">共 {view.Total} 笔，按时间倒序；点表头可排序。</span>");
        html.AppendLine("  </div>");
        html.AppendLine("  <div class=\"table-wrap detail-wrap\"><table class=\"detail-table\" data-sortable>");
        html.AppendLine("    <thead><tr>");
        foreach (var header in DetailHeaders)
        {
            html.AppendLine($"      <th data-sort=\"text\">{header}</th>");
        }
        html.AppendLine("    </tr></thead>");
        html.AppendLine("    <tbody>");
        foreach (var row in view.Rows)
        {
            var actors = catalog.TranslateList(TranslationKind.Actor, row.Actors);
            var features = catalog.TranslateList(TranslationKind.Feature, row.Features);
            var category = catalog.Translate(TranslationKind.Category, row.Category);
            var studio = catalog.Translate(TranslationKind.Studio, row.Studio);
            var series = catalog.Translate(TranslationKind.Series, row.Series);
            var originals = string.Join(' ', new[] { row.Category, row.Studio, row.Series }
                .Concat(row.Actors)
                .Concat(row.Features));
            var title = row.Title;
            html.AppendLine($"      <tr data-orig=\"{E(originals)}\">");
            html.AppendLine($"        <td data-value=\"{row.At:yyyy-MM-ddTHH:mm:ss}\" class=\"nowrap\">{row.At:yyyy-MM-dd HH:mm}</td>");
            html.AppendLine($"        <td><span class=\"tag\">{E(row.TypeLabel)}</span></td>");
            html.AppendLine($"        <td class=\"code\">{E(row.CarCode)}</td>");
            html.AppendLine($"        <td>{E(row.Prefix)}</td>");
            html.AppendLine($"        <td class=\"title\" title=\"{E(title)}\"><div>{E(title)}</div></td>");
            html.AppendLine($"        <td class=\"nowrap\">{(row.ReleaseDate is { } date ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "—")}</td>");
            html.AppendLine($"        <td>{E(category)}</td>");
            html.AppendLine($"        <td>{E(studio)}</td>");
            html.AppendLine($"        <td>{E(series)}</td>");
            html.AppendLine($"        <td class=\"multi\">{E(actors)}</td>");
            html.AppendLine($"        <td class=\"multi\">{E(features)}</td>");
            html.AppendLine($"        <td>{E(row.Source)}</td>");
            html.AppendLine($"        <td class=\"folder\" title=\"{E(row.Folder)}\"><div>{E(row.Folder)}</div></td>");
            html.AppendLine("      </tr>");
        }
        html.AppendLine("    </tbody>");
        html.AppendLine("  </table></div>");
        html.AppendLine("</section>");
    }

    private static void RenderFooter(StringBuilder html, InboundReport report, TranslationCatalog catalog)
    {
        html.AppendLine("<footer>");
        html.AppendLine("<h2>口径与说明</h2>");
        html.AppendLine("<ul>");
        html.AppendLine("<li>统计按车牌去重；同一车牌多次入库/使用只算一个车牌，笔数单独统计。</li>");
        html.AppendLine("<li>“入库”= 新入库（ADD）+ 转正入库（PROMOTE）+ 更新（UPDATE）；“使用”= 缓存命中（HIT）。</li>");
        html.AppendLine("<li>演员、特征属一对多：一个车牌多个演员/特征会分别计入，各分类合计大于车牌数属正常。</li>");
        html.AppendLine("<li>分类按页签切换；左侧选择分类项，右侧显示属于该项的车牌，「全部」显示当前视角的全部车牌。</li>");
        html.AppendLine("<li>类别按来源站点推断（有码/无码/素人/FC2），不是站点原始字段。</li>");
        html.AppendLine("<li>元数据取缓存库当前快照，不是入库当时的版本；缓存库里没有的车牌显示“元数据缺失”。</li>");
        html.AppendLine($"<li>片商/系列/演员/特征/类别的显示名来自 data/{TranslationCatalog.FileName}（本次 {catalog.Count} 条）；缺项显示原文，旧报表不会自动更新。</li>");
        html.AppendLine("<li>流水只来自文本日志；删除 data/logs 会丢失对应时段的历史。季度文件按起始月命名（01/04/07/10），旧缓存日志文件仍可读取。</li>");
        html.AppendLine($"<li>本次实际可用流水覆盖：{(report.LogFrom is { } from && report.LogTo is { } to ? $"{from:yyyy-MM-dd HH:mm} ~ {to:yyyy-MM-dd HH:mm}" : "无")}。</li>");
        html.AppendLine("<li>报表含真实车牌/标题/演员，仅本地查看。</li>");
        html.AppendLine("</ul>");
        html.AppendLine("</footer>");
    }

    private static TranslationCatalog LoadDefaultTranslations(string dbPath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(dbPath));
        return string.IsNullOrEmpty(directory)
            ? TranslationCatalog.Empty
            : TranslationCatalog.Load(TranslationCatalog.DefaultPathFor(directory));
    }

    /// <summary>分类维度 → 译名类别；月份/来源站/前缀等不需要翻译。</summary>
    private static TranslationKind? TranslationKindOf(InboundDimensionKind? kind) => kind switch
    {
        InboundDimensionKind.Studio => TranslationKind.Studio,
        InboundDimensionKind.Series => TranslationKind.Series,
        InboundDimensionKind.Actor => TranslationKind.Actor,
        InboundDimensionKind.Feature => TranslationKind.Feature,
        InboundDimensionKind.Category => TranslationKind.Category,
        _ => null,
    };

    private static string E(string? text) => WebUtility.HtmlEncode(text ?? "");

    private static readonly string[] DetailHeaders =
    [
        "时间", "类型", "车牌", "前缀", "标题", "发行日", "类别", "片商", "系列", "演员", "特征", "来源", "目录",
    ];

    private static readonly string[] CodeHeaders =
    [
        "车牌", "前缀", "标题", "发行日", "类别", "片商", "系列", "演员", "特征", "入库次数", "使用次数", "最近",
    ];

    private const string Css = """
        :root { color-scheme: light; }
        * { box-sizing: border-box; }
        body {
            margin: 0; padding: 0 0 40px; background: #f3f4f6; color: #1f2328;
            font-family: "Segoe UI", "Microsoft YaHei", "PingFang SC", sans-serif; font-size: 14px;
        }
        header { background: #fff; border-bottom: 1px solid #dfe3e8; padding: 20px 28px 16px; }
        h1 { margin: 0 0 10px; font-size: 24px; letter-spacing: .5px; }
        h2 { margin: 0 0 10px; font-size: 17px; }
        .meta { display: flex; flex-wrap: wrap; gap: 8px 22px; color: #4b5563; }
        .meta b { color: #111827; font-weight: 600; }
        .source { margin-top: 9px; color: #6b7280; font-size: 12px; word-break: break-all; }
        .warn {
            margin-top: 12px; padding: 9px 12px; border-radius: 6px;
            background: #fff7e6; border: 1px solid #f5d9a8; color: #8a5a00;
        }
        .switch {
            position: sticky; top: 0; z-index: 5; display: flex; gap: 8px; padding: 12px 28px;
            background: rgba(243,244,246,.96); backdrop-filter: blur(4px); border-bottom: 1px solid #e2e5e9;
        }
        .switch button {
            border: 1px solid #cfd5dc; background: #fff; color: #374151; padding: 7px 16px;
            border-radius: 18px; cursor: pointer; font-size: 13px;
        }
        .switch button.active { background: #1f6feb; border-color: #1f6feb; color: #fff; }
        main.view { padding: 16px 28px 0; }
        .cards { display: grid; grid-template-columns: repeat(auto-fit, minmax(112px, 1fr)); gap: 10px; margin-bottom: 14px; }
        .card { background: #fff; border: 1px solid #e2e6ea; border-radius: 8px; padding: 11px 13px; }
        .card b { display: block; font-size: 22px; line-height: 1.15; color: #0f172a; }
        .card span { color: #6b7280; font-size: 12px; }
        .hint { margin: -4px 0 12px; color: #6b7280; font-size: 12px; }
        .explorer {
            background: #fff; border: 1px solid #e2e6ea; border-radius: 8px;
            margin-bottom: 16px; overflow: hidden;
        }
        .dim-tabs {
            display: flex; flex-wrap: wrap; gap: 4px; padding: 8px 10px 0;
            background: #fafbfc; border-bottom: 1px solid #e2e6ea;
        }
        .dim-tabs button {
            border: 1px solid transparent; border-bottom: none; background: transparent;
            color: #4b5563; padding: 7px 12px; border-radius: 6px 6px 0 0;
            cursor: pointer; font-size: 13px;
        }
        .dim-tabs button:hover { color: #1f6feb; }
        .dim-tabs button.active {
            background: #fff; border-color: #e2e6ea; color: #1f6feb;
            font-weight: 600; margin-bottom: -1px;
        }
        .dim-tabs .count { margin-left: 6px; color: #9ca3af; font-weight: 400; font-size: 11px; }
        .explorer-body { display: flex; min-height: 420px; max-height: 640px; }
        .bucket-pane {
            width: 300px; flex: 0 0 300px; display: flex; flex-direction: column;
            border-right: 1px solid #eef1f4;
        }
        .bucket-head { padding: 10px 14px; color: #6b7280; font-size: 12px; flex: 0 0 auto; }
        .bucket-list { flex: 1; overflow: auto; padding: 0 8px 10px; }
        .bucket-list[hidden] { display: none; }
        .bucket-note { margin: 0 6px 6px; color: #9ca3af; font-size: 11.5px; }
        .bucket {
            display: block; width: 100%; text-align: left; cursor: pointer;
            border: 1px solid transparent; background: transparent; color: #1f2328;
            padding: 8px 10px; border-radius: 6px; font-size: 13px;
        }
        .bucket:hover { background: #f0f6ff; }
        .bucket.active { background: #eef4ff; border-color: #c9dcff; }
        .bucket .name { display: block; font-weight: 600; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
        .bucket .stat { display: flex; justify-content: space-between; gap: 8px; margin-top: 2px; color: #6b7280; font-size: 11px; }
        .bucket .mini { display: block; height: 4px; margin-top: 5px; background: #edf1f5; border-radius: 3px; overflow: hidden; }
        .bucket .mini i { display: block; height: 100%; background: linear-gradient(90deg, #6aa8ff, #1f6feb); border-radius: 3px; }
        .empty { margin: 6px 10px; color: #9ca3af; }
        .code-pane { flex: 1; min-width: 0; display: flex; flex-direction: column; }
        .code-head {
            display: flex; flex-wrap: wrap; align-items: center; gap: 12px;
            padding: 10px 14px; border-bottom: 1px solid #eef1f4; flex: 0 0 auto;
        }
        .code-head h2 { margin: 0; font-size: 15px; white-space: nowrap; }
        .code-head .sel { margin-left: 8px; color: #6b7280; font-weight: 400; font-size: 12px; }
        .code-pane .code-wrap { flex: 1; overflow: auto; }
        .table-wrap { overflow: auto; }
        table { border-collapse: collapse; width: 100%; }
        th, td { padding: 7px 10px; border-top: 1px solid #eef1f4; text-align: left; vertical-align: top; }
        thead th { position: sticky; top: 0; background: #fafbfc; color: #4b5563; font-weight: 600; font-size: 12px; border-bottom: 1px solid #e2e6ea; z-index: 1; }
        tbody tr:nth-child(even) { background: #fbfcfd; }
        tbody tr:hover { background: #f0f6ff; }
        .detail { background: #fff; border: 1px solid #e2e6ea; border-radius: 8px; padding: 14px; margin-bottom: 16px; }
        .toolbar { display: flex; flex-wrap: wrap; align-items: center; gap: 12px; margin-bottom: 10px; }
        .search { width: min(460px, 100%); padding: 7px 10px; border: 1px solid #cfd5dc; border-radius: 6px; font-size: 13px; }
        .muted { color: #6b7280; font-size: 12px; }
        .detail-wrap { max-height: 620px; }
        .bucket-codes td { font-size: 12.5px; }
        .bucket-codes td.code { font-weight: 700; white-space: nowrap; }
        .bucket-codes td.title div { max-width: 320px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
        .bucket-codes td.multi { max-width: 240px; }
        .bucket-codes .num { text-align: right; font-variant-numeric: tabular-nums; white-space: nowrap; }
        .bucket-codes th[data-sort] { cursor: pointer; user-select: none; white-space: nowrap; }
        .bucket-codes th[data-sort]:hover { color: #1f6feb; }
        .detail-table th[data-sort] { cursor: pointer; user-select: none; white-space: nowrap; }
        .detail-table th[data-sort]:hover { color: #1f6feb; }
        .detail-table td { font-size: 12.5px; }
        .detail-table td.code { font-weight: 700; white-space: nowrap; }
        .detail-table td.title div, .detail-table td.folder div { max-width: 320px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
        .detail-table td.multi { max-width: 260px; }
        .nowrap { white-space: nowrap; }
        .tag { display: inline-block; padding: 1px 7px; border-radius: 10px; background: #eef4ff; color: #1f4fa8; font-size: 11px; white-space: nowrap; }
        footer { margin: 8px 28px 0; padding: 16px 18px; background: #fff; border: 1px solid #e2e6ea; border-radius: 8px; color: #4b5563; }
        footer h2 { font-size: 15px; }
        footer ul { margin: 0; padding-left: 20px; }
        footer li { margin: 4px 0; font-size: 12.5px; }
        @media print {
            body { background: #fff; }
            .switch, .toolbar, .search, .mini { display: none; }
            main.view { padding: 0; }
            .explorer-body { display: block; max-height: none; }
            .bucket-pane { width: auto; border-right: none; border-bottom: 1px solid #eef1f4; }
            .bucket-list { max-height: none; overflow: visible; }
            .code-pane .code-wrap, .detail-wrap { max-height: none; overflow: visible; }
            .explorer, .detail { break-inside: avoid; }
        }
        """;

    private const string Script = """
        (function () {
            const buttons = Array.from(document.querySelectorAll('.switch button'));
            const views = Array.from(document.querySelectorAll('main.view'));
            buttons.forEach(function (button) {
                button.addEventListener('click', function () {
                    buttons.forEach(function (other) { other.classList.toggle('active', other === button); });
                    views.forEach(function (view) { view.hidden = view.dataset.view !== button.dataset.view; });
                    window.scrollTo({ top: 0, behavior: 'smooth' });
                });
            });

            views.forEach(function (view) {
                const table = view.querySelector('table.bucket-codes');
                const rows = table ? Array.from(table.tBodies[0].rows) : [];
                const tabs = Array.from(view.querySelectorAll('.dim-tab'));
                const lists = Array.from(view.querySelectorAll('.bucket-list'));
                const selection = view.querySelector('.code-head .sel');
                const codeSearch = view.querySelector('.code-head .search');
                const state = { dim: 0, bucket: -1, label: '全部' };

                function apply() {
                    const query = codeSearch && codeSearch.value ? codeSearch.value.trim().toLowerCase() : '';
                    let visible = 0;
                    rows.forEach(function (row) {
                        const members = (row.dataset['d' + state.dim] || '').split(' ').filter(Boolean);
                        const inBucket = state.bucket < 0 || members.indexOf(String(state.bucket)) >= 0;
                        const haystack = (row.textContent + ' ' + (row.dataset.orig || '')).toLowerCase();
                        const inQuery = query.length === 0 || haystack.indexOf(query) >= 0;
                        row.hidden = !(inBucket && inQuery);
                        if (!row.hidden) visible++;
                    });
                    if (selection) {
                        selection.textContent = state.label + '・' + visible + ' 个车牌';
                    }
                }

                function select(dim, index, button) {
                    state.dim = dim;
                    state.bucket = index;
                    state.label = button.dataset.label || '全部';
                    tabs.forEach(function (tab) {
                        tab.classList.toggle('active', Number(tab.dataset.dim) === dim);
                    });
                    lists.forEach(function (list) {
                        list.hidden = Number(list.dataset.dim) !== dim;
                    });
                    const list = lists[dim];
                    if (list) {
                        Array.from(list.querySelectorAll('.bucket')).forEach(function (item) {
                            item.classList.toggle('active', item === button);
                        });
                    }
                    apply();
                }

                function firstSelectable(list) {
                    if (!list) return null;
                    const buttons = Array.from(list.querySelectorAll('.bucket'));
                    return buttons.find(function (item) { return Number(item.dataset.index) >= 0; })
                        || buttons[0]
                        || null;
                }

                tabs.forEach(function (tab) {
                    tab.addEventListener('click', function () {
                        const dim = Number(tab.dataset.dim);
                        const first = firstSelectable(lists[dim]);
                        if (first) {
                            select(dim, Number(first.dataset.index), first);
                        } else {
                            state.dim = dim;
                            apply();
                        }
                    });
                });

                lists.forEach(function (list) {
                    Array.from(list.querySelectorAll('.bucket')).forEach(function (button) {
                        button.addEventListener('click', function () {
                            select(Number(list.dataset.dim), Number(button.dataset.index), button);
                        });
                    });
                });

                if (codeSearch) {
                    codeSearch.addEventListener('input', apply);
                }

                const firstBucket = lists.length > 0 ? firstSelectable(lists[0]) : null;
                if (firstBucket) {
                    select(0, Number(firstBucket.dataset.index), firstBucket);
                } else {
                    apply();
                }

                Array.from(view.querySelectorAll('input.search[data-table]')).forEach(function (input) {
                    const target = view.querySelector(input.dataset.table);
                    if (!target) return;
                    input.addEventListener('input', function () {
                        const query = input.value.trim().toLowerCase();
                        Array.from(target.tBodies[0].rows).forEach(function (row) {
                            const haystack = (row.textContent + ' ' + (row.dataset.orig || '')).toLowerCase();
                            row.hidden = query.length > 0 && haystack.indexOf(query) < 0;
                        });
                    });
                });

                Array.from(view.querySelectorAll('table[data-sortable]')).forEach(function (target) {
                    Array.from(target.querySelectorAll('th[data-sort]')).forEach(function (header, index) {
                        header.addEventListener('click', function () {
                            const body = target.tBodies[0];
                            const sorted = Array.from(body.rows);
                            const direction = header.dataset.dir === 'asc' ? -1 : 1;
                            target.querySelectorAll('th').forEach(function (other) { delete other.dataset.dir; });
                            header.dataset.dir = direction === 1 ? 'asc' : 'desc';
                            sorted.sort(function (left, right) {
                                const a = left.cells[index].dataset.value || left.cells[index].textContent;
                                const b = right.cells[index].dataset.value || right.cells[index].textContent;
                                return a.localeCompare(b, 'zh-CN', { numeric: true }) * direction;
                            });
                            sorted.forEach(function (row) { body.appendChild(row); });
                        });
                    });
                });
            });
        })();
        """;
}
