using HyperJav.Core.Localization;
using HyperJav.Core.Tests.Organizing;

namespace HyperJav.Core.Tests.Localization;

public class TranslationCatalogTests
{
    [Fact]
    public void Missing_file_and_unknown_values_fall_back_to_original()
    {
        using var temp = new TempDir($"translation-missing-{Guid.NewGuid():N}");
        var catalog = TranslationCatalog.Load(temp.Rel("translations.json"));

        Assert.Null(catalog.LoadError);
        Assert.Equal(0, catalog.Count);
        Assert.Equal("スタジオ甲", catalog.Translate(TranslationKind.Studio, "スタジオ甲"));
        Assert.Equal("", catalog.Translate(TranslationKind.Studio, null));
    }

    [Fact]
    public void Loads_chinese_sections_aliases_and_normalizes_lookup_keys()
    {
        using var temp = new TempDir($"translation-load-{Guid.NewGuid():N}");
        var path = temp.Rel("translations.json");
        File.WriteAllText(path, """
            {
              "_说明": ["占位说明"],
              "片商": { "スタジオ甲": "片商甲", "  Ｓ１  ": "S1 社" },
              "系列": { "シリーズ乙": "系列乙" },
              "演员": { "俳優丙": "演员丙", "": "空键忽略" },
              "特征": { "タグ丁": "标签丁", "タグ戊": "  " },
              "category": { "有码": "有码（推断）" },
              "未知分组": { "不应加载": "忽略" }
            }
            """);

        var catalog = TranslationCatalog.Load(path);

        Assert.Null(catalog.LoadError);
        Assert.Equal(6, catalog.Count);
        Assert.Equal("片商甲", catalog.Translate(TranslationKind.Studio, "スタジオ甲"));
        Assert.Equal("S1 社", catalog.Translate(TranslationKind.Studio, "S1"));
        Assert.Equal("S1 社", catalog.Translate(TranslationKind.Studio, "Ｓ１"));
        Assert.Equal("系列乙", catalog.Translate(TranslationKind.Series, "シリーズ乙"));
        Assert.Equal("演员丙", catalog.Translate(TranslationKind.Actor, "俳優丙"));
        Assert.Equal("标签丁", catalog.Translate(TranslationKind.Feature, "タグ丁"));
        Assert.Equal("タグ戊", catalog.Translate(TranslationKind.Feature, "タグ戊"));
        Assert.Equal("有码（推断）", catalog.Translate(TranslationKind.Category, "有码"));
        Assert.Equal("原样", catalog.Translate(null, "原样"));
    }

    [Fact]
    public void TranslateList_joins_translated_items()
    {
        using var temp = new TempDir($"translation-list-{Guid.NewGuid():N}");
        var path = temp.Rel("translations.json");
        File.WriteAllText(path, """
            { "演员": { "俳優丙": "演员丙" } }
            """);

        var catalog = TranslationCatalog.Load(path);

        Assert.Equal("演员丙、俳優丁", catalog.TranslateList(TranslationKind.Actor, ["俳優丙", "俳優丁"]));
    }

    [Fact]
    public void Broken_json_is_reported_without_throwing()
    {
        using var temp = new TempDir($"translation-broken-{Guid.NewGuid():N}");
        var path = temp.Rel("translations.json");
        File.WriteAllText(path, """{ "片商": """);

        var catalog = TranslationCatalog.Load(path);

        Assert.NotNull(catalog.LoadError);
        Assert.Contains("JSON", catalog.LoadError);
        Assert.Equal(0, catalog.Count);
        Assert.Equal("スタジオ甲", catalog.Translate(TranslationKind.Studio, "スタジオ甲"));
    }

    [Fact]
    public void EnsureFile_writes_template_only_when_missing()
    {
        using var temp = new TempDir($"translation-template-{Guid.NewGuid():N}");
        var path = temp.Rel("nested/translations.json");

        TranslationCatalog.EnsureFile(path);
        var first = File.ReadAllText(path);
        Assert.Contains("\"片商\"", first);
        Assert.Contains("\"演员\"", first);
        var template = TranslationCatalog.Load(path);
        Assert.Null(template.LoadError);
        Assert.Equal(0, template.Count);

        File.WriteAllText(path, """{ "片商": { "スタジオ甲": "片商甲" } }""");
        TranslationCatalog.EnsureFile(path);
        Assert.Equal("""{ "片商": { "スタジオ甲": "片商甲" } }""", File.ReadAllText(path));
    }
}
