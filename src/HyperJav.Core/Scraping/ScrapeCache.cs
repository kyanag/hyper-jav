using System.Globalization;
using HyperJav.Core.Models;

namespace HyperJav.Core.Scraping;

/// <summary>命中结果：正缓存带回元数据；负缓存只带"未收录"结论。</summary>
public sealed record ScrapeCacheHit(
    MovieMetadata? Metadata,
    bool NotFound,
    string? Source,
    int HitCount,
    int NegativeHitCount);

/// <summary>写库结果（日志据此区分新增/刷新/负转正/拒绝覆盖）。</summary>
public enum CacheStoreResult
{
    /// <summary>新车牌的正缓存。</summary>
    Inserted,

    /// <summary>已有正缓存被新刮到的元数据刷新（命中计数保留）。</summary>
    Refreshed,

    /// <summary>新车牌的负缓存。</summary>
    InsertedNegative,

    /// <summary>已有负缓存再次确认未收录。</summary>
    RefreshedNegative,

    /// <summary>负缓存转正（此前未收录，这次刮到了）。</summary>
    Promoted,

    /// <summary>正缓存存在时拒绝写入负缓存（保护已有元数据）。</summary>
    KeptMetadata,
}

/// <summary>报表用的单行快照。</summary>
public sealed record ScrapeCacheRow(
    string CarCode,
    string Kind,
    string? Source,
    int HitCount,
    int NegativeHitCount,
    DateTime? CreatedAt,
    DateTime? FetchedAt,
    DateTime? LastHitAt,
    DateTime? KindChangedAt)
{
    public bool IsNegative => Kind == "notfound";
}

/// <summary>整库聚合（报表数据源）。</summary>
public sealed record ScrapeCacheStats(
    int PositiveCount,
    int NegativeCount,
    long HitCount,
    long NegativeHitCount,
    int PositiveNeverHit,
    int PromotedCount,
    IReadOnlyList<(string Source, int Count)> BySource,
    IReadOnlyList<ScrapeCacheRow> TopHits,
    DateTime? EarliestCreatedAt,
    DateTime? LatestFetchedAt);

/// <summary>
/// 刮削缓存（SQLite，单库文件 data/scrape-cache.db）。已识别车牌的元数据/未收录结论落库，
/// 下次遇到直接读取，不再访问站点。缓存等级由调用方决定（完全不使用 = 不构造）。
/// <para>
/// 计数口径：<c>hit_count</c> = 作为正缓存被使用的次数；<c>neg_hit_count</c> = 作为负缓存被跳过的次数。
/// 负→正时正计数归零（负计数保留为历史），并记录 <c>kind_changed_at</c>；正缓存永不被"未收录"覆盖。
/// </para>
/// </summary>
public sealed class ScrapeCache : IDisposable
{
    private const string KindMetadata = "metadata";
    private const string KindNotFound = "notfound";

    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;

