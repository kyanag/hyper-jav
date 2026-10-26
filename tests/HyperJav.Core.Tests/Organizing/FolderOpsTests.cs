namespace HyperJav.Core.Tests.Organizing;

using HyperJav.Core.Organizing;
using HyperJav.Core.Naming;

/// <summary>文件夹改名/合并/搬移 + 归类路径规划。</summary>
public sealed class FolderOpsTests
{
    [Fact]
    public void EnsureFolder_renames_when_target_absent()
    {
        using var temp = new TempDir();
        var source = temp.Rel("root/旧夹");
        File.WriteAllText(System.IO.Path.Combine(source, "v.mp4"), "v");

        var result = FolderOps.EnsureFolder(source, temp.Rel("root/新夹"));

        Assert.Equal(Path.Combine(temp.Root, "root", "新夹"), result);
        Assert.True(File.Exists(System.IO.Path.Combine(result, "v.mp4")));
        Assert.False(Directory.Exists(source));
    }

    [Fact]
    public void EnsureFolder_merges_when_target_exists()
    {
        using var temp = new TempDir();
        var source = temp.Rel("root/旧夹");
        var target = temp.Rel("root/新夹");
        File.WriteAllText(System.IO.Path.Combine(source, "旧文件.mp4"), "x");
        File.WriteAllText(System.IO.Path.Combine(target, "已存在.mp4"), "y");

        var result = FolderOps.EnsureFolder(source, target);

        Assert.Equal(target, result);
        Assert.True(File.Exists(System.IO.Path.Combine(target, "旧文件.mp4")));
        Assert.False(Directory.Exists(source));
    }

    [Fact]
    public void EnsureFolder_same_path_is_noop()
    {
        using var temp = new TempDir();
        var source = temp.Rel("root/夹");
        File.WriteAllText(System.IO.Path.Combine(source, "v.mp4"), "v");

        Assert.Equal(Path.GetFullPath(source), FolderOps.EnsureFolder(source, source));
    }

    [Fact]
    public void MoveInto_renames_on_name_conflict()
    {
        using var temp = new TempDir();
        var source = temp.Rel("from/a.mp4");
        var target = temp.Rel("to");
        File.WriteAllText(source, "x");
        File.WriteAllText(temp.Rel("to/a.mp4"), "y");

        FolderOps.MoveInto(target, source);

        var moved = Directory.EnumerateFiles(target, "a*.mp4").ToArray();
        Assert.Equal(2, moved.Length); // 原文件保留 + 冲突重命名（带随机后缀）。
        Assert.False(File.Exists(source)); // 源已搬空。
    }
}

public sealed class ClassifyPlannerTests
{
    [Fact]
    public void Resolves_placeholder_root_to_actual_working_root()
    {
        Assert.Equal(@"D:\库", ClassifyPlanner.ResolveRoot("所选文件夹", @"D:\库"));
        Assert.Equal(@"E:\分类库", ClassifyPlanner.ResolveRoot(@"E:\分类库", @"D:\库"));
        Assert.Equal(@"D:\库", ClassifyPlanner.ResolveRoot(@"D:\库/所选文件夹", @"D:\库"));
    }

    [Fact]
    public void Builds_segments_and_sanitizes_each_segment()
    {
        using var temp = new TempDir();
        var context = new NamingContext { CategoryLabel = "有码", Actors = ["演员甲"], CarCode = "ABP:123" };
        var target = ClassifyPlanner.BuildTargetDirectory(
            temp.Root, "影片类型\\全部演员", context, new NamingRenderOptions { MaxTitleLength = null });

        Assert.Equal(
            Path.Combine(temp.Root, "有码", "演员甲"),
            target.TrimEnd(Path.DirectorySeparatorChar));
    }

    [Fact]
    public void Empty_render_falls_back_to_root()
    {
        using var temp = new TempDir();
        var target = ClassifyPlanner.BuildTargetDirectory(
            temp.Root, "影片类型", new NamingContext { CategoryLabel = "" }, new NamingRenderOptions());

        Assert.Equal(Path.TrimEndingDirectorySeparator(temp.Root), Path.TrimEndingDirectorySeparator(target));
    }
}
