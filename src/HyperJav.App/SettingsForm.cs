using HyperJav.Core.Configuration;
using HyperJav.Core.Models;
using HyperJav.Core.Scraping;

namespace HyperJav.App;

/// <summary>
/// 设置窗体：整理规则一键读写 exe 同目录的「整理规则.ini」。
/// 行控件由绑定表生成（读取 ini → 控件；保存时回写 cfg 再落盘）。
/// </summary>
public partial class SettingsForm : Form
{
    private readonly AppConfig _config;
    private readonly List<Action> _appliers = [];

    public SettingsForm()
    {
        InitializeComponent();
        _config = AppState.LoadConfig();
        BuildRows();
        btnOk.Click += OnOkClick;
        btnCancelForm.Click += (_, _) => Close();
    }

    private void BuildRows()
    {
        AddSection("收集 nfo");
        AddCheck("跳过已存在 nfo 的文件夹", () => _config.Nfo.SkipFoldersWithNfo, v => _config.Nfo.SkipFoldersWithNfo = v);
        AddCheck("收集 nfo", () => _config.Nfo.CollectNfo, v => _config.Nfo.CollectNfo = v);
        AddCheck("去除标题末尾的演员姓名", () => _config.Nfo.RemoveTrailingActorNames, v => _config.Nfo.RemoveTrailingActorNames = v);
        AddCheck("将特征保存到 genre", () => _config.Nfo.SaveFeaturesToGenre, v => _config.Nfo.SaveFeaturesToGenre = v);
        AddCheck("将特征保存到 tag", () => _config.Nfo.SaveFeaturesToTag, v => _config.Nfo.SaveFeaturesToTag = v);
        AddText("nfo 中 title 的格式", () => _config.Nfo.TitleTemplate, v => _config.Nfo.TitleTemplate = v);

        AddSection("重命名影片");
        AddCheck("重命名影片", () => _config.Rename.Enabled, v => _config.Rename.Enabled = v);
        AddText("重命名影片的格式", () => _config.Rename.Template, v => _config.Rename.Template = v);

        AddSection("修改文件夹");
        AddCheck("重命名或创建独立文件夹", () => _config.Folder.Enabled, v => _config.Folder.Enabled = v);
        AddText("新文件夹的格式", () => _config.Folder.NewFolderTemplate, v => _config.Folder.NewFolderTemplate = v);

        AddSection("归类影片");
        AddCheck("归类影片", () => _config.Classify.Enabled, v => _config.Classify.Enabled = v);
        AddCombo("针对文件还是文件夹", ["文件夹", "文件"], WithTarget, SetTarget);
        AddText("归类的根目录", () => _config.Classify.RootDirectory, v => _config.Classify.RootDirectory = v);
        AddText("归类的标准", () => _config.Classify.Standard, v => _config.Classify.Standard = v);

        AddSection("下载封面");
        AddCheck("下载封面海报", () => _config.Cover.Enabled, v => _config.Cover.Enabled = v);
        AddText("海报的格式", () => _config.Cover.PosterTemplate, v => _config.Cover.PosterTemplate = v);
        AddText("fanart 的格式", () => _config.Cover.FanartTemplate, v => _config.Cover.FanartTemplate = v);
        AddCheck("为海报加中文字幕条幅", () => _config.Cover.SubtitleBanner, v => _config.Cover.SubtitleBanner = v);
        AddCheck("为海报加无码流出条幅", () => _config.Cover.LeakBanner, v => _config.Cover.LeakBanner = v);

        AddSection("字幕文件");
        AddCheck("重命名已有的字幕文件", () => _config.Subtitle.Enabled, v => _config.Subtitle.Enabled = v);
        AddCheck("跳过已有字幕的影片", () => _config.Subtitle.SkipMoviesWithSubtitles, v => _config.Subtitle.SkipMoviesWithSubtitles = v);

        AddSection("kodi 专用");
        AddCheck("多 cd 只收集一份 nfo", () => _config.Kodi.MultiCdSingleNfo, v => _config.Kodi.MultiCdSingleNfo = v);

        AddSection("局部代理");
        AddCheck("使用局部代理", () => _config.Proxy.Enabled, v => _config.Proxy.Enabled = v);
        AddText("代理服务器（[socks5|http]://host:port，协议可省略默认 socks5）",
            () => _config.Proxy.Server, v => _config.Proxy.Server = v.Trim());
        AddCheck("代理 javlibrary", () => _config.Proxy.ProxyJavlibrary, v => _config.Proxy.ProxyJavlibrary = v);
        AddCheck("代理 javbus", () => _config.Proxy.ProxyJavbus, v => _config.Proxy.ProxyJavbus = v);
        AddCheck("代理 jav321", () => _config.Proxy.ProxyJav321, v => _config.Proxy.ProxyJav321 = v);
        AddCheck("代理 javdb", () => _config.Proxy.ProxyJavDb, v => _config.Proxy.ProxyJavDb = v);
        AddCheck("代理 dmm 图片", () => _config.Proxy.ProxyDmmImages, v => _config.Proxy.ProxyDmmImages = v);

        AddSection("信息来源");
        AddCombo("刮削源", [.. SourceChoice.All.Select(item => item.Display)],
            () => SourceChoice.IndexOfKey(_config.InfoSource.ScrapeSource),
            value => _config.InfoSource.ScrapeSource = SourceChoice.All[Math.Clamp(value, 0, SourceChoice.All.Length - 1)].Key);
        AddCheck("javlibrary 整理时收集网友热评", () => _config.InfoSource.CollectHotComments, v => _config.InfoSource.CollectHotComments = v);
        AddCheck("javlibrary 整理时优先用 javbus 图片", () => _config.InfoSource.PreferJavbusImages, v => _config.InfoSource.PreferJavbusImages = v);

        AddSection("其他设置");
        AddCombo("简繁中文", ["简", "繁"], () => (int)_config.Misc.Script, v => _config.Misc.Script = (ScriptMode)v);
        AddText("javlibrary 网址", () => _config.Misc.JavlibraryUrl, v => _config.Misc.JavlibraryUrl = v);
        AddText("javbus 网址", () => _config.Misc.JavbusUrl, v => _config.Misc.JavbusUrl = v);
        AddText("javdb 网址", () => _config.Misc.JavDbUrl, v => _config.Misc.JavDbUrl = v);
        AddText("jav321 网址", () => _config.Misc.Jav321Url, v => _config.Misc.Jav321Url = v);
        AddCheck("忽略 ssl 证书", () => _config.Misc.IgnoreSsl, v => _config.Misc.IgnoreSsl = v);
        AddCombo("刮削缓存", ScrapeCacheLevels.All.Select(item => item.Display).ToArray(),
            () => ScrapeCacheLevels.IndexOf(_config.Misc.CacheLevel),
            value => _config.Misc.CacheLevel = ScrapeCacheLevels.At(value));
        AddText("扫描文件类型（顿号分隔）", () => Join(_config.Misc.ScanExtensions), v => _config.Misc.ScanExtensions = [.. Split(v)]);
        AddText("清理文件大小上限（MB）", () => _config.Misc.CleanupSizeLimitMb.ToString(),
            v => _config.Misc.CleanupSizeLimitMb = int.TryParse(v.Trim(), out var mb) ? Math.Clamp(mb, 1, 102400) : 100);
        AddText("标题长度（50~150）", () => _config.Misc.TitleTruncateLength.ToString(), v => _config.Misc.TitleTruncateLength = int.TryParse(v.Trim(), out var n) ? Math.Clamp(n, 50, 150) : 50);
    }

