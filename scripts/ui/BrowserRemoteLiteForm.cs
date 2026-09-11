using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

internal sealed class BrowserRemoteLiteForm : Form
{
    private readonly Func<Dictionary<string, string>> getCurrentMappings;
    private readonly Func<BrowserRemotePlan, ActionResult> applyPlan;
    private readonly Func<ActionResult> undoPlan;
    private readonly Func<bool> canUndo;
    private readonly Func<string, string, string, Action<ActionResult>, ActionResult> beginActionTest;
    private readonly Action<ActionResult> publishResult;
    private readonly ComboBox rightChoice = new ComboBox();
    private readonly ComboBox functionLongChoice = new ComboBox();
    private readonly ComboBox browserChoice = new ComboBox();
    private readonly ListView differences = new ListView();
    private readonly Label stateLabel = new Label();
    private readonly Button applyButton = new Button();
    private readonly Button undoButton = new Button();
    private readonly Button testButton = new Button();
    private readonly Button closeButton = new Button();
    private readonly Panel scrollHost = new Panel();
    private readonly TableLayoutPanel root = new TableLayoutPanel();
    private BrowserRemotePlan currentPlan;
    private bool darkTheme;
    private bool busy;

    internal BrowserRemoteLiteForm(Func<Dictionary<string, string>> getCurrentMappings,
        Func<BrowserRemotePlan, ActionResult> applyPlan, Func<ActionResult> undoPlan,
        Func<bool> canUndo,
        Func<string, string, string, Action<ActionResult>, ActionResult> beginActionTest,
        Action<ActionResult> publishResult, bool useDarkTheme)
    {
        // Not wired to UiDisplayScale: like Capture & Ask, this form sets AutoScaleDimensions = (96,96) with
        // AutoScaleMode.Dpi, so Windows Forms scales it, and a second pass at load made it larger than the
        // working area — its own fit then clamped it, so it filled the screen (2536x1416) instead of taking its
        // design size. Measured without this call: 1654x1369, whose client area is 1628x1298, which is exactly
        // this form's design client size (814x649, from a design window of 840x720) times this display's
        // scaling. My first attempt at this was reverted because I had the design size wrong (1268x708, inferred
        // from the measured window), which made a correct result look unverifiable.
        this.getCurrentMappings = getCurrentMappings ?? delegate { return new Dictionary<string, string>(); };
        this.applyPlan = applyPlan ?? delegate
        {
            return ActionResult.Create("应用浏览器遥控", "浏览器 AI", ActionState.Error,
                "推荐配置未应用", "配置入口不可用", "关闭后重试", "BROWSER-APPLY-UNAVAILABLE");
        };
        this.undoPlan = undoPlan ?? delegate
        {
            return ActionResult.Create("撤销浏览器遥控", "浏览器 AI", ActionState.Warning,
                "没有可撤销配置", "撤销入口不可用", "关闭后重试", "BROWSER-UNDO-UNAVAILABLE");
        };
        this.canUndo = canUndo ?? delegate { return false; };
        this.beginActionTest = beginActionTest ?? delegate(string browser, string label,
            string action, Action<ActionResult> completion)
        {
            return ActionResult.Create("测试浏览器动作", label, ActionState.Error,
                "按键测试未派发", "测试入口不可用", "关闭后重试", "BROWSER-TEST-UNAVAILABLE");
        };
        this.publishResult = publishResult ?? delegate { };

        Text = "Browser Remote Lite";
        Name = "browserRemoteLiteForm";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(840, 720);
        MinimumSize = new Size(680, 560);
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Microsoft YaHei UI", 9.5f);
        KeyPreview = true;

        scrollHost.Name = "browserRemoteScrollHost";
        scrollHost.Dock = DockStyle.Fill;
        scrollHost.AutoScroll = true;

        root.Dock = DockStyle.Top;
        root.Height = 680;
        root.MinimumSize = new Size(0, 680);
        root.Padding = new Padding(24, 20, 24, 18);
        root.ColumnCount = 1;
        root.RowCount = 7;
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 104f));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56f));

        var heading = new Panel { Dock = DockStyle.Fill };
        var title = NewLabel("Browser Remote Lite", 18f, FontStyle.Bold);
        title.Name = "browserRemoteTitle";
        title.Location = new Point(0, 0);
        title.Size = new Size(500, 34);
        var subtitle = NewLabel("浏览与检查 · 显式应用，可一键恢复应用前配置", 9f, FontStyle.Regular);
        subtitle.Name = "browserRemoteSubtitle";
        subtitle.Location = new Point(1, 40);
        subtitle.Size = new Size(650, 24);
        heading.Controls.Add(title);
        heading.Controls.Add(subtitle);

        var options = new TableLayoutPanel();
        options.Dock = DockStyle.Fill;
        options.ColumnCount = 2;
        options.RowCount = 2;
        options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        options.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));
        options.RowStyles.Add(new RowStyle(SizeType.Absolute, 46f));
        options.Controls.Add(NewLabel("右键", 9f, FontStyle.Bold), 0, 0);
        options.Controls.Add(NewLabel("功能键长按", 9f, FontStyle.Bold), 1, 0);
        ConfigureChoice(rightChoice, "browserRemoteRightChoice");
        rightChoice.AccessibleName = "右键推荐动作";
        rightChoice.Items.Add(new BrowserOption("下一个可操作项（Tab）", "tab"));
        rightChoice.Items.Add(new BrowserOption("下一个标签页（Ctrl + Tab）", "next-tab"));
        rightChoice.Items.Add(new BrowserOption("浏览器前进", "forward"));
        rightChoice.SelectedIndex = 0;
        ConfigureChoice(functionLongChoice, "browserRemoteFunctionLongChoice");
        functionLongChoice.AccessibleName = "功能键长按推荐动作";
        functionLongChoice.Items.Add(new BrowserOption("聚焦地址栏（Ctrl + L）", "address-bar"));
        functionLongChoice.Items.Add(new BrowserOption("页面查找（Ctrl + F）", "find"));
        functionLongChoice.SelectedIndex = 0;
        options.Controls.Add(rightChoice, 0, 1);
        options.Controls.Add(functionLongChoice, 1, 1);

        differences.Name = "browserRemoteDiff";
        differences.Dock = DockStyle.Fill;
        differences.View = View.Details;
        differences.FullRowSelect = true;
        differences.GridLines = true;
        differences.HideSelection = false;
        differences.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        differences.Columns.Add("实体键", 150);
        differences.Columns.Add("现有配置", 245);
        differences.Columns.Add("推荐配置", 245);

        var testRow = new TableLayoutPanel();
        testRow.Name = "browserRemoteTestRow";
        testRow.Dock = DockStyle.Fill;
        testRow.ColumnCount = 4;
        testRow.RowCount = 1;
        testRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108f));
        testRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170f));
        testRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        testRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 128f));
        var browserLabel = NewLabel("逐项测试目标", 8.8f, FontStyle.Bold);
        browserLabel.Dock = DockStyle.Fill;
        browserLabel.TextAlign = ContentAlignment.MiddleLeft;
        ConfigureChoice(browserChoice, "browserRemoteBrowserChoice");
        browserChoice.AccessibleName = "逐项测试目标";
        browserChoice.Items.Add(new BrowserOption("Chrome", "chrome"));
        browserChoice.Items.Add(new BrowserOption("Edge", "edge"));
        browserChoice.SelectedIndex = 0;
        var testHint = NewLabel("先在上方选择一个动作；测试只派发按键，页面结果需目视确认。",
            8.2f, FontStyle.Regular);
        testHint.Name = "browserRemoteTestHint";
        testHint.Dock = DockStyle.Fill;
        testHint.TextAlign = ContentAlignment.MiddleLeft;
        ConfigureButton(testButton, "测试选中动作", "browserRemoteTestButton", false, 122);
        testButton.Dock = DockStyle.Fill;
        testButton.Margin = new Padding(6, 7, 0, 7);
        testButton.Click += delegate { TestSelectedAction(); };
        testRow.Controls.Add(browserLabel, 0, 0);
        testRow.Controls.Add(browserChoice, 1, 0);
        testRow.Controls.Add(testHint, 2, 0);
        testRow.Controls.Add(testButton, 3, 0);

        var privacy = NewLabel("只修改上、下、左、右、确认和功能键；录音键、Home、TV、语音参数与 Smart Profiles 不会改变。",
            8.6f, FontStyle.Regular);
        privacy.Name = "browserRemoteBoundary";
        privacy.Dock = DockStyle.Fill;
        privacy.TextAlign = ContentAlignment.MiddleLeft;

        stateLabel.Name = "browserRemoteState";
        stateLabel.Dock = DockStyle.Fill;
        stateLabel.Padding = new Padding(14, 8, 14, 8);
        stateLabel.TextAlign = ContentAlignment.MiddleLeft;
        stateLabel.AutoEllipsis = false;

        var footer = new FlowLayoutPanel();
        footer.Dock = DockStyle.Fill;
        footer.FlowDirection = FlowDirection.RightToLeft;
        footer.WrapContents = false;
        footer.Padding = new Padding(0, 8, 0, 0);
        ConfigureButton(applyButton, "应用推荐配置", "browserRemoteApplyButton", true, 148);
        ConfigureButton(undoButton, "一键撤销", "browserRemoteUndoButton", false, 112);
        ConfigureButton(closeButton, "关闭", "browserRemoteCloseButton", false, 88);
        applyButton.Click += delegate { ApplySelectedPlan(); };
        undoButton.Click += delegate { UndoAppliedPlan(); };
        closeButton.Click += delegate { Close(); };
        footer.Controls.Add(applyButton);
        footer.Controls.Add(undoButton);
        footer.Controls.Add(closeButton);

        root.Controls.Add(heading, 0, 0);
        root.Controls.Add(options, 0, 1);
        root.Controls.Add(differences, 0, 2);
        root.Controls.Add(testRow, 0, 3);
        root.Controls.Add(privacy, 0, 4);
        root.Controls.Add(stateLabel, 0, 5);
        root.Controls.Add(footer, 0, 6);
        scrollHost.Controls.Add(root);
        Controls.Add(scrollHost);

        rightChoice.SelectedIndexChanged += delegate { RefreshPlan(); };
        functionLongChoice.SelectedIndexChanged += delegate { RefreshPlan(); };
        differences.SelectedIndexChanged += delegate { UpdateTestButton(); };
        browserChoice.SelectedIndexChanged += delegate { UpdateTestButton(); };
        scrollHost.Resize += delegate { LayoutScrollableContent(); };
        ApplyTheme(useDarkTheme);
        RefreshPlan();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        ApplyWorkingAreaLayout(Screen.FromControl(this).WorkingArea);
    }

    private void ApplyWorkingAreaLayout(Rectangle workingArea)
    {
        Size scaledMinimum = MinimumSize;
        Size fitted = FitWindowToWorkingArea(Size, workingArea);
        MinimumSize = Size.Empty;
        Size = fitted;
        MinimumSize = new Size(Math.Min(scaledMinimum.Width, fitted.Width),
            Math.Min(scaledMinimum.Height, fitted.Height));
        Location = new Point(workingArea.Left + Math.Max(0, (workingArea.Width - Width) / 2),
            workingArea.Top + Math.Max(0, (workingArea.Height - Height) / 2));
        LayoutScrollableContent();
    }

    private void LayoutScrollableContent()
    {
        root.Width = Math.Max(1, scrollHost.ClientSize.Width);
        root.Height = Math.Max(scrollHost.ClientSize.Height, root.MinimumSize.Height);
        int available = Math.Max(300, differences.ClientSize.Width - 5);
        differences.Columns[0].Width = Math.Max(100, available * 22 / 100);
        differences.Columns[1].Width = Math.Max(160, available * 39 / 100);
        differences.Columns[2].Width = Math.Max(160, available - differences.Columns[0].Width -
            differences.Columns[1].Width);
    }

    private static Size FitWindowToWorkingArea(Size desired, Rectangle workingArea)
    {
        const int margin = 24;
        return new Size(Math.Min(Math.Max(1, desired.Width), Math.Max(1, workingArea.Width - margin)),
            Math.Min(Math.Max(1, desired.Height), Math.Max(1, workingArea.Height - margin)));
    }

    internal void ApplyTheme(bool useDarkTheme)
    {
        darkTheme = useDarkTheme;
        Color page = darkTheme ? Color.FromArgb(25, 26, 31) : Color.FromArgb(245, 247, 251);
        Color ink = darkTheme ? Color.FromArgb(229, 232, 239) : Color.FromArgb(18, 30, 54);
        Color muted = darkTheme ? Color.FromArgb(153, 161, 177) : Color.FromArgb(91, 104, 134);
        Color surface = darkTheme ? Color.FromArgb(35, 37, 44) : Color.White;
        Color border = darkTheme ? Color.FromArgb(55, 59, 69) : Color.FromArgb(210, 217, 231);
        BackColor = page;
        ForeColor = ink;
        scrollHost.BackColor = page;
        root.BackColor = page;
        ApplyControlPalette(root, page, surface, ink, muted, border);
        stateLabel.BackColor = darkTheme ? Color.FromArgb(38, 40, 48) : Color.FromArgb(238, 241, 248);
        differences.BackColor = surface;
        differences.ForeColor = ink;
        rightChoice.BackColor = darkTheme ? Color.FromArgb(31, 33, 39) : Color.White;
        rightChoice.ForeColor = ink;
        functionLongChoice.BackColor = rightChoice.BackColor;
        functionLongChoice.ForeColor = ink;
        browserChoice.BackColor = rightChoice.BackColor;
        browserChoice.ForeColor = ink;
        ApplyButtonPalette(applyButton, true, ink, surface, border);
        ApplyButtonPalette(undoButton, false, ink, surface, border);
        ApplyButtonPalette(testButton, false, ink, surface, border);
        ApplyButtonPalette(closeButton, false, ink, surface, border);
    }

    private void RefreshPlan()
    {
        BrowserOption right = rightChoice.SelectedItem as BrowserOption;
        BrowserOption functionLong = functionLongChoice.SelectedItem as BrowserOption;
        if (right == null || functionLong == null) return;
        currentPlan = BrowserProfileTemplate.CreatePlan(BrowserProfileTemplate.ProfileId,
            getCurrentMappings(), right.Value, functionLong.Value);
        differences.Items.Clear();
        Dictionary<string, string> current = currentPlan.CurrentMappings();
        Dictionary<string, string> recommended = currentPlan.RecommendedMappings();
        AddDifference("上键", "上键", current, recommended);
        AddDifference("下键", "下键", current, recommended);
        AddDifference("左键", "左键", current, recommended);
        AddDifference("右键", "右键", current, recommended);
        AddDifference("确认键", "确认键", current, recommended);
        AddDifference("功能键短按", "功能键:short", current, recommended);
        AddDifference("功能键长按", "功能键:long", current, recommended);
        if (differences.Items.Count > 0) differences.Items[0].Selected = true;
        bool hasChanges = currentPlan.IsValid && currentPlan.Changes.Count > 0;
        stateLabel.ForeColor = darkTheme ? Color.FromArgb(153, 161, 177) : Color.FromArgb(91, 104, 134);
        stateLabel.Text = !currentPlan.IsValid ? "无法读取浏览器 Profile，未修改任何设置。" :
            hasChanges ? "等待确认 · 将修改 " + VisibleChangeCount(currentPlan) + " 个实体键动作" :
            "当前已是所选推荐配置。";
        applyButton.Enabled = !busy && hasChanges;
        undoButton.Enabled = !busy && canUndo();
        UpdateTestButton();
    }

    private void AddDifference(string label, string key, Dictionary<string, string> current,
        Dictionary<string, string> recommended)
    {
        string before = current.ContainsKey(key) ? current[key] : "";
        string after = recommended.ContainsKey(key) ? recommended[key] : before;
        var item = new ListViewItem(label);
        item.Tag = new BrowserRemoteDifference(label, after);
        item.SubItems.Add(ActionText(before));
        item.SubItems.Add(ActionText(after));
        if (!string.Equals(before, after, StringComparison.Ordinal))
            item.ForeColor = darkTheme ? Color.FromArgb(205, 157, 81) : Color.FromArgb(181, 112, 17);
        differences.Items.Add(item);
    }

    private void TestSelectedAction()
    {
        BrowserRemoteDifference selected = differences.SelectedItems.Count == 1
            ? differences.SelectedItems[0].Tag as BrowserRemoteDifference : null;
        BrowserOption browser = browserChoice.SelectedItem as BrowserOption;
        if (busy || selected == null || browser == null) return;
        SetBusy(true);
        SetState(ActionState.Checking, "正在验证 " + browser.Label + " 前台窗口...");
        ActionResult started = beginActionTest(browser.Value, selected.Label, selected.Action,
            CompleteActionTestThreadSafe);
        if (started == null || started.State != ActionState.Running)
        {
            CompleteActionTest(started ?? ActionResult.Create("测试浏览器动作", selected.Label,
                ActionState.Error, "按键测试未派发", "测试入口没有返回结果",
                "关闭后重试", "BROWSER-TEST-NO-RESULT"));
            return;
        }
        publishResult(started);
        SetState(started.State, started.OverlayDetailText());
    }

    private void CompleteActionTestThreadSafe(ActionResult result)
    {
        if (IsDisposed || Disposing) return;
        if (InvokeRequired)
        {
            try { BeginInvoke(new Action<ActionResult>(CompleteActionTestThreadSafe), result); }
            catch { }
            return;
        }
        CompleteActionTest(result);
    }

    private void CompleteActionTest(ActionResult result)
    {
        if (IsDisposed || Disposing) return;
        SetBusy(false);
        ActionResult actual = result ?? ActionResult.Create("测试浏览器动作", "浏览器",
            ActionState.Error, "按键测试没有返回结果", "按键服务回执不可用",
            "确认按键服务正在运行后重试", "BROWSER-TEST-NO-RESULT");
        publishResult(actual);
        SetState(actual.State, actual.OverlayDetailText());
    }

    private void UpdateTestButton()
    {
        testButton.Enabled = !busy && differences.SelectedItems.Count == 1 &&
            browserChoice.SelectedItem is BrowserOption;
    }

    private static int VisibleChangeCount(BrowserRemotePlan plan)
    {
        var physical = new HashSet<string>();
        foreach (BrowserRemoteMappingChange change in plan.Changes)
            physical.Add(change.Key == "功能键" ? "功能键:short" : change.Key);
        return physical.Count;
    }

    private void ApplySelectedPlan()
    {
        if (busy || currentPlan == null || !currentPlan.IsValid || currentPlan.Changes.Count == 0) return;
        SetBusy(true);
        SetState(ActionState.Running, "正在保存推荐配置并等待按键服务确认...");
        ActionResult result = applyPlan(currentPlan);
        publishResult(result);
        SetBusy(false);
        RefreshPlan();
        SetState(result == null ? ActionState.Error : result.State,
            result == null ? "推荐配置未应用。" : result.OverlayDetailText());
    }

    private void UndoAppliedPlan()
    {
        if (busy || !canUndo()) return;
        SetBusy(true);
        SetState(ActionState.Running, "正在恢复应用前配置并等待按键服务确认...");
        ActionResult result = undoPlan();
        publishResult(result);
        SetBusy(false);
        RefreshPlan();
        SetState(result == null ? ActionState.Error : result.State,
            result == null ? "未能恢复应用前配置。" : result.OverlayDetailText());
    }

    private void SetBusy(bool value)
    {
        busy = value;
        rightChoice.Enabled = !value;
        functionLongChoice.Enabled = !value;
        browserChoice.Enabled = !value;
        differences.Enabled = !value;
        closeButton.Enabled = true;
        applyButton.Enabled = !value && currentPlan != null && currentPlan.Changes.Count > 0;
        undoButton.Enabled = !value && canUndo();
        UpdateTestButton();
    }

    private void SetState(ActionState state, string text)
    {
        stateLabel.Text = text ?? "";
        stateLabel.ForeColor = state == ActionState.Success
            ? (darkTheme ? Color.FromArgb(76, 174, 127) : Color.FromArgb(10, 150, 95))
            : state == ActionState.Error
                ? (darkTheme ? Color.FromArgb(205, 101, 110) : Color.FromArgb(204, 70, 82))
                : state == ActionState.Warning || state == ActionState.Canceled
                    ? (darkTheme ? Color.FromArgb(205, 157, 81) : Color.FromArgb(201, 128, 24))
                    : (darkTheme ? Color.FromArgb(126, 118, 213) : Color.FromArgb(104, 82, 244));
    }

    private static string ActionText(string action)
    {
        string value = (action ?? "").Trim();
        if (value == "pageup") return "向上翻页";
        if (value == "pagedown") return "向下翻页";
        if (value == "browserback") return "浏览器后退";
        if (value == "tab") return "下一个可操作项";
        if (value == "enter") return "确认";
        if (value == "shortcut:ctrl+r") return "刷新页面";
        if (value == "shortcut:ctrl+l") return "聚焦地址栏";
        if (value == "shortcut:ctrl+f") return "页面查找";
        if (value == "shortcut:ctrl+tab") return "下一个标签页";
        if (value == "shortcut:browserforward") return "浏览器前进";
        return value.Length == 0 ? "未设置" : value;
    }

    private static Label NewLabel(string text, float size, FontStyle style)
    {
        return new Label { Text = text, Font = new Font("Microsoft YaHei UI", size, style),
            AutoEllipsis = true };
    }

    private static void ConfigureChoice(ComboBox combo, string name)
    {
        combo.Name = name;
        combo.Dock = DockStyle.Fill;
        combo.DropDownStyle = ComboBoxStyle.DropDownList;
        combo.FlatStyle = FlatStyle.Flat;
        combo.Margin = new Padding(0, 4, 14, 4);
    }

    private static void ConfigureButton(Button button, string text, string name, bool primary, int width)
    {
        button.Text = text;
        button.Name = name;
        button.Size = new Size(width, 40);
        button.Margin = new Padding(10, 0, 0, 0);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = primary ? 0 : 1;
        button.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
    }

    private void ApplyButtonPalette(Button button, bool primary, Color ink,
        Color surface, Color border)
    {
        if (button == null) return;
        button.BackColor = primary
            ? (darkTheme ? Color.FromArgb(126, 118, 213) : Color.FromArgb(104, 82, 244))
            : surface;
        button.ForeColor = primary ? Color.White : ink;
        button.FlatAppearance.BorderColor = border;
        button.FlatAppearance.MouseOverBackColor = primary
            ? (darkTheme ? Color.FromArgb(142, 135, 226) : Color.FromArgb(88, 66, 238))
            : (darkTheme ? Color.FromArgb(47, 49, 57) : Color.FromArgb(232, 236, 255));
        button.FlatAppearance.MouseDownBackColor = primary
            ? (darkTheme ? Color.FromArgb(158, 152, 233) : Color.FromArgb(72, 52, 220))
            : (darkTheme ? Color.FromArgb(55, 58, 68) : Color.FromArgb(219, 225, 252));
        if (button.Tag == null)
        {
            button.Tag = "rounded-feedback";
            Action updateRegion = delegate
            {
                if (button.Width <= 0 || button.Height <= 0) return;
                Region previous = button.Region;
                using (var path = RoundedButtonPath(new Rectangle(0, 0, button.Width, button.Height), 6))
                    button.Region = new Region(path);
                if (previous != null) previous.Dispose();
            };
            button.Resize += delegate { updateRegion(); };
            updateRegion();
        }
    }

    private static GraphicsPath RoundedButtonPath(Rectangle rectangle, int radius)
    {
        int diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static void ApplyControlPalette(Control rootControl, Color page, Color surface,
        Color ink, Color muted, Color border)
    {
        foreach (Control control in rootControl.Controls)
        {
            if (control is Panel || control is TableLayoutPanel || control is FlowLayoutPanel)
                control.BackColor = page;
            if (control is Label) control.ForeColor = muted;
            ApplyControlPalette(control, page, surface, ink, muted, border);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape) { e.Handled = true; Close(); return; }
        base.OnKeyDown(e);
    }
}

internal sealed class BrowserRemoteDifference
{
    internal string Label { get; private set; }
    internal string Action { get; private set; }
    internal BrowserRemoteDifference(string label, string action)
    {
        Label = label ?? "浏览器动作";
        Action = action ?? "";
    }
}

internal sealed class BrowserOption
{
    internal string Label { get; private set; }
    internal string Value { get; private set; }
    internal BrowserOption(string label, string value) { Label = label; Value = value; }
    public override string ToString() { return Label; }
}
