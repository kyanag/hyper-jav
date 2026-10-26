namespace HyperJav.Core.Tests;

/// <summary>定位仓库根（data/、.docs/ 所在目录）。</summary>
internal static class TestPaths
{
    private static readonly Lazy<string> RepoRoot = new(() =>
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 10 && dir is not null; i++)
        {
            if (File.Exists(Path.Combine(dir.FullName, "HyperJav.slnx")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        throw new InvalidOperationException("未找到仓库根（HyperJav.slnx）。");
    });

    public static string Data(string relative) => Path.Combine(RepoRoot.Value, "data", relative);
}
