using System.Text.Json;
using HyperJav.Core.Models;

namespace HyperJav.Core.Parsing;

/// <summary>素人车牌前缀表（data/suren-prefixes.json，比较用 OrdinalIgnoreCase）。</summary>
public sealed class SurenPrefixTable
{
    private SurenPrefixTable(IReadOnlySet<string> prefixes) => Prefixes = prefixes;

    public IReadOnlySet<string> Prefixes { get; }

    /// <summary>空表（无 data 资产时的降级）。</summary>
    public static SurenPrefixTable Empty { get; } = new(new HashSet<string>(StringComparer.OrdinalIgnoreCase));

    public bool Contains(string prefix) => Prefixes.Contains(prefix);

    /// <summary>文件结构：{ "description"?: …, "prefixes": ["ara", "gana", …] }。</summary>
    public static SurenPrefixTable Load(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var prefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (doc.RootElement.TryGetProperty("prefixes", out var array)
            && array.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in array.EnumerateArray())
            {
                var value = item.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    prefixes.Add(value.Trim());
                }
            }
        }
        return new SurenPrefixTable(prefixes);
    }
}
