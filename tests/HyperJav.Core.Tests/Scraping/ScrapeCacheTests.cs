using HyperJav.Core.Models;
using HyperJav.Core.Scraping;
using HyperJav.Core.Tests.Organizing; // TempDir

namespace HyperJav.Core.Tests.Scraping;

public class ScrapeCacheTests
{
    private static MovieMetadata Sample(string code = "XYZ-001") => new()
    {
        CarCode = code,
        Title = "鏍囬",
        ReleaseDate = new DateOnly(2024, 1, 2),
        RuntimeMinutes = 120,
        Rating = 8.5,
        Actors = ["婕斿憳A", "婕斿憳B"],
        RawFeatures = ["鐔熷コ"],
        ExtraImages = ["https://img.example/1.jpg"],
        SourceSite = ScrapeSource.JavbusYouma,
        Subtitle = true,
        Leak = false,
    };

    [Fact]
    public void Roundtrip_metadata_between_instances()
    {
        using var temp = new TempDir($"scrape-cache-{Guid.NewGuid():N}");
        var dbPath = temp.Rel("scrape-cache.db");

        using (var cache = new ScrapeCache(dbPath))
        {
            cache.Store(Sample());
            Assert.Equal(1, cache.Count);
        }

        using var reopened = new ScrapeCache(dbPath);
        var found = reopened.Find("XYZ-001", out var notFound);
        Assert.False(notFound);
        Assert.NotNull(found);
        Assert.Equal("鏍囬", found!.Title);
        Assert.Equal(new DateOnly(2024, 1, 2), found.ReleaseDate);
        Assert.Equal(2, found.Actors.Count);
        Assert.Equal(ScrapeSource.JavbusYouma, found.SourceSite);
    }

    [Fact]
    public void Notfound_negative_cache_persists()
    {
        using var temp = new TempDir($"scrape-cache-{Guid.NewGuid():N}");
        var dbPath = temp.Rel("scrape-cache.db");

        using (var cache = new ScrapeCache(dbPath))
        {
            cache.StoreNotFound("XYZ-002", "Javbus鏈夌爜");
        }

        using var reopened = new ScrapeCache(dbPath);
        var found = reopened.Find("XYZ-002", out var notFound);
        Assert.Null(found);
        Assert.True(notFound);
    }

    [Fact]
    public void Miss_returns_null_notfound_false()
    {
        using var temp = new TempDir($"scrape-cache-{Guid.NewGuid():N}");
        using var cache = new ScrapeCache(temp.Rel("scrape-cache.db"));
        var found = cache.Find("NOPE-000", out var notFound);
        Assert.Null(found);
        Assert.False(notFound);
    }

