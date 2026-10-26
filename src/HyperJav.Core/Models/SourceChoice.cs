namespace HyperJav.Core.Models;

/// <summary>
/// 刮削源选择项（GUI 下拉 / ini [信息来源]"刮削源" 共用一处定义）。
/// "auto" = 按影片类型选站（有码→Javlibrary 等），站点失败自动回退；具体键 = 强制使用该站。
/// </summary>
public static class SourceChoice
{
    public static readonly (string Key, string Display)[] All =
    [
        ("auto", "自动（失败回退）"),
        ("javlibrary", "强制 Javlibrary"),
        ("javbusyouma", "强制 Javbus有码"),
        ("javbuswuma", "强制 Javbus无码"),
        ("jav321", "强制 Jav321"),
        ("javdb", "强制 JavDb FC2"),
    ];

    /// <summary>ini 键 → 强制站点；"auto"/未知 → null（自动模式）。接受中英文键名。</summary>
    public static ScrapeSource? Parse(string? key) => key?.Trim().ToLowerInvariant() switch
    {
        "javlibrary" => ScrapeSource.Javlibrary,
        "javbusyouma" or "javbus有码" => ScrapeSource.JavbusYouma,
        "javbuswuma" or "javbus无码" => ScrapeSource.JavbusWuma,
        "jav321" => ScrapeSource.Jav321,
        "javdb" or "javdbfc2" => ScrapeSource.JavDbFc2,
        _ => null,
    };

    /// <summary>ini/CLI 键写入前的归一（未知键原样返回，保持"未知配置不静默改写"）。</summary>
    public static string NormalizeKey(string? key)
    {
        var forced = Parse(key);
        return forced is null ? (key is null ? "auto" : key.Trim()) : KeyOf(forced.Value);
    }

    /// <summary>强制站点 → 键（写入 ini / 下拉回显用）。</summary>
    public static string KeyOf(ScrapeSource source) => source switch
    {
        ScrapeSource.Javlibrary => "javlibrary",
        ScrapeSource.JavbusYouma => "javbusyouma",
        ScrapeSource.JavbusWuma => "javbuswuma",
        ScrapeSource.Jav321 => "jav321",
        ScrapeSource.JavDbFc2 => "javdb",
        _ => "auto",
    };

    /// <summary>下拉框选中序号（键已知则为 -1，不选中）。</summary>
    public static int IndexOfKey(string? key)
    {
        var normalized = NormalizeKey(key).ToLowerInvariant();
        for (var i = 0; i < All.Length; i++)
        {
            if (All[i].Key == normalized)
            {
                return i;
            }
        }
        return 0;
    }
}
