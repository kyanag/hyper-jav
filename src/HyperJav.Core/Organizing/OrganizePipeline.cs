using HyperJav.Core.Configuration;
using HyperJav.Core.Models;
using HyperJav.Core.Naming;
using HyperJav.Core.Nfo;
using HyperJav.Core.Parsing;
using HyperJav.Core.Scraping;

namespace HyperJav.Core.Organizing;

/// <summary>进度事件（UI 展示用）：状态行文本 或 单个整理结果（二选一）。</summary>
public sealed record OrganizeProgressEvent(string? Status, OrganizeOutcome? Outcome)
{
    public static OrganizeProgressEvent For(OrganizeOutcome outcome) => new(null, outcome);

    public static OrganizeProgressEvent For(string status) => new(status, null);
}

/// <summary>
/// 整理管线：扫描 → 车牌 → 刮削 → 重命名影片 → 字幕 → 建夹/夹改名 → NFO → 封面 → 归类 → 清单。
/// 顺序、开关与默认值全部来自 OrganizeOptions（ini 映射），目录级失败不影响其余。
/// </summary>
public sealed class OrganizePipeline
{
    private readonly OrganizeEnvironment _environment;

    public OrganizePipeline(OrganizeEnvironment environment) => _environment = environment;

    public async Task<OrganizeReport> RunAsync(string rootDirectory, CancellationToken cancellationToken = default) =>
        await RunAsync(rootDirectory, progress: null, cancellationToken);

    /// <summary>带进度回调（UI 用）；进度对象自线程封送。</summary>
    public async Task<OrganizeReport> RunAsync(
        string rootDirectory, IProgress<OrganizeProgressEvent>? progress, CancellationToken cancellationToken)
    {
        var options = _environment.Options;
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(OrganizeProgressEvent.For($"开始扫描：{rootDirectory}"));
        var groups = VideoFileScanner.Scan(rootDirectory, options.VideoExtensions);
        progress?.Report(OrganizeProgressEvent.For($"扫描完成：{groups.Count} 个影片文件夹"));
        var outcomes = new List<OrganizeOutcome>();
        var total = groups.Count;
        var index = 0;
        var cacheStats = new CacheRunStats { Total = total };
        var cacheLog = _environment.CacheLog;
        // 缓存连接的生命周期归调用方（测试同一实例可跨运行复用；GUI 在 finally 释放）。
        cacheLog?.Event(CacheLogEvents.RunStart, detail:
            $"total={total} level={ScrapeCacheLevels.TextOf(options.CacheLevel)}");
        try
        {
            foreach (var group in groups)
            {
                index++;
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(OrganizeProgressEvent.For(
                    $"正在处理 [{index}/{total}]：{Path.GetFileName(group.Directory)}{ViaDescription(group, options)}"));
                foreach (var outcome in await ProcessFolderAsync(
                    group, rootDirectory, progress, cancellationToken, cacheStats))
                {
                    outcomes.Add(outcome);
                }
            }
        }
        finally
        {
            if (cacheLog is not null)
            {
                cacheLog.Event(CacheLogEvents.RunEnd, detail: cacheStats.Summary);
                if (cacheStats.Hits + cacheStats.NegativeHits > 0)
                {
                    progress?.Report(OrganizeProgressEvent.For(
                        $"缓存：正命中 {cacheStats.Hits}・负跳过 {cacheStats.NegativeHits}・写入 {cacheStats.Writes}"));
                }
            }
        }
        var checklistDirectory = string.IsNullOrWhiteSpace(_environment.ChecklistDirectory)
            ? Path.Combine(OrganizeDefaults.ResolveDataDirectory(null), TextOutputs.ChecklistDirectoryName)
            : _environment.ChecklistDirectory;
        var checklistPath = ChecklistWriter.Write(checklistDirectory, outcomes);
        progress?.Report(OrganizeProgressEvent.For(
            checklistPath is null ? "整理清单写入失败" : $"整理清单：{checklistPath}"));
        return new OrganizeReport(outcomes, checklistPath);
    }