    [Fact]
    public void Upsert_overwrites_same_code()
    {
        using var temp = new TempDir($"scrape-cache-{Guid.NewGuid():N}");
        using var cache = new ScrapeCache(temp.Rel("scrape-cache.db"));
        cache.StoreNotFound("XYZ-003", null);
        cache.Store(Sample("XYZ-003"));

        var found = cache.Find("XYZ-003", out var notFound);
        Assert.False(notFound);
        Assert.NotNull(found);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void Old_schema_migrates_without_losing_rows()
    {
        using var temp = new TempDir($"cache-migrate-{Guid.NewGuid():N}");
        var dbPath = temp.Rel("scrape-cache.db");
        var json = ScrapeCacheJson.Serialize(Sample("OLD-001"));

        // 模拟上线前的五列库（没有计数/时间列）。
        using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE scrape_cache (
                    car_code TEXT PRIMARY KEY, kind TEXT NOT NULL, json TEXT, source TEXT, fetched_at TEXT NOT NULL);
                INSERT INTO scrape_cache (car_code, kind, json, source, fetched_at)
                VALUES ('OLD-001', 'metadata', $json, 'JavbusYouma', '2026-01-02T03:04:05.0000000Z');
                """;
            command.Parameters.AddWithValue("$json", json);
            command.ExecuteNonQuery();
        }

        using var cache = new ScrapeCache(dbPath);
        var found = cache.Find("OLD-001", out var notFound);
        Assert.False(notFound);
        Assert.Equal("OLD-001", found!.CarCode);
        Assert.Equal(1, cache.Count);

        var row = cache.GetRow("OLD-001");
        Assert.NotNull(row);
        Assert.Equal(0, row!.HitCount);
        Assert.Equal(0, row.NegativeHitCount);
        Assert.NotNull(row.CreatedAt); // 老库用 fetched_at 回填
    }

    [Fact]
    public void Lookup_increments_positive_hit_count_and_keeps_fetched_at()
    {
        using var temp = new TempDir($"cache-hit-{Guid.NewGuid():N}");
        using var cache = new ScrapeCache(temp.Rel("scrape-cache.db"));
        cache.Store(Sample());
        var fetchedAt = cache.GetRow("XYZ-001")!.FetchedAt;

        var first = cache.Lookup("XYZ-001", countNegativeHit: true);
        Assert.NotNull(first);
        Assert.NotNull(first!.Metadata);
        Assert.False(first.NotFound);
        Assert.Equal(1, first.HitCount);

        var second = cache.Lookup("XYZ-001", countNegativeHit: true);
        Assert.Equal(2, second!.HitCount);

        var row = cache.GetRow("XYZ-001")!;
        Assert.Equal(2, row.HitCount);
        Assert.NotNull(row.LastHitAt);
        Assert.Equal(fetchedAt, row.FetchedAt); // 命中不改抓取时间
    }

    [Fact]
    public void Negative_hits_count_only_when_requested()
    {
        using var temp = new TempDir($"cache-neg-{Guid.NewGuid():N}");
        using var cache = new ScrapeCache(temp.Rel("scrape-cache.db"));
        cache.StoreNotFound("XYZ-001", "Javbus有码");

        var ignored = cache.Lookup("XYZ-001", countNegativeHit: false);
        Assert.NotNull(ignored);
        Assert.True(ignored!.NotFound);
        Assert.Equal(0, ignored.NegativeHitCount);
        Assert.Null(cache.GetRow("XYZ-001")!.LastHitAt); // 忽略不算使用

        var counted = cache.Lookup("XYZ-001", countNegativeHit: true);
        Assert.True(counted!.NotFound);
        Assert.Equal(1, counted.NegativeHitCount);
        Assert.NotNull(cache.GetRow("XYZ-001")!.LastHitAt);
    }

    [Fact]
    public void Promote_negative_to_positive_keeps_history_and_resets_positive_count()
    {
        using var temp = new TempDir($"cache-promote-{Guid.NewGuid():N}");
        using var cache = new ScrapeCache(temp.Rel("scrape-cache.db"));
        cache.StoreNotFound("XYZ-001", "Javbus有码");
        cache.Lookup("XYZ-001", countNegativeHit: true);
        cache.Lookup("XYZ-001", countNegativeHit: true);

        var result = cache.Store(Sample());
        Assert.Equal(CacheStoreResult.Promoted, result);

        var row = cache.GetRow("XYZ-001")!;
        Assert.Equal("metadata", row.Kind);
        Assert.Equal(0, row.HitCount);          // 正计数从零开始
        Assert.Equal(2, row.NegativeHitCount);  // 负缓存历史保留
        Assert.NotNull(row.KindChangedAt);

        var stats = cache.ReadStats();
        Assert.Equal(1, stats.PositiveCount);
        Assert.Equal(0, stats.NegativeCount);
        Assert.Equal(1, stats.PromotedCount);
        Assert.Equal(2, stats.NegativeHitCount);
        Assert.Equal(0, stats.HitCount);
    }

    [Fact]
    public void Negative_store_never_overwrites_metadata()
    {
        using var temp = new TempDir($"cache-keep-{Guid.NewGuid():N}");
        using var cache = new ScrapeCache(temp.Rel("scrape-cache.db"));
        cache.Store(Sample());

        var result = cache.StoreNotFound("XYZ-001", "Javbus有码");
        Assert.Equal(CacheStoreResult.KeptMetadata, result);

        var found = cache.Find("XYZ-001", out var notFound);
        Assert.False(notFound);
        Assert.NotNull(found);
    }
}
