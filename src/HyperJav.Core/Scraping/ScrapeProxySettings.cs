using System.Globalization;
using System.Text.RegularExpressions;
using HyperJav.Core.Configuration;

namespace HyperJav.Core.Scraping;

/// <summary>
/// 单站点的代理设置（原版 [局部代理]：http/socks5 + 按站点开关）。
/// null 表示该站点直连。
/// </summary>
public sealed record ScrapeProxySettings(ProxyKind Kind, string Host, int Port)
{
    /// <summary>
    /// 解析"代理服务器"值：[socks5|socks5h|http|https]://host:port。
    /// 协议可省略（默认 Socks5，与原版 ini 习惯一致）；socks5h 视同 socks5（实际总是由代理端解析 DNS）。
    /// 失败（空 host / 端口非法 / 未知协议）→ null（视作该站点直连）。
    /// </summary>
    public static ScrapeProxySettings? Parse(string server)
    {
        var value = server.Trim();
        if (value.Length == 0)
        {
            return null;
        }
        var kind = ProxyKind.Socks5;
        var scheme = Regex.Match(value, @"^(?i)(socks5h?|https?)://");
        if (scheme.Success)
        {
            kind = scheme.Groups[1].Value.StartsWith("socks", StringComparison.OrdinalIgnoreCase)
                ? ProxyKind.Socks5 : ProxyKind.Http;
            value = value[scheme.Length..];
        }
        else if (value.Contains("://"))
        {
            // 未知协议（如 ftp://）→ 不猜，视为无效。
            return null;
        }
        var sep = value.LastIndexOf(':');
        if (sep <= 0)
        {
            return null;
        }
        var host = value[..sep].Trim('[', ']');
        if (host.Length == 0
            || !int.TryParse(value[(sep + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var port)
            || port is <= 0 or > 65535)
        {
            return null;
        }
        return new ScrapeProxySettings(kind, host, port);
    }

    /// <summary>从 AppConfig 组装（按站点开关决定是否启用）。</summary>
    public static ScrapeProxySettings? FromAppConfig(AppConfig config, bool proxyForSite) =>
        !proxyForSite || !config.Proxy.Enabled ? null : Parse(config.Proxy.Server);
}
