using HyperJav.Core.Configuration;
using HyperJav.Core.Models;
using HyperJav.Core.Scraping;
using Xunit;

namespace HyperJav.Core.Tests.Scraping;

/// <summary>代理设置解析与 ScraperFactory 组装（按站点开关联动）。</summary>
public sealed class ScrapeProxySettingsTests
{
    private static AppConfig ProxyConfig(string server, ProxyOptions? overrides = null)
    {
        var config = new AppConfig();
        config.Proxy.Server = server;
        if (overrides is not null)
        {
            config.Proxy = overrides;
        }
        return config;
    }

    [Theory]
    [InlineData("127.0.0.1:10808", ProxyKind.Socks5, "127.0.0.1", 10808)]
    [InlineData("socks5://127.0.0.1:10808", ProxyKind.Socks5, "127.0.0.1", 10808)]
    [InlineData("http://127.0.0.1:10809", ProxyKind.Http, "127.0.0.1", 10809)]
    [InlineData("https://proxy.example:8443", ProxyKind.Http, "proxy.example", 8443)]
    [InlineData("socks5h://10.0.0.1:1194", ProxyKind.Socks5, "10.0.0.1", 1194)]
    [InlineData("[::1]:10808", ProxyKind.Socks5, "::1", 10808)]
    [InlineData(" [::1]:10808 ", ProxyKind.Socks5, "::1", 10808)]
    public void Parses_host_and_port_with_or_without_scheme(string server, ProxyKind kind, string host, int port)
    {
        var settings = ScrapeProxySettings.Parse(server);
        Assert.NotNull(settings);
        Assert.Equal(host, settings!.Host);
        Assert.Equal(port, settings.Port);
        Assert.Equal(kind, settings.Kind);
    }

    [Theory]
    [InlineData("not-a-address")]
    [InlineData("socks5://")]
    [InlineData("ftp://127.0.0.1:21")]
    [InlineData("127.0.0.1:70000")]
    [InlineData("   ")]
    public void Invalid_server_falls_back_to_direct(string server)
    {
        Assert.Null(ScrapeProxySettings.Parse(server));
    }

    [Fact]
    public void Site_toggle_controls_whether_proxy_is_used()
    {
        var config = new AppConfig();
        config.Proxy.Server = "127.0.0.1:10808";
        config.Proxy.ProxyJav321 = false; // 该站点直连

        Assert.NotNull(ScrapeProxySettings.FromAppConfig(config, proxyForSite: true));
        Assert.Null(ScrapeProxySettings.FromAppConfig(config, proxyForSite: false));
    }

    [Fact]
    public void Disabled_global_proxy_means_direct_connection()
    {
        var config = new AppConfig();
        config.Proxy.Enabled = false;
        Assert.Null(ScrapeProxySettings.FromAppConfig(config, proxyForSite: true));
    }

    [Fact]
    public void Invalid_address_falls_back_to_direct()
    {
        var config = new AppConfig();
        config.Proxy.Server = "not-a-address";
        Assert.Null(ScrapeProxySettings.FromAppConfig(config, proxyForSite: true));
    }

    [Fact]
    public void Factory_uses_configured_site_urls()
    {
        var config = new AppConfig();
        config.Misc.JavlibraryUrl = "https://www.example-lib.com/";
        config.Misc.JavbusUrl = "https://www.example-bus.com";
        config.Misc.JavDbUrl = "https://www.example-db.com/";

        var javlibrary = ScraperFactory.CreateJavlibrary(config);
        var javbus = ScraperFactory.CreateJavbus(config, uncensored: false);
        var javbusWuma = ScraperFactory.CreateJavbus(config, uncensored: true);
        var jav321 = ScraperFactory.CreateJav321(config);
        var javdb = ScraperFactory.CreateJavDbFc2(config);

        Assert.Equal(ScrapeSource.Javlibrary, javlibrary.Source);
        Assert.Equal(ScrapeSource.JavbusYouma, javbus.Source);
        Assert.Equal(ScrapeSource.JavbusWuma, javbusWuma.Source);
        Assert.Equal(ScrapeSource.Jav321, jav321.Source);
        Assert.Equal(ScrapeSource.JavDbFc2, javdb.Source);
    }
}

/// <summary>小工具：URL 绝对化 / 数值解析 / 标题车牌剥离。</summary>
public sealed class ScrapeHelpersTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("b\t\nc", "b c")]
    public void CollapseWhitespace(string? input, string? expected)
        => Assert.Equal(expected, ScrapeHelpers.CollapseWhitespace(input));

    [Fact]
    public void Absolutize_variants()
    {
        Assert.Equal("https://x.example/a.jpg", ScrapeSites.Absolutize("//x.example/a.jpg", "https://y.example/"));
        Assert.Equal("https://y.example/pic.jpg", ScrapeSites.Absolutize("/pic.jpg", "https://y.example/"));
        Assert.Equal("https://y.example/a/b.jpg", ScrapeSites.Absolutize("b.jpg", "https://y.example/a/"));
        Assert.Equal("https://z.example/", ScrapeSites.Absolutize("https://z.example/", "https://y.example/"));
        Assert.Equal("", ScrapeSites.Absolutize(null, "https://y.example/"));
    }

    [Fact]
    public void Parses_values()
    {
        Assert.Equal(120, ScrapeHelpers.Int("120 分鐘"));
        Assert.Null(ScrapeHelpers.Int("无时长"));
        Assert.Equal(4.56, ScrapeHelpers.Double("評分 4.56"));
        Assert.Equal(new DateOnly(2020, 1, 1), ScrapeHelpers.Date("發行日期: 2020/01/01"));
        Assert.Equal(new DateOnly(2019, 5, 10), ScrapeHelpers.Date("2019.05.10"));
        Assert.Null(ScrapeHelpers.Date("无日期"));
        Assert.Equal(["演员甲", "演员乙", "演员丙"], ScrapeHelpers.SplitNames("演员甲、演员乙 / , 演员丙"));
        Assert.Null(ScrapeHelpers.DashToNull("-"));
        Assert.Null(ScrapeHelpers.DashToNull("----"));
    }

    [Fact]
    public void TrimCarCode_strips_and_never_empties()
    {
        Assert.Equal("示例标题", ScrapeHelpers.TrimCarCode("ABP-123 示例标题", "ABP-123"));
        Assert.Equal("示例标题", ScrapeHelpers.TrimCarCode("ABP-123  示例标题", "ABP-123"));
        // 车牌占满标题时不返回空串。
        Assert.Equal("ABP-123", ScrapeHelpers.TrimCarCode("ABP-123", "ABP-123"));
        Assert.Null(ScrapeHelpers.TrimCarCode(null, "ABP-123"));
    }
}
