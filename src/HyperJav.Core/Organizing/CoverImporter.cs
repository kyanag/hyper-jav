using HyperJav.Core.Configuration;
using HyperJav.Core.Scraping;

namespace HyperJav.Core.Organizing;

/// <summary>封面下载器（返回原始图片字节；找不到/失败返回 null 并由调用方记警告）。</summary>
public interface ICoverDownloader
{
    Task<byte[]?> DownloadAsync(string? url, CancellationToken cancellationToken = default);
}

/// <summary>
/// 封面下载：按图床 host 路由到对应的局部代理开关（是否代理dmm图片 / javbus / jav321 / javdb / javlibrary）。
/// </summary>
public sealed class HttpCoverDownloader : ICoverDownloader
{
    private readonly AppConfig _config;

    public HttpCoverDownloader(AppConfig config) => _config = config;

    public static ScrapeProxySettings? RouteProxy(Uri url, AppConfig config)
    {
        var host = url.Host.ToLowerInvariant();
        foreach (var (keyword, toggle) in Routes(config))
        {
            if (host.Contains(keyword, StringComparison.Ordinal))
            {
                return ScrapeProxySettings.FromAppConfig(config, toggle);
            }
        }
        // 未匹配的图床：按无代理处理（原版仅有明确的几个站点开关）。
        return null;
    }

    private static (string Keyword, bool Toggle)[] Routes(AppConfig config) =>
    [
        ("dmm", config.Proxy.ProxyDmmImages),
        ("javbus", config.Proxy.ProxyJavbus),
        ("jav321", config.Proxy.ProxyJav321),
        ("javdb", config.Proxy.ProxyJavDb),
        ("javlibrary", config.Proxy.ProxyJavlibrary),
    ];

    /// <summary>封面下载：单次尝试；任何网络失败（403 防盗链/超时/被墙）→ null（调用方记警告），绝不中断整理。</summary>
    public async Task<byte[]?> DownloadAsync(string? url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }
        var proxy = RouteProxy(uri, _config);
        try
        {
            using var http = new System.Net.Http.HttpClient(BuildHandler(proxy, _config.Misc.IgnoreSsl))
            {
                Timeout = TimeSpan.FromSeconds(60),
            };
            http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", ScrapeClientOptions.DefaultUserAgent);
            // 图床普遍有防盗链：带同源 Referer（如 javbus 大图 403 的常见成因）。
            http.DefaultRequestHeaders.TryAddWithoutValidation("Referer", $"{uri.Scheme}://{uri.Host}/");
            var bytes = await http.GetByteArrayAsync(uri, cancellationToken);
            return bytes.Length > 0 ? bytes : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error) when (error is HttpRequestException
            or ScrapeException
            or System.IO.IOException
            or TimeoutException)
        {
            // 缺图只记警告；返回 null 由调用方走 http 警告路径。
            return null;
        }
    }

    private static System.Net.Http.HttpMessageHandler BuildHandler(ScrapeProxySettings? proxy, bool allowBadSsl = true) => proxy?.Kind switch
    {
        ProxyKind.Socks5 => new System.Net.Http.SocketsHttpHandler
        {
            UseProxy = false,
            ConnectCallback = async (context, ct) =>
                await Socks5Tunnel.ConnectAsync(proxy, context.DnsEndPoint, ct),
            SslOptions = SslOptionsOf(allowBadSsl),
        },
        ProxyKind.Http => new System.Net.Http.SocketsHttpHandler
        {
            Proxy = new System.Net.WebProxy($"http://{proxy!.Host}:{proxy.Port}"),
            UseProxy = true,
            SslOptions = SslOptionsOf(allowBadSsl),
        },
        _ => new System.Net.Http.SocketsHttpHandler
        {
            SslOptions = SslOptionsOf(allowBadSsl),
        },
    };

    /// <summary>ssl 处置与页面抓取同一口径（忽略 = 接受任意证书）。</summary>
    private static System.Net.Security.SslClientAuthenticationOptions SslOptionsOf(bool allowBadSsl) => new()
    {
        RemoteCertificateValidationCallback = allowBadSsl
            ? (_, _, _, _) => true
            : (_, _, _, policyErrors) => policyErrors == System.Net.Security.SslPolicyErrors.None,
    };
}

