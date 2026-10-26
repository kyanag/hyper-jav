namespace HyperJav.Core.Tests;

internal static class TestFixtures
{
    /// <summary>读取测试样本（tests/HyperJav.Core.Tests/TestData/…，内容均为中性占位文本）。</summary>
    public static async Task<string> LoadAsync(params string[] segments) => await File.ReadAllTextAsync(
        Path.Combine([.. segments]));

    public static Task<string> ScrapingAsync(string name) =>
        LoadAsync(Path.Combine(AppContext.BaseDirectory, "TestData", "Scraping", name));
}
