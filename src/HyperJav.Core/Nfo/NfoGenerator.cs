using System.Text;
using System.Xml;
using System.Xml.Linq;
using HyperJav.Core.Models;
using HyperJav.Core.Naming;
using HyperJav.Core.Normalization;

namespace HyperJav.Core.Nfo;

/// <summary>NFO 生成结果（XML 文本 + 告警）。</summary>
public sealed record NfoGenerationResult(string Xml, IReadOnlyList<string> Warnings);

/// <summary>
/// Kodi/Emby/Jellyfin 兼容 NFO 生成（字段对齐原版输出，见 docs/specs/field-mapping.md 第 3 节）。
/// 特征管线：规范化（可选）→ 附加元素（系列、片商）→ 标记（㊥/无码流出）→ 按开关写入 genre/tag。
/// </summary>
public sealed class NfoGenerator
{
    public NfoGenerationResult Generate(MovieMetadata metadata, NfoOptions options)
    {
        var warnings = new List<string>();
        var (title, titleWarnings) = BuildTitle(metadata, options);
        warnings.AddRange(titleWarnings);

        var (features, featureWarnings) = BuildFeatures(metadata, options);
        warnings.AddRange(featureWarnings);

        var root = new XElement("movie");

        void Add(string name, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                root.Add(new XElement(name, value.Trim()));
            }
        }

        Add("title", title);
        Add("originaltitle", metadata.OriginalTitle);
        Add("plot", metadata.Plot);
        Add("outline", metadata.Outline);
        if (metadata.ReleaseDate is { } releaseDate)
        {
            root.Add(new XElement("premiered", releaseDate.ToString("yyyy-MM-dd")));
            root.Add(new XElement("year", releaseDate.Year));
        }
        if (metadata.RuntimeMinutes is { } runtime)
        {
            root.Add(new XElement("runtime", runtime.ToString()));
        }
        Add("director", metadata.Director);
        Add("studio", metadata.Studio);
        if (!string.IsNullOrWhiteSpace(metadata.Series))
        {
            root.Add(new XElement("set", new XElement("name", metadata.Series!.Trim())));
        }

        if (options.SaveFeaturesToGenre && features.Count > 0)
        {
            root.Add(features.Select(g => new XElement("genre", g)));
        }
        if (options.SaveFeaturesToTag && features.Count > 0)
        {
            root.Add(features.Select(t => new XElement("tag", t)));
        }

        if (metadata.Rating is { } rating)
        {
            root.Add(new XElement("rating",
                rating.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)));
        }
        if (metadata.Votes is { } votes)
        {
            root.Add(new XElement("votes", votes.ToString()));
        }
        if (!string.IsNullOrWhiteSpace(metadata.CarCode))
        {
            root.Add(new XElement("uniqueid",
                new XAttribute("type", "num"),
                new XAttribute("default", "true"),
                metadata.CarCode.Trim()));
        }
        foreach (var actor in metadata.Actors.Where(a => !string.IsNullOrWhiteSpace(a)))
        {
            var actorElement = new XElement("actor", new XElement("name", actor.Trim()));
            root.Add(actorElement);
        }

        Add("poster", options.PosterFileName);
        Add("fanart", options.FanartFileName);

        var document = new XDocument(new XDeclaration("1.0", "utf-8", null), root);
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = true,
            IndentChars = "  ",
            OmitXmlDeclaration = false,
        };
        using var buffer = new Utf8Buffer();
        using (var writer = XmlWriter.Create(buffer.Stream, settings))
        {
            document.Save(writer);
        }
        return new NfoGenerationResult(buffer.Text, warnings);
    }

    private (string? Title, IReadOnlyList<string> Warnings) BuildTitle(MovieMetadata metadata, NfoOptions options)
    {
        var warnings = new List<string>();
        string? title = metadata.Title;
        if (options.RemoveTrailingActorNames && title is not null && metadata.Actors.Count > 0)
        {
            title = TitleCleaner.RemoveTrailingActorNames(title, metadata.Actors);
            _ = titleWarnings(warnings);
        }
        var context = new NamingContext
        {
            CarCode = metadata.CarCode,
            Title = title,
            Actors = metadata.Actors,
            Series = metadata.Series,
            Studio = metadata.Studio,
            ReleaseDate = metadata.ReleaseDate,
            VideoName = options.VideoName ?? "",
            Subtitle = metadata.Subtitle,
            Leak = metadata.Leak,
        };
        var rendered = new NamingTemplate(options.TitleTemplate).Render(context);
        return (rendered.Length > 0 ? rendered : null, warnings);

        static IReadOnlyList<string> titleWarnings(List<string> _) => Array.Empty<string>();
    }

    private (IReadOnlyList<string> Features, IReadOnlyList<string> Warnings) BuildFeatures(
        MovieMetadata metadata, NfoOptions options)
    {
        var warnings = new List<string>();
        List<string> features;
        if (options.FeatureDictionary is { } dictionary)
        {
            var normalizer = new Normalization.FeatureNormalizer(dictionary, options.Script);
            var result = normalizer.Normalize(metadata.RawFeatures, metadata.SourceSite);
            features = [.. result.Values];
            warnings.AddRange(result.Warnings);
        }
        else
        {
            features = [.. metadata.RawFeatures];
        }

        foreach (var element in options.ExtraFeatureElements)
        {
            var value = element switch
            {
                ExtraFeatureElement.Series => metadata.Series,
                ExtraFeatureElement.Studio => metadata.Studio,
                _ => null,
            };
            if (!string.IsNullOrWhiteSpace(value))
            {
                features.Add(value.Trim());
            }
        }

        if (metadata.Subtitle && options.SubtitleBadge.Length > 0)
        {
            features.Add(options.SubtitleBadge);
        }
        if (metadata.Leak && options.LeakBadge.Length > 0)
        {
            features.Add(options.LeakBadge);
        }

        // 去重（保序）。
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var distinct = features.Where(seen.Add).ToList();
        return (distinct, warnings);
    }

    /// <summary>把 XmlWriter 输出收集为文本（含 UTF-8 声明）。</summary>
    private sealed class Utf8Buffer : IDisposable
    {
        private readonly MemoryStream _stream = new();

        public Stream Stream => _stream;

        public string Text
        {
            get
            {
                _stream.Position = 0;
                using var reader = new StreamReader(_stream, new UTF8Encoding(false));
                return reader.ReadToEnd();
            }
        }

        public void Dispose() => _stream.Dispose();
    }
}
