using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

internal sealed class VibeMicForm : Form
{
    private readonly string root = AppDomain.CurrentDomain.BaseDirectory;
    private readonly string sessionDir;
    private readonly string configPath;
    private readonly string eventsPath;
    private readonly string brandLogoPath;
    private readonly Color ink = Color.FromArgb(20, 31, 55);
    private readonly Color muted = Color.FromArgb(96, 109, 139);
    private readonly Color violet = Color.FromArgb(101, 92, 255);
    private readonly Color green = Color.FromArgb(25, 167, 106);
    private readonly Color amber = Color.FromArgb(226, 151, 35);
    private readonly Color cyan = Color.FromArgb(24, 148, 194);
    private readonly Color line = Color.FromArgb(220, 226, 239);
    private readonly Panel content = new Panel();
    private readonly List<Button> navButtons = new List<Button>();
    private readonly Label connectionBadge = new Label();
    private readonly NotifyIcon tray = new NotifyIcon();
    private readonly bool backgroundLaunch;
    private Label heroTitle;
    private Label heroSubtitle;
    private Label heroStateLabel;
    private Label activityLabel;
    private Button bridgeButton;
    private RoundPanel heroPanel;
    private RemoteVisual remoteVisual;
    private Label voiceBridgeStateLabel;
    private TextBox logBox;
    private Process captureProcess;
    private Process keyboardBridgeProcess;
    private EventWaitHandle showWindowEvent;
    private VibeMicConfig config;
    private System.Windows.Forms.Timer activityTimer;
    private System.Windows.Forms.Timer reconnectTimer;
    private System.Windows.Forms.Timer visualTimer;
    private long lastEventLength;
    private int reconnectAttempt;
    private bool captureStopping;
    private bool applicationExiting;
    private bool setupWizardOpen;
    private bool bridgeReady;
    private DateTime activeStreamStarted = DateTime.MinValue;

    [STAThread]
    private static void Main(string[] args)
    {
        bool background = Array.Exists(args, delegate(string arg) { return arg.Equals("--background", StringComparison.OrdinalIgnoreCase); });
        bool createdNew;
        using (var instance = new Mutex(true, "Local\\VibeMic", out createdNew))
        {
            if (!createdNew)
            {
                if (!background) SignalEvent("Local\\VibeMicShowWindow");
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new VibeMicForm(background));
        }
    }

    private VibeMicForm(bool launchInBackground)
    {
        backgroundLaunch = launchInBackground;
        sessionDir = Path.Combine(root, "remote-voice-session");
        configPath = Path.Combine(root, "vibe-mic-config.json");
        eventsPath = Path.Combine(sessionDir, "remote-voice-events.jsonl");
        brandLogoPath = Path.Combine(root, "vibe-flow-logo.png");
        Directory.CreateDirectory(sessionDir);
        config = LoadConfig();
        if (config.launchAtStartup) SetLaunchAtStartup(true);
        ReleaseVoiceHotkey();

        Text = "言灵 · Vibe Flow";
        Width = 1280;
        Height = 840;
        MinimumSize = new Size(1080, 720);
        StartPosition = FormStartPosition.CenterScreen;
        if (backgroundLaunch)
        {
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-32000, -32000);
            ShowInTaskbar = false;
        }
        BackColor = Color.FromArgb(244, 247, 252);
        Font = new Font("Microsoft YaHei UI", 10f);
        Icon = CreateAppIcon();

        BuildShell();
        ShowPage(0);
        SetupTray();
        showWindowEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\VibeMicShowWindow");
        ThreadPool.QueueUserWorkItem(delegate
        {
            try
            {
                while (true)
                {
                    showWindowEvent.WaitOne();
                    if (IsDisposed || applicationExiting) return;
                    BeginInvoke(new Action(ShowMainWindow));
                }
            }
            catch { }
        });

        activityTimer = new System.Windows.Forms.Timer();
        activityTimer.Interval = 500;
        activityTimer.Tick += delegate { PollActivity(); };
        activityTimer.Start();

