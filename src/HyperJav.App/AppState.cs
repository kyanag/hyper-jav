using HyperJav.Core.Configuration;

namespace HyperJav.App;

/// <summary>应用数据位置与 ini 配置读写（写出 UTF-8 带 BOM；读取自动探测，兼容旧 GB18030 文件）。</summary>
internal static class AppState
{
    /// <summary>exe 同目录的数据资产目录（data/feature-dictionary.json、data/suren-prefixes.json）。</summary>
    public static string DataDirectory => Path.Combine(Application.StartupPath, "data");

    /// <summary>整理规则 ini（exe 同目录；缺失时用默认值，保存时创建）。</summary>
    public static string IniPath => Path.Combine(Application.StartupPath, "整理规则.ini");

    public static AppConfig LoadConfig()
    {
        try
        {
            return File.Exists(IniPath) ? AppConfigLoader.Load(IniPath) : new AppConfig();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // ini 损坏 → 用默认配置，不至于整程序不可用。
            return new AppConfig();
        }
    }

    public static void SaveConfig(AppConfig config) => AppConfigLoader.Save(IniPath, config);
}
