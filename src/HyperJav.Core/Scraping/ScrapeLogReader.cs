using System.Globalization;

namespace HyperJav.Core.Scraping;

/// <summary>流水事件类型（报表只关心这几类；RUN-START/RUN-END 等直接忽略）。</summary>
public enum ScrapeLogKind
{
    /// <summary>新入库（首次刮到并写入缓存）。</summary>
    Add,

    /// <summary>转正入库（此前未收录，后来刮到）。</summary>
    Promote,

    /// <summary>资料更新（同车牌重新刮到并覆盖）。</summary>
    Update,

    /// <summary>库存使用（缓存命中，未访问站点）。</summary>
    Hit,

    /// <summary>未收录跳过。</summary>
    NegativeHit,

    /// <summary>查询未命中（去站点刮削）。</summary>
    Miss,

    /// <summary>缓存读写异常。</summary>
    Error,
}

/// <summary>一条流水：时间 + 类型 + 车牌 + 站点 + 详情 + 目录。</summary>
public sealed record ScrapeLogEvent(
    DateTime At,
    ScrapeLogKind Kind,
    string CarCode,
    string? Source,
    string? Detail,
    string? Folder);

/// <summary>日志读取结果（含全部事件与时间覆盖范围）。</summary>
public sealed record ScrapeLogReadResult(
    IReadOnlyList<ScrapeLogEvent> Events,
    int Files,
    DateTime? From,
    DateTime? To)
{
    public IReadOnlyList<ScrapeLogEvent> InRange(DateTime from, DateTime to) =>
        [.. Events.Where(item => item.At >= from && item.At <= to)];
}

/// <summary>
/// 入库流水解析：扫 <c>data/logs/入库流水-*.log</c>（季度文件）与旧版 <c>缓存日志-*.log</c>。
/// 坏行/缺字段/未知事件直接跳过，不中断；时间以行内时间戳为准（本地时区）。
/// </summary>
public static class ScrapeLogReader
{
    public static ScrapeLogReadResult ReadAll(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return new ScrapeLogReadResult([], 0, null, null);
        }

        var events = new List<ScrapeLogEvent>();
        var files = 0;
        IEnumerable<string> paths;
        try
        {
            paths = Directory.EnumerateFiles(directory, "*.log")
                .Where(IsLogFile)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return new ScrapeLogReadResult([], 0, null, null);
        }

        foreach (var path in paths)
        {
            string[] lines;
            try
            {
                lines = File.ReadAllLines(path);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                continue; // 单个文件读不了不影响其余季度。
            }
            files++;
            foreach (var line in lines)
            {
                if (TryParse(line, out var item))
                {
                    events.Add(item);
                }
            }
        }

        DateTime? from = events.Count == 0 ? null : events.Min(item => item.At);
        DateTime? to = events.Count == 0 ? null : events.Max(item => item.At);
        return new ScrapeLogReadResult(events, files, from, to);
    }

    public static bool TryParse(string line, out ScrapeLogEvent item)
    {
        item = null!;
        var fields = line.Split(" | ");
        if (fields.Length < 6)
        {
            return false;
        }
        if (!DateTime.TryParseExact(fields[0].Trim(), "yyyy-MM-dd HH:mm:ss.fff",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var at))
        {
            return false;
        }
        if (!TryKind(fields[1].Trim(), out var kind))
        {
            return false;
        }
        var code = fields[2].Trim();
        if (code.Length == 0 || code == "-")
        {
            return false;
        }

        item = new ScrapeLogEvent(
            at, kind, code, Dash(fields[3]), Dash(fields[4]), Dash(fields[5]));
        return true;
    }

    private static bool TryKind(string text, out ScrapeLogKind kind)
    {
        switch (text)
        {
            case CacheLogEvents.Add: kind = ScrapeLogKind.Add; return true;
            case CacheLogEvents.Promote: kind = ScrapeLogKind.Promote; return true;
            case CacheLogEvents.Update: kind = ScrapeLogKind.Update; return true;
            case CacheLogEvents.Hit: kind = ScrapeLogKind.Hit; return true;
            case CacheLogEvents.NegativeHit: kind = ScrapeLogKind.NegativeHit; return true;
            case CacheLogEvents.Miss: kind = ScrapeLogKind.Miss; return true;
            case CacheLogEvents.Error: kind = ScrapeLogKind.Error; return true;
            default: kind = default; return false;
        }
    }

    private static string? Dash(string text) =>
        string.IsNullOrWhiteSpace(text) || text.Trim() == "-" ? null : text.Trim();

    public static bool HasLogs(string directory)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory)
                && Directory.EnumerateFiles(directory, "*.log").Any(IsLogFile);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static bool IsLogFile(string path)
    {
        var name = Path.GetFileName(path);
        return name.StartsWith(ScrapeCacheLog.FilePrefix, StringComparison.OrdinalIgnoreCase)
            || name.StartsWith(ScrapeCacheLog.LegacyFilePrefix, StringComparison.OrdinalIgnoreCase);
    }
}
