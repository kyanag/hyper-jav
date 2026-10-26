using System.Buffers;
using System.Text;

namespace HyperJav.Core.Configuration;

/// <summary>ini 读写的目标编码。</summary>
public enum IniEncoding
{
    /// <summary>GB18030（原版 ini 的编码；仅显式指定时使用）。</summary>
    Gb18030 = 0,

    /// <summary>UTF-8 带 BOM（默认写出格式；BOM 让多数编辑器自动识别，读取侧无 BOM 也兼容）。</summary>
    Utf8 = 1,

    /// <summary>按内容自动探测（BOM → UTF-8 严格试探 → GB18030）。</summary>
    Auto = 2,
}

/// <summary>ini 编解码（GB18030 依赖 CodePages 编码提供程序）。</summary>
internal static class IniTextCodec
{
    static IniTextCodec() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public static Encoding Gb18030 => Encoding.GetEncoding("GB18030");

    /// <summary>UTF-8 带 BOM（写出的默认编码）。</summary>
    public static Encoding Utf8Bom => new UTF8Encoding(true);

    public static (Encoding Encoding, string Text) DetectAndDecode(byte[] bytes)
    {
        // BOM 优先。
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return (new UTF8Encoding(true), new UTF8Encoding(false).GetString(bytes, 3, bytes.Length - 3));
        }
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return (Encoding.Unicode, Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2));
        }

        // 无 BOM：先用严格 UTF-8 试探（throwOnInvalidBytes: true），失败则退回 GB18030（原版编码）。
        try
        {
            return (new UTF8Encoding(false), new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes));
        }
        catch (DecoderFallbackException)
        {
            return (Gb18030, Gb18030.GetString(bytes));
        }
    }

    public static Encoding Resolve(IniEncoding encoding, Encoding? detected = null) => encoding switch
    {
        IniEncoding.Utf8 => Utf8Bom,
        IniEncoding.Gb18030 => Gb18030,
        _ => detected ?? Utf8Bom,
    };
}

/// <summary>ini 条目（key 已 trim，value 保留写入值）。</summary>
public sealed class IniEntry
{
    public required string Key { get; init; }
    public string Value { get; set; } = "";
}

/// <summary>ini 节。</summary>
public sealed class IniSection
{
    private readonly List<IniEntry> _entries;

    public IniSection(string name)
    {
        Name = name;
        _entries = [];
    }

    /// <summary>节名（不含方括号）。"" 表示节头之前的匿名区。</summary>
    public string Name { get; }

    public IReadOnlyList<IniEntry> Entries => _entries;

    public bool TryGet(string key, out string value)
    {
        var entry = _entries.FirstOrDefault(e => string.Equals(e.Key, key.Trim(), StringComparison.Ordinal));
        if (entry is null)
        {
            value = "";
            return false;
        }
        value = entry.Value;
        return true;
    }

    /// <summary>更新已存在的键或追加新键（保持键序）。</summary>
    public IniEntry Set(string key, string value)
    {
        var trimmed = key.Trim();
        var entry = _entries.FirstOrDefault(e => string.Equals(e.Key, trimmed, StringComparison.Ordinal));
        if (entry is not null)
        {
            entry.Value = value;
            return entry;
        }
        var added = new IniEntry { Key = key.Trim(), Value = value };
        _entries.Add(added);
        return added;
    }

    /// <summary>删除键（不存在则无事发生）；用于历史键合并清理。</summary>
    public void Remove(string key)
    {
        var entry = _entries.FirstOrDefault(e => string.Equals(e.Key, key.Trim(), StringComparison.Ordinal));
        if (entry is not null)
        {
            _entries.Remove(entry);
        }
    }
}

/// <summary>
/// 顺序保持的简易 ini 实现：
/// 与原版互通 — 节序/键序保持；格式为 "键 = 值"；键/值两侧空白 trim；
/// 说明：注释与空行不保留（原版 ini 无注释），全角 "＝" 归一为 "="。
/// </summary>
public sealed class IniFile
{
    private readonly List<IniSection> _sections;

    public IniFile() => _sections = [];

    private IniFile(List<IniSection> sections) => _sections = sections;

    public IReadOnlyList<IniSection> Sections => _sections;

    public static IniFile Parse(string text)
    {
        var sections = new List<IniSection>();
        var current = new IniSection("");
        sections.Add(current);
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            // 去掉可能的 BOM 字符与全角等号。
            line = line.TrimStart('\uFEFF').Replace('＝', '=');
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }
            if (trimmed[0] == '[')
            {
                var end = trimmed.IndexOf(']');
                if (end > 0)
                {
                    current = new IniSection(trimmed[1..end].Trim());
                    sections.Add(current);
                    continue;
                }
            }
            var eq = trimmed.IndexOf('=');
            if (eq <= 0)
            {
                // 无等号的行丢弃（原版 ini 不存在该形态；保留会破坏键值对齐）。
                continue;
            }
            var key = trimmed[..eq].Trim();
            var value = trimmed[(eq + 1)..].Trim();
            current.Set(key, value);
        }
        // 匿名区为空时不保留。
        if (sections.Count > 0 && sections[0].Name.Length == 0 && sections[0].Entries.Count == 0)
        {
            sections.RemoveAt(0);
        }
        return new IniFile(sections);
    }

    public static IniFile Load(string path)
    {
        var (encoding, text) = IniTextCodec.DetectAndDecode(File.ReadAllBytes(path));
        _ = encoding; // 编码信息在保存时由调用方指定或再探测。
        return Parse(text);
    }

    public IniSection? Find(string section) =>
        _sections.FirstOrDefault(s =>
            string.Equals(s.Name, section.Trim(), StringComparison.Ordinal));

    public bool TryGet(string section, string key, out string value)
    {
        var found = Find(section);
        if (found is null)
        {
            value = "";
            return false;
        }
        return found.TryGet(key, out value!);
    }

    /// <summary>更新键值；节/键不存在则追加（节追加到末尾）。</summary>
    public void SetValue(string section, string key, string value)
    {
        var found = Find(section);
        if (found is null)
        {
            found = new IniSection(section);
            _sections.Add(found);
        }
        found.Set(key, value);
    }

    public string ToIniText()
    {
        var sb = new StringBuilder();
        foreach (var section in _sections.Where(s => s.Name.Length > 0))
        {
            if (sb.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine();
            }
            sb.Append('[').Append(section.Name).Append(']').AppendLine();
            foreach (var entry in section.Entries)
            {
                sb.Append(entry.Key).Append(" = ").Append(entry.Value).AppendLine();
            }
        }
        return sb.ToString();
    }

    public void Save(string path, IniEncoding encoding = IniEncoding.Utf8)
        => File.WriteAllText(path, ToIniText(), IniTextCodec.Resolve(encoding));
}
