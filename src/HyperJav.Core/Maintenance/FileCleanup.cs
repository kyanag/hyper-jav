using System.Globalization;

namespace HyperJav.Core.Maintenance;

/// <summary>清理列表的文件类型筛选。</summary>
public enum CleanupFileKind
{
    All,
    Video,
    Image,
    Subtitle,
    Other,
}

/// <summary>一个待清理文件（大小严格小于阈值）。</summary>
public sealed record FileCleanupCandidate(string Path, long SizeBytes, DateTime LastWriteTime)
{
    public string FileName => System.IO.Path.GetFileName(Path);

    public string Directory => System.IO.Path.GetDirectoryName(Path) ?? "";
}

/// <summary>删除失败的单条记录。</summary>
public sealed record FileCleanupFailure(string Path, string Error);

/// <summary>删除结果（成功数 + 失败明细）。</summary>
public sealed record FileCleanupDeleteResult(int Deleted, IReadOnlyList<FileCleanupFailure> Failures);

/// <summary>
/// 文件清理：递归扫描目录，列出小于阈值（严格小于）的文件，按大小升序。
/// 跳过隐藏/系统文件与符号链接；无权限的目录/文件直接忽略，不中断扫描。
/// 删除为永久删除（不进回收站），调用方必须先确认。
/// </summary>
public static class FileCleanup
{
    public static readonly string[] DefaultVideoExtensions =
        ["mp4", "mkv", "avi", "wmv", "iso", "rmvb", "flv", "ts", "m2ts", "mov", "m4v", "mpg", "mpeg"];

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "jpg", "jpeg", "png", "webp", "gif", "bmp", "tif", "tiff", "heic",
    };

    private static readonly HashSet<string> SubtitleExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "srt", "ass", "ssa", "vtt", "sub", "idx", "sup",
    };

    public static IReadOnlyList<FileCleanupCandidate> Scan(
        string rootDirectory,
        long maxSizeBytes,
        CleanupFileKind kind = CleanupFileKind.All,
        IReadOnlyCollection<string>? videoExtensions = null)
    {
        if (maxSizeBytes <= 0 || string.IsNullOrWhiteSpace(rootDirectory) || !Directory.Exists(rootDirectory))
        {
            return [];
        }

        var videos = Normalize(videoExtensions ?? DefaultVideoExtensions);
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            ReturnSpecialDirectories = false,
            // 不把隐藏/系统文件纳入清理，不跟随符号链接（避免重复/越界）。
            AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.Hidden | FileAttributes.System,
        };

        var candidates = new List<FileCleanupCandidate>();
        try
        {
            foreach (var path in Directory.EnumerateFiles(rootDirectory, "*", options))
            {
                try
                {
                    var info = new FileInfo(path);
                    if (!info.Exists || info.Length >= maxSizeBytes)
                    {
                        continue;
                    }
                    if (!Matches(kind, info.Extension, videos))
                    {
                        continue;
                    }
                    candidates.Add(new FileCleanupCandidate(path, info.Length, info.LastWriteTime));
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    // 单个文件读不到（被占用/权限）→ 跳过。
                }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // 根目录枚举失败 → 返回已收集的部分。
        }

        return [.. candidates
            .OrderBy(item => item.SizeBytes)
            .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>永久删除（不进回收站）；逐条失败不中断，返回失败明细。</summary>
    public static FileCleanupDeleteResult Delete(IEnumerable<string> paths)
    {
        var deleted = 0;
        var failures = new List<FileCleanupFailure>();
        foreach (var path in paths.Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                File.Delete(path);
                deleted++;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException
                or NotSupportedException)
            {
                failures.Add(new FileCleanupFailure(path, error.Message));
            }
        }
        return new FileCleanupDeleteResult(deleted, failures);
    }

    /// <summary>人类可读大小（B/KB/MB/GB）。</summary>
    public static string HumanSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return unit == 0
            ? $"{bytes} B"
            : $"{value.ToString("0.0", CultureInfo.InvariantCulture)} {units[unit]}";
    }

    private static bool Matches(CleanupFileKind kind, string extension, HashSet<string> videos)
    {
        var ext = extension.TrimStart('.').ToLowerInvariant();
        return kind switch
        {
            CleanupFileKind.All => true,
            CleanupFileKind.Video => videos.Contains(ext),
            CleanupFileKind.Image => ImageExtensions.Contains(ext),
            CleanupFileKind.Subtitle => SubtitleExtensions.Contains(ext),
            CleanupFileKind.Other => !videos.Contains(ext)
                && !ImageExtensions.Contains(ext)
                && !SubtitleExtensions.Contains(ext),
            _ => true,
        };
    }

    private static HashSet<string> Normalize(IEnumerable<string> extensions) =>
        new(extensions.Select(ext => ext.Trim().TrimStart('.').ToLowerInvariant())
            .Where(ext => ext.Length > 0), StringComparer.OrdinalIgnoreCase);
}
