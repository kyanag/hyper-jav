using System.Globalization;
using HyperJav.Core.Configuration;
using HyperJav.Core.Models;
using HyperJav.Core.Normalization;

namespace HyperJav.Core.Scraping;

/// <summary>报表视角（入库 / 入库+使用 / 仅使用）。</summary>
public enum InboundView
{
    /// <summary>入库流水：ADD + PROMOTE + UPDATE。</summary>
    Inbound,

    /// <summary>入库流水 + 缓存使用（HIT）。</summary>
    InboundAndUsage,

    /// <summary>仅缓存使用（HIT）。</summary>
    UsageOnly,
}

/// <summary>明细一行（一笔流水 + 当前库存元数据快照）。</summary>
public sealed record InboundRow(
    DateTime At,
    ScrapeLogKind Kind,
    string CarCode,
    string Prefix,
    string Title,
    DateOnly? ReleaseDate,
    string Category,
    string Studio,
    string Series,
    IReadOnlyList<string> Actors,
    IReadOnlyList<string> Features,
    string Source,
    string Folder,
    bool MetadataMissing)
{
    public string TypeLabel => Kind switch
    {
        ScrapeLogKind.Add => "新入库",
        ScrapeLogKind.Promote => "转正入库",
        ScrapeLogKind.Update => "更新",
        ScrapeLogKind.Hit => "使用（缓存命中）",
        _ => "其他",
    };
}

/// <summary>分类桶：名称 + 去重车牌数 + 占比 + 全部车牌（供报表右侧展开明细）。</summary>
public sealed record InboundBucket(
    string Name,
    int CodeCount,
    double Share,
    IReadOnlyList<string> Codes);

/// <summary>分类维度对应的字段（用于显示译名；换月份/来源站等不需要译名）。</summary>
public enum InboundDimensionKind
{
    Prefix,
    Studio,
    Series,
    Actor,
    Feature,
    Category,
    Source,
    ReleaseMonth,
    FlowMonth,
}

/// <summary>一个分类维度。</summary>
public sealed record InboundDimension(
    string Title,
    string? Note,
    IReadOnlyList<InboundBucket> Buckets,
    bool SortByName = false,
    InboundDimensionKind? Kind = null);

/// <summary>单个视角的完整数据。</summary>
public sealed record InboundViewData(
    InboundView View,
    string Title,
    IReadOnlyList<InboundRow> Rows,
    int DistinctCodes,
    int AddCount,
    int PromoteCount,
    int UpdateCount,
    int HitCount,
    IReadOnlyList<InboundDimension> Dimensions,
    IReadOnlyList<InboundCodeSummary> Codes)
{
    public int Total => Rows.Count;

    public int StudioCount => CountDistinct(Rows.Select(row => row.Studio));

    public int SeriesCount => CountDistinct(Rows.Select(row => row.Series));

    public int ActorCount => CountDistinct(Rows.SelectMany(row => row.Actors));

    private static int CountDistinct(IEnumerable<string> values) =>
        values.Where(value => value is not "元数据缺失" and not "未知" and not "未标注")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
}

/// <summary>按车牌去重汇总（回答“哪些车牌”，并带入库/使用次数）。</summary>
public sealed record InboundCodeSummary(
    string CarCode,
    string Prefix,
    string Title,
    DateOnly? ReleaseDate,
    string Category,
    string Studio,
    string Series,
    IReadOnlyList<string> Actors,
    IReadOnlyList<string> Features,
    int InboundCount,
    int UsageCount,
    DateTime LastAt);

/// <summary>整份入库明细报表。</summary>
public sealed record InboundReport(
    DateTime GeneratedAt,
    DateTime From,
    DateTime To,
    IReadOnlyList<InboundViewData> Views,
    ScrapeCacheStats? Inventory,
    int NegativeHits,
    int Errors,
    DateTime? LogFrom,
    DateTime? LogTo,
    int LogFiles,
    bool MetadataAvailable)
{
    public InboundViewData View(InboundView view) => Views.First(item => item.View == view);
}

/// <summary>
/// 入库流水 → 统计聚合。流水来自文本日志，元数据来自缓存库当前快照（同车牌只留一份）。
/// 统计按车牌去重：同一车牌多次入库/使用只算一个车牌，笔数单独计。
/// </summary>
public static class InboundReportBuilder
{
    public static InboundReport Build(
        string logDirectory,
        string dbPath,
        DateTime from,
        DateTime to,
        FeatureDictionary? dictionary = null,
        ScriptMode script = ScriptMode.Simplified)
    {
        var logs = ScrapeLogReader.ReadAll(logDirectory);
        var events = logs.InRange(from, to);

        var metadata = new Dictionary<string, MovieMetadata>(StringComparer.OrdinalIgnoreCase);
        ScrapeCacheStats? inventory = null;
        var metadataAvailable = false;
        if (File.Exists(dbPath))
        {
            try
            {
                using var cache = new ScrapeCache(dbPath);
                foreach (var (code, value) in cache.ReadMetadata())
                {
                    metadata[code] = value;
                }
                inventory = cache.ReadStats();
                metadataAvailable = true;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException
                or Microsoft.Data.Sqlite.SqliteException or InvalidOperationException)
            {
                // 库损坏/被占用 → 报表仍出，只是元数据缺失。
            }
        }

        var normalizer = new FeatureNormalizer(dictionary, script);
        var views = new[] { InboundView.Inbound, InboundView.InboundAndUsage, InboundView.UsageOnly }
            .Select(view => BuildView(view, events, metadata, normalizer))
            .ToArray();

        return new InboundReport(
            DateTime.Now, from, to, views, inventory,
            events.Count(item => item.Kind == ScrapeLogKind.NegativeHit),
            events.Count(item => item.Kind == ScrapeLogKind.Error),
            logs.From, logs.To, logs.Files, metadataAvailable);
    }

