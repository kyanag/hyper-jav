using System.Text;

namespace HyperJav.Core.Scraping;

/// <summary>缓存日志事件名（管线写入 / 报表解析共用，避免字符串漂移）。</summary>
public static class CacheLogEvents
{
    public const string RunStart = "RUN-START";
    public const string Hit = "HIT";
    public const string NegativeHit = "NEG-HIT";
    public const string Miss = "MISS";
    public const string Add = "ADD";
    public const string Update = "UPDATE";
    public const string Promote = "PROMOTE";
    public const string AddNegative = "ADD-NEG";
    public const string RefreshNegative = "NEG-REFRESH";
    public const string Kept = "KEPT";
    public const string Error = "ERR";
    public const string RunEnd = "RUN-END";
}

/// <summary>一次运行的缓存计数（RUN-END 行内容，也用于 GUI 结束提示）。</summary>
public sealed class CacheRunStats
{
    public int Total { get; set; }
    public int Hits { get; set; }
    public int NegativeHits { get; set; }
    public int Misses { get; set; }
    public int Added { get; set; }
    public int Updated { get; set; }
    public int Promoted { get; set; }
    public int NegativeAdded { get; set; }
    public int Kept { get; set; }
    public int Errors { get; set; }

    public int Cached => Hits + NegativeHits;

    /// <summary>本次运行落库的条数（新增 + 刷新 + 转正 + 负缓存）。</summary>
    public int Writes => Added + Updated + Promoted + NegativeAdded;

    public string Summary =>
        $"total={Total} pos={Hits} neg={NegativeHits} miss={Misses} add={Added} upd={Updated} "
        + $"promo={Promoted} negadd={NegativeAdded} kept={Kept} err={Errors}";
}

/// <summary>
/// 入库流水日志：<c>data/logs/入库流水-YYYYMM.log</c>（按季度滚动，文件名用季度起始月：
/// 01/04/07/10），UTF-8 追加。单行固定 6 段（时间 | 事件 | 车牌 | 站点 | 详情 | 目录），
/// 人可读、报表可直接解析。旧版 <c>缓存日志-*.log</c>（按天/按季度）仍可被报表读取。
/// 日志写失败绝不影响整理。
/// </summary>
public sealed class ScrapeCacheLog : IDisposable
{
    public const string FilePrefix = "入库流水-";

    /// <summary>旧版日志前缀（继续解析，不再写入）。</summary>
    public const string LegacyFilePrefix = "缓存日志-";

    private readonly string _directory;
    private readonly object _gate = new();
    private StreamWriter? _writer;
    private DateOnly _openQuarter;

    public ScrapeCacheLog(string directory) => _directory = directory;

    public string Directory => _directory;

    /// <summary>所在季度的起始月（1/4/7/10）；文件名即 <c>缓存日志-{yyyyMM}.log</c>。</summary>
    public static DateOnly QuarterStart(DateOnly day) =>
        new(day.Year, (day.Month - 1) / 3 * 3 + 1, 1);

    public static string FileNameFor(DateOnly day) => $"{FilePrefix}{QuarterStart(day):yyyyMM}.log";

    public void Event(
        string name, string? code = null, string? source = null, string? detail = null, string? folder = null)
    {
        lock (_gate)
        {
            try
            {
                var now = DateTime.Now;
                var writer = WriterFor(DateOnly.FromDateTime(now));
                writer.WriteLine(
                    $"{now:yyyy-MM-dd HH:mm:ss.fff} | {name} | {Clean(code)} | {Clean(source)} | {Clean(detail)} | {Clean(folder)}");
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // 日志不可写不影响整理结果。
            }
        }
    }

    private StreamWriter WriterFor(DateOnly day)
    {
        var quarter = QuarterStart(day);
        if (_writer is not null && quarter == _openQuarter)
        {
            return _writer;
        }

        _writer?.Dispose();
        System.IO.Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, FileNameFor(day));
        _writer = new StreamWriter(
            new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = true,
        };
        _openQuarter = quarter;
        return _writer;
    }

    private static string Clean(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? "-"
            : value.Replace('\r', ' ').Replace('\n', ' ').Replace(" | ", "/").Trim();

    public void Dispose()
    {
        lock (_gate)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }
}