        visualTimer = new System.Windows.Forms.Timer();
        visualTimer.Interval = 50;
        visualTimer.Tick += delegate
        {
            if (remoteVisual != null && !remoteVisual.IsDisposed)
            {
                remoteVisual.AnimationPhase += 0.11f;
                remoteVisual.Invalidate();
            }
        };
        visualTimer.Start();
    }

    private void BuildShell()
    {
        var sidebar = new Panel();
        sidebar.Dock = DockStyle.Left;
        sidebar.Width = 220;
        sidebar.BackColor = Color.FromArgb(249, 251, 255);
        sidebar.Paint += delegate(object sender, PaintEventArgs e)
        {
            using (var pen = new Pen(line)) e.Graphics.DrawLine(pen, sidebar.Width - 1, 0, sidebar.Width - 1, sidebar.Height);
        };

        var logo = new PictureBox();
        logo.Image = LoadBrandLogo();
        logo.SizeMode = PictureBoxSizeMode.Zoom;
        logo.BackColor = Color.Transparent;
        logo.Location = new Point(24, 24);
        logo.Size = new Size(48, 48);

        var brand = NewLabel("言灵", 19f, FontStyle.Bold, ink);
        brand.Location = new Point(82, 23);
        brand.AutoSize = true;
        var sub = NewLabel("VIBE FLOW", 8f, FontStyle.Bold, violet);
        sub.Location = new Point(84, 58);
        sub.AutoSize = true;

        string[] navText = { "\uE80F   总览", "\uE720   语音听写", "\uE765   按键快捷方式", "\uE702   连接与诊断", "\uE713   偏好设置" };
        for (int i = 0; i < navText.Length; i++)
        {
            int page = i;
            var button = new Button();
            button.Text = navText[i];
            button.Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Regular);
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.Padding = new Padding(20, 0, 0, 0);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = Color.Transparent;
            button.ForeColor = ink;
            button.Location = new Point(18, 120 + i * 58);
            button.Size = new Size(184, 46);
            button.Cursor = Cursors.Hand;
            button.Click += delegate { ShowPage(page); };
            navButtons.Add(button);
            sidebar.Controls.Add(button);
        }

        connectionBadge.Text = "●  正在检查连接";
        connectionBadge.ForeColor = amber;
        connectionBadge.BackColor = Color.White;
        connectionBadge.TextAlign = ContentAlignment.MiddleCenter;
        connectionBadge.Location = new Point(22, 12);
        connectionBadge.Size = new Size(176, 46);
        connectionBadge.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);

        var sidebarFooter = new Panel();
        sidebarFooter.Dock = DockStyle.Bottom;
        sidebarFooter.Height = 76;
        sidebarFooter.BackColor = sidebar.BackColor;
        sidebarFooter.Controls.Add(connectionBadge);

        sidebar.Controls.Add(logo);
        sidebar.Controls.Add(brand);
        sidebar.Controls.Add(sub);
        sidebar.Controls.Add(sidebarFooter);

        content.Dock = DockStyle.Fill;
        content.Padding = new Padding(34, 26, 34, 26);
        content.BackColor = Color.FromArgb(244, 247, 252);
        Controls.Add(content);
        Controls.Add(sidebar);
    }

    private void ShowPage(int index)
    {
        for (int i = 0; i < navButtons.Count; i++)
        {
            navButtons[i].BackColor = i == index ? Color.FromArgb(237, 239, 255) : Color.Transparent;
            navButtons[i].ForeColor = i == index ? violet : ink;
            navButtons[i].Font = new Font("Microsoft YaHei UI", 10f, i == index ? FontStyle.Bold : FontStyle.Regular);
        }
        content.SuspendLayout();
        content.Controls.Clear();
        if (index == 0) BuildOverview();
        else if (index == 1) BuildVoicePage();
        else if (index == 2) BuildMappingsPage();
        else if (index == 3) BuildDevicePage();
        else BuildSettingsPage();
        content.ResumeLayout();
    }

    private void BuildOverview()
    {
        AddPageTitle("总览", "遥控器状态与常用操作");

        var hero = NewCard(new Point(34, 92), new Size(960, 270));
        heroPanel = hero;
        hero.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        heroStateLabel = NewLabel(IsCapturing ? "VOICE LINK" : "VOICE LINK OFF", 8.5f, FontStyle.Bold, violet);
        heroStateLabel.Location = new Point(52, 38);
        heroStateLabel.AutoSize = true;
        heroTitle = NewLabel(IsCapturing ? "正在连接" : "语音桥接已暂停", 27f, FontStyle.Bold, ink);
        heroTitle.Location = new Point(50, 68);
        heroTitle.AutoSize = true;
        heroSubtitle = NewLabel(IsCapturing ? "正在建立遥控器语音通道，请稍候" : "启动后，按住遥控器录音键即可在当前输入框听写", 10.5f, FontStyle.Regular, muted);
        heroSubtitle.Location = new Point(52, 117);
        heroSubtitle.Size = new Size(560, 30);

        bridgeButton = PrimaryButton(IsCapturing ? "暂停语音桥接" : "启动语音桥接", new Point(52, 174), new Size(152, 44));
        bridgeButton.Click += delegate { ToggleCapture(); };
        var scan = SecondaryButton("检查连接", new Point(216, 174), new Size(124, 44));
        scan.Click += delegate { ScanDevice(); };

        remoteVisual = new RemoteVisual();
        remoteVisual.Location = new Point(716, 8);
        remoteVisual.Size = new Size(210, 252);
        remoteVisual.Anchor = AnchorStyles.Top | AnchorStyles.Right;

        hero.Controls.Add(heroStateLabel);
        hero.Controls.Add(heroTitle);
        hero.Controls.Add(heroSubtitle);
        hero.Controls.Add(bridgeButton);
        hero.Controls.Add(scan);
        hero.Controls.Add(remoteVisual);

        var flow = NewCard(new Point(34, 378), new Size(470, 220));
        flow.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        flow.Controls.Add(SectionTitle("开始一次听写", "\uE720", new Point(24, 18)));
        string[] steps = { "按住录音键", "说出内容", "自动回填文字" };
        string[] icons = { "\uE720", "\uE9D2", "\uE724" };
        for (int i = 0; i < 3; i++)
        {
            int x = 34 + i * 144;
            var circle = new RoundPanel();
            circle.Location = new Point(x, 60);
            circle.Size = new Size(62, 62);
            circle.Radius = 31;
            circle.BackColor = i == 0 ? Color.FromArgb(237, 235, 255) : Color.FromArgb(246, 249, 253);
            circle.BorderColor = i == 0 ? Color.FromArgb(209, 204, 255) : line;
            var glyph = NewLabel(icons[i], 20f, FontStyle.Regular, i == 1 ? cyan : violet);
            glyph.Font = new Font("Segoe MDL2 Assets", 20f, FontStyle.Regular);
            glyph.Dock = DockStyle.Fill;
            glyph.TextAlign = ContentAlignment.MiddleCenter;
            circle.Controls.Add(glyph);
            flow.Controls.Add(circle);
            var label = NewLabel(steps[i], 9f, FontStyle.Regular, muted);
            label.Location = new Point(x - 18, 130);
            label.Size = new Size(98, 24);
            label.TextAlign = ContentAlignment.MiddleCenter;
            flow.Controls.Add(label);
            if (i < 2)
            {
                var connector = NewLabel("····", 11f, FontStyle.Regular, Color.FromArgb(180, 190, 212));
                connector.Location = new Point(x + 72, 79);
                connector.AutoSize = true;
                flow.Controls.Add(connector);
            }
        }
        activityLabel = NewLabel("已就绪，等待按下录音键", 9.5f, FontStyle.Bold, muted);
        activityLabel.Location = new Point(24, 174);
        activityLabel.Size = new Size(420, 24);
        activityLabel.TextAlign = ContentAlignment.MiddleCenter;
        flow.Controls.Add(activityLabel);

        var shortcuts = NewCard(new Point(520, 378), new Size(474, 220));
        shortcuts.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        shortcuts.Controls.Add(SectionTitle("常用按键", "\uE765", new Point(24, 18)));
        string[,] quick = { { "录音", "按住听写" }, { "确认", "确认或发送" }, { "TV", "打开任务切换" }, { "方向键", "短按移动，长按调音量" } };
        for (int i = 0; i < 4; i++)
        {
            var chip = NewLabel(i == 0 ? "●" : "·", 11f, FontStyle.Bold, i == 0 ? violet : cyan);
            chip.Location = new Point(24, 56 + i * 36);
            chip.Size = new Size(34, 28);
            chip.TextAlign = ContentAlignment.MiddleCenter;
            chip.BackColor = Color.FromArgb(246, 248, 253);
            var key = NewLabel(quick[i, 0], 9.5f, FontStyle.Bold, ink);
            key.Location = new Point(70, 59 + i * 36);
            key.Size = new Size(92, 25);
            var value = NewLabel(quick[i, 1], 9f, FontStyle.Regular, muted);
            value.Location = new Point(176, 59 + i * 36);
            value.Size = new Size(240, 25);
            shortcuts.Controls.Add(chip);
            shortcuts.Controls.Add(key);
            shortcuts.Controls.Add(value);
        }

        var status = NewCard(new Point(34, 614), new Size(960, 86));
        status.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        string[] statusNames = { "蓝牙", "遥控器麦克风", "语音数据", "微信音频通道", "隐私保护" };
        string[] statusValues = { "已配对", "已接入", "正常", "已就绪", "本地运行" };
        for (int i = 0; i < statusNames.Length; i++)
        {
            int x = 18 + i * 188;
            var glyph = NewLabel(i == 0 ? "\uE702" : i == 4 ? "\uEA18" : "●", 12f, FontStyle.Regular, i == 4 ? green : i < 2 ? violet : cyan);
            glyph.Font = i == 0 || i == 4 ? new Font("Segoe MDL2 Assets", 12f) : glyph.Font;
            glyph.Location = new Point(x, 21);
            glyph.Size = new Size(30, 34);
            glyph.TextAlign = ContentAlignment.MiddleCenter;
            var label = NewLabel(statusNames[i], 8.8f, FontStyle.Bold, ink);
            label.Location = new Point(x + 36, 18);
            label.Size = new Size(145, 23);
            var value = NewLabel(statusValues[i], 8.3f, FontStyle.Regular, muted);
            value.Location = new Point(x + 36, 42);
            value.Size = new Size(140, 20);
            status.Controls.Add(glyph);
            status.Controls.Add(label);
            status.Controls.Add(value);
        }

        content.Controls.Add(hero);
        content.Controls.Add(flow);
        content.Controls.Add(shortcuts);
        content.Controls.Add(status);
        UpdateCaptureUi();
    }

    private void BuildVoicePage()
    {
        AddPageTitle("语音听写", "遥控器负责收音，微信输入法负责转写与整理");
        var card = NewCard(new Point(34, 100), new Size(960, 570));
        card.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        card.Controls.Add(SectionTitle("听写通道", "\uE720", new Point(30, 24)));

        var stateBand = new Panel();
        stateBand.Location = new Point(30, 64);
        stateBand.Size = new Size(900, 62);
        stateBand.BackColor = IsCapturing ? Color.FromArgb(238, 250, 244) : Color.FromArgb(246, 248, 252);
        voiceBridgeStateLabel = NewLabel(IsCapturing ? "●  已就绪 · 聚焦输入框后按住录音键" : "●  语音桥接已暂停", 10.5f, FontStyle.Bold,
            IsCapturing ? green : muted);
        voiceBridgeStateLabel.Location = new Point(22, 19);
        voiceBridgeStateLabel.AutoSize = true;
        stateBand.Controls.Add(voiceBridgeStateLabel);
        card.Controls.Add(stateBand);

        AddFieldLabel(card, "如何使用", 156);
        var trigger = NewLabel("按住录音键说话，松开后结束听写", 10f, FontStyle.Bold, ink);
        trigger.Location = new Point(220, 156);
        trigger.AutoSize = true;
        card.Controls.Add(trigger);

        AddFieldLabel(card, "文字由谁生成", 210);
        var route = NewLabel("微信输入法 · 使用遥控器麦克风的实时声音", 10f, FontStyle.Regular, ink);
        route.Location = new Point(220, 210);
        route.AutoSize = true;
        card.Controls.Add(route);

        AddFieldLabel(card, "麦克风音量", 264);
        var gainHelp = NewLabel("建议保持 1.0×；只有声音偏小时再小幅调高。", 9.2f, FontStyle.Regular, muted);
        gainHelp.Location = new Point(220, 266);
        gainHelp.Size = new Size(520, 24);
        card.Controls.Add(gainHelp);
        var gain = new TrackBar();
        gain.Location = new Point(212, 296);
        gain.Size = new Size(390, 44);
        gain.Minimum = 5;
        gain.Maximum = 40;
        gain.Value = Math.Max(5, Math.Min(40, (int)(config.gain * 10)));
        var gainValue = NewLabel((gain.Value / 10.0).ToString("0.0") + "×", 10f, FontStyle.Bold, violet);
        gainValue.Location = new Point(620, 304);
        gainValue.Size = new Size(70, 28);
        gain.Scroll += delegate { gainValue.Text = (gain.Value / 10.0).ToString("0.0") + "×"; };
        gain.MouseUp += delegate { config.gain = gain.Value / 10.0; SaveConfig(); };
        card.Controls.Add(gain);
        card.Controls.Add(gainValue);

        var cableState = NewLabel(HasCableInput() ? "●  微信音频通道已就绪" : "●  需要安装或检查 VB-CABLE", 10f, FontStyle.Bold,
            HasCableInput() ? green : Color.FromArgb(202, 76, 76));
        cableState.Location = new Point(220, 358);
        cableState.AutoSize = true;
        card.Controls.Add(cableState);

        var start = PrimaryButton(IsCapturing ? "暂停语音桥接" : "启动语音桥接", new Point(220, 402), new Size(152, 44));
        start.Click += delegate { ToggleCapture(); start.Text = IsCapturing ? "暂停语音桥接" : "启动语音桥接"; };
        var test = SecondaryButton("测试微信语音", new Point(386, 402), new Size(148, 44));
        test.Click += delegate { TestVoiceHotkey(); };
        var sound = SecondaryButton("检查麦克风设置", new Point(548, 402), new Size(158, 44));
        sound.Click += delegate { OpenUri("ms-settings:sound"); };
        card.Controls.Add(start);
        card.Controls.Add(test);
        card.Controls.Add(sound);

        var note = NewLabel("首次使用时，请在微信输入法中将麦克风选择为 CABLE Output。言灵不保存录音、不读取听写文字，也不会上传音频。", 9.3f, FontStyle.Regular, muted);
        note.Location = new Point(30, 494);
        note.Size = new Size(880, 44);
        card.Controls.Add(note);

        content.Controls.Add(card);
        ApplyVisualState(!IsCapturing ? "stopped" : bridgeReady ? "ready" : "connecting");
    }

    private void BuildMappingsPage()
    {
        AddPageTitle("按键快捷方式", "只显示 RC003 真机验证通过的单击与长按操作");
        var card = NewCard(new Point(34, 100), new Size(960, 610));
        card.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        card.Controls.Add(SectionTitle("使用场景", "\uE765", new Point(30, 22)));
        var preset = StyledCombo(new Point(230, 20), new Size(280, 38));
        preset.Items.AddRange(new object[] { "AI 编程（推荐）", "文本编辑", "代码阅读与评审", "自定义" });
        preset.SelectedIndex = config.mappingPreset == "editing" ? 1 : config.mappingPreset == "review" ? 2 : config.mappingPreset == "custom" ? 3 : 0;
        var applyPreset = SecondaryButton("应用这套方案", new Point(528, 18), new Size(138, 40));
        applyPreset.Click += delegate
        {
            if (preset.SelectedIndex == 3) { Toast("在下方选择快捷方式，修改会自动保存"); return; }
            ApplyMappingPreset(preset.SelectedIndex == 1 ? "editing" : preset.SelectedIndex == 2 ? "review" : "coding");
            SaveConfig();
            StartKeyboardBridge();
            ShowPage(2);
            Toast("按键方案已应用");
        };
        card.Controls.Add(preset);
        card.Controls.Add(applyPreset);
        string[,] rows = { { "录音键", "managed", "固定：按住听写，松开结束" }, { "确认键", "enter", "默认：确认或发送" }, { "Home", "win+d", "默认：显示桌面" }, { "TV", "task-switcher", "打开任务切换，左右选择" }, { "功能键", "ctrl+shift+p", "单击执行所选操作" }, { "方向键", "direction-volume-fallback", "短按移动，长按上下调音量" } };
        for (int i = 0; i < rows.GetLength(0); i++)
        {
            int y = 76 + i * 55;
            var icon = NewLabel(i == 0 ? "\uE720" : "\uE765", 14f, FontStyle.Regular, i == 0 ? violet : ink);
            icon.Location = new Point(34, y);
            icon.Size = new Size(42, 38);
            icon.TextAlign = ContentAlignment.MiddleCenter;
            var name = NewLabel(rows[i, 0], 10f, FontStyle.Bold, ink);
            name.Location = new Point(88, y + 3);
            name.Size = new Size(132, 28);
            string configKey = rows[i, 0] == "方向键" ? "上 / 下 / 左 / 右" : rows[i, 0];
            string currentAction = GetMapping(configKey, rows[i, 1]);
            List<ShortcutChoice> choices = ShortcutChoicesFor(configKey, currentAction);
            var input = StyledCombo(new Point(230, y), new Size(300, 36));
            foreach (ShortcutChoice choice in choices) input.Items.Add(choice);
            input.SelectedIndex = FindShortcutChoice(choices, currentAction);
            input.Enabled = rows[i, 0] != "录音键" && rows[i, 0] != "方向键";
            int rowIndex = i;
            input.SelectedIndexChanged += delegate
            {
                ShortcutChoice selected = input.SelectedItem as ShortcutChoice;
                if (selected == null || !input.Enabled) return;
                config.mappingPreset = "custom";
                string selectedKey = rows[rowIndex, 0] == "方向键" ? "上 / 下 / 左 / 右" : rows[rowIndex, 0];
                SetMapping(selectedKey, selected.Shortcut);
                SaveConfig();
            };
            var hint = NewLabel(rows[i, 2], 9.5f, FontStyle.Regular, muted);
            hint.Location = new Point(552, y + 5);
            hint.Size = new Size(350, 30);
            card.Controls.Add(icon);
            card.Controls.Add(name);
            card.Controls.Add(input);
            card.Controls.Add(hint);
        }
        var save = PrimaryButton("立即应用", new Point(230, 530), new Size(132, 42));
        save.Click += delegate { SaveConfig(); StartKeyboardBridge(); Toast("按键快捷方式已生效"); };
        var openBridge = SecondaryButton("打开高级配置", new Point(378, 530), new Size(150, 42));
        openBridge.Click += delegate { Process.Start(Path.Combine(root, "voxdeck-shortcuts.json")); };
        card.Controls.Add(save);
        card.Controls.Add(openBridge);
        content.Controls.Add(card);
    }

    private void BuildDevicePage()
    {
        AddPageTitle("连接与诊断", "检查遥控器、微信音频通道和最近运行状态");
        var device = NewCard(new Point(34, 100), new Size(350, 250));
        device.Controls.Add(SectionTitle("遥控器", "\uE702", new Point(26, 22)));
        var state = NewLabel(IsCapturing && bridgeReady ? "●  已连接，可以使用" : IsCapturing ? "●  正在建立语音连接" : "●  当前未连接", 12f, FontStyle.Bold,
            IsCapturing && bridgeReady ? green : IsCapturing ? amber : muted);
        state.Location = new Point(28, 70);
        state.AutoSize = true;
        var name = NewLabel("小米蓝牙语音遥控器 2 Pro", 10f, FontStyle.Regular, muted);
        name.Location = new Point(29, 108);
        name.AutoSize = true;
        var detail = NewLabel("型号 RC003 · 按键与麦克风", 9f, FontStyle.Regular, muted);
        detail.Location = new Point(29, 136);
        detail.AutoSize = true;
        var scan = PrimaryButton("检查连接", new Point(28, 184), new Size(126, 42));
        scan.Click += delegate { ScanDevice(); };
        var health = SecondaryButton("全面检测", new Point(166, 184), new Size(126, 42));
        health.Click += delegate { RunHealthCheck(); };
        device.Controls.Add(state);
        device.Controls.Add(name);
        device.Controls.Add(detail);
        device.Controls.Add(scan);
        device.Controls.Add(health);

        var diagnostics = NewCard(new Point(402, 100), new Size(592, 570));
        diagnostics.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        diagnostics.Controls.Add(SectionTitle("运行记录", "\uE9D9", new Point(26, 22)));
        var logHelp = NewLabel("正常使用无需关注；连接或听写异常时，可刷新或导出给开发者排查。", 9f, FontStyle.Regular, muted);
        logHelp.Location = new Point(28, 54);
        logHelp.Size = new Size(520, 28);
        diagnostics.Controls.Add(logHelp);
        logBox = new TextBox();
        logBox.Location = new Point(28, 88);
        logBox.Size = new Size(536, 410);
        logBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        logBox.Multiline = true;
        logBox.ScrollBars = ScrollBars.Vertical;
        logBox.ReadOnly = true;
        logBox.BorderStyle = BorderStyle.FixedSingle;
        logBox.BackColor = Color.FromArgb(246, 249, 253);
        logBox.ForeColor = ink;
        logBox.Font = new Font("Consolas", 9.5f);
        logBox.Text = LoadRecentDiagnostics();
        var copy = SecondaryButton("复制记录", new Point(28, 512), new Size(112, 38));
        copy.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
        copy.Click += delegate { if (!string.IsNullOrWhiteSpace(logBox.Text)) Clipboard.SetText(logBox.Text); };
        var refresh = SecondaryButton("刷新", new Point(152, 512), new Size(94, 38));
        refresh.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
        refresh.Click += delegate { logBox.Text = LoadRecentDiagnostics(); };
        var export = SecondaryButton("导出诊断", new Point(258, 512), new Size(112, 38));
        export.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
        export.Click += delegate { ExportDiagnostics(); };
        diagnostics.Controls.Add(logBox);
        diagnostics.Controls.Add(copy);
        diagnostics.Controls.Add(refresh);
        diagnostics.Controls.Add(export);
        content.Controls.Add(device);
        content.Controls.Add(diagnostics);
    }

    private void BuildSettingsPage()
    {
        AddPageTitle("偏好设置", "让言灵按你的习惯在后台运行");
        var card = NewCard(new Point(34, 100), new Size(960, 550));
        card.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        card.Controls.Add(SectionTitle("启动与窗口", "\uE713", new Point(30, 24)));
        var start = StyledCheck("打开言灵后自动连接遥控器", config.startBridgeOnLaunch, new Point(34, 76));
        start.CheckedChanged += delegate { config.startBridgeOnLaunch = start.Checked; SaveConfig(); };
        var traySetting = StyledCheck("关闭主窗口后继续在系统托盘运行", config.minimizeToTray, new Point(34, 124));
        traySetting.CheckedChanged += delegate { config.minimizeToTray = traySetting.Checked; SaveConfig(); };
        var startup = StyledCheck("登录 Windows 后自动启动言灵", config.launchAtStartup, new Point(34, 172));
        startup.CheckedChanged += delegate { config.launchAtStartup = startup.Checked; SetLaunchAtStartup(startup.Checked); SaveConfig(); };
        var privacyTitle = SectionTitle("隐私与安全", "\uEA18", new Point(30, 234));
        var privacy = StyledCheck("本地安全模式：不保存录音、不上传音频、不注入其他程序", true, new Point(34, 276));
        privacy.Enabled = false;
        card.Controls.Add(start);
        card.Controls.Add(traySetting);
        card.Controls.Add(startup);
        card.Controls.Add(privacyTitle);
        card.Controls.Add(privacy);

        var setup = PrimaryButton("重新完成新手设置", new Point(34, 342), new Size(168, 42));
        setup.Click += delegate { ShowSetupWizard(); };
        var open = SecondaryButton("打开高级配置", new Point(218, 342), new Size(148, 42));
        open.Click += delegate { Process.Start(configPath); };
        var export = SecondaryButton("备份配置", new Point(382, 342), new Size(126, 42));
        export.Click += delegate { ExportConfig(); };
        var about = NewLabel("言灵 · Vibe Flow · Windows Preview\r\n面向 RC003 的本地语音输入与快捷操作工具 · 开源版本", 9.5f, FontStyle.Regular, muted);
        about.Location = new Point(34, 454);
        about.Size = new Size(760, 58);
        card.Controls.Add(setup);
        card.Controls.Add(open);
        card.Controls.Add(export);
        card.Controls.Add(about);
        content.Controls.Add(card);
    }

    private void AddPageTitle(string title, string subtitle)
    {
        var a = NewLabel(title, 24f, FontStyle.Bold, ink);
        a.Location = new Point(42, 24);
        a.AutoSize = true;
        var b = NewLabel(subtitle, 10f, FontStyle.Regular, muted);
        b.Location = new Point(45, 67);
        b.AutoSize = true;
        content.Controls.Add(a);
        content.Controls.Add(b);
    }

    private Label SectionTitle(string title, string glyph, Point point)
    {
        var label = NewLabel(glyph + "   " + title, 11f, FontStyle.Bold, ink);
        label.Location = point;
        label.AutoSize = true;
        return label;
    }

    private void AddFieldLabel(Control parent, string text, int y)
    {
        var label = NewLabel(text, 10f, FontStyle.Bold, ink);
        label.Location = new Point(32, y);
        label.Size = new Size(160, 28);
        parent.Controls.Add(label);
    }

    private RoundPanel NewCard(Point location, Size size)
    {
        var panel = new RoundPanel();
        panel.Location = location;
        panel.Size = size;
        panel.BackColor = Color.White;
        panel.BorderColor = line;
        panel.Radius = 8;
        return panel;
    }

    private Label NewLabel(string text, float size, FontStyle style, Color color)
    {
        var label = new Label();
        label.Text = text;
        label.Font = new Font("Microsoft YaHei UI", size, style);
        label.ForeColor = color;
        label.BackColor = Color.Transparent;
        return label;
    }

    private Button PrimaryButton(string text, Point location, Size size)
    {
        var b = FlatButton(text, location, size);
        b.BackColor = violet;
        b.ForeColor = Color.White;
        b.FlatAppearance.BorderColor = violet;
        b.FlatAppearance.MouseOverBackColor = Color.FromArgb(86, 78, 236);
        b.FlatAppearance.MouseDownBackColor = Color.FromArgb(72, 65, 216);
        return b;
    }

    private Button SecondaryButton(string text, Point location, Size size)
    {
        var b = FlatButton(text, location, size);
        b.BackColor = Color.FromArgb(249, 249, 253);
        b.ForeColor = violet;
        b.FlatAppearance.MouseOverBackColor = Color.FromArgb(238, 240, 255);
        b.FlatAppearance.MouseDownBackColor = Color.FromArgb(226, 230, 250);
        return b;
    }

    private Button FlatButton(string text, Point location, Size size)
    {
        var b = new Button();
        b.Text = text;
        b.Location = location;
        b.Size = size;
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderColor = Color.FromArgb(218, 220, 242);
        b.FlatAppearance.BorderSize = 1;
        b.Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold);
        b.Cursor = Cursors.Hand;
        b.UseVisualStyleBackColor = false;
        b.TabStop = true;
        Action updateRegion = delegate
        {
            if (b.Width <= 0 || b.Height <= 0) return;
            Region previous = b.Region;
            using (GraphicsPath path = RoundedControlPath(new Rectangle(0, 0, b.Width, b.Height), 6)) b.Region = new Region(path);
            if (previous != null) previous.Dispose();
        };
        b.Resize += delegate { updateRegion(); };
        updateRegion();
        return b;
    }

    private static GraphicsPath RoundedControlPath(Rectangle rectangle, int radius)
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

    private ComboBox StyledCombo(Point location, Size size)
    {
        var c = new ComboBox();
        c.Location = location;
        c.Size = size;
        c.DropDownStyle = ComboBoxStyle.DropDownList;
        c.FlatStyle = FlatStyle.Flat;
        c.Font = new Font("Microsoft YaHei UI", 9.5f);
        c.BackColor = Color.White;
        return c;
    }

    private TextBox StyledTextBox(string value, Point location, Size size)
    {
        var t = new TextBox();
        t.Text = value ?? "";
        t.Location = location;
        t.Size = size;
        t.BorderStyle = BorderStyle.FixedSingle;
        t.Font = new Font("Microsoft YaHei UI", 9.5f);
        return t;
    }

    private CheckBox StyledCheck(string text, bool value, Point location)
    {
        var c = new CheckBox();
        c.Text = text;
        c.Checked = value;
        c.Location = location;
        c.Size = new Size(520, 34);
        c.ForeColor = ink;
        c.Font = new Font("Microsoft YaHei UI", 9.5f);
        return c;
    }

    private void PaintHeroGlow(object sender, PaintEventArgs e)
    {
        var panel = (Control)sender;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        for (int i = 0; i < 7; i++)
        {
            int diameter = 105 + i * 38;
            using (var pen = new Pen(Color.FromArgb(18, 119, 105, 255), 1f))
                e.Graphics.DrawEllipse(pen, panel.Width - 240 - diameter / 2, 170 - diameter / 2, diameter, diameter);
        }
    }

    private void SetupTray()
    {
        tray.Icon = Icon;
        tray.Text = "言灵 · Vibe Flow";
        tray.Visible = true;
        tray.DoubleClick += delegate { ShowMainWindow(); };
        var menu = new ContextMenuStrip();
        menu.Items.Add("打开言灵", null, delegate { ShowMainWindow(); });
        menu.Items.Add("启动 / 暂停语音桥接", null, delegate { ToggleCapture(); });
        menu.Items.Add("退出", null, delegate { config.minimizeToTray = false; Close(); });
        tray.ContextMenuStrip = menu;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (backgroundLaunch)
        {
            Hide();
            ShowInTaskbar = false;
        }
        if (!config.setupCompleted)
        {
            if (!backgroundLaunch) ShowSetupWizard();
            return;
        }
        StartKeyboardBridge();
        if (config.startBridgeOnLaunch && !IsCapturing) StartCapture();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (config.minimizeToTray && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        applicationExiting = true;
        if (visualTimer != null) { visualTimer.Stop(); visualTimer.Dispose(); visualTimer = null; }
        StopCapture();
        StopKeyboardBridge();
        ReleaseVoiceHotkey();
        try { if (showWindowEvent != null) { showWindowEvent.Set(); showWindowEvent.Dispose(); } } catch { }
        tray.Visible = false;
        base.OnFormClosing(e);
    }

    private void ShowMainWindow()
    {
        ShowInTaskbar = true;
        if (Location.X < -1000 || Location.Y < -1000)
        {
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(area.Left + Math.Max(0, (area.Width - Width) / 2), area.Top + Math.Max(0, (area.Height - Height) / 2));
        }
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
        if (!config.setupCompleted && !setupWizardOpen) BeginInvoke(new Action(ShowSetupWizard));
    }

    private bool IsCapturing { get { return captureProcess != null && !captureProcess.HasExited; } }

    private void ToggleCapture()
    {
        if (IsCapturing) StopCapture(); else StartCapture();
        UpdateCaptureUi();
    }

    private void StartCapture()
    {
        config = LoadConfig();
        captureStopping = false;
        bridgeReady = false;
        string script = Path.Combine(root, "scripts", "remote-voice-capture.ps1");
        string nativeCapture = Path.Combine(root, "VibeMicAtvvCapture.exe");
        if (!File.Exists(nativeCapture) && !File.Exists(script)) { Toast("语音组件不完整，请重新安装言灵"); return; }
        try
        {
            StopOrphanCaptureCore();
            StartKeyboardBridge();
            if (File.Exists(eventsPath)) File.Delete(eventsPath);
            lastEventLength = 0;
            var start = new ProcessStartInfo();
            if (File.Exists(nativeCapture))
            {
                start.FileName = nativeCapture;
                start.Arguments = config.captureSeconds + " \"" + sessionDir + "\" \"" + config.audioEndpointName + "\" " + config.gain.ToString(CultureInfo.InvariantCulture) + " " + config.drainMs;
            }
            else
            {
                start.FileName = "powershell.exe";
                start.Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + script + "\" -Seconds " + config.captureSeconds + " -OutDir \"" + sessionDir + "\"";
            }
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.WindowStyle = ProcessWindowStyle.Hidden;
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;
            start.StandardOutputEncoding = Encoding.UTF8;
            start.StandardErrorEncoding = Encoding.UTF8;
            captureProcess = new Process();
            captureProcess.StartInfo = start;
            captureProcess.EnableRaisingEvents = true;
            captureProcess.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (e.Data != null) Log(e.Data); };
            captureProcess.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (e.Data != null) Log("ERR " + e.Data); };
            captureProcess.Exited += delegate { try { BeginInvoke(new Action(CaptureExited)); } catch { } };
            captureProcess.Start();
            captureProcess.BeginOutputReadLine();
            captureProcess.BeginErrorReadLine();
            Log("Voice bridge started.");
            UpdateCaptureUi();
            Toast("正在连接遥控器麦克风，请稍候");
        }
        catch (Exception ex)
        {
            Log("Start failed: " + ex.Message);
            Toast("连接没有成功，正在自动重试");
            ScheduleCaptureRestart();
        }
    }

    private void StopCapture()
    {
        captureStopping = true;
        bridgeReady = false;
        if (reconnectTimer != null) { reconnectTimer.Stop(); reconnectTimer.Dispose(); reconnectTimer = null; }
        try
        {
            if (IsCapturing)
            {
                SignalEvent("Local\\VibeMicStopCapture");
                if (!captureProcess.WaitForExit(5000))
                {
                    captureProcess.Kill();
                    captureProcess.WaitForExit(1500);
                }
            }
        }
        catch { }
        finally { ReleaseVoiceHotkey(); }
        Log("Voice bridge stopped.");
        UpdateCaptureUi();
    }

    private void CaptureExited()
    {
        bridgeReady = false;
        UpdateCaptureUi();
        if (applicationExiting || captureStopping || !config.startBridgeOnLaunch) return;
        Log("Capture helper exited unexpectedly; reconnect scheduled.");
        connectionBadge.Text = "●  正在重新连接";
        connectionBadge.ForeColor = Color.FromArgb(224, 144, 40);
        ScheduleCaptureRestart();
    }

    private void ScheduleCaptureRestart()
    {
        if (applicationExiting || captureStopping || !config.startBridgeOnLaunch) return;
        if (reconnectTimer != null) return;
        int[] delays = { 1500, 3000, 6000, 12000, 30000 };
        int delay = delays[Math.Min(reconnectAttempt, delays.Length - 1)];
        reconnectAttempt++;
        reconnectTimer = new System.Windows.Forms.Timer();
        reconnectTimer.Interval = delay;
        reconnectTimer.Tick += delegate
        {
            reconnectTimer.Stop();
            reconnectTimer.Dispose();
            reconnectTimer = null;
            if (!applicationExiting && !captureStopping && !IsCapturing) StartCapture();
        };
        reconnectTimer.Start();
        Log("Reconnect in " + delay + " ms.");
    }

    private void StopOrphanCaptureCore()
    {
        SignalEvent("Local\\VibeMicStopCapture");
        foreach (Process process in Process.GetProcessesByName("VibeMicAtvvCapture"))
        {
            try
            {
                if (captureProcess == null || process.Id != captureProcess.Id)
                {
                    if (!process.WaitForExit(3000))
                    {
                        process.Kill();
                        process.WaitForExit(1500);
                    }
                }
            }
            catch { }
            finally { process.Dispose(); }
        }
    }

    private void StartKeyboardBridge()
    {
        try
        {
            SyncKeyboardBridgeConfig();
            Process[] running = Process.GetProcessesByName("VoxDeckInputBridge");
            if (running.Length > 0)
            {
                keyboardBridgeProcess = running[0];
                for (int i = 1; i < running.Length; i++) running[i].Dispose();
                return;
            }
            string executable = Path.Combine(root, "VoxDeckInputBridge.exe");
            if (!File.Exists(executable)) { Log("Keyboard bridge is missing."); return; }
            var start = new ProcessStartInfo(executable, "--background");
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.WindowStyle = ProcessWindowStyle.Hidden;
            keyboardBridgeProcess = Process.Start(start);
            Log("Keyboard bridge started in background.");
        }
        catch (Exception ex) { Log("Keyboard bridge start failed: " + ex.Message); }
    }

    private void StopKeyboardBridge()
    {
        try
        {
            SignalEvent("Local\\VibeMicStopKeyboardBridge");
            foreach (Process process in Process.GetProcessesByName("VoxDeckInputBridge"))
            {
                try
                {
                    if (!process.WaitForExit(2500)) process.Kill();
                }
                catch { }
                finally { process.Dispose(); }
            }
        }
        catch { }
    }

    private static void SignalEvent(string name)
    {
        try
        {
            using (EventWaitHandle handle = EventWaitHandle.OpenExisting(name)) handle.Set();
        }
        catch (WaitHandleCannotBeOpenedException) { }
        catch { }
    }

    private void TestVoiceHotkey()
    {
        ThreadPool.QueueUserWorkItem(delegate
        {
            keybd_event(0x11, 0x1D, 0, UIntPtr.Zero);
            keybd_event(0x5B, 0x5B, 0, UIntPtr.Zero);
            Thread.Sleep(180);
            ReleaseVoiceHotkey();
        });
        Toast("已发送 Ctrl + Win，请确认微信语音输入已唤起");
    }

    private static void ReleaseVoiceHotkey()
    {
        keybd_event(0x5B, 0x5B, 0x0002, UIntPtr.Zero);
        keybd_event(0x11, 0x1D, 0x0002, UIntPtr.Zero);
    }

    private static void OpenUri(string uri)
    {
        try
        {
            var start = new ProcessStartInfo(uri);
            start.UseShellExecute = true;
            Process.Start(start);
        }
        catch { }
    }

    private bool HasCableInput()
    {
        try
        {
            for (uint i = 0; i < waveOutGetNumDevs(); i++)
            {
                WaveOutCaps caps;
                if (waveOutGetDevCaps((UIntPtr)i, out caps, (uint)Marshal.SizeOf(typeof(WaveOutCaps))) == 0 &&
                    (caps.name ?? "").IndexOf(config.audioEndpointName ?? "CABLE Input", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
        }
        catch { }
        return false;
    }

    private void ShowSetupWizard()
    {
        if (setupWizardOpen) return;
        setupWizardOpen = true;
        using (var wizard = new Form())
        {
            wizard.Text = "欢迎使用言灵 · Vibe Flow";
            wizard.Width = 790;
            wizard.Height = 680;
            wizard.FormBorderStyle = FormBorderStyle.FixedDialog;
            wizard.MaximizeBox = false;
            wizard.MinimizeBox = false;
            wizard.StartPosition = FormStartPosition.CenterParent;
            wizard.BackColor = Color.FromArgb(244, 247, 252);
            wizard.Font = Font;
            wizard.Icon = Icon;

            var eyebrow = NewLabel("言灵 · VIBE FLOW · 首次设置", 9f, FontStyle.Bold, violet);
            eyebrow.Location = new Point(78, 28);
            eyebrow.AutoSize = true;
            var setupLogo = new PictureBox();
            setupLogo.Image = LoadBrandLogo();
            setupLogo.SizeMode = PictureBoxSizeMode.Zoom;
            setupLogo.BackColor = Color.Transparent;
            setupLogo.Location = new Point(42, 20);
            setupLogo.Size = new Size(28, 28);
            wizard.Disposed += delegate { if (setupLogo.Image != null) setupLogo.Image.Dispose(); };
            var title = NewLabel("三步完成，以后按住就能说", 23f, FontStyle.Bold, ink);
            title.Location = new Point(40, 49);
            title.AutoSize = true;
            var subtitle = NewLabel("跟着页面完成一次设置，言灵以后会自动连接并安静地留在系统托盘。", 10f, FontStyle.Regular, muted);
            subtitle.Location = new Point(43, 91);
            subtitle.AutoSize = true;
            wizard.Controls.Add(setupLogo);
            wizard.Controls.Add(eyebrow);
            wizard.Controls.Add(title);
            wizard.Controls.Add(subtitle);

            bool cableReady = HasCableInput();
            RoundPanel firstStep = AddSetupStep(wizard, 1, "连接遥控器", "先在 Windows 蓝牙中配对 MI RC / 小米蓝牙语音遥控器。", 128,
                "打开蓝牙设置", delegate { OpenUri("ms-settings:bluetooth"); });
            foreach (Control control in firstStep.Controls)
            {
                Label detailLabel = control as Label;
                if (detailLabel != null && detailLabel.Text.IndexOf("Windows 蓝牙", StringComparison.Ordinal) >= 0)
                {
                    detailLabel.Location = new Point(76, 36);
                    detailLabel.Size = new Size(430, 22);
                }
            }
            bool startupDefault = config.setupCompleted ? config.launchAtStartup : true;
            var startupChoice = StyledCheck("开机后自动启动言灵（推荐）", startupDefault, new Point(76, 62));
            startupChoice.Size = new Size(420, 26);
            startupChoice.Font = new Font("Segoe UI", 9f);
            firstStep.Controls.Add(startupChoice);
            AddSetupStep(wizard, 2, "安装语音通道", cableReady ? "VB-CABLE 已就绪，无需重复安装。" : "安装免费的 VB-CABLE，让遥控器声音进入微信输入法。", 240,
                cableReady ? "通道已就绪" : "前往官方安装", delegate { if (!cableReady) OpenUri("https://vb-audio.com/Cable/"); });
            AddSetupStep(wizard, 3, "选择微信输入法麦克风", "在微信输入法中将麦克风选择为 CABLE Output，只需设置一次。", 352,
                "打开声音设置", delegate { OpenUri("ms-settings:sound"); });

            var routeConfirmed = StyledCheck("我已在微信输入法中选择 CABLE Output", false, new Point(44, 476));
            routeConfirmed.Size = new Size(470, 34);
            var cableStatus = NewLabel(cableReady ? "●  语音通道已就绪" : "●  还未检测到 VB-CABLE", 9.5f, FontStyle.Bold,
                cableReady ? green : Color.FromArgb(202, 76, 76));
            cableStatus.Location = new Point(47, 518);
            cableStatus.AutoSize = true;

            var recheck = SecondaryButton("重新检测", new Point(495, 510), new Size(110, 40));
            var test = SecondaryButton("测试微信语音", new Point(44, 570), new Size(145, 44));
            test.Click += delegate { TestVoiceHotkey(); };
            var finish = PrimaryButton("完成并开始使用", new Point(562, 570), new Size(170, 44));
            Action updateFinishState = delegate
            {
                finish.Enabled = cableReady && routeConfirmed.Checked;
                finish.BackColor = finish.Enabled ? violet : Color.FromArgb(210, 214, 226);
            };
            updateFinishState();
            routeConfirmed.CheckedChanged += delegate { updateFinishState(); };
            recheck.Click += delegate
            {
                cableReady = HasCableInput();
                cableStatus.Text = cableReady ? "●  语音通道已就绪" : "●  未检测到 VB-CABLE，请完成安装";
                cableStatus.ForeColor = cableReady ? green : Color.FromArgb(202, 76, 76);
                updateFinishState();
            };
            finish.Click += delegate
            {
                if (!HasCableInput() || !routeConfirmed.Checked)
                {
                    MessageBox.Show(wizard, "还差一步：请确认 VB-CABLE 已就绪，并在微信输入法中选择 CABLE Output。", "言灵 · Vibe Flow", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                config.setupCompleted = true;
                config.launchAtStartup = startupChoice.Checked;
                config.startBridgeOnLaunch = true;
                config.minimizeToTray = true;
                SetLaunchAtStartup(startupChoice.Checked);
                SaveConfig();
                wizard.DialogResult = DialogResult.OK;
                wizard.Close();
            };
            var privacy = NewLabel("本地运行 · 不保存录音 · 不读取听写文字", 9f, FontStyle.Regular, muted);
            privacy.Location = new Point(220, 582);
            privacy.Size = new Size(320, 24);
            privacy.TextAlign = ContentAlignment.MiddleCenter;
            wizard.Controls.Add(routeConfirmed);
            wizard.Controls.Add(cableStatus);
            wizard.Controls.Add(recheck);
            wizard.Controls.Add(test);
            wizard.Controls.Add(privacy);
            wizard.Controls.Add(finish);
            wizard.AcceptButton = finish;

            if (wizard.ShowDialog(this) == DialogResult.OK)
            {
                StartKeyboardBridge();
                if (!IsCapturing) StartCapture();
                UpdateCaptureUi();
                Toast("设置完成，言灵正在连接遥控器");
            }
        }
        setupWizardOpen = false;
    }

    private RoundPanel AddSetupStep(Control parent, int number, string title, string description, int y, string buttonText, EventHandler action)
    {
        var step = new RoundPanel();
        step.Location = new Point(38, y);
        step.Size = new Size(694, 98);
        step.Radius = 8;
        step.BackColor = Color.White;
        step.BorderColor = line;
        var numberLabel = NewLabel(number.ToString(), 12f, FontStyle.Bold, Color.White);
        numberLabel.BackColor = violet;
        numberLabel.TextAlign = ContentAlignment.MiddleCenter;
        numberLabel.Location = new Point(18, 29);
        numberLabel.Size = new Size(36, 36);
        var heading = NewLabel(title, 11f, FontStyle.Bold, ink);
        heading.Location = new Point(76, 19);
        heading.AutoSize = true;
        var detail = NewLabel(description, 9.5f, FontStyle.Regular, muted);
        detail.Location = new Point(76, 49);
        detail.Size = new Size(430, 38);
        var button = SecondaryButton(buttonText, new Point(536, 28), new Size(135, 40));
        button.Click += action;
        step.Controls.Add(numberLabel);
        step.Controls.Add(heading);
        step.Controls.Add(detail);
        step.Controls.Add(button);
        parent.Controls.Add(step);
        return step;
    }

    private void UpdateCaptureUi()
    {
        if (InvokeRequired) { BeginInvoke(new Action(UpdateCaptureUi)); return; }
        if (bridgeButton != null && !bridgeButton.IsDisposed) bridgeButton.Text = IsCapturing ? "暂停语音桥接" : "启动语音桥接";
        if (heroTitle != null && !heroTitle.IsDisposed)
            heroTitle.Text = !IsCapturing ? "语音桥接已暂停" : bridgeReady ? "已准备好" : "正在连接";
        if (heroSubtitle != null && !heroSubtitle.IsDisposed)
            heroSubtitle.Text = !IsCapturing ? "启动后，按住遥控器录音键即可在当前输入框听写" : bridgeReady ? "聚焦输入框，按住遥控器录音键开始说话" : "正在建立遥控器语音通道，请稍候";
        if (heroStateLabel != null && !heroStateLabel.IsDisposed)
            heroStateLabel.Text = !IsCapturing ? "VOICE LINK OFF" : bridgeReady ? "READY" : "CONNECTING";
        connectionBadge.Text = !IsCapturing ? "●  语音已暂停" : bridgeReady ? "●  语音链路就绪" : "●  正在连接";
        connectionBadge.ForeColor = !IsCapturing ? muted : bridgeReady ? green : amber;
        ApplyVisualState(!IsCapturing ? "stopped" : bridgeReady ? "ready" : "connecting");
    }

    private void ApplyVisualState(string state)
    {
        Color accent;
        Color surface;
        string voiceText;
        if (state == "recording")
        {
            accent = violet;
            surface = Color.FromArgb(244, 242, 255);
            voiceText = "●  正在听写 · 遥控器麦克风收音中";
        }
        else if (state == "ready")
        {
            accent = green;
            surface = Color.FromArgb(238, 250, 244);
            voiceText = "●  已就绪 · 聚焦输入框后按住录音键";
        }
        else if (state == "connecting")
        {
            accent = amber;
            surface = Color.FromArgb(255, 248, 234);
            voiceText = "●  正在连接遥控器麦克风";
        }
        else
        {
            accent = muted;
            surface = Color.FromArgb(248, 249, 252);
            voiceText = "●  语音桥接已暂停";
        }
        if (heroStateLabel != null && !heroStateLabel.IsDisposed) heroStateLabel.ForeColor = accent;
        if (heroPanel != null && !heroPanel.IsDisposed) heroPanel.BackColor = surface;
        if (remoteVisual != null && !remoteVisual.IsDisposed)
        {
            remoteVisual.AccentColor = accent;
            remoteVisual.IsActive = state != "stopped";
            remoteVisual.IsRecording = state == "recording";
            remoteVisual.Invalidate();
        }
        connectionBadge.BackColor = surface;
        if (voiceBridgeStateLabel != null && !voiceBridgeStateLabel.IsDisposed)
        {
            voiceBridgeStateLabel.Text = voiceText;
            voiceBridgeStateLabel.ForeColor = accent;
            if (voiceBridgeStateLabel.Parent != null) voiceBridgeStateLabel.Parent.BackColor = surface;
        }
    }

    private void PollActivity()
    {
        try
        {
            if (File.Exists(eventsPath))
            {
                long len = new FileInfo(eventsPath).Length;
                if (len > lastEventLength)
                {
                    lastEventLength = len;
                    reconnectAttempt = 0;
                    if (!bridgeReady)
                    {
                        string events = File.ReadAllText(eventsPath, Encoding.UTF8);
                        if (events.IndexOf("\"name\":\"capabilities\"", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            bridgeReady = true;
                            UpdateCaptureUi();
                        }
                    }
                }
            }
            UpdateSessionConfidence();
        }
        catch { }
    }

    private void ScanDevice()
    {
        Toast("正在检查蓝牙和遥控器语音通道");
        if (!IsCapturing) StartCapture();
        UpdateCaptureUi();
    }

    private void RunHealthCheck()
    {
        bool cable = HasCableInput();
        bool capture = IsCapturing;
        bool keyboard = IsProcessRunning("VoxDeckInputBridge");
        string runtimeLog = Path.Combine(sessionDir, "vibe-mic-runtime.log");
        string runtime = ReadLogTail(runtimeLog, 160);
        bool atvv = runtime.IndexOf("ATVV READY", StringComparison.OrdinalIgnoreCase) >= 0;
        bool recentStream = runtime.IndexOf("STREAM STOP", StringComparison.OrdinalIgnoreCase) >= 0;

        var result = new StringBuilder();
        result.AppendLine("言灵 · Vibe Flow 全面检测  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        result.AppendLine(StatusMark(keyboard) + " 遥控器按键" + (keyboard ? "可以使用" : "未启动，点击“检查连接”重试"));
        result.AppendLine(StatusMark(capture) + " 语音桥接" + (capture ? "正在运行" : "当前未运行"));
        result.AppendLine(StatusMark(atvv) + " 遥控器麦克风" + (atvv ? "已经接入" : "尚未就绪，请检查蓝牙配对"));
        result.AppendLine(StatusMark(cable) + " 微信音频通道" + (cable ? "已经就绪" : "未检测到 VB-CABLE，请安装后重新打开言灵"));
        result.AppendLine(StatusMark(recentStream) + " 遥控器收音" + (recentStream ? "已有成功会话" : "请按住录音键完成一次测试"));
        result.AppendLine((config.launchAtStartup ? "[ON] " : "[--] ") + "开机自启动" + (config.launchAtStartup ? "已启用" : "未启用（可在设置中开启）"));
        result.AppendLine();
        result.AppendLine(keyboard && capture && atvv && cable
            ? "结论：核心链路已就绪。聚焦输入框后按住录音键即可听写。"
            : "结论：仍有项目需要处理，按上方提示修复后重新检查。");
        result.AppendLine();
        result.Append(LoadRecentDiagnostics());

        if (logBox != null && !logBox.IsDisposed) logBox.Text = result.ToString();
        Toast(keyboard && capture && atvv && cable ? "健康检查通过" : "健康检查发现待处理项目");
    }

    private static string StatusMark(bool success)
    {
        return success ? "[OK] " : "[!!] ";
    }

    private static bool IsProcessRunning(string processName)
    {
        Process[] processes = Process.GetProcessesByName(processName);
        bool running = processes.Length > 0;
        foreach (Process process in processes) process.Dispose();
        return running;
    }

    private static string ReadLogTail(string path, int maximumLines)
    {
        if (!File.Exists(path)) return "";
        string[] lines = File.ReadAllLines(path, Encoding.UTF8);
        int start = Math.Max(0, lines.Length - maximumLines);
        return string.Join(Environment.NewLine, lines, start, lines.Length - start);
    }

    private void UpdateSessionConfidence()
    {
        if (activityLabel == null || activityLabel.IsDisposed) return;
        string path = Path.Combine(sessionDir, "vibe-mic-runtime.log");
        if (!File.Exists(path)) return;
        string[] lines = File.ReadAllLines(path, Encoding.UTF8);
        int startIndex = -1;
        int stopIndex = -1;
        for (int i = Math.Max(0, lines.Length - 200); i < lines.Length; i++)
        {
            if (lines[i].IndexOf("STREAM START", StringComparison.OrdinalIgnoreCase) >= 0) startIndex = i;
            if (lines[i].IndexOf("STREAM STOP", StringComparison.OrdinalIgnoreCase) >= 0) stopIndex = i;
        }

        if (IsCapturing && startIndex > stopIndex)
        {
            DateTime parsed;
            if (TryParseRuntimeTimestamp(lines[startIndex], out parsed)) activeStreamStarted = parsed;
            TimeSpan elapsed = activeStreamStarted == DateTime.MinValue ? TimeSpan.Zero : DateTime.Now - activeStreamStarted;
            activityLabel.Text = "●  正在听写  " + Math.Max(0, (int)elapsed.TotalMinutes).ToString("00") + ":" + Math.Max(0, elapsed.Seconds).ToString("00") + "  ·  遥控器收音中";
            activityLabel.ForeColor = violet;
            if (heroTitle != null && !heroTitle.IsDisposed) heroTitle.Text = "正在听写";
            if (heroSubtitle != null && !heroSubtitle.IsDisposed) heroSubtitle.Text = "请自然说话，松开录音键后由微信输入法整理文字";
            if (heroStateLabel != null && !heroStateLabel.IsDisposed) heroStateLabel.Text = "LISTENING";
            ApplyVisualState("recording");
            return;
        }

        activeStreamStarted = DateTime.MinValue;
        UpdateCaptureUi();
        if (stopIndex >= 0)
        {
            string duration = ExtractMetric(lines[stopIndex], "audio_ms");
            string peak = ExtractMetric(lines[stopIndex], "peak_pct");
            int milliseconds;
            string seconds = int.TryParse(duration, out milliseconds) ? (milliseconds / 1000.0).ToString("0.0") : "--";
            activityLabel.Text = "上一段听写 " + seconds + " 秒  ·  声音峰值 " + (string.IsNullOrWhiteSpace(peak) ? "--" : peak) + "%";
            activityLabel.ForeColor = muted;
        }
    }

    private static bool TryParseRuntimeTimestamp(string line, out DateTime value)
    {
        value = DateTime.MinValue;
        if (string.IsNullOrWhiteSpace(line) || line.Length < 23) return false;
        return DateTime.TryParseExact(line.Substring(0, 23), "yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal, out value);
    }

    private static string ExtractMetric(string line, string name)
    {
        string marker = name + "=";
        int start = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return "";
        start += marker.Length;
        int end = line.IndexOf(' ', start);
        return end < 0 ? line.Substring(start) : line.Substring(start, end - start);
    }

    private void Log(string message)
    {
        if (InvokeRequired) { BeginInvoke(new Action<string>(Log), message); return; }
        string lineText = DateTime.Now.ToString("HH:mm:ss") + " " + message;
        if (logBox != null && !logBox.IsDisposed) logBox.AppendText(lineText + Environment.NewLine);
    }

    private string LoadRecentDiagnostics()
    {
        var sb = new StringBuilder();
        sb.AppendLine("言灵 · Vibe Flow 运行诊断");
        sb.AppendLine("RC003: Windows paired");
        sb.AppendLine("ATVV service: ab5e0001-5a21-4f05-bc7d-af01f617b664");
        sb.AppendLine("Voice key: F5 / scan 0x3F");
        sb.AppendLine("Session: " + sessionDir);
        if (File.Exists(eventsPath)) sb.AppendLine("Event log: " + new FileInfo(eventsPath).Length + " bytes");
        string runtimeLog = Path.Combine(sessionDir, "vibe-mic-runtime.log");
        if (File.Exists(runtimeLog))
        {
            sb.AppendLine();
            sb.AppendLine("Recent runtime events:");
            string[] lines = File.ReadAllLines(runtimeLog, Encoding.UTF8);
            int start = Math.Max(0, lines.Length - 40);
            for (int i = start; i < lines.Length; i++) sb.AppendLine(lines[i]);
        }
        string inputLog = Path.Combine(root, "input-bridge-log.txt");
        if (File.Exists(inputLog))
        {
            sb.AppendLine();
            sb.AppendLine("Recent RC003 button events:");
            string[] lines = File.ReadAllLines(inputLog, Encoding.UTF8);
            int start = Math.Max(0, lines.Length - 50);
            for (int i = start; i < lines.Length; i++) sb.AppendLine(lines[i]);
        }
        return sb.ToString();
    }

    private void Toast(string text)
    {
        if (InvokeRequired) { BeginInvoke(new Action<string>(Toast), text); return; }
        if (heroSubtitle != null && !heroSubtitle.IsDisposed) heroSubtitle.Text = text;
    }

    private VibeMicConfig LoadConfig()
    {
        EnsureConfig();
        try
        {
            VibeMicConfig loaded = new JavaScriptSerializer().Deserialize<VibeMicConfig>(File.ReadAllText(configPath, Encoding.UTF8)) ?? VibeMicConfig.Default();
            if (MigrateConfig(loaded)) File.WriteAllText(configPath, new JavaScriptSerializer().Serialize(loaded), Encoding.UTF8);
            return loaded;
        }
        catch { return VibeMicConfig.Default(); }
    }

    private static bool MigrateConfig(VibeMicConfig value)
    {
        bool changed = value.schemaVersion < 8;
        value.schemaVersion = 8;
        if (value.captureSeconds < 0) { value.captureSeconds = 0; changed = true; }
        if (value.gain <= 0 || value.gain > 4) { value.gain = 1.0; changed = true; }
        if (string.IsNullOrWhiteSpace(value.voiceMode)) { value.voiceMode = "hold"; changed = true; }
        if (string.IsNullOrWhiteSpace(value.audioEndpointName)) { value.audioEndpointName = "CABLE Input"; changed = true; }
        if (string.IsNullOrWhiteSpace(value.inputMethod)) { value.inputMethod = "wechat"; changed = true; }
        if (string.IsNullOrWhiteSpace(value.inputMethodHotkey)) { value.inputMethodHotkey = "ctrl+win"; changed = true; }
        if (value.drainMs <= 0) { value.drainMs = 180; changed = true; }
        if (string.IsNullOrWhiteSpace(value.mappingPreset)) { value.mappingPreset = "coding"; changed = true; }
        if (value.mappings == null) { value.mappings = new Dictionary<string, string>(); changed = true; }
        Dictionary<string, string> defaults = VibeMicConfig.Default().mappings;
        foreach (KeyValuePair<string, string> pair in defaults)
        {
            if (!value.mappings.ContainsKey(pair.Key)) { value.mappings[pair.Key] = pair.Value; changed = true; }
        }
        string[] unsupportedMappings = { "返回键", "音量 + / -", "返回操作", "换行 / 删除" };
        foreach (string key in unsupportedMappings)
            if (value.mappings.Remove(key)) changed = true;
        return changed;
    }

    private void EnsureConfig()
    {
        if (!File.Exists(configPath)) File.WriteAllText(configPath, new JavaScriptSerializer().Serialize(VibeMicConfig.Default()), Encoding.UTF8);
    }

    private void SaveConfig()
    {
        try
        {
            config.schemaVersion = 8;
            File.WriteAllText(configPath, new JavaScriptSerializer().Serialize(config), Encoding.UTF8);
            SyncKeyboardBridgeConfig();
        }
        catch (Exception ex) { Log("Config save failed: " + ex.Message); }
    }

    private void SetLaunchAtStartup(bool enabled)
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", true))
            {
                if (key == null) return;
                key.DeleteValue("Vibe Mic", false);
                key.DeleteValue("声启 MIC", false);
                if (enabled) key.SetValue("Vibe Flow", "\"" + Application.ExecutablePath + "\" --background");
                else key.DeleteValue("Vibe Flow", false);
            }
        }
        catch (Exception ex) { Log("Startup setting failed: " + ex.Message); }
    }

    private void SyncKeyboardBridgeConfig()
    {
        if (config == null) return;
        try
        {
            var mappings = new List<Dictionary<string, object>>();
            mappings.Add(BridgeMapping("voice", "录音键", "F5", "0x3F", true, true, "suppress", ""));
            mappings.Add(BridgeMapping("back", "返回键（Windows 未上报）", "BrowserBack", "", false, false, "passthrough", "browserback"));
            mappings.Add(ConfiguredMapping("home", "Home 键", "Home", "0x47", GetMapping("Home", "win+d"), "home"));
            mappings.Add(ConfiguredMapping("tv", "TV 键", "Oemtilde", "0x29", GetMapping("TV", "task-switcher"), "oemtilde"));
            mappings.Add(ConfiguredMapping("menu", "功能键", "Apps", "0x5D", GetMapping("功能键", "ctrl+shift+p"), "apps"));
            mappings.Add(ConfiguredMapping("ok", "确认键", "Enter", "0x1C", GetMapping("确认键", "enter"), "enter"));
            mappings.Add(BridgeMapping("up", "上键", "Up", "0x48", false, false, "passthrough", "up"));
            mappings.Add(BridgeMapping("down", "下键", "Down", "0x50", false, false, "passthrough", "down"));
            mappings.Add(BridgeMapping("left", "左键", "Left", "0x4B", false, false, "passthrough", "left"));
            mappings.Add(BridgeMapping("right", "右键", "Right", "0x4D", false, false, "passthrough", "right"));
            mappings.Add(BridgeMapping("volume_up", "独立音量 +（Windows 未上报）", "VolumeUp", "", false, false, "passthrough", "volumeup"));
            mappings.Add(BridgeMapping("volume_down", "独立音量 -（Windows 未上报）", "VolumeDown", "", false, false, "passthrough", "volumedown"));
            var document = new Dictionary<string, object>();
            document["version"] = 2;
            document["notes"] = "Generated by Vibe Flow. Only distinctive RC003 keys are remapped; ordinary navigation remains native by default.";
            document["mappings"] = mappings.ToArray();
            File.WriteAllText(Path.Combine(root, "voxdeck-shortcuts.json"), new JavaScriptSerializer().Serialize(document), Encoding.UTF8);
        }
        catch (Exception ex) { Log("Keyboard config sync failed: " + ex.Message); }
    }

    private Dictionary<string, object> ConfiguredMapping(string name, string label, string vk, string scan, string action, string nativeAction)
    {
        string normalized = (action ?? "").Trim().ToLowerInvariant();
        bool passthrough = normalized.Length == 0 || normalized == "passthrough" || normalized == nativeAction;
        return BridgeMapping(name, label, vk, scan, !passthrough, !passthrough, passthrough ? "passthrough" : "tap", passthrough ? nativeAction : action);
    }

    private static Dictionary<string, object> BridgeMapping(string name, string label, string vk, string scan, bool enabled, bool suppress, string mode, string shortcut)
    {
        var value = new Dictionary<string, object>();
        value["name"] = name;
        value["label"] = label;
        value["vk"] = vk;
        value["scan"] = scan;
        value["enabled"] = enabled;
        value["suppress"] = suppress;
        value["mode"] = mode;
        value["shortcut"] = shortcut;
        return value;
    }

    private string GetMapping(string key, string fallback)
    {
        if (config.mappings != null && config.mappings.ContainsKey(key)) return config.mappings[key];
        return fallback;
    }

    private static List<ShortcutChoice> ShortcutChoicesFor(string key, string current)
    {
        var choices = new List<ShortcutChoice>();
        if (key == "录音键") choices.Add(new ShortcutChoice("由言灵管理", "managed"));
        else if (key == "上 / 下 / 左 / 右") choices.Add(new ShortcutChoice("短按方向 · 长按上下调音量", "direction-volume-fallback"));
        else
        {
            choices.Add(new ShortcutChoice("确认 / 换行", "enter"));
            choices.Add(new ShortcutChoice("复制", "ctrl+c"));
            choices.Add(new ShortcutChoice("剪切", "ctrl+x"));
            choices.Add(new ShortcutChoice("粘贴", "ctrl+v"));
            choices.Add(new ShortcutChoice("撤销", "ctrl+z"));
            choices.Add(new ShortcutChoice("重做", "ctrl+shift+z"));
            choices.Add(new ShortcutChoice("命令面板", "ctrl+shift+p"));
            choices.Add(new ShortcutChoice("查找", "ctrl+f"));
            if (key == "TV") choices.Add(new ShortcutChoice("任务切换器（左右选择）", "task-switcher"));
            choices.Add(new ShortcutChoice("快速切换应用", "alt+tab"));
            choices.Add(new ShortcutChoice("切换标签页", "ctrl+tab"));
            choices.Add(new ShortcutChoice("显示桌面", "win+d"));
            choices.Add(new ShortcutChoice("返回上一页", "alt+left"));
            choices.Add(new ShortcutChoice("Esc / 取消", "escape"));
        }
        bool found = false;
        foreach (ShortcutChoice choice in choices) if (choice.Shortcut.Equals(current ?? "", StringComparison.OrdinalIgnoreCase)) found = true;
        if (!found && !string.IsNullOrWhiteSpace(current)) choices.Add(new ShortcutChoice("自定义 · " + current, current));
        return choices;
    }

    private static int FindShortcutChoice(List<ShortcutChoice> choices, string shortcut)
    {
        for (int i = 0; i < choices.Count; i++)
            if (choices[i].Shortcut.Equals(shortcut ?? "", StringComparison.OrdinalIgnoreCase)) return i;
        return 0;
    }

    private void ApplyMappingPreset(string preset)
    {
        if (config.mappings == null) config.mappings = new Dictionary<string, string>();
        config.mappingPreset = preset;
        config.mappings["确认键"] = "enter";
        config.mappings["上 / 下 / 左 / 右"] = "direction-volume-fallback";

        if (preset == "editing")
        {
            config.mappings["Home"] = "ctrl+z";
            config.mappings["TV"] = "task-switcher";
            config.mappings["功能键"] = "ctrl+v";
        }
        else if (preset == "review")
        {
            config.mappings["Home"] = "ctrl+f";
            config.mappings["TV"] = "task-switcher";
            config.mappings["功能键"] = "ctrl+shift+p";
        }
        else
        {
            config.mappings["Home"] = "win+d";
            config.mappings["TV"] = "task-switcher";
            config.mappings["功能键"] = "ctrl+shift+p";
        }
    }

    private void SetMapping(string key, string value)
    {
        if (config.mappings == null) config.mappings = new Dictionary<string, string>();
        config.mappings[key] = value;
    }

    private void ExportConfig()
    {
        var dialog = new SaveFileDialog();
        dialog.Filter = "JSON 配置|*.json";
        dialog.FileName = "vibe-flow-config.json";
        if (dialog.ShowDialog() == DialogResult.OK) File.Copy(configPath, dialog.FileName, true);
    }

    private void ExportDiagnostics()
    {
        try
        {
            var dialog = new SaveFileDialog();
            dialog.Filter = "诊断文本|*.txt";
            dialog.FileName = "vibe-flow-diagnostics-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt";
            if (dialog.ShowDialog() != DialogResult.OK) return;

            var report = new StringBuilder();
            report.AppendLine("Vibe Flow diagnostics");
            report.AppendLine("Generated: " + DateTime.Now.ToString("o"));
            report.AppendLine("Windows: " + Environment.OSVersion.VersionString);
            report.AppendLine("App: " + Application.ProductVersion);
            report.AppendLine("Capture running: " + IsCapturing);
            report.AppendLine("Audio endpoint: " + config.audioEndpointName);
            report.AppendLine("Input method: " + config.inputMethod + " / " + config.inputMethodHotkey);
            report.AppendLine("Mappings: " + new JavaScriptSerializer().Serialize(config.mappings));
            report.AppendLine();
            AppendLogTail(report, Path.Combine(sessionDir, "vibe-mic-runtime.log"), "Runtime log", 200);
            AppendLogTail(report, Path.Combine(root, "input-bridge-log.txt"), "Input bridge log", 200);
            string captureReport = Path.Combine(sessionDir, "remote-voice-report.json");
            if (File.Exists(captureReport))
            {
                report.AppendLine("Capture report");
                report.AppendLine(File.ReadAllText(captureReport, Encoding.UTF8));
            }
            File.WriteAllText(dialog.FileName, report.ToString(), new UTF8Encoding(false));
            Toast("诊断已导出，不包含录音和蓝牙设备路径");
        }
        catch (Exception ex) { Toast("诊断导出失败"); Log("Diagnostics export failed: " + ex.Message); }
    }

    private static void AppendLogTail(StringBuilder output, string path, string title, int maximumLines)
    {
        output.AppendLine(title);
        if (!File.Exists(path))
        {
            output.AppendLine("Not available");
            output.AppendLine();
            return;
        }
        string[] lines = File.ReadAllLines(path, Encoding.UTF8);
        int start = Math.Max(0, lines.Length - maximumLines);
        for (int i = start; i < lines.Length; i++) output.AppendLine(lines[i]);
        output.AppendLine();
    }

    private Image LoadBrandLogo()
    {
        try
        {
            using (Image source = Image.FromFile(brandLogoPath)) return new Bitmap(source);
        }
        catch
        {
            var fallback = new Bitmap(64, 64);
            using (Graphics graphics = Graphics.FromImage(fallback))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);
                using (var brush = new SolidBrush(violet)) graphics.FillRoundedRectangle(brush, new Rectangle(2, 2, 60, 60), 12);
                using (var font = new Font("Segoe UI", 18f, FontStyle.Bold))
                using (var brush = new SolidBrush(Color.White)) graphics.DrawString("VF", font, brush, 10, 16);
            }
            return fallback;
        }
    }

    private Icon CreateAppIcon()
    {
        using (Image source = LoadBrandLogo())
        using (var bitmap = new Bitmap(32, 32))
        {
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawImage(source, new Rectangle(0, 0, 32, 32));
            }
            IntPtr handle = bitmap.GetHicon();
            try { return (Icon)Icon.FromHandle(handle).Clone(); }
            finally { DestroyIcon(handle); }
        }
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);

    [DllImport("winmm.dll")]
    private static extern uint waveOutGetNumDevs();

    [DllImport("winmm.dll", CharSet = CharSet.Auto)]
    private static extern uint waveOutGetDevCaps(UIntPtr deviceId, out WaveOutCaps caps, uint size);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct WaveOutCaps
    {
        public ushort manufacturerId, productId;
        public uint driverVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string name;
        public uint formats;
        public ushort channels, reserved;
        public uint support;
    }

    private sealed class VibeMicConfig
    {
        public int schemaVersion { get; set; }
        public int captureSeconds { get; set; }
        public double gain { get; set; }
        public string voiceMode { get; set; }
        public bool setupCompleted { get; set; }
        public bool launchAtStartup { get; set; }
        public bool startBridgeOnLaunch { get; set; }
        public bool minimizeToTray { get; set; }
        public string audioEndpointName { get; set; }
        public string inputMethod { get; set; }
        public string inputMethodHotkey { get; set; }
        public int drainMs { get; set; }
        public string mappingPreset { get; set; }
        public Dictionary<string, string> mappings { get; set; }

        public static VibeMicConfig Default()
        {
            var c = new VibeMicConfig();
            c.schemaVersion = 8;
            c.captureSeconds = 0;
            c.gain = 1.0;
            c.voiceMode = "hold";
            c.setupCompleted = false;
            c.launchAtStartup = false;
            c.startBridgeOnLaunch = false;
            c.minimizeToTray = true;
            c.audioEndpointName = "CABLE Input";
            c.inputMethod = "wechat";
            c.inputMethodHotkey = "ctrl+win";
            c.drainMs = 180;
            c.mappingPreset = "coding";
            c.mappings = new Dictionary<string, string>();
            c.mappings["确认键"] = "enter";
            c.mappings["Home"] = "win+d";
            c.mappings["TV"] = "task-switcher";
            c.mappings["功能键"] = "ctrl+shift+p";
            c.mappings["上 / 下 / 左 / 右"] = "direction-volume-fallback";
            return c;
        }
    }

    private sealed class ShortcutChoice
    {
        public readonly string Label;
        public readonly string Shortcut;
        public ShortcutChoice(string label, string shortcut) { Label = label; Shortcut = shortcut; }
        public override string ToString() { return Label; }
    }
}

internal sealed class RoundPanel : Panel
{
    public int Radius = 8;
    public Color BorderColor = Color.FromArgb(226, 230, 242);
    public RoundPanel() { DoubleBuffered = true; ResizeRedraw = true; }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var path = RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), Radius))
        using (var pen = new Pen(BorderColor)) e.Graphics.DrawPath(pen, path);
    }
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        using (var path = RoundedRect(new Rectangle(0, 0, Width, Height), Radius)) Region = new Region(path);
    }
    private static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        int d = radius * 2;
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}

