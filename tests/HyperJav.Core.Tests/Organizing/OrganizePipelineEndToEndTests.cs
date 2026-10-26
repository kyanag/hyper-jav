using HyperJav.Core.Configuration;
using HyperJav.Core.Models;
using HyperJav.Core.Organizing;
using HyperJav.Core.Parsing;
using HyperJav.Core.Scraping;
using HyperJav.Core.Scraping.Scrapers;
using HyperJav.Core.Tests.Scraping;

namespace HyperJav.Core.Tests.Organizing;

/// <summary>
/// 整理管线端到端（假件注入，不联网）。刮削样本为中性占位 HTML（javlibrary-*-*.html）。
/// 网络层换成 FakePageFetcher，封面下载换成 FakeCoverDownloader——只验文件系统落位与清单输出。
/// </summary>
public sealed class OrganizePipelineEndToEndTests
{
    private const string Lib = "https://www.example-lib.com";
    private const string PosterUrl = "https://pics.example/abp123pl.jpg";
    private const string UnknownCode = "XYZ-999";

    private static readonly byte[] PosterBytes = [1, 2, 3, 4, 5];

    private static FakePageFetcher JavlibraryFetcher()
    {
        var search = TestFixtures.ScrapingAsync("javlibrary-search.html");
        var detail = TestFixtures.ScrapingAsync("javlibrary-detail.html");
        return new FakePageFetcher(new Dictionary<string, string>
        {
            [$"{Lib}/search.php?q=ABP-123&type=0&parent=ce"] = search.Result,
            [$"{Lib}/?v=javme0"] = detail.Result,
            [$"{Lib}/search.php?q={UnknownCode}&type=0&parent=ce"] = "<html><body><h1>无结果</h1></body></html>",
        });
    }

    private static OrganizeEnvironment Environment(
        OrganizeOptions options,
        FakePageFetcher? fetcher = null,
        ICoverDownloader? downloader = null,
        IPosterBannerRenderer? renderer = null,
        ScrapeCache? cache = null,
        ScrapeCacheLog? cacheLog = null,
        string? checklistDirectory = null)
        => new(
            options,
            source => source switch
            {
                ScrapeSource.Javlibrary => new JavlibraryScraper(fetcher ?? JavlibraryFetcher(), Lib, collectHotComments: false),
                _ => new NeverFoundScraper(source),
            },
            downloader ?? new FakeCoverDownloader(new Dictionary<string, byte[]> { [PosterUrl] = PosterBytes }),
            renderer ?? new RecordingBannerRenderer(),
            cache,
            cacheLog,
            checklistDirectory ?? NewChecklistDirectory());

