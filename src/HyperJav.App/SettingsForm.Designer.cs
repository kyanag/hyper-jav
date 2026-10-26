namespace HyperJav.App;

partial class SettingsForm
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

    private Panel rowsHost;
    private TableLayoutPanel rows;
    private TableLayoutPanel tableButtons;
    private Button btnOk;
    private Button btnCancelForm;

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        rowsHost = new Panel();
        rows = new TableLayoutPanel();
        tableButtons = new TableLayoutPanel();
        btnOk = new Button();
        btnCancelForm = new Button();
        rowsHost.SuspendLayout();
        rows.SuspendLayout();
        tableButtons.SuspendLayout();
        SuspendLayout();
        //
        // rowsHost — 滚动容器（普通 Panel：按子控件实际高度计算滚动范围，
        // 避免 TableLayoutPanel 的 AutoScroll 在自动换行标签撑高后算不满、滚不到底）
        //
        rowsHost.AutoScroll = true;
        rowsHost.Dock = DockStyle.Fill;
        rowsHost.Location = new Point(0, 0);
        rowsHost.Name = "rowsHost";
        rowsHost.Padding = new Padding(8, 8, 8, 0);
        rowsHost.Size = new Size(560, 610);
        rowsHost.TabIndex = 0;
        //
        // rows — 绑定行容器（代码填充；AutoSize 随行数撑高）
        //
        rows.AutoSize = true;
        rows.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        rows.ColumnCount = 2;
        rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38F));
        rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62F));
        rows.Dock = DockStyle.Top;
        rows.Location = new Point(8, 8);
        rows.Name = "rows";
        rows.TabIndex = 0;
        rowsHost.Controls.Add(rows);
        //
        // tableButtons
        //
        tableButtons.ColumnCount = 2;
        tableButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        tableButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        tableButtons.Controls.Add(btnOk, 0, 0);
        tableButtons.Controls.Add(btnCancelForm, 1, 0);
        tableButtons.Dock = DockStyle.Bottom;
        tableButtons.Height = 44;
        tableButtons.Location = new Point(0, 625);
        tableButtons.Padding = new Padding(8, 6, 8, 8);
        tableButtons.Name = "tableButtons";
        tableButtons.RowCount = 1;
        tableButtons.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tableButtons.Size = new Size(560, 44);
        tableButtons.TabIndex = 1;
        //
        // btnOk / btnCancelForm
        //
        btnOk.Dock = DockStyle.Fill;
        btnOk.Location = new Point(11, 9);
        btnOk.Name = "btnOk";
        btnOk.Size = new Size(265, 29);
        btnOk.TabIndex = 0;
        btnOk.Text = "保存";
        btnOk.UseVisualStyleBackColor = true;
        btnCancelForm.Dock = DockStyle.Fill;
        btnCancelForm.Location = new Point(284, 9);
        btnCancelForm.Name = "btnCancelForm";
        btnCancelForm.Size = new Size(265, 29);
        btnCancelForm.TabIndex = 1;
        btnCancelForm.Text = "取消";
        btnCancelForm.UseVisualStyleBackColor = true;
        //
        // SettingsForm
        //
        AutoScaleDimensions = new SizeF(7F, 17F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(560, 669);
        Controls.Add(rowsHost);
        Controls.Add(tableButtons);
        Name = "SettingsForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "整理规则（整理规则.ini）";
        rowsHost.ResumeLayout(false);
        rows.ResumeLayout(false);
        rows.PerformLayout();
        tableButtons.ResumeLayout(false);
        ResumeLayout(false);
    }

    #endregion
}
