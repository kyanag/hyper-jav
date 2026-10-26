using System.Text;
using System.Text.Json;
using HyperJav.Core.Configuration;

namespace HyperJav.Core.Localization;

/// <summary>可翻译的元数据类别（只影响报表显示，不改数据库、不改文件夹名）。</summary>
public enum TranslationKind
{
    Category,
    Studio,
    Series,
    Actor,
    Feature,
}

/// <summary>
/// 报表显示译名表（默认 data/translations.json）。
/// 结构：<c>{ "片商": { "日文原名": "中文名" }, "系列": { ... } }</c>；
/// 缺文件、坏文件、缺项都回退原文，读取失败不会中断报表生成。
/// </summary>
public sealed class TranslationCatalog
{
    public const string FileName = "translations.json";

    private const long MaxBytes = 16 * 1024 * 1024;
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly Dictionary<TranslationKind, Dictionary<string, string>> _maps;

    private TranslationCatalog(
        Dictionary<TranslationKind, Dictionary<string, string>> maps,
        string? sourcePath,
        string? loadError)
    {
        _maps = maps;
        SourcePath = sourcePath;
        LoadError = loadError;
        Count = maps.Values.Sum(map => map.Count);
    }

    /// <summary>没有任何译名（所有项都显示原文）。</summary>
    public static TranslationCatalog Empty { get; } =
        new([], sourcePath: null, loadError: null);

    /// <summary>实际尝试读取的文件；未传路径时为 null（文件不存在时仍保留路径，便于界面提示）。</summary>
    public string? SourcePath { get; }

    /// <summary>读取/解析失败的原因（仅类型或行号，不含文件内容）；成功时为 null。</summary>
    public string? LoadError { get; }

    /// <summary>已加载的译名条数。</summary>
    public int Count { get; }

    public static string DefaultPathFor(string dataDirectory) => Path.Combine(dataDirectory, FileName);

    /// <summary>读取译名表；文件不存在、过大、JSON 损坏都返回空表并记录原因。</summary>
    public static TranslationCatalog Load(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return new TranslationCatalog([], path, loadError: null);
        }

        try
        {
            if (new FileInfo(path).Length > MaxBytes)
            {
                return new TranslationCatalog([], path, "文件过大（超过 16 MB），已忽略。");
            }
            var text = IniTextCodec.DetectAndDecode(File.ReadAllBytes(path)).Text;
            return Parse(text, path);
        }
        catch (JsonException error)
        {
            return new TranslationCatalog([], path, $"JSON 格式错误：{error.Message}");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return new TranslationCatalog([], path, $"读取失败：{error.GetType().Name}");
        }
    }

    /// <summary>文件不存在时写一份空模板（带说明），已存在则不动。</summary>
    public static void EnsureFile(string path)
    {
        if (File.Exists(path))
        {
            return;
        }
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(path, Template, Utf8NoBom);
    }

    /// <summary>按类别翻译单个值；未配置、值为空或译文为空时原样返回。</summary>
    public string Translate(TranslationKind? kind, string? value)
    {
        if (kind is not { } actual || string.IsNullOrWhiteSpace(value))
        {
            return value ?? "";
        }
        var key = NormalizeKey(value);
        if (key.Length == 0)
        {
            return value;
        }
        return _maps.TryGetValue(actual, out var map) && map.TryGetValue(key, out var translated)
            ? translated
            : value;
    }

    /// <summary>逐个翻译后用「、」连接（演员/特征这类一对多字段）。</summary>
    public string TranslateList(TranslationKind? kind, IEnumerable<string> values) =>
        string.Join("、", values.Select(value => Translate(kind, value)));

    private static TranslationCatalog Parse(string text, string path)
    {
        using var document = JsonDocument.Parse(text, new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip,
        });
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return new TranslationCatalog([], path, "根节点必须是对象。");
        }

        var maps = new Dictionary<TranslationKind, Dictionary<string, string>>();
        foreach (var property in root.EnumerateObject())
        {
            if (!TryKind(property.Name, out var kind) || property.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }
            if (!maps.TryGetValue(kind, out var map))
            {
                map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                maps[kind] = map;
            }
            foreach (var entry in property.Value.EnumerateObject())
            {
                var key = NormalizeKey(entry.Name);
                if (key.Length == 0 || entry.Value.ValueKind != JsonValueKind.String)
                {
                    continue;
                }
                var translated = entry.Value.GetString();
                if (!string.IsNullOrWhiteSpace(translated))
                {
                    map[key] = translated.Trim();
                }
            }
        }
        return new TranslationCatalog(maps, path, loadError: null);
    }

    /// <summary>类别键：支持中文（报表上的叫法）和英文别名。</summary>
    private static bool TryKind(string name, out TranslationKind kind)
    {
        switch (NormalizeKey(name).ToLowerInvariant())
        {
            case "类别" or "分类" or "category" or "type":
                kind = TranslationKind.Category;
                return true;
            case "片商" or "制片商" or "厂牌" or "studio" or "maker":
                kind = TranslationKind.Studio;
                return true;
            case "系列" or "series":
                kind = TranslationKind.Series;
                return true;
            case "演员" or "女优" or "女優" or "actor" or "actors" or "actress":
                kind = TranslationKind.Actor;
                return true;
            case "特征" or "特征类型" or "类型" or "标签" or "feature" or "features" or "tag" or "tags":
                kind = TranslationKind.Feature;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    /// <summary>匹配键：NFKC（全角→半角等）+ 去首尾空白 + 合并连续空白。</summary>
    private static string NormalizeKey(string value)
    {
        var text = value.Normalize(NormalizationForm.FormKC).Trim();
        return text.Contains("  ", StringComparison.Ordinal)
            ? string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            : text;
    }

    private const string Template = """
        {
          "_说明": [
            "片商 / 系列 / 演员 / 特征标签 / 类别 的中文显示名。只影响入库报表的显示，不改数据库、不改文件夹名。",
            "键写报表里出现的日文原文，值写要显示的中文，例如 \"片商\": { \"日文原名\": \"中文名\" }。",
            "找不到或值留空的项自动显示原文；同一原文只需写一次。",
            "保存后重新生成报表即可生效（旧报表文件不会自动更新）。"
          ],
          "类别": {},
          "片商": {},
          "系列": {},
          "演员": {},
          "特征": {}
        }
        """;
}
