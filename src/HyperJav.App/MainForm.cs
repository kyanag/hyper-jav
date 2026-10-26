using HyperJav.Core.Configuration;
using HyperJav.Core.Localization;
using HyperJav.Core.Maintenance;
using HyperJav.Core.Models;
using HyperJav.Core.Organizing;
using HyperJav.Core.Parsing;
using HyperJav.Core.Scraping;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HyperJav.App;

/// <summary>主窗体：选目录 → 开始整理（后台跑管线）→ 进度事件入日志 → 清单可打开。</summary>
public partial class MainForm : Form
{
    private AppConfig _config;
    private CancellationTokenSource? _canceller;
    private bool _cleanupBusy;

    /// <summary>整理/探测进行中的状态文字动画帧（不占布局空间）。</summary>
    private static readonly string[] BusyFrames = ["◐", "◓", "◑", "◒"];

    private const int WmVsCroll = 0x0115; // WM_VSCROLL
    private const int SbBottom = 7;       // SB_BOTTOM

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    private readonly System.Windows.Forms.Timer _busyTimer;
    private bool _busyIndicator;
    private int _busyFrame;
    private string _statusText = "";

    public MainForm()
    {
        InitializeComponent();
        _config = AppState.LoadConfig();
        BindSourceCombo();
        BindCacheCombo();
        BindCleanupDefaults();
        _busyTimer = new System.Windows.Forms.Timer(components!) { Interval = 130 };
        _busyTimer.Tick += OnBusyTick;
        btnStart.Click += OnStartClick;
        btnCancel.Click += OnCancelClick;
        btnBrowse.Click += OnBrowseClick;
        btnSettings.Click += OnSettingsClick;
        btnOpenChecklist.Click += OnOpenChecklistClick;
        btnProbe.Click += OnProbeClick;
        btnStatsReport.Click += OnStatsReportClick;
        btnTranslationFile.Click += OnTranslationFileClick;
        cmbSource.SelectedValueChanged += OnSourceChanged;
        cmbCache.SelectedIndexChanged += OnCacheLevelChanged;

        btnCleanupBrowse.Click += OnCleanupBrowseClick;
        btnCleanupUseMain.Click += OnCleanupUseMainClick;
        btnCleanupScan.Click += OnCleanupScanClick;
        btnCleanupSelectAll.Click += OnCleanupSelectAllClick;
        btnCleanupSelectNone.Click += OnCleanupSelectNoneClick;
        btnCleanupDelete.Click += OnCleanupDeleteClick;
        txtCleanupLimit.Leave += OnCleanupLimitLeave;
        tabControl.SelectedIndexChanged += OnTabChanged;
        gridCleanup.CurrentCellDirtyStateChanged += OnCleanupGridDirtyStateChanged;
        gridCleanup.CellValueChanged += OnCleanupGridCellValueChanged;
        gridCleanup.SortCompare += OnCleanupGridSortCompare;
        gridCleanup.EditMode = DataGridViewEditMode.EditOnEnter;
        // DataGridView 的行高不会随 DPI 自动缩放；按当前 DPI 放大，避免复选框被裁掉。
        gridCleanup.RowTemplate.Height = Math.Max(28, (int)Math.Round(28 * DeviceDpi / 96.0));
    }

    private void BindCleanupDefaults()
    {
        txtCleanupLimit.Text = Math.Clamp(_config.Misc.CleanupSizeLimitMb, 1, 102400).ToString();
        cmbCleanupType.SelectedIndex = 0;
    }

    /// <summary>刮削源下拉：选项文本来自 SourceChoice（Core 与 ini/CLI 同一出处）。</summary>
    private void BindSourceCombo()
    {
        cmbSource.Items.Clear();
        foreach (var (_, display) in SourceChoice.All)
        {
            cmbSource.Items.Add(display);
        }
        cmbSource.SelectedIndex = SourceChoice.IndexOfKey(_config.InfoSource.ScrapeSource);
    }