    public ScrapeCache(string dbPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(dbPath))!);
        _connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}");
        _connection.Open();
        EnsureSchema();
    }

    /// <summary>旧库迁移：建表（新库）/ 补列（老库）/ 回填首次入库时间。绝不重建表，不丢已有条目。</summary>
    private void EnsureSchema()
    {
        using (var create = Command("""
            CREATE TABLE IF NOT EXISTS scrape_cache (
                car_code        TEXT PRIMARY KEY,
                kind            TEXT NOT NULL,                 -- 'metadata' / 'notfound'
                json            TEXT,                          -- 元数据 JSON（负缓存为 null）
                source          TEXT,
                fetched_at      TEXT NOT NULL,                 -- 元数据抓取时间（命中不改）
                created_at      TEXT,                          -- 首次入库时间（转正也不改）
                hit_count       INTEGER NOT NULL DEFAULT 0,    -- 正缓存命中次数（转正归零）
                neg_hit_count   INTEGER NOT NULL DEFAULT 0,    -- 负缓存跳过次数（转正保留）
                last_hit_at     TEXT,                          -- 最后一次被使用的时间
                kind_changed_at TEXT                           -- 最近一次负→正的时间
            );
            """))
        {
            create.ExecuteNonQuery();
        }

        var columns = ColumnNames();
        AddColumn(columns, "created_at", "TEXT");
        AddColumn(columns, "hit_count", "INTEGER NOT NULL DEFAULT 0");
        AddColumn(columns, "neg_hit_count", "INTEGER NOT NULL DEFAULT 0");
        AddColumn(columns, "last_hit_at", "TEXT");
        AddColumn(columns, "kind_changed_at", "TEXT");

        // 老库没有 created_at：用抓取时间回填，报表的"最早入库"才有意义。
        using var backfill = Command("UPDATE scrape_cache SET created_at = fetched_at WHERE created_at IS NULL");
        backfill.ExecuteNonQuery();
    }

    private HashSet<string> ColumnNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var command = Command("PRAGMA table_info(scrape_cache)");
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            names.Add(reader.GetString(1));
        }
        return names;
    }

    private void AddColumn(HashSet<string> existing, string name, string definition)
    {
        if (existing.Contains(name))
        {
            return;
        }
        using var command = Command($"ALTER TABLE scrape_cache ADD COLUMN {name} {definition}");
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// 查询并计数：正缓存命中 → <c>hit_count + 1</c>；负缓存仅在 <paramref name="countNegativeHit"/> 时计
    /// <c>neg_hit_count + 1</c>（"只使用正缓存"档重新访问站点，不算一次跳过）。未命中返回 null。
    /// </summary>
    public ScrapeCacheHit? Lookup(string carCode, bool countNegativeHit)
    {
        using var transaction = _connection.BeginTransaction();

        string kind;
        string? source;
        using (var select = Command("SELECT kind, source FROM scrape_cache WHERE car_code = $code", transaction))
        {
            select.Parameters.AddWithValue("$code", carCode);
            using var reader = select.ExecuteReader();
            if (!reader.Read())
            {
                return null;
            }
            kind = reader.GetString(0);
            source = reader.IsDBNull(1) ? null : reader.GetString(1);
        }

        var now = Now();
        if (kind == KindMetadata)
        {
            using (var update = Command(
                "UPDATE scrape_cache SET hit_count = hit_count + 1, last_hit_at = $now WHERE car_code = $code",
                transaction))
            {
                update.Parameters.AddWithValue("$code", carCode);
                update.Parameters.AddWithValue("$now", now);
                update.ExecuteNonQuery();
            }

            string json;
            int hitCount;
            int negativeHitCount;
            using (var read = Command(
                "SELECT json, hit_count, neg_hit_count FROM scrape_cache WHERE car_code = $code", transaction))
            {
                read.Parameters.AddWithValue("$code", carCode);
                using var reader = read.ExecuteReader();
                reader.Read();
                json = reader.GetString(0);
                hitCount = reader.GetInt32(1);
                negativeHitCount = reader.GetInt32(2);
            }

            var hit = new ScrapeCacheHit(
                ScrapeCacheJson.Deserialize(json), false, source, hitCount, negativeHitCount);
            transaction.Commit();
            return hit;
        }

        if (countNegativeHit)
        {
            using var update = Command(
                "UPDATE scrape_cache SET neg_hit_count = neg_hit_count + 1, last_hit_at = $now WHERE car_code = $code",
                transaction);
            update.Parameters.AddWithValue("$code", carCode);
            update.Parameters.AddWithValue("$now", now);
            update.ExecuteNonQuery();
        }

        int positives;
        int negatives;
        using (var read = Command(
            "SELECT hit_count, neg_hit_count FROM scrape_cache WHERE car_code = $code", transaction))
        {
            read.Parameters.AddWithValue("$code", carCode);
            using var reader = read.ExecuteReader();
            reader.Read();
            positives = reader.GetInt32(0);
            negatives = reader.GetInt32(1);
        }

        var negativeHit = new ScrapeCacheHit(null, true, source, positives, negatives);
        transaction.Commit();
        return negativeHit;
    }

    /// <summary>只读查询（报表/测试用，不计数）。命中正缓存返回元数据；负缓存返回 null 且 notFound=true。</summary>
    public MovieMetadata? Find(string carCode, out bool notFound)
    {
        notFound = false;
        using var command = Command("SELECT kind, json FROM scrape_cache WHERE car_code = $code");
        command.Parameters.AddWithValue("$code", carCode);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }
        if (reader.GetString(0) == KindNotFound)
        {
            notFound = true;
            return null;
        }
        return ScrapeCacheJson.Deserialize(reader.GetString(1));
    }

    /// <summary>写入正缓存。负→正记 <see cref="CacheStoreResult.Promoted"/>（正计数归零、负计数保留）。</summary>
    public CacheStoreResult Store(MovieMetadata metadata)
    {
        var previous = KindOf(metadata.CarCode);
        var now = Now();

        using var transaction = _connection.BeginTransaction();
        using (var command = Command("""
            INSERT INTO scrape_cache (car_code, kind, json, source, fetched_at, created_at)
            VALUES ($code, 'metadata', $json, $source, $now, $now)
            ON CONFLICT(car_code) DO UPDATE SET
                kind = 'metadata',
                json = $json,
                source = $source,
                fetched_at = $now,
                hit_count = CASE WHEN scrape_cache.kind = 'metadata' THEN scrape_cache.hit_count ELSE 0 END,
                kind_changed_at = CASE
                    WHEN scrape_cache.kind = 'metadata' THEN scrape_cache.kind_changed_at
                    ELSE $now
                END
            """, transaction))
        {
            command.Parameters.AddWithValue("$code", metadata.CarCode);
            command.Parameters.AddWithValue("$json", ScrapeCacheJson.Serialize(metadata));
            command.Parameters.AddWithValue(
                "$source", (object?)ScrapeProbeResult.LabelOf(metadata.SourceSite) ?? DBNull.Value);
            command.Parameters.AddWithValue("$now", now);
            command.ExecuteNonQuery();
        }
        transaction.Commit();

        return previous switch
        {
            null => CacheStoreResult.Inserted,
            KindMetadata => CacheStoreResult.Refreshed,
            _ => CacheStoreResult.Promoted,
        };
    }

    /// <summary>写入负缓存。已有正缓存时拒绝覆盖（返回 <see cref="CacheStoreResult.KeptMetadata"/>）。</summary>
    public CacheStoreResult StoreNotFound(string carCode, string? sourceLabel)
    {
        var previous = KindOf(carCode);
        if (previous == KindMetadata)
        {
            return CacheStoreResult.KeptMetadata; // 站点抽风不能抹掉好数据。
        }

        var now = Now();
        using var transaction = _connection.BeginTransaction();
        using (var command = Command("""
            INSERT INTO scrape_cache (car_code, kind, json, source, fetched_at, created_at)
            VALUES ($code, 'notfound', NULL, $source, $now, $now)
            ON CONFLICT(car_code) DO UPDATE SET
                kind = 'notfound',
                json = NULL,
                source = $source,
                fetched_at = $now
            """, transaction))
        {
            command.Parameters.AddWithValue("$code", carCode);
            command.Parameters.AddWithValue("$source", (object?)sourceLabel ?? DBNull.Value);
            command.Parameters.AddWithValue("$now", now);
            command.ExecuteNonQuery();
        }
        transaction.Commit();

        return previous is null ? CacheStoreResult.InsertedNegative : CacheStoreResult.RefreshedNegative;
    }

    /// <summary>读取全部正缓存元数据（报表 join 用；一次查询，不计数）。</summary>
    public IReadOnlyDictionary<string, MovieMetadata> ReadMetadata()
    {
        var result = new Dictionary<string, MovieMetadata>(StringComparer.OrdinalIgnoreCase);
        using var command = Command(
            "SELECT car_code, json FROM scrape_cache WHERE kind = 'metadata' AND json IS NOT NULL");
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            MovieMetadata? metadata;
            try
            {
                metadata = ScrapeCacheJson.Deserialize(reader.GetString(1));
            }
            catch (System.Text.Json.JsonException)
            {
                continue; // 单条元数据损坏不拖垮整份报表。
            }
            if (metadata is not null)
            {
                result[reader.GetString(0)] = metadata;
            }
        }
        return result;
    }

    /// <summary>只读单行快照（诊断/测试用，不计数）。</summary>
    public ScrapeCacheRow? GetRow(string carCode)
    {
        using var command = Command("""
            SELECT car_code, kind, source, hit_count, neg_hit_count,
                   created_at, fetched_at, last_hit_at, kind_changed_at
            FROM scrape_cache WHERE car_code = $code
            """);
        command.Parameters.AddWithValue("$code", carCode);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadRow(reader) : null;
    }

    public int Count => ScalarInt("SELECT COUNT(*) FROM scrape_cache");

    /// <summary>整库聚合（报表用；一次读库，不做任何写入）。</summary>
    public ScrapeCacheStats ReadStats(int top = 10)
    {
        var positive = ScalarInt("SELECT COUNT(*) FROM scrape_cache WHERE kind = 'metadata'");
        var negative = ScalarInt("SELECT COUNT(*) FROM scrape_cache WHERE kind = 'notfound'");
        long hits = ScalarLong("SELECT COALESCE(SUM(hit_count), 0) FROM scrape_cache");
        long negativeHits = ScalarLong("SELECT COALESCE(SUM(neg_hit_count), 0) FROM scrape_cache");
        var neverHit = ScalarInt("SELECT COUNT(*) FROM scrape_cache WHERE kind = 'metadata' AND hit_count = 0");
        var promoted = ScalarInt(
            "SELECT COUNT(*) FROM scrape_cache WHERE kind = 'metadata' AND kind_changed_at IS NOT NULL");

        var bySource = new List<(string Source, int Count)>();
        using (var command = Command("""
            SELECT COALESCE(source, '（未知）'), COUNT(*) FROM scrape_cache
            GROUP BY source ORDER BY COUNT(*) DESC, source
            """))
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                bySource.Add((reader.GetString(0), reader.GetInt32(1)));
            }
        }

        var topHits = new List<ScrapeCacheRow>();
        using (var command = Command("""
            SELECT car_code, kind, source, hit_count, neg_hit_count,
                   created_at, fetched_at, last_hit_at, kind_changed_at
            FROM scrape_cache
            WHERE kind = 'metadata' AND hit_count > 0
            ORDER BY hit_count DESC, car_code
            LIMIT $top
            """))
        {
            command.Parameters.AddWithValue("$top", Math.Clamp(top, 1, 100));
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                topHits.Add(ReadRow(reader));
            }
        }

        DateTime? earliest = null;
        DateTime? latest = null;
        using (var command = Command("SELECT MIN(created_at), MAX(fetched_at) FROM scrape_cache"))
        using (var reader = command.ExecuteReader())
        {
            if (reader.Read())
            {
                earliest = reader.IsDBNull(0) ? null : ParseTime(reader.GetString(0));
                latest = reader.IsDBNull(1) ? null : ParseTime(reader.GetString(1));
            }
        }

        return new ScrapeCacheStats(
            positive, negative, hits, negativeHits, neverHit, promoted,
            bySource, topHits, earliest, latest);
    }

    private ScrapeCacheRow ReadRow(Microsoft.Data.Sqlite.SqliteDataReader reader) => new(
        reader.GetString(0),
        reader.GetString(1),
        reader.IsDBNull(2) ? null : reader.GetString(2),
        reader.GetInt32(3),
        reader.GetInt32(4),
        reader.IsDBNull(5) ? null : ParseTime(reader.GetString(5)),
        reader.IsDBNull(6) ? null : ParseTime(reader.GetString(6)),
        reader.IsDBNull(7) ? null : ParseTime(reader.GetString(7)),
        reader.IsDBNull(8) ? null : ParseTime(reader.GetString(8)));

    private string? KindOf(string carCode)
    {
        using var command = Command("SELECT kind FROM scrape_cache WHERE car_code = $code");
        command.Parameters.AddWithValue("$code", carCode);
        return command.ExecuteScalar() as string;
    }

    private int ScalarInt(string sql)
    {
        using var command = Command(sql);
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private long ScalarLong(string sql)
    {
        using var command = Command(sql);
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private Microsoft.Data.Sqlite.SqliteCommand Command(
        string sql, Microsoft.Data.Sqlite.SqliteTransaction? transaction = null)
    {
        var command = _connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        return command;
    }

    private static string Now() => DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

    private static DateTime? ParseTime(string text) =>
        DateTime.TryParse(text, CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind | DateTimeStyles.AllowWhiteSpaces, out var value)
            ? value
            : null;

    public void Dispose() => _connection.Dispose();
}

/// <summary>元数据序列化（独立静态类，测试可直接断言往返）。</summary>
public static class ScrapeCacheJson
{
    public static string Serialize(MovieMetadata metadata) =>
        System.Text.Json.JsonSerializer.Serialize(metadata, Options);

    public static MovieMetadata? Deserialize(string json) =>
        System.Text.Json.JsonSerializer.Deserialize<MovieMetadata>(json, Options);

    private static System.Text.Json.JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = false,
    };
}
