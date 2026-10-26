namespace HyperJav.App;

partial class MainForm
{
    /// <summary>
    ///  Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    ///  Clean up any resources being used.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private TabControl tabControl;
    private TabPage tabMain;
    private TabPage tabTools;
    private TextBox txtRoot;
    private Button btnBrowse;
    private Button btnSettings;
    private Button btnStart;
    private Button btnCancel;
    private Button btnOpenChecklist;
    private ComboBox cmbSource;
    private ComboBox cmbCache;
    private Button btnProbe;
    private Button btnStatsReport;
    private Button btnTranslationFile;
    private Label lblStatus;
    private RichTextBox txtLog;
    private TableLayoutPanel tableTop;
    private TableLayoutPanel rowPath;
    private TableLayoutPanel rowOptions;

    private TableLayoutPanel tableTools;
    private GroupBox grpReport;
    private TableLayoutPanel tableReport;
    private Label lblReportHint;
    private GroupBox grpCleanup;
    private TableLayoutPanel tableCleanup;
    private TableLayoutPanel rowCleanupRoot;
    private Label lblCleanupRoot;
    private TextBox txtCleanupRoot;
    private Button btnCleanupBrowse;
    private Button btnCleanupUseMain;
    private TableLayoutPanel rowCleanupFilter;
    private Label lblCleanupLimit;
    private TextBox txtCleanupLimit;
    private Label lblCleanupUnit;
    private Label lblCleanupType;
    private ComboBox cmbCleanupType;
    private Button btnCleanupScan;
    private Label lblCleanupSummary;
    private TableLayoutPanel rowCleanupActions;
    private Button btnCleanupSelectAll;
    private Button btnCleanupSelectNone;
    private Button btnCleanupDelete;
    private Label lblCleanupWarning;
    private DataGridView gridCleanup;

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        tabControl = new TabControl();
        tabMain = new TabPage();
        tabTools = new TabPage();
        tableTop = new TableLayoutPanel();
        rowPath = new TableLayoutPanel();
        rowOptions = new TableLayoutPanel();
        txtRoot = new TextBox();
        btnBrowse = new Button();
        btnSettings = new Button();
        btnStart = new Button();
        btnCancel = new Button();
        btnOpenChecklist = new Button();
        lblStatus = new Label();
        txtLog = new RichTextBox();
        cmbSource = new ComboBox();
        cmbCache = new ComboBox();
        btnProbe = new Button();
        tableTools = new TableLayoutPanel();
        grpReport = new GroupBox();
        tableReport = new TableLayoutPanel();
        lblReportHint = new Label();
        btnStatsReport = new Button();
        btnTranslationFile = new Button();
        grpCleanup = new GroupBox();
        tableCleanup = new TableLayoutPanel();
        rowCleanupRoot = new TableLayoutPanel();
        lblCleanupRoot = new Label();
        txtCleanupRoot = new TextBox();
        btnCleanupBrowse = new Button();
        btnCleanupUseMain = new Button();
        rowCleanupFilter = new TableLayoutPanel();
        lblCleanupLimit = new Label();
        txtCleanupLimit = new TextBox();
        lblCleanupUnit = new Label();
        lblCleanupType = new Label();
        cmbCleanupType = new ComboBox();
        btnCleanupScan = new Button();
        lblCleanupSummary = new Label();
        rowCleanupActions = new TableLayoutPanel();
        btnCleanupSelectAll = new Button();
        btnCleanupSelectNone = new Button();
        btnCleanupDelete = new Button();
        lblCleanupWarning = new Label();
        gridCleanup = new DataGridView();

        tabControl.SuspendLayout();
        tabMain.SuspendLayout();
        tabTools.SuspendLayout();
        tableTop.SuspendLayout();
        rowPath.SuspendLayout();
        rowOptions.SuspendLayout();
        tableTools.SuspendLayout();
        grpReport.SuspendLayout();
        tableReport.SuspendLayout();
        grpCleanup.SuspendLayout();
        tableCleanup.SuspendLayout();
        rowCleanupRoot.SuspendLayout();
        rowCleanupFilter.SuspendLayout();
        rowCleanupActions.SuspendLayout();
        SuspendLayout();