    private async Task<OrganizeOutcome[]> ProcessFolderAsync(
        FolderMovieGroup folder,
        string rootDirectory,
        IProgress<OrganizeProgressEvent>? progress,
        CancellationToken ct,
        CacheRunStats cacheStats)
    {
        var options = _environment.Options;

        if (options.SkipFoldersWithNfo && Directory.Exists(folder.Directory)
            && Directory.EnumerateFiles(folder.Directory, "*.nfo").Any())
        {
            return [.. folder.Videos.Select(video =>
            {
                var skipped = new OrganizeOutcome(
                    video.FullPath, OrganizeStatus.Skipped, null, CarCategory.Unknown, "跳过：文件夹已存在 nfo");
                progress?.Report(OrganizeProgressEvent.For(skipped));
                return skipped;
            })];
        }

        // 1. 车牌解析（null 车牌的按文件独立成组）。
        var parsed = folder.Videos
            .Select(video => (Entry: video, Code: CarCodeParser.Parse(video.FileName, options.Codes)))
            .ToList();

        // 文件夹里同时有多个不同车牌 → 不做文件夹级操作（p3-notes 决策）。
        var distinctCodes = parsed
            .Select(p => p.Code.CarCode)
            .Where(code => code is not null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var folderOpsAllowed = distinctCodes <= 1;

        var outcomes = new List<OrganizeOutcome>();
        foreach (var group in parsed.GroupBy(p => p.Code.CarCode ?? "!" + p.Entry.FullPath, StringComparer.Ordinal))
        {
            try
            {
                foreach (var outcome in await ProcessCodeGroupAsync(
                    group, folder, rootDirectory, folderOpsAllowed, progress, ct, cacheStats))
                {
                    progress?.Report(OrganizeProgressEvent.For(outcome));
                    outcomes.Add(outcome);
                }
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                // 单夹失败（网络/IO/解析）只产出该夹错误 outcome，不中断其余 Ordinal。
                var failures = group.Select(p =>
                {
                    var failed = new OrganizeOutcome(
                        p.Entry.FullPath, OrganizeStatus.Error, p.Code.CarCode, p.Code.Category,
                        $"整理失败：{error.Message}");
                    progress?.Report(OrganizeProgressEvent.For(failed));
                    return failed;
                }).ToArray();
                outcomes.AddRange(failures);
            }
        }
        return [.. outcomes];
    }

    /// <summary>
    /// 刮削候选序列（P5 适配器模型）：强制源 → 单站；auto 模式 → 按影片类别筛适配器能力（登记序=优先级，
    /// 同一类别可多站：首站网络失败/未收录自动转下一站）。
    /// </summary>
    private static IReadOnlyList<ScrapeSource> SourceCandidates(CarCodeResult code, OrganizeOptions options)
    {
        if (options.ForcedSource is { } forced)
        {
            return [forced];
        }
        return ScraperFactory.CapableSources(code.Category);
    }

    /// <summary>进度行附带的站点序列（显示用；从夹内首个可识别车牌取类别）。</summary>
    private string ViaDescription(FolderMovieGroup folder, OrganizeOptions options)
    {
        var parsed = folder.Videos
            .Select(video => CarCodeParser.Parse(video.FileName, options.Codes))
            .FirstOrDefault(p => p.Category != CarCategory.Unknown);
        if (parsed is null)
        {
            return "";
        }
        var sources = SourceCandidates(parsed, options);
        return sources.Count == 0 ? "" : $"（刮削源：{string.Join("→", sources.Select(ScrapeProbeResult.LabelOf))}）";
    }

    /// <summary>尝试记录里的请求 URL 块（缩进一行一 URL，供日志直接可见访问轨迹）。</summary>
    private static string UrlsOf(IScraper scraper) =>
        string.Join("\n", scraper.Requests.Select(url => $"    {url}"));

    /// <summary>异常消息的短摘要（去掉"（原始：…）"尾巴等），尝试列表用。</summary>
    private static string ShortReason(string message)
    {
        var originalIndex = message.IndexOf("（原始", StringComparison.Ordinal);
        var text = originalIndex >= 0 ? message[..originalIndex] : message;
        return text.Length > 44 ? text[..44] + "…" : text;
    }

    private async Task<OrganizeOutcome[]> ProcessCodeGroupAsync(
        IGrouping<string, (VideoEntry Entry, CarCodeResult Code)> group,
        FolderMovieGroup folder,
        string rootDirectory,
        bool folderOpsAllowed,
        IProgress<OrganizeProgressEvent>? progress,
        CancellationToken ct,
        CacheRunStats cacheStats)
    {
        var options = _environment.Options;
        var items = group.ToList();
        var primary = items[0];
        var code = primary.Code.CarCode;
        var category = primary.Code.Category;
        var subtitleFlag = items.Any(i => i.Code.Subtitle);
        var leakFlag = items.Any(i => i.Code.Leak);

        // 未识别车牌：不刮削不重命名。
        if (string.IsNullOrWhiteSpace(code) || category == CarCategory.Unknown)
        {
            return [.. items.Select(item => new OrganizeOutcome(
                item.Entry.FullPath, OrganizeStatus.Skipped, item.Code.CarCode, item.Code.Category,
                "未识别车牌（或类型未知），未做整理"))];
        }

        // 2. 刮削（缓存优先：命中直接用，绝不访问站点；未命中逐站刮 + 写回）。
        var sourceCandidates = SourceCandidates(primary.Code, options);
        MovieMetadata? metadata = null;
        ScrapeException? lastError = null;
        var attempted = new List<string>(); // 每站一块"站点 + 结果 + 访问 URL"（诊断用）。
        var labelNote = sourceCandidates.Count == 0
            ? ""
            : string.Join("→", sourceCandidates.Select(ScrapeProbeResult.LabelOf));

        var cache = _environment.ScrapeCache;
        var cacheLog = _environment.CacheLog;
        var folderLabel = RelativeFolder(rootDirectory, folder.Directory);
        string? missDetail = null; // 未命中原因（写日志用）

        if (cache is not null)
        {
            try
            {
                // 负缓存只在"完全使用缓存"档计数并采用；"只使用正缓存"档忽略它重新访问站点。
                var cached = cache.Lookup(code!, options.CacheLevel == ScrapeCacheLevel.Full);
                if (cached?.Metadata is { } cachedMetadata)
                {
                    cacheStats.Hits++;
                    var sourceLabel = ScrapeProbeResult.LabelOf(cachedMetadata.SourceSite);
                    progress?.Report(OrganizeProgressEvent.For(
                        $"命中缓存：{code}（{sourceLabel}，第 {cached.HitCount} 次）"));
                    cacheLog?.Event(CacheLogEvents.Hit, code, sourceLabel, $"#{cached.HitCount}", folderLabel);
                    metadata = cachedMetadata;
                }
                else if (cached is { NotFound: true } && options.CacheLevel == ScrapeCacheLevel.Full)
                {
                    cacheStats.NegativeHits++;
                    cacheLog?.Event(
                        CacheLogEvents.NegativeHit, code, cached.Source, $"#{cached.NegativeHitCount}", folderLabel);
                    return [.. items.Select(item => new OrganizeOutcome(
                        item.Entry.FullPath, OrganizeStatus.Skipped, code, category,
                        "缓存记录：上次已确认各站未收录，保留原名"))];
                }
                else if (cached is { NotFound: true })
                {
                    missDetail = "负缓存已忽略（只使用正缓存）";
                }
            }
            catch (Exception error) when (error is IOException or Microsoft.Data.Sqlite.SqliteException
                or ObjectDisposedException)
            {
                // 缓存读不可用 → 降级为正常刮削。
                cacheStats.Errors++;
                cacheLog?.Event(CacheLogEvents.Error, code, null, $"读缓存失败：{error.GetType().Name}", folderLabel);
            }
        }

        if (metadata is null)
        {
            if (cache is not null)
            {
                cacheStats.Misses++;
                cacheLog?.Event(CacheLogEvents.Miss, code, labelNote, missDetail, folderLabel);
            }
            foreach (var source in sourceCandidates)
            {
                var label = ScrapeProbeResult.LabelOf(source);
                var scraper = _environment.Scrapers(source);
                try
                {
                    metadata = await scraper.ScrapeAsync(code!, ct);
                    if (metadata is not null)
                    {
                        progress?.Report(OrganizeProgressEvent.For($"刮削成功：{label}（{code}）"));
                        break;
                    }
                    attempted.Add($"{label}：未收录\n{UrlsOf(scraper)}");
                }
                catch (ScrapeException caught)
                {
                    lastError = caught;
                    attempted.Add($"{label}：{ShortReason(caught.Message)}\n{UrlsOf(scraper)}");
                }
            }

            if (cache is not null)
            {
                try
                {
                    if (metadata is not null)
                    {
                        var result = cache.Store(metadata with { Subtitle = subtitleFlag, Leak = leakFlag });
                        var sourceLabel = ScrapeProbeResult.LabelOf(metadata.SourceSite);
                        switch (result)
                        {
                            case CacheStoreResult.Inserted:
                                cacheStats.Added++;
                                cacheLog?.Event(CacheLogEvents.Add, code, sourceLabel, "metadata", folderLabel);
                                break;
                            case CacheStoreResult.Refreshed:
                                cacheStats.Updated++;
                                cacheLog?.Event(CacheLogEvents.Update, code, sourceLabel, "metadata", folderLabel);
                                break;
                            case CacheStoreResult.Promoted:
                                cacheStats.Promoted++;
                                cacheLog?.Event(CacheLogEvents.Promote, code, sourceLabel, "负转正", folderLabel);
                                progress?.Report(OrganizeProgressEvent.For(
                                    $"缓存更新：{code} 由未收录转为已收录（{sourceLabel}）"));
                                break;
                            case CacheStoreResult.KeptMetadata:
                                cacheStats.Kept++;
                                cacheLog?.Event(CacheLogEvents.Kept, code, null, "已有元数据，拒绝负缓存覆盖", folderLabel);
                                break;
                        }
                    }
                    else if (lastError is null)
                    {
                        var result = cache.StoreNotFound(code!, labelNote);
                        if (result == CacheStoreResult.KeptMetadata)
                        {
                            cacheStats.Kept++;
                            cacheLog?.Event(CacheLogEvents.Kept, code, null, "已有元数据，拒绝负缓存覆盖", folderLabel);
                        }
                        else
                        {
                            cacheStats.NegativeAdded++;
                            cacheLog?.Event(
                                result == CacheStoreResult.InsertedNegative
                                    ? CacheLogEvents.AddNegative
                                    : CacheLogEvents.RefreshNegative,
                                code, labelNote, "notfound", folderLabel);
                        }
                    }
                }
                catch (Exception error) when (error is IOException or Microsoft.Data.Sqlite.SqliteException)
                {
                    // 缓存写失败不影响本夹结果（下次重刮而已）。
                    cacheStats.Errors++;
                    cacheLog?.Event(CacheLogEvents.Error, code, null, $"写缓存失败：{error.GetType().Name}", folderLabel);
                }
            }
        }
        if (metadata is null && lastError is not null)
        {
            return [.. items.Select(item => new OrganizeOutcome(
                item.Entry.FullPath, OrganizeStatus.Skipped, code, category,
                $"刮削失败（{string.Join("；", attempted)}）：{lastError.Message}"))];
        }
        if (metadata is null)
        {
            return [.. items.Select(item => new OrganizeOutcome(
                item.Entry.FullPath, OrganizeStatus.Skipped, code, category,
                $"站点未收录该车牌（{string.Join("；", attempted)}），保留原名"))];
        }
        metadata = metadata with { Subtitle = subtitleFlag, Leak = leakFlag };

        var warnings = new List<string>();
        var directory = folder.Directory;
        var isRootDirectory = PathsEqual(directory, rootDirectory);
        var namingOptions = RenderOptionsFrom(options);
        var noBadgeOptions = namingOptions with { SubtitleBadge = "", LeakBadge = "" };
        var artifacts = new List<string>(); // 归类（针对文件）要搬的文件。
        var outcomes = new List<OrganizeOutcome>();
        var currentVideoPaths = new Dictionary<VideoEntry, string>();

        // 3. 重命名影片 + 字幕（多 CD：文件同名会冲突，第 2 部起追加 -cdN）。
        var posterName = "";
        var fanartName = "";
        string? nfoName = null;
        string? primaryStem = null;
        for (var index = 0; index < items.Count; index++)
        {
            var (entry, _) = items[index];
            var current = entry.FullPath;
            var stem = entry.Stem;

            if (options.RenameEnabled)
            {
                // "跳过已有字幕的影片"：按文件名/车牌查找配对字幕，命中则不重命名视频（p3-notes 决策 5）。
                var hasSubtitle = SubtitleRenamer.HasMatch(directory, stem, code);
                var skipRename = options.SkipMoviesWithSubtitles && hasSubtitle;
                if (!skipRename)
                {
                    var context = NamingContextOf(metadata, options, category, subtitleFlag, leakFlag, stem);
                    var rendered = PathSanitizer.Sanitize(new NamingTemplate(options.RenameTemplate).Render(context, namingOptions));
                    if (items.Count > 1 && index > 0)
                    {
                        rendered += $"-cd{index + 1}";
                    }
                    if (rendered.Length > 0 && !string.Equals(rendered, stem, StringComparison.Ordinal))
                    {
                        var moved = Path.Combine(directory, $"{rendered}{Path.GetExtension(current)}");
                        if (!File.Exists(moved) || PathsEqual(moved, current))
                        {
                            File.Move(current, moved);
                        }
                        current = moved;
                        stem = Path.GetFileNameWithoutExtension(current);
                        outcomes.Add(new OrganizeOutcome(entry.FullPath, OrganizeStatus.Success, code, category, "已重命名")
                        {
                            NewPath = current,
                        });
                    }
                }
                else
                {
                    outcomes.Add(new OrganizeOutcome(entry.FullPath, OrganizeStatus.Partial, code, category, "保留原名（已有字幕）")
                    {
                        NewPath = current,
                    });
                }
            }
            if (outcomes.All(o => o.OriginalPath != entry.FullPath))
            {
                outcomes.Add(new OrganizeOutcome(entry.FullPath, OrganizeStatus.Success, code, category, "文件名未变化")
                {
                    NewPath = current,
                });
            }
            currentVideoPaths[entry] = current;
            artifacts.Add(current);

            if (index == 0)
            {
                primaryStem = stem;
                var coverContext = NamingContextOf(metadata, options, category, subtitleFlag, leakFlag, stem);
                if (options.CoverEnabled)
                {
                    posterName = PathSanitizer.Sanitize(new NamingTemplate(options.PosterTemplate).Render(coverContext, noBadgeOptions));
                    fanartName = PathSanitizer.Sanitize(new NamingTemplate(options.FanartTemplate).Render(coverContext, noBadgeOptions));
                }
                if (options.CollectNfo)
                {
                    nfoName = $"{stem}.nfo";
                }

                // 字幕改名只处理主文件（多 CD 副盘共用一套字幕时通常跟随 cd1）。
                if (options.SubtitleRenameEnabled && options.RenameEnabled &&
                    !(options.SkipMoviesWithSubtitles && SubtitleRenamer.HasMatch(directory, entry.Stem, code)))
                {
                    foreach (var (subOld, subNew) in SubtitleRenamer.Rename(directory, entry.Stem, stem, code))
                    {
                        artifacts.Add(subNew);
                        outcomes.Add(new OrganizeOutcome(subOld, OrganizeStatus.Success, code, category, "已重命名字幕")
                        {
                            NewPath = subNew,
                        });
                    }
                }
            }
        }

        // 4. 文件夹：重命名所在夹；视频直接在扫描根目录时创建独立夹（不动根目录本身）。
        if (options.FolderEnabled && folderOpsAllowed)
        {
            var folderContext = NamingContextOf(metadata, options, category, subtitleFlag, leakFlag, primaryStem ?? "");
            var folderName = PathSanitizer.Sanitize(new NamingTemplate(options.FolderTemplate).Render(folderContext, noBadgeOptions));
            if (folderName.Length > 0)
            {
                if (isRootDirectory)
                {
                    var target = Path.Combine(rootDirectory, folderName);
                    FolderOps.MoveInto(target, artifacts);
                    directory = target;
                }
                else
                {
                    directory = FolderOps.EnsureFolder(directory, Path.Combine(Path.GetDirectoryName(directory)!, folderName));
                }
                // 目录变动后统一重定位 artifacts（同文件夹内改名不换文件名）。
                artifacts = artifacts
                    .Select(path => Path.Combine(directory, Path.GetFileName(path)))
                    .Where(File.Exists)
                    .ToList();
                currentVideoPaths = currentVideoPaths.ToDictionary(
                    pair => pair.Key,
                    pair => Path.Combine(directory, Path.GetFileName(pair.Value)));
            }
        }

        // 5. NFO（nfo title 不截断 → 见 p1-notes 决策 9）。
        if (options.CollectNfo && primaryStem is not null)
        {
            var nfoResult = new NfoGenerator().Generate(metadata, BuildNfoOptions(options, posterName, fanartName, primaryStem));
            warnings.AddRange(nfoResult.Warnings);

            IEnumerable<string> nfoFiles = options.MultiCdSingleNfo
                ? [nfoName!]
                : currentVideoPaths.Values.Select(path => $"{Path.GetFileNameWithoutExtension(path)}.nfo");

            foreach (var name in nfoFiles.Distinct())
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }
                var nfoPath = Path.Combine(directory, name);
                File.WriteAllText(nfoPath, nfoResult.Xml, TextOutputs.Utf8NoBom);
                artifacts.Add(nfoPath);
            }
        }

