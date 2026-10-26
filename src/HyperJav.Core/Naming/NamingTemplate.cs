using System.Globalization;
using System.Text;

namespace HyperJav.Core.Naming;

/// <summary>
/// 命名模板引擎。模板语法：元素以 "+" 连接，"空格" 为空格元素，其余为字面量。
/// 例：'车牌+空格+标题'、'【+全部演员+】+车牌'、'影片类型\全部演员'、'视频+-poster.jpg'。
/// （对应原版 ini 的 nfo中title的格式 / 重命名影片的格式 / 新文件夹的格式 / 归类的标准 / dvd封面的格式 / 海报的格式。）
/// </summary>
public sealed class NamingTemplate
{
    /// <summary>模板可引用的元素（长名优先匹配，"影片类型" 先于 "类型"）。</summary>
    private static readonly string[] Elements =
    [
        "原文件夹名", "影片类型", "全部演员", "原文件名", "发行日期",
        "车牌", "标题", "系列", "片商", "类型", "视频", "空格",
    ];

    private readonly string _template;
    private readonly List<object> _parts; // string(字面量) / ElementTok(元素)

    public NamingTemplate(string template)
    {
        _template = template;
        _parts = Tokenize(template);
    }

    private readonly record struct ElementTok(string Name);

    private static List<object> Tokenize(string template)
    {
        var parts = new List<object>();
        var literal = new StringBuilder();
        for (var i = 0; i < template.Length;)
        {
            var matched = false;
            foreach (var element in Elements)
            {
                if (template.Length - i >= element.Length &&
                    string.CompareOrdinal(template, i, element, 0, element.Length) == 0)
                {
                    FlushLiteral(literal, parts);
                    parts.Add(new ElementTok(element));
                    i += element.Length;
                    matched = true;
                    break;
                }
            }
            if (matched)
            {
                continue;
            }
            if (template[i] == '+')
            {
                // "+" 为连接符，一律跳过（原版模板把 "+" 只用作元素分隔）。
                i++;
                continue;
            }
            literal.Append(template[i]);
            i++;
        }
        FlushLiteral(literal, parts);
        return parts;
    }

    private static void FlushLiteral(StringBuilder literal, List<object> parts)
    {
        if (literal.Length > 0)
        {
            parts.Add(literal.ToString());
            literal.Clear();
        }
    }

    /// <summary>渲染模板；标记按 中字→流出 追加（原版开关：是否…的表现形式）。</summary>
    public string Render(NamingContext context, NamingRenderOptions? options = null)
    {
        options ??= new NamingRenderOptions();
        var maxTitleLength = options.MaxTitleLength;
        var sb = new StringBuilder();
        foreach (var part in _parts)
        {
            if (part is string literal)
            {
                sb.Append(literal); // 字面量
                continue;
            }
            var element = ((ElementTok)part).Name;
            var value = element switch
            {
                "空格" => " ",
                "车牌" => context.CarCode ?? "",
                "标题" => Truncate(context.Title, maxTitleLength),
                "全部演员" => string.Join(context.ActorSeparator, context.Actors),
                "原文件名" => context.OriginalFileName ?? "",
                "原文件夹名" => context.OriginalFolderName ?? "",
                "系列" => context.Series ?? "",
                "片商" => context.Studio ?? "",
                "发行日期" => context.ReleaseDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
                "类型" => context.CategoryLabel,
                "影片类型" => context.CategoryLabel,
                "视频" => context.VideoName,
                _ => "",
            };
            sb.Append(value);
        }

        // 追加标记（㊥ / 无码流出）。
        var badges = new List<string>();
        if (context.Subtitle && options.SubtitleBadge.Length > 0)
        {
            badges.Add(options.SubtitleBadge);
        }
        if (context.Leak && options.LeakBadge.Length > 0)
        {
            badges.Add(options.LeakBadge);
        }
        foreach (var badge in badges)
        {
            var rendered = sb.ToString().TrimEnd();
            if (rendered.Length > 0 && !rendered.EndsWith(badge, StringComparison.Ordinal))
            {
                sb.Append(' ');
            }
            sb.Append(badge);
        }

        // 清理：压缩连续空格，去首尾空格（不动 "\" 等路径字符）。
        var result = sb.ToString();
        return System.Text.RegularExpressions.Regex.Replace(result, " {2,}", " ").Trim(' ');
    }

    private static string Truncate(string? title, int? maxTitleLength)
    {
        var value = title ?? "";
        return maxTitleLength is > 0 && value.Length > maxTitleLength
            ? value[..maxTitleLength.Value].TrimEnd()
            : value;
    }
}

/// <summary>渲染选项（来自 AppConfig：标题长度、标记表现形式）。</summary>
public sealed record NamingRenderOptions
{
    /// <summary>标题长度截断（"重命名中的标题长度（50~150）"）；null 不截断。</summary>
    public int? MaxTitleLength { get; init; }

    /// <summary>是否中字的表现形式（㊥）。</summary>
    public string SubtitleBadge { get; init; } = "㊥";

    /// <summary>是否流出的表现形式（无码流出）。</summary>
    public string LeakBadge { get; init; } = "无码流出";

    public static NamingRenderOptions FromConfig(Configuration.AppConfig config) => new()
    {
        MaxTitleLength = config.Misc.TitleTruncateLength,
        SubtitleBadge = config.MovieNature.SubtitleBadge,
        LeakBadge = config.MovieNature.LeakBadge,
    };
}