        //
        // tabControl
        //
        tabControl.Controls.Add(tabMain);
        tabControl.Controls.Add(tabTools);
        tabControl.Dock = DockStyle.Fill;
        tabControl.Location = new Point(0, 0);
        tabControl.Name = "tabControl";
        tabControl.SelectedIndex = 0;
        tabControl.Size = new Size(1000, 640);
        tabControl.TabIndex = 0;
        //
        // tabMain
        //
        tabMain.Controls.Add(txtLog);
        tabMain.Controls.Add(tableTop);
        tabMain.Location = new Point(4, 28);
        tabMain.Name = "tabMain";
        tabMain.Padding = new Padding(3);
        tabMain.Size = new Size(992, 608);
        tabMain.TabIndex = 0;
        tabMain.Text = "整理";
        tabMain.UseVisualStyleBackColor = true;
        //
        // tableTop
        //
        tableTop.ColumnCount = 1;
        tableTop.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tableTop.Controls.Add(rowPath, 0, 0);
        tableTop.Controls.Add(rowOptions, 0, 1);
        tableTop.Dock = DockStyle.Top;
        tableTop.Location = new Point(3, 3);
        tableTop.Name = "tableTop";
        tableTop.RowCount = 2;
        tableTop.RowStyles.Add(new RowStyle(SizeType.Absolute, 27F));
        tableTop.RowStyles.Add(new RowStyle(SizeType.Absolute, 27F));
        tableTop.Size = new Size(986, 54);
        tableTop.TabIndex = 0;
        //
        // rowPath
        //
        rowPath.ColumnCount = 4;
        rowPath.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rowPath.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));
        rowPath.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F));
        rowPath.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));
        rowPath.Controls.Add(txtRoot, 0, 0);
        rowPath.Controls.Add(btnBrowse, 1, 0);
        rowPath.Controls.Add(btnStart, 2, 0);
        rowPath.Controls.Add(btnCancel, 3, 0);
        rowPath.Dock = DockStyle.Fill;
        rowPath.Location = new Point(0, 0);
        rowPath.Margin = new Padding(0);
        rowPath.Name = "rowPath";
        rowPath.RowCount = 1;
        rowPath.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rowPath.Size = new Size(986, 27);
        rowPath.TabIndex = 0;
        //
        // rowOptions
        //
        rowOptions.ColumnCount = 6;
        rowOptions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rowOptions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));
        rowOptions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));
        rowOptions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F));
        rowOptions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F));
        rowOptions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));
        rowOptions.Controls.Add(lblStatus, 0, 0);
        rowOptions.Controls.Add(btnSettings, 1, 0);
        rowOptions.Controls.Add(btnOpenChecklist, 2, 0);
        rowOptions.Controls.Add(cmbSource, 3, 0);
        rowOptions.Controls.Add(cmbCache, 4, 0);
        rowOptions.Controls.Add(btnProbe, 5, 0);
        rowOptions.Dock = DockStyle.Fill;
        rowOptions.Location = new Point(0, 27);
        rowOptions.Margin = new Padding(0);
        rowOptions.Name = "rowOptions";
        rowOptions.RowCount = 1;
        rowOptions.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rowOptions.Size = new Size(986, 27);
        rowOptions.TabIndex = 1;
        //
        // txtRoot
        //
        txtRoot.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        txtRoot.Location = new Point(4, 2);
        txtRoot.Margin = new Padding(4, 2, 4, 2);
        txtRoot.Name = "txtRoot";
        txtRoot.Size = new Size(658, 23);
        txtRoot.TabIndex = 0;
        //
        // btnBrowse
        //
        btnBrowse.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        btnBrowse.Location = new Point(670, 2);
        btnBrowse.Margin = new Padding(4, 2, 4, 2);
        btnBrowse.Name = "btnBrowse";
        btnBrowse.Size = new Size(82, 23);
        btnBrowse.TabIndex = 1;
        btnBrowse.Text = "浏览…";
        btnBrowse.UseVisualStyleBackColor = true;
        //
        // btnSettings
        //
        btnSettings.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        btnSettings.Location = new Point(440, 2);
        btnSettings.Margin = new Padding(4, 2, 4, 2);
        btnSettings.Name = "btnSettings";
        btnSettings.Size = new Size(82, 23);
        btnSettings.TabIndex = 2;
        btnSettings.Text = "设置…";
        btnSettings.UseVisualStyleBackColor = true;
        //
        // btnStart
        //
        btnStart.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        btnStart.Location = new Point(760, 2);
        btnStart.Margin = new Padding(4, 2, 4, 2);
        btnStart.Name = "btnStart";
        btnStart.Size = new Size(132, 23);
        btnStart.TabIndex = 3;
        btnStart.Text = "开始整理";
        btnStart.UseVisualStyleBackColor = true;
        //
        // cmbSource
        //
        cmbSource.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        cmbSource.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbSource.FormattingEnabled = true;
        cmbSource.Location = new Point(620, 2);
        cmbSource.Margin = new Padding(4, 2, 4, 2);
        cmbSource.Name = "cmbSource";
        cmbSource.Size = new Size(132, 23);
        cmbSource.TabIndex = 7;
        //
        // cmbCache
        //
        cmbCache.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        cmbCache.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbCache.FormattingEnabled = true;
        cmbCache.Location = new Point(760, 2);
        cmbCache.Margin = new Padding(4, 2, 4, 2);
        cmbCache.Name = "cmbCache";
        cmbCache.Size = new Size(132, 23);
        cmbCache.TabIndex = 10;
        //
        // btnCancel
        //
        btnCancel.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        btnCancel.Enabled = false;
        btnCancel.Location = new Point(900, 2);
        btnCancel.Margin = new Padding(4, 2, 4, 2);
        btnCancel.Name = "btnCancel";
        btnCancel.Size = new Size(82, 23);
        btnCancel.TabIndex = 5;
        btnCancel.Text = "取消";
        btnCancel.UseVisualStyleBackColor = true;
        //
        // btnOpenChecklist
        //
        btnOpenChecklist.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        btnOpenChecklist.Enabled = false;
        btnOpenChecklist.Location = new Point(530, 2);
        btnOpenChecklist.Margin = new Padding(4, 2, 4, 2);
        btnOpenChecklist.Name = "btnOpenChecklist";
        btnOpenChecklist.Size = new Size(82, 23);
        btnOpenChecklist.TabIndex = 6;
        btnOpenChecklist.Text = "整理清单";
        btnOpenChecklist.UseVisualStyleBackColor = true;
        //
        // btnProbe
        //
        btnProbe.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        btnProbe.Location = new Point(900, 2);
        btnProbe.Margin = new Padding(4, 2, 4, 2);
        btnProbe.Name = "btnProbe";
        btnProbe.Size = new Size(82, 23);
        btnProbe.TabIndex = 8;
        btnProbe.Text = "测连通";
        btnProbe.UseVisualStyleBackColor = true;
        //
        // lblStatus
        //
        lblStatus.Dock = DockStyle.Fill;
        lblStatus.Location = new Point(4, 2);
        lblStatus.Margin = new Padding(4, 2, 4, 2);
        lblStatus.Name = "lblStatus";
        lblStatus.Size = new Size(428, 23);
        lblStatus.TabIndex = 9;
        lblStatus.TextAlign = ContentAlignment.MiddleLeft;
        lblStatus.AutoEllipsis = true;
        //
        // txtLog
        //
        txtLog.BackColor = SystemColors.Window;
        txtLog.Dock = DockStyle.Fill;
        txtLog.Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point);
        txtLog.Location = new Point(3, 57);
        txtLog.Multiline = true;
        txtLog.Name = "txtLog";
        txtLog.ReadOnly = true;
        txtLog.ScrollBars = RichTextBoxScrollBars.Vertical;
        txtLog.Size = new Size(986, 548);
        txtLog.TabIndex = 1;
        //
        // tabTools
        //
        tabTools.Controls.Add(tableTools);
        tabTools.Location = new Point(4, 28);
        tabTools.Name = "tabTools";
        tabTools.Padding = new Padding(6);
        tabTools.Size = new Size(992, 608);
        tabTools.TabIndex = 1;
        tabTools.Text = "报表与清理";
        tabTools.UseVisualStyleBackColor = true;
        //
        // tableTools
        //
        tableTools.ColumnCount = 1;
        tableTools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tableTools.Controls.Add(grpReport, 0, 0);
        tableTools.Controls.Add(grpCleanup, 0, 1);
        tableTools.Dock = DockStyle.Fill;
        tableTools.Location = new Point(6, 6);
        tableTools.Name = "tableTools";
        tableTools.RowCount = 2;
        tableTools.RowStyles.Add(new RowStyle(SizeType.Absolute, 96F));
        tableTools.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tableTools.Size = new Size(980, 596);
        tableTools.TabIndex = 0;
        //
        // grpReport
        //
        grpReport.Controls.Add(tableReport);
        grpReport.Dock = DockStyle.Fill;
        grpReport.Location = new Point(3, 3);
        grpReport.Name = "grpReport";
        grpReport.Padding = new Padding(10, 6, 10, 6);
        grpReport.Size = new Size(974, 90);
        grpReport.TabIndex = 0;
        grpReport.TabStop = false;
        grpReport.Text = "入库明细报表";
        //
        // tableReport
        //
        tableReport.ColumnCount = 3;
        tableReport.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tableReport.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116F));
        tableReport.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
        tableReport.Controls.Add(lblReportHint, 0, 0);
        tableReport.Controls.Add(btnTranslationFile, 1, 0);
        tableReport.Controls.Add(btnStatsReport, 2, 0);
        tableReport.Dock = DockStyle.Fill;
        tableReport.Location = new Point(10, 26);
        tableReport.Name = "tableReport";
        tableReport.RowCount = 1;
        tableReport.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tableReport.Size = new Size(954, 58);
        tableReport.TabIndex = 0;
        //
        // lblReportHint
        //
        lblReportHint.Dock = DockStyle.Fill;
        lblReportHint.Location = new Point(3, 0);
        lblReportHint.Name = "lblReportHint";
        lblReportHint.Size = new Size(682, 58);
        lblReportHint.TabIndex = 0;
        lblReportHint.Text = "从入库流水生成 HTML 报表（默认最近 180 天，生成后自动打开浏览器）。片商/系列/演员/特征/类别的中文显示名可在「语言文件」里维护。";
        lblReportHint.TextAlign = ContentAlignment.MiddleLeft;
        //
        // btnTranslationFile
        //
        btnTranslationFile.Anchor = AnchorStyles.Right;
        btnTranslationFile.Location = new Point(691, 11);
        btnTranslationFile.Name = "btnTranslationFile";
        btnTranslationFile.Size = new Size(108, 36);
        btnTranslationFile.TabIndex = 1;
        btnTranslationFile.Text = "语言文件";
        btnTranslationFile.UseVisualStyleBackColor = true;
        //
        // btnStatsReport
        //
        btnStatsReport.Anchor = AnchorStyles.Right;
        btnStatsReport.Location = new Point(814, 11);
        btnStatsReport.Name = "btnStatsReport";
        btnStatsReport.Size = new Size(137, 36);
        btnStatsReport.TabIndex = 2;
        btnStatsReport.Text = "生成报表";
        btnStatsReport.UseVisualStyleBackColor = true;
        //
        // grpCleanup
        //
        grpCleanup.Controls.Add(tableCleanup);
        grpCleanup.Dock = DockStyle.Fill;
        grpCleanup.Location = new Point(3, 99);
        grpCleanup.Name = "grpCleanup";
        grpCleanup.Padding = new Padding(10, 6, 10, 8);
        grpCleanup.Size = new Size(974, 494);
        grpCleanup.TabIndex = 1;
        grpCleanup.TabStop = false;
        grpCleanup.Text = "文件清理（永久删除，不进回收站）";
        //
        // tableCleanup
        //
        tableCleanup.ColumnCount = 1;
        tableCleanup.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tableCleanup.Controls.Add(rowCleanupRoot, 0, 0);
        tableCleanup.Controls.Add(rowCleanupFilter, 0, 1);
        tableCleanup.Controls.Add(rowCleanupActions, 0, 2);
        tableCleanup.Controls.Add(gridCleanup, 0, 3);
        tableCleanup.Dock = DockStyle.Fill;
        tableCleanup.Location = new Point(10, 26);
        tableCleanup.Name = "tableCleanup";
        tableCleanup.RowCount = 4;
        tableCleanup.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
        tableCleanup.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
        tableCleanup.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
        tableCleanup.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tableCleanup.Size = new Size(954, 460);
        tableCleanup.TabIndex = 0;
        //
        // rowCleanupRoot
        //
        rowCleanupRoot.ColumnCount = 4;
        rowCleanupRoot.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        rowCleanupRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rowCleanupRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));
        rowCleanupRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110F));
        rowCleanupRoot.Controls.Add(lblCleanupRoot, 0, 0);
        rowCleanupRoot.Controls.Add(txtCleanupRoot, 1, 0);
        rowCleanupRoot.Controls.Add(btnCleanupBrowse, 2, 0);
        rowCleanupRoot.Controls.Add(btnCleanupUseMain, 3, 0);
        rowCleanupRoot.Dock = DockStyle.Fill;
        rowCleanupRoot.Location = new Point(3, 3);
        rowCleanupRoot.Name = "rowCleanupRoot";
        rowCleanupRoot.RowCount = 1;
        rowCleanupRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rowCleanupRoot.Size = new Size(948, 30);
        rowCleanupRoot.TabIndex = 0;
        //
        // lblCleanupRoot
        //
        lblCleanupRoot.Anchor = AnchorStyles.Left;
        lblCleanupRoot.AutoSize = true;
        lblCleanupRoot.Location = new Point(4, 5);
        lblCleanupRoot.Margin = new Padding(4, 0, 4, 0);
        lblCleanupRoot.Name = "lblCleanupRoot";
        lblCleanupRoot.Size = new Size(48, 20);
        lblCleanupRoot.TabIndex = 0;
        lblCleanupRoot.Text = "目录：";
        //
        // txtCleanupRoot
        //
        txtCleanupRoot.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        txtCleanupRoot.Location = new Point(60, 1);
        txtCleanupRoot.Margin = new Padding(4, 2, 4, 2);
        txtCleanupRoot.Name = "txtCleanupRoot";
        txtCleanupRoot.Size = new Size(663, 29);
        txtCleanupRoot.TabIndex = 1;
        //
        // btnCleanupBrowse
        //
        btnCleanupBrowse.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        btnCleanupBrowse.Location = new Point(731, 2);
        btnCleanupBrowse.Margin = new Padding(4, 2, 4, 2);
        btnCleanupBrowse.Name = "btnCleanupBrowse";
        btnCleanupBrowse.Size = new Size(82, 26);
        btnCleanupBrowse.TabIndex = 2;
        btnCleanupBrowse.Text = "浏览…";
        btnCleanupBrowse.UseVisualStyleBackColor = true;
        //
        // btnCleanupUseMain
        //
        btnCleanupUseMain.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        btnCleanupUseMain.Location = new Point(821, 2);
        btnCleanupUseMain.Margin = new Padding(4, 2, 4, 2);
        btnCleanupUseMain.Name = "btnCleanupUseMain";
        btnCleanupUseMain.Size = new Size(102, 26);
        btnCleanupUseMain.TabIndex = 3;
        btnCleanupUseMain.Text = "同整理目录";
        btnCleanupUseMain.UseVisualStyleBackColor = true;
        //
        // rowCleanupFilter
        //
        rowCleanupFilter.ColumnCount = 7;
        rowCleanupFilter.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        rowCleanupFilter.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));
        rowCleanupFilter.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        rowCleanupFilter.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        rowCleanupFilter.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
        rowCleanupFilter.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 94F));
        rowCleanupFilter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rowCleanupFilter.Controls.Add(lblCleanupLimit, 0, 0);
        rowCleanupFilter.Controls.Add(txtCleanupLimit, 1, 0);
        rowCleanupFilter.Controls.Add(lblCleanupUnit, 2, 0);
        rowCleanupFilter.Controls.Add(lblCleanupType, 3, 0);
        rowCleanupFilter.Controls.Add(cmbCleanupType, 4, 0);
        rowCleanupFilter.Controls.Add(btnCleanupScan, 5, 0);
        rowCleanupFilter.Controls.Add(lblCleanupSummary, 6, 0);
        rowCleanupFilter.Dock = DockStyle.Fill;
        rowCleanupFilter.Location = new Point(3, 39);
        rowCleanupFilter.Name = "rowCleanupFilter";
        rowCleanupFilter.RowCount = 1;
        rowCleanupFilter.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rowCleanupFilter.Size = new Size(948, 32);
        rowCleanupFilter.TabIndex = 1;
        //
        // lblCleanupLimit
        //
        lblCleanupLimit.Dock = DockStyle.Fill;
        lblCleanupLimit.Margin = new Padding(4, 4, 2, 4);
        lblCleanupLimit.Name = "lblCleanupLimit";
        lblCleanupLimit.Size = new Size(36, 24);
        lblCleanupLimit.TabIndex = 0;
        lblCleanupLimit.Text = "小于";
        lblCleanupLimit.TextAlign = ContentAlignment.MiddleLeft;
        //
        // txtCleanupLimit
        //
        txtCleanupLimit.Dock = DockStyle.Fill;
        txtCleanupLimit.Margin = new Padding(4, 4, 4, 4);
        txtCleanupLimit.Name = "txtCleanupLimit";
        txtCleanupLimit.Size = new Size(82, 24);
        txtCleanupLimit.TabIndex = 1;
        txtCleanupLimit.Text = "100";
        txtCleanupLimit.TextAlign = HorizontalAlignment.Right;
        //
        // lblCleanupUnit
        //
        lblCleanupUnit.Dock = DockStyle.Fill;
        lblCleanupUnit.Margin = new Padding(4, 4, 14, 4);
        lblCleanupUnit.Name = "lblCleanupUnit";
        lblCleanupUnit.Size = new Size(66, 24);
        lblCleanupUnit.TabIndex = 2;
        lblCleanupUnit.Text = "MB 的文件";
        lblCleanupUnit.TextAlign = ContentAlignment.MiddleLeft;
        //
        // lblCleanupType
        //
        lblCleanupType.Dock = DockStyle.Fill;
        lblCleanupType.Margin = new Padding(4, 4, 2, 4);
        lblCleanupType.Name = "lblCleanupType";
        lblCleanupType.Size = new Size(36, 24);
        lblCleanupType.TabIndex = 3;
        lblCleanupType.Text = "类型";
        lblCleanupType.TextAlign = ContentAlignment.MiddleLeft;
        //
        // cmbCleanupType
        //
        cmbCleanupType.Dock = DockStyle.Fill;
        cmbCleanupType.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbCleanupType.FormattingEnabled = true;
        cmbCleanupType.Items.AddRange(new object[] { "全部文件", "仅视频", "仅图片", "仅字幕", "其他文件" });
        cmbCleanupType.Margin = new Padding(4, 4, 10, 4);
        cmbCleanupType.Name = "cmbCleanupType";
        cmbCleanupType.Size = new Size(116, 24);
        cmbCleanupType.TabIndex = 4;
        //
        // btnCleanupScan
        //
        btnCleanupScan.Dock = DockStyle.Fill;
        btnCleanupScan.Margin = new Padding(4, 4, 12, 4);
        btnCleanupScan.Name = "btnCleanupScan";
        btnCleanupScan.Size = new Size(78, 24);
        btnCleanupScan.TabIndex = 5;
        btnCleanupScan.Text = "扫描";
        btnCleanupScan.UseVisualStyleBackColor = true;
        //
        // lblCleanupSummary
        //
        lblCleanupSummary.Dock = DockStyle.Fill;
        lblCleanupSummary.ForeColor = SystemColors.GrayText;
        lblCleanupSummary.Margin = new Padding(4, 4, 4, 4);
        lblCleanupSummary.Name = "lblCleanupSummary";
        lblCleanupSummary.Size = new Size(614, 24);
        lblCleanupSummary.TabIndex = 6;
        lblCleanupSummary.Text = "未扫描";
        lblCleanupSummary.TextAlign = ContentAlignment.MiddleLeft;
        //
        // rowCleanupActions
        //
        rowCleanupActions.ColumnCount = 4;
        rowCleanupActions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88F));
        rowCleanupActions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88F));
        rowCleanupActions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108F));
        rowCleanupActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rowCleanupActions.Controls.Add(btnCleanupSelectAll, 0, 0);
        rowCleanupActions.Controls.Add(btnCleanupSelectNone, 1, 0);
        rowCleanupActions.Controls.Add(btnCleanupDelete, 2, 0);
        rowCleanupActions.Controls.Add(lblCleanupWarning, 3, 0);
        rowCleanupActions.Dock = DockStyle.Fill;
        rowCleanupActions.Location = new Point(3, 77);
        rowCleanupActions.Name = "rowCleanupActions";
        rowCleanupActions.RowCount = 1;
        rowCleanupActions.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rowCleanupActions.Size = new Size(948, 30);
        rowCleanupActions.TabIndex = 2;
        //
        // btnCleanupSelectAll
        //
        btnCleanupSelectAll.Dock = DockStyle.Fill;
        btnCleanupSelectAll.Margin = new Padding(4);
        btnCleanupSelectAll.Name = "btnCleanupSelectAll";
        btnCleanupSelectAll.Size = new Size(80, 22);
        btnCleanupSelectAll.TabIndex = 0;
        btnCleanupSelectAll.Text = "全选";
        btnCleanupSelectAll.UseVisualStyleBackColor = true;
        //
        // btnCleanupSelectNone
        //
        btnCleanupSelectNone.Dock = DockStyle.Fill;
        btnCleanupSelectNone.Margin = new Padding(4);
        btnCleanupSelectNone.Name = "btnCleanupSelectNone";
        btnCleanupSelectNone.Size = new Size(80, 22);
        btnCleanupSelectNone.TabIndex = 1;
        btnCleanupSelectNone.Text = "全不选";
        btnCleanupSelectNone.UseVisualStyleBackColor = true;
        //
        // btnCleanupDelete
        //
        btnCleanupDelete.Dock = DockStyle.Fill;
        btnCleanupDelete.ForeColor = Color.FromArgb(170, 30, 30);
        btnCleanupDelete.Margin = new Padding(4);
        btnCleanupDelete.Name = "btnCleanupDelete";
        btnCleanupDelete.Size = new Size(100, 22);
        btnCleanupDelete.TabIndex = 2;
        btnCleanupDelete.Text = "删除选中";
        btnCleanupDelete.UseVisualStyleBackColor = true;
        //
        // lblCleanupWarning
        //
        lblCleanupWarning.Dock = DockStyle.Fill;
        lblCleanupWarning.ForeColor = Color.FromArgb(170, 30, 30);
        lblCleanupWarning.Margin = new Padding(4, 4, 4, 4);
        lblCleanupWarning.Name = "lblCleanupWarning";
        lblCleanupWarning.Size = new Size(660, 22);
        lblCleanupWarning.TabIndex = 3;
        lblCleanupWarning.Text = "删除为永久操作，不进回收站，请谨慎勾选。";
        lblCleanupWarning.TextAlign = ContentAlignment.MiddleLeft;
        //
        // gridCleanup
        //
        gridCleanup.AllowUserToAddRows = false;
        gridCleanup.AllowUserToDeleteRows = false;
        gridCleanup.AllowUserToResizeRows = false;
        gridCleanup.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        gridCleanup.BackgroundColor = SystemColors.Window;
        gridCleanup.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        gridCleanup.Columns.AddRange(
            new DataGridViewColumn[]
            {
                new DataGridViewCheckBoxColumn
                {
                    HeaderText = "", Name = "colSelected", Width = 36, FillWeight = 8,
                    Resizable = DataGridViewTriState.False, SortMode = DataGridViewColumnSortMode.NotSortable,
                },
                new DataGridViewTextBoxColumn
                {
                    HeaderText = "文件名", Name = "colName", ReadOnly = true, FillWeight = 32,
                },
                new DataGridViewTextBoxColumn
                {
                    HeaderText = "大小", Name = "colSize", ReadOnly = true, FillWeight = 14,
                },
                new DataGridViewTextBoxColumn
                {
                    HeaderText = "修改时间", Name = "colModified", ReadOnly = true, FillWeight = 22,
                },
                new DataGridViewTextBoxColumn
                {
                    HeaderText = "所在目录", Name = "colFolder", ReadOnly = true, FillWeight = 48,
                },
            });
        gridCleanup.Dock = DockStyle.Fill;
        gridCleanup.Location = new Point(3, 113);
        gridCleanup.MultiSelect = false;
        gridCleanup.Name = "gridCleanup";
        gridCleanup.RowHeadersVisible = false;
        gridCleanup.RowTemplate.Height = 24;
        gridCleanup.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        gridCleanup.Size = new Size(948, 344);
        gridCleanup.TabIndex = 3;
        //
        // MainForm
        //
        AutoScaleDimensions = new SizeF(7F, 17F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1000, 640);
        Controls.Add(tabControl);
        MinimumSize = new Size(840, 540);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "HyperJav —— 影片刮削整理";

        ((System.ComponentModel.ISupportInitialize)gridCleanup).EndInit();
        tabControl.ResumeLayout(false);
        tabMain.ResumeLayout(false);
        tabTools.ResumeLayout(false);
        tableTop.ResumeLayout(false);
        tableTop.PerformLayout();
        rowPath.ResumeLayout(false);
        rowPath.PerformLayout();
        rowOptions.ResumeLayout(false);
        rowOptions.PerformLayout();
        tableTools.ResumeLayout(false);
        grpReport.ResumeLayout(false);
        tableReport.ResumeLayout(false);
        grpCleanup.ResumeLayout(false);
        tableCleanup.ResumeLayout(false);
        rowCleanupRoot.ResumeLayout(false);
        rowCleanupRoot.PerformLayout();
        rowCleanupFilter.ResumeLayout(false);
        rowCleanupFilter.PerformLayout();
        rowCleanupActions.ResumeLayout(false);
        rowCleanupActions.PerformLayout();
        ResumeLayout(false);
    }

    #endregion
}