        // 6. 封面（海报/远图缺一用另一张兜底；条幅只画在海报上）。
        if (options.CoverEnabled)
        {
            var badges = new List<string>();
            if (subtitleFlag && options.SubtitleBanner && options.SubtitleBadge.Length > 0)
            {
                badges.Add(options.SubtitleBadge);
            }
            if (leakFlag && options.LeakBanner && options.LeakBadge.Length > 0)
            {
                badges.Add(options.LeakBadge);
            }

            var posterSource = metadata.PosterUrl ?? metadata.FanartUrl;
            var posterBytes = await _environment.CoverDownloader.DownloadAsync(posterSource, ct);
            if (posterBytes is not null && posterName.Length > 0)
            {
                if (badges.Count > 0)
                {
                    posterBytes = _environment.BannerRenderer.Render(posterBytes, badges);
                }
                var posterPath = Path.Combine(directory, posterName);
                File.WriteAllBytes(posterPath, posterBytes);
                artifacts.Add(posterPath);
            }
            else
            {
                warnings.Add($"海报下载失败：{posterSource}");
            }

            var fanartSource = metadata.FanartUrl ?? metadata.PosterUrl;
            var fanartBytes = await _environment.CoverDownloader.DownloadAsync(fanartSource, ct);
            if (fanartBytes is not null && fanartName.Length > 0)
            {
                var fanartPath = Path.Combine(directory, fanartName);
                File.WriteAllBytes(fanartPath, fanartBytes);
                artifacts.Add(fanartPath);
            }
            else
            {
                warnings.Add($"远图下载失败：{fanartSource}");
            }
        }

