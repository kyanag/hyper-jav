using HyperJav.Core.Models;

namespace HyperJav.Core.Tests.Models;

public class ScrapeCacheLevelsTests
{
    [Theory]
    [InlineData("完全使用缓存", ScrapeCacheLevel.Full)]
    [InlineData("full", ScrapeCacheLevel.Full)]
    [InlineData("只使用正缓存", ScrapeCacheLevel.PositiveOnly)]
    [InlineData("positive", ScrapeCacheLevel.PositiveOnly)]
    [InlineData("完全不使用缓存", ScrapeCacheLevel.Disabled)]
    [InlineData("off", ScrapeCacheLevel.Disabled)]
    [InlineData("否", ScrapeCacheLevel.Disabled)]
    [InlineData("不认识的值", ScrapeCacheLevel.Full)]
    [InlineData(null, ScrapeCacheLevel.Full)]
    public void Parse_maps_text_to_level(string? text, ScrapeCacheLevel expected) =>
        Assert.Equal(expected, ScrapeCacheLevels.Parse(text));

    [Fact]
    public void Display_text_round_trips_for_every_level()
    {
        foreach (var (level, _, display) in ScrapeCacheLevels.All)
        {
            Assert.Equal(level, ScrapeCacheLevels.Parse(display));
            Assert.Equal(display, ScrapeCacheLevels.TextOf(level));
            Assert.Equal(level, ScrapeCacheLevels.At(ScrapeCacheLevels.IndexOf(level)));
        }
    }
}
