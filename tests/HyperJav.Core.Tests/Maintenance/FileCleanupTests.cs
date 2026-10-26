using HyperJav.Core.Maintenance;
using HyperJav.Core.Tests.Organizing; // TempDir

namespace HyperJav.Core.Tests.Maintenance;

public class FileCleanupTests
{
    [Fact]
    public void Scan_returns_files_strictly_below_limit()
    {
        using var temp = new TempDir($"cleanup-scan-{Guid.NewGuid():N}");
        var root = temp.Rel("库");
        File.WriteAllBytes(temp.Rel("库/small.mp4"), new byte[1024]);
        File.WriteAllBytes(temp.Rel("库/large.mp4"), new byte[4096]);
        File.WriteAllBytes(temp.Rel("库/sub/small2.mkv"), new byte[2048]);
        File.WriteAllBytes(temp.Rel("库/sub/big.jpg"), new byte[8192]);

        var files = FileCleanup.Scan(root, 4096);

        Assert.Equal(2, files.Count);
        Assert.Equal("small.mp4", files[0].FileName);       // 按大小升序
        Assert.Equal("small2.mkv", files[1].FileName);
        Assert.DoesNotContain(files, item => item.SizeBytes >= 4096);
    }

    [Fact]
    public void Scan_filters_by_kind()
    {
        using var temp = new TempDir($"cleanup-kind-{Guid.NewGuid():N}");
        var root = temp.Rel("库");
        File.WriteAllBytes(temp.Rel("库/a.mp4"), new byte[100]);
        File.WriteAllBytes(temp.Rel("库/b.mkv"), new byte[100]);
        File.WriteAllBytes(temp.Rel("库/c.jpg"), new byte[100]);
        File.WriteAllBytes(temp.Rel("库/d.srt"), new byte[100]);
        File.WriteAllBytes(temp.Rel("库/e.txt"), new byte[100]);

        Assert.Equal(2, FileCleanup.Scan(root, 1024, CleanupFileKind.Video).Count);
        Assert.Single(FileCleanup.Scan(root, 1024, CleanupFileKind.Image));
        Assert.Single(FileCleanup.Scan(root, 1024, CleanupFileKind.Subtitle));
        Assert.Single(FileCleanup.Scan(root, 1024, CleanupFileKind.Other));
        Assert.Equal(5, FileCleanup.Scan(root, 1024, CleanupFileKind.All).Count);
    }

    [Fact]
    public void Scan_respects_custom_video_extensions()
    {
        using var temp = new TempDir($"cleanup-ext-{Guid.NewGuid():N}");
        var root = temp.Rel("库");
        File.WriteAllBytes(temp.Rel("库/a.xyz"), new byte[100]);
        File.WriteAllBytes(temp.Rel("库/b.mp4"), new byte[100]);

        var files = FileCleanup.Scan(root, 1024, CleanupFileKind.Video, ["xyz"]);
        Assert.Equal("a.xyz", Assert.Single(files).FileName);
    }

    [Fact]
    public void Scan_skips_hidden_files()
    {
        using var temp = new TempDir($"cleanup-hidden-{Guid.NewGuid():N}");
        var root = temp.Rel("库");
        var visible = temp.Rel("库/visible.mp4");
        var hidden = temp.Rel("库/hidden.mp4");
        File.WriteAllBytes(visible, new byte[100]);
        File.WriteAllBytes(hidden, new byte[100]);
        File.SetAttributes(hidden, FileAttributes.Hidden);

        var files = FileCleanup.Scan(root, 1024);
        Assert.Equal("visible.mp4", Assert.Single(files).FileName);
    }

    [Fact]
    public void Missing_directory_returns_empty()
    {
        using var temp = new TempDir($"cleanup-missing-{Guid.NewGuid():N}");
        var missing = Path.Combine(temp.Root, "不存在-" + Guid.NewGuid().ToString("N"));
        Assert.Empty(FileCleanup.Scan(missing, 1024));
        Assert.Empty(FileCleanup.Scan(temp.Root, 0));
    }

    [Fact]
    public void Delete_removes_only_requested_files()
    {
        using var temp = new TempDir($"cleanup-delete-{Guid.NewGuid():N}");
        var root = temp.Rel("库");
        var first = temp.Rel("库/a.mp4");
        var second = temp.Rel("库/b.mp4");
        var keep = temp.Rel("库/c.mp4");
        foreach (var path in new[] { first, second, keep })
        {
            File.WriteAllBytes(path, new byte[10]);
        }

        var result = FileCleanup.Delete([first, second]);

        Assert.Equal(2, result.Deleted);
        Assert.Empty(result.Failures);
        Assert.False(File.Exists(first));
        Assert.False(File.Exists(second));
        Assert.True(File.Exists(keep));
    }

    [Fact]
    public void Delete_reports_locked_file_without_stopping_others()
    {
        using var temp = new TempDir($"cleanup-locked-{Guid.NewGuid():N}");
        var root = temp.Rel("库");
        var locked = temp.Rel("库/locked.mp4");
        var free = temp.Rel("库/free.mp4");
        File.WriteAllBytes(locked, new byte[10]);
        File.WriteAllBytes(free, new byte[10]);

        using var stream = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None);
        var result = FileCleanup.Delete([locked, free]);

        Assert.Equal(1, result.Deleted);
        Assert.Equal(locked, Assert.Single(result.Failures).Path);
        Assert.True(File.Exists(locked));
        Assert.False(File.Exists(free));
        Assert.DoesNotContain(Directory.GetFiles(root), path => path != locked);
    }

    [Fact]
    public void Human_size_formats_units()
    {
        Assert.Equal("100 B", FileCleanup.HumanSize(100));
        Assert.Equal("1.0 KB", FileCleanup.HumanSize(1024));
        Assert.Equal("100.0 MB", FileCleanup.HumanSize(100L * 1024 * 1024));
    }
}