    private void OnSourceChanged(object? sender, EventArgs args)
    {
        if (cmbSource.SelectedIndex < 0)
        {
            return;
        }
        _config.InfoSource.ScrapeSource = SourceChoice.All[cmbSource.SelectedIndex].Key;
        SaveConfigQuietly();
    }

    /// <summary>缓存等级下拉：完全 / 只正缓存 / 不使用，和 ini、设置窗体同一套文本。</summary>
    private void BindCacheCombo()
    {
        cmbCache.Items.Clear();
        foreach (var (_, _, display) in ScrapeCacheLevels.All)
        {
            cmbCache.Items.Add(display);
        }
        cmbCache.SelectedIndex = ScrapeCacheLevels.IndexOf(_config.Misc.CacheLevel);
    }

    private void OnCacheLevelChanged(object? sender, EventArgs args)
    {
        if (cmbCache.SelectedIndex < 0)
        {
            return;
        }
        var level = ScrapeCacheLevels.At(cmbCache.SelectedIndex);
        if (_config.Misc.CacheLevel == level)
        {
            return;
        }
        _config.Misc.CacheLevel = level;
        SaveConfigQuietly(); // 即选即存，下次启动还在。
    }

    private void SaveConfigQuietly()
    {
        try
        {
            AppState.SaveConfig(_config);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // 保存失败不打断，本次会话仍用内存配置。
        }
    }

    private string? _checklistPath;

