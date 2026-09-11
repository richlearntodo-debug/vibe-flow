using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal sealed class ContextDeckForm : Form
{
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int SW_SHOWNOACTIVATE = 4;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private readonly Font titleFont = new Font("Microsoft YaHei UI", 19f, FontStyle.Bold);
    private readonly Font sectionFont = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold);
    private readonly Font bodyFont = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular);
    private readonly Label applicationValue = new Label();
    private readonly Label profileValue = new Label();
    private readonly Label projectValue = new Label();
    private readonly Label targetValue = new Label();
    private readonly Label deviceValue = new Label();
    private readonly Label voiceValue = new Label();
    private readonly Label actionValue = new Label();
    private readonly Panel scrollHost;
    private readonly TableLayoutPanel root;
    private readonly Panel heading;
    private readonly Label eyebrow;
    private readonly Label title;
    private readonly Panel remotePanel;
    private readonly TableLayoutPanel right;
    private readonly TableLayoutPanel context;
    private readonly Label contextTitle;
    private readonly Panel mappingSurface;
    private readonly Label mappingTitle;
    private readonly Panel actionSurface;
    private readonly Label actionTitle;
    private readonly List<Label> contextNameLabels = new List<Label>();
    private readonly TableLayoutPanel mappingTable = new TableLayoutPanel();
    private readonly RemoteVisual remote = new RemoteVisual();
    private bool darkTheme;

    internal ContextDeckForm()
    {
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "遥控器状态 · 当前遥控器";
        AccessibleName = "遥控器状态 · 当前遥控器";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(820, 696);
        MinimumSize = new Size(720, 696);
        BackColor = Color.FromArgb(245, 247, 251);
        ShowInTaskbar = false;

        scrollHost = new Panel();
        scrollHost.Name = "contextDeckScrollHost";
        scrollHost.Dock = DockStyle.Fill;
        scrollHost.AutoScroll = true;
        scrollHost.BackColor = BackColor;
        root = new TableLayoutPanel();
        root.Dock = DockStyle.Top;
        root.Height = 648;
        root.Padding = new Padding(24);
        root.ColumnCount = 2;
        root.RowCount = 2;
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250f));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72f));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        heading = new Panel { Dock = DockStyle.Fill };
        heading.Paint += delegate(object sender, PaintEventArgs e)
        {
            if (heading.Width <= 0 || heading.Height <= 2) return;
            using (var brush = new LinearGradientBrush(
                new Rectangle(0, heading.Height - 2, heading.Width, 2),
                darkTheme ? Color.FromArgb(140, 126, 118, 213) : Color.FromArgb(140, 124, 100, 255),
                darkTheme ? Color.FromArgb(140, 79, 163, 181) : Color.FromArgb(140, 0, 168, 222),
                LinearGradientMode.Horizontal))
                e.Graphics.FillRectangle(brush, 0, heading.Height - 2, heading.Width, 2);
        };
        eyebrow = CreateLabel("REMOTE STATUS", sectionFont, Color.FromArgb(104, 82, 244));
        eyebrow.Location = new Point(0, 0);
        eyebrow.Size = new Size(240, 22);
        title = CreateLabel("当前遥控器", titleFont, Color.FromArgb(18, 30, 54));
        title.Location = new Point(0, 23);
        title.Size = new Size(280, 42);
        heading.Controls.Add(eyebrow);
        heading.Controls.Add(title);
        root.Controls.Add(heading, 0, 0);
        root.SetColumnSpan(heading, 2);

        remotePanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(18) };
        remote.Dock = DockStyle.Fill;
        remote.ShowCallouts = false;
        remote.IsActive = true;
        remotePanel.Controls.Add(remote);
        root.Controls.Add(remotePanel, 0, 1);

        right = new TableLayoutPanel();
        right.Dock = DockStyle.Fill;
        right.Padding = new Padding(18, 0, 0, 0);
        right.ColumnCount = 1;
        right.RowCount = 3;
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 180f));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 132f));

        context = new TableLayoutPanel();
        context.Dock = DockStyle.Fill;
        context.BackColor = Color.White;
        context.Padding = new Padding(16, 12, 16, 10);
        context.ColumnCount = 2;
        context.RowCount = 7;
        context.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112f));
        context.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        context.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
        for (int i = 1; i < 7; i++) context.RowStyles.Add(new RowStyle(SizeType.Percent, 16.66f));
        contextTitle = CreateLabel("当前上下文", sectionFont, Color.FromArgb(18, 30, 54));
        context.Controls.Add(contextTitle, 0, 0);
        context.SetColumnSpan(contextTitle, 2);
        AddContextRow(context, 1, "当前应用", applicationValue);
        AddContextRow(context, 2, "Profile", profileValue);
        AddContextRow(context, 3, "项目现场", projectValue);
        AddContextRow(context, 4, "语音目标", targetValue);
        AddContextRow(context, 5, "设备", deviceValue);
        AddContextRow(context, 6, "语音", voiceValue);

        mappingSurface = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(16, 12, 16, 12) };
        mappingTitle = CreateLabel("实体键当前动作", sectionFont, Color.FromArgb(18, 30, 54));
        mappingTitle.Dock = DockStyle.Top;
        mappingTitle.Height = 30;
        mappingTable.Dock = DockStyle.Fill;
        mappingTable.ColumnCount = 4;
        mappingTable.RowCount = 1;
        mappingTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96f));
        mappingTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        mappingTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104f));
        mappingTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        mappingTable.Name = "contextDeckMappingTable";
        mappingTable.AutoScroll = false;
        mappingTable.Padding = Padding.Empty;
        mappingSurface.Controls.Add(mappingTable);
        mappingSurface.Controls.Add(mappingTitle);

        actionSurface = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(16, 10, 16, 10) };
        actionTitle = CreateLabel("最近一次动作", sectionFont, Color.FromArgb(18, 30, 54));
        actionTitle.Dock = DockStyle.Top;
        actionTitle.Height = 28;
        actionValue.Dock = DockStyle.Fill;
        actionValue.Name = "contextDeckActionValue";
        actionValue.Font = bodyFont;
        actionValue.ForeColor = Color.FromArgb(52, 65, 91);
        actionValue.TextAlign = ContentAlignment.TopLeft;
        actionValue.AutoEllipsis = true;
        actionValue.TabStop = false;
        actionSurface.Controls.Add(actionValue);
        actionSurface.Controls.Add(actionTitle);

        right.Controls.Add(context, 0, 0);
        right.Controls.Add(mappingSurface, 0, 1);
        right.Controls.Add(actionSurface, 0, 2);
        root.Controls.Add(right, 1, 1);
        scrollHost.Controls.Add(root);
        Controls.Add(scrollHost);
        Load += delegate
        {
            VibeWindowLayout.FitToWorkingArea(this, Screen.FromControl(this).WorkingArea);
        };
    }

    protected override bool ShowWithoutActivation { get { return true; } }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams parameters = base.CreateParams;
            parameters.ExStyle |= WS_EX_NOACTIVATE;
            return parameters;
        }
    }

    internal void ShowInactive(Form owner)
    {
        if (IsDisposed) return;
        if (owner != null && Owner != owner) Owner = owner;
        if (!IsHandleCreated) CreateControl();
        Screen screen = owner == null ? Screen.FromPoint(Cursor.Position) : Screen.FromControl(owner);
        VibeWindowLayout.FitToWorkingArea(this, screen.WorkingArea);
        IntPtr window = Handle;
        ShowWindow(window, SW_SHOWNOACTIVATE);
        SetWindowPos(window, IntPtr.Zero, Left, Top, Width, Height,
            SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }

    internal void HideInactive()
    {
        if (IsHandleCreated) ShowWindow(Handle, 0);
    }

    internal void ApplyTheme(bool useDarkTheme)
    {
        if (IsDisposed) return;
        darkTheme = useDarkTheme;
        Color background = useDarkTheme ? Color.FromArgb(25, 26, 31) : Color.FromArgb(245, 247, 251);
        Color surface = useDarkTheme ? Color.FromArgb(34, 36, 43) : Color.White;
        Color ink = useDarkTheme ? Color.FromArgb(229, 232, 239) : Color.FromArgb(18, 30, 54);
        Color muted = useDarkTheme ? Color.FromArgb(153, 161, 177) : Color.FromArgb(91, 104, 134);

        BackColor = background;
        scrollHost.BackColor = background;
        root.BackColor = background;
        heading.BackColor = background;
        right.BackColor = background;
        remotePanel.BackColor = surface;
        remote.BackColor = surface;
        context.BackColor = surface;
        mappingSurface.BackColor = surface;
        mappingTable.BackColor = surface;
        actionSurface.BackColor = surface;
        title.ForeColor = ink;
        contextTitle.ForeColor = ink;
        mappingTitle.ForeColor = ink;
        actionTitle.ForeColor = ink;
        foreach (Label label in contextNameLabels) label.ForeColor = muted;
        applicationValue.ForeColor = ink;
        profileValue.ForeColor = ink;
        projectValue.ForeColor = ink;
        targetValue.ForeColor = ink;
        deviceValue.ForeColor = ink;
        voiceValue.ForeColor = ink;
        actionValue.ForeColor = useDarkTheme ? Color.FromArgb(190, 196, 208) : Color.FromArgb(52, 65, 91);
        ApplyMappingPalette();
        Invalidate(true);
    }

    internal void ApplySnapshot(VibeUiStatusSnapshot snapshot)
    {
        if (snapshot == null || IsDisposed) return;
        applicationValue.Text = snapshot.CurrentApplication;
        profileValue.Text = snapshot.ProfileName;
        projectValue.Text = snapshot.ProjectSpaceName;
        targetValue.Text = snapshot.FocusTargetName;
        deviceValue.Text = snapshot.DeviceStatus;
        voiceValue.Text = snapshot.VoiceStatus;
        actionValue.Text = snapshot.LatestAction.Stage + " · " + snapshot.LatestAction.OverlayDetailText();
        remote.HighlightedControl = snapshot.HighlightedControl;
        remote.IsRecording = snapshot.RealAudioActive;
        remote.AccentColor = snapshot.RealAudioActive ? Color.FromArgb(104, 82, 244) : Color.FromArgb(0, 153, 190);
        remote.Invalidate();

        mappingTable.SuspendLayout();
        while (mappingTable.Controls.Count > 0)
        {
            Control control = mappingTable.Controls[0];
            mappingTable.Controls.RemoveAt(0);
            control.Dispose();
        }
        var entries = new List<KeyValuePair<string, string>>(snapshot.Mappings);
        int leftCount = (entries.Count + 1) / 2;
        mappingTable.RowCount = Math.Max(1, leftCount);
        mappingTable.RowStyles.Clear();
        for (int row = 0; row < leftCount; row++)
        {
            mappingTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / leftCount));
            KeyValuePair<string, string> pair = entries[row];
            Label key = CreateLabel(pair.Key, sectionFont, MutedColor());
            Label action = CreateLabel(pair.Value, bodyFont, InkColor());
            key.Dock = DockStyle.Fill;
            action.Dock = DockStyle.Fill;
            action.AutoEllipsis = true;
            mappingTable.Controls.Add(key, 0, row);
            mappingTable.Controls.Add(action, 1, row);
            int rightIndex = row + leftCount;
            if (rightIndex < entries.Count)
            {
                KeyValuePair<string, string> rightPair = entries[rightIndex];
                Label rightKey = CreateLabel(rightPair.Key, sectionFont, MutedColor());
                Label rightAction = CreateLabel(rightPair.Value, bodyFont, InkColor());
                rightKey.Dock = DockStyle.Fill;
                rightAction.Dock = DockStyle.Fill;
                rightAction.AutoEllipsis = true;
                mappingTable.Controls.Add(rightKey, 2, row);
                mappingTable.Controls.Add(rightAction, 3, row);
            }
        }
        mappingTable.ResumeLayout(true);
    }

    private void ApplyMappingPalette()
    {
        foreach (Control control in mappingTable.Controls)
        {
            Label label = control as Label;
            if (label == null) continue;
            int column = mappingTable.GetColumn(label);
            label.ForeColor = column == 0 || column == 2 ? MutedColor() : InkColor();
        }
    }

    private Color InkColor()
    {
        return darkTheme ? Color.FromArgb(229, 232, 239) : Color.FromArgb(18, 30, 54);
    }

    private Color MutedColor()
    {
        return darkTheme ? Color.FromArgb(153, 161, 177) : Color.FromArgb(91, 104, 134);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            titleFont.Dispose();
            sectionFont.Dispose();
            bodyFont.Dispose();
        }
        base.Dispose(disposing);
    }

    private void AddContextRow(TableLayoutPanel table, int row, string name, Label value)
    {
        Label label = CreateLabel(name, bodyFont, Color.FromArgb(91, 104, 134));
        contextNameLabels.Add(label);
        label.Dock = DockStyle.Fill;
        value.Dock = DockStyle.Fill;
        value.Font = bodyFont;
        value.ForeColor = Color.FromArgb(18, 30, 54);
        value.TextAlign = ContentAlignment.MiddleLeft;
        value.AutoEllipsis = true;
        value.TabStop = false;
        table.Controls.Add(label, 0, row);
        table.Controls.Add(value, 1, row);
    }

    private static Label CreateLabel(string text, Font font, Color color)
    {
        return new Label
        {
            Text = text,
            Font = font,
            ForeColor = color,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            TabStop = false
        };
    }

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int command);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y,
        int cx, int cy, uint flags);
}
