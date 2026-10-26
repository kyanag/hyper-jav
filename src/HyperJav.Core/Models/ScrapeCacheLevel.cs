namespace HyperJav.Core.Models;

/// <summary>刮削缓存使用等级（读/写口径见 docs/specs/p5-notes.md）。</summary>
public enum ScrapeCacheLevel
{
    /// <summary>完全使用缓存：读正缓存 + 负缓存，写两者。</summary>
    Full = 0,

    /// <summary>只使用正缓存：只读元数据缓存；“未收录”一律重新访问站点验证（仍写负缓存以备后用）。</summary>
    PositiveOnly = 1,

    /// <summary>完全不使用缓存：不读不写。</summary>
    Disabled = 2,
}

/// <summary>缓存等级与 ini 文本/GUI 下拉的互相转换（单一来源定义）。</summary>
public static class ScrapeCacheLevels
{
    public static readonly (ScrapeCacheLevel Level, string Key, string Display)[] All =
    [
        (ScrapeCacheLevel.Full, "full", "完全使用缓存"),
        (ScrapeCacheLevel.PositiveOnly, "positive", "只使用正缓存"),
        (ScrapeCacheLevel.Disabled, "off", "完全不使用缓存"),
    ];

    /// <summary>ini 文本 → 等级；未知/空 → Full（默认）。接受中文显示名与英文键。</summary>
    public static ScrapeCacheLevel Parse(string? text)
    {
        var value = text?.Trim().ToLowerInvariant();
        return value switch
        {
            "off" or "none" or "完全不使用缓存" or "不使用" or "否" => ScrapeCacheLevel.Disabled,
            "positive" or "positiveonly" or "只使用正缓存" or "仅正缓存" => ScrapeCacheLevel.PositiveOnly,
            _ => ScrapeCacheLevel.Full,
        };
    }

    /// <summary>等级 → ini 写入文本（中文显示名，便于手改辨认）。</summary>
    public static string TextOf(ScrapeCacheLevel level) =>
        All.FirstOrDefault(item => item.Level == level).Display ?? All[0].Display;

    /// <summary>下拉框选中序号。</summary>
    public static int IndexOf(ScrapeCacheLevel level) =>
        Math.Max(Array.FindIndex(All, item => item.Level == level), 0);

    public static ScrapeCacheLevel At(int index) => All[Math.Clamp(index, 0, All.Length - 1)].Level;
}