    private async void OnStartClick(object? sender, EventArgs args)
    {
        var root = txtRoot.Text.Trim();
        if (root.Length == 0 || !Directory.Exists(root))
        {
            MessageBox.Show(this, "请先选择存在的整理目录。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (string.Equals(Path.GetPathRoot(root), root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
        {
            // 防手滑：不直接对盘根动手（整理是移动/改名操作）。
            var confirm = MessageBox.Show(this,
                "目标目录是磁盘根目录，整理会大量移动文件，确定继续？", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes)
            {
                return;
            }
        }

        _config = AppState.LoadConfig(); // 设置窗体可能保存过 ini。
        // 下拉当前选择覆盖 ini 值（用户在主窗体刚切过)。
        _config.InfoSource.ScrapeSource = SourceChoice.All[Math.Max(cmbSource.SelectedIndex, 0)].Key;
        if (cmbCache.SelectedIndex >= 0)
        {
            _config.Misc.CacheLevel = ScrapeCacheLevels.At(cmbCache.SelectedIndex);
        }
        _checklistPath = null;
        btnStart.Enabled = false;
        btnSettings.Enabled = false;
        btnCancel.Enabled = true;
        txtLog.Clear();
        LogLine($"目标目录：{root}", isStatus: true);

        _canceller = new CancellationTokenSource();
        _rootFolder = root; // 日志显示相对路径的基准。
        var progress = new Progress<OrganizeProgressEvent>(OnProgress);
        SetBusyIndicator(true);
        try
        {
            using var environment = OrganizeDefaults.Create(_config, dataDirectory: AppState.DataDirectory);
            var report = await Task.Run(() => new OrganizePipeline(environment).RunAsync(root, progress, _canceller.Token));
            _checklistPath = report.ChecklistPath;
            LogLine($"完成：OK {report.Count(OrganizeStatus.Success)}・注意 {report.Count(OrganizeStatus.Partial)}・" +
                $"跳过 {report.Count(OrganizeStatus.Skipped)}・错误 {report.Count(OrganizeStatus.Error)}", isStatus: true);
            btnOpenChecklist.Enabled = _checklistPath is not null;
        }
        catch (OperationCanceledException)
        {
            LogLine("已取消。", isStatus: true);
        }
        catch (Exception error)
        {
            LogLine($"出错：{error.Message}", isStatus: true);
        }
        finally
        {
            _canceller.Dispose();
            _canceller = null;
            btnStart.Enabled = true;
            btnSettings.Enabled = true;
            btnCancel.Enabled = false;
            SetBusyIndicator(false);
        }
    }

    private void OnCancelClick(object? sender, EventArgs args) => _canceller?.Cancel();

    /// <summary>逐站探测连通性（10 秒/站、不重试）；结果只打布尔 + 一行结构化错误。</summary>
    private async void OnProbeClick(object? sender, EventArgs args)
    {
        _config = AppState.LoadConfig();
        btnProbe.Enabled = false;
        btnStart.Enabled = false;
        SetBusyIndicator(true);
        try
        {
            LogLine($"开始探测 {ScrapeProber.Sources.Length} 个站点入口（各 10 秒超时，不重试）…", isStatus: true);
            foreach (var source in ScrapeProber.Sources)
            {
                var result = await Task.Run(() => ScrapeProber.ProbeAsync(_config, source));
                var label = ScrapeProbeResult.LabelOf(source);
                if (result.Ok)
                {
                    var via = _config.Proxy.Enabled ? "按站点开关走代理" : "直连";
                    LogLine($"{label}：连通 ✓（{via}）", isStatus: true);
                }
                else
                {
                    LogLine($"{label}：不可连 ✗ {result.Error}", isStatus: false);
                }
            }
            LogLine("探测完成。不可连的站整理时会回退下一个候选（自动模式），或改用强制单站。", isStatus: true);
        }
        catch (Exception error)
        {
            LogLine($"探测出错：{error.Message}", isStatus: false);
        }
        finally
        {
            btnProbe.Enabled = true;
            btnStart.Enabled = true;
            SetBusyIndicator(false);
        }
    }

    private void OnBrowseClick(object? sender, EventArgs args)
    {
        var dialog = new FolderBrowserDialog
        {
            Description = "选择要整理的目标目录",
            ShowNewFolderButton = false,
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            txtRoot.Text = dialog.SelectedPath;
        }
    }

    private void OnSettingsClick(object? sender, EventArgs args)
    {
        using var dialog = new SettingsForm();
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _config = AppState.LoadConfig();
            BindSourceCombo();
            BindCacheCombo();
            txtCleanupLimit.Text = Math.Clamp(_config.Misc.CleanupSizeLimitMb, 1, 102400).ToString();
        }
    }

    private void OnTabChanged(object? sender, EventArgs args)
    {
        if (tabControl.SelectedTab != tabTools)
        {
            return;
        }
        if (txtCleanupRoot.Text.Trim().Length == 0)
        {
            txtCleanupRoot.Text = txtRoot.Text.Trim(); // 默认与整理目录一致，可直接改。
        }
        txtCleanupLimit.Text = Math.Clamp(_config.Misc.CleanupSizeLimitMb, 1, 102400).ToString();
    }

    private void OnCleanupBrowseClick(object? sender, EventArgs args)
    {
        var dialog = new FolderBrowserDialog
        {
            Description = "选择要清理的目录（递归扫描）",
            ShowNewFolderButton = false,
        };
        if (Directory.Exists(txtCleanupRoot.Text.Trim()))
        {
            dialog.SelectedPath = txtCleanupRoot.Text.Trim();
        }
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            txtCleanupRoot.Text = dialog.SelectedPath;
            gridCleanup.Rows.Clear();
            lblCleanupSummary.Text = "未扫描";
        }
    }