        // 7. 字典未命中告警落盘（p1-notes：警告信息.txt）。
        if (warnings.Count > 0)
        {
            TextOutputs.WriteWarnings(directory, warnings);
        }

        // 8. 归类（文件夹整体搬 → 针对文件夹；成组文件搬 → 针对文件）。多车牌目录跳过（见 p3-notes）。
        if (options.ClassifyEnabled && folderOpsAllowed)
        {
            var classifyRoot = ClassifyPlanner.ResolveRoot(options.ClassifyRoot, rootDirectory);
            var classifyContext = NamingContextOf(metadata, options, category, subtitleFlag, leakFlag, primaryStem ?? "");
            // segmentsDir = 分类父目录（标准模板渲染结果）；影片夹/文件放进它之下。
            var segmentsDir = ClassifyPlanner.BuildTargetDirectory(classifyRoot, options.ClassifyStandard, classifyContext, noBadgeOptions);

            // 针对文件模式下影片文件搬走后原影片夹会留空，需要删除它（记住搬移前的夹路径）。
            var sourceDirectory = directory;

            if (options.ClassifyTarget == ClassifyTarget.Folders)
            {
                // 影片文件夹整体搬到 segmentsDir 之下（保持自身夹名）。
                if (!PathsEqual(directory, segmentsDir))
                {
                    directory = FolderOps.EnsureFolder(directory, Path.Combine(segmentsDir, Path.GetFileName(directory)));
                }
                currentVideoPaths = currentVideoPaths.ToDictionary(
                    pair => pair.Key, pair => Path.Combine(directory, Path.GetFileName(pair.Value)));
            }
            else
            {
                FolderOps.MoveInto(segmentsDir, artifacts);
                currentVideoPaths = currentVideoPaths.ToDictionary(
                    pair => pair.Key, pair => Path.Combine(segmentsDir, Path.GetFileName(pair.Value)));
                // 针对文件：搬完后删空原影片夹（根目录除外——不能删用户所选整理根，见 p5-notes）。
                if (!PathsEqual(sourceDirectory, rootDirectory))
                {
                    FolderOps.DeleteIfEmpty(sourceDirectory);
                }
                directory = segmentsDir;
            }
        }

