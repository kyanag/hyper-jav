using System.Xml.Linq;
using HyperJav.Core.Models;
using HyperJav.Core.Nfo;
using HyperJav.Core.Normalization;
using Xunit;

namespace HyperJav.Core.Tests.Nfo;

/// <summary>NFO 生成（Kodi/Emby/Jellyfin 兼容，字段对齐原版输出）。</summary>
public sealed class NfoGeneratorTests
{
    private static NfoGenerator Generator() => new();

    private static MovieMetadata Sample() => new()
    {
        CarCode = "ABP-123",
        Title = "示例标题",
        OriginalTitle = "サンプル・タイトル",
        Plot = "示例剧情。",
        Outline = "概述。",
        ReleaseDate = new DateOnly(2020, 1, 1),
        Director = "导演一",
        Studio = "片商一",
        Series = "系列一",
        RuntimeMinutes = 120,
        RawFeatures = ["特征甲", "特征乙"],
        Actors = ["演员甲", "演员乙"],
        Rating = 8.5,
        Votes = 321,
        SourceSite = ScrapeSource.Javlibrary,
    };

    private static XDocument Parse(string xml) =>
        XDocument.Parse(xml, LoadOptions.None);

    [Fact]
    public void Renders_core_fields()
    {
        var result = Generator().Generate(Sample(), new NfoOptions { TitleTemplate = "车牌+空格+标题" });
        var root = Parse(result.Xml).Root!;

        Assert.Equal("movie", root.Name.LocalName);
        Assert.Equal("ABP-123 示例标题", root.Element("title")!.Value);
        Assert.Equal("サンプル・タイトル", root.Element("originaltitle")!.Value);
        Assert.Equal("示例剧情。", root.Element("plot")!.Value);
        Assert.Equal("2020-01-01", root.Element("premiered")!.Value);
        Assert.Equal(2020, (int)root.Element("year")!);
        Assert.Equal(120, (int)root.Element("runtime")!);
        Assert.Equal("导演一", root.Element("director")!.Value);
        Assert.Equal("片商一", root.Element("studio")!.Value);
        Assert.Equal("系列一", root.Element("set")!.Element("name")!.Value);
        Assert.Equal("8.50", root.Element("rating")!.Value);
        Assert.Equal("321", root.Element("votes")!.Value);
        Assert.Equal("ABP-123", root.Element("uniqueid")!.Value);
        Assert.Equal("num", root.Element("uniqueid")!.Attribute("type")!.Value);
        Assert.True(root.Element("uniqueid")!.Attribute("default")!.Value == "true");

        // genre / tag 默认都保存，含附加元素（系列、片商）。
        var genreValues = root.Elements("genre").Select(e => e.Value).ToArray();
        Assert.Equal(["特征甲", "特征乙", "系列一", "片商一"], genreValues);
        var tagValues = root.Elements("tag").Select(e => e.Value).ToArray();
        Assert.Equal(genreValues, tagValues);

        // 演员。
        var actorNames = root.Elements("actor").Select(e => e.Element("name")!.Value).ToArray();
        Assert.Equal(["演员甲", "演员乙"], actorNames);
    }

    [Fact]
    public void Genre_and_tag_switches_are_respected()
    {
        var result = Generator().Generate(Sample(), new NfoOptions
        {
            TitleTemplate = "车牌+空格+标题",
            SaveFeaturesToGenre = false,
            SaveFeaturesToTag = true,
            ExtraFeatureElements = [],
        });
        var root = Parse(result.Xml).Root!;
        Assert.Empty(root.Elements("genre"));
        Assert.Equal(2, root.Elements("tag").Count());
    }

    [Fact]
    public void Uses_title_template_and_trailing_actor_removal()
    {
        var metadata = Sample() with { Title = "示例标题 演员甲 演员乙" };
        var result = Generator().Generate(metadata, new NfoOptions
        {
            TitleTemplate = "【+全部演员+】+车牌",
            RemoveTrailingActorNames = true,
        });
        Assert.Equal("【演员甲、演员乙】ABP-123", Parse(result.Xml).Root!.Element("title")!.Value);
    }

    [Fact]
    public void Normalizes_features_and_appends_flags()
    {
        var dictionary = new FeatureDictionaryLoaderFake().Load();
        var result = Generator().Generate(Sample() with { Subtitle = true, Leak = true }, new NfoOptions
        {
            TitleTemplate = "车牌+空格+标题",
            FeatureDictionary = dictionary,
            ExtraFeatureElements = [],
        });
        var root = Parse(result.Xml).Root!;
        Assert.Equal(["规范甲", "特征乙", "㊥", "无码流出"], root.Elements("genre").Select(e => e.Value).ToArray());
        Assert.Single(result.Warnings); // 特征乙 未命中
    }

    [Fact]
    public void Omits_empty_optional_fields()
    {
        var metadata = Sample() with
        {
            OriginalTitle = null,
            Plot = null,
            Outline = null,
            Director = null,
            Studio = null,
            Series = null,
            RuntimeMinutes = null,
            Rating = null,
            Votes = null,
            RawFeatures = [],
            ExtraImages = [],
        };
        var options = new NfoOptions
        {
            TitleTemplate = "车牌+空格+标题",
            ExtraFeatureElements = [],
            PosterFileName = null,
            FanartFileName = null,
        };
        var root = Parse(Generator().Generate(metadata, options).Xml).Root!;

        Assert.Null(root.Element("originaltitle"));
        Assert.Null(root.Element("plot"));
        Assert.Null(root.Element("outline"));
        Assert.Null(root.Element("director"));
        Assert.Null(root.Element("studio"));
        Assert.Null(root.Element("set"));
        Assert.Null(root.Element("runtime"));
        Assert.Null(root.Element("rating"));
        Assert.Null(root.Element("votes"));
        Assert.Null(root.Element("poster"));
        Assert.Null(root.Element("fanart"));
        Assert.Empty(root.Elements("genre"));
        Assert.Empty(root.Elements("tag"));
    }

    [Fact]
    public void Writes_poster_and_fanart_names()
    {
        var options = new NfoOptions
        {
            TitleTemplate = "车牌+空格+标题",
            PosterFileName = "ABP-123-poster.jpg",
            FanartFileName = "视频名-fanart.jpg",
        };
        var root = Parse(Generator().Generate(Sample(), options).Xml).Root!;
        Assert.Equal("ABP-123-poster.jpg", root.Element("poster")!.Value);
        Assert.Equal("视频名-fanart.jpg", root.Element("fanart")!.Value);
    }

    [Fact]
    public void Xml_has_utf8_declaration_and_escapes_entities()
    {
        var metadata = Sample() with { Title = "标题 <A> & \"名\"" };
        var result = Generator().Generate(metadata, new NfoOptions { TitleTemplate = "车牌+空格+标题" });

        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", result.Xml.TrimStart());
        var title = Parse(result.Xml).Root!.Element("title")!.Value;
        Assert.Equal("ABP-123 标题 <A> & \"名\"", title);
    }

    /// <summary>测试专用假词典（中性占位）。</summary>
    private sealed class FeatureDictionaryLoaderFake
    {
        public FeatureDictionary Load() => FeatureDictionary.FromJson("""
            { "Javlibrary": [ { "source": "特征甲", "simplified": "规范甲" } ] }
            """);
    }
}
