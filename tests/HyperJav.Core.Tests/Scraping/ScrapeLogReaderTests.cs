using HyperJav.Core.Scraping;
using HyperJav.Core.Tests.Organizing; // TempDir

namespace HyperJav.Core.Tests.Scraping;

public class ScrapeLogReaderTests
{
    private static string Line(string at, string name, string code, string source = "-", string folder = "-") =>
        $"{at} | {name} | {code} | {source} | - | {folder}";

    [Fact]
    public void Reads_quarterly_and_legacy_files_and_skips_malformed_lines()
    {
        using var temp = new TempDir($"log-reader-{Guid.NewGuid():N}");
        var directory = temp.Rel("logs");
        File.WriteAllLines(Path.Combine(directory, "入库流水-202610.log"),
        [
            Line("2026-10-01 09:00:00.000", CacheLogEvents.Add, "ABC-001", "Javlibrary", "ABC-001"),
            Line("2026-10-02 09:00:00.000", CacheLogEvents.Hit, "ABC-001", "Javlibrary", "ABC-001"),
            "not a log line",
            "2026-10-03 09:00:00.000 | RUN-END | - | - | total=1 | -",
            "2026-10-04 09:00:00.000 | ADD | ABC-002 | Javlibrary",
        ]);
        // 旧版按天文件（兼容）。
        File.WriteAllLines(Path.Combine(directory, "缓存日志-20261003.log"),
        [
            Line("2026-10-03 10:00:00.000", CacheLogEvents.Promote, "XYZ-003", "Javbus有码", "XYZ-003"),
        ]);

        var result = ScrapeLogReader.ReadAll(directory);

        Assert.Equal(2, result.Files);
        Assert.Equal(3, result.Events.Count);
        Assert.Equal(
            [ScrapeLogKind.Add, ScrapeLogKind.Hit, ScrapeLogKind.Promote],
            result.Events.Select(item => item.Kind));
        Assert.Equal(new DateTime(2026, 10, 1, 9, 0, 0), result.From);
        Assert.Equal(new DateTime(2026, 10, 3, 10, 0, 0), result.To);

        var range = result.InRange(new DateTime(2026, 10, 2), new DateTime(2026, 10, 3, 23, 59, 59));
        Assert.Equal(2, range.Count);
        Assert.All(range, item => Assert.InRange(item.At, new DateTime(2026, 10, 2), new DateTime(2026, 10, 3, 23, 59, 59)));
    }

    [Fact]
    public void Missing_directory_returns_empty()
    {
        var result = ScrapeLogReader.ReadAll(@"C:\Windows\Temp\opencode\definitely-not-here");
        Assert.Empty(result.Events);
        Assert.Equal(0, result.Files);
        Assert.Null(result.From);
    }
}
