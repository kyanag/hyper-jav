using System.Net;
using System.Text;
using HyperJav.Core.Configuration;

namespace HyperJav.Core.Scraping;

/// <summary>页面抓取统一接口（生产实现 HttpPageFetcher；测试用假实现）。</summary>
public interface IPageFetcher
{
    /// <summary>本次实例发出的请求序列（"GET url" / "POST url"；诊断用，不落页面内容）。</summary>
    IReadOnlyList<string> Requests { get; }

    Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default);

    /// <summary>POST 表单（jav321 搜索等）。</summary>
    Task<string> PostFormAsync(string url, IReadOnlyDictionary<string, string> form, CancellationToken cancellationToken = default);
}

/// <summary>
/// 站点页面抓取：浏览器头、按站限速、失败重试、局部代理（http 代理或 SOCKS5 隧道）、
/// 响应按 BOM/charset/严格 UTF-8→GB18030 解码（与原版 ini 同一套编码策略）。
/// </summary>
public sealed class HttpPageFetcher : IPageFetcher, IDisposable
{
    private readonly HttpClient _http;
    private readonly ScrapeClientOptions _options;
    private readonly RateGate _gate;

    private readonly List<string> _requests = [];

    public IReadOnlyList<string> Requests => _requests;

    public HttpPageFetcher(ScrapeClientOptions options)
    {
        _options = options;
        _gate = new RateGate(options.MinRequestIntervalMs);
        HttpMessageHandler handler = options.Proxy?.Kind switch
        {
            ProxyKind.Socks5 => Socks5Handler(options),
            ProxyKind.Http => HttpProxyHandler(options),
            _ => DefaultHandler(options),
        };
        _http = new HttpClient(handler) { Timeout = options.Timeout };
        _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", options.UserAgent);
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "zh-CN,zh-TW;q=0.9,ja;q=0.8,en;q=0.6");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
    }

    public async Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default)
    {
        await _gate.WaitForTurnAsync(cancellationToken).ConfigureAwait(false);
        _requests.Add($"GET {url}");
        return await RetryAsync(() => FetchAsync(() => _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken), cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> PostFormAsync(string url, IReadOnlyDictionary<string, string> form, CancellationToken cancellationToken = default)
    {
        await _gate.WaitForTurnAsync(cancellationToken).ConfigureAwait(false);
        _requests.Add($"POST {url}");
        return await RetryAsync(() => FetchAsync(() =>
        {
            // FormUrlEncodedContent 只能消费一次，重试时重建。
            using var content = new FormUrlEncodedContent(form);
            return _http.PostAsync(url, content, cancellationToken);
        }, cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> FetchAsync(Func<Task<HttpResponseMessage>> send, CancellationToken cancellationToken)
    {
        using var response = await send().ConfigureAwait(false);
        var status = (int)response.StatusCode;
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

        if (status >= 200 && status < 300)
        {
            return ScrapeText.Decode(bytes, response.Content.Headers.ContentType?.CharSet);
        }
        if (status == 404 || status == 410)
        {
            throw new ScrapeException($"HTTP {status}（重试无意义）", notFound: true);
        }
        if (status == 403)
        {
            // 典型：Cloudflare/站点风控返回验证页。重试无意义（管线会转下一候选站）。
            throw new ScrapeException("HTTP 403（可能被站点防护拦截——例如 Cloudflare 验证页；换候选站或代理出口）");
        }
        throw new ScrapeException($"HTTP {status}");
    }

    private async Task<T> RetryAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        var attempt = 0;
        while (true)
        {
            try
            {
                return await action().ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception error) when (error is not ScrapeException { IsNotFound: true })
            {
                attempt++;
                if (attempt >= _options.MaxAttempts)
                {
                    var label = _options.MaxAttempts <= 1 ? "" : $"抓取重试 {_options.MaxAttempts} 次后放弃：";
                    throw new ScrapeException($"{label}{ScrapeFriendly.Message(error)}", inner: error);
                }
                await Task.Delay(_options.RetryDelayMs * attempt, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static SocketsHttpHandler DefaultHandler(ScrapeClientOptions options) => new()
    {
        AutomaticDecompression = System.Net.DecompressionMethods.All,
        UseCookies = false,
        SslOptions = SslOptionsOf(options),
    };

    private static SocketsHttpHandler HttpProxyHandler(ScrapeClientOptions options) => new()
    {
        AutomaticDecompression = System.Net.DecompressionMethods.All,
        Proxy = new WebProxy($"http://{options.Proxy!.Host}:{options.Proxy.Port}"),
        UseProxy = true,
        SslOptions = SslOptionsOf(options),
    };

    private static SocketsHttpHandler Socks5Handler(ScrapeClientOptions options) => new()
    {
        AutomaticDecompression = System.Net.DecompressionMethods.All,
        UseProxy = false,
        ConnectCallback = async (context, ct) =>
            await Socks5Tunnel.ConnectAsync(options.Proxy!, context.DnsEndPoint, ct),
        SslOptions = SslOptionsOf(options),
    };

    /// <summary>
    /// ssl 证书处置：忽略 = 接受任意证书（站点证书过期/自签、代理节点替换很常见；只抓公开条目页，无敏感凭据）；
    /// 严格 = 系统默认校验（不降级 TLS 协议版本）。
    /// </summary>
    private static System.Net.Security.SslClientAuthenticationOptions SslOptionsOf(ScrapeClientOptions options) => new()
    {
        RemoteCertificateValidationCallback = options.AllowBadSsl
            ? (_, _, _, policyErrors) => true
            : (_, _, _, policyErrors) => policyErrors == System.Net.Security.SslPolicyErrors.None,
    };

    public void Dispose() => _http.Dispose();
}

/// <summary>响应字节 → 文本（BOM → charset → 严格 UTF-8 → GB18030）。</summary>
public static class ScrapeText
{    public static string Decode(byte[] bytes, string? charset)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return new UTF8Encoding(false).GetString(bytes, 3, bytes.Length - 3);
        }
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }
        if (!string.IsNullOrWhiteSpace(charset))
        {
            try
            {
                return Encoding.GetEncoding(charset).GetString(bytes);
            }
            catch (ArgumentException)
            {
                // 未知 charset 名，走自动探测。
            }
        }
        try
        {
            return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return IniTextCodec.Gb18030.GetString(bytes);
        }
    }
}

/// <summary>
/// 网络异常 → 用户能处置的一行话（原始报错附在括号里，截断防刷屏）。
/// 典型场景：没开代理直连被封站 → Windows 报"由于目标计算机积极拒绝，无法连接"。
/// </summary>
public static class ScrapeFriendly
{
    public static string Message(Exception error)
    {
        // 自家成品文案（Socks5Tunnel 点名代理的那条）优先——别再往深处挖 socket 原话。
        var curated = FindCurated(error);
        if (curated is not null)
        {
            return curated;
        }
        var raw = DeepestMessage(error);
        if (Contains(raw, "积极拒绝", "actively refused"))
        {
            return $"连接被拒绝——该站点可能需要代理：请检查 [局部代理] 开关、协议（socks5/http）与端口（原始：{raw}）";
        }
        if (Contains(raw, "ssl", "tls", "证书", "身份验证失败", "authentication failed", "secure channel"))
        {
            return $"SSL/TLS 握手失败——站点证书问题或代理节点异常（实现已放宽证书校验）；若用代理可换一个节点再试（原始：{raw}）";
        }
        if (Contains(raw, "frame size", "corrupted frame"))
        {
            return $"TLS 数据异常——隧道里收到的不是正常的 TLS 响应。最常见两个原因：①代理协议与端口不匹配（客户端标 SOCKS5 的端口配成了 http，或反之，两者各试一次）；②代理出口把该站点走了直连，被运营商/防火墙劫持改发明文页——把客户端切到全局模式或确认路由规则（原始：{raw}）";
        }
        if (Contains(raw, "timed out", "超时"))
        {
            return $"连接超时——检查代理进程是否在跑、端口是否正确（原始：{raw}）";
        }
        if (Contains(raw, "无法解析", "no such host", "getaddrinfo", "name or service"))
        {
            return $"域名解析失败——检查网络连接与 DNS（原始：{raw}）";
        }
        if (Contains(raw, "代理", "proxy"))
        {
            return $"代理连接失败——检查 [局部代理] 地址与协议（原始：{raw}）";
        }
        return raw.Length > 200 ? raw[..200] + "…" : raw;
    }

    private static bool Contains(string text, params string[] keys) =>
        keys.Any(key => text.Contains(key, StringComparison.OrdinalIgnoreCase));

    /// <summary>链上最近的自家"连不上代理"提示（明确指向代理本身，比底层 socket 原话更具指向性）。</summary>
    private static string? FindCurated(Exception error)
    {
        for (var current = (Exception?)error; current is not null; current = current.InnerException)
        {
            if (current is ScrapeException { IsNotFound: false, Message: { } message }
                && message.StartsWith("连不上代理", StringComparison.Ordinal))
            {
                return message;
            }
        }
        return null;
    }

    /// <summary>异常链最深一层的消息（Socket 的原话比 HttpRequest 的包装更可判别）。</summary>
    private static string DeepestMessage(Exception error)
    {
        var current = error;
        while (current.InnerException is { } inner)
        {
            current = inner;
        }
        return (current.Message ?? "").Replace("\r", " ").Replace("\n", " ");
    }
}
