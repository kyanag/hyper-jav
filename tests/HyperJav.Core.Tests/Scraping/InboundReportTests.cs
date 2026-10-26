using HyperJav.Core.Localization;
using HyperJav.Core.Models;
using HyperJav.Core.Scraping;
using HyperJav.Core.Tests.Organizing; // TempDir

namespace HyperJav.Core.Tests.Scraping;

public class InboundReportTests
{
    private static readonly DateTime From = new(2026, 10, 1);
    private static readonly DateTime To = new(2026, 10, 31, 23, 59, 59);

    private static string Line(
        string at, string name, string code, string source = "-", string detail = "-", string folder = "-") =>
        $"{at} | {name} | {code} | {source} | {detail} | {folder}";

    private static string WriteLogs(string directory)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllLines(Path.Combine(directory, "入库流水-202610.log"),
        [
            Line("2026-10-01 09:00:00.000", CacheLogEvents.Add, "ABC-001", "Javlibrary", "metadata", "ABC-001"),
            Line("2026-10-02 09:01:00.000", CacheLogEvents.Hit, "ABC-001", "Javlibrary", "#1", "ABC-001"),
            Line("2026-10-03 09:02:00.000", CacheLogEvents.Add, "ABC-002", "Javbus有码", "metadata", "ABC-002"),
            Line("2026-10-04 09:03:00.000", CacheLogEvents.Hit, "ABC-002", "Javbus有码", "#1", "ABC-002"),
            Line("2026-10-05 09:04:00.000", CacheLogEvents.Add, "XYZ-003", "Jav321", "metadata", "XYZ-003"),
            Line("2026-10-06 09:05:00.000", CacheLogEvents.Promote, "ABC-001", "Javlibrary", "负转正", "ABC-001"),
            Line("2026-10-07 09:06:00.000", CacheLogEvents.Hit, "ABC-002", "Javbus有码", "#2", "ABC-002"),
            Line("2026-10-08 09:07:00.000", CacheLogEvents.Add, "MISS-999", "Javbus有码", "metadata", "MISS-999"),
            Line("2026-10-09 09:08:00.000", CacheLogEvents.Hit, "MISS-999", "Javbus有码", "#1", "MISS-999"),
            Line("2026-10-10 09:09:00.000", CacheLogEvents.NegativeHit, "NEG-888", "Javlibrary", "#1", "NEG-888"),
            Line("2026-10-11 09:10:00.000", CacheLogEvents.Error, "ABC-001", "-", "读缓存失败：IOException", "ABC-001"),
            "garbage line",
            "2026-10-12 09:11:00.000 | RUN-END | - | - | total=1 pos=0 ... | -",
        ]);