    private int WithTarget() => _config.Classify.Target == ClassifyTarget.Files ? 1 : 0;

    private void SetTarget(int value) => _config.Classify.Target = value == 1 ? ClassifyTarget.Files : ClassifyTarget.Folders;

    private static string Join(IReadOnlyList<string> items) => string.Join("、", items);

    private static IEnumerable<string> Split(string text) => text.Split('、', ',', ';', ' ', '、')
        .Select(t => t.Trim())
        .Where(t => t.Length > 0);

    private void AddSection(string title)
    {
        var label = new Label
        {
            Text = $"―― {title} ――",
            AutoSize = true,
            ForeColor = Color.FromArgb(60, 90, 130),
            Margin = new Padding(4, 10, 4, 4),
        };
        rows.Controls.Add(label);
        rows.SetColumnSpan(label, 2);
    }

    private void AddCheck(string text, Func<bool> get, Action<bool> set, bool readOnly = false)
    {
        var checkbox = new CheckBox
        {
            Text = text,
            AutoSize = true,
            Enabled = !readOnly,
            Margin = new Padding(4, 4, 4, 4),
        };
        checkbox.Checked = get();
        if (!readOnly)
        {
            _appliers.Add(() => set(checkbox.Checked));
        }
        rows.Controls.Add(checkbox);
        rows.SetColumnSpan(checkbox, 2);
    }

    private void AddText(string label, Func<string> get, Action<string> set)
    {
        var labelControl = new Label
        {
            Text = label,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(4, 6, 4, 4),
        };
        var box = new TextBox
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(4, 6, 8, 4),
        };
        box.Text = get();
        _appliers.Add(() => set(box.Text));
        rows.Controls.Add(labelControl);
        rows.Controls.Add(box);
    }

    private void AddCombo(string label, string[] items, Func<int> get, Action<int> set)
    {
        var labelControl = new Label
        {
            Text = label,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(4, 6, 4, 4),
        };
        var combo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill,
            Margin = new Padding(4, 6, 8, 4),
        };
        combo.Items.AddRange(items);
        combo.SelectedIndex = Math.Clamp(get(), 0, items.Length - 1);
        _appliers.Add(() => set(combo.SelectedIndex));
        rows.Controls.Add(labelControl);
        rows.Controls.Add(combo);
    }

    private void OnOkClick(object? sender, EventArgs args)
    {
        foreach (var apply in _appliers)
        {
            apply();
        }
        try
        {
            AppState.SaveConfig(_config);
            Close();
        }
        catch (Exception error)
        {
            MessageBox.Show(this, $"保存失败：{error.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
