using HyperJav.Core.Configuration;
using HyperJav.Core.Models;

namespace HyperJav.Core.Organizing;

/// <summary>文本产物：整理清单 + 警告信息（UTF-8 带 BOM，编辑器直接可读）；NFO 由 NfoGenerator 走 UTF-8。</summary>
public static class TextOutputs
{
    public const string ChecklistFileName = "整理清单.txt";

    /// <summary>整理清单的输出目录名（放在程序 data 目录下，不写目标目录）。</summary>
    public const string ChecklistDirectoryName = "整理清单";

    public const string WarningsFileName = "警告信息.txt";

    public static readonly System.Text.Encoding Utf8NoBom = new System.Text.UTF8Encoding(false);

    public static void WriteWarnings(string directory, IReadOnlyList<string> warnings) =>
        File.WriteAllLines(Path.Combine(directory, WarningsFileName), warnings, Utf8NoBom);
}

/// <summary>整理清单（data/整理清单/整理清单-时间戳.txt；逐视频一行的状态名册）。</summary>
public static class ChecklistWriter
{
    public static string? Write(string outputDirectory, IReadOnlyList<OrganizeOutcome> outcomes)
    {
        try
        {
            Directory.CreateDirectory(outputDirectory);
            var name = $"{Path.GetFileNameWithoutExtension(TextOutputs.ChecklistFileName)}-" +
                $"{DateTime.Now:yyyyMMdd-HHmmss-fff}.txt";
            var path = Path.Combine(outputDirectory, name);
            var lines = outcomes.Select(FormatLine).ToArray();
            File.WriteAllLines(path, lines, IniTextCodec.Utf8Bom);
            return path;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return null; // 清单写不出来不影响整理本身。
        }
    }

    public static string FormatLine(OrganizeOutcome outcome)
    {
        var status = outcome.Status switch
        {
            OrganizeStatus.Success => "OK",
            OrganizeStatus.Partial => "注意",
            OrganizeStatus.Skipped => "跳过",
            _ => "错误",
        };
        var code = outcome.CarCode ?? "无事车-";
        var destination = outcome.NewPath ?? outcome.OriginalPath;
        return $"[{status}] {code}（{outcome.Message}）：{outcome.OriginalPath} → {destination}";
    }
}
