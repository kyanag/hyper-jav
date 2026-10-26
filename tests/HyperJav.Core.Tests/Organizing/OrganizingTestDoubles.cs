using HyperJav.Core.Organizing;

namespace HyperJav.Core.Tests.Organizing;

/// <summary>假封面下载器：URL → 预置字节；未注册返回 null（用占位字节，不真联网）。</summary>
public sealed class FakeCoverDownloader : ICoverDownloader
{
    private readonly Dictionary<string, byte[]> _images;

    public FakeCoverDownloader(IReadOnlyDictionary<string, byte[]> images) =>
        _images = new Dictionary<string, byte[]>(images, StringComparer.OrdinalIgnoreCase);

    public List<string> Requests { get; } = [];

    public Task<byte[]?> DownloadAsync(string? url, CancellationToken cancellationToken = default)
    {
        if (url is not null)
        {
            Requests.Add(url);
        }
        return Task.FromResult(url is not null && _images.TryGetValue(url, out var bytes) ? bytes : null);
    }
}

/// <summary>录制型条幅渲染器：记下调用参数，原样返回字节（不真画图）。</summary>
public sealed class RecordingBannerRenderer : IPosterBannerRenderer
{
    public List<(IReadOnlyList<string> Badges, int BytesLength)> Calls { get; } = [];

    public byte[] Render(byte[] posterBytes, IReadOnlyList<string> badges)
    {
        Calls.Add((badges, posterBytes.Length));
        return posterBytes;
    }
}

/// <summary>永远返回 null 的下载器（模拟图床失联）。</summary>
public sealed class NullCoverDownloader : ICoverDownloader
{
    public Task<byte[]?> DownloadAsync(string? url, CancellationToken cancellationToken = default) => Task.FromResult<byte[]?>(null);
}