        return FinalizeOutcomes(outcomes, directory);
    }

    /// <summary>把每个视频的最终落位写回 outcome（目录改名后 NewPath/NewFolder 统一刷新）。</summary>
    private static OrganizeOutcome[] FinalizeOutcomes(List<OrganizeOutcome> outcomes, string finalDirectory)
    {
        var final = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var outcome in outcomes)
        {
            if (outcome.NewPath is not null)
            {
                final[outcome.OriginalPath] = Path.Combine(finalDirectory, Path.GetFileName(outcome.NewPath));
            }
        }
        return [.. outcomes.Select(o => o with
        {
            NewPath = final.TryGetValue(o.OriginalPath, out var path) ? path : null,
            NewFolder = finalDirectory,
        })];
    }

    private static NfoOptions BuildNfoOptions(OrganizeOptions options, string posterName, string fanartName, string videoName) => new()
    {
        TitleTemplate = options.NfoTitleTemplate,
        RemoveTrailingActorNames = options.RemoveTrailingActorNames,
        SaveFeaturesToGenre = options.SaveFeaturesToGenre,
        SaveFeaturesToTag = options.SaveFeaturesToTag,
        ExtraFeatureElements = options.ExtraFeatureElements,
        Script = options.Script,
        FeatureDictionary = options.Dictionary,
        SubtitleBadge = options.SubtitleBadge,
        LeakBadge = options.LeakBadge,
        PosterFileName = options.CoverEnabled ? posterName : null,
        FanartFileName = options.CoverEnabled ? fanartName : null,
        VideoName = videoName,
    };

    private static NamingRenderOptions RenderOptionsFrom(OrganizeOptions options) => new()
    {
        MaxTitleLength = options.MaxTitleLength,
        SubtitleBadge = options.SubtitleBadge,
        LeakBadge = options.LeakBadge,
    };

    private static NamingContext NamingContextOf(
        MovieMetadata metadata, OrganizeOptions options, CarCategory category, bool subtitle, bool leak, string videoName)
        => new()
        {
            CarCode = metadata.CarCode,
            Title = metadata.Title,
            Actors = metadata.Actors,
            Series = metadata.Series,
            Studio = metadata.Studio,
            ReleaseDate = metadata.ReleaseDate,
            CategoryLabel = options.Labels.Get(category),
            VideoName = videoName,
            Subtitle = subtitle,
            Leak = leak,
        };

    private static bool PathsEqual(string a, string b)
        => string.Equals(Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);

    /// <summary>日志里的相对目录（扫描根下），避免每行重复长长的绝对路径。</summary>
    private static string RelativeFolder(string rootDirectory, string directory)
    {
        try
        {
            var relative = Path.GetRelativePath(rootDirectory, directory);
            return relative == "." ? Path.GetFileName(directory) : relative;
        }
        catch (ArgumentException)
        {
            return directory;
        }
    }
}
