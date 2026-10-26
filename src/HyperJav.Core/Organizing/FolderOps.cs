namespace HyperJav.Core.Organizing;

/// <summary>字幕文件重命名（对齐 [字幕文件] 两开关）。</summary>
public static class SubtitleRenamer
{
    public static readonly string[] SubtitleExtensions = ["srt", "ass", "ssa", "sub", "vtt"];

    /// <summary>
    /// 找到与旧视频名配对的字幕并改名到新视频名（不改扩展名，保留 "名称后缀" 如 .chs/.big5）。
    /// 匹配规则：字幕 stem == 旧 stem，或以 旧stem. 开头，或包含车牌（不区分大小写）。
    /// 返回 (原路径, 新路径) 列表（未改名的跳过）。
    /// </summary>
    public static IReadOnlyList<(string OldPath, string NewPath)> Rename(
        string directory, string oldStem, string newStem, string? carCode)
    {
        var renamed = new List<(string, string)>();
        foreach (var file in Directory.EnumerateFiles(directory))
        {
            var extension = Path.GetExtension(file).TrimStart('.').ToLowerInvariant();
            if (!SubtitleExtensions.Contains(extension))
            {
                continue;
            }
            var stem = Path.GetFileNameWithoutExtension(file);
            if (!Matches(stem, oldStem, carCode))
            {
                continue;
            }
            var suffix = stem.Length > oldStem.Length ? stem[oldStem.Length..] : "";
            // 形如 "旧stem.CHS" → 新 stem.后缀；避免生成 "新stem..srt" 这类双点。
            while (suffix.StartsWith('.'))
            {
                suffix = suffix[1..];
            }
            var newFileName = string.IsNullOrEmpty(suffix)
                ? $"{newStem}.{extension}"
                : $"{newStem}.{suffix}.{extension}";
            var newPath = Path.Combine(directory, newFileName);
            if (!string.Equals(file, newPath, StringComparison.OrdinalIgnoreCase))
            {
                File.Move(file, newPath);
                renamed.Add((file, newPath));
            }
        }
        return renamed;
    }

    /// <summary>是否已存在与本视频配对的字幕（用于"跳过已有字幕的影片"）。</summary>
    public static bool HasMatch(string directory, string oldStem, string? carCode)
    {
        foreach (var file in Directory.EnumerateFiles(directory))
        {
            var extension = Path.GetExtension(file).TrimStart('.').ToLowerInvariant();
            if (SubtitleExtensions.Contains(extension)
                && Matches(Path.GetFileNameWithoutExtension(file), oldStem, carCode))
            {
                return true;
            }
        }
        return false;
    }

    private static bool Matches(string stem, string oldStem, string? carCode) =>
        string.Equals(stem, oldStem, StringComparison.OrdinalIgnoreCase)
        || stem.StartsWith(oldStem + ".", StringComparison.OrdinalIgnoreCase)
        || (!string.IsNullOrWhiteSpace(carCode) && stem.Contains(carCode, StringComparison.OrdinalIgnoreCase));
}

/// <summary>文件/文件夹名净化（Windows 非法字符 → 空格；去首尾空白点）。</summary>
public static class PathSanitizer
{
    public static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(c => invalid.Contains(c) && c != Path.DirectorySeparatorChar && c != '\\' ? ' ' : c).ToArray());
        return cleaned.Trim(' ', '.');
    }
}

/// <summary>文件夹创建/重命名/搬移。</summary>
public static class FolderOps
{
    /// <summary>
    /// 让目录变成 targetPath：
    ///  · 相同 → 原样返回 current；
    ///  · target 不存在 → 整目录重命名（Directory.Move）；
    ///  · target 已存在 → 把 current 里的文件逐个搬进去，current 留空尝试删除（失败忽略）。
    /// 返回达成的目录全路径。
    /// </summary>
    public static string EnsureFolder(string current, string targetPath)
    {
        var a = Path.GetFullPath(current).TrimEnd(Path.DirectorySeparatorChar);
        var b = Path.GetFullPath(targetPath).TrimEnd(Path.DirectorySeparatorChar);
        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
        {
            return a;
        }
        if (Directory.Exists(b))
        {
            foreach (var file in Directory.EnumerateFiles(a))
            {
                var destination = Path.Combine(b, Path.GetFileName(file));
                if (File.Exists(destination))
                {
                    destination = Path.Combine(b, $"{Path.GetFileNameWithoutExtension(file)}_{Guid.NewGuid():N}{Path.GetExtension(file)}");
                }
                File.Move(file, destination);
            }
            DeleteIfEmpty(a);
            return b;
        }
        // 归类目标父目录链（"标准"模板多层）可能尚未存在，先补齐。
        Directory.CreateDirectory(Path.GetDirectoryName(b)!);
        Directory.Move(a, b);
        return b;
    }

    public static void MoveInto(string targetDirectory, params IEnumerable<string> files)
    {
        Directory.CreateDirectory(targetDirectory);
        foreach (var file in files.Where(File.Exists))
        {
            var destination = Path.Combine(targetDirectory, Path.GetFileName(file));
            if (File.Exists(destination))
            {
                destination = Path.Combine(targetDirectory, $"{Path.GetFileNameWithoutExtension(file)}_{Guid.NewGuid():N}{Path.GetExtension(file)}");
            }
            File.Move(file, destination);
        }
    }

    /// <summary>
    /// 空目录即删（留给"归类-针对文件"搬完后的原影片夹；目录内有任何内容都不动）。
    /// 删除失败（如目录被占用）静默保留空夹。
    /// </summary>
    public static void DeleteIfEmpty(string path)
    {
        try
        {
            if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
            {
                Directory.Delete(path);
            }
        }
        catch (IOException)
        {
            // 留空目录无妨。
        }
    }
}
