using HyperJav.Core.Naming;
using Xunit;

namespace HyperJav.Core.Tests.Naming;

public sealed class NamingTemplateTests
{
    private static NamingContext Sample() => new()
    {
        CarCode = "ABP-123",
        Title = "示例标题",
        Actors = ["演员甲", "演员乙"],
        OriginalFileName = "原名",
        OriginalFolderName = "原夹",
        Series = "系列一",
        Studio = "片商一",
        ReleaseDate = new DateOnly(2020, 1, 1),
        CategoryLabel = "有码",
        VideoName = "视频名",
    };

    [Theory]
    [InlineData("车牌+空格+标题", "ABP-123 示例标题")]
    [InlineData("【+全部演员+】+车牌", "【演员甲、演员乙】ABP-123")]
    [InlineData("车牌+【+全部演员+】", "ABP-123【演员甲、演员乙】")]
    [InlineData("原文件名", "原名")]
    [InlineData("影片类型\\全部演员", "有码\\演员甲、演员乙")]
    [InlineData("视频+-fanart.jpg", "视频名-fanart.jpg")]
    [InlineData("视频+-poster.jpg", "视频名-poster.jpg")]
    [InlineData("发行日期", "2020-01-01")]
    [InlineData("系列", "系列一")]
    [InlineData("片商", "片商一")]
    [InlineData("原文件夹名", "原夹")]
    public void Renders_default_formats(string template, string expected)
    {
        var rendered = new NamingTemplate(template).Render(Sample());
        Assert.Equal(expected, rendered);
    }

    [Fact]
    public void Film_types_is_matched_as_one_element()
    {
        // "影片类型" 长名整体为一个元素（不拆成 "影"+...），两侧元素互不干扰。
        var rendered = new NamingTemplate("影片类型=类型").Render(Sample());
        Assert.Equal("有码=有码", rendered);
    }

    [Fact]
    public void Truncates_title_to_max_length()
    {
        var context = Sample() with { Title = new string('题', 80) };
        var options = new NamingRenderOptions { MaxTitleLength = 50 };
        var rendered = new NamingTemplate("车牌+空格+标题").Render(context, options);
        Assert.Equal($"ABP-123 {new string('题', 50)}", rendered);
    }

    [Fact]
    public void Appends_badges_for_flags()
    {
        var context = Sample() with { Subtitle = true, Leak = true };
        var rendered = new NamingTemplate("车牌+空格+标题").Render(context);
        Assert.Equal("ABP-123 示例标题 ㊥ 无码流出", rendered);

        // 自定义表现形式。
        var custom = new NamingRenderOptions { SubtitleBadge = "中字", LeakBadge = "" };
        Assert.Equal("ABP-123 示例标题 中字", new NamingTemplate("车牌+空格+标题").Render(context, custom));
    }

    [Fact]
    public void Missing_values_render_as_empty_without_extra_spaces()
    {
        var context = Sample() with { Title = null, CarCode = null };
        var rendered = new NamingTemplate("车牌+空格+标题").Render(context);
        Assert.Equal("", rendered);
    }
}

public sealed class TitleCleanerTests
{
    [Fact]
    public void Removes_trailing_actor_names_with_separators()
    {
        Assert.Equal("午间剧", TitleCleaner.RemoveTrailingActorNames("午间剧 演员甲", ["演员甲"]));
        Assert.Equal("午间剧", TitleCleaner.RemoveTrailingActorNames("午间剧 演员甲 演员乙", ["演员乙", "演员甲"]));
        Assert.Equal("午间剧A", TitleCleaner.RemoveTrailingActorNames("午间剧A-演员甲", ["演员甲"]));
    }

    [Fact]
    public void Keeps_title_when_actor_not_at_end()
    {
        Assert.Equal("演员甲 的故事", TitleCleaner.RemoveTrailingActorNames("演员甲 的故事", ["演员甲"]));
    }

    [Fact]
    public void Never_empties_title()
    {
        Assert.Equal("演员甲", TitleCleaner.RemoveTrailingActorNames("演员甲", ["演员甲"]));
    }
}
