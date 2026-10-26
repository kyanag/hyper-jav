using System.Net;
using System.Net.Sockets;
using System.Text;

namespace HyperJav.Core.Scraping;

/// <summary>
/// 极简 SOCKS5 客户端（无认证 CONNECT）。.NET HttpClient 原生不支持 socks5，
/// 通过 SocketsHttpHandler.ConnectCallback 把连接经 SOCKS5 隧道转发（对应原版"局部代理 socks5"）。
/// </summary>
public static class Socks5Tunnel
{
    public static async Task<Stream> ConnectAsync(ScrapeProxySettings proxy, DnsEndPoint target, CancellationToken cancellationToken)
    {
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        NetworkStream? stream = null;
        try
        {
            IPAddress[] entry;
            try
            {
                entry = await Dns.GetHostAddressesAsync(proxy.Host, cancellationToken);
            }
            catch (SocketException error)
            {
                throw new ScrapeException($"无法解析代理地址：{proxy.Host}（{error.Message}）", inner: error);
            }
            var proxyAddress = entry.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                ?? entry.FirstOrDefault()
                ?? throw new ScrapeException($"无法解析代理地址：{proxy.Host}");
            try
            {
                await socket.ConnectAsync(proxyAddress, proxy.Port, cancellationToken);
            }
            catch (SocketException error)
            {
                // .NET 会把这类失败包装成"目标站点被拒"，极具误导性；这里点明是代理本身。
                throw new ScrapeException(
                    $"连不上代理 {proxy.Host}:{proxy.Port}（代理进程可能没开、端口不对，或被防火墙拦截）。原始：{error.Message}",
                    inner: error);
            }

            stream = new NetworkStream(socket, ownsSocket: true);

        // 1. 握手：版本 5、1 种方式、无认证。
        byte[] greeting = [0x05, 0x01, 0x00];
        await stream.WriteAsync(greeting, cancellationToken);
            var hello = new byte[2];
            await stream.ReadExactlyAsync(hello, cancellationToken);
            if (hello[0] != 0x05 || hello[1] != 0x00)
            {
                throw new ScrapeException($"SOCKS5 握手失败（代理 {proxy.Host}:{proxy.Port}）：{hello[0]:X2} {hello[1]:X2}");
            }

            // 2. CONNECT 目标（目标主机一律按域名送出，由代理解析 DNS）。
            var host = Encoding.ASCII.GetBytes(target.Host);
            var request = new byte[7 + host.Length];
            request[0] = 0x05;
            request[1] = 0x01;
            request[2] = 0x00;
            request[3] = 0x03; // ATYP: domain
            request[4] = (byte)host.Length;
            host.CopyTo(request, 5);
            request[^2] = (byte)(target.Port >> 8);
            request[^1] = (byte)(target.Port & 0xFF);
            await stream.WriteAsync(request, cancellationToken);

            var reply = new byte[4];
            await stream.ReadExactlyAsync(reply, cancellationToken);
            if (reply[1] != 0x00)
            {
                throw new ScrapeException($"SOCKS5 代理连接失败：{ErrorText(reply[1])}");
            }

            // 3. 跳过绑定地址（长度取决于 ATYP；0x03 读域长再读域）。
            switch (reply[3])
            {
                case 0x01:
                    await ReadSkipAsync(stream, 4, cancellationToken);
                    break;
                case 0x03:
                    var lengthByte = new byte[1];
                    await stream.ReadExactlyAsync(lengthByte, cancellationToken);
                    await ReadSkipAsync(stream, lengthByte[0], cancellationToken);
                    break;
                case 0x04:
                    await ReadSkipAsync(stream, 16, cancellationToken);
                    break;
            }

            return stream;
        }
        catch
        {
            if (stream is not null)
            {
                await stream.DisposeAsync();
            }
            else
            {
                socket.Dispose();
            }
            throw;
        }
    }

    private static async Task ReadSkipAsync(NetworkStream stream, int count, CancellationToken ct)
    {
        var buffer = new byte[count];
        await stream.ReadExactlyAsync(buffer, ct);
    }

    private static string ErrorText(byte code) => code switch
    {
        0x01 => "一般 SOCKS 服务器故障",
        0x02 => "规则不允许",
        0x03 => "网络不可达",
        0x04 => "主机不可达",
        0x05 => "连接被拒绝",
        0x06 => "TTL 过期",
        0x07 => "不支持该命令",
        0x08 => "不支持该地址类型",
        _ => $"未知错误码 {code}",
    };
}