    private static string NewChecklistDirectory() =>
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "hyperjav-tests", "checklists", Guid.NewGuid().ToString("N"));

    private static OrganizeOptions Options() => new()
    {
        Codes = CarCodeOptions.Default(new HashSet<string>(StringComparer.OrdinalIgnoreCase)),
        VideoExtensions = ["mp4"],
        NfoTitleTemplate = "车牌+空格+标题",
        RenameTemplate = "车牌+空格+标题",
        FolderTemplate = "车牌+【+全部演员+】",
        PosterTemplate = "视频+-poster.jpg",
        FanartTemplate = "视频+-fanart.jpg",
        MaxTitleLength = null,
        SkipMoviesWithSubtitles = false, // 默认关闭短路，个别用例 with 打开。
    };

    private static async Task<OrganizeReport> RunAsync(OrganizeEnvironment environment, string root)
        => await new OrganizePipeline(environment).RunAsync(root);

    [Fact]
    public void Defaults_place_checklist_under_program_data_directory()
    {
        using var temp = new TempDir($"checklist-default-{Guid.NewGuid():N}");
        using var environment = OrganizeDefaults.Create(new AppConfig(), dataDirectory: temp.Root);

        Assert.Equal(
            System.IO.Path.Combine(temp.Root, TextOutputs.ChecklistDirectoryName),
            environment.ChecklistDirectory);
    }

    [Fact]
    public async Task First_source_network_failure_falls_back_to_next()
    {
        using var temp = new TempDir("fallback");
        var movieFolder = temp.Rel("库/[ABP-123]");
        File.WriteAllBytes(System.IO.Path.Combine(movieFolder, "ABP-123.mp4"), [0x01]);
        var root = temp.Rel("库");

        // Javlibrary 假站抛网络错误 → 回退 Javbus 假站（返回元数据）。
        var javbusCalls = 0;
        var durable = Options() with { CollectNfo = false, CoverEnabled = false, RenameEnabled = false, FolderEnabled = false };
        var environment = new OrganizeEnvironment(
            durable,
            source => source switch
            {
                ScrapeSource.Javlibrary => new ThrowingScraper(ScrapeSource.Javlibrary),
                ScrapeSource.JavbusYouma => new SuccessScraper(() => javbusCalls++, "javbus 回退成功"),
                _ => new NeverFoundScraper(source),
            },
            new NullCoverDownloader(),
            new RecordingBannerRenderer(),
            ChecklistDirectory: System.IO.Path.Combine(temp.Root, "清单"));

        var report = await new OrganizePipeline(environment).RunAsync(root);

        var outcome = Assert.Single(report.Outcomes);
        Assert.Equal(OrganizeStatus.Success, outcome.Status);
        Assert.Equal(1, javbusCalls);
    }

    [Fact]
    public async Task Forced_source_is_used_without_fallback()
    {
        using var temp = new TempDir("forced");
        var movieFolder = temp.Rel("库/[ABP-123]");
        File.WriteAllBytes(System.IO.Path.Combine(movieFolder, "ABP-123.mp4"), [0x01]);
        var root = temp.Rel("库");

        // 强制 JavBus：即使 Javlibrary（默认首站）可用也不该被调用。
        var javlibraryCalls = 0;
        var javbusCalls = 0;
        var durable = Options() with { CollectNfo = false, CoverEnabled = false, RenameEnabled = false, FolderEnabled = false, ForcedSource = ScrapeSource.JavbusYouma };
        var environment = new OrganizeEnvironment(
            durable,
            source => source switch
            {
                ScrapeSource.Javlibrary => new SuccessScraper(() => javlibraryCalls++, "不应被调用"),
                ScrapeSource.JavbusYouma => new SuccessScraper(() => javbusCalls++, "javbus 强制成功"),
                _ => new NeverFoundScraper(source),
            },
            new NullCoverDownloader(),
            new RecordingBannerRenderer(),
            ChecklistDirectory: System.IO.Path.Combine(temp.Root, "清单"));

        var report = await new OrganizePipeline(environment).RunAsync(root);

        var outcome = Assert.Single(report.Outcomes);
        Assert.Equal(OrganizeStatus.Success, outcome.Status);
        Assert.Equal(0, javlibraryCalls);
        Assert.Equal(1, javbusCalls);
    }

    /// <summary>每次抓取都抛网络错误的站点假件。</summary>
    private sealed class ThrowingScraper(ScrapeSource source) : IScraper
    {
        public ScrapeSource Source { get; } = source;

        public IReadOnlyList<CarCategory> Categories { get; } = [CarCategory.Youma];

        public IReadOnlyList<string> Requests { get; } = [];

        public Task<MovieMetadata?> ScrapeAsync(string carCode, CancellationToken cancellationToken = default) =>
            throw new ScrapeException("网络错误（模拟）");
    }

    /// <summary>计数 + 恒返回元数据的站点假件（本次仅注册在 Javbus 有码槽位）。</summary>
    private sealed class SuccessScraper(Action counter, string title) : IScraper
    {
        public ScrapeSource Source { get; } = ScrapeSource.JavbusYouma;

        public IReadOnlyList<CarCategory> Categories { get; } = [CarCategory.Youma];

        public IReadOnlyList<string> Requests { get; } = [];

        public Task<MovieMetadata?> ScrapeAsync(string carCode, CancellationToken cancellationToken = default)
        {
            counter();
            MovieMetadata? metadata = new()
            {
                CarCode = carCode,
                Title = title,
                ReleaseDate = new DateOnly(2015, 1, 1),
            };
            return Task.FromResult<MovieMetadata?>(metadata);
        }
    }

    [Fact]
    public async Task Full_flow_renames_video_subtitle_folder_and_writes_nfo_covers_checklist()
    {
        using var temp = new TempDir("full-flow");
        var movieFolder = temp.Rel("库/ABP-123 示例");
        File.WriteAllBytes(System.IO.Path.Combine(movieFolder, "ABP-123.mp4"), [0xFF]);
        File.WriteAllText(System.IO.Path.Combine(movieFolder, "ABP-123.chs.srt"), "示例字幕");
        var root = temp.Rel("库");

        var report = await RunAsync(Environment(Options()), root);
        var outcome = Assert.Single(report.Outcomes, o => o.CarCode == "ABP-123" && !o.OriginalPath.EndsWith(".srt", StringComparison.Ordinal));

        Assert.Equal(OrganizeStatus.Success, outcome.Status);
        var finalFolder = System.IO.Path.Combine(root, "ABP-123【演员甲、演员乙】");
        Assert.Equal(finalFolder, outcome.NewFolder);

        // 视频 + 字幕重命名（保留 .chs 后缀）。
        Assert.True(File.Exists(System.IO.Path.Combine(finalFolder, "ABP-123 示例标题.mp4")));
        Assert.True(File.Exists(System.IO.Path.Combine(finalFolder, "ABP-123 示例标题.chs.srt")));

        // NFO（title 不截断；UTF-8 无 BOM）。
        var nfoText = await File.ReadAllTextAsync(System.IO.Path.Combine(finalFolder, "ABP-123 示例标题.nfo"));
        Assert.Contains("<title>ABP-123 示例标题</title>", nfoText);
        Assert.Contains("演员甲", nfoText);

        // 封面（海报 + 远图同名兜底，均落盘）。
        Assert.True(File.Exists(System.IO.Path.Combine(finalFolder, "ABP-123 示例标题-poster.jpg")));
        Assert.True(File.Exists(System.IO.Path.Combine(finalFolder, "ABP-123 示例标题-fanart.jpg")));

        // 无字典告警 → 不写警告信息。
        Assert.False(File.Exists(System.IO.Path.Combine(finalFolder, "警告信息.txt")));

        // 清单（UTF-8 带 BOM；写到独立目录，不写目标目录）。
        var checklist = report.ChecklistPath;
        Assert.NotNull(checklist);
        Assert.True(File.Exists(checklist));
        Assert.False(File.Exists(System.IO.Path.Combine(root, "整理清单.txt")));
        var text = File.ReadAllText(checklist);
        Assert.Contains("[OK]", text);
        Assert.Contains("ABP-123", text);
    }

    [Fact]
    public async Task SkipFoldersWithNfo_untouched()
    {
        using var temp = new TempDir("skip-nfo");
        var movieFolder = temp.Rel("库/ABP-123");
        File.WriteAllBytes(System.IO.Path.Combine(movieFolder, "ABP-123.mp4"), [0xFF]);
        File.WriteAllText(System.IO.Path.Combine(movieFolder, "existing.nfo"), "<movie/>");
        var root = temp.Rel("库");

        var report = await RunAsync(Environment(Options() with { SkipFoldersWithNfo = true }), root);

        var outcome = Assert.Single(report.Outcomes, o => o.CarCode is null);
        Assert.Equal(OrganizeStatus.Skipped, outcome.Status);
        Assert.Contains("已存在 nfo", outcome.Message);
        Assert.True(File.Exists(System.IO.Path.Combine(movieFolder, "ABP-123.mp4"))); // 原名未动
        Assert.Equal(2, Directory.GetFiles(movieFolder).Length); // 只有视频 + 已有 nfo，无新增产物。
    }

    [Fact]
    public async Task Unknown_car_code_is_skipped_without_requests()
    {
        using var temp = new TempDir("unknown");
        File.WriteAllBytes(temp.Rel("库/示例杂片.mp4"), [0x01]);
        var root = temp.Rel("库");
        var fetcher = JavlibraryFetcher();

        var report = await RunAsync(Environment(Options(), fetcher), root);

        var outcome = Assert.Single(report.Outcomes, o => o.CarCode is null && o.Status == OrganizeStatus.Skipped);
        Assert.Contains("未识别车牌", outcome.Message);
        Assert.True(File.Exists(temp.Rel("库/示例杂片.mp4")));
        Assert.Empty(fetcher.Requests); // 未刮削。
    }

    [Fact]
    public async Task Not_indexed_code_keeps_original_name()
    {
        using var temp = new TempDir("not-indexed");
        File.WriteAllBytes(temp.Rel("库/XYZ-999.mp4"), [0x01]);
        var root = temp.Rel("库");

        var report = await RunAsync(Environment(Options()), root);

        var outcome = Assert.Single(report.Outcomes, o => o.CarCode == UnknownCode);
        Assert.Equal(OrganizeStatus.Skipped, outcome.Status);
        Assert.Contains("未收录该车牌", outcome.Message);
        // 尝试明细里带站点名与访问过的 URL（诊断用）。
        Assert.Contains("Javlibrary：未收录", outcome.Message);
        Assert.Contains("    https://", outcome.Message);
        Assert.True(File.Exists(temp.Rel("库/XYZ-999.mp4")));
    }

    [Fact]
    public async Task Multiple_codes_in_folder_disable_folder_level_ops()
    {
        using var temp = new TempDir("multi-code");
        var movieFolder = temp.Rel("库/杂夹");
        File.WriteAllBytes(System.IO.Path.Combine(movieFolder, "ABP-123.mp4"), [0x01]);
        File.WriteAllBytes(System.IO.Path.Combine(movieFolder, "XYZ-999.mp4"), [0x02]);
        var root = temp.Rel("库");

        var report = await RunAsync(Environment(Options()), root);

        // 夹里多个不同车牌 → 不动夹（p3-notes 决策 1）；但逐文件操作照常：
        // ABP-123 正常改名产出，XYZ-999 未收录被跳过。
        var abp = Assert.Single(report.Outcomes, o => o.CarCode == "ABP-123");
        var xyz = Assert.Single(report.Outcomes, o => o.CarCode == UnknownCode);
        Assert.Equal(OrganizeStatus.Success, abp.Status);
        Assert.Equal(OrganizeStatus.Skipped, xyz.Status);
        Assert.True(Directory.Exists(movieFolder)); // 夹未改名。
        Assert.True(File.Exists(System.IO.Path.Combine(movieFolder, "ABP-123 示例标题.mp4")));
        Assert.True(File.Exists(System.IO.Path.Combine(movieFolder, "ABP-123 示例标题.nfo")));
        Assert.True(File.Exists(System.IO.Path.Combine(movieFolder, "XYZ-999.mp4"))); // 保原名。
        Assert.False(File.Exists(System.IO.Path.Combine(movieFolder, "XYZ-999 示例标题.mp4")));
    }

    [Fact]
    public async Task Multi_cd_writes_per_cd_nfo_or_single()
    {
        using var temp = new TempDir("multi-cd");
        var movieFolder = temp.Rel("库/ABP-123 套装");
        File.WriteAllBytes(System.IO.Path.Combine(movieFolder, "ABP-123.mp4"), [0x01]);
        // 注意：cd2 使用 "-2" 而非 "_cd2"——"_C" 是中字标记的不区分大小写子串（见 p3-notes 待核对项）。
        File.WriteAllBytes(System.IO.Path.Combine(movieFolder, "ABP-123-2.mp4"), [0x02]);
        var root = temp.Rel("库");

        // 多 CD 逐盘 nfo（默认即逐盘）。
        var report = await RunAsync(Environment(Options()), root);
        var final = System.IO.Path.Combine(root, "ABP-123【演员甲、演员乙】");
        var contents = string.Join(";", Directory.EnumerateFileSystemEntries(final)
            .Select(p => FormatCodePoints(System.IO.Path.GetFileName(p))));
        Assert.True(File.Exists(System.IO.Path.Combine(final, "ABP-123 示例标题.mp4")),
            $"final={final} contents=[{contents}] outcomes=[{string.Join(" | ", report.Outcomes.Select(o => $"{o.Status}:{o.Message}"))}]");
        // 第 2 片改名追加 -cdN（p3-notes 决策 3）。
        Assert.True(File.Exists(System.IO.Path.Combine(final, "ABP-123 示例标题-cd2.mp4")));
        Assert.True(File.Exists(System.IO.Path.Combine(final, "ABP-123 示例标题.nfo")));
        Assert.True(File.Exists(System.IO.Path.Combine(final, "ABP-123 示例标题-cd2.nfo")));

        // 聚合成单 nfo（关封面便于计数）。
        using var temp2 = new TempDir("multi-cd-single");
        var folder2 = temp2.Rel("库/ABP-123 套装");
        File.WriteAllBytes(System.IO.Path.Combine(folder2, "ABP-123.mp4"), [0x01]);
        File.WriteAllBytes(System.IO.Path.Combine(folder2, "ABP-123-2.mp4"), [0x02]);
        await RunAsync(Environment(Options() with
        {
            MultiCdSingleNfo = true,
            CoverEnabled = false,
        }), temp2.Rel("库"));
        var final2 = System.IO.Path.Combine(temp2.Rel("库"), "ABP-123【演员甲、演员乙】");
        Assert.True(File.Exists(System.IO.Path.Combine(final2, "ABP-123 示例标题.nfo")));
        Assert.False(File.Exists(System.IO.Path.Combine(final2, "ABP-123 示例标题-cd2.nfo")));
        Assert.Equal(3, Directory.EnumerateFiles(final2).Count()); // 2 视频 + 1 nfo
    }

    [Fact]
    public async Task SkipMoviesWithSubtitles_keeps_original_filename()
    {
        using var temp = new TempDir("skip-sub");
        var movieFolder = temp.Rel("库/ABP-123");
        File.WriteAllBytes(System.IO.Path.Combine(movieFolder, "ABP-123.mp4"), [0x01]);
        File.WriteAllText(System.IO.Path.Combine(movieFolder, "ABP-123.chs.srt"), "示例字幕");
        var root = temp.Rel("库");

        var report = await RunAsync(Environment(Options() with { SkipMoviesWithSubtitles = true }), root);
        var outcome = Assert.Single(report.Outcomes, o => o.CarCode == "ABP-123");

        Assert.Equal(OrganizeStatus.Partial, outcome.Status);
        Assert.Contains("已有字幕", outcome.Message);
        // 视频保原名（p3-notes 决策 5），字幕也不动；夹照常改名。
        var final = System.IO.Path.Combine(root, "ABP-123【演员甲、演员乙】");
        Assert.True(Directory.Exists(final));
        Assert.True(File.Exists(System.IO.Path.Combine(final, "ABP-123.mp4")));
        Assert.True(File.Exists(System.IO.Path.Combine(final, "ABP-123.chs.srt")));
        // NFO/封面仍按原 stem 产出。
        Assert.True(File.Exists(System.IO.Path.Combine(final, "ABP-123.nfo")));
        Assert.True(File.Exists(System.IO.Path.Combine(final, "ABP-123-poster.jpg")));
    }

    [Fact]
    public async Task Classify_target_folders_moves_movie_folder_into_standard_parent()
    {
        using var temp = new TempDir("classify-folders");
        var movieFolder = temp.Rel("库/ABP-123");
        File.WriteAllBytes(System.IO.Path.Combine(movieFolder, "ABP-123.mp4"), [0x01]);
        var root = temp.Rel("库");

        var report = await RunAsync(Environment(Options() with
        {
            ClassifyEnabled = true,
            ClassifyStandard = "影片类型\\全部演员",
            ClassifyTarget = ClassifyTarget.Folders,
        }), root);

        // 影片夹整体搬进「根目录/有码/演员甲、演员乙/」之下（保持夹名，p3-notes 决策 6）。
        var final = System.IO.Path.Combine(root, "有码", "演员甲、演员乙", "ABP-123【演员甲、演员乙】");
        Assert.True(Directory.Exists(final));
        Assert.False(Directory.Exists(System.IO.Path.Combine(root, "ABP-123【演员甲、演员乙】"))); // 原位已搬走。
        Assert.True(File.Exists(System.IO.Path.Combine(final, "ABP-123 示例标题.mp4")));
        Assert.Equal(final, Assert.Single(report.Outcomes).NewFolder);
    }

    [Fact]
    public async Task Classify_target_files_moves_artifacts_into_standard_parent()
    {
        using var temp = new TempDir("classify-files");
        var movieFolder = temp.Rel("库/ABP-123");
        File.WriteAllBytes(System.IO.Path.Combine(movieFolder, "ABP-123.mp4"), [0x01]);
        var root = temp.Rel("库");

        var report = await RunAsync(Environment(Options() with
        {
            ClassifyEnabled = true,
            ClassifyStandard = "影片类型\\全部演员",
            ClassifyTarget = ClassifyTarget.Files,
        }), root);

        var final = System.IO.Path.Combine(root, "有码", "演员甲、演员乙");
        Assert.True(File.Exists(System.IO.Path.Combine(final, "ABP-123 示例标题.mp4")));
        Assert.True(File.Exists(System.IO.Path.Combine(final, "ABP-123 示例标题.nfo")));
        Assert.True(File.Exists(System.IO.Path.Combine(final, "ABP-123 示例标题-poster.jpg")));
        // 针对文件：影片文件搬空后原影片夹删掉（p5-notes）。
        Assert.False(Directory.Exists(System.IO.Path.Combine(root, "ABP-123【演员甲、演员乙】")));
        Assert.Equal(final, Assert.Single(report.Outcomes).NewFolder);
        var first = Assert.Single(report.Outcomes);
        Assert.True(File.Exists(first.NewPath));
    }

    [Fact]
    public async Task Missing_covers_writes_warning_file()
    {
        using var temp = new TempDir("no-cover");
        var movieFolder = temp.Rel("库/ABP-123");
        File.WriteAllBytes(System.IO.Path.Combine(movieFolder, "ABP-123.mp4"), [0x01]);
        var root = temp.Rel("库");

        await RunAsync(Environment(Options(), downloader: new NullCoverDownloader()), root);

        var final = System.IO.Path.Combine(root, "ABP-123【演员甲、演员乙】");
        var warnings = System.IO.Path.Combine(final, "警告信息.txt");
        Assert.True(File.Exists(warnings));
        // 警告文件 UTF-8（无 BOM）。
        Assert.NotEqual((byte)0xEF, File.ReadAllBytes(warnings)[0]);
        Assert.Contains("海报下载失败", File.ReadAllText(warnings));
        Assert.Contains("远图下载失败", File.ReadAllText(warnings));
    }

    [Fact]
    public async Task Banner_renderer_receives_badges_only_for_flagged_movies()
    {
        using var temp = new TempDir("banner");
        var renderer = new RecordingBannerRenderer();
        var options = Options() with
        {
            SubtitleBanner = true,
            LeakBanner = true,
        };
        // 文件名带中字 + 流出标记（标识文本走中性假标记已由配置注入，这里用默认标记 "-C"/"流出"）。
        var movieFolder = temp.Rel("库/ABP-123 客");
        File.WriteAllBytes(System.IO.Path.Combine(movieFolder, "ABP-123-C流出.mp4"), [0x01]);
        var root = temp.Rel("库");

        await RunAsync(Environment(options, renderer: renderer), root);

        var call = Assert.Single(renderer.Calls);
        Assert.Equal(["㊥", "无码流出"], call.Badges);
        Assert.Equal(PosterBytes.Length, call.BytesLength); // 渲染器收原始字节。
    }

    [Fact]
    public async Task Leak_and_subtitle_flags_flow_outcomes()
    {
        using var temp = new TempDir("flags");
        var movieFolder = temp.Rel("库/ABP-123-C标记流出");
        File.WriteAllBytes(System.IO.Path.Combine(movieFolder, "ABP-123-C.mp4"), [0x01]);
        var root = temp.Rel("库");

        var options = Options() with
        {
            SubtitleBanner = false,
            SubtitleRenameEnabled = false,
        };
        var report = await RunAsync(Environment(options), root);

        // 中字/流出由文件名标记识别并影响渲染/归档，仅验证 outcome 正常（P5 会再核对原版细节）。
        var outcome = Assert.Single(report.Outcomes, o => o.CarCode == "ABP-123");
        Assert.Equal(OrganizeStatus.Success, outcome.Status);
    }

    [Fact]
    public async Task Second_run_hits_cache_without_scrape_requests()
    {
        using var temp = new TempDir($"cache-hit-{Guid.NewGuid():N}");
        var root = temp.Rel("库");
        var dbPath = temp.Rel("scrape-cache.db");

        // 第一遍：真刮（网络假件），结果落缓存。
        File.WriteAllBytes(temp.Rel("库/ABP-123/ABP-123.mp4"), [0x01]);
        var fetcher = JavlibraryFetcher();
        using (var cache = new ScrapeCache(dbPath))
        {
            await RunAsync(Environment(Options(), fetcher: fetcher, cache: cache), root);
            Assert.Equal(1, cache.Count);
        }

        // 第二遍：新夹（同名车牌）——命中缓存，零请求。
        File.WriteAllBytes(temp.Rel("库2/ABP-123/ABP-123.mp4"), [0x01]);
        var fetcher2 = JavlibraryFetcher();
        using (var cache = new ScrapeCache(dbPath))
        {
            await RunAsync(Environment(Options(), fetcher: fetcher2, cache: cache), temp.Rel("库2"));
        }
        Assert.Empty(fetcher2.Requests);
    }

    [Fact]
    public async Task Negative_cache_skips_repeated_notfound_scans()
    {
        using var temp = new TempDir($"cache-negative-{Guid.NewGuid():N}");
        var root = temp.Rel("库");
        var dbPath = temp.Rel("scrape-cache.db");

        // 第一遍：全站未收录 → 负缓存写入。
        File.WriteAllBytes(temp.Rel("库/XYZ-999/XYZ-999.mp4"), [0x01]);
        OrganizeReport first;
        using (var cache = new ScrapeCache(dbPath))
        {
            first = await RunAsync(Environment(Options(), cache: cache), root);
        }

        var firstOutcome = Assert.Single(first.Outcomes, o => o.CarCode == UnknownCode);
        Assert.Contains("未收录该车牌", firstOutcome.Message);

        // 第二遍：负缓存命中——直接跳过，不再逐站打请求。
        File.WriteAllBytes(temp.Rel("库2/XYZ-999/XYZ-999.mp4"), [0x01]);
        using (var cache = new ScrapeCache(dbPath))
        {
            var second = await RunAsync(Environment(Options(), cache: cache), temp.Rel("库2"));
            var secondOutcome = Assert.Single(second.Outcomes, o => o.CarCode == UnknownCode);
            Assert.Contains("缓存记录", secondOutcome.Message);
        }
    }

    [Fact]
    public async Task Cache_disabled_scrapes_every_time()
    {
        using var temp = new TempDir("cache-off");
        var root = temp.Rel("库");
        File.WriteAllBytes(temp.Rel("库/ABP-123/ABP-123.mp4"), [0x01]);

        await RunAsync(Environment(Options()), root); // cache: null（缺省）
        await RunAsync(Environment(Options()), root); // 再跑：无缓存 → 仍会真刮（走库内假件同样的请求序列）。
        var outcome = Assert.Single((await RunAsync(Environment(Options()), root)).Outcomes, o => o.CarCode == "ABP-123");
        Assert.Equal(OrganizeStatus.Success, outcome.Status);
    }

    [Fact]
    public async Task PositiveOnly_level_rechecks_notfound_but_uses_metadata_cache()
    {
        using var temp = new TempDir($"cache-positive-{Guid.NewGuid():N}");
        var dbPath = temp.Rel("scrape-cache.db");
        var options = Options() with { CacheLevel = ScrapeCacheLevel.PositiveOnly };

        // 预置两条缓存：正缓存（ABP-123）与负缓存（XYZ-999）。
        using (var seed = new ScrapeCache(dbPath))
        {
            seed.Store(new MovieMetadata
            {
                CarCode = "ABP-123",
                Title = "缓存标题",
                SourceSite = ScrapeSource.Javlibrary,
            });
            seed.StoreNotFound("XYZ-999", "Javlibrary");
        }

        // 正缓存：仍命中（零请求）。
        File.WriteAllBytes(temp.Rel("库/ABP-123/ABP-123.mp4"), [0x01]);
        var fetcher = JavlibraryFetcher();
        using (var cache = new ScrapeCache(dbPath))
        {
            await RunAsync(Environment(options, fetcher: fetcher, cache: cache), temp.Rel("库"));
        }
        Assert.Empty(fetcher.Requests);

        // 负缓存：等级=只正缓存 → 不采用，重新去站点（假件未收录 → 再次确认未收录）。
        File.WriteAllBytes(temp.Rel("库2/XYZ-999/XYZ-999.mp4"), [0x01]);
        using (var cache = new ScrapeCache(dbPath))
        {
            var report = await RunAsync(Environment(options, cache: cache), temp.Rel("库2"));
            var outcome = Assert.Single(report.Outcomes, o => o.CarCode == UnknownCode);
            Assert.Contains("未收录该车牌", outcome.Message);
        }
    }

    [Fact]
    public async Task Cache_hit_increments_counter_and_writes_hit_log()
    {
        using var temp = new TempDir($"cache-log-hit-{Guid.NewGuid():N}");
        var dbPath = temp.Rel("scrape-cache.db");
        var logDirectory = temp.Rel("logs");

        // 第一遍：真刮 → 新增。
        File.WriteAllBytes(temp.Rel("库/ABP-123/ABP-123.mp4"), [0x01]);
        using (var cache = new ScrapeCache(dbPath))
        using (var log = new ScrapeCacheLog(logDirectory))
        {
            await RunAsync(Environment(Options(), cache: cache, cacheLog: log), temp.Rel("库"));
            Assert.Equal(0, cache.GetRow("ABP-123")!.HitCount);
        }

        // 第二遍：命中 → 计数 +1，零请求。
        File.WriteAllBytes(temp.Rel("库2/ABP-123/ABP-123.mp4"), [0x01]);
        var fetcher = JavlibraryFetcher();
        using (var cache = new ScrapeCache(dbPath))
        using (var log = new ScrapeCacheLog(logDirectory))
        {
            await RunAsync(Environment(Options(), fetcher: fetcher, cache: cache, cacheLog: log), temp.Rel("库2"));
            Assert.Empty(fetcher.Requests);
            Assert.Equal(1, cache.GetRow("ABP-123")!.HitCount);
        }

        var text = File.ReadAllText(Path.Combine(
            logDirectory, ScrapeCacheLog.FileNameFor(DateOnly.FromDateTime(DateTime.Now))));
        Assert.Contains("| ADD | ABP-123", text);
        Assert.Contains("| HIT | ABP-123", text);
        Assert.Contains("| RUN-END |", text);
    }

    [Fact]
    public async Task PositiveOnly_promotes_notfound_record_when_site_now_has_it()
    {
        using var temp = new TempDir($"cache-promote-{Guid.NewGuid():N}");
        var dbPath = temp.Rel("scrape-cache.db");
        var logDirectory = temp.Rel("logs");
        using (var seed = new ScrapeCache(dbPath))
        {
            seed.StoreNotFound("ABP-123", "Javlibrary");
        }

        File.WriteAllBytes(temp.Rel("库/ABP-123/ABP-123.mp4"), [0x01]);
        var options = Options() with { CacheLevel = ScrapeCacheLevel.PositiveOnly };
        var fetcher = JavlibraryFetcher();
        using (var cache = new ScrapeCache(dbPath))
        using (var log = new ScrapeCacheLog(logDirectory))
        {
            var report = await RunAsync(
                Environment(options, fetcher: fetcher, cache: cache, cacheLog: log), temp.Rel("库"));
            var outcome = Assert.Single(report.Outcomes, o => o.CarCode == "ABP-123");
            Assert.Equal(OrganizeStatus.Success, outcome.Status);
            Assert.NotEmpty(fetcher.Requests); // 负缓存被忽略，真的访问了站点
            Assert.Equal("metadata", cache.GetRow("ABP-123")!.Kind);
        }

        var text = File.ReadAllText(Path.Combine(
            logDirectory, ScrapeCacheLog.FileNameFor(DateOnly.FromDateTime(DateTime.Now))));
        Assert.Contains("| PROMOTE | ABP-123", text);
        Assert.Contains("负缓存已忽略", text);
    }

    [Fact]
    public async Task Logs_and_inbound_report_round_trip_after_two_runs()
    {
        using var temp = new TempDir($"cache-report-e2e-{Guid.NewGuid():N}");
        var dbPath = temp.Rel("scrape-cache.db");
        var logDirectory = temp.Rel("logs");

        File.WriteAllBytes(temp.Rel("库/ABP-123/ABP-123.mp4"), [0x01]);
        using (var cache = new ScrapeCache(dbPath))
        using (var log = new ScrapeCacheLog(logDirectory))
        {
            await RunAsync(Environment(Options(), cache: cache, cacheLog: log), temp.Rel("库"));
        }

        File.WriteAllBytes(temp.Rel("库2/ABP-123/ABP-123.mp4"), [0x01]);
        using (var cache = new ScrapeCache(dbPath))
        using (var log = new ScrapeCacheLog(logDirectory))
        {
            await RunAsync(Environment(Options(), cache: cache, cacheLog: log), temp.Rel("库2"));
        }

        var now = DateTime.Now;
        var report = InboundReportBuilder.Build(
            logDirectory, dbPath, now.AddDays(-1), now.AddDays(1));
        var inbound = report.View(InboundView.Inbound);
        Assert.Equal(1, inbound.DistinctCodes);
        Assert.Equal(1, inbound.Total);
        Assert.Equal(1, inbound.AddCount);

        var all = report.View(InboundView.InboundAndUsage);
        Assert.Equal(2, all.Total);
        Assert.Equal(1, all.HitCount);

        var html = InboundHtmlReportWriter.Generate(
            logDirectory, dbPath, now.AddDays(-1), now.AddDays(1),
            outputDirectory: temp.Rel("reports"));
        var text = File.ReadAllText(html.Path);
        Assert.Contains("ABP-123", text);
        Assert.Contains("新入库", text);
        Assert.Contains("使用（缓存命中）", text);
    }

    /// <summary>永远返回 null 的站点（测试"未收录"场景 / 未实现站点兜底）。</summary>
    private sealed class NeverFoundScraper(ScrapeSource source) : IScraper
    {
        public ScrapeSource Source { get; } = source;

        public IReadOnlyList<CarCategory> Categories { get; } = [CarCategory.Youma];

        public IReadOnlyList<string> Requests { get; } = [];

        public Task<MovieMetadata?> ScrapeAsync(string carCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<MovieMetadata?>(null);
    }

    private static string FormatCodePoints(string text) => string.Join(" ", text.Select(c => ((int)c).ToString("X4")));

    [Fact]
    public async Task Progress_reports_status_and_every_outcome()
    {
        using var temp = new TempDir("progress");
        var movieFolder = temp.Rel("库/ABP-123");
        File.WriteAllBytes(System.IO.Path.Combine(movieFolder, "ABP-123.mp4"), [0x01]);
        File.WriteAllText(System.IO.Path.Combine(movieFolder, "ABP-123.chs.srt"), "示例字幕");
        var root = temp.Rel("库");

        var events = new List<OrganizeProgressEvent>();
        var progress = new CollectingProgress(events);
        await new OrganizePipeline(Environment(Options())).RunAsync(root, progress, CancellationToken.None);

        // 状态行：开始扫描 / 扫描完成 / 正在处理 / 整理清单。
        var statuses = events.Select(e => e.Status).Where(s => s is not null).ToArray();
        Assert.Contains(statuses, s => s!.StartsWith("开始扫描", StringComparison.Ordinal));
        Assert.Contains(statuses, s => s!.StartsWith("扫描完成", StringComparison.Ordinal));
        Assert.Contains(statuses, s => s!.StartsWith("正在处理 [", StringComparison.Ordinal));
        Assert.Contains(statuses, s => s!.StartsWith("整理清单", StringComparison.Ordinal));

        // 每个产物（视频 + 字幕 = 2 条）都上报。
        Assert.Equal(2, events.Count(e => e.Outcome is not null));
    }

    [Fact]
    public void OrganizeDefaults_loads_data_assets_and_falls_back()
    {
        // data 目录真实资产仅做结构断言（不读内容到断言文本）。
        var withData = OrganizeDefaults.CreateOptions(new AppConfig(), dataDirectory: TestPaths.Data(""));
        Assert.NotNull(withData.Dictionary);
        Assert.Equal(5, withData.Dictionary!.SheetNames.Count);
        Assert.True(withData.Codes.SurenPrefixes.Count > 0);

        // 缺失目录 → 降级：无字典 + 空素人表。
        var withMissing = OrganizeDefaults.CreateOptions(
            new AppConfig(), dataDirectory: Path.Combine(Path.GetTempPath(), "不存在目录-示例"));
        Assert.Null(withMissing.Dictionary);
        Assert.Empty(withMissing.Codes.SurenPrefixes);
    }

    /// <summary>同步收集进度（不跨线程封送；封送行为由 Progress&lt;T&gt; 在 UI 侧保证）。</summary>
    private sealed class CollectingProgress(List<OrganizeProgressEvent> sink) : IProgress<OrganizeProgressEvent>
    {
        public void Report(OrganizeProgressEvent value) => sink.Add(value);
    }
}
