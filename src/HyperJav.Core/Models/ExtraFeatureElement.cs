namespace HyperJav.Core.Models;

/// <summary>"额外将以下元素添加到特征中"的可选元素（ini [收集nfo]）。</summary>
public enum ExtraFeatureElement
{
    /// <summary>系列（&lt;set&gt; 名）。</summary>
    Series = 1,

    /// <summary>片商（&lt;studio&gt;）。</summary>
    Studio = 2,
}

/// <summary>特征附加元素解析/序列化。</summary>
public static class ExtraFeatureElementExtensions
{
    private const string Separator = "、";

    /// <summary>解析如 "系列、片商" 的配置值；无法识别的元素忽略。</summary>
    public static IReadOnlyList<ExtraFeatureElement> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return text.Split(Separator)
            .Select(part => part.Trim())
            .Select(part => part switch
            {
                "系列" => ExtraFeatureElement.Series,
                "片商" => ExtraFeatureElement.Studio,
                _ => (ExtraFeatureElement?)null,
            })
            .Where(e => e is not null)
            .Select(e => e!.Value)
            .ToArray();
    }

    /// <summary>序列化为 "系列、片商" 形式。</summary>
    public static string Format(IReadOnlyList<ExtraFeatureElement> elements) =>
        string.Join(Separator, elements.Select(ToText));

    private static string ToText(ExtraFeatureElement element) => element switch
    {
        ExtraFeatureElement.Series => "系列",
        ExtraFeatureElement.Studio => "片商",
        _ => throw new NotSupportedException($"未支持的附加特征元素：{element}"),
    };
}
