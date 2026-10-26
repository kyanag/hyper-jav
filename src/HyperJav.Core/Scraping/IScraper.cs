using HyperJav.Core.Models;

namespace HyperJav.Core.Scraping;

/// <summary>
/// 单个站点适配器（原版 5 个站点 exe 之抽象）。
/// 适配器自声明能力（<see cref="Categories"/>：能刮哪些影片类别），
/// 管线据类别过滤出候选序列（优先级由 <see cref="ScraperFactory"/> 的注册顺序决定），不再硬编程"类别→站点"分工表。
/// </summary>
public interface IScraper
{
    ScrapeSource Source { get; }

    /// <summary>该适配器声明的能力：可刮的影片类别（按处理擅长度排序，越靠前越擅长）。</summary>
    IReadOnlyList<CarCategory> Categories { get; }

    /// <summary>该适配器实例已发出的请求序列（"GET/POST url"；未收录/失败时供管线展示）。</summary>
    IReadOnlyList<string> Requests { get; }

    /// <summary>
    /// 按规范化车牌刮削。站点明确"未找到"（搜索无结果 / 404）返回 null；
    /// 网络错误重试耗尽抛 <see cref="ScrapeException"/>（调用方决定提示或跳过）。
    /// </summary>
    Task<MovieMetadata?> ScrapeAsync(string carCode, CancellationToken cancellationToken = default);
}
