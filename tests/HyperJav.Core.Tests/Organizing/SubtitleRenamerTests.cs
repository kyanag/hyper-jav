namespace HyperJav.Core.Tests.Organizing;

using HyperJav.Core.Organizing;

/// <summary>字幕配对改名 + 文件名净化（中性假名）。</summary>
public sealed class SubtitleRenamerTests
{
    [Fact]
    public void Renames_paired_subtitle_keeping_extension_and_suffix()
    {
        using var temp = new TempDir();
        var sub = temp.Rel("root/" + "ABP-123.chs.srt");
        File.WriteAllText(sub, "示例字幕");

        var renamed = SubtitleRenamer.Rename(temp.Rel("root"), "ABP-123", "ABP-123 示例标题", "ABP-123");

        Assert.Single(renamed);
        var (oldPath, newPath) = renamed[0];
        Assert.Equal(sub, oldPath);
        Assert.Equal("ABP-123 示例标题.chs.srt", System.IO.Path.GetFileName(newPath));
        Assert.True(File.Exists(newPath));
        Assert.False(File.Exists(oldPath));
    }

    [Theory]
    [InlineData("ABP-123.ass", true)]       // 同名 → 配对
    [InlineData("ABP-123.chs.ass", true)]   // 带后缀 → 配对
    [InlineData("车牌ABP-123合集.srt", true)] // 含车牌 → 配对
    [InlineData("无关字幕.srt", false)]     // 无关 → 跳过
    public void HasMatch_detects_paired_subtitles(string fileName, bool expected)
    {
        using var temp = new TempDir();
        File.WriteAllText(temp.Rel("root/" + fileName), "示例字幕");

        Assert.Equal(expected, SubtitleRenamer.HasMatch(temp.Rel("root"), "ABP-123", "ABP-123"));
    }

    [Fact]
    public void Leaves_unrelated_files_untouched()
    {
        using var temp = new TempDir();
        var unrelated = temp.Rel("root/无关字幕.srt");
        File.WriteAllText(unrelated, "示例字幕");

        var renamed = SubtitleRenamer.Rename(temp.Rel("root"), "ABP-123", "ABP-123 示例标题", "ABP-123");

        Assert.Empty(renamed);
        Assert.True(File.Exists(unrelated));
    }
}

public sealed class PathSanitizerTests
{
    [Theory]
    [InlineData("ABP:123/示例", "ABP 123 示例")]
    [InlineData("标题*?\">|", "标题")]
    [InlineData("  尾点. ", "尾点")]
    [InlineData("正常名字", "正常名字")]
    public void Sanitizes_windows_illegal_characters(string input, string expected)
    {
        Assert.Equal(expected, PathSanitizer.Sanitize(input));
    }
}
