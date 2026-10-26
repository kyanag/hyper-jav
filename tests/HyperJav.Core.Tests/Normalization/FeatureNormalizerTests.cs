using HyperJav.Core.Configuration;
using HyperJav.Core.Models;
using HyperJav.Core.Normalization;
using Xunit;

namespace HyperJav.Core.Tests.Normalization;

/// <summary>特征规范化——测试用中性假字典（"表一/表二"等），不使用真实站点词语。</summary>
public sealed class FeatureNormalizerTests : IDisposable
{
    private readonly string _dictionaryPath = Path.Combine(Path.GetTempPath(), $"hyperjav-dict-{Guid.NewGuid():N}.json");
    private readonly string _customPath = Path.Combine(Path.GetTempPath(), $"hyperjav-dict-custom-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        foreach (var path in new[] { _dictionaryPath, _customPath })
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static readonly string DictionaryJson = """
        {
          "Javlibrary": [
            { "source": "tag-alpha", "simplified": "标签甲", "traditional": "標簽甲", "japanese": "タグ甲" },
            { "source": "tag-beta", "simplified": "标签乙" }
          ],
          "Javbus有码": [
            { "source": "tag-alpha", "simplified": "简体甲" }
          ]
        }
        """;

    [Fact]
    public void Maps_to_simplified_by_default_and_keeps_unmatched_raw()
    {
        File.WriteAllText(_dictionaryPath, DictionaryJson);
        var dictionary = FeatureDictionary.Load(_dictionaryPath);
        var normalizer = new FeatureNormalizer(dictionary, ScriptMode.Simplified);

        var result = normalizer.Normalize(["tag-alpha", "tag-beta", "tag-unknown"], ScrapeSource.Javlibrary);

        Assert.Equal(["标签甲", "标签乙", "tag-unknown"], result.Values);
        Assert.Single(result.Warnings);
        Assert.Contains("tag-unknown", result.Warnings[0]);
    }

    [Fact]
    public void Traditional_mode_falls_back_to_simplified()
    {
        File.WriteAllText(_dictionaryPath, DictionaryJson);
        var dictionary = FeatureDictionary.Load(_dictionaryPath);
        var normalizer = new FeatureNormalizer(dictionary, ScriptMode.Traditional);

        var result = normalizer.Normalize(["tag-alpha", "tag-beta"], ScrapeSource.Javlibrary);

        Assert.Equal(["標簽甲", "标签乙"], result.Values);
    }

    [Fact]
    public void Uses_the_scrapers_own_sheet()
    {
        File.WriteAllText(_dictionaryPath, DictionaryJson);
        var dictionary = FeatureDictionary.Load(_dictionaryPath);
        var normalizer = new FeatureNormalizer(dictionary, ScriptMode.Simplified);

        var result = normalizer.Normalize(["tag-alpha"], ScrapeSource.JavbusYouma);

        Assert.Equal(["简体甲"], result.Values);
    }

    [Fact]
    public void Sources_without_sheet_pass_through()
    {
        File.WriteAllText(_dictionaryPath, DictionaryJson);
        var dictionary = FeatureDictionary.Load(_dictionaryPath);
        var normalizer = new FeatureNormalizer(dictionary, ScriptMode.Simplified);

        var result = normalizer.Normalize(["tag-alpha"], ScrapeSource.Jav321);

        Assert.Equal(["tag-alpha"], result.Values);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Custom_dictionary_overrides_and_appends()
    {
        File.WriteAllText(_dictionaryPath, DictionaryJson);
        File.WriteAllText(_customPath, """{ "Javlibrary": [ { "source": "tag-alpha", "simplified": "覆盖后" }, { "source": "tag-new", "simplified": "新增" } ] }""");
        var dictionary = FeatureDictionary.Load(_dictionaryPath, _customPath);
        var normalizer = new FeatureNormalizer(dictionary, ScriptMode.Simplified);

        var result = normalizer.Normalize(["tag-alpha", "tag-new"], ScrapeSource.Javlibrary);

        Assert.Equal(["覆盖后", "新增"], result.Values);
    }

    [Fact]
    public void Dedupes_values_preserving_order()
    {
        File.WriteAllText(_dictionaryPath, DictionaryJson);
        var dictionary = FeatureDictionary.Load(_dictionaryPath);
        var normalizer = new FeatureNormalizer(dictionary, ScriptMode.Simplified);

        var result = normalizer.Normalize(["tag-alpha", "tag-alpha", "标签甲"], ScrapeSource.Javlibrary);

        Assert.Equal(["标签甲"], result.Values);
    }

    [Fact]
    public void Without_dictionary_passes_everything_through()
    {
        var normalizer = new FeatureNormalizer();
        var result = normalizer.Normalize(["任意词"], ScrapeSource.JavDbFc2);
        Assert.Equal(["任意词"], result.Values);
        Assert.Empty(result.Warnings);
    }
}
