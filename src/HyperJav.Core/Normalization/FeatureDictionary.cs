using System.Text.Json;

namespace HyperJav.Core.Normalization;

/// <summary>特征字典条目（【特征对照表】.xlsx 转出的 json 行：source/simplified/traditional/japanese）。</summary>
public sealed record FeatureEntry(
    string Source,
    string? Simplified,
    string? Traditional,
    string? Japanese);

/// <summary>
/// 特征字典。数据源：data/feature-dictionary.json（构建期由 xlsx 一次转换，运行时不依赖 Excel）；
/// 支持用户覆盖文件 feature-dictionary.custom.json（同结构，按 source 覆盖/追加）。
/// 注意：运行时把标签文本当不透明字符串处理，代码不感知具体词语内容。
/// </summary>
public sealed class FeatureDictionary
{
    private readonly Dictionary<string, Dictionary<string, FeatureEntry>> _sheets;

    private FeatureDictionary(Dictionary<string, Dictionary<string, FeatureEntry>> sheets) => _sheets = sheets;

    /// <summary>全部 sheet 名（与原 xlsx 的 5 张表一致）。</summary>
    public IReadOnlyCollection<string> SheetNames => _sheets.Keys;

    public bool TryGet(string sheet, string source, out FeatureEntry entry)
    {
        entry = null!;
        return _sheets.TryGetValue(sheet, out var byKey)
            && byKey.TryGetValue(source.Trim(), out entry!);
    }

    /// <summary>加载词典，可选加载覆盖文件。</summary>
    public static FeatureDictionary Load(string path, string? customPath = null)
    {
        var sheets = ParseSheets(File.ReadAllText(path));
        if (customPath is not null && File.Exists(customPath))
        {
            var custom = ParseSheets(File.ReadAllText(customPath));
            foreach (var (sheet, entries) in custom)
            {
                if (!sheets.TryGetValue(sheet, out var target))
                {
                    target = sheets[sheet] = [];
                }
                foreach (var (source, entry) in entries)
                {
                    target[source] = entry; // 覆盖或追加
                }
            }
        }
        return new FeatureDictionary(sheets);
    }

    /// <summary>从 json 文本构建（测试 / P0 转换工具用）。</summary>
    public static FeatureDictionary FromJson(string json) => new(ParseSheets(json));

    private static Dictionary<string, Dictionary<string, FeatureEntry>> ParseSheets(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var sheets = new Dictionary<string, Dictionary<string, FeatureEntry>>(StringComparer.Ordinal);
        foreach (var sheetProp in doc.RootElement.EnumerateObject())
        {
            if (sheetProp.Value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }
            var byKey = new Dictionary<string, FeatureEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in sheetProp.Value.EnumerateArray())
            {
                string? Get(string name) =>
                    row.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.String
                        ? value.GetString()
                        : null;
                var source = Get("source");
                if (string.IsNullOrWhiteSpace(source))
                {
                    continue;
                }
                var entry = new FeatureEntry(
                    source.Trim(),
                    Get("simplified"),
                    Get("traditional"),
                    Get("japanese"));
                byKey[source.Trim()] = entry; // 后者优先（保持用户自定义意图）
            }
            sheets[sheetProp.Name] = byKey;
        }
        return sheets;
    }
}
