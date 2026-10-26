namespace HyperJav.Core.Tests.Organizing;

using HyperJav.Core.Organizing;

/// <summary>视频文件扫描（递归 + 扩展名过滤 + 目录分组）。</summary>
public sealed class VideoFileScannerTests
{
    private static readonly string[] DefaultExtensions = ["mp4", "mkv", "avi"];

    [Fact]
    public void Groups_videos_by_directory_and_skips_empty_folders()
    {
        using var temp = new TempDir();
        File.WriteAllText(temp.Rel("root/甲夹/a.mp4"), "v");
        File.WriteAllText(temp.Rel("root/甲夹/b.mkv"), "v");
        File.WriteAllText(temp.Rel("root/空夹/notes.txt"), "x"); // 无视频 → 不成组
        File.WriteAllText(temp.Rel("root/c.avi"), "v");          // 根目录自身也会成组

        var groups = VideoFileScanner.Scan(temp.Root, DefaultExtensions);

        Assert.Equal(2, groups.Count);
        var folder = Assert.Single(groups, g => g.Directory.EndsWith("甲夹", StringComparison.Ordinal));
        Assert.Equal(2, folder.Videos.Count);
        Assert.Equal("a", folder.Videos[0].Stem);
        Assert.Equal("mp4", folder.Videos[0].Extension);
        Assert.Contains(groups, g => g.Directory.EndsWith("root", StringComparison.Ordinal));
    }

    [Fact]
    public void Filters_by_extension_case_insensitive()
    {
        using var temp = new TempDir();
        File.WriteAllText(temp.Rel("root/big.MP4"), "v");
        File.WriteAllText(temp.Rel("root/small.wav"), "v");

        foreach (var group in VideoFileScanner.Scan(temp.Root, DefaultExtensions))
        {
            Assert.Equal(["big.MP4"], group.Videos.Select(v => v.FileName).ToArray()); // 原大小写保留。
        }
    }

    [Fact]
    public void Scans_subdirectories_recursively()
    {
        using var temp = new TempDir();
        File.WriteAllText(temp.Rel("root/一级/二级/深/clip.avi"), "v");

        Assert.Single(VideoFileScanner.Scan(temp.Root, DefaultExtensions).Single().Videos);
    }
}

/// <summary>临时目录（测试结束整体删除）。</summary>
internal sealed class TempDir : IDisposable
{
    public string Root { get; }

    public TempDir(string? name = null)
    {
        Root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "hyperjav-tests", name ?? Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    /// <summary>在 Root 下拼出相对路径：叶子是文件夹（无扩展名）时连文件夹一起建。</summary>
    public string Rel(params string[] relative)
    {
        var path = PathOf(Root, relative);
        var leaf = relative[^1];
        if (!System.IO.Path.HasExtension(leaf))
        {
            Directory.CreateDirectory(path);
        }
        return path;
    }

    /// <summary>在指定基准下拼相对路径（供子目录临时目录使用）。</summary>
    public static string PathOf(string baseDirectory, params string[] relative)
    {
        var path = baseDirectory;
        foreach (var segment in relative)
        {
            foreach (var piece in segment.Split('/', '\\'))
            {
                path = System.IO.Path.Combine(path, piece);
            }
        }
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        return path;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // 删除失败无碍断言结果。
        }
    }
}

