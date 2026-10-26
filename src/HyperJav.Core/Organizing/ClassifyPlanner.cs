using HyperJav.Core.Configuration;
using HyperJav.Core.Naming;

namespace HyperJav.Core.Organizing;

/// <summary>归类目标路径（[归类影片]：根目录 + 标准 模板 + 针对文件夹/文件）。</summary>
public static class ClassifyPlanner
{
    /// <summary>
    /// 由标准模板（如 影片类型\全部演员）渲染出根目录下的相对目标路径。
    /// 每个路径段做非法字符净化；渲染失败时返回根目录本身。
    /// </summary>
    public static string BuildTargetDirectory(
        string rootDirectory, string standard, NamingContext context, NamingRenderOptions renderOptions)
    {
        var rendered = new NamingTemplate(standard).Render(context, renderOptions);
        var rootNormalized = rootDirectory.Trim();
        if (string.IsNullOrWhiteSpace(rendered))
        {
            return rootNormalized;
        }
        var path = rootNormalized;
        foreach (var segment in rendered.Split('\\', '/')
                     .Select(segment => PathSanitizer.Sanitize(segment))
                     .Where(segment => segment.Length > 0))
        {
            path = Path.Combine(path, segment);
        }
        return path;
    }

    /// <summary>[归类影片]根目录 = "所选文件夹" 时，用实际工作目录替代。</summary>
    public static string ResolveRoot(string configuredRoot, string workingRoot) =>
        string.IsNullOrWhiteSpace(configuredRoot) || configuredRoot.Contains("所选文件夹", StringComparison.Ordinal)
            ? workingRoot
            : configuredRoot;
}