        // 旧版按天文件：报表解析要兼容。
        File.WriteAllLines(Path.Combine(directory, "缓存日志-20260930.log"),
        [
            Line("2026-09-30 23:00:00.000", CacheLogEvents.Add, "OLD-001", "Javlibrary", "metadata", "OLD-001"),
        ]);
        return directory;
    }

    private static string SeedCache(string dbPath)
    {
        using var cache = new ScrapeCache(dbPath);
        cache.Store(new MovieMetadata
        {
            CarCode = "ABC-001",
            Title = "标题A",
            Studio = "片商A",
            Series = "系列A",
            Actors = ["演员A", "演员B"],
            RawFeatures = ["标签A"],
            ReleaseDate = new DateOnly(2024, 1, 15),
            SourceSite = ScrapeSource.Javlibrary,
        });
        cache.Store(new MovieMetadata
        {
            CarCode = "ABC-002",
            Title = "标题<B>",
            Studio = "片商A",
            Series = "系列B",
            Actors = ["演员B"],
            RawFeatures = ["标签B"],
            ReleaseDate = new DateOnly(2024, 2, 20),
            SourceSite = ScrapeSource.JavbusYouma,
        });
        cache.Store(new MovieMetadata
        {
            CarCode = "XYZ-003",
            Title = "标题C <script>alert(1)</script>",
            Studio = "片商C",
            Actors = ["演员C"],
            SourceSite = ScrapeSource.Jav321,
        });
        return dbPath;
    }

    [Fact]
    public void Builds_three_views_with_distinct_code_counts()
    {
        using var temp = new TempDir($"inbound-views-{Guid.NewGuid():N}");
        var logs = WriteLogs(temp.Rel("logs"));
        var db = SeedCache(temp.Rel("scrape-cache.db"));

        var report = InboundReportBuilder.Build(logs, db, From, To);

        var inbound = report.View(InboundView.Inbound);
        Assert.Equal(4, inbound.DistinctCodes); // ABC-001/002、XYZ-003、MISS-999
        Assert.Equal(5, inbound.Total);         // 4 个 ADD + 1 个 PROMOTE
        Assert.Equal(4, inbound.AddCount);
        Assert.Equal(1, inbound.PromoteCount);
        Assert.Equal(0, inbound.HitCount);
        Assert.Equal(4, inbound.Codes.Count);
        var inboundAbc1 = Assert.Single(inbound.Codes, item => item.CarCode == "ABC-001");
        Assert.Equal(2, inboundAbc1.InboundCount);  // ADD + PROMOTE
        Assert.Equal(1, inboundAbc1.UsageCount);    // 汇总按区间全部流水，不受视角裁剪

        var all = report.View(InboundView.InboundAndUsage);
        Assert.Equal(9, all.Total);             // 再 + 4 个 HIT
        Assert.Equal(4, all.HitCount);
        var abc1 = Assert.Single(all.Codes, item => item.CarCode == "ABC-001");
        Assert.Equal(2, abc1.InboundCount);     // ADD + PROMOTE
        Assert.Equal(1, abc1.UsageCount);

        var usage = report.View(InboundView.UsageOnly);
        Assert.Equal(3, usage.DistinctCodes);   // ABC-001/002、MISS-999
        Assert.Equal(4, usage.Total);
        Assert.Equal(4, usage.HitCount);
        Assert.Equal(0, usage.AddCount);

        Assert.Equal(1, report.NegativeHits);
        Assert.Equal(1, report.Errors);
        Assert.Equal(2, report.LogFiles);
        Assert.Equal(new DateTime(2026, 9, 30, 23, 0, 0), report.LogFrom);
    }

    [Fact]
    public void Classifies_by_prefix_studio_series_actors_and_missing_metadata()
    {
        using var temp = new TempDir($"inbound-dims-{Guid.NewGuid():N}");
        var logs = WriteLogs(temp.Rel("logs"));
        var db = SeedCache(temp.Rel("scrape-cache.db"));
        var inbound = InboundReportBuilder.Build(logs, db, From, To).View(InboundView.Inbound);

        Assert.Equal(2, Bucket(inbound, "按车牌前缀", "ABC").CodeCount);
        Assert.Equal(1, Bucket(inbound, "按车牌前缀", "XYZ").CodeCount);
        Assert.Equal(1, Bucket(inbound, "按车牌前缀", "MISS").CodeCount);

        Assert.Equal(2, Bucket(inbound, "按片商", "片商A").CodeCount);
        Assert.Equal(1, Bucket(inbound, "按片商", "片商C").CodeCount);
        Assert.Equal(1, Bucket(inbound, "按片商", "元数据缺失").CodeCount);

        Assert.Equal(2, Bucket(inbound, "按演员", "演员B").CodeCount);
        Assert.Equal(1, Bucket(inbound, "按演员", "演员A").CodeCount);
        Assert.Equal(1, Bucket(inbound, "按演员", "元数据缺失").CodeCount);

        Assert.Equal(1, Bucket(inbound, "按系列", "系列A").CodeCount);
        Assert.Equal(1, Bucket(inbound, "按系列", "系列B").CodeCount);
        Assert.Equal(1, Bucket(inbound, "按系列", "未标注").CodeCount);
        Assert.Equal(1, Bucket(inbound, "按系列", "元数据缺失").CodeCount);

        Assert.Equal(3, Bucket(inbound, "按类别", "有码").CodeCount); // ABC-001/002 + MISS-999（来源站推断）
        Assert.Equal(1, Bucket(inbound, "按类别", "素人").CodeCount);

        Assert.Equal(1, Bucket(inbound, "按发行年月", "2024-01").CodeCount);
        Assert.Equal(1, Bucket(inbound, "按发行年月", "2024-02").CodeCount);
        Assert.Equal(1, Bucket(inbound, "按发行年月", "未知").CodeCount);
        Assert.Equal(1, Bucket(inbound, "按发行年月", "元数据缺失").CodeCount);

        Assert.Equal(4, Bucket(inbound, "按流水月份", "2026-10").CodeCount);
    }

    [Fact]
    public void Buckets_expose_full_code_lists_for_report_drilldown()
    {
        using var temp = new TempDir($"inbound-bucket-codes-{Guid.NewGuid():N}");
        var logs = WriteLogs(temp.Rel("logs"));
        var db = SeedCache(temp.Rel("scrape-cache.db"));
        var inbound = InboundReportBuilder.Build(logs, db, From, To).View(InboundView.Inbound);

        foreach (var dimension in inbound.Dimensions)
        {
            foreach (var bucket in dimension.Buckets)
            {
                Assert.Equal(bucket.CodeCount, bucket.Codes.Count);
                Assert.All(bucket.Codes, code => Assert.Contains(inbound.Codes, item => item.CarCode == code));
            }
        }
        Assert.Equal(["ABC-001", "ABC-002"], Bucket(inbound, "按车牌前缀", "ABC").Codes);
    }

    [Fact]
    public void Date_range_filters_out_older_flows()
    {
        using var temp = new TempDir($"inbound-range-{Guid.NewGuid():N}");
        var logs = WriteLogs(temp.Rel("logs"));
        var db = SeedCache(temp.Rel("scrape-cache.db"));

        var report = InboundReportBuilder.Build(logs, db, From, new DateTime(2026, 10, 3, 23, 59, 59));
        var inbound = report.View(InboundView.Inbound);
        Assert.Equal(2, inbound.DistinctCodes); // ABC-001（10-01）+ ABC-002（10-03）；HIT 不算入库
        Assert.Equal(2, inbound.Total);
        Assert.Equal(new DateTime(2026, 9, 30, 23, 0, 0), report.LogFrom); // 覆盖范围仍报全量
    }

    [Fact]
    public void Html_is_self_contained_with_three_views_and_escaped_values()
    {
        using var temp = new TempDir($"inbound-html-{Guid.NewGuid():N}");
        var logs = WriteLogs(temp.Rel("logs"));
        var db = SeedCache(temp.Rel("scrape-cache.db"));

        var result = InboundHtmlReportWriter.Generate(
            logs, db, From, To, outputDirectory: temp.Rel("reports"));

        Assert.True(File.Exists(result.Path));
        var html = File.ReadAllText(result.Path);
        Assert.Contains("<h1>入库明细报表</h1>", html);
        Assert.Contains("data-view=\"inbound\"", html);
        Assert.Contains("data-view=\"all\"", html);
        Assert.Contains("data-view=\"usage\"", html);
        Assert.Contains("按车牌前缀", html);
        Assert.Contains("按片商", html);
        Assert.Contains("按演员", html);
        Assert.Contains("class=\"dim-tab", html);
        Assert.Contains("class=\"bucket-list\"", html);
        Assert.Contains("class=\"bucket-codes\"", html);
        Assert.Contains("车牌明细", html);
        Assert.Contains("data-d0=", html);
        Assert.Contains("ABC-001", html);
        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
        Assert.Contains("标题&lt;B&gt;", html);
        Assert.Contains("统计区间", html);
        Assert.Contains("口径与说明", html);
    }

    [Fact]
    public void Html_applies_display_translations_and_keeps_originals_for_search()
    {
        using var temp = new TempDir($"inbound-html-lang-{Guid.NewGuid():N}");
        var logs = WriteLogs(temp.Rel("logs"));
        var db = SeedCache(temp.Rel("scrape-cache.db"));
        var languagePath = temp.Rel("translations.json");
        File.WriteAllText(languagePath, """
            {
              "片商": { "片商A": "片商甲" },
              "系列": { "系列B": "系列乙" },
              "演员": { "演员B": "演员乙" },
              "特征": { "标签A": "标签甲" },
              "类别": { "有码": "有码（推断）" }
            }
            """);

        var catalog = TranslationCatalog.Load(languagePath);
        Assert.Null(catalog.LoadError);
        var report = InboundReportBuilder.Build(logs, db, From, To);
        var html = InboundHtmlReportWriter.Render(report, logs, db, catalog);

        Assert.Contains("片商甲", html);
        Assert.Contains("系列乙", html);
        Assert.Contains("演员乙", html);
        Assert.Contains("标签甲", html);
        Assert.Contains("有码（推断）", html);
        Assert.Contains("data-orig=\"", html);
        Assert.Contains("演员B", html); // 原文保留在 data-orig，搜索仍可命中日文原名。
        Assert.Contains("译名", html);
        Assert.Contains("translations.json", html);
    }

    [Fact]
    public void Generate_loads_translations_json_next_to_cache_database()
    {
        using var temp = new TempDir($"inbound-html-default-lang-{Guid.NewGuid():N}");
        var logs = WriteLogs(temp.Rel("logs"));
        var db = SeedCache(temp.Rel("scrape-cache.db"));
        File.WriteAllText(
            Path.Combine(Path.GetDirectoryName(db)!, TranslationCatalog.FileName),
            """{ "片商": { "片商A": "片商甲" } }""");

        var result = InboundHtmlReportWriter.Generate(
            logs, db, From, To, outputDirectory: temp.Rel("reports"));

        Assert.Contains("片商甲", File.ReadAllText(result.Path));
    }

    [Theory]
    [InlineData("ABC-123", "ABC")]
    [InlineData("abc-001", "ABC")]
    [InlineData("259LUXU-1886", "LUXU")]
    [InlineData("FC2-PPV-1234567", "FC2")]
    [InlineData("T28-123", "T28")]
    [InlineData("1234", "其他")]
    [InlineData("", "其他")]
    public void Prefix_extraction_handles_real_world_shapes(string code, string expected) =>
        Assert.Equal(expected, InboundReportBuilder.PrefixOf(code));

    [Theory]
    [InlineData("Javbus有码", "有码")]
    [InlineData("Javlibrary", "有码")]
    [InlineData("Javbus无码", "无码")]
    [InlineData("Jav321", "素人")]
    [InlineData("JavDb FC2", "FC2")]
    [InlineData("未知站点", "未知")]
    public void Category_is_inferred_from_source_label(string label, string expected) =>
        Assert.Equal(expected, InboundReportBuilder.CategoryOf(label));

    private static InboundBucket Bucket(InboundViewData view, string dimensionTitle, string name)
    {
        var dimension = Assert.Single(view.Dimensions, item => item.Title == dimensionTitle);
        return Assert.Single(dimension.Buckets, bucket => bucket.Name == name);
    }
}
