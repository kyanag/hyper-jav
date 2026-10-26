using HyperJav.Core.Configuration;
using HyperJav.Core.Models;

namespace HyperJav.Core.Normalization;

/// <summary>规范化结果与告警（未命中条目 → 原【可删除】警告信息.txt 内容）。</summary>
public sealed record FeatureNormalizationResult(
    IReadOnlyList<string> Values,
    IReadOnlyList<string> Warnings);

/// <summary>特征规范化管线（docs/specs/field-mapping.md 第 2 节）。</summary>
public sealed class FeatureNormalizer
{
    private readonly FeatureDictionary? _dictionary;
    private readonly bool _preferTraditional;

    public FeatureNormalizer(FeatureDictionary? dictionary = null, ScriptMode script = ScriptMode.Simplified)
    {
        _dictionary = dictionary;
        _preferTraditional = script == ScriptMode.Traditional;
    }

    /// <summary>原始特征 → 规范化特征列表；字典未命中时保留原值并产生告警；字典未提供时原样透传。</summary>
    public FeatureNormalizationResult Normalize(IReadOnlyList<string> rawFeatures, ScrapeSource source)
    {
        var values = new List<string>();
        var warnings = new List<string>();
        var sheet = TrySheetName(source);

        foreach (var raw in rawFeatures)
        {
            var key = raw.Trim();
            if (key.Length == 0)
            {
                continue;
            }
            if (_dictionary is null || sheet is null
                || !_dictionary.TryGet(sheet, key, out var entry))
            {
                values.Add(raw);
                if (_dictionary is not null && sheet is not null)
                {
                    warnings.Add($"特征未命中字典：{sheet} / {raw}");
                }
                continue;
            }

            var mapped = _preferTraditional
                ? (entry.Traditional ?? entry.Simplified)
                : (entry.Simplified ?? entry.Traditional);
            values.Add(mapped ?? key);
        }
        return new FeatureNormalizationResult(DistinctPreserveOrder(values), warnings);
    }

    private static string? TrySheetName(ScrapeSource source) =>
        source is ScrapeSource.Javlibrary or ScrapeSource.JavbusYouma
            or ScrapeSource.JavbusWuma or ScrapeSource.JavDbFc2 or ScrapeSource.JavDbYouma
            ? source.ToFeatureSheetName()
            : null;

    private static List<string> DistinctPreserveOrder(List<string> values)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>(values.Count);
        foreach (var value in values)
        {
            if (seen.Add(value))
            {
                result.Add(value);
            }
        }
        return result;
    }
}
