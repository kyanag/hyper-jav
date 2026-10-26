namespace HyperJav.Core.Configuration;

/// <summary>AppConfig 文件级读写（ini 格式；读取编码自动探测（BOM/UTF-8/GB18030），写出 UTF-8 带 BOM）。</summary>
public static class AppConfigLoader
{
    /// <summary>从 ini 文件加载；文件不存在的键保留默认值。</summary>
    public static AppConfig Load(string path)
    {
        var ini = IniFile.Load(path);
        return AppConfigMapper.FromIni(ini);
    }

    /// <summary>把配置回写为 ini；若文件已存在则在其基础上更新已知键（未知键原样保留）。</summary>
    public static void Save(string path, AppConfig config)
    {
        var ini = File.Exists(path) ? IniFile.Load(path) : new IniFile();
        AppConfigMapper.ToIni(config, ini);
        ini.Save(path);
    }
}