/// <summary>海报条幅渲染器：在海报上画出（㊥ / 无码流出）条幅；返回新图片字节。</summary>
public interface IPosterBannerRenderer
{
    byte[] Render(byte[] posterBytes, IReadOnlyList<string> badges);
}

/// <summary>空渲染器（条幅关闭 / 测试替身）。</summary>
public sealed class NullPosterBannerRenderer : IPosterBannerRenderer
{
    public static readonly NullPosterBannerRenderer Instance = new();

    public byte[] Render(byte[] posterBytes, IReadOnlyList<string> badges) => posterBytes;
}

/// <summary>
/// SkiaSharp 条幅渲染：在左下角画条幅（两个条幅时纵向堆叠），
/// 中字㊥ 蓝底白字、流出/其它 红底白字；文字宽度按条幅居中。
/// （原版具体样式未知，样式参数集中在常量区，P5 可调。）
/// </summary>
public sealed class SkiaPosterBannerRenderer : IPosterBannerRenderer
{
    private static readonly SkiaSharp.SKColor SubtitleBandColor = new(21, 101, 192); // 中字：蓝
    private static readonly SkiaSharp.SKColor LeakBandColor = new(183, 28, 28);      // 流出/其它：红

    public byte[] Render(byte[] posterBytes, IReadOnlyList<string> badges)
    {
        var effective = badges.Where(b => !string.IsNullOrWhiteSpace(b)).ToArray();
        if (effective.Length == 0)
        {
            return posterBytes;
        }
        using var bitmap = DecodePoster(posterBytes);
        if (bitmap is null || bitmap.Width <= 0)
        {
            return posterBytes; // 解码失败（含非图片字节）原样返回。
        }
        using (var canvas = new SkiaSharp.SKCanvas(bitmap))
        {
            using var font = LoadFont(bitmap.Height);
            using var textPaint = new SkiaSharp.SKPaint
            {
                Color = SkiaSharp.SKColors.White,
                IsAntialias = true,
            };
            var bandHeight = BandHeight(bitmap.Height);
            // 左下角起自下而上堆叠：第 1 个条幅贴底（㊥ 优先露出），其余向上排。
            for (var index = 0; index < effective.Length; index++)
            {
                var badge = effective[index];
                var top = bitmap.Height - (index + 1) * bandHeight;

                using var bandPaint = new SkiaSharp.SKPaint
                {
                    Color = badge.Contains('㊥', StringComparison.Ordinal) ? SubtitleBandColor : LeakBandColor,
                    IsAntialias = true,
                };
                canvas.DrawRect(0, top, bitmap.Width, bandHeight, bandPaint);

                if (font is not null)
                {
                    var textWidth = font.MeasureText(badge);
                    var x = Math.Max((bitmap.Width - textWidth) / 2f, bandHeight / 4f);
                    canvas.DrawText(badge, new SkiaSharp.SKPoint(x, top + (bandHeight + font.Size) / 2f), SkiaSharp.SKTextAlign.Left, font, textPaint);
                }
            }
        }

        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SkiaSharp.SKEncodedImageFormat.Jpeg, 90);
        return encoded.ToArray();
    }

    private static int BandHeight(int bitmapHeight) => Math.Clamp(bitmapHeight / 14, 36, 90);

    /// <summary>解码海报字节；非图片字节（SKCodec 为 null）返回 null 而不抛异常。</summary>
    private static SkiaSharp.SKBitmap? DecodePoster(byte[] posterBytes)
    {
        try
        {
            using var stream = new MemoryStream(posterBytes, writable: false);
            using var codec = SkiaSharp.SKCodec.Create(stream);
            if (codec is null)
            {
                return null;
            }
            return SkiaSharp.SKBitmap.Decode(codec);
        }
        catch (Exception error) when (error is ArgumentException or IOException)
        {
            return null;
        }
    }

    private static SkiaSharp.SKFont? LoadFont(int bitmapHeight)
    {
        var typeface = SkiaSharp.SKTypeface.FromFamilyName("Microsoft YaHei", SkiaSharp.SKFontStyle.Bold)
            ?? SkiaSharp.SKTypeface.FromFamilyName(null);
        return typeface is null ? null : new SkiaSharp.SKFont(typeface, BandHeight(bitmapHeight) * 0.62f);
    }
}
