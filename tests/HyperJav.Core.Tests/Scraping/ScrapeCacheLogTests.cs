using HyperJav.Core.Scraping;
using HyperJav.Core.Tests.Organizing; // TempDir

namespace HyperJav.Core.Tests.Scraping;

public class ScrapeCacheLogTests
{
    [Fact]
    public void Quarterly_file_names_use_quarter_start_month()
    {
        Assert.Equal("入库流水-202601.log", ScrapeCacheLog.FileNameFor(new DateOnly(2026, 1, 1)));
        Assert.Equal("入库流水-202601.log", ScrapeCacheLog.FileNameFor(new DateOnly(2026, 3, 31)));
        Assert.Equal("入库流水-202604.log", ScrapeCacheLog.FileNameFor(new DateOnly(2026, 4, 1)));
        Assert.Equal("入库流水-202607.log", ScrapeCacheLog.FileNameFor(new DateOnly(2026, 7, 15)));
        Assert.Equal("入库流水-202610.log", ScrapeCacheLog.FileNameFor(new DateOnly(2026, 10, 25)));
        Assert.Equal("入库流水-202610.log", ScrapeCacheLog.FileNameFor(new DateOnly(2026, 12, 31)));
        Assert.Equal("入库流水-202701.log", ScrapeCacheLog.FileNameFor(new DateOnly(2027, 2, 1)));
        Assert.Equal(new DateOnly(2026, 10, 1), ScrapeCacheLog.QuarterStart(new DateOnly(2026, 12, 31)));
    }

    [Fact]
    public void Writes_fixed_six_field_lines_and_run_summary()
    {
        using var temp = new TempDir($"cache-log-{Guid.NewGuid():N}");
        var directory = temp.Rel("logs");

        using (var log = new ScrapeCacheLog(directory))
        {
            log.Event(CacheLogEvents.Hit, "ABC-001", "Javbus有码", "#3", "ABC-001");
            log.Event(CacheLogEvents.RunEnd, detail: new CacheRunStats
            {
                Total = 5,
                Hits = 3,
                NegativeHits = 1,
                Misses = 1,
                Added = 1,
            }.Summary);
        }

        var path = Path.Combine(directory, ScrapeCacheLog.FileNameFor(DateOnly.FromDateTime(DateTime.Now)));
        Assert.True(File.Exists(path));
        var lines = File.ReadAllLines(path);
        Assert.Equal(2, lines.Length);
        Assert.Contains("| HIT | ABC-001 | Javbus有码 | #3 | ABC-001", lines[0]);
        Assert.Contains("total=5 pos=3 neg=1 miss=1", lines[1]);
    }

    [Fact]
    public void Reopened_log_appends_to_same_quarter_file()
    {
        using var temp = new TempDir($"cache-log-append-{Guid.NewGuid():N}");
        var directory = temp.Rel("logs");
        using (var log = new ScrapeCacheLog(directory))
        {
            log.Event(CacheLogEvents.Miss, "ABC-002");
        }
        using (var log = new ScrapeCacheLog(directory))
        {
            log.Event(CacheLogEvents.Add, "ABC-002", "Javlibrary");
        }

        var path = Path.Combine(directory, ScrapeCacheLog.FileNameFor(DateOnly.FromDateTime(DateTime.Now)));
        Assert.Equal(2, File.ReadAllLines(path).Length);
    }
}