    private static InboundViewData BuildView(
        InboundView view,
        IReadOnlyList<ScrapeLogEvent> events,
        IReadOnlyDictionary<string, MovieMetadata> metadata,
        FeatureNormalizer normalizer)
    {
        var selected = events
            .Where(item => IsIncluded(view, item.Kind))
            .OrderByDescending(item => item.At)
            .ToArray();
        var rows = selected.Select(item => ToRow(item, metadata, normalizer)).ToArray();
        var distinctCodes = rows.Select(row => row.CarCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        return new InboundViewData(
            view,
            TitleOf(view),
            rows,
            distinctCodes,
            rows.Count(row => row.Kind == ScrapeLogKind.Add),
            rows.Count(row => row.Kind == ScrapeLogKind.Promote),
            rows.Count(row => row.Kind == ScrapeLogKind.Update),
            rows.Count(row => row.Kind == ScrapeLogKind.Hit),
            BuildDimensions(view, rows, distinctCodes),
            BuildCodeSummaries(rows, events));
    }

    /// <summary>
    /// 去重车牌汇总。次数统计用区间内**全部流水**（不只当前视角），
    /// 这样“入库”视角也能看到该车牌被使用了多少次，而不是误导性的 0。
    /// </summary>
    private static IReadOnlyList<InboundCodeSummary> BuildCodeSummaries(
        IReadOnlyList<InboundRow> rows,
        IReadOnlyList<ScrapeLogEvent> allEvents)
    {
        var flows = allEvents
            .GroupBy(item => item.CarCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.ToArray(),
                StringComparer.OrdinalIgnoreCase);

        return
        [
            .. rows
                .GroupBy(row => row.CarCode, StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    var latest = group.OrderByDescending(row => row.At).First();
                    var codeFlows = flows.TryGetValue(latest.CarCode, out var values) ? values : [];
                    return new InboundCodeSummary(
                        latest.CarCode,
                        latest.Prefix,
                        latest.Title,
                        latest.ReleaseDate,
                        latest.Category,
                        latest.Studio,
                        latest.Series,
                        latest.Actors,
                        latest.Features,
                        codeFlows.Count(row => row.Kind is ScrapeLogKind.Add
                            or ScrapeLogKind.Promote or ScrapeLogKind.Update),
                        codeFlows.Count(row => row.Kind == ScrapeLogKind.Hit),
                        codeFlows.Length == 0 ? latest.At : codeFlows.Max(row => row.At));
                })
                .OrderByDescending(item => item.LastAt)
                .ThenBy(item => item.CarCode, StringComparer.OrdinalIgnoreCase),
        ];
    }

    private static bool IsIncluded(InboundView view, ScrapeLogKind kind) => view switch
    {
        InboundView.Inbound => kind is ScrapeLogKind.Add or ScrapeLogKind.Promote or ScrapeLogKind.Update,
        InboundView.InboundAndUsage => kind is ScrapeLogKind.Add or ScrapeLogKind.Promote
            or ScrapeLogKind.Update or ScrapeLogKind.Hit,
        _ => kind is ScrapeLogKind.Hit,
    };

    private static string TitleOf(InboundView view) => view switch
    {
        InboundView.Inbound => "入库",
        InboundView.InboundAndUsage => "入库 + 使用",
        _ => "仅缓存使用",
    };

    private static InboundRow ToRow(
        ScrapeLogEvent item,
        IReadOnlyDictionary<string, MovieMetadata> metadata,
        FeatureNormalizer normalizer)
    {
        metadata.TryGetValue(item.CarCode, out var data);
        var missing = data is null;
        var source = data is not null
            ? ScrapeProbeResult.LabelOf(data.SourceSite)
            : item.Source ?? "未知";
        var features = data is null
            ? (IReadOnlyList<string>)["元数据缺失"]
            : data.RawFeatures.Count == 0
                ? ["未标注"]
                : normalizer.Normalize(data.RawFeatures, data.SourceSite).Values is { Count: > 0 } normalized
                    ? normalized
                    : ["未标注"];

        return new InboundRow(
            item.At,
            item.Kind,
            item.CarCode,
            PrefixOf(item.CarCode),
            data?.Title is { Length: > 0 } title ? title : missing ? "（元数据缺失）" : "（无标题）",
            data?.ReleaseDate,
            CategoryOf(source),
            Field(data?.Studio, missing, "未知"),
            Field(data?.Series, missing, "未标注"),
            data is null ? ["元数据缺失"] : data.Actors.Count > 0 ? data.Actors : ["未标注"],
            features,
            source,
            item.Folder ?? "-",
            missing);
    }

