using HyperJav.Core.Configuration;
using Xunit;

namespace HyperJav.Core.Tests.Configuration;

public sealed class IniFileTests : IDisposable
{
    private readonly string _tempPath = Path.Combine(Path.GetTempPath(), $"hyperjav-ini-{Guid.NewGuid():N}.ini");

    public void Dispose()
    {
        if (File.Exists(_tempPath))
        {
            File.Delete(_tempPath);
        }
    }

    private const string SampleIni = "[收集nfo]\n是否收集nfo？ = 是\n标题样例键 = 值一\n\n[字幕文件]\n是否重命名已有的字幕文件？ = 否\n";

    [Fact]
    public void Parses_sections_keys_values_in_order()
    {
        var ini = IniFile.Parse(SampleIni);
        Assert.Equal(["收集nfo", "字幕文件"], ini.Sections.Select(s => s.Name).ToArray());
        Assert.True(ini.TryGet("收集nfo", "是否收集nfo？", out var value));
        Assert.Equal("是", value);
        Assert.Equal("否", ini.Find("字幕文件")!.Entries[0].Value);
        Assert.False(ini.TryGet("字幕文件", "不存在键", out _));
    }

    [Fact]
    public void Kept_entries_update_existing_key_in_place()
    {
        var ini = IniFile.Parse(SampleIni);
        ini.SetValue("收集nfo", "是否收集nfo？", "否");
        ini.SetValue("收集nfo", "新增键", "新增值");
        var text = ini.ToIniText();
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Equal(
            ["[收集nfo]", "是否收集nfo？ = 否", "标题样例键 = 值一", "新增键 = 新增值", "[字幕文件]", "是否重命名已有的字幕文件？ = 否"],
            lines);
    }

    [Fact]
    public void Round_trips_gb18030_and_utf8_files()
    {
        var text = IniFile.Parse(SampleIni).ToIniText();
        IniFile.Parse(text).Save(_tempPath, IniEncoding.Gb18030);
        Assert.Equal(text, IniFile.Load(_tempPath).ToIniText());
        Assert.Equal(text, File.ReadAllText(_tempPath, IniFileCodecProbe.Gb18030));

        // 默认（Utf8）写出的应是 UTF-8 带 BOM。
        IniFile.Parse(text).Save(_tempPath, IniEncoding.Utf8);
        Assert.Equal(text, IniFile.Load(_tempPath).ToIniText());
        var saved = File.ReadAllBytes(_tempPath);
        Assert.Equal(0xEF, saved[0]);
        Assert.Equal(0xBB, saved[1]);
        Assert.Equal(0xBF, saved[2]);
    }
}

/// <summary>GB18030 编码探针（与 IniTextCodec 相同的注册逻辑，测试侧独立验证）。</summary>
internal static class IniFileCodecProbe
{
    internal static readonly System.Text.Encoding Gb18030 = Create();

    private static System.Text.Encoding Create()
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        return System.Text.Encoding.GetEncoding("GB18030");
    }
}
