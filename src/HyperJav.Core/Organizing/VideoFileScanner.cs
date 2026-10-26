namespace HyperJav.Core.Organizing;

/// <summary>单个视频文件（扫描产物）。</summary>
public sealed record VideoEntry(string FullPath)
{
    /// <summary>文件名（含扩展名）。</summary>
    public string FileName => Path.GetFileName(FullPath);

    /// <summary>不含扩展名的文件名。</summary>
    public string Stem => Path.GetFileNameWithoutExtension(FileName);

    /// <summary>全小写扩展名。</summary>
    public string Extension => Path.GetExtension(FileName).TrimStart('.').ToLowerInvariant();
}

/// <summary>同一目录下的全部视频（目录 = 待整理单元）。</summary>
public sealed record FolderMovieGroup(string Directory, IReadOnlyList<VideoEntry> Videos);

/// <summary>视频文件扫描：递归 root 与其子目录，按扩展名命中并按目录分组。</summary>
public static class VideoFileScanner
{
    public static IReadOnlyList<FolderMovieGroup> Scan(string rootDirectory, IReadOnlyList<string> extensions)
    {
        var extensionSet = extensions
            .Select(e => e.TrimStart('.').ToLowerInvariant())
            .Where(e => e.Length > 0)
            .ToHashSet();

        var groups = new List<FolderMovieGroup>();
        foreach (var directory in EnumerateDirectories(rootDirectory))
        {
            var videos = Directory
                .EnumerateFiles(directory)
                .Select(path => (Path: path, Name: Path.GetFileName(path)))
                .Where(p => extensionSet.Contains(Path.GetExtension(p.Name).TrimStart('.').ToLowerInvariant()))
                .Select(p => new VideoEntry(p.Path))
                .OrderBy(v => v.FileName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (videos.Count > 0)
            {
                groups.Add(new FolderMovieGroup(directory, videos));
            }
        }
        return groups;
    }

    private static IEnumerable<string> EnumerateDirectories(string root)
    {
        yield return root;
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            DirectoryInfo? info;
            try
            {
                info = new DirectoryInfo(current);
            }
            catch (Exception)
            {
                continue;
            }
            if (!info.Exists)
            {
                continue;
            }
            foreach (var sub in info.EnumerateDirectories())
            {
                yield return sub.FullName;
                pending.Push(sub.FullName);
            }
        }
    }
}
