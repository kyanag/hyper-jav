using HyperJav.Core.Organizing;
using SkiaSharp;

namespace HyperJav.Core.Tests.Organizing;

/// <summary>SkiaSharp 条幅渲染冒烟（真画图；样式细节留 P5 对照原版再调）。</summary>
public sealed class SkiaPosterBannerRendererTests
{
    private static byte[] MakePoster(int width = 300, int height = 424)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Gray);
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        return data.ToArray();
    }

    [Fact]
    public void Renders_decodable_jpeg_with_same_dimensions()
    {
        var source = MakePoster();
        var rendered = new SkiaPosterBannerRenderer().Render(source, ["㊥", "无码流出"]);

        using var bitmap = SKBitmap.Decode(rendered);
        Assert.NotNull(bitmap);
        Assert.Equal(300, bitmap!.Width);
        Assert.Equal(424, bitmap.Height);
    }

    [Fact]
    public void Bottom_band_is_blue_for_subtitle_badge()
    {
        var rendered = new SkiaPosterBannerRenderer().Render(MakePoster(), ["㊥"]);
        using var bitmap = SKBitmap.Decode(rendered);
        Assert.NotNull(bitmap);

        var bandHeight = Math.Clamp(bitmap!.Height / 14, 36, 90);
        Assert.True(bitmap.Height > bandHeight);
        var pixel = bitmap.GetPixel(10, bitmap.Height - 5); // 左下角（JPEG 有压缩噪声，用容差判断）。
        Assert.True(pixel.Red < 90 && pixel.Blue > 130, $"左下角应为蓝底条幅，实际 {pixel}");
    }

    [Fact]
    public void Bottom_band_is_red_for_leak_badge()
    {
        var rendered = new SkiaPosterBannerRenderer().Render(MakePoster(), ["无码流出"]);
        using var bitmap = SKBitmap.Decode(rendered);
        Assert.NotNull(bitmap);

        var pixel = bitmap!.GetPixel(10, bitmap.Height - 5);
        Assert.True(pixel.Red > 130 && pixel.Green < 90 && pixel.Blue < 90, $"左下角应为红底条幅，实际 {pixel}");
    }

    [Fact]
    public void Empty_badges_returns_original_bytes()
    {
        var source = MakePoster();
        Assert.Equal(source, new SkiaPosterBannerRenderer().Render(source, []));
    }

    [Fact]
    public void Undecodable_bytes_returned_unchanged()
    {
        var garbage = new byte[] { 0x13, 0x37, 0x00 };
        Assert.Equal(garbage, new SkiaPosterBannerRenderer().Render(garbage, ["㊥"]));
    }
}