    /// <summary>空字段的占位：元数据整体缺失 → “元数据缺失”；有元数据但字段空 → 各自占位。</summary>
    private static string Field(string? value, bool metadataMissing, string emptyLabel) =>
        string.IsNullOrWhiteSpace(value) ? (metadataMissing ? "元数据缺失" : emptyLabel) : value.Trim();

    /// <summary>车牌前缀：第一个 - 前的字母数字段，去前导数字；无 - 时取前导字母。</summary>
    public static string PrefixOf(string carCode)
    {
        if (string.IsNullOrWhiteSpace(carCode))
        {
            return "其他";
        }
        var text = carCode.Trim().ToUpperInvariant();
        var dash = text.IndexOf('-');
        var head = dash > 0
            ? text[..dash]
            : new string(text.TakeWhile(character => !char.IsDigit(character)).ToArray());
        head = new string(head.SkipWhile(char.IsDigit).ToArray()).Trim();
        return head.Length == 0 ? "其他" : head;
    }

    /// <summary>类别（按来源站点推断；站点原始字段没有类别）。</summary>
    public static string CategoryOf(string sourceLabel) => sourceLabel switch
    {
        "Javbus有码" or "Javlibrary" => "有码",
        "Javbus无码" => "无码",
        "Jav321" => "素人",
        "JavDb FC2" => "FC2",
        _ => "未知",
    };

    private static IReadOnlyList<InboundDimension> BuildDimensions(
        InboundView view,
        IReadOnlyList<InboundRow> rows,
        int distinctCodes)
    {
        var monthsTitle = view == InboundView.UsageOnly ? "按使用月份" : "按流水月份";
        return
        [
            Single("按车牌前缀", "按规范化车牌的字母段归类。", rows, distinctCodes,
                row => [row.Prefix], kind: InboundDimensionKind.Prefix),
            Single("按片商", null, rows, distinctCodes, row => [row.Studio],
                kind: InboundDimensionKind.Studio),
            Single("按系列", null, rows, distinctCodes, row => [row.Series],
                kind: InboundDimensionKind.Series),
            Single("按演员", "一个车牌多个演员会分别计入，合计大于车牌数属正常。", rows, distinctCodes,
                row => row.Actors, kind: InboundDimensionKind.Actor),
            Single("按特征/类型", "一个车牌多个特征会分别计入；有特征词典时用规范化名称。", rows, distinctCodes,
                row => row.Features, kind: InboundDimensionKind.Feature),
            Single("按类别", "类别按来源站点推断。", rows, distinctCodes, row => [row.Category],
                kind: InboundDimensionKind.Category),
            Single("按来源站点", null, rows, distinctCodes, row => [row.Source],
                kind: InboundDimensionKind.Source),
            Single("按发行年月", "按发行日期归月；元数据缺失单列。", rows, distinctCodes,
                row => [row.ReleaseDate is { } date ? date.ToString("yyyy-MM", CultureInfo.InvariantCulture)
                    : row.MetadataMissing ? "元数据缺失" : "未知"],
                sortByName: true, kind: InboundDimensionKind.ReleaseMonth),
            Single(monthsTitle, "按流水时间归月（不是发行日期）。", rows, distinctCodes,
                row => [row.At.ToString("yyyy-MM", CultureInfo.InvariantCulture)],
                sortByName: true, kind: InboundDimensionKind.FlowMonth),
        ];
    }

    private static InboundDimension Single(
        string title,
        string? note,
        IReadOnlyList<InboundRow> rows,
        int distinctCodes,
        Func<InboundRow, IEnumerable<string>> selector,
        bool sortByName = false,
        InboundDimensionKind? kind = null)
    {
        var groups = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            foreach (var name in selector(row)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!groups.TryGetValue(name, out var codes))
                {
                    codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    groups[name] = codes;
                }
                codes.Add(row.CarCode);
            }
        }

        var buckets = groups
            .Select(pair =>
            {
                var codes = pair.Value.OrderBy(code => code, StringComparer.OrdinalIgnoreCase).ToArray();
                return new InboundBucket(
                    pair.Key,
                    codes.Length,
                    distinctCodes == 0 ? 0 : codes.Length * 100.0 / distinctCodes,
                    codes);
            })
            .ToList();
        buckets = sortByName
            ? [.. buckets.OrderByDescending(bucket => bucket.Name, StringComparer.Ordinal)]
            : [.. buckets.OrderByDescending(bucket => bucket.CodeCount)
                .ThenBy(bucket => bucket.Name, StringComparer.OrdinalIgnoreCase)];

        return new InboundDimension(title, note, buckets, sortByName, kind);
    }
}
