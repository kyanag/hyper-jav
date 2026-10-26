namespace HyperJav.Core.Models;

/// <summary>刮削源（原版按站点拆分的 5 个刮削 exe 之抽象）。</summary>
public enum ScrapeSource
{
    None = 0,

    /// <summary>【有码】javlibrary.exe —— Javlibrary 有码刮削（含网友热评）。</summary>
    Javlibrary = 1,

    /// <summary>【有码】javbus.exe —— Javbus 有码刮削。</summary>
    JavbusYouma = 2,

    /// <summary>【无码】javbus.exe —— Javbus 无码刮削。</summary>
    JavbusWuma = 3,

    /// <summary>【素人】jav321.exe —— 素人刮削（须代理）。</summary>
    Jav321 = 4,

    /// <summary>【FC2】javdb.exe —— JavDb FC2 刮削。</summary>
    JavDbFc2 = 5,

    /// <summary>JavDb 有码（特征字典含对应 sheet，预留）。</summary>
    JavDbYouma = 6,
}

/// <summary>刮削源辅助映射。</summary>
public static class ScrapeSourceExtensions
{
    /// <summary>特征对照表（xlsx → data/feature-dictionary.json）的 sheet 名。素人源无对应 sheet。</summary>
    public static string ToFeatureSheetName(this ScrapeSource source) => source switch
    {
        ScrapeSource.Javlibrary => "Javlibrary",
        ScrapeSource.JavbusYouma => "Javbus有码",
        ScrapeSource.JavbusWuma => "Javbus无码",
        ScrapeSource.JavDbFc2 => "JavdbFc2",
        ScrapeSource.JavDbYouma => "Javdb有码",
        _ => throw new NotSupportedException($"刮削源 {source} 没有对应的特征对照表 sheet。"),
    };
}