internal sealed class RemoteVisual : Control
{
    public Color AccentColor = Color.FromArgb(126, 139, 174);
    public bool IsActive;
    public bool IsRecording;
    public float AnimationPhase;
    public RemoteVisual() { DoubleBuffered = true; }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        float scale = Math.Min(1f, Math.Max(0.68f, (Height - 12f) / 296f));
        int bodyWidth = (int)(86 * scale);
        int x = Width / 2 - bodyWidth / 2;
        Func<int, int> sx = delegate(int value) { return x + (int)(value * scale); };
        Func<int, int> sy = delegate(int value) { return 7 + (int)(value * scale); };
        Func<int, int> sr = delegate(int value) { return Math.Max(5, (int)(value * scale)); };
        var body = new Rectangle(x, 7, bodyWidth, Math.Min(Height - 12, (int)(288 * scale)));
        if (IsActive)
        {
            float wave = (float)((Math.Sin(AnimationPhase) + 1.0) / 2.0);
            for (int i = 2; i >= 0; i--)
            {
                int spread = 10 + i * 12 + (int)(wave * (IsRecording ? 12 : 5));
                int alpha = Math.Max(10, (IsRecording ? 52 : 32) - i * 10);
                using (var glow = new Pen(Color.FromArgb(alpha, AccentColor), IsRecording ? 3f : 2f))
                    e.Graphics.DrawRoundedRectangle(glow, new Rectangle(body.X - spread / 2, body.Y - spread / 2, body.Width + spread, body.Height + spread), 12 + spread / 3);
            }
        }
        using (var shadow = new SolidBrush(Color.FromArgb(24, 50, 40, 120))) e.Graphics.FillRectangle(shadow, x + sr(8), 12, bodyWidth, body.Height);
        using (var brush = new LinearGradientBrush(body, Color.FromArgb(245, 246, 248), Color.FromArgb(190, 193, 202), 0f))
            e.Graphics.FillRoundedRectangle(brush, body, 10);
        DrawButton(e.Graphics, sx(20), sy(24), sr(13), "");
        DrawButton(e.Graphics, sx(66), sy(24), sr(13), "");
        if (IsActive)
        {
            int micAlpha = IsRecording ? 235 : 150;
            using (var micGlow = new SolidBrush(Color.FromArgb(micAlpha, AccentColor))) e.Graphics.FillEllipse(micGlow, sx(34), sy(14), sr(18), sr(18));
            using (var micCore = new SolidBrush(Color.White)) e.Graphics.FillEllipse(micCore, sx(40), sy(19), sr(6), sr(9));
        }
        using (var b = new SolidBrush(Color.FromArgb(42, 43, 47))) e.Graphics.FillEllipse(b, sx(13), sy(54), sr(60), sr(60));
        using (var p = new Pen(Color.FromArgb(90, 91, 96), 1.5f)) e.Graphics.DrawEllipse(p, sx(27), sy(68), sr(32), sr(32));
        DrawButton(e.Graphics, sx(23), sy(134), sr(14), "‹");
        DrawButton(e.Graphics, sx(63), sy(134), sr(14), "+");
        DrawButton(e.Graphics, sx(23), sy(170), sr(14), "⌂");
        DrawButton(e.Graphics, sx(63), sy(170), sr(14), "−");
        DrawButton(e.Graphics, sx(23), sy(206), sr(14), "≡");
        DrawButton(e.Graphics, sx(63), sy(206), sr(14), "□");
        using (var font = new Font("Segoe UI", 6.5f, FontStyle.Bold))
        using (var b = new SolidBrush(Color.FromArgb(70, 73, 82)))
            e.Graphics.DrawString("XIAOMI", font, b, sx(27), sy(262));
    }
    private static void DrawButton(Graphics g, int cx, int cy, int r, string text)
    {
        using (var b = new SolidBrush(Color.FromArgb(47, 48, 52))) g.FillEllipse(b, cx - r, cy - r, r * 2, r * 2);
        if (!string.IsNullOrEmpty(text))
        {
            using (var f = new Font("Segoe UI Symbol", 10f, FontStyle.Bold))
            using (var b = new SolidBrush(Color.White))
            {
                var s = g.MeasureString(text, f);
                g.DrawString(text, f, b, cx - s.Width / 2, cy - s.Height / 2);
            }
        }
    }
}

internal static class GraphicsExtensions
{
    public static void DrawRoundedRectangle(this Graphics graphics, Pen pen, Rectangle rect, int radius)
    {
        int d = radius * 2;
        using (var path = new GraphicsPath())
        {
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            graphics.DrawPath(pen, path);
        }
    }

    public static void FillRoundedRectangle(this Graphics graphics, Brush brush, Rectangle rect, int radius)
    {
        int d = radius * 2;
        using (var path = new GraphicsPath())
        {
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            graphics.FillPath(brush, path);
        }
    }
}