    private void OnCleanupUseMainClick(object? sender, EventArgs args)
    {
        var root = txtRoot.Text.Trim();
        if (root.Length == 0)
        {
            MessageBox.Show(this, "主界面还没有选择整理目录。", "文件清理",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        txtCleanupRoot.Text = root;
        gridCleanup.Rows.Clear();
        lblCleanupSummary.Text = "未扫描";
    }

    private void OnCleanupLimitLeave(object? sender, EventArgs args)
    {
        var limitMb = ReadCleanupLimit();
        txtCleanupLimit.Text = limitMb.ToString();
        PersistCleanupLimit(limitMb);
    }

    private int ReadCleanupLimit() =>
        int.TryParse(txtCleanupLimit.Text.Trim(), out var limitMb)
            ? Math.Clamp(limitMb, 1, 102400)
            : Math.Clamp(_config.Misc.CleanupSizeLimitMb, 1, 102400);

    private void PersistCleanupLimit(int limitMb)
    {
        if (_config.Misc.CleanupSizeLimitMb == limitMb)
        {
            return;
        }
        _config.Misc.CleanupSizeLimitMb = limitMb;
        try
        {
            AppState.SaveConfig(_config);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // 保存失败不影响本次会话。
        }
    }

    private async void OnCleanupScanClick(object? sender, EventArgs args)
    {
        if (_cleanupBusy)
        {
            return;
        }
        var root = txtCleanupRoot.Text.Trim();
        if (root.Length == 0)
        {
            root = txtRoot.Text.Trim();
        }
        if (root.Length == 0 || !Directory.Exists(root))
        {
            MessageBox.Show(this, "请先选择存在的清理目录。", "文件清理",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        txtCleanupRoot.Text = root;

        var limitMb = ReadCleanupLimit();
        txtCleanupLimit.Text = limitMb.ToString();
        PersistCleanupLimit(limitMb);
        var maxBytes = limitMb * 1024L * 1024L;
        var kind = SelectedCleanupKind();
        var videos = _config.Misc.ScanExtensions;
        var sortedColumn = gridCleanup.SortedColumn;
        var sortOrder = gridCleanup.SortOrder;
        SetCleanupBusy(true);
        gridCleanup.Rows.Clear();
        lblCleanupSummary.Text = "扫描中…";
        try
        {
            var files = await Task.Run(() => FileCleanup.Scan(root, maxBytes, kind, videos));
            foreach (var file in files)
            {
                var index = gridCleanup.Rows.Add(
                    false,
                    file.FileName,
                    FileCleanup.HumanSize(file.SizeBytes),
                    file.LastWriteTime.ToString("yyyy-MM-dd HH:mm"),
                    file.Directory);
                gridCleanup.Rows[index].Tag = file;
            }
            if (sortedColumn is not null && sortOrder != SortOrder.None)
            {
                gridCleanup.Sort(sortedColumn, sortOrder == SortOrder.Descending
                    ? System.ComponentModel.ListSortDirection.Descending
                    : System.ComponentModel.ListSortDirection.Ascending);
            }
            UpdateCleanupSummary();
            LogLine($"清理扫描：{root}（< {limitMb} MB，{cmbCleanupType.Text}）→ {files.Count} 个文件", isStatus: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            lblCleanupSummary.Text = "扫描失败";
            MessageBox.Show(this, $"扫描失败：{error.Message}", "文件清理",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            SetCleanupBusy(false);
        }
    }

    private void OnCleanupSelectAllClick(object? sender, EventArgs args)
    {
        foreach (DataGridViewRow row in gridCleanup.Rows)
        {
            row.Cells[0].Value = true;
        }
        UpdateCleanupSummary();
    }

    private void OnCleanupSelectNoneClick(object? sender, EventArgs args)
    {
        foreach (DataGridViewRow row in gridCleanup.Rows)
        {
            row.Cells[0].Value = false;
        }
        UpdateCleanupSummary();
    }

    private void OnCleanupGridDirtyStateChanged(object? sender, EventArgs args)
    {
        if (gridCleanup.IsCurrentCellDirty)
        {
            gridCleanup.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }
    }

    private void OnCleanupGridCellValueChanged(object? sender, DataGridViewCellEventArgs args)
    {
        if (args.ColumnIndex == 0)
        {
            UpdateCleanupSummary();
        }
    }

    /// <summary>
    /// 排序用底层数据比较：大小为字节数、修改时间为 DateTime，避免"1.0 KB"和"100.0 MB"按字符串比较。
    /// </summary>
    private void OnCleanupGridSortCompare(object? sender, DataGridViewSortCompareEventArgs args)
    {
        if (args.RowIndex1 < 0 || args.RowIndex2 < 0)
        {
            return;
        }
        if (gridCleanup.Rows[args.RowIndex1].Tag is not FileCleanupCandidate left
            || gridCleanup.Rows[args.RowIndex2].Tag is not FileCleanupCandidate right)
        {
            return;
        }

        args.SortResult = args.Column.Name switch
        {
            "colSize" => left.SizeBytes.CompareTo(right.SizeBytes),
            "colModified" => left.LastWriteTime.CompareTo(right.LastWriteTime),
            "colFolder" => string.Compare(left.Directory, right.Directory, StringComparison.OrdinalIgnoreCase),
            "colName" => string.Compare(left.FileName, right.FileName, StringComparison.OrdinalIgnoreCase),
            _ => 0,
        };
        args.Handled = true;
    }

    private async void OnCleanupDeleteClick(object? sender, EventArgs args)
    {
        if (_cleanupBusy)
        {
            return;
        }
        var selected = new List<FileCleanupCandidate>();
        foreach (DataGridViewRow row in gridCleanup.Rows)
        {
            if (row.Tag is FileCleanupCandidate file && Convert.ToBoolean(row.Cells[0].Value))
            {
                selected.Add(file);
            }
        }
        if (selected.Count == 0)
        {
            MessageBox.Show(this, "没有勾选任何文件。", "文件清理",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var total = selected.Sum(file => file.SizeBytes);
        var confirm = MessageBox.Show(this,
            $"将永久删除 {selected.Count} 个文件（合计 {FileCleanup.HumanSize(total)}）。\n\n"
            + "删除后不可恢复，确定继续？",
            "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        SetCleanupBusy(true);
        try
        {
            var paths = selected.Select(file => file.Path).ToArray();
            var result = await Task.Run(() => FileCleanup.Delete(paths));
            var failed = result.Failures.Select(failure => failure.Path)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var deleted = paths.Where(path => !failed.Contains(path))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            for (var index = gridCleanup.Rows.Count - 1; index >= 0; index--)
            {
                if (gridCleanup.Rows[index].Tag is FileCleanupCandidate file && deleted.Contains(file.Path))
                {
                    gridCleanup.Rows.RemoveAt(index);
                }
            }
            UpdateCleanupSummary();

            var summary = $"已删除 {result.Deleted} 个文件。";
            if (result.Failures.Count > 0)
            {
                var detail = string.Join("\n", result.Failures.Take(5)
                    .Select(failure => $"{Path.GetFileName(failure.Path)}：{failure.Error}"));
                if (result.Failures.Count > 5)
                {
                    detail += $"\n… 另有 {result.Failures.Count - 5} 个";
                }
                summary += $"\n失败 {result.Failures.Count} 个（可能被占用或无权限）：\n{detail}";
            }
            MessageBox.Show(this, summary, "文件清理", MessageBoxButtons.OK,
                result.Failures.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            LogLine($"文件清理：删除 {result.Deleted} 个・失败 {result.Failures.Count}", isStatus: true);
            foreach (var failure in result.Failures.Take(20))
            {
                LogLine($"  删除失败：{failure.Path}（{failure.Error}）", isStatus: false);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"删除失败：{error.Message}", "文件清理",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            SetCleanupBusy(false);
        }
    }

    private CleanupFileKind SelectedCleanupKind() => cmbCleanupType.SelectedIndex switch
    {
        1 => CleanupFileKind.Video,
        2 => CleanupFileKind.Image,
        3 => CleanupFileKind.Subtitle,
        4 => CleanupFileKind.Other,
        _ => CleanupFileKind.All,
    };

    private void UpdateCleanupSummary()
    {
        var count = gridCleanup.Rows.Count;
        if (count == 0)
        {
            lblCleanupSummary.Text = "没有符合条件的文件";
            return;
        }
        long total = 0;
        var selected = 0;
        foreach (DataGridViewRow row in gridCleanup.Rows)
        {
            if (row.Tag is FileCleanupCandidate file)
            {
                total += file.SizeBytes;
            }
            if (Convert.ToBoolean(row.Cells[0].Value))
            {
                selected++;
            }
        }
        lblCleanupSummary.Text = selected > 0
            ? $"共 {count} 个・已选 {selected} 个・合计 {FileCleanup.HumanSize(total)}"
            : $"共 {count} 个・合计 {FileCleanup.HumanSize(total)}";
    }

    private void SetCleanupBusy(bool busy)
    {
        _cleanupBusy = busy;
        btnCleanupScan.Enabled = !busy;
        btnCleanupDelete.Enabled = !busy;
        btnCleanupSelectAll.Enabled = !busy;
        btnCleanupSelectNone.Enabled = !busy;
        btnCleanupBrowse.Enabled = !busy;
        btnCleanupUseMain.Enabled = !busy;
        txtCleanupLimit.Enabled = !busy;
        cmbCleanupType.Enabled = !busy;
        txtCleanupRoot.Enabled = !busy;
        UseWaitCursor = busy;
    }

    private void OnOpenChecklistClick(object? sender, EventArgs args)
    {
        if (_checklistPath is not null && File.Exists(_checklistPath))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_checklistPath}\""));
        }
    }

    /// <summary>手动生成入库明细报表（默认最近 180 天），生成后用默认浏览器打开。</summary>
    private void OnStatsReportClick(object? sender, EventArgs args)
    {
        var dataDirectory = AppState.DataDirectory;
        var logDirectory = Path.Combine(dataDirectory, "logs");
        if (!ScrapeLogReader.HasLogs(logDirectory))
        {
            MessageBox.Show(this,
                "还没有入库流水。先跑一次整理（缓存等级不能是「完全不使用缓存」）再生成报表。",
                "入库明细报表", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _config = AppState.LoadConfig();
        try
        {
            var report = InboundHtmlReportWriter.Generate(
                logDirectory,
                Path.Combine(dataDirectory, "scrape-cache.db"),
                DateTime.Today.AddDays(-179),
                DateTime.Today.AddDays(1).AddTicks(-1),
                outputDirectory: Path.Combine(dataDirectory, "reports"),
                dictionary: OrganizeDefaults.LoadFeatureDictionary(dataDirectory),
                script: _config.Misc.Script);
            var inbound = report.Report.View(InboundView.Inbound);
            LogLine($"入库报表：{report.Path}（车牌 {inbound.DistinctCodes}・流水 {inbound.Total} 笔）", isStatus: true);
            Process.Start(new ProcessStartInfo(report.Path) { UseShellExecute = true });
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException
            or Microsoft.Data.Sqlite.SqliteException)
        {
            MessageBox.Show(this, $"生成报表失败：{error.Message}", "入库明细报表",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    /// <summary>打开译名表（不存在时先写空模板），用系统默认文本编辑器维护。</summary>
    private void OnTranslationFileClick(object? sender, EventArgs args)
    {
        var path = TranslationCatalog.DefaultPathFor(AppState.DataDirectory);
        try
        {
            TranslationCatalog.EnsureFile(path);
            LogLine($"语言文件：{path}", isStatus: true);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException
            or System.ComponentModel.Win32Exception)
        {
            // 没有关联程序时退回记事本，避免按钮直接报错。
            try
            {
                Process.Start(new ProcessStartInfo("notepad.exe", $"\"{path}\"")
                {
                    UseShellExecute = true,
                });
            }
            catch (Exception fallback) when (fallback is System.ComponentModel.Win32Exception or IOException)
            {
                MessageBox.Show(this,
                    $"打不开语言文件：\n{path}\n\n{error.Message}",
                    "语言文件", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    private void OnProgress(OrganizeProgressEvent evt)
    {
        if (evt.Outcome is { } outcome)
        {
            LogLine(FormatOutcome(outcome), isStatus: false);
        }
        else if (evt.Status is { } status)
        {
            LogLine(status, isStatus: true);
        }
    }

    private string _rootFolder = ""; // 本次整理的扫描根（日志唯一显示全路径地，后续行显示相对路径）。

    private string DisplayPath(string path)
    {
        if (_rootFolder.Length == 0)
        {
            return path;
        }
        var prefix = _rootFolder.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? path[prefix.Length..] : path;
    }

    private string FormatOutcome(OrganizeOutcome outcome)
    {
        var status = outcome.Status switch
        {
            OrganizeStatus.Success => "OK  ",
            OrganizeStatus.Partial => "注意",
            OrganizeStatus.Skipped => "跳过",
            _ => "错误",
        };
        var from = DisplayPath(outcome.OriginalPath);
        var to = outcome.NewPath is null ? "" : $" → {DisplayPath(outcome.NewPath)}";
        return $"[{status}] {outcome.CarCode ?? "无车牌"}：{outcome.Message}\n        {from}{to}";
    }

    private void LogLine(string text, bool isStatus)
    {
        if (isStatus)
        {
            _statusText = text;
            RenderStatus();
        }
        var wasAtBottom = IsLogAtBottom();
        if (txtLog.TextLength > 0)
        {
            txtLog.AppendText(Environment.NewLine);
        }
        txtLog.SelectionStart = txtLog.TextLength;
        txtLog.SelectionLength = 0;
        txtLog.SelectionColor = isStatus ? Color.FromArgb(0, 90, 160) : SystemColors.ControlText;
        txtLog.AppendText(text);
        txtLog.SelectionColor = txtLog.ForeColor;
        if (wasAtBottom)
        {
            ScrollLogToBottom();
        }
    }

    /// <summary>
    /// 日志框当前是否停在底部。用户往上翻看旧记录时返回 false，此时不抢滚动位置；
    /// 控件不可见（在另一个页签/最小化）时视为在底部，保证切回来能看到最新一行。
    /// </summary>
    private bool IsLogAtBottom()
    {
        if (txtLog.TextLength == 0 || !txtLog.IsHandleCreated || !txtLog.Visible)
        {
            return true;
        }
        var last = txtLog.GetPositionFromCharIndex(txtLog.TextLength - 1);
        return last.Y <= txtLog.ClientSize.Height - txtLog.Font.Height;
    }

    /// <summary>直接发 WM_VSCROLL/SB_BOTTOM：不需要焦点，也不会清掉用户已有的文本选择。</summary>
    private void ScrollLogToBottom()
    {
        if (txtLog.IsHandleCreated)
        {
            SendMessage(txtLog.Handle, WmVsCroll, (IntPtr)SbBottom, IntPtr.Zero);
        }
    }

    private string DescriptionOfProxyKind() => _config.Proxy.Server.Length == 0
        ? "无"
        : _config.Proxy.Server;

    /// <summary>开启/结束忙碌指示：状态文字前缀逐帧旋转，不额外占用布局空间。</summary>
    private void SetBusyIndicator(bool busy)
    {
        _busyIndicator = busy;
        _busyFrame = 0;
        if (busy)
        {
            _busyTimer.Start();
        }
        else
        {
            _busyTimer.Stop();
        }
        RenderStatus();
    }

    private void OnBusyTick(object? sender, EventArgs args)
    {
        _busyFrame = (_busyFrame + 1) % BusyFrames.Length;
        RenderStatus();
    }

    private void RenderStatus() =>
        lblStatus.Text = _busyIndicator && _statusText.Length > 0
            ? $"{BusyFrames[_busyFrame]}  {_statusText}"
            : _statusText;
}