using System.Text.RegularExpressions;

namespace HyperJav.Core.Naming;

/// <summary>标题后处理。</summary>
public static class TitleCleaner
{
    /// <summary>
    /// 去除标题末尾的演员姓名（原版开关：是否去除标题末尾的演员姓名？）。
    /// 逐个演员名（长者优先）剥离标题结尾，允许结尾分隔符（空格/_/-/·/,，、）。
    /// </summary>
    public static string RemoveTrailingActorNames(string title, IReadOnlyList<string> actors)
    {
        var cleaned = title.Trim();
        var changed = true;
        while (changed && cleaned.Length > 0)
        {
            changed = false;
            foreach (var actor in actors
                         .Where(a => !string.IsNullOrWhiteSpace(a))
                         .Distinct()
                         .OrderByDescending(a => a.Length))
            {
                var pattern = $"(?i)[\\s_\\-·,，、]{{0,3}}{Regex.Escape(actor.Trim())}$";
                var after = Regex.Replace(cleaned, pattern, string.Empty).Trim();
                if (after.Length > 0 && after.Length < cleaned.Length)
                {
                    cleaned = after;
                    changed = true;
                }
            }
        }
        return cleaned;
    }
}
