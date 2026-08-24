using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Globalization;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: System.Reflection.AssemblyTitle("Vibe Flow Remote")]
[assembly: System.Reflection.AssemblyProduct("言灵 · Vibe Flow Remote")]
[assembly: System.Reflection.AssemblyCompany("Vibe Flow Contributors")]
[assembly: System.Reflection.AssemblyVersion("1.0.3.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.3.0")]
[assembly: System.Reflection.AssemblyInformationalVersion("1.0.3")]

internal sealed class VibeMicForm : Form
{
    private const string DisplayProductName = "言灵 · Vibe Flow Remote";
    private const string ProductRelease = "1.0.3";
    private const int ConfigSchemaVersion = 15;
    private const int CurrentOnboardingVersion = 3;
    private const int StableVoiceProfileVersion = 11;
    private const double StableVoiceGain = 1.0;
    private const int StableVoiceDrainMs = 180;
    private const string StableVoiceEndpoint = "CABLE Input";
    private const string StableVoiceProcessing = "speech";
    private readonly string root = AppDomain.CurrentDomain.BaseDirectory;
    private readonly string sessionDir;
    private readonly string configPath;
    private readonly string eventsPath;
    private readonly string brandLogoPath;
    private readonly string hostLogPath;
    private readonly Color ink = Color.FromArgb(18, 30, 54);
    private readonly Color muted = Color.FromArgb(91, 104, 134);
    private readonly Color violet = Color.FromArgb(104, 82, 244);
    private readonly Color green = Color.FromArgb(10, 164, 104);
    private readonly Color amber = Color.FromArgb(229, 151, 39);
    private readonly Color cyan = Color.FromArgb(0, 153, 190);
    private readonly Color coral = Color.FromArgb(204, 70, 82);
    private readonly Color line = Color.FromArgb(220, 226, 239);
    private readonly Panel content = new Panel();
    private readonly List<Button> navButtons = new List<Button>();
    private readonly Label[] overviewStatusValues = new Label[5];
    private readonly Label[] overviewStatusGlyphs = new Label[5];
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
    private EventWaitHandle exitApplicationEvent;
    private EventWaitHandle voiceWakeRequestEvent;
    private VibeMicConfig config;
    private System.Windows.Forms.Timer activityTimer;
    private System.Windows.Forms.Timer reconnectTimer;
    private System.Windows.Forms.Timer visualTimer;
    private long lastEventLength;
    private int reconnectAttempt;
    private int startupRecoveryCount;
    private bool captureStopping;
    private bool applicationExiting;
    private bool providerWarmupActive;
    private int providerWarmupLaunchRequested;
    private readonly object providerWarmupLock = new object();
    private DateTime captureStartedAt = DateTime.MinValue;
    private bool setupWizardOpen;
    private bool bridgeReady;
    private DateTime activeStreamStarted = DateTime.MinValue;
    private RoundPanel toastPanel;
    private Label toastIcon;
    private Label toastLabel;
    private System.Windows.Forms.Timer toastTimer;
    private SoundPlayer dictationCompletePlayer;
    private SoundPlayer dictationErrorPlayer;
    private MemoryStream dictationCompleteSound;
    private MemoryStream dictationErrorSound;
    private long runtimeFeedbackPosition;
    private long inputFeedbackPosition;
    private int lastFeedbackGeneration;
    private int currentPageIndex;
    private DateTime remoteHighlightUntil = DateTime.MinValue;
    private DateTime transientFeedbackUntil = DateTime.MinValue;
    private string transientFeedbackState = "";
    private string transientFeedbackText = "";
    private Color currentVisualAccent = Color.FromArgb(15, 158, 100);
    private string currentVisualState = "connecting";

    [STAThread]
    private static void Main(string[] args)
    {
        bool background = Array.Exists(args, delegate(string arg) { return arg.Equals("--background", StringComparison.OrdinalIgnoreCase); });
        bool createdNew;
        using (var instance = new Mutex(true, "Local\\VibeMic", out createdNew))
        {
            if (!createdNew)
            {
                bool replaceExisting = ExistingInstanceUsesDifferentPath();
                if (replaceExisting)
                {
                    SignalEvent("Local\\VibeMicExitForUpdate");
                    try { createdNew = instance.WaitOne(12000, false); }
                    catch (AbandonedMutexException) { createdNew = true; }
                }
                if (!createdNew)
                {
                    if (!background) SignalEvent("Local\\VibeMicShowWindow");
                    return;
                }
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new VibeMicForm(background));
        }
    }

    private static bool ExistingInstanceUsesDifferentPath()
    {
        try
        {
            string current = Path.GetFullPath(Application.ExecutablePath);
            string[] names = { "VibeFlow", "VibeMic" };
            foreach (string name in names)
            {
                foreach (Process process in Process.GetProcessesByName(name))
                {
                    try
                    {
                        if (process.Id == Process.GetCurrentProcess().Id) continue;
                        string other = Path.GetFullPath(process.MainModule.FileName);
                        if (!other.Equals(current, StringComparison.OrdinalIgnoreCase)) return true;
                    }
                    catch { }
                    finally { process.Dispose(); }
                }
            }
        }
        catch { }
        return false;
    }

    private VibeMicForm(bool launchInBackground)
    {
        backgroundLaunch = launchInBackground;
        sessionDir = Path.Combine(root, "remote-voice-session");
        configPath = Path.Combine(root, "vibe-mic-config.json");
        eventsPath = Path.Combine(sessionDir, "remote-voice-events.jsonl");
        brandLogoPath = Path.Combine(root, "vibe-flow-logo.png");
        hostLogPath = Path.Combine(sessionDir, "vibe-flow-host.log");
        Directory.CreateDirectory(sessionDir);
        config = LoadConfig();
        if (config.launchAtStartup) SetLaunchAtStartup(true);
        ReleaseVoiceHotkey();
        RotateLogFile(Path.Combine(sessionDir, "vibe-mic-runtime.log"), 4 * 1024 * 1024);
        RotateLogFile(hostLogPath, 2 * 1024 * 1024);
        RotateLogFile(Path.Combine(root, "input-bridge-log.txt"), 4 * 1024 * 1024);
        InitializeFeedbackSounds();
        string existingRuntimeLog = Path.Combine(sessionDir, "vibe-mic-runtime.log");
        runtimeFeedbackPosition = File.Exists(existingRuntimeLog) ? new FileInfo(existingRuntimeLog).Length : 0;
        string existingInputLog = Path.Combine(root, "input-bridge-log.txt");
        inputFeedbackPosition = File.Exists(existingInputLog) ? new FileInfo(existingInputLog).Length : 0;

        Text = DisplayProductName + " · V1";
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
        BackColor = Color.FromArgb(245, 247, 251);
        Font = new Font("Microsoft YaHei UI", 10f);
        Icon = CreateAppIcon();
        DoubleBuffered = true;

        BuildShell();
        ShowPage(0);
        SetupTray();
        showWindowEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\VibeMicShowWindow");
        exitApplicationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\VibeMicExitForUpdate");
        voiceWakeRequestEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\VibeMicVoiceWakeRequested");
        HostLog("HOST START mode=" + (backgroundLaunch ? "background" : "interactive") +
            " provider=" + NormalizeProviderKey(config.inputMethod) + " startup=" + config.launchAtStartup);
        ThreadPool.QueueUserWorkItem(delegate
        {
            try
            {
                WaitHandle[] handles = { showWindowEvent, exitApplicationEvent, voiceWakeRequestEvent };
                while (true)
                {
                    int signal = WaitHandle.WaitAny(handles);
                    if (IsDisposed || applicationExiting) return;
                    if (signal == 0) BeginInvoke(new Action(ShowMainWindow));
                    else if (signal == 1) BeginInvoke(new Action(delegate { config.minimizeToTray = false; Close(); }));
                    else BeginInvoke(new Action(HandleVoiceWakeRequest));
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
            if (heroPanel != null && !heroPanel.IsDisposed &&
                (currentVisualState == "recording" || currentVisualState == "processing" || currentVisualState == "connecting"))
                heroPanel.Invalidate();
        };
        visualTimer.Start();
    }

    private void BuildShell()
    {
        var sidebar = new Panel();
        sidebar.Dock = DockStyle.Left;
        sidebar.Width = 232;
        sidebar.BackColor = Color.FromArgb(249, 251, 255);
        sidebar.Paint += delegate(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            for (int y = 98; y < sidebar.Height - 92; y += 26)
            {
                using (var dot = new SolidBrush(Color.FromArgb(18, 101, 92, 255)))
                    e.Graphics.FillEllipse(dot, 10, y, 2, 2);
            }
            using (var accent = new Pen(Color.FromArgb(105, violet), 2f))
                e.Graphics.DrawLine(accent, 0, 0, 0, Math.Min(210, sidebar.Height));
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
        var sub = NewLabel("VIBE FLOW REMOTE · V1", 7.4f, FontStyle.Bold, violet);
        sub.Location = new Point(84, 58);
        sub.AutoSize = true;

        string[] navText = { "总览", "语音听写", "按键快捷方式", "连接与自检", "偏好设置" };
        string[] navIcon = { "overview", "voice", "shortcuts", "diagnostics", "settings" };
        for (int i = 0; i < navText.Length; i++)
        {
            int page = i;
            var button = new Button();
            button.Text = navText[i];
            button.Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Regular);
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.Image = CreateNavigationIcon(navIcon[i], muted, false);
            button.ImageAlign = ContentAlignment.MiddleLeft;
            button.TextImageRelation = TextImageRelation.ImageBeforeText;
            button.Padding = new Padding(18, 0, 10, 0);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(239, 242, 250);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(229, 234, 247);
            button.BackColor = Color.Transparent;
            button.ForeColor = ink;
            button.Tag = navIcon[i];
            button.AccessibleName = navText[i];
            button.Location = new Point(18, 120 + i * 58);
            button.Size = new Size(196, 48);
            button.Cursor = Cursors.Hand;
            button.Click += delegate { ShowPage(page); };
            button.Paint += delegate(object sender, PaintEventArgs e)
            {
                if (currentPageIndex != page) return;
                using (var brush = new SolidBrush(violet))
                    e.Graphics.FillRoundedRectangle(brush, new Rectangle(1, 14, 3, 20), 1);
            };
            ApplyRoundedRegion(button, 7);
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
        ApplyRoundedRegion(connectionBadge, 7);

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
        content.BackColor = Color.FromArgb(245, 247, 251);
        content.AutoScroll = true;
        content.AutoScrollMinSize = new Size(1030, 744);
        content.Paint += PaintWorkspaceTexture;
        Controls.Add(content);
        Controls.Add(sidebar);
        BuildToastOverlay();
    }

    private void PaintWorkspaceTexture(object sender, PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var dot = new SolidBrush(Color.FromArgb(22, 109, 121, 155)))
        {
            for (int y = 18; y < content.Height; y += 32)
                for (int x = 18; x < content.Width; x += 32)
                    e.Graphics.FillEllipse(dot, x, y, 1.4f, 1.4f);
        }
        using (var cyanLine = new Pen(Color.FromArgb(25, cyan), 1f))
        using (var violetLine = new Pen(Color.FromArgb(18, violet), 1f))
        {
            e.Graphics.DrawLine(cyanLine, Math.Max(0, content.Width - 260), 0, content.Width, 0);
            e.Graphics.DrawLine(violetLine, Math.Max(0, content.Width - 160), 4, content.Width, 4);
        }
    }

    private static void ApplyRoundedRegion(Control control, int radius)
    {
        if (control == null || control.Width <= 0 || control.Height <= 0) return;
        Region previous = control.Region;
        using (GraphicsPath path = RoundedControlPath(new Rectangle(0, 0, control.Width, control.Height), radius))
            control.Region = new Region(path);
        if (previous != null) previous.Dispose();
    }

    private void BuildToastOverlay()
    {
        toastPanel = new RoundPanel();
        toastPanel.Size = new Size(420, 58);
        toastPanel.Location = new Point(Math.Max(240, ClientSize.Width - 444), Math.Max(80, ClientSize.Height - 82));
        toastPanel.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
        toastPanel.BackColor = Color.White;
        toastPanel.BorderColor = Color.FromArgb(203, 211, 231);
        toastPanel.Radius = 8;
        toastPanel.Visible = false;

        toastIcon = NewLabel("\uE73E", 12f, FontStyle.Regular, green);
        toastIcon.Font = new Font("Segoe MDL2 Assets", 12f);
        toastIcon.Location = new Point(16, 14);
        toastIcon.Size = new Size(28, 28);
        toastIcon.TextAlign = ContentAlignment.MiddleCenter;
        toastLabel = NewLabel("", 9.3f, FontStyle.Bold, ink);
        toastLabel.Location = new Point(50, 12);
        toastLabel.Size = new Size(350, 34);
        toastLabel.TextAlign = ContentAlignment.MiddleLeft;
        toastLabel.AutoEllipsis = true;
        toastPanel.Controls.Add(toastIcon);
        toastPanel.Controls.Add(toastLabel);
        Controls.Add(toastPanel);
        toastPanel.BringToFront();

        toastTimer = new System.Windows.Forms.Timer();
        toastTimer.Interval = 2800;
        toastTimer.Tick += delegate
        {
            toastTimer.Stop();
            if (toastPanel != null) toastPanel.Visible = false;
        };
    }

    private void ShowPage(int index)
    {
        currentPageIndex = Math.Max(0, Math.Min(4, index));
        for (int i = 0; i < navButtons.Count; i++)
        {
            navButtons[i].BackColor = i == currentPageIndex ? Color.FromArgb(233, 237, 255) : Color.Transparent;
            navButtons[i].ForeColor = i == currentPageIndex ? violet : ink;
            navButtons[i].Font = new Font("Microsoft YaHei UI", 10f, i == currentPageIndex ? FontStyle.Bold : FontStyle.Regular);
            Image previousIcon = navButtons[i].Image;
            navButtons[i].Image = CreateNavigationIcon(navButtons[i].Tag as string, i == currentPageIndex ? violet : muted, i == currentPageIndex);
            if (previousIcon != null) previousIcon.Dispose();
            ApplyRoundedRegion(navButtons[i], 7);
            navButtons[i].Invalidate();
        }
        content.SuspendLayout();
        content.AutoScrollPosition = Point.Empty;
        content.Controls.Clear();
        if (currentPageIndex == 0) BuildOverview();
        else if (currentPageIndex == 1) BuildVoicePage();
        else if (currentPageIndex == 2) BuildMappingsPage();
        else if (currentPageIndex == 3) BuildDevicePage();
        else BuildSettingsPage();
        content.ResumeLayout();
        ActiveControl = null;
    }

    private void BuildOverview()
    {
        AddPageTitle("总览", "遥控器状态与常用操作");

        var hero = NewCard(new Point(34, 92), new Size(960, 322));
        heroPanel = hero;
        hero.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        hero.Paint += PaintHeroSurface;

        heroStateLabel = NewLabel(IsCapturing ? "VOICE LINK" : "VOICE LINK OFF", 8.5f, FontStyle.Bold, violet);
        heroStateLabel.Location = new Point(52, 34);
        heroStateLabel.AutoSize = true;
        heroTitle = NewLabel(IsCapturing ? "正在连接" : "语音桥接已暂停", 27f, FontStyle.Bold, ink);
        heroTitle.Location = new Point(50, 62);
        heroTitle.AutoSize = true;
        heroSubtitle = NewLabel(IsCapturing ? "正在建立遥控器语音通道，请稍候" : "启动后，按住遥控器录音键即可在当前输入框听写", 10.5f, FontStyle.Regular, muted);
        heroSubtitle.Location = new Point(52, 111);
        heroSubtitle.Size = new Size(560, 30);

        string[,] linkFacts = {
            { "●", "RC003 遥控器" },
            { "●", ProviderDisplayName(config.inputMethod) },
            { "●", "稳定语音档案 v" + StableVoiceProfileVersion }
        };
        Color[] linkColors = { violet, cyan, green };
        int[] linkWidths = { 132, 154, 176 };
        int factX = 52;
        for (int i = 0; i < linkFacts.GetLength(0); i++)
        {
            var fact = NewLabel(linkFacts[i, 0] + "  " + linkFacts[i, 1], 8.7f, FontStyle.Bold, linkColors[i]);
            fact.Location = new Point(factX, 158);
            fact.Size = new Size(linkWidths[i], 25);
            factX += linkWidths[i] + 12;
            hero.Controls.Add(fact);
        }

        bridgeButton = PrimaryButton(IsCapturing ? "管理语音桥接" : "启动语音桥接", new Point(52, 217), new Size(152, 44));
        bridgeButton.Click += delegate
        {
            if (IsCapturing) ShowPage(1);
            else ToggleCapture();
        };
        var scan = SecondaryButton("检查连接", new Point(216, 217), new Size(124, 44));
        scan.Click += delegate { ScanDevice(); };

        var gestureHint = NewLabel("按住说话  ·  松开后交给转写工具整理", 8.7f, FontStyle.Regular, muted);
        gestureHint.Location = new Point(52, 276);
        gestureHint.Size = new Size(420, 24);

        remoteVisual = new RemoteVisual();
        remoteVisual.Location = new Point(688, 4);
        remoteVisual.Size = new Size(246, 314);
        remoteVisual.Anchor = AnchorStyles.Top | AnchorStyles.Right;

        hero.Controls.Add(heroStateLabel);
        hero.Controls.Add(heroTitle);
        hero.Controls.Add(heroSubtitle);
        hero.Controls.Add(bridgeButton);
        hero.Controls.Add(scan);
        hero.Controls.Add(gestureHint);
        hero.Controls.Add(remoteVisual);

        var flow = NewCard(new Point(34, 430), new Size(470, 178));
        flow.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        flow.Controls.Add(SectionTitle("开始一次听写", "\uE720", new Point(24, 18)));
        string[] steps = { "按住录音键", "说出内容", "自动回填文字" };
        string[] icons = { "\uE720", "\uE9D2", "\uE724" };
        for (int i = 0; i < 3; i++)
        {
            int x = 40 + i * 140;
            var circle = new RoundPanel();
            circle.Location = new Point(x, 52);
            circle.Size = new Size(48, 48);
            circle.Radius = 24;
            circle.BackColor = i == 0 ? Color.FromArgb(237, 235, 255) : Color.FromArgb(246, 249, 253);
            circle.BorderColor = i == 0 ? Color.FromArgb(209, 204, 255) : line;
            var glyph = NewLabel(icons[i], 15f, FontStyle.Regular, i == 1 ? cyan : violet);
            glyph.Font = new Font("Segoe MDL2 Assets", 15f, FontStyle.Regular);
            glyph.Dock = DockStyle.Fill;
            glyph.TextAlign = ContentAlignment.MiddleCenter;
            circle.Controls.Add(glyph);
            flow.Controls.Add(circle);
            var label = NewLabel(steps[i], 9f, FontStyle.Regular, muted);
            label.Location = new Point(x - 24, 105);
            label.Size = new Size(96, 22);
            label.TextAlign = ContentAlignment.MiddleCenter;
            flow.Controls.Add(label);
            if (i < 2)
            {
                var connector = NewLabel("···", 9f, FontStyle.Regular, Color.FromArgb(165, 179, 207));
                connector.Location = new Point(x + 75, 65);
                connector.Size = new Size(34, 20);
                connector.TextAlign = ContentAlignment.MiddleCenter;
                flow.Controls.Add(connector);
            }
        }
        activityLabel = NewLabel("已就绪，等待按下录音键", 9.5f, FontStyle.Bold, muted);
        activityLabel.Location = new Point(24, 142);
        activityLabel.Size = new Size(420, 22);
        activityLabel.TextAlign = ContentAlignment.MiddleCenter;
        flow.Controls.Add(activityLabel);

        var shortcuts = NewCard(new Point(520, 430), new Size(474, 178));
        shortcuts.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        shortcuts.Controls.Add(SectionTitle("常用按键", "\uE765", new Point(24, 18)));
        string[,] quick = {
            { "录音", "按住听写" }, { "确认", "确认 / 发送" },
            { "Home", "显示桌面" }, { "TV", "任务切换" },
            { "功能键", "打开客户端" }, { "方向键", "导航 / 调音量" }
        };
        for (int i = 0; i < quick.GetLength(0); i++)
        {
            int column = i % 2;
            int row = i / 2;
            int x = 24 + column * 224;
            int y = 50 + row * 39;
            var chip = NewLabel("●", 7f, FontStyle.Bold, i == 0 ? violet : i < 4 ? cyan : green);
            chip.Location = new Point(x, y + 2);
            chip.Size = new Size(18, 24);
            chip.TextAlign = ContentAlignment.MiddleCenter;
            var key = NewLabel(quick[i, 0], 9.5f, FontStyle.Bold, ink);
            key.Location = new Point(x + 24, y);
            key.Size = new Size(68, 24);
            var value = NewLabel(quick[i, 1], 9f, FontStyle.Regular, muted);
            value.Location = new Point(x + 94, y);
            value.Size = new Size(120, 24);
            shortcuts.Controls.Add(chip);
            shortcuts.Controls.Add(key);
            shortcuts.Controls.Add(value);
        }

        var status = NewCard(new Point(34, 624), new Size(960, 86));
        status.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        SessionHealth latestHealth = GetLatestSessionHealth();
        string[] statusNames = { "蓝牙", "遥控器麦克风", "语音数据", "转写工具", "隐私保护" };
        string[] statusValues = {
            bridgeReady ? "已连接" : IsCapturing ? "连接中" : "待连接",
            bridgeReady ? "已接入" : "等待接入",
            latestHealth.Success ? "最近一次正常" : latestHealth.Started ? "需要检查" : "等待首次听写",
            ProviderDisplayName(config.inputMethod),
            "不读取文字"
        };
        bool[] statusReady = { bridgeReady, bridgeReady, latestHealth.Success, IsProviderRunning(config.inputMethod), true };
        for (int i = 0; i < statusNames.Length; i++)
        {
            int x = 18 + i * 188;
            Color statusColor = statusReady[i] ? (i == 4 ? green : i < 2 ? violet : cyan) : amber;
            var glyph = NewLabel(i == 0 ? "\uE702" : i == 4 ? "\uEA18" : "●", 12f, FontStyle.Regular, statusColor);
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
            overviewStatusGlyphs[i] = glyph;
            overviewStatusValues[i] = value;
        }

        content.Controls.Add(hero);
        content.Controls.Add(flow);
        content.Controls.Add(shortcuts);
        content.Controls.Add(status);
        UpdateCaptureUi();
    }

    private void UpdateOverviewStatus()
    {
        if (overviewStatusValues[0] == null || overviewStatusValues[0].IsDisposed) return;
        SessionHealth latestHealth = GetLatestSessionHealth();
        string[] values = {
            bridgeReady ? "已连接" : IsCapturing ? "连接中" : "待连接",
            bridgeReady ? "已接入" : "等待接入",
            latestHealth.Success ? "最近一次正常" : latestHealth.Started ? "需要检查" : "等待首次听写",
            ProviderDisplayName(config.inputMethod),
            "不读取文字"
        };
        bool[] ready = { bridgeReady, bridgeReady, latestHealth.Success, IsProviderRunning(config.inputMethod), true };
        for (int i = 0; i < values.Length; i++)
        {
            if (overviewStatusValues[i] != null && !overviewStatusValues[i].IsDisposed) overviewStatusValues[i].Text = values[i];
            if (overviewStatusGlyphs[i] != null && !overviewStatusGlyphs[i].IsDisposed)
            {
                overviewStatusGlyphs[i].ForeColor = ready[i] ? (i == 4 ? green : i < 2 ? violet : cyan) : amber;
            }
        }
    }

    private void BuildVoicePage()
    {
        AddPageTitle("语音听写", "遥控器负责收音，所选语音工具负责转写与整理");
        var card = NewCard(new Point(34, 100), new Size(960, 590));
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
        bool stableVoiceProfile = HasStableVoiceProfile(config);
        bool advancedAudioUnlocked = !stableVoiceProfile;
        var profileBadge = NewLabel(stableVoiceProfile ? "●  稳定档案 v" + StableVoiceProfileVersion + " 已应用" : "●  参数已自定义", 8.8f, FontStyle.Bold,
            stableVoiceProfile ? green : amber);
        profileBadge.Location = new Point(650, 20);
        profileBadge.Size = new Size(225, 26);
        profileBadge.TextAlign = ContentAlignment.MiddleRight;
        stateBand.Controls.Add(voiceBridgeStateLabel);
        stateBand.Controls.Add(profileBadge);
        card.Controls.Add(stateBand);

        AddFieldLabel(card, "转写工具", 152);
        var provider = StyledCombo(new Point(220, 148), new Size(260, 38));
        provider.Items.AddRange(new object[] { "微信输入法", "Typeless", "Windows 语音输入", "Voquill（开源）", "其他语音工具" });
        provider.SelectedIndex = ProviderIndex(config.inputMethod);
        var providerStatus = NewLabel(ProviderStatusText(config.inputMethod), 9.2f, FontStyle.Bold,
            IsProviderRunning(config.inputMethod) ? green : amber);
        providerStatus.Location = new Point(505, 154);
        providerStatus.Size = new Size(300, 28);
        card.Controls.Add(provider);
        card.Controls.Add(providerStatus);

        AddFieldLabel(card, "启动快捷键", 206);
        var hotkey = StyledTextBox(config.inputMethodHotkey, new Point(220, 202), new Size(220, 34));
        var triggerMode = StyledCombo(new Point(458, 200), new Size(160, 38));
        triggerMode.Items.AddRange(new object[] { "单击切换", "按住触发" });
        triggerMode.SelectedIndex = config.inputMethodTrigger == "hold" ? 1 : 0;
        var hotkeyHelp = NewLabel("须与所选工具中的快捷键一致", 9f, FontStyle.Regular, muted);
        hotkeyHelp.Location = new Point(640, 207);
        hotkeyHelp.Size = new Size(260, 25);
        card.Controls.Add(hotkey);
        card.Controls.Add(triggerMode);
        card.Controls.Add(hotkeyHelp);

        AddFieldLabel(card, "声音处理", 260);
        var processing = StyledCombo(new Point(220, 256), new Size(260, 38));
        processing.Items.AddRange(new object[] { "清晰增强（推荐）", "原始直通" });
        processing.SelectedIndex = config.audioProcessingMode == "transparent" ? 1 : 0;
        processing.Enabled = advancedAudioUnlocked;
        var processingHelp = NewLabel(stableVoiceProfile
            ? "已锁定为真机验证的清晰增强模式。"
            : config.audioProcessingMode == "transparent"
            ? "仅做格式转换，适合排查原始音频。"
            : "稳定补偿轻声，孤立尖峰不会压低整段语音。", 9f, FontStyle.Regular, muted);
        processingHelp.Location = new Point(505, 263);
        processingHelp.Size = new Size(390, 25);
        card.Controls.Add(processing);
        card.Controls.Add(processingHelp);

        AddFieldLabel(card, "收音灵敏度", 314);
        var gainHelp = NewLabel(stableVoiceProfile
            ? "已锁定为真机验证值 1.0×；普通使用无需调整。"
            : "建议保持 1.0×；仅在排障时小幅调整。", 9.2f, FontStyle.Regular, muted);
        gainHelp.Location = new Point(220, 316);
        gainHelp.Size = new Size(520, 24);
        card.Controls.Add(gainHelp);
        var gain = new TrackBar();
        gain.Location = new Point(212, 342);
        gain.Size = new Size(390, 44);
        gain.Minimum = 5;
        gain.Maximum = 40;
        gain.Value = Math.Max(5, Math.Min(40, (int)(config.gain * 10)));
        gain.Enabled = advancedAudioUnlocked;
        var gainValue = NewLabel((gain.Value / 10.0).ToString("0.0") + "×", 10f, FontStyle.Bold, violet);
        gainValue.Location = new Point(620, 350);
        gainValue.Size = new Size(70, 28);
        gain.Scroll += delegate { gainValue.Text = (gain.Value / 10.0).ToString("0.0") + "×"; };
        card.Controls.Add(gain);
        card.Controls.Add(gainValue);

        var autoRoute = StyledCheck("听写时自动使用遥控器麦克风（推荐）", config.autoRouteVirtualMicrophone, new Point(212, 390));
        autoRoute.Size = new Size(330, 34);
        autoRoute.Enabled = advancedAudioUnlocked;
        var routeHelp = NewLabel("结束听写后自动恢复原来的 Windows 麦克风", 8.9f, FontStyle.Regular, muted);
        routeHelp.Location = new Point(548, 397);
        routeHelp.Size = new Size(350, 24);
        card.Controls.Add(autoRoute);
        card.Controls.Add(routeHelp);

        bool cableReady = HasCableInput() && HasCableOutput();
        var cableState = NewLabel(cableReady ? "●  CABLE 音频通道已就绪" : "●  需要安装或检查 VB-CABLE", 10f, FontStyle.Bold,
            cableReady ? green : Color.FromArgb(202, 76, 76));
        cableState.Location = new Point(220, 432);
        cableState.AutoSize = true;
        card.Controls.Add(cableState);

        var start = PrimaryButton(IsCapturing ? "暂停语音桥接" : "启动语音桥接", new Point(220, 470), new Size(152, 44));
        start.Click += delegate { ToggleCapture(); start.Text = IsCapturing ? "暂停语音桥接" : "启动语音桥接"; };
        var test = SecondaryButton("测试所选工具", new Point(386, 470), new Size(148, 44));
        test.Click += delegate { TestVoiceHotkey(); };
        var sound = SecondaryButton(config.inputMethod == "typeless" || config.inputMethod == "voquill" ? "获取所选工具" : "检查麦克风设置",
            new Point(548, 470), new Size(158, 44));
        sound.Click += delegate { OpenProviderHelp(config.inputMethod); };
        var profileAction = SecondaryButton(stableVoiceProfile ? "调整高级参数" : "恢复稳定参数", new Point(720, 470), new Size(170, 44));
        profileAction.Tag = stableVoiceProfile ? "unlock" : "restore";
        profileAction.Click += delegate
        {
            if (string.Equals(profileAction.Tag as string, "unlock", StringComparison.OrdinalIgnoreCase))
            {
                DialogResult confirmation = MessageBox.Show(
                    "稳定档案已经通过真机反复验证。只有在排查特殊设备问题时才建议修改声音处理、灵敏度或麦克风路由。\r\n\r\n是否继续？",
                    "调整高级音频参数", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (confirmation != DialogResult.Yes) return;
                advancedAudioUnlocked = true;
                processing.Enabled = true;
                gain.Enabled = true;
                autoRoute.Enabled = true;
                processingHelp.Text = config.audioProcessingMode == "transparent" ? "仅做格式转换，适合排查原始音频。" : "稳定补偿轻声，孤立尖峰不会压低整段语音。";
                gainHelp.Text = "建议保持 1.0×；仅在排障时小幅调整。";
                profileAction.Text = "恢复稳定参数";
                profileAction.Tag = "restore";
                ShowToast("高级参数已解锁，修改后可随时恢复稳定档案", "info");
                return;
            }
            ApplyStableVoiceProfile(config);
            SaveConfig();
            RestartCaptureForAudioSettings();
            ShowPage(1);
            ShowToast("已恢复真机验证的稳定语音参数", "success");
        };
        card.Controls.Add(start);
        card.Controls.Add(test);
        card.Controls.Add(sound);
        card.Controls.Add(profileAction);

        var note = NewLabel(ProviderRouteInstruction(config.inputMethod, config.autoRouteVirtualMicrophone) + "。言灵只转发遥控器音频，不保存录音、不读取听写文字，也不会自行上传音频。", 9.3f, FontStyle.Regular, muted);
        note.Location = new Point(30, 526);
        note.Size = new Size(880, 44);
        card.Controls.Add(note);

        bool updating = false;
        Action markProfileCustomized = delegate
        {
            profileBadge.Text = "●  参数已自定义";
            profileBadge.ForeColor = amber;
            profileAction.Text = "恢复稳定参数";
            profileAction.Tag = "restore";
        };
        gain.MouseUp += delegate
        {
            config.gain = gain.Value / 10.0;
            markProfileCustomized();
            SaveConfig();
            RestartCaptureForAudioSettings();
        };
        provider.SelectedIndexChanged += delegate
        {
            if (updating) return;
            ApplyProviderProfile(config, ProviderKeyFromIndex(provider.SelectedIndex));
            updating = true;
            hotkey.Text = config.inputMethodHotkey;
            triggerMode.SelectedIndex = config.inputMethodTrigger == "hold" ? 1 : 0;
            providerStatus.Text = ProviderStatusText(config.inputMethod);
            providerStatus.ForeColor = IsProviderRunning(config.inputMethod) ? green : amber;
            updating = false;
            SaveConfig();
            RestartCaptureForAudioSettings();
            BeginInvoke(new Action(delegate { ShowPage(1); }));
        };
        hotkey.Leave += delegate
        {
            string value = hotkey.Text.Trim().ToLowerInvariant();
            if (!IsValidTranscriptionHotkey(value))
            {
                hotkey.Text = config.inputMethodHotkey;
                Toast("快捷键格式不正确，请使用例如 ctrl+win、rightalt 或 win+h");
                return;
            }
            if (value == config.inputMethodHotkey) return;
            config.inputMethodHotkey = value;
            SaveConfig();
            RestartCaptureForAudioSettings();
        };
        triggerMode.SelectedIndexChanged += delegate
        {
            if (updating) return;
            string value = triggerMode.SelectedIndex == 1 ? "hold" : "toggle";
            if (value == config.inputMethodTrigger) return;
            config.inputMethodTrigger = value;
            SaveConfig();
            RestartCaptureForAudioSettings();
        };
        processing.SelectedIndexChanged += delegate
        {
            string value = processing.SelectedIndex == 1 ? "transparent" : "speech";
            if (value == config.audioProcessingMode) return;
            config.audioProcessingMode = value;
            config.autoLevel = value == "speech";
            processingHelp.Text = value == "transparent" ? "仅做格式转换，适合排查原始音频。" : "稳定补偿轻声，孤立尖峰不会压低整段语音。";
            markProfileCustomized();
            SaveConfig();
            RestartCaptureForAudioSettings();
        };
        autoRoute.CheckedChanged += delegate
        {
            config.autoRouteVirtualMicrophone = autoRoute.Checked;
            note.Text = ProviderRouteInstruction(config.inputMethod, config.autoRouteVirtualMicrophone) +
                "。言灵只转发遥控器音频，不保存录音、不读取听写文字，也不会自行上传音频。";
            markProfileCustomized();
            SaveConfig();
            RestartCaptureForAudioSettings();
        };

        content.Controls.Add(card);
        ApplyVisualState(!IsCapturing ? "stopped" : bridgeReady ? "ready" : "connecting");
    }

    private void BuildMappingsPage()
    {
        AddPageTitle("按键快捷方式", "选择按键功能，右侧遥控器会同步标出对应位置");
        var mappings = NewCard(new Point(34, 100), new Size(620, 610));
        mappings.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        mappings.Controls.Add(SectionTitle("快捷方式方案", "\uE765", new Point(24, 20)));

        var presetLabel = NewLabel("方案", 9f, FontStyle.Bold, muted);
        presetLabel.Location = new Point(24, 58);
        presetLabel.Size = new Size(46, 32);
        presetLabel.TextAlign = ContentAlignment.MiddleLeft;
        var preset = StyledCombo(new Point(76, 54), new Size(274, 38));
        preset.Items.AddRange(new object[] { "AI 编程（推荐）", "文本编辑", "代码阅读与评审", "自定义" });
        preset.SelectedIndex = config.mappingPreset == "editing" ? 1 : config.mappingPreset == "review" ? 2 : config.mappingPreset == "custom" ? 3 : 0;
        var applyPreset = SecondaryButton("应用方案", new Point(366, 53), new Size(116, 40));
        applyPreset.Click += delegate
        {
            if (preset.SelectedIndex == 3) { Toast("在下方选择快捷方式，修改会自动保存"); return; }
            ApplyMappingPreset(preset.SelectedIndex == 1 ? "editing" : preset.SelectedIndex == 2 ? "review" : "coding");
            SaveConfig();
            StartKeyboardBridge();
            ShowPage(2);
            Toast("按键方案已应用");
        };
        var presetHelp = NewLabel("从常用模板开始；修改任一项目后会自动切换为自定义。", 8.4f, FontStyle.Regular, muted);
        presetHelp.Location = new Point(24, 96);
        presetHelp.Size = new Size(548, 24);
        mappings.Controls.Add(presetLabel);
        mappings.Controls.Add(preset);
        mappings.Controls.Add(applyPreset);
        mappings.Controls.Add(presetHelp);

        var preview = NewCard(new Point(670, 100), new Size(324, 610));
        preview.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        preview.Controls.Add(SectionTitle("遥控器功能速查", "\uE7F4", new Point(24, 20)));
        var previewHint = NewLabel("悬停或选择左侧按键，快速定位实体位置", 8.5f, FontStyle.Regular, muted);
        previewHint.Location = new Point(24, 50);
        previewHint.Size = new Size(276, 24);
        var previewRemote = new RemoteVisual();
        previewRemote.Location = new Point(10, 72);
        previewRemote.Size = new Size(304, 370);
        previewRemote.IsActive = true;
        previewRemote.ShowCallouts = true;
        previewRemote.AccentColor = violet;
        previewRemote.HighlightedControl = "voice";
        var mappingSelection = NewLabel("录音键\r\n按住听写", 9.2f, FontStyle.Bold, violet);
        mappingSelection.Location = new Point(24, 454);
        mappingSelection.Size = new Size(276, 54);
        mappingSelection.TextAlign = ContentAlignment.MiddleCenter;
        mappingSelection.BackColor = Color.FromArgb(243, 241, 255);
        ApplyRoundedRegion(mappingSelection, 6);
        var previewHelp = NewLabel("独立音量 +/-：此型号未检测到稳定事件\r\n当前由长按 ↑ / ↓ 调节音量", 7.8f, FontStyle.Regular, amber);
        previewHelp.Location = new Point(24, 526);
        previewHelp.Size = new Size(276, 46);
        previewHelp.TextAlign = ContentAlignment.MiddleCenter;
        preview.Controls.Add(previewHint);
        preview.Controls.Add(previewRemote);
        preview.Controls.Add(mappingSelection);
        preview.Controls.Add(previewHelp);

        string[,] rows = {
            { "录音键", "managed", "系统托管 · 保持稳定语音链路" },
            { "确认键", "enter", "默认：确认或发送" },
            { "Home", "win+d", "默认：显示桌面" },
            { "TV", "task-switcher", "打开任务切换，左右选择" },
            { "功能键", "launch-client:chatgpt", "打开或切回所选客户端" },
            { "方向键", "direction-volume-fallback", "短按导航；长按上下调音量" }
        };
        string[] rowGlyphs = { "\uE720", "\uE73E", "\uE80F", "TV", "\uE765", "\uE7AD" };
        var mappingRows = new List<Panel>();
        for (int i = 0; i < rows.GetLength(0); i++)
        {
            var rowBand = new Panel();
            rowBand.Location = new Point(24, 126 + i * 62);
            rowBand.Size = new Size(572, 60);
            rowBand.BackColor = Color.White;
            rowBand.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (var divider = new Pen(Color.FromArgb(232, 236, 245)))
                    e.Graphics.DrawLine(divider, 0, rowBand.Height - 1, rowBand.Width, rowBand.Height - 1);
            };
            mappingRows.Add(rowBand);

            var icon = NewLabel(rowGlyphs[i], rowGlyphs[i] == "TV" ? 8f : 11.5f, FontStyle.Bold, i == 0 ? violet : cyan);
            icon.Font = rowGlyphs[i] == "TV" ? new Font("Segoe UI", 8f, FontStyle.Bold) : new Font("Segoe MDL2 Assets", 11.5f, FontStyle.Regular);
            icon.Location = new Point(4, 13);
            icon.Size = new Size(28, 28);
            icon.TextAlign = ContentAlignment.MiddleCenter;
            var name = NewLabel(rows[i, 0], 10f, FontStyle.Bold, ink);
            name.Location = new Point(42, 15);
            name.Size = new Size(74, 28);
            string configKey = rows[i, 0] == "方向键" ? "上 / 下 / 左 / 右" : rows[i, 0];
            string currentAction = GetMapping(configKey, rows[i, 1]);
            List<ShortcutChoice> choices = ShortcutChoicesFor(configKey, currentAction);
            var input = StyledCombo(new Point(120, 10), new Size(232, 36));
            foreach (ShortcutChoice choice in choices) input.Items.Add(choice);
            input.SelectedIndex = FindShortcutChoice(choices, currentAction);
            input.Enabled = rows[i, 0] != "录音键" && rows[i, 0] != "方向键";
            int rowIndex = i;
            string previewControl = RemoteControlForMappingKey(configKey);
            Action updatePreview = delegate
            {
                foreach (Panel mappingRow in mappingRows) mappingRow.BackColor = Color.White;
                rowBand.BackColor = Color.FromArgb(248, 247, 255);
                previewRemote.HighlightedControl = previewControl;
                ShortcutChoice selected = input.SelectedItem as ShortcutChoice;
                string selectedText = rowIndex == 0 ? "按住听写" : rowIndex == 5 ? "短按导航 · 长按上下调音量" : selected == null ? rows[rowIndex, 2] : selected.Label;
                mappingSelection.Text = rows[rowIndex, 0] + "\r\n" + selectedText;
                previewRemote.Invalidate();
            };
            rowBand.MouseEnter += delegate { updatePreview(); };
            input.Enter += delegate { updatePreview(); };
            input.MouseEnter += delegate { updatePreview(); };
            name.MouseEnter += delegate { updatePreview(); };
            icon.MouseEnter += delegate { updatePreview(); };
            input.SelectedIndexChanged += delegate
            {
                ShortcutChoice selected = input.SelectedItem as ShortcutChoice;
                if (selected == null || !input.Enabled) return;
                config.mappingPreset = "custom";
                string selectedKey = rows[rowIndex, 0] == "方向键" ? "上 / 下 / 左 / 右" : rows[rowIndex, 0];
                SetMapping(selectedKey, selected.Shortcut);
                SaveConfig();
                updatePreview();
                ShowToast(rows[rowIndex, 0] + "已设为“" + selected.Label + "”", "success");
            };
            var hint = NewLabel(rows[i, 2], 8.4f, FontStyle.Regular, muted);
            hint.Location = new Point(370, 9);
            hint.Size = new Size(194, 40);
            hint.TextAlign = ContentAlignment.MiddleLeft;
            hint.MouseEnter += delegate { updatePreview(); };
            rowBand.Controls.Add(icon);
            rowBand.Controls.Add(name);
            rowBand.Controls.Add(input);
            rowBand.Controls.Add(hint);
            mappings.Controls.Add(rowBand);
            if (i == 0) updatePreview();
        }
        var save = PrimaryButton("立即应用", new Point(144, 538), new Size(132, 42));
        save.Click += delegate { SaveConfig(); StartKeyboardBridge(); Toast("按键快捷方式已生效"); };
        var openBridge = SecondaryButton("打开高级配置", new Point(290, 538), new Size(150, 42));
        openBridge.Click += delegate { Process.Start(Path.Combine(root, "voxdeck-shortcuts.json")); };
        mappings.Controls.Add(save);
        mappings.Controls.Add(openBridge);
        content.Controls.Add(mappings);
        content.Controls.Add(preview);
    }

    private void BuildDevicePage()
    {
        AddPageTitle("连接与自检", "自动定位蓝牙、音频路由、转写工具与最近一次听写问题");
        SelfCheckReport report = BuildSelfCheckReport();

        var overview = NewCard(new Point(34, 100), new Size(960, 120));
        overview.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        overview.BackColor = report.FailedCount > 0 ? Color.FromArgb(255, 247, 247) : report.WarningCount > 0 ? Color.FromArgb(255, 250, 239) : Color.FromArgb(239, 251, 246);
        var score = new RoundPanel();
        score.Location = new Point(24, 22);
        score.Size = new Size(76, 76);
        score.Radius = 38;
        score.BackColor = report.FailedCount > 0 ? Color.FromArgb(255, 232, 232) : report.WarningCount > 0 ? Color.FromArgb(255, 242, 212) : Color.FromArgb(220, 247, 233);
        score.BorderColor = report.FailedCount > 0 ? Color.FromArgb(238, 185, 185) : report.WarningCount > 0 ? Color.FromArgb(242, 211, 151) : Color.FromArgb(164, 225, 193);
        var scoreValue = NewLabel(report.PassedCount + "/" + report.Items.Count, 14f, FontStyle.Bold,
            report.FailedCount > 0 ? coral : report.WarningCount > 0 ? amber : green);
        scoreValue.Dock = DockStyle.Fill;
        scoreValue.TextAlign = ContentAlignment.MiddleCenter;
        score.Controls.Add(scoreValue);
        var headline = NewLabel(report.Headline, 16f, FontStyle.Bold, ink);
        headline.Location = new Point(124, 24);
        headline.Size = new Size(460, 32);
        var overviewDetail = NewLabel(report.Detail, 9.2f, FontStyle.Regular, muted);
        overviewDetail.Location = new Point(125, 60);
        overviewDetail.Size = new Size(520, 38);
        var rerun = PrimaryButton("重新自检", new Point(700, 38), new Size(112, 42));
        rerun.Click += delegate { RunSelfCheckAndRefresh(); };
        var setup = SecondaryButton("首次设置", new Point(824, 38), new Size(108, 42));
        setup.Click += delegate { ShowSetupWizard(); };
        overview.Controls.Add(score);
        overview.Controls.Add(headline);
        overview.Controls.Add(overviewDetail);
        overview.Controls.Add(rerun);
        overview.Controls.Add(setup);

        var checks = NewCard(new Point(34, 236), new Size(620, 470));
        checks.Controls.Add(SectionTitle("自检项目", "\uE9D9", new Point(24, 18)));
        var checkHint = NewLabel("出现异常时，点击右侧按钮即可前往对应设置。", 8.7f, FontStyle.Regular, muted);
        checkHint.Location = new Point(250, 21);
        checkHint.Size = new Size(330, 24);
        checks.Controls.Add(checkHint);
        for (int i = 0; i < report.Items.Count; i++) AddSelfCheckRow(checks, report.Items[i], 54 + i * 56);

        var session = NewCard(new Point(670, 236), new Size(324, 470));
        session.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        session.Controls.Add(SectionTitle("最近一次听写", "\uE720", new Point(22, 18)));
        var summaryBand = new Panel();
        summaryBand.Location = new Point(22, 52);
        summaryBand.Size = new Size(280, 174);
        summaryBand.BackColor = Color.FromArgb(246, 249, 253);
        var summary = NewLabel(BuildSessionHealthSummary(), 8.35f, FontStyle.Regular, ink);
        summary.Location = new Point(12, 9);
        summary.Size = new Size(256, 158);
        summaryBand.Controls.Add(summary);
        var copy = PrimaryButton("复制问题摘要", new Point(22, 242), new Size(132, 38));
        copy.Click += delegate
        {
            Clipboard.SetText(BuildProblemSummary());
            ShowToast("问题摘要已复制，可直接粘贴到 GitHub Issue", "success");
        };
        var export = SecondaryButton("导出诊断", new Point(166, 242), new Size(136, 38));
        export.Click += delegate { ExportDiagnostics(); };
        var rawTitle = NewLabel("技术日志片段", 8.7f, FontStyle.Bold, muted);
        rawTitle.Location = new Point(22, 294);
        rawTitle.Size = new Size(130, 22);
        logBox = new TextBox();
        logBox.Location = new Point(22, 318);
        logBox.Size = new Size(280, 82);
        logBox.Multiline = true;
        logBox.ScrollBars = ScrollBars.Vertical;
        logBox.ReadOnly = true;
        logBox.BorderStyle = BorderStyle.FixedSingle;
        logBox.BackColor = Color.FromArgb(246, 249, 253);
        logBox.ForeColor = muted;
        logBox.Font = new Font("Consolas", 7.8f);
        logBox.Text = LoadRecentDiagnostics();
        var openLogs = SecondaryButton("打开日志", new Point(22, 414), new Size(108, 36));
        openLogs.Click += delegate { OpenLogFolder(); };
        var captureAudio = SecondaryButton("诊断下一段音频", new Point(142, 414), new Size(160, 36));
        captureAudio.Click += delegate { CaptureNextAudioDiagnostic(); };
        session.Controls.Add(summaryBand);
        session.Controls.Add(copy);
        session.Controls.Add(export);
        session.Controls.Add(rawTitle);
        session.Controls.Add(logBox);
        session.Controls.Add(openLogs);
        session.Controls.Add(captureAudio);

        content.Controls.Add(overview);
        content.Controls.Add(checks);
        content.Controls.Add(session);
        logBox.SelectionStart = logBox.TextLength;
        logBox.ScrollToCaret();
    }

    private void BuildSettingsPage()
    {
        AddPageTitle("偏好设置", "让言灵按你的习惯在后台运行");
        var startupCard = NewCard(new Point(34, 100), new Size(580, 310));
        startupCard.Controls.Add(SectionTitle("启动与窗口", "\uE713", new Point(28, 22)));
        var start = StyledCheck("打开言灵后自动连接遥控器", config.startBridgeOnLaunch, new Point(32, 70));
        start.CheckedChanged += delegate { config.startBridgeOnLaunch = start.Checked; SaveConfig(); };
        var traySetting = StyledCheck("关闭主窗口后继续在系统托盘运行", config.minimizeToTray, new Point(32, 118));
        traySetting.CheckedChanged += delegate { config.minimizeToTray = traySetting.Checked; SaveConfig(); };
        var startup = StyledCheck("登录 Windows 后自动启动言灵", config.launchAtStartup, new Point(32, 166));
        startup.CheckedChanged += delegate { config.launchAtStartup = startup.Checked; SetLaunchAtStartup(startup.Checked); SaveConfig(); };
        var startupBand = new Panel();
        startupBand.Location = new Point(30, 222);
        startupBand.Size = new Size(520, 58);
        startupBand.BackColor = Color.FromArgb(246, 249, 253);
        var startupState = NewLabel((config.launchAtStartup ? "●  已设置开机启动" : "●  仅在手动打开后运行") + "  ·  " +
            (config.minimizeToTray ? "关闭窗口后保持连接" : "关闭窗口时退出"), 9f, FontStyle.Bold,
            config.launchAtStartup ? green : muted);
        startupState.Location = new Point(16, 17);
        startupState.Size = new Size(486, 26);
        startupBand.Controls.Add(startupState);
        startupCard.Controls.Add(start);
        startupCard.Controls.Add(traySetting);
        startupCard.Controls.Add(startup);
        startupCard.Controls.Add(startupBand);

        var feedbackCard = NewCard(new Point(630, 100), new Size(364, 310));
        feedbackCard.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        feedbackCard.Controls.Add(SectionTitle("交互反馈", "\uE8BD", new Point(26, 22)));
        var feedbackSound = StyledCheck("听写完成或失败时播放提示音", config.soundFeedbackEnabled, new Point(28, 72));
        feedbackSound.Size = new Size(308, 40);
        feedbackSound.CheckedChanged += delegate
        {
            config.soundFeedbackEnabled = feedbackSound.Checked;
            SaveConfig();
            ShowToast(feedbackSound.Checked ? "听写提示音已开启" : "听写提示音已关闭", "success");
        };
        var previewSound = SecondaryButton("试听完成提示音", new Point(28, 126), new Size(146, 40));
        previewSound.Click += delegate
        {
            PlayFeedbackSound(true);
            ShowToast("已播放听写完成提示音", "success");
        };
        var feedbackNote = NewLabel("录音、整理、完成与失败状态会同步显示在首页遥控器和状态栏。", 8.9f, FontStyle.Regular, muted);
        feedbackNote.Location = new Point(28, 194);
        feedbackNote.Size = new Size(304, 52);
        var feedbackState = NewLabel("●  视觉反馈始终开启", 9f, FontStyle.Bold, violet);
        feedbackState.Location = new Point(28, 258);
        feedbackState.Size = new Size(260, 24);
        feedbackCard.Controls.Add(feedbackSound);
        feedbackCard.Controls.Add(previewSound);
        feedbackCard.Controls.Add(feedbackNote);
        feedbackCard.Controls.Add(feedbackState);

        var privacyCard = NewCard(new Point(34, 426), new Size(960, 280));
        privacyCard.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        var privacyTitle = SectionTitle("隐私与维护", "\uEA18", new Point(28, 22));
        var privacy = StyledCheck("本地安全模式：默认不保存录音、不上传音频、不读取听写文字", true, new Point(32, 62));
        privacy.Enabled = false;
        var privacyNote = NewLabel("普通日志只记录连接状态与聚合指标，单个日志自动限制为 4 MB。诊断音频必须每次明确确认。", 8.8f, FontStyle.Regular, muted);
        privacyNote.Location = new Point(34, 104);
        privacyNote.Size = new Size(830, 28);
        var setup = PrimaryButton("打开入门指南", new Point(32, 154), new Size(140, 42));
        setup.Click += delegate { ShowSetupWizard(); };
        var open = SecondaryButton("查看配置文件", new Point(184, 154), new Size(142, 42));
        open.Click += delegate { Process.Start(configPath); };
        var export = SecondaryButton("备份配置", new Point(338, 154), new Size(112, 42));
        export.Click += delegate { ExportConfig(); };
        var updates = SecondaryButton("检查更新", new Point(462, 154), new Size(112, 42));
        updates.Click += delegate { OpenUri("https://github.com/richlearntodo-debug/vibe-flow/releases/latest"); };
        var about = NewLabel(DisplayProductName + " · " + ProductRelease + " · Windows 正式版\r\nRC003 本地语音传输与快捷操作工具 · 开源版本", 9.5f, FontStyle.Regular, muted);
        about.Location = new Point(600, 154);
        about.Size = new Size(320, 62);
        var profile = NewLabel("稳定语音档案 v" + StableVoiceProfileVersion + "  ·  配置 schema " + ConfigSchemaVersion, 8.7f, FontStyle.Bold, violet);
        profile.Location = new Point(32, 226);
        profile.Size = new Size(400, 24);
        privacyCard.Controls.Add(privacyTitle);
        privacyCard.Controls.Add(privacy);
        privacyCard.Controls.Add(privacyNote);
        privacyCard.Controls.Add(setup);
        privacyCard.Controls.Add(open);
        privacyCard.Controls.Add(export);
        privacyCard.Controls.Add(updates);
        privacyCard.Controls.Add(about);
        privacyCard.Controls.Add(profile);

        content.Controls.Add(startupCard);
        content.Controls.Add(feedbackCard);
        content.Controls.Add(privacyCard);
    }

    private void AddPageTitle(string title, string subtitle)
    {
        var a = NewLabel(title, 24f, FontStyle.Bold, ink);
        a.Location = new Point(42, 24);
        a.AutoSize = true;
        var b = NewLabel(subtitle, 10f, FontStyle.Regular, muted);
        b.Location = new Point(45, 67);
        b.AutoSize = true;
        var release = NewLabel("V" + ProductRelease, 8.3f, FontStyle.Bold, green);
        release.Location = new Point(Math.Max(760, content.ClientSize.Width - 146), 29);
        release.Size = new Size(104, 30);
        release.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        release.TextAlign = ContentAlignment.MiddleCenter;
        release.BackColor = Color.FromArgb(235, 249, 242);
        ApplyRoundedRegion(release, 6);
        content.Controls.Add(a);
        content.Controls.Add(b);
        content.Controls.Add(release);
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

    private static Bitmap CreateNavigationIcon(string icon, Color color, bool active)
    {
        var bitmap = new Bitmap(34, 24);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        using (var pen = new Pen(color, active ? 2.05f : 1.75f))
        using (var soft = new SolidBrush(Color.FromArgb(active ? 42 : 18, color)))
        using (var solid = new SolidBrush(color))
        {
            graphics.Clear(Color.Transparent);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            pen.LineJoin = LineJoin.Round;

            if (icon == "overview")
            {
                graphics.FillRoundedRectangle(soft, new Rectangle(2, 3, 8, 8), 2);
                graphics.DrawRoundedRectangle(pen, new Rectangle(2, 3, 8, 8), 2);
                graphics.DrawRoundedRectangle(pen, new Rectangle(13, 3, 8, 8), 2);
                graphics.DrawRoundedRectangle(pen, new Rectangle(2, 14, 8, 7), 2);
                graphics.DrawRoundedRectangle(pen, new Rectangle(13, 14, 8, 7), 2);
            }
            else if (icon == "voice")
            {
                graphics.FillRoundedRectangle(soft, new Rectangle(6, 2, 8, 13), 4);
                graphics.DrawRoundedRectangle(pen, new Rectangle(6, 2, 8, 13), 4);
                graphics.DrawArc(pen, new Rectangle(3, 7, 14, 12), 0, 180);
                graphics.DrawLine(pen, 10, 19, 10, 22);
                graphics.DrawLine(pen, 7, 22, 13, 22);
                graphics.DrawArc(pen, new Rectangle(16, 6, 6, 9), -70, 140);
            }
            else if (icon == "shortcuts")
            {
                graphics.FillRoundedRectangle(soft, new Rectangle(1, 4, 21, 16), 3);
                graphics.DrawRoundedRectangle(pen, new Rectangle(1, 4, 21, 16), 3);
                for (int i = 0; i < 3; i++) graphics.FillRoundedRectangle(solid, new Rectangle(5 + i * 5, 8, 3, 3), 1);
                graphics.DrawLine(pen, 5, 16, 18, 16);
            }
            else if (icon == "diagnostics")
            {
                using (var path = new GraphicsPath())
                {
                    path.AddLines(new PointF[] {
                        new PointF(11.5f, 2), new PointF(20.5f, 5.5f), new PointF(19.5f, 14),
                        new PointF(16.5f, 19), new PointF(11.5f, 22), new PointF(6.5f, 19),
                        new PointF(3.5f, 14), new PointF(2.5f, 5.5f)
                    });
                    path.CloseFigure();
                    graphics.FillPath(soft, path);
                    graphics.DrawPath(pen, path);
                }
                graphics.DrawLines(pen, new PointF[] { new PointF(6.5f, 12), new PointF(10, 15.5f), new PointF(16.8f, 8.5f) });
            }
            else
            {
                int[] knobX = { 8, 16, 11 };
                for (int i = 0; i < 3; i++)
                {
                    int y = 5 + i * 7;
                    graphics.DrawLine(pen, 2, y, 21, y);
                    graphics.FillEllipse(soft, knobX[i] - 3, y - 3, 6, 6);
                    graphics.FillEllipse(solid, knobX[i] - 1, y - 1, 3, 3);
                }
            }
        }
        return bitmap;
    }

    private static Bitmap CreateGlyphBitmap(string glyph, Color color, int width, int height, float fontSize)
    {
        var bitmap = new Bitmap(width, height);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        using (var font = new Font("Segoe MDL2 Assets", fontSize, FontStyle.Regular, GraphicsUnit.Point))
        using (var brush = new SolidBrush(color))
        {
            graphics.Clear(Color.Transparent);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            SizeF size = graphics.MeasureString(glyph ?? "", font);
            graphics.DrawString(glyph ?? "", font, brush, (width - size.Width) / 2f, (height - size.Height) / 2f);
        }
        return bitmap;
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
        tray.Text = DisplayProductName;
        tray.Visible = true;
        tray.DoubleClick += delegate { ShowMainWindow(); };
        var menu = new ContextMenuStrip();
        menu.Items.Add("打开言灵", null, delegate { ShowMainWindow(); });
        ToolStripItem bridgeItem = menu.Items.Add(IsCapturing ? "暂停语音桥接" : "启动语音桥接", null, delegate { ToggleCapture(); });
        menu.Items.Add("退出", null, delegate { config.minimizeToTray = false; Close(); });
        menu.Opening += delegate { bridgeItem.Text = IsCapturing ? "暂停语音桥接" : "启动语音桥接"; };
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
        WarmConfiguredProviderAsync(false);
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
        if (activityTimer != null) { activityTimer.Stop(); activityTimer.Dispose(); activityTimer = null; }
        if (visualTimer != null) { visualTimer.Stop(); visualTimer.Dispose(); visualTimer = null; }
        if (toastTimer != null) { toastTimer.Stop(); toastTimer.Dispose(); toastTimer = null; }
        StopCapture();
        StopKeyboardBridge();
        ReleaseVoiceHotkey();
        if (dictationCompletePlayer != null) { dictationCompletePlayer.Dispose(); dictationCompletePlayer = null; }
        if (dictationErrorPlayer != null) { dictationErrorPlayer.Dispose(); dictationErrorPlayer = null; }
        if (dictationCompleteSound != null) { dictationCompleteSound.Dispose(); dictationCompleteSound = null; }
        if (dictationErrorSound != null) { dictationErrorSound.Dispose(); dictationErrorSound = null; }
        try { if (showWindowEvent != null) { showWindowEvent.Set(); showWindowEvent.Dispose(); } } catch { }
        try { if (exitApplicationEvent != null) { exitApplicationEvent.Set(); exitApplicationEvent.Dispose(); } } catch { }
        try { if (voiceWakeRequestEvent != null) { voiceWakeRequestEvent.Set(); voiceWakeRequestEvent.Dispose(); } } catch { }
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
                start.Arguments = config.captureSeconds + " \"" + sessionDir + "\" \"" + config.audioEndpointName + "\" " +
                    config.gain.ToString(CultureInfo.InvariantCulture) + " " + config.drainMs + " " + config.autoLevel + " " +
                    SafeCaptureArgument(config.inputMethod) + " " + SafeCaptureArgument(config.inputMethodHotkey) + " " +
                    SafeCaptureArgument(config.inputMethodTrigger) + " " + config.providerStartupDelayMs + " " +
                    SafeCaptureArgument(config.audioProcessingMode) + " " + config.autoRouteVirtualMicrophone;
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
            Process startedCapture = captureProcess;
            captureProcess.Exited += delegate
            {
                try { BeginInvoke(new Action(delegate { CaptureExited(startedCapture); })); }
                catch { }
            };
            captureProcess.Start();
            captureStartedAt = DateTime.Now;
            captureProcess.BeginOutputReadLine();
            captureProcess.BeginErrorReadLine();
            HostLog("CAPTURE START pid=" + captureProcess.Id + " provider=" + NormalizeProviderKey(config.inputMethod));
            UpdateCaptureUi();
            Toast("正在连接遥控器麦克风，请稍候");
        }
        catch (Exception ex)
        {
            HostLog("CAPTURE START FAILED error=" + ex.Message);
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
        captureStartedAt = DateTime.MinValue;
        HostLog("CAPTURE STOP");
        UpdateCaptureUi();
    }

    private void PaintHeroSurface(object sender, PaintEventArgs e)
    {
        var panel = sender as Control;
        if (panel == null) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        int right = panel.Width - 34;
        using (var accentRail = new SolidBrush(Color.FromArgb(210, currentVisualAccent)))
            e.Graphics.FillRectangle(accentRail, 0, 0, 4, panel.Height);
        using (var gridPen = new Pen(Color.FromArgb(18, 61, 83, 126), 1f))
        {
            for (int x = panel.Width - 330; x < panel.Width - 24; x += 24)
                e.Graphics.DrawLine(gridPen, x, 22, x, panel.Height - 22);
            for (int y = 22; y < panel.Height - 12; y += 24)
                e.Graphics.DrawLine(gridPen, panel.Width - 330, y, panel.Width - 24, y);
        }
        using (var cyanPen = new Pen(Color.FromArgb(70, cyan), 2f))
        using (var violetPen = new Pen(Color.FromArgb(62, currentVisualAccent), 2f))
        {
            e.Graphics.DrawLine(cyanPen, right - 220, 28, right, 28);
            e.Graphics.DrawLine(violetPen, right - 150, 36, right, 36);
        }
        float phase = remoteVisual == null ? 0f : remoteVisual.AnimationPhase;
        int signalX = Math.Max(610, panel.Width - 322);
        int signalCenter = panel.Height / 2;
        int activeHeight = currentVisualState == "recording" ? 32 : currentVisualState == "processing" ? 22 : 12;
        for (int i = 0; i < 8; i++)
        {
            double wave = (Math.Sin(phase * 1.45f + i * 0.82f) + 1.0) / 2.0;
            int barHeight = 5 + (int)(wave * activeHeight);
            var bar = new Rectangle(signalX + i * 8, signalCenter - barHeight / 2, 3, barHeight);
            using (var brush = new SolidBrush(Color.FromArgb(currentVisualState == "recording" ? 145 : 72, currentVisualAccent)))
                e.Graphics.FillRoundedRectangle(brush, bar, 1);
        }
    }

    private void RestartCaptureForAudioSettings()
    {
        if (!IsCapturing) return;
        StopCapture();
        StartCapture();
        Toast("新的语音设置已生效");
    }

    private static string SafeCaptureArgument(string value)
    {
        return "\"" + (value ?? "").Replace("\"", "") + "\"";
    }

    private void CaptureExited(Process exitedCapture)
    {
        if (!ReferenceEquals(captureProcess, exitedCapture))
        {
            HostLog("CAPTURE EXIT superseded=true ignored=true");
            return;
        }
        captureProcess = null;
        bridgeReady = false;
        captureStartedAt = DateTime.MinValue;
        UpdateCaptureUi();
        if (applicationExiting || captureStopping || !config.startBridgeOnLaunch) return;
        HostLog("CAPTURE EXIT unexpected=true reconnect=scheduled");
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
        HostLog("CAPTURE RECONNECT delay_ms=" + delay);
    }

    private void HandleVoiceWakeRequest()
    {
        if (applicationExiting) return;
        config = LoadConfig();
        bool held = IsVoiceKeyHeld();
        HostLog("VOICE WAKE REQUEST held=" + held + " capture_running=" + IsCapturing +
            " atvv_ready=" + bridgeReady + " provider=" + NormalizeProviderKey(config.inputMethod) +
            " provider_ready=" + IsProviderReadyForStartup(config.inputMethod));
        WarmConfiguredProviderAsync(true);

        if (captureStopping)
        {
            HostLog("VOICE WAKE ignored reason=bridge_paused");
            return;
        }
        if (!IsCapturing)
        {
            HostLog("VOICE WAKE recovery=start_capture");
            StartCapture();
            return;
        }
        if (bridgeReady || !held || captureStartedAt == DateTime.MinValue) return;

        int waitingMs = (int)Math.Max(0, (DateTime.Now - captureStartedAt).TotalMilliseconds);
        if (waitingMs < 30000)
        {
            HostLog("VOICE WAKE pending_atvv waiting_ms=" + waitingMs);
            return;
        }

        startupRecoveryCount++;
        HostLog("VOICE WAKE recovery=restart_stalled_capture waiting_ms=" + waitingMs +
            " attempt=" + startupRecoveryCount);
        StopCapture();
        StartCapture();
    }

    private static bool IsVoiceKeyHeld()
    {
        try
        {
            using (EventWaitHandle handle = EventWaitHandle.OpenExisting("Local\\VibeMicVoiceKeyHeld"))
                return handle.WaitOne(0);
        }
        catch { return false; }
    }

    private void WarmConfiguredProviderAsync(bool launchImmediately)
    {
        string provider = NormalizeProviderKey(config.inputMethod);
        if (provider == "windows" || provider == "custom") return;
        if (launchImmediately) Interlocked.Exchange(ref providerWarmupLaunchRequested, 1);
        lock (providerWarmupLock)
        {
            if (providerWarmupActive) return;
            providerWarmupActive = true;
        }

        ThreadPool.QueueUserWorkItem(delegate
        {
            bool launchAttempted = false;
            try
            {
                for (int attempt = 0; attempt < 40 && !applicationExiting; attempt++)
                {
                    if (!NormalizeProviderKey(config.inputMethod).Equals(provider, StringComparison.OrdinalIgnoreCase)) return;
                    if (IsProviderReadyForStartup(provider))
                    {
                        HostLog("PROVIDER READY provider=" + provider + " waited_ms=" + (attempt * 500));
                        return;
                    }
                    bool expedited = Interlocked.CompareExchange(ref providerWarmupLaunchRequested, 0, 0) == 1;
                    if (!launchAttempted && (expedited || attempt >= 8))
                    {
                        launchAttempted = true;
                        Interlocked.Exchange(ref providerWarmupLaunchRequested, 0);
                        TryLaunchConfiguredProvider(provider);
                    }
                    Thread.Sleep(500);
                }
                HostLog("PROVIDER NOT READY provider=" + provider + " action=check_startup_and_hotkey");
            }
            finally
            {
                Interlocked.Exchange(ref providerWarmupLaunchRequested, 0);
                lock (providerWarmupLock) providerWarmupActive = false;
            }
        });
    }

    private bool IsProviderReadyForStartup(string provider)
    {
        string normalized = NormalizeProviderKey(provider);
        if (normalized == "wechat")
            return IsProcessRunning("wetype_server") &&
                (IsProcessRunning("wetype_renderer") || FindWindow("wetype.statusbar.window", null) != IntPtr.Zero);
        return IsProviderRunning(normalized);
    }

    private void TryLaunchConfiguredProvider(string provider)
    {
        if (IsProviderReadyForStartup(provider)) return;
        string target = FindProviderLaunchTarget(provider);
        if (string.IsNullOrWhiteSpace(target))
        {
            HostLog("PROVIDER LAUNCH unavailable provider=" + provider);
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            HostLog("PROVIDER LAUNCH requested provider=" + provider + " target=" + target);
        }
        catch (Exception ex)
        {
            HostLog("PROVIDER LAUNCH failed provider=" + provider + " error=" + ex.Message);
        }
    }

    private static string FindProviderLaunchTarget(string provider)
    {
        string normalized = NormalizeProviderKey(provider);
        var directCandidates = new List<string>();
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (normalized == "typeless")
        {
            directCandidates.Add(Path.Combine(local, "Programs", "Typeless", "Typeless.exe"));
            directCandidates.Add(Path.Combine(roaming, "Typeless.exe", "Typeless.exe"));
        }
        else if (normalized == "voquill")
        {
            directCandidates.Add(Path.Combine(local, "Programs", "Voquill", "Voquill.exe"));
            directCandidates.Add(Path.Combine(roaming, "Voquill", "Voquill.exe"));
        }
        foreach (string candidate in directCandidates) if (File.Exists(candidate)) return candidate;

        string[] needles = normalized == "wechat"
            ? new string[] { "微信输入法", "wetype" }
            : normalized == "typeless" ? new string[] { "typeless" } : new string[] { "voquill" };
        string[] startMenuRoots =
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms)
        };
        foreach (string startMenuRoot in startMenuRoots)
        {
            if (string.IsNullOrWhiteSpace(startMenuRoot) || !Directory.Exists(startMenuRoot)) continue;
            try
            {
                foreach (string shortcut in Directory.GetFiles(startMenuRoot, "*.lnk", SearchOption.AllDirectories))
                {
                    string name = Path.GetFileNameWithoutExtension(shortcut).ToLowerInvariant();
                    if (name.Contains("uninstall") || name.Contains("卸载")) continue;
                    foreach (string needle in needles)
                        if (name.Contains(needle.ToLowerInvariant())) return shortcut;
                }
            }
            catch { }
        }
        return "";
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
            if (!File.Exists(executable)) { HostLog("KEYBOARD BRIDGE missing=true"); return; }
            var start = new ProcessStartInfo(executable, "--background");
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.WindowStyle = ProcessWindowStyle.Hidden;
            keyboardBridgeProcess = Process.Start(start);
            HostLog("KEYBOARD BRIDGE started=true pid=" + keyboardBridgeProcess.Id);
        }
        catch (Exception ex) { HostLog("KEYBOARD BRIDGE start_failed=true error=" + ex.Message); }
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
        config = LoadConfig();
        string provider = NormalizeProviderKey(config.inputMethod);
        if (provider == "wechat" && TryClickWeTypeToolbar())
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                Thread.Sleep(1200);
                TryClickWeTypeToolbar();
            });
            Toast("已触发微信语音面板；面板出现即表示启动控制正常");
            return;
        }

        string shortcut = config.inputMethodHotkey;
        bool hold = config.inputMethodTrigger == "hold";
        ThreadPool.QueueUserWorkItem(delegate
        {
            if (!SendConfiguredHotkey(shortcut, false)) return;
            Thread.Sleep(hold ? 1000 : 80);
            SendConfiguredHotkey(shortcut, true);
            if (!hold)
            {
                Thread.Sleep(1000);
                SendConfiguredHotkey(shortcut, false);
                Thread.Sleep(80);
                SendConfiguredHotkey(shortcut, true);
            }
        });
        Toast("已测试 " + ProviderDisplayName(provider) + " 快捷键 " + shortcut.Replace("+", " + "));
    }

    private static bool SendConfiguredHotkey(string shortcut, bool keyUp)
    {
        if (!IsValidTranscriptionHotkey(shortcut)) return false;
        string[] parts = shortcut.Split(new char[] { '+', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var keys = new List<int>();
        foreach (string part in parts)
        {
            int key = TranscriptionVirtualKey(part);
            if (key <= 0) return false;
            keys.Add(key);
        }
        if (keyUp) keys.Reverse();
        foreach (int key in keys)
        {
            uint flags = keyUp ? 0x0002u : 0u;
            if (key == 0x5B || key == 0x5C || key == 0xA3 || key == 0xA5) flags |= 0x0001u;
            keybd_event((byte)key, (byte)MapVirtualKey((uint)key, 0), flags, UIntPtr.Zero);
        }
        return true;
    }

    private static int TranscriptionVirtualKey(string raw)
    {
        string value = (raw ?? "").Trim().ToLowerInvariant();
        if (value.Length == 1)
        {
            char character = char.ToUpperInvariant(value[0]);
            if (char.IsLetterOrDigit(character)) return character;
        }
        if (value.StartsWith("f"))
        {
            int number;
            if (int.TryParse(value.Substring(1), out number) && number >= 1 && number <= 24) return 0x70 + number - 1;
        }
        var names = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "ctrl", 0xA2 }, { "control", 0xA2 }, { "leftctrl", 0xA2 }, { "lctrl", 0xA2 },
            { "rightctrl", 0xA3 }, { "rctrl", 0xA3 }, { "win", 0x5B }, { "meta", 0x5B },
            { "leftwin", 0x5B }, { "lwin", 0x5B }, { "rightwin", 0x5C }, { "rwin", 0x5C },
            { "alt", 0xA4 }, { "leftalt", 0xA4 }, { "lalt", 0xA4 }, { "rightalt", 0xA5 }, { "ralt", 0xA5 },
            { "shift", 0xA0 }, { "leftshift", 0xA0 }, { "rightshift", 0xA1 },
            { "space", 0x20 }, { "enter", 0x0D }, { "tab", 0x09 }, { "escape", 0x1B }, { "esc", 0x1B }
        };
        int result;
        return names.TryGetValue(value, out result) ? result : -1;
    }

    private static bool TryClickWeTypeToolbar()
    {
        IntPtr toolbar = FindWindow("wetype.statusbar.window", null);
        if (toolbar == IntPtr.Zero) return false;
        ClientRect rectangle;
        if (!GetClientRect(toolbar, out rectangle) || rectangle.Right <= 0 || rectangle.Bottom <= 0) return false;
        int x = Math.Max(1, rectangle.Right * 45 / 142);
        int y = Math.Max(1, rectangle.Bottom / 2);
        IntPtr point = new IntPtr((y << 16) | (x & 0xFFFF));
        bool down = PostMessage(toolbar, 0x0201, new IntPtr(1), point);
        bool up = PostMessage(toolbar, 0x0202, IntPtr.Zero, point);
        return down && up;
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

    private static bool HasCableOutput()
    {
        try
        {
            for (uint i = 0; i < waveInGetNumDevs(); i++)
            {
                WaveInCaps caps;
                if (waveInGetDevCaps((UIntPtr)i, out caps, (uint)Marshal.SizeOf(typeof(WaveInCaps))) == 0 &&
                    (caps.name ?? "").IndexOf("CABLE Output", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
        }
        catch { }
        return false;
    }

    private void ShowSetupWizard()
    {
        if (setupWizardOpen) return;
        setupWizardOpen = true;
        try
        {
            using (var wizard = new Form())
            {
                wizard.Text = "欢迎使用 " + DisplayProductName + " · V1";
                wizard.ClientSize = new Size(920, 660);
                wizard.FormBorderStyle = FormBorderStyle.FixedDialog;
                wizard.MaximizeBox = false;
                wizard.MinimizeBox = false;
                wizard.StartPosition = FormStartPosition.CenterParent;
                wizard.BackColor = Color.FromArgb(245, 247, 251);
                wizard.Font = Font;
                wizard.Icon = Icon;

                var rail = new Panel();
                rail.Dock = DockStyle.Left;
                rail.Width = 228;
                rail.BackColor = Color.FromArgb(248, 250, 255);
                rail.Paint += delegate(object sender, PaintEventArgs e)
                {
                    using (var progress = new Pen(Color.FromArgb(218, 224, 239), 2f))
                        e.Graphics.DrawLine(progress, 43, 146, 43, 364);
                    using (var pen = new Pen(line)) e.Graphics.DrawLine(pen, rail.Width - 1, 0, rail.Width - 1, rail.Height);
                };
                var setupLogo = new PictureBox();
                setupLogo.Image = LoadBrandLogo();
                setupLogo.SizeMode = PictureBoxSizeMode.Zoom;
                setupLogo.BackColor = Color.Transparent;
                setupLogo.Location = new Point(28, 26);
                setupLogo.Size = new Size(42, 42);
                var railBrand = NewLabel("言灵", 17f, FontStyle.Bold, ink);
                railBrand.Location = new Point(82, 26);
                railBrand.AutoSize = true;
                var railEnglish = NewLabel("VIBE FLOW REMOTE · V1", 7.3f, FontStyle.Bold, violet);
                railEnglish.Location = new Point(84, 56);
                railEnglish.AutoSize = true;
                rail.Controls.Add(setupLogo);
                rail.Controls.Add(railBrand);
                rail.Controls.Add(railEnglish);
                wizard.Disposed += delegate { if (setupLogo.Image != null) setupLogo.Image.Dispose(); };

                string[] stepNames = { "选择转写工具", "安装音频通道", "连接遥控器", "匹配快捷键", "完成首次听写" };
                var progressLabels = new Label[stepNames.Length];
                for (int i = 0; i < stepNames.Length; i++)
                {
                    var number = NewLabel((i + 1).ToString(), 9.5f, FontStyle.Bold, muted);
                    number.Location = new Point(28, 116 + i * 62);
                    number.Size = new Size(30, 30);
                    number.TextAlign = ContentAlignment.MiddleCenter;
                    number.BackColor = Color.FromArgb(237, 240, 248);
                    var stepLabel = NewLabel(stepNames[i], 9.5f, FontStyle.Bold, muted);
                    stepLabel.Location = new Point(70, 119 + i * 62);
                    stepLabel.Size = new Size(140, 27);
                    progressLabels[i] = stepLabel;
                    rail.Controls.Add(number);
                    rail.Controls.Add(stepLabel);
                }
                var privacyRail = NewLabel("本地传输\r\n默认不保存录音\r\n不读取听写文字", 8.7f, FontStyle.Regular, muted);
                privacyRail.Location = new Point(30, 524);
                privacyRail.Size = new Size(170, 72);
                rail.Controls.Add(privacyRail);

                var body = new Panel();
                body.Dock = DockStyle.Fill;
                body.BackColor = Color.FromArgb(245, 247, 251);
                var pageContent = new Panel();
                pageContent.Location = new Point(34, 30);
                pageContent.Size = new Size(624, 520);
                pageContent.BackColor = Color.Transparent;
                var back = SecondaryButton("上一步", new Point(34, 584), new Size(112, 42));
                var next = PrimaryButton("下一步", new Point(508, 584), new Size(150, 42));
                var stepCounter = NewLabel("第 1 步，共 5 步", 8.5f, FontStyle.Bold, violet);
                stepCounter.Location = new Point(164, 594);
                stepCounter.Size = new Size(100, 24);
                var wizardFeedback = NewLabel("", 8.8f, FontStyle.Bold, muted);
                wizardFeedback.Location = new Point(270, 590);
                wizardFeedback.Size = new Size(226, 28);
                wizardFeedback.TextAlign = ContentAlignment.MiddleCenter;
                body.Controls.Add(pageContent);
                body.Controls.Add(back);
                body.Controls.Add(next);
                body.Controls.Add(stepCounter);
                body.Controls.Add(wizardFeedback);
                wizard.Controls.Add(body);
                wizard.Controls.Add(rail);

                int currentStep = 0;
                string selectedProvider = NormalizeProviderKey(config.inputMethod);
                string selectedHotkey = config.inputMethodHotkey;
                string selectedTrigger = config.inputMethodTrigger;
                bool startupChoiceValue = config.launchAtStartup;
                bool autoRouteChoiceValue = config.autoRouteVirtualMicrophone;
                TextBox shortcutBox = null;
                ComboBox triggerBox = null;
                Label liveConnectionStatus = null;
                Label firstDictationStatus = null;
                int firstDictationBaselineGeneration = 0;
                bool firstDictationSucceeded = false;
                Action<int> renderStep = null;
                Action<string, bool> showWizardFeedback = delegate(string message, bool success)
                {
                    wizardFeedback.Text = message;
                    wizardFeedback.ForeColor = success ? green : Color.FromArgb(202, 76, 76);
                };
                Action<string> showWizardInfo = delegate(string message)
                {
                    wizardFeedback.Text = message;
                    wizardFeedback.ForeColor = cyan;
                };

                renderStep = delegate(int step)
                {
                    currentStep = Math.Max(0, Math.Min(4, step));
                    while (pageContent.Controls.Count > 0) pageContent.Controls[0].Dispose();
                    shortcutBox = null;
                    triggerBox = null;
                    liveConnectionStatus = null;
                    firstDictationStatus = null;
                    for (int i = 0; i < progressLabels.Length; i++)
                    {
                        progressLabels[i].ForeColor = i == currentStep ? violet : i < currentStep ? green : muted;
                        progressLabels[i].Font = new Font("Microsoft YaHei UI", 9.5f, i == currentStep ? FontStyle.Bold : FontStyle.Regular);
                        Control number = progressLabels[i].Parent.Controls[progressLabels[i].Parent.Controls.IndexOf(progressLabels[i]) - 1];
                        number.Text = i < currentStep ? "✓" : (i + 1).ToString();
                        number.ForeColor = i <= currentStep ? Color.White : muted;
                        number.BackColor = i < currentStep ? green : i == currentStep ? violet : Color.FromArgb(237, 240, 248);
                    }
                    stepCounter.Text = "第 " + (currentStep + 1) + " 步，共 5 步";
                    wizardFeedback.Text = "";
                    back.Enabled = currentStep > 0;
                    next.Text = currentStep == 0 ? "确认选择" : currentStep == 1 ? "确认音频通道" :
                        currentStep == 2 ? "确认连接" : currentStep == 3 ? "保存并测试" : "完成设置";

                    string headingText = currentStep == 0 ? "先选择你每天使用的转写工具" :
                        currentStep == 1 ? "安装一次本地音频通道" :
                        currentStep == 2 ? "连接并唤醒遥控器" :
                        currentStep == 3 ? "让快捷键与工具保持一致" : "完成第一次遥控器听写";
                    string subtitleText = currentStep == 0 ? "言灵负责传输遥控器声音，所选工具负责识别和整理文字。" :
                        currentStep == 1 ? "VB-CABLE 是当前语音链路唯一需要额外安装的本地驱动；检测通过后无需重复安装。" :
                        currentStep == 2 ? "先在 Windows 中完成蓝牙配对，再由言灵建立语音链路。" :
                        currentStep == 3 ? "这里的快捷键必须与转写工具内部设置完全相同。" : "点击下方输入框，按住遥控器录音键说完一句话后松开。";
                    var heading = NewLabel(headingText, 20f, FontStyle.Bold, ink);
                    heading.Location = new Point(0, 4);
                    heading.AutoSize = true;
                    var subtitle = NewLabel(subtitleText, 9.7f, FontStyle.Regular, muted);
                    subtitle.Location = new Point(2, 47);
                    subtitle.Size = new Size(608, 44);
                    pageContent.Controls.Add(heading);
                    pageContent.Controls.Add(subtitle);

                    if (currentStep == 0)
                    {
                        var providerLabel = NewLabel("我的转写工具", 9.5f, FontStyle.Bold, ink);
                        providerLabel.Location = new Point(4, 105);
                        providerLabel.Size = new Size(140, 28);
                        var providerChoice = StyledCombo(new Point(4, 137), new Size(336, 40));
                        providerChoice.Items.AddRange(new object[] { "微信输入法", "Typeless", "Windows 语音输入", "Voquill（开源）", "其他语音工具" });
                        providerChoice.SelectedIndex = ProviderIndex(selectedProvider);
                        var providerState = NewLabel(ProviderStatusText(selectedProvider), 9.2f, FontStyle.Bold,
                            IsProviderRunning(selectedProvider) ? green : amber);
                        providerState.Location = new Point(362, 143);
                        providerState.Size = new Size(250, 30);
                        var info = NewCard(new Point(4, 200), new Size(602, 184));
                        var infoTitle = NewLabel(ProviderDisplayName(selectedProvider), 12f, FontStyle.Bold, ink);
                        infoTitle.Location = new Point(22, 18);
                        infoTitle.AutoSize = true;
                        var infoSummary = NewLabel(ProviderSummary(selectedProvider), 9.4f, FontStyle.Regular, muted);
                        infoSummary.Location = new Point(22, 52);
                        infoSummary.Size = new Size(552, 45);
                        var profile = NewLabel("推荐配置  " + ProviderShortcutDescription(selectedProvider), 9.3f, FontStyle.Bold, violet);
                        profile.Location = new Point(22, 106);
                        profile.Size = new Size(550, 26);
                        var help = SecondaryButton("查看安装与设置", new Point(22, 136), new Size(150, 36));
                        help.Click += delegate { OpenProviderHelp(selectedProvider); };
                        info.Controls.Add(infoTitle);
                        info.Controls.Add(infoSummary);
                        info.Controls.Add(profile);
                        info.Controls.Add(help);
                        var startupChoice = StyledCheck("登录 Windows 后自动启动言灵", startupChoiceValue, new Point(4, 410));
                        startupChoice.CheckedChanged += delegate { startupChoiceValue = startupChoice.Checked; };
                        var startupHelp = NewLabel("默认不启用；可随时在“偏好设置”中修改。", 8.8f, FontStyle.Regular, muted);
                        startupHelp.Location = new Point(30, 445);
                        startupHelp.Size = new Size(520, 24);
                        providerChoice.SelectedIndexChanged += delegate
                        {
                            selectedProvider = ProviderKeyFromIndex(providerChoice.SelectedIndex);
                            selectedHotkey = DefaultHotkeyForProvider(selectedProvider);
                            selectedTrigger = DefaultTriggerForProvider(selectedProvider);
                            renderStep(0);
                        };
                        pageContent.Controls.Add(providerLabel);
                        pageContent.Controls.Add(providerChoice);
                        pageContent.Controls.Add(providerState);
                        pageContent.Controls.Add(info);
                        pageContent.Controls.Add(startupChoice);
                        pageContent.Controls.Add(startupHelp);
                    }
                    else if (currentStep == 1)
                    {
                        bool inputReady = HasCableInput();
                        bool outputReady = HasCableOutput();
                        var required = NewLabel(inputReady && outputReady ? "●  必需组件已安装" : "●  必需组件 · 仅需安装一次", 9f, FontStyle.Bold,
                            inputReady && outputReady ? green : amber);
                        required.Location = new Point(6, 94);
                        required.Size = new Size(280, 24);
                        var route = NewCard(new Point(4, 124), new Size(602, 224));
                        var routeTitle = NewLabel("RC003  →  CABLE Input  →  CABLE Output  →  " + ProviderDisplayName(selectedProvider), 10f, FontStyle.Bold, ink);
                        routeTitle.Location = new Point(22, 22);
                        routeTitle.Size = new Size(556, 28);
                        var inputState = NewLabel((inputReady ? "●  已检测到" : "●  未检测到") + "  CABLE Input（播放端）", 9.5f, FontStyle.Bold, inputReady ? green : Color.FromArgb(202, 76, 76));
                        inputState.Location = new Point(22, 72);
                        inputState.Size = new Size(360, 28);
                        var outputState = NewLabel((outputReady ? "●  已检测到" : "●  未检测到") + "  CABLE Output（录音端）", 9.5f, FontStyle.Bold, outputReady ? green : Color.FromArgb(202, 76, 76));
                        outputState.Location = new Point(22, 108);
                        outputState.Size = new Size(360, 28);
                        var install = SecondaryButton(inputReady && outputReady ? "打开声音设置" : "前往官方安装", new Point(400, 67), new Size(164, 38));
                        install.Click += delegate { OpenUri(inputReady && outputReady ? "ms-settings:sound" : "https://vb-audio.com/Cable/"); };
                        var recheck = SecondaryButton("重新检测", new Point(400, 113), new Size(164, 38));
                        recheck.Click += delegate { renderStep(1); };
                        var routeNote = NewLabel("安装后若仍未检测到，请按驱动提示重启 Windows。听写结束后会自动恢复原麦克风。", 8.8f, FontStyle.Regular, muted);
                        routeNote.Location = new Point(22, 168);
                        routeNote.Size = new Size(540, 42);
                        route.Controls.Add(routeTitle);
                        route.Controls.Add(inputState);
                        route.Controls.Add(outputState);
                        route.Controls.Add(install);
                        route.Controls.Add(recheck);
                        route.Controls.Add(routeNote);
                        var autoRouteChoice = StyledCheck("听写时自动使用遥控器麦克风（强烈推荐）", autoRouteChoiceValue, new Point(4, 376));
                        autoRouteChoice.CheckedChanged += delegate { autoRouteChoiceValue = autoRouteChoice.Checked; };
                        var safety = NewLabel("关闭后，需要在每个转写工具中手动选择 CABLE Output。", 8.9f, FontStyle.Regular, muted);
                        safety.Location = new Point(30, 414);
                        safety.Size = new Size(550, 26);
                        pageContent.Controls.Add(required);
                        pageContent.Controls.Add(route);
                        pageContent.Controls.Add(autoRouteChoice);
                        pageContent.Controls.Add(safety);
                    }
                    else if (currentStep == 2)
                    {
                        var connection = NewCard(new Point(4, 108), new Size(602, 230));
                        liveConnectionStatus = NewLabel(IsCapturing && bridgeReady ? "●  已连接，可以使用" : IsCapturing ? "●  正在建立语音链路" : "●  等待开始检测", 13f, FontStyle.Bold,
                            IsCapturing && bridgeReady ? green : IsCapturing ? amber : muted);
                        liveConnectionStatus.Location = new Point(24, 28);
                        liveConnectionStatus.Size = new Size(500, 34);
                        var model = NewLabel("小米蓝牙语音遥控器 2 Pro · RC003", 10f, FontStyle.Regular, ink);
                        model.Location = new Point(25, 76);
                        model.Size = new Size(480, 28);
                        var connectionHelp = NewLabel("若一直连接中，请按任意方向键唤醒遥控器，或在 Windows 中重新连接。", 9.2f, FontStyle.Regular, muted);
                        connectionHelp.Location = new Point(25, 112);
                        connectionHelp.Size = new Size(535, 46);
                        var bluetooth = SecondaryButton("打开蓝牙设置", new Point(24, 168), new Size(150, 40));
                        bluetooth.Click += delegate { OpenUri("ms-settings:bluetooth"); };
                        var detect = PrimaryButton(IsCapturing ? "重新检测" : "开始检测", new Point(188, 168), new Size(140, 40));
                        detect.Click += delegate
                        {
                            StartKeyboardBridge();
                            if (!IsCapturing) StartCapture();
                            liveConnectionStatus.Text = "●  正在建立语音链路";
                            liveConnectionStatus.ForeColor = amber;
                            detect.Text = "重新检测";
                            showWizardInfo("正在检测，请稍候");
                        };
                        connection.Controls.Add(liveConnectionStatus);
                        connection.Controls.Add(model);
                        connection.Controls.Add(connectionHelp);
                        connection.Controls.Add(bluetooth);
                        connection.Controls.Add(detect);
                        var readyHint = NewLabel("看到“已连接，可以使用”后再继续。首次建立蓝牙语音服务可能需要数秒。", 9.1f, FontStyle.Regular, muted);
                        readyHint.Location = new Point(6, 366);
                        readyHint.Size = new Size(590, 45);
                        pageContent.Controls.Add(connection);
                        pageContent.Controls.Add(readyHint);
                    }
                    else if (currentStep == 3)
                    {
                        var profileCard = NewCard(new Point(4, 108), new Size(602, 278));
                        var providerName = NewLabel(ProviderDisplayName(selectedProvider), 12f, FontStyle.Bold, ink);
                        providerName.Location = new Point(24, 22);
                        providerName.AutoSize = true;
                        var providerSetup = NewLabel(ProviderSetupInstruction(selectedProvider), 9.2f, FontStyle.Regular, muted);
                        providerSetup.Location = new Point(24, 54);
                        providerSetup.Size = new Size(548, 44);
                        var hotkeyLabel = NewLabel("启动快捷键", 9.3f, FontStyle.Bold, ink);
                        hotkeyLabel.Location = new Point(24, 116);
                        hotkeyLabel.Size = new Size(110, 28);
                        shortcutBox = StyledTextBox(selectedHotkey, new Point(140, 112), new Size(200, 34));
                        var triggerLabel = NewLabel("触发方式", 9.3f, FontStyle.Bold, ink);
                        triggerLabel.Location = new Point(24, 162);
                        triggerLabel.Size = new Size(110, 28);
                        triggerBox = StyledCombo(new Point(140, 158), new Size(200, 38));
                        triggerBox.Items.AddRange(new object[] { "单击切换", "按住触发" });
                        triggerBox.SelectedIndex = selectedTrigger == "hold" ? 1 : 0;
                        var reset = SecondaryButton("恢复推荐配置", new Point(366, 111), new Size(166, 38));
                        reset.Click += delegate
                        {
                            shortcutBox.Text = DefaultHotkeyForProvider(selectedProvider);
                            triggerBox.SelectedIndex = DefaultTriggerForProvider(selectedProvider) == "hold" ? 1 : 0;
                        };
                        var test = PrimaryButton("测试启动与结束", new Point(24, 216), new Size(166, 40));
                        test.Click += delegate
                        {
                            selectedHotkey = shortcutBox.Text.Trim();
                            selectedTrigger = triggerBox.SelectedIndex == 1 ? "hold" : "toggle";
                            if (!IsValidTranscriptionHotkey(selectedHotkey))
                            {
                                showWizardFeedback("快捷键格式不正确", false);
                                return;
                            }
                            SaveWizardProviderConfig(selectedProvider, selectedHotkey, selectedTrigger, autoRouteChoiceValue);
                            showWizardInfo("已发送测试，请确认工具已打开并结束");
                            TestVoiceHotkey();
                        };
                        var help = SecondaryButton("查看工具设置", new Point(204, 216), new Size(146, 40));
                        help.Click += delegate { OpenProviderHelp(selectedProvider); };
                        profileCard.Controls.Add(providerName);
                        profileCard.Controls.Add(providerSetup);
                        profileCard.Controls.Add(hotkeyLabel);
                        profileCard.Controls.Add(shortcutBox);
                        profileCard.Controls.Add(triggerLabel);
                        profileCard.Controls.Add(triggerBox);
                        profileCard.Controls.Add(reset);
                        profileCard.Controls.Add(test);
                        profileCard.Controls.Add(help);
                        var testNote = NewLabel("测试时应看到工具开始听写，约一秒后自动结束；不会发送遥控器音频。", 9f, FontStyle.Regular, muted);
                        testNote.Location = new Point(6, 410);
                        testNote.Size = new Size(590, 42);
                        pageContent.Controls.Add(profileCard);
                        pageContent.Controls.Add(testNote);
                    }
                    else
                    {
                        if (!IsCapturing)
                        {
                            StartKeyboardBridge();
                            StartCapture();
                        }
                        SessionHealth latest = GetLatestSessionHealth();
                        if (firstDictationBaselineGeneration == 0) firstDictationBaselineGeneration = latest.Generation;
                        var phrase = NewLabel("建议说：测试麦克风，一二三四五六，期待效果。", 9.4f, FontStyle.Bold, violet);
                        phrase.Location = new Point(4, 102);
                        phrase.Size = new Size(590, 28);
                        var testInput = new TextBox();
                        testInput.Location = new Point(4, 140);
                        testInput.Size = new Size(602, 122);
                        testInput.Multiline = true;
                        testInput.BorderStyle = BorderStyle.FixedSingle;
                        testInput.Font = new Font("Microsoft YaHei UI", 12f);
                        testInput.BackColor = Color.White;
                        firstDictationStatus = NewLabel(firstDictationSucceeded ? "●  首次听写成功，已经可以开始使用" : "●  点击上方输入框，然后按住遥控器录音键", 10f, FontStyle.Bold,
                            firstDictationSucceeded ? green : violet);
                        firstDictationStatus.Location = new Point(4, 286);
                        firstDictationStatus.Size = new Size(596, 34);
                        var privacyNote = NewLabel("成功状态只来自本地链路日志；言灵不会读取、保存或上传输入框中的文字。完成后可随时在“连接与自检”一键排查。", 8.9f, FontStyle.Regular, muted);
                        privacyNote.Location = new Point(4, 330);
                        privacyNote.Size = new Size(590, 42);
                        var retry = SecondaryButton("重新检查链路", new Point(4, 396), new Size(146, 40));
                        retry.Click += delegate
                        {
                            SessionHealth current = GetLatestSessionHealth();
                            firstDictationBaselineGeneration = current.Generation;
                            firstDictationSucceeded = false;
                            if (!IsCapturing) StartCapture();
                            firstDictationStatus.Text = "●  已就绪，请按住遥控器录音键开始";
                            firstDictationStatus.ForeColor = violet;
                            testInput.Focus();
                        };
                        pageContent.Controls.Add(phrase);
                        pageContent.Controls.Add(testInput);
                        pageContent.Controls.Add(firstDictationStatus);
                        pageContent.Controls.Add(privacyNote);
                        pageContent.Controls.Add(retry);
                        testInput.Focus();
                    }
                };

                Action completeSetup = delegate
                {
                    ApplyStableVoiceProfile(config);
                    config.setupCompleted = true;
                    config.onboardingVersion = CurrentOnboardingVersion;
                    config.launchAtStartup = startupChoiceValue;
                    config.startBridgeOnLaunch = true;
                    config.minimizeToTray = true;
                    config.autoRouteVirtualMicrophone = autoRouteChoiceValue;
                    SetLaunchAtStartup(startupChoiceValue);
                    SaveConfig();
                    wizard.DialogResult = DialogResult.OK;
                    wizard.Close();
                };

                back.Click += delegate { if (currentStep > 0) renderStep(currentStep - 1); };
                next.Click += delegate
                {
                    if (currentStep == 0)
                    {
                        selectedHotkey = DefaultHotkeyForProvider(selectedProvider);
                        selectedTrigger = DefaultTriggerForProvider(selectedProvider);
                        renderStep(1);
                    }
                    else if (currentStep == 1)
                    {
                        if (!HasCableInput() || !HasCableOutput())
                        {
                            showWizardFeedback("请先准备两个 CABLE 端点", false);
                            return;
                        }
                        renderStep(2);
                    }
                    else if (currentStep == 2)
                    {
                        if (!IsCapturing || !bridgeReady)
                        {
                            showWizardFeedback("语音链路还未就绪", false);
                            return;
                        }
                        renderStep(3);
                    }
                    else if (currentStep == 3)
                    {
                        selectedHotkey = shortcutBox == null ? selectedHotkey : shortcutBox.Text.Trim();
                        selectedTrigger = triggerBox != null && triggerBox.SelectedIndex == 1 ? "hold" : "toggle";
                        if (!IsValidTranscriptionHotkey(selectedHotkey))
                        {
                            showWizardFeedback("快捷键格式不正确", false);
                            return;
                        }
                        SaveWizardProviderConfig(selectedProvider, selectedHotkey, selectedTrigger, autoRouteChoiceValue);
                        SessionHealth current = GetLatestSessionHealth();
                        firstDictationBaselineGeneration = current.Generation;
                        renderStep(4);
                    }
                    else
                    {
                        if (!firstDictationSucceeded)
                        {
                            showWizardFeedback("请先完成一次真实听写", false);
                            return;
                        }
                        completeSetup();
                    }
                };

                var wizardTimer = new System.Windows.Forms.Timer();
                wizardTimer.Interval = 400;
                wizardTimer.Tick += delegate
                {
                    if (currentStep == 2 && liveConnectionStatus != null && !liveConnectionStatus.IsDisposed)
                    {
                        liveConnectionStatus.Text = IsCapturing && bridgeReady ? "●  已连接，可以使用" : IsCapturing ? "●  正在建立语音链路" : "●  等待开始检测";
                        liveConnectionStatus.ForeColor = IsCapturing && bridgeReady ? green : IsCapturing ? amber : muted;
                        if (IsCapturing && bridgeReady) showWizardFeedback("连接成功", true);
                        else if (IsCapturing) showWizardInfo("正在检测，请稍候");
                    }
                    if (currentStep == 4 && firstDictationStatus != null && !firstDictationStatus.IsDisposed)
                    {
                        SessionHealth health = GetLatestSessionHealth();
                        if (health.Generation <= firstDictationBaselineGeneration) return;
                        if (health.Success)
                        {
                            firstDictationSucceeded = true;
                            firstDictationStatus.Text = "●  首次听写成功 · 收音、传输和转写触发均正常";
                            firstDictationStatus.ForeColor = green;
                        }
                        else if (health.Failed)
                        {
                            firstDictationStatus.Text = "●  本次未完成 · " + health.NextAction;
                            firstDictationStatus.ForeColor = Color.FromArgb(202, 76, 76);
                        }
                        else
                        {
                            firstDictationStatus.Text = "●  正在听写，请自然说话，完成后松开录音键";
                            firstDictationStatus.ForeColor = violet;
                        }
                    }
                };
                wizardTimer.Start();
                renderStep(0);
                DialogResult result = wizard.ShowDialog(this);
                wizardTimer.Stop();
                wizardTimer.Dispose();
                if (result == DialogResult.OK)
                {
                    StartKeyboardBridge();
                    if (!IsCapturing) StartCapture();
                    UpdateCaptureUi();
                    ShowToast("设置完成，言灵已经可以使用", "success");
                }
            }
        }
        finally { setupWizardOpen = false; }
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
        if (bridgeButton != null && !bridgeButton.IsDisposed) bridgeButton.Text = IsCapturing ? "管理语音桥接" : "启动语音桥接";
        if (DateTime.Now < transientFeedbackUntil && !string.IsNullOrWhiteSpace(transientFeedbackState))
        {
            ApplyVisualState(transientFeedbackState);
            return;
        }
        if (heroTitle != null && !heroTitle.IsDisposed)
            heroTitle.Text = !IsCapturing ? "语音桥接已暂停" : bridgeReady ? "已准备好" : "正在连接";
        if (heroSubtitle != null && !heroSubtitle.IsDisposed)
            heroSubtitle.Text = !IsCapturing ? "启动后，按住遥控器录音键即可在当前输入框听写" : bridgeReady ? "聚焦输入框，按住遥控器录音键开始说话" : "正在建立遥控器语音通道，请稍候";
        if (heroStateLabel != null && !heroStateLabel.IsDisposed)
            heroStateLabel.Text = !IsCapturing ? "VOICE LINK OFF" : bridgeReady ? "READY" : "CONNECTING";
        connectionBadge.Text = !IsCapturing ? "●  语音已暂停" : bridgeReady ? "●  语音链路就绪" : "●  正在连接";
        connectionBadge.ForeColor = !IsCapturing ? muted : bridgeReady ? green : amber;
        UpdateOverviewStatus();
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
        else if (state == "completed")
        {
            accent = green;
            surface = Color.FromArgb(236, 250, 244);
            voiceText = "●  听写已完成 · 文字已交给转写工具";
        }
        else if (state == "processing")
        {
            accent = cyan;
            surface = Color.FromArgb(239, 249, 252);
            voiceText = "●  录音已结束 · 正在整理并回填文字";
        }
        else if (state == "error")
        {
            accent = Color.FromArgb(202, 76, 76);
            surface = Color.FromArgb(255, 242, 242);
            voiceText = "●  本次听写未完成 · 请打开诊断查看原因";
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
        currentVisualAccent = accent;
        currentVisualState = state;
        if (heroStateLabel != null && !heroStateLabel.IsDisposed) heroStateLabel.ForeColor = accent;
        if (heroPanel != null && !heroPanel.IsDisposed)
        {
            heroPanel.BackColor = surface;
            heroPanel.BorderColor = Color.FromArgb(92, accent);
            heroPanel.Invalidate();
        }
        if (remoteVisual != null && !remoteVisual.IsDisposed)
        {
            remoteVisual.AccentColor = accent;
            remoteVisual.IsActive = state != "stopped";
            remoteVisual.IsRecording = state == "recording";
            if (state == "recording")
            {
                remoteVisual.HighlightedControl = "voice";
                remoteHighlightUntil = DateTime.Now.AddSeconds(1);
            }
            else if (DateTime.Now >= remoteHighlightUntil)
            {
                remoteVisual.HighlightedControl = "";
            }
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
            PollRuntimeFeedback();
            PollInputFeedback();
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

    private SessionHealth GetLatestSessionHealth()
    {
        var health = new SessionHealth();
        health.Provider = NormalizeProviderKey(config.inputMethod);
        health.NextAction = "按住遥控器录音键完成一次测试";
        string path = Path.Combine(sessionDir, "vibe-mic-runtime.log");
        if (!File.Exists(path)) return health;

        string[] lines;
        try { lines = ReadLogTailLines(path, 512 * 1024); }
        catch { return health; }
        int searchStart = Math.Max(0, lines.Length - 2400);
        int sessionStart = -1;
        for (int i = lines.Length - 1; i >= searchStart; i--)
        {
            if (lines[i].IndexOf("REMOTE STREAM START session=", StringComparison.OrdinalIgnoreCase) < 0) continue;
            int generation;
            if (!int.TryParse(ExtractMetric(lines[i], "generation"), out generation)) continue;
            health.Generation = generation;
            int.TryParse(ExtractMetric(lines[i], "session"), out health.SessionId);
            TryParseRuntimeTimestamp(lines[i], out health.StartedAt);
            health.Started = true;
            sessionStart = i;
            break;
        }
        if (sessionStart < 0) return health;

        for (int i = sessionStart; i < lines.Length; i++)
        {
            string item = lines[i];
            if (i > sessionStart && item.IndexOf("REMOTE STREAM START session=", StringComparison.OrdinalIgnoreCase) >= 0) break;
            int itemGeneration;
            bool hasGeneration = int.TryParse(ExtractMetric(item, "generation"), out itemGeneration);
            if (hasGeneration && itemGeneration != health.Generation) continue;

            if (item.IndexOf("TRANSCRIPTION READY", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                health.Ready = true;
                health.Provider = NormalizeProviderKey(ExtractMetric(item, "provider"));
                int.TryParse(ExtractMetric(item, "trigger_to_ready_ms"), out health.TriggerToReadyMs);
            }
            else if (item.IndexOf("DEFAULT CAPTURE ROUTE ACQUIRED", StringComparison.OrdinalIgnoreCase) >= 0) health.RouteAcquired = true;
            else if (item.IndexOf("DEFAULT CAPTURE ROUTE RESTORED", StringComparison.OrdinalIgnoreCase) >= 0) health.RouteRestored = true;
            else if (item.IndexOf("DEFAULT CAPTURE ROUTE RESTORE PENDING", StringComparison.OrdinalIgnoreCase) >= 0) health.RouteRestorePending = true;
            else if (item.IndexOf("REMOTE STREAM STOP session=", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                health.StreamStopped = true;
                int.TryParse(ExtractMetric(item, "audio_ms"), out health.AudioMs);
                int.TryParse(ExtractMetric(item, "max_gap_ms"), out health.MaxGapMs);
                int.TryParse(ExtractMetric(item, "queue_drops"), out health.QueueDrops);
                int.TryParse(ExtractMetric(item, "sink_queue_drops"), out health.SinkQueueDrops);
                double.TryParse(ExtractMetric(item, "raw_rms_pct"), NumberStyles.Float, CultureInfo.InvariantCulture, out health.RawRmsPercent);
                double.TryParse(ExtractMetric(item, "output_rms_pct"), NumberStyles.Float, CultureInfo.InvariantCulture, out health.OutputRmsPercent);
            }
            else if (item.IndexOf("VIRTUAL MIC DRAIN COMPLETE", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                health.Drained = true;
                int.TryParse(ExtractMetric(item, "pending_after"), out health.PendingAfterDrain);
                int.TryParse(ExtractMetric(item, "waited_ms"), out health.DrainWaitMs);
            }

            bool end = item.IndexOf("WETYPE SESSION END", StringComparison.OrdinalIgnoreCase) >= 0 ||
                item.IndexOf("TRANSCRIPTION SESSION END", StringComparison.OrdinalIgnoreCase) >= 0;
            if (end)
            {
                health.Completed = true;
                health.AudioDelivered = item.IndexOf("audio_delivered=True", StringComparison.OrdinalIgnoreCase) >= 0;
                TryParseRuntimeTimestamp(item, out health.EndedAt);
            }
            if (item.IndexOf("AUDIO LIVE FAILED", StringComparison.OrdinalIgnoreCase) >= 0 ||
                item.IndexOf("SESSION ERROR", StringComparison.OrdinalIgnoreCase) >= 0 ||
                item.IndexOf("DEFAULT CAPTURE ROUTE FAILED", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                health.Failed = true;
                health.Error = item.Length > 180 ? item.Substring(0, 180) : item;
            }
        }

        health.Success = health.Completed && health.AudioDelivered && health.StreamStopped && !health.Failed;
        if (health.Failed) health.NextAction = "打开诊断记录并复制问题摘要";
        else if (!health.Ready) health.NextAction = "转写工具没有进入听写状态，请先测试工具快捷键";
        else if (!health.StreamStopped) health.NextAction = "仍在听写；说完后松开录音键并等待完成";
        else if (health.AudioMs > 0 && health.AudioMs < 700) health.NextAction = "按住时间太短，请完整说完一句话后再松开";
        else if (health.QueueDrops > 0 || health.SinkQueueDrops > 0) health.NextAction = "音频队列出现丢包，请重新连接蓝牙后再试";
        else if (health.OutputRmsPercent > 0 && health.OutputRmsPercent < 0.8) health.NextAction = "声音偏小，请靠近遥控器麦克风并自然说话";
        else if (health.MaxGapMs > 250) health.NextAction = "蓝牙音频间隔偏大，请减少距离或重新连接遥控器";
        else if (config.autoRouteVirtualMicrophone && !health.RouteAcquired) health.NextAction = "没有切换到 CABLE Output，请重新检测本地音频通道";
        else if (health.Success && health.RouteRestorePending) health.NextAction = "文字已送出，但请检查 Windows 默认麦克风是否已恢复";
        else if (health.Success) health.NextAction = "链路正常，可以继续使用";
        else if (health.Completed && !health.AudioDelivered) health.NextAction = "转写工具未接收音频，请检查快捷键和触发方式";
        return health;
    }

    private string BuildSessionHealthSummary()
    {
        SessionHealth health = GetLatestSessionHealth();
        var result = new StringBuilder();
        if (!health.Started)
        {
            result.AppendLine("最近一次听写：尚无测试记录");
            result.AppendLine("下一步：聚焦输入框，按住遥控器录音键说一句完整的话。 ");
            return result.ToString();
        }

        string state = health.Success ? "成功" : health.Failed ? "失败" : health.Completed ? "需要检查" : "进行中";
        result.AppendLine("最近一次听写：" + state + "  ·  会话 #" + health.Generation);
        result.AppendLine("转写工具：" + ProviderDisplayName(health.Provider));
        result.AppendLine("录音时长：" + FormatMillisecondsAsSeconds(health.AudioMs) +
            "  ·  工具响应：" + FormatMilliseconds(health.TriggerToReadyMs) +
            "  ·  输出电平：" + FormatPercent(health.OutputRmsPercent));
        result.AppendLine("蓝牙最大间隔：" + FormatMilliseconds(health.MaxGapMs) +
            "  ·  音频丢包：" + Math.Max(0, health.QueueDrops + health.SinkQueueDrops) +
            "  ·  排空：" + (health.Drained ? FormatMilliseconds(health.DrainWaitMs) : "等待中"));
        result.AppendLine("麦克风路由：" + (!config.autoRouteVirtualMicrophone ? "手动" : health.RouteAcquired ? "已切换到 CABLE Output" : "未确认切换") +
            "  ·  恢复：" + (!config.autoRouteVirtualMicrophone ? "不适用" : health.RouteRestored ? "已恢复" : health.RouteRestorePending ? "待确认" : "等待中"));
        result.AppendLine("结论：" + health.NextAction);
        return result.ToString();
    }

    private string BuildProblemSummary()
    {
        var result = new StringBuilder();
        result.AppendLine(DisplayProductName + " 问题摘要");
        result.AppendLine("版本：" + ProductRelease + "  ·  时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        result.AppendLine("系统：" + Environment.OSVersion.VersionString);
        result.AppendLine("转写工具：" + ProviderDisplayName(config.inputMethod) + "  ·  快捷键：" + config.inputMethodHotkey + "  ·  " + (config.inputMethodTrigger == "hold" ? "按住触发" : "单击切换"));
        result.AppendLine("语音桥接：" + (IsCapturing ? "运行中" : "已暂停") + "  ·  遥控器语音：" + (bridgeReady ? "已就绪" : "未就绪"));
        result.AppendLine("VB-CABLE：Input " + (HasCableInput() ? "已检测" : "未检测") + " / Output " + (HasCableOutput() ? "已检测" : "未检测"));
        result.AppendLine("稳定语音档案：" + (HasStableVoiceProfile(config) ? "v" + StableVoiceProfileVersion + " 已应用" : "参数已自定义"));
        SelfCheckReport selfCheck = BuildSelfCheckReport();
        result.AppendLine("自检：通过 " + selfCheck.PassedCount + " / " + selfCheck.Items.Count + "  ·  建议 " + selfCheck.WarningCount + "  ·  待修复 " + selfCheck.FailedCount);
        result.AppendLine();
        result.Append(BuildSessionHealthSummary());
        result.AppendLine("隐私：此摘要不包含录音、识别文字、蓝牙地址或完整设备路径。 ");
        return result.ToString();
    }

    private static string FormatMilliseconds(int value)
    {
        return value > 0 ? value + " ms" : "--";
    }

    private static string FormatMillisecondsAsSeconds(int value)
    {
        return value > 0 ? (value / 1000.0).ToString("0.0") + " 秒" : "--";
    }

    private static string FormatPercent(double value)
    {
        return value > 0 ? value.ToString("0.0") + "%" : "--";
    }

    private SelfCheckReport BuildSelfCheckReport()
    {
        var report = new SelfCheckReport();
        string currentRuntime = ReadCurrentRuntimeSegment();
        bool stableRuntime = !IsCapturing || string.IsNullOrWhiteSpace(currentRuntime) ||
            currentRuntime.IndexOf("voice_state_machine=v11", StringComparison.OrdinalIgnoreCase) >= 0;
        bool componentsReady = File.Exists(Path.Combine(root, "VibeMicAtvvCapture.exe")) &&
            File.Exists(Path.Combine(root, "VoxDeckInputBridge.exe")) &&
            File.Exists(Path.Combine(root, "NAudio.Core.dll")) &&
            File.Exists(Path.Combine(root, "NAudio.Wasapi.dll")) && stableRuntime;
        report.Items.Add(new SelfCheckItem("components", "本地核心组件",
            componentsReady ? "pass" : "fail",
            componentsReady ? "语音捕获、按键桥接与 WASAPI 运行库完整，语音状态机为 v11" :
                !stableRuntime ? "当前捕获组件不是已验证的 v11，请重新安装完整发布包" : "安装目录缺少必要组件，请重新解压完整发布包",
            componentsReady ? "" : "重新下载", componentsReady ? "" : "download-release"));

        bool cableInput = HasCableInput();
        bool cableOutput = HasCableOutput();
        bool cableReady = cableInput && cableOutput;
        report.Items.Add(new SelfCheckItem("cable", "VB-CABLE 本地音频通道",
            cableReady ? "pass" : "fail",
            cableReady ? "CABLE Input（播放）与 CABLE Output（录音）均已检测到" :
                "缺少 " + (!cableInput && !cableOutput ? "CABLE Input 和 CABLE Output" : !cableInput ? "CABLE Input" : "CABLE Output") + "，遥控器声音无法交给转写工具",
            cableReady ? "" : "安装驱动", cableReady ? "" : "install-cable"));

        bool stableProfile = HasStableVoiceProfile(config);
        bool configuredEndpointReady = string.Equals(config.audioEndpointName, StableVoiceEndpoint, StringComparison.OrdinalIgnoreCase);
        report.Items.Add(new SelfCheckItem("profile", "已验证稳定语音档案 v" + StableVoiceProfileVersion,
            stableProfile ? "pass" : configuredEndpointReady ? "warning" : "fail",
            stableProfile ? "1.0× · 清晰增强 · 180 ms 排空 · 自动切换并恢复麦克风" :
                !configuredEndpointReady ? "播放端点不是 CABLE Input，遥控器音频无法进入对应的 CABLE Output" : "当前参数已偏离真机验证基线，可能影响灵敏度、延迟或端点恢复",
            stableProfile ? "" : "恢复稳定参数", stableProfile ? "" : "restore-profile"));

        bool keyboardRunning = IsProcessRunning("VoxDeckInputBridge");
        bool servicesReady = IsCapturing && keyboardRunning;
        report.Items.Add(new SelfCheckItem("services", "后台桥接服务",
            servicesReady ? "pass" : "fail",
            servicesReady ? "语音桥接与遥控器按键桥接均在运行" :
                (!IsCapturing && !keyboardRunning ? "语音与按键桥接均未运行" : !IsCapturing ? "语音桥接未运行" : "按键桥接未运行"),
            servicesReady ? "" : "启动桥接", servicesReady ? "" : "start-bridge"));

        bool bleConnected = currentRuntime.IndexOf("status=Connected", StringComparison.OrdinalIgnoreCase) >= 0;
        bool atvvReady = bridgeReady || currentRuntime.IndexOf("ATVV READY", StringComparison.OrdinalIgnoreCase) >= 0;
        bool remoteReady = atvvReady && (bleConnected || bridgeReady);
        report.Items.Add(new SelfCheckItem("remote", "RC003 蓝牙与麦克风",
            remoteReady ? "pass" : IsCapturing ? "warning" : "fail",
            remoteReady ? "遥控器已连接，ATVV 16 kHz 麦克风服务已就绪" :
                IsCapturing ? "正在等待遥控器麦克风；请按方向键唤醒后重新自检" : "尚未建立 RC003 语音连接",
            remoteReady ? "" : "蓝牙设置", remoteReady ? "" : "bluetooth"));

        string provider = NormalizeProviderKey(config.inputMethod);
        bool validHotkey = IsValidTranscriptionHotkey(config.inputMethodHotkey);
        bool customProvider = provider == "custom";
        bool providerRunning = IsProviderRunning(provider);
        bool providerTimingReady = customProvider || config.providerStartupDelayMs == DefaultStartupDelayForProvider(provider);
        string providerState = !validHotkey ? "fail" : customProvider ? "warning" : providerRunning && providerTimingReady ? "pass" : "warning";
        string providerDetail = !validHotkey ? "快捷键格式无效，无法启动转写" :
            customProvider ? "自定义工具无法自动识别，请确认客户端已启动且快捷键一致" :
            !providerTimingReady ? ProviderDisplayName(provider) + " 的启动等待已偏离推荐值，请恢复所选工具配置" :
            providerRunning ? ProviderDisplayName(provider) + " 已运行 · " + config.inputMethodHotkey.Replace("+", " + ") + " · " + (config.inputMethodTrigger == "hold" ? "按住触发" : "单击切换") :
            "未检测到 " + ProviderDisplayName(provider) + " 客户端，请先启动或检查工具设置";
        report.Items.Add(new SelfCheckItem("provider", "转写工具与快捷键", providerState, providerDetail,
            providerState == "pass" ? "" : "检查配置", providerState == "pass" ? "" : "provider"));

        SessionHealth health = GetLatestSessionHealth();
        string sessionState;
        string sessionDetail;
        string sessionAction = "";
        string sessionActionText = "";
        if (!health.Started)
        {
            sessionState = "warning";
            sessionDetail = "尚无真实听写记录；需要按住录音键说一句话才能验证完整链路";
            sessionAction = "test-dictation";
            sessionActionText = "开始测试";
        }
        else
        {
            bool routeHealthy = !config.autoRouteVirtualMicrophone ||
                (health.RouteAcquired && health.RouteRestored && !health.RouteRestorePending);
            bool transportHealthy = health.QueueDrops == 0 && health.SinkQueueDrops == 0 &&
                health.MaxGapMs <= 250 && health.PendingAfterDrain == 0 && health.Drained;
            bool levelHealthy = health.OutputRmsPercent >= 0.8;
            bool timingHealthy = health.TriggerToReadyMs <= 1500;
            if (health.Failed || (health.Completed && (!routeHealthy || !transportHealthy))) sessionState = "fail";
            else if (health.Success && levelHealthy && timingHealthy) sessionState = "pass";
            else sessionState = "warning";
            sessionDetail = "响应 " + FormatMilliseconds(health.TriggerToReadyMs) + " · 输出 " + FormatPercent(health.OutputRmsPercent) +
                " · 蓝牙间隔 " + FormatMilliseconds(health.MaxGapMs) + " · 丢包 " + Math.Max(0, health.QueueDrops + health.SinkQueueDrops) +
                (health.Success ? " · 音频已送达" : " · " + health.NextAction);
            if (sessionState != "pass")
            {
                sessionAction = "test-dictation";
                sessionActionText = "重新测试";
            }
        }
        report.Items.Add(new SelfCheckItem("session", "最近一次端到端听写", sessionState, sessionDetail, sessionActionText, sessionAction));

        foreach (SelfCheckItem item in report.Items)
        {
            if (item.State == "pass") report.PassedCount++;
            else if (item.State == "fail") report.FailedCount++;
            else report.WarningCount++;
        }
        if (report.FailedCount > 0)
        {
            report.Headline = "发现 " + report.FailedCount + " 项需要处理";
            report.Detail = "按检查项右侧提示逐项修复；所有检测只在本机运行，不读取转写文字。";
        }
        else if (report.WarningCount > 0)
        {
            report.Headline = "核心链路可用，还有 " + report.WarningCount + " 项建议确认";
            report.Detail = "完成一次真实听写并保持稳定档案，可获得最可靠的发布版体验。";
        }
        else
        {
            report.Headline = "全部通过，可以稳定听写";
            report.Detail = "蓝牙、RC003、虚拟音频、转写工具与最近会话均处于健康状态。";
        }
        return report;
    }

    private string ReadCurrentRuntimeSegment()
    {
        string path = Path.Combine(sessionDir, "vibe-mic-runtime.log");
        if (!File.Exists(path)) return "";
        string[] lines;
        try { lines = ReadLogTailLines(path, 256 * 1024); }
        catch { return ""; }
        int start = 0;
        for (int i = lines.Length - 1; i >= 0; i--)
        {
            if (lines[i].IndexOf(" START endpoint=", StringComparison.OrdinalIgnoreCase) < 0) continue;
            start = i;
            break;
        }
        return string.Join(Environment.NewLine, lines, start, lines.Length - start);
    }

    private void AddSelfCheckRow(Control parent, SelfCheckItem item, int y)
    {
        var row = new Panel();
        row.Location = new Point(18, y);
        row.Size = new Size(584, 54);
        row.BackColor = Color.Transparent;
        row.Paint += delegate(object sender, PaintEventArgs e)
        {
            using (var pen = new Pen(Color.FromArgb(232, 236, 245))) e.Graphics.DrawLine(pen, 0, row.Height - 1, row.Width, row.Height - 1);
        };
        Color statusColor = item.State == "pass" ? green : item.State == "fail" ? coral : amber;
        var mark = NewLabel(item.State == "pass" ? "✓" : item.State == "fail" ? "!" : "·", 10f, FontStyle.Bold, Color.White);
        mark.Location = new Point(4, 12);
        mark.Size = new Size(28, 28);
        mark.TextAlign = ContentAlignment.MiddleCenter;
        mark.BackColor = statusColor;
        ApplyRoundedRegion(mark, 14);
        var title = NewLabel(item.Title, 9.2f, FontStyle.Bold, ink);
        title.Location = new Point(46, 5);
        title.Size = new Size(420, 23);
        var detail = NewLabel(item.Detail, 8.05f, FontStyle.Regular, muted);
        detail.Location = new Point(46, 28);
        detail.Size = new Size(string.IsNullOrEmpty(item.Action) ? 518 : 420, 21);
        detail.AutoEllipsis = true;
        row.Controls.Add(mark);
        row.Controls.Add(title);
        row.Controls.Add(detail);
        if (!string.IsNullOrEmpty(item.Action))
        {
            var action = SecondaryButton(item.ActionText, new Point(472, 9), new Size(108, 34));
            action.Font = new Font("Microsoft YaHei UI", 8.4f, FontStyle.Bold);
            action.Click += delegate { HandleSelfCheckAction(item.Action); };
            row.Controls.Add(action);
        }
        parent.Controls.Add(row);
    }

    private void HandleSelfCheckAction(string action)
    {
        if (action == "setup") ShowSetupWizard();
        else if (action == "download-release") OpenUri("https://github.com/richlearntodo-debug/vibe-flow/releases/latest");
        else if (action == "install-cable") OpenUri("https://vb-audio.com/Cable/");
        else if (action == "restore-profile")
        {
            ApplyStableVoiceProfile(config);
            SaveConfig();
            RestartCaptureForAudioSettings();
            ShowPage(3);
            ShowToast("已恢复真机验证的稳定语音参数 v" + StableVoiceProfileVersion, "success");
        }
        else if (action == "start-bridge")
        {
            StartKeyboardBridge();
            if (!IsCapturing) StartCapture();
            ShowToast("正在启动后台桥接，请稍候后重新自检", "info");
        }
        else if (action == "bluetooth") OpenUri("ms-settings:bluetooth");
        else if (action == "provider") ShowPage(1);
        else if (action == "test-dictation")
        {
            ShowPage(0);
            ShowToast("聚焦任意输入框，按住遥控器录音键说完一句话后松开", "info");
        }
    }

    private void RunSelfCheckAndRefresh()
    {
        SelfCheckReport report = BuildSelfCheckReport();
        ShowPage(3);
        if (report.FailedCount == 0 && report.WarningCount == 0)
        {
            ShowToast("自检全部通过，语音链路状态良好", "success");
            if (config.soundFeedbackEnabled) PlayFeedbackSound(true);
        }
        else
        {
            ShowToast(report.FailedCount > 0 ? "自检发现待修复项目" : "自检完成，还有建议确认项",
                report.FailedCount > 0 ? "error" : "info");
        }
    }

    private static bool IsProcessRunning(string processName)
    {
        Process[] processes = Process.GetProcessesByName(processName);
        bool running = processes.Length > 0;
        foreach (Process process in processes) process.Dispose();
        return running;
    }

    private static string[] ReadLogTailLines(string path, int maximumBytes)
    {
        var lines = new List<string>();
        if (!File.Exists(path)) return lines.ToArray();
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        {
            long start = Math.Max(0, stream.Length - maximumBytes);
            stream.Position = start;
            using (var reader = new StreamReader(stream, Encoding.UTF8, true, 4096, false))
            {
                if (start > 0) reader.ReadLine();
                string item;
                while ((item = reader.ReadLine()) != null) lines.Add(item);
            }
        }
        return lines.ToArray();
    }

    private void UpdateSessionConfidence()
    {
        if (activityLabel == null || activityLabel.IsDisposed) return;
        string path = Path.Combine(sessionDir, "vibe-mic-runtime.log");
        if (!File.Exists(path)) return;
        string[] lines = ReadLogTailLines(path, 256 * 1024);
        int startIndex = -1;
        int stopIndex = -1;
        for (int i = Math.Max(0, lines.Length - 200); i < lines.Length; i++)
        {
            if (lines[i].IndexOf("REMOTE STREAM START session=", StringComparison.OrdinalIgnoreCase) >= 0) startIndex = i;
            if (lines[i].IndexOf("REMOTE STREAM STOP session=", StringComparison.OrdinalIgnoreCase) >= 0) stopIndex = i;
        }

        if (IsCapturing && startIndex > stopIndex)
        {
            DateTime parsed;
            if (TryParseRuntimeTimestamp(lines[startIndex], out parsed)) activeStreamStarted = parsed;
            TimeSpan elapsed = activeStreamStarted == DateTime.MinValue ? TimeSpan.Zero : DateTime.Now - activeStreamStarted;
            activityLabel.Text = "●  正在听写  " + Math.Max(0, (int)elapsed.TotalMinutes).ToString("00") + ":" + Math.Max(0, elapsed.Seconds).ToString("00") + "  ·  遥控器收音中";
            activityLabel.ForeColor = violet;
            if (heroTitle != null && !heroTitle.IsDisposed) heroTitle.Text = "正在听写";
            if (heroSubtitle != null && !heroSubtitle.IsDisposed) heroSubtitle.Text = "请自然说话，松开录音键后由 " + ProviderDisplayName(config.inputMethod) + " 整理文字";
            if (heroStateLabel != null && !heroStateLabel.IsDisposed) heroStateLabel.Text = "LISTENING";
            ApplyVisualState("recording");
            return;
        }

        activeStreamStarted = DateTime.MinValue;
        if (DateTime.Now < transientFeedbackUntil && !string.IsNullOrWhiteSpace(transientFeedbackState))
        {
            ApplyVisualState(transientFeedbackState);
            if (activityLabel != null && !activityLabel.IsDisposed)
            {
                activityLabel.Text = transientFeedbackText;
                activityLabel.ForeColor = transientFeedbackState == "error" ? Color.FromArgb(202, 76, 76) : green;
            }
            return;
        }
        transientFeedbackState = "";
        transientFeedbackText = "";
        UpdateCaptureUi();
        if (stopIndex >= 0)
        {
            string duration = ExtractMetric(lines[stopIndex], "audio_ms");
            string level = ExtractMetric(lines[stopIndex], "output_rms_pct");
            int milliseconds;
            string seconds = int.TryParse(duration, out milliseconds) ? (milliseconds / 1000.0).ToString("0.0") : "--";
            activityLabel.Text = "上一段听写 " + seconds + " 秒  ·  输出电平 " + (string.IsNullOrWhiteSpace(level) ? "--" : level) + "%";
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

    private void HostLog(string message)
    {
        if (InvokeRequired) { BeginInvoke(new Action<string>(HostLog), message); return; }
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        string lineText = timestamp.Substring(11, 8) + " " + message;
        if (logBox != null && !logBox.IsDisposed) logBox.AppendText(lineText + Environment.NewLine);
        try { File.AppendAllText(hostLogPath, timestamp + " " + message + Environment.NewLine, new UTF8Encoding(false)); }
        catch { }
    }

    private string LoadRecentDiagnostics()
    {
        var sb = new StringBuilder();
        sb.AppendLine("最近技术事件（提交问题时通常只需复制上方摘要）");
        if (File.Exists(hostLogPath))
        {
            sb.AppendLine("主程序启动与恢复：");
            string[] hostLines = ReadLogTailLines(hostLogPath, 64 * 1024);
            int hostStart = Math.Max(0, hostLines.Length - 16);
            for (int i = hostStart; i < hostLines.Length; i++) sb.AppendLine(hostLines[i]);
            sb.AppendLine();
        }
        string runtimeLog = Path.Combine(sessionDir, "vibe-mic-runtime.log");
        if (File.Exists(runtimeLog))
        {
            string[] lines = ReadLogTailLines(runtimeLog, 128 * 1024);
            int start = Math.Max(0, lines.Length - 30);
            for (int i = start; i < lines.Length; i++) sb.AppendLine(lines[i]);
        }
        string inputLog = Path.Combine(root, "input-bridge-log.txt");
        if (File.Exists(inputLog))
        {
            sb.AppendLine();
            sb.AppendLine("最近遥控器按键事件：");
            string[] lines = ReadLogTailLines(inputLog, 64 * 1024);
            int start = Math.Max(0, lines.Length - 18);
            for (int i = start; i < lines.Length; i++) sb.AppendLine(lines[i]);
        }
        return sb.ToString();
    }

    private void OpenLogFolder()
    {
        try
        {
            Directory.CreateDirectory(sessionDir);
            Process.Start(new ProcessStartInfo(sessionDir) { UseShellExecute = true });
            ShowToast("已打开本地日志文件夹", "success");
        }
        catch (Exception ex)
        {
            Log("Open log folder failed: " + ex.Message);
            ShowToast("无法打开日志文件夹", "error");
        }
    }

    private void Toast(string text) { ShowToast(text, "info"); }

    private void ShowToast(string text, string kind)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<string, string>(ShowToast), text, kind);
            return;
        }
        if (heroSubtitle != null && !heroSubtitle.IsDisposed) heroSubtitle.Text = text;
        if (toastPanel == null || toastPanel.IsDisposed) return;
        Color accent = kind == "error" ? Color.FromArgb(202, 76, 76) : kind == "success" ? green : violet;
        toastIcon.Text = kind == "error" ? "\uEA39" : kind == "success" ? "\uE73E" : "\uE946";
        toastIcon.ForeColor = accent;
        toastPanel.BorderColor = Color.FromArgb(accent.R, accent.G, accent.B);
        toastPanel.BackColor = kind == "error" ? Color.FromArgb(255, 247, 247) : kind == "success" ? Color.FromArgb(244, 252, 248) : Color.White;
        toastLabel.Text = text;
        toastPanel.Visible = true;
        toastPanel.BringToFront();
        toastTimer.Stop();
        toastTimer.Start();
    }

    private void InitializeFeedbackSounds()
    {
        try
        {
            dictationCompleteSound = CreateFeedbackWave(true);
            dictationErrorSound = CreateFeedbackWave(false);
            dictationCompletePlayer = new SoundPlayer(dictationCompleteSound);
            dictationErrorPlayer = new SoundPlayer(dictationErrorSound);
            dictationCompletePlayer.Load();
            dictationErrorPlayer.Load();
        }
        catch
        {
            dictationCompletePlayer = null;
            dictationErrorPlayer = null;
        }
    }

    private static MemoryStream CreateFeedbackWave(bool success)
    {
        const int sampleRate = 22050;
        int durationMs = success ? 320 : 260;
        int sampleCount = sampleRate * durationMs / 1000;
        var stream = new MemoryStream(44 + sampleCount * 2);
        using (var writer = new BinaryWriter(stream, Encoding.ASCII, true))
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + sampleCount * 2);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(sampleRate);
            writer.Write(sampleRate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(sampleCount * 2);
            for (int i = 0; i < sampleCount; i++)
            {
                double elapsed = i * 1000.0 / sampleRate;
                double frequency = success ? (elapsed < 145 ? 660.0 : 880.0) : (elapsed < 130 ? 420.0 : 315.0);
                double attack = Math.Min(1.0, elapsed / 24.0);
                double release = Math.Min(1.0, (durationMs - elapsed) / 70.0);
                double envelope = Math.Max(0.0, Math.Min(attack, release));
                short sample = (short)(Math.Sin(2.0 * Math.PI * frequency * i / sampleRate) * 3600.0 * envelope);
                writer.Write(sample);
            }
        }
        stream.Position = 0;
        return stream;
    }

    private void PollRuntimeFeedback()
    {
        string path = Path.Combine(sessionDir, "vibe-mic-runtime.log");
        if (!File.Exists(path)) return;
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        {
            if (runtimeFeedbackPosition > stream.Length) runtimeFeedbackPosition = 0;
            if (runtimeFeedbackPosition == stream.Length) return;
            stream.Position = runtimeFeedbackPosition;
            using (var reader = new StreamReader(stream, Encoding.UTF8, true, 4096, true))
            {
                string lineText;
                while ((lineText = reader.ReadLine()) != null) HandleRuntimeFeedbackLine(lineText);
            }
            runtimeFeedbackPosition = stream.Position;
        }
    }

    private void HandleRuntimeFeedbackLine(string lineText)
    {
        int generation;
        int.TryParse(ExtractMetric(lineText, "generation"), out generation);
        if (lineText.IndexOf("ATVV READY", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            bridgeReady = true;
            reconnectAttempt = 0;
            startupRecoveryCount = 0;
            HostLog("CAPTURE READY startup_ms=" +
                (captureStartedAt == DateTime.MinValue ? "unknown" : ((int)(DateTime.Now - captureStartedAt).TotalMilliseconds).ToString()));
            UpdateCaptureUi();
            return;
        }
        if (lineText.IndexOf("REMOTE STREAM START ", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            transientFeedbackUntil = DateTime.MinValue;
            transientFeedbackState = "recording";
            transientFeedbackText = "正在听写 · 遥控器麦克风收音中";
            ApplyVisualState("recording");
            return;
        }
        if (lineText.IndexOf("REMOTE STREAM STOP session=", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            transientFeedbackUntil = DateTime.Now.AddSeconds(12);
            transientFeedbackState = "processing";
            transientFeedbackText = "录音已结束 · 正在整理并回填文字";
            if (heroTitle != null && !heroTitle.IsDisposed) heroTitle.Text = "正在整理文字";
            if (heroSubtitle != null && !heroSubtitle.IsDisposed) heroSubtitle.Text = transientFeedbackText;
            if (heroStateLabel != null && !heroStateLabel.IsDisposed) heroStateLabel.Text = "PROCESSING";
            ApplyVisualState("processing");
            return;
        }

        bool sessionEnd = lineText.IndexOf("WETYPE SESSION END", StringComparison.OrdinalIgnoreCase) >= 0 ||
            lineText.IndexOf("TRANSCRIPTION SESSION END", StringComparison.OrdinalIgnoreCase) >= 0;
        if (sessionEnd && generation > lastFeedbackGeneration)
        {
            lastFeedbackGeneration = generation;
            bool delivered = lineText.IndexOf("audio_delivered=True", StringComparison.OrdinalIgnoreCase) >= 0;
            SetSessionFeedback(delivered ? "completed" : "error",
                delivered ? "听写已完成，文字正在回填" : "本次听写没有送出音频");
            return;
        }

        bool sessionError = lineText.IndexOf("AUDIO LIVE FAILED", StringComparison.OrdinalIgnoreCase) >= 0 ||
            lineText.IndexOf("SESSION ERROR", StringComparison.OrdinalIgnoreCase) >= 0 ||
            lineText.IndexOf("DEFAULT CAPTURE ROUTE FAILED", StringComparison.OrdinalIgnoreCase) >= 0;
        if (sessionError && (generation <= 0 || generation > lastFeedbackGeneration))
        {
            if (generation > 0) lastFeedbackGeneration = generation;
            SetSessionFeedback("error", "本次听写未完成，请打开连接与自检");
        }
    }

    private void SetSessionFeedback(string state, string text)
    {
        transientFeedbackState = state;
        transientFeedbackText = text;
        transientFeedbackUntil = DateTime.Now.AddMilliseconds(state == "completed" ? 2200 : 3200);
        if (heroTitle != null && !heroTitle.IsDisposed) heroTitle.Text = state == "completed" ? "听写已完成" : "听写未完成";
        if (heroSubtitle != null && !heroSubtitle.IsDisposed) heroSubtitle.Text = text;
        if (heroStateLabel != null && !heroStateLabel.IsDisposed) heroStateLabel.Text = state == "completed" ? "COMPLETED" : "CHECK NEEDED";
        connectionBadge.Text = state == "completed" ? "●  听写已完成" : "●  需要检查";
        connectionBadge.ForeColor = state == "completed" ? green : Color.FromArgb(202, 76, 76);
        ApplyVisualState(state);
        UpdateOverviewStatus();
        ShowToast(text, state == "completed" ? "success" : "error");
        if (config.soundFeedbackEnabled)
            PlayFeedbackSound(state == "completed");
    }

    private void PlayFeedbackSound(bool success)
    {
        try
        {
            if (success && dictationCompletePlayer != null) dictationCompletePlayer.Play();
            else if (!success && dictationErrorPlayer != null) dictationErrorPlayer.Play();
        }
        catch { }
    }

    private void PollInputFeedback()
    {
        string path = Path.Combine(root, "input-bridge-log.txt");
        if (!File.Exists(path)) return;
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        {
            if (inputFeedbackPosition > stream.Length) inputFeedbackPosition = 0;
            if (inputFeedbackPosition < stream.Length)
            {
                stream.Position = inputFeedbackPosition;
                using (var reader = new StreamReader(stream, Encoding.UTF8, true, 4096, true))
                {
                    string lineText;
                    while ((lineText = reader.ReadLine()) != null) HandleInputFeedbackLine(lineText);
                }
                inputFeedbackPosition = stream.Position;
            }
        }
        if (remoteVisual != null && !remoteVisual.IsDisposed && DateTime.Now >= remoteHighlightUntil && !remoteVisual.IsRecording)
        {
            if (!string.IsNullOrEmpty(remoteVisual.HighlightedControl))
            {
                remoteVisual.HighlightedControl = "";
                remoteVisual.Invalidate();
            }
        }
    }

    private void HandleInputFeedbackLine(string lineText)
    {
        if (lineText.IndexOf(" DOWN", StringComparison.OrdinalIgnoreCase) < 0) return;
        string control = "";
        if (lineText.IndexOf("录音键 DOWN", StringComparison.OrdinalIgnoreCase) >= 0) control = "voice";
        else if (lineText.IndexOf("确认键 DOWN", StringComparison.OrdinalIgnoreCase) >= 0) control = "ok";
        else if (lineText.IndexOf("Home 键 DOWN", StringComparison.OrdinalIgnoreCase) >= 0) control = "home";
        else if (lineText.IndexOf("TV 键 DOWN", StringComparison.OrdinalIgnoreCase) >= 0) control = "tv";
        else if (lineText.IndexOf("功能键 DOWN", StringComparison.OrdinalIgnoreCase) >= 0) control = "menu";
        else if (lineText.IndexOf("vk=0x26", StringComparison.OrdinalIgnoreCase) >= 0) control = "up";
        else if (lineText.IndexOf("vk=0x28", StringComparison.OrdinalIgnoreCase) >= 0) control = "down";
        else if (lineText.IndexOf("vk=0x25", StringComparison.OrdinalIgnoreCase) >= 0) control = "left";
        else if (lineText.IndexOf("vk=0x27", StringComparison.OrdinalIgnoreCase) >= 0) control = "right";
        if (string.IsNullOrEmpty(control) || remoteVisual == null || remoteVisual.IsDisposed) return;
        remoteVisual.HighlightedControl = control;
        remoteHighlightUntil = DateTime.Now.AddMilliseconds(control == "voice" ? 900 : 520);
        remoteVisual.Invalidate();
    }

    private static void RotateLogFile(string path, long maximumBytes)
    {
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length <= maximumBytes) return;
            string oldest = path + ".2";
            string previous = path + ".1";
            if (File.Exists(oldest)) File.Delete(oldest);
            if (File.Exists(previous)) File.Move(previous, oldest);
            File.Move(path, previous);
        }
        catch { }
    }

    private static string NormalizeProviderKey(string value)
    {
        string provider = (value ?? "").Trim().ToLowerInvariant();
        if (provider == "wetype" || provider == "wechat") return "wechat";
        if (provider == "typeless") return "typeless";
        if (provider == "windows" || provider == "win+h") return "windows";
        if (provider == "voquill" || provider == "vokie") return "voquill";
        return provider == "custom" ? "custom" : "wechat";
    }

    private static string ProviderDisplayName(string provider)
    {
        switch (NormalizeProviderKey(provider))
        {
            case "typeless": return "Typeless";
            case "windows": return "Windows 语音输入";
            case "voquill": return "Voquill（开源）";
            case "custom": return "其他语音工具";
            default: return "微信输入法";
        }
    }

    private static string ProviderSummary(string provider)
    {
        switch (NormalizeProviderKey(provider))
        {
            case "typeless": return "适合跨应用长文本听写，可继续使用 Typeless 自己的润色、格式整理和词典能力。";
            case "windows": return "Windows 自带，无需安装额外客户端，适合快速开始和基础听写。";
            case "voquill": return "开源桌面听写工具，适合希望自行托管或进一步定制工作流的用户。";
            case "custom": return "连接任意支持全局快捷键启动和结束的本地语音输入工具。";
            default: return "适合中文输入与结构化整理。言灵优先调用已验证的微信输入法工具栏入口。";
        }
    }

    private static string ProviderSetupInstruction(string provider)
    {
        switch (NormalizeProviderKey(provider))
        {
            case "typeless": return "在 Typeless 设置中确认录音快捷键。常见默认值是 Right Alt，按一下开始、再按一下结束。";
            case "windows": return "Windows 语音输入使用 Win + H。首次使用时请先在任意输入框中手动按一次完成系统初始化。";
            case "voquill": return "在 Voquill 中确认 Push-to-talk 快捷键。当前开源 Windows 默认是 Ctrl + Win 按住触发。";
            case "custom": return "先在目标工具中设置一个不超过四个按键的全局快捷键，再把相同内容填写到这里。";
            default: return "先启动微信输入法，并确认工具栏麦克风可以手动打开。Ctrl + Win 仅作为工具栏不可用时的回退。";
        }
    }

    private static string ProviderShortcutDescription(string provider)
    {
        string trigger = DefaultTriggerForProvider(provider) == "hold" ? "按住触发" : "单击切换";
        return DefaultHotkeyForProvider(provider).Replace("+", " + ") + " · " + trigger;
    }

    private static string DefaultHotkeyForProvider(string provider)
    {
        switch (NormalizeProviderKey(provider))
        {
            case "typeless": return "rightalt";
            case "windows": return "win+h";
            case "voquill": return "ctrl+win";
            default: return "ctrl+win";
        }
    }

    private static string DefaultTriggerForProvider(string provider)
    {
        return NormalizeProviderKey(provider) == "voquill" ? "hold" : "toggle";
    }

    private static int DefaultStartupDelayForProvider(string provider)
    {
        switch (NormalizeProviderKey(provider))
        {
            case "windows": return 300;
            case "typeless": return 120;
            case "voquill": return 120;
            case "custom": return 150;
            default: return 80;
        }
    }

    private static int ProviderIndex(string provider)
    {
        switch (NormalizeProviderKey(provider))
        {
            case "typeless": return 1;
            case "windows": return 2;
            case "voquill": return 3;
            case "custom": return 4;
            default: return 0;
        }
    }

    private static string ProviderKeyFromIndex(int index)
    {
        return index == 1 ? "typeless" : index == 2 ? "windows" : index == 3 ? "voquill" : index == 4 ? "custom" : "wechat";
    }

    private static void ApplyProviderProfile(VibeMicConfig value, string provider)
    {
        value.inputMethod = NormalizeProviderKey(provider);
        value.inputMethodHotkey = DefaultHotkeyForProvider(value.inputMethod);
        value.inputMethodTrigger = DefaultTriggerForProvider(value.inputMethod);
        value.providerStartupDelayMs = DefaultStartupDelayForProvider(value.inputMethod);
    }

    private static bool IsValidTranscriptionHotkey(string shortcut)
    {
        if (string.IsNullOrWhiteSpace(shortcut)) return false;
        string[] parts = shortcut.Split(new char[] { '+', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts.Length > 4) return false;
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ctrl", "control", "leftctrl", "lctrl", "rightctrl", "rctrl", "win", "meta", "leftwin", "lwin",
            "rightwin", "rwin", "alt", "leftalt", "lalt", "rightalt", "ralt", "shift", "leftshift", "rightshift",
            "space", "enter", "tab", "escape", "esc"
        };
        foreach (string raw in parts)
        {
            string part = raw.Trim().ToLowerInvariant();
            if (names.Contains(part)) continue;
            if (part.Length == 1 && char.IsLetterOrDigit(part[0])) continue;
            int functionNumber;
            if (part.StartsWith("f") && int.TryParse(part.Substring(1), out functionNumber) && functionNumber >= 1 && functionNumber <= 24) continue;
            return false;
        }
        return true;
    }

    private bool IsProviderRunning(string provider)
    {
        switch (NormalizeProviderKey(provider))
        {
            case "wechat": return IsProcessRunning("WeType") || IsProcessRunning("WeTypeService") ||
                IsProcessRunning("wetype_server") || IsProcessRunning("wetype_service");
            case "typeless": return IsProcessRunning("Typeless");
            case "voquill": return IsProcessRunning("Voquill") || IsProcessRunning("voquill");
            case "windows": return true;
            default: return true;
        }
    }

    private string ProviderStatusText(string provider)
    {
        if (NormalizeProviderKey(provider) == "windows") return "●  系统内置";
        if (NormalizeProviderKey(provider) == "custom") return "●  请确保客户端已启动";
        return IsProviderRunning(provider) ? "●  客户端正在运行" : "●  未检测到运行中的客户端";
    }

    private static string ProviderRouteInstruction(string provider, bool automaticRoute)
    {
        return automaticRoute
            ? "听写时自动让 " + ProviderDisplayName(provider) + " 使用 CABLE Output，结束后恢复原麦克风"
            : "请在 " + ProviderDisplayName(provider) + " 中手动选择 CABLE Output";
    }

    private void OpenProviderHelp(string provider)
    {
        switch (NormalizeProviderKey(provider))
        {
            case "wechat": OpenUri("https://z.weixin.qq.com/"); break;
            case "typeless": OpenUri("https://www.typeless.com/zh-cn/help/quickstart/first-dictation"); break;
            case "voquill": OpenUri("https://github.com/voquill/voquill"); break;
            default: OpenUri("ms-settings:sound"); break;
        }
    }

    private void SaveWizardProviderConfig(string provider, string hotkey, string trigger, bool automaticRoute)
    {
        config.inputMethod = NormalizeProviderKey(provider);
        config.inputMethodHotkey = hotkey;
        config.inputMethodTrigger = trigger == "hold" ? "hold" : "toggle";
        config.providerStartupDelayMs = DefaultStartupDelayForProvider(config.inputMethod);
        config.autoRouteVirtualMicrophone = automaticRoute;
        SaveConfig();
        RestartCaptureForAudioSettings();
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

    private static bool HasStableVoiceProfile(VibeMicConfig value)
    {
        if (value == null) return false;
        return value.captureSeconds == 0 &&
            Math.Abs(value.gain - StableVoiceGain) < 0.001 &&
            value.autoLevel &&
            string.Equals(value.voiceMode, "hold", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(value.audioEndpointName, StableVoiceEndpoint, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(value.audioProcessingMode, StableVoiceProcessing, StringComparison.OrdinalIgnoreCase) &&
            value.autoRouteVirtualMicrophone &&
            value.drainMs == StableVoiceDrainMs;
    }

    private static void ApplyStableVoiceProfile(VibeMicConfig value)
    {
        if (value == null) return;
        value.captureSeconds = 0;
        value.gain = StableVoiceGain;
        value.autoLevel = true;
        value.voiceMode = "hold";
        value.audioEndpointName = StableVoiceEndpoint;
        value.audioProcessingMode = StableVoiceProcessing;
        value.autoRouteVirtualMicrophone = true;
        value.drainMs = StableVoiceDrainMs;
        if (NormalizeProviderKey(value.inputMethod) != "custom")
            value.providerStartupDelayMs = DefaultStartupDelayForProvider(value.inputMethod);
        value.stableVoiceProfileVersion = StableVoiceProfileVersion;
    }

    private static bool MigrateConfig(VibeMicConfig value)
    {
        int previousSchema = value.schemaVersion;
        bool changed = previousSchema < ConfigSchemaVersion;
        value.schemaVersion = ConfigSchemaVersion;
        if (value.captureSeconds < 0) { value.captureSeconds = 0; changed = true; }
        if (value.gain <= 0 || value.gain > 4) { value.gain = 1.0; changed = true; }
        if (previousSchema < 11) { value.autoLevel = true; changed = true; }
        if (string.IsNullOrWhiteSpace(value.voiceMode)) { value.voiceMode = "hold"; changed = true; }
        if (string.IsNullOrWhiteSpace(value.audioEndpointName)) { value.audioEndpointName = "CABLE Input"; changed = true; }
        if (string.IsNullOrWhiteSpace(value.inputMethod)) { value.inputMethod = "wechat"; changed = true; }
        if (string.IsNullOrWhiteSpace(value.inputMethodHotkey)) { value.inputMethodHotkey = "ctrl+win"; changed = true; }
        string normalizedProvider = NormalizeProviderKey(value.inputMethod);
        if (!normalizedProvider.Equals(value.inputMethod, StringComparison.OrdinalIgnoreCase))
        {
            value.inputMethod = normalizedProvider;
            changed = true;
        }
        if (string.IsNullOrWhiteSpace(value.inputMethodTrigger))
        {
            value.inputMethodTrigger = DefaultTriggerForProvider(value.inputMethod);
            changed = true;
        }
        if (value.inputMethodTrigger != "toggle" && value.inputMethodTrigger != "hold")
        {
            value.inputMethodTrigger = DefaultTriggerForProvider(value.inputMethod);
            changed = true;
        }
        if (value.providerStartupDelayMs < 20 || value.providerStartupDelayMs > 2000)
        {
            value.providerStartupDelayMs = DefaultStartupDelayForProvider(value.inputMethod);
            changed = true;
        }
        if (string.IsNullOrWhiteSpace(value.audioProcessingMode))
        {
            value.audioProcessingMode = value.autoLevel ? "speech" : "transparent";
            changed = true;
        }
        if (value.audioProcessingMode != "speech" && value.audioProcessingMode != "transparent")
        {
            value.audioProcessingMode = "speech";
            changed = true;
        }
        value.autoLevel = value.audioProcessingMode == "speech";
        if (previousSchema < 13) { value.autoRouteVirtualMicrophone = true; changed = true; }
        if (previousSchema < 14)
        {
            value.soundFeedbackEnabled = true;
            value.onboardingVersion = CurrentOnboardingVersion;
            changed = true;
        }
        if (value.drainMs <= 0) { value.drainMs = 180; changed = true; }
        if (string.IsNullOrWhiteSpace(value.mappingPreset)) { value.mappingPreset = "coding"; changed = true; }
        if (value.mappings == null) { value.mappings = new Dictionary<string, string>(); changed = true; }
        Dictionary<string, string> defaults = VibeMicConfig.Default().mappings;
        foreach (KeyValuePair<string, string> pair in defaults)
        {
            if (!value.mappings.ContainsKey(pair.Key)) { value.mappings[pair.Key] = pair.Value; changed = true; }
        }
        if (previousSchema < 9 && value.mappings.ContainsKey("功能键") &&
            value.mappings["功能键"].Equals("ctrl+shift+p", StringComparison.OrdinalIgnoreCase))
        {
            value.mappings["功能键"] = "launch-client:chatgpt";
            changed = true;
        }
        if (previousSchema < 10 && value.mappings.ContainsKey("功能键") &&
            value.mappings["功能键"].StartsWith("launch-ai:", StringComparison.OrdinalIgnoreCase))
        {
            value.mappings["功能键"] = "launch-client:" + value.mappings["功能键"].Substring("launch-ai:".Length);
            changed = true;
        }
        string[] unsupportedMappings = { "返回键", "音量 + / -", "返回操作", "换行 / 删除" };
        foreach (string key in unsupportedMappings)
            if (value.mappings.Remove(key)) changed = true;
        int profileVersion = HasStableVoiceProfile(value) ? StableVoiceProfileVersion : 0;
        if (value.stableVoiceProfileVersion != profileVersion)
        {
            value.stableVoiceProfileVersion = profileVersion;
            changed = true;
        }
        if (value.onboardingVersion < CurrentOnboardingVersion)
        {
            value.onboardingVersion = CurrentOnboardingVersion;
            changed = true;
        }
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
            config.schemaVersion = ConfigSchemaVersion;
            config.autoLevel = config.audioProcessingMode == "speech";
            config.stableVoiceProfileVersion = HasStableVoiceProfile(config) ? StableVoiceProfileVersion : 0;
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
            mappings.Add(ConfiguredMapping("menu", "功能键", "Apps", "0x5D", GetMapping("功能键", "launch-client:chatgpt"), "apps"));
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

    private static string RemoteControlForMappingKey(string key)
    {
        if (key == "录音键") return "voice";
        if (key == "确认键") return "ok";
        if (key == "Home") return "home";
        if (key == "TV") return "tv";
        if (key == "功能键") return "menu";
        return "directions";
    }

    private static List<ShortcutChoice> ShortcutChoicesFor(string key, string current)
    {
        var choices = new List<ShortcutChoice>();
        if (key == "录音键") choices.Add(new ShortcutChoice("由言灵管理", "managed"));
        else if (key == "上 / 下 / 左 / 右") choices.Add(new ShortcutChoice("短按方向 · 长按上下调音量", "direction-volume-fallback"));
        else
        {
            if (key == "功能键")
            {
                choices.Add(new ShortcutChoice("客户端 · ChatGPT（默认）", "launch-client:chatgpt"));
                choices.Add(new ShortcutChoice("客户端 · DeepSeek", "launch-client:deepseek"));
                choices.Add(new ShortcutChoice("客户端 · Claude", "launch-client:claude"));
                choices.Add(new ShortcutChoice("开发工具 · Cursor", "launch-client:cursor"));
                choices.Add(new ShortcutChoice("开发工具 · Visual Studio Code", "launch-client:vscode"));
                choices.Add(new ShortcutChoice("开发工具 · Windsurf", "launch-client:windsurf"));
                choices.Add(new ShortcutChoice("系统工具 · Windows Terminal", "launch-client:terminal"));
            }
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
            config.mappings["功能键"] = "launch-client:chatgpt";
        }
        else if (preset == "review")
        {
            config.mappings["Home"] = "ctrl+f";
            config.mappings["TV"] = "task-switcher";
            config.mappings["功能键"] = "launch-client:chatgpt";
        }
        else
        {
            config.mappings["Home"] = "win+d";
            config.mappings["TV"] = "task-switcher";
            config.mappings["功能键"] = "launch-client:chatgpt";
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

    private void CaptureNextAudioDiagnostic()
    {
        if (!IsCapturing)
        {
            Toast("请先启动语音桥接");
            return;
        }

        DialogResult consent = MessageBox.Show(this,
            "仅下一次按住录音键时，言灵会在本机保存三份音频：遥控器解码原声、处理后声音和 CABLE Output。最长 30 秒，完成后自动关闭，可随时删除。\r\n\r\n请说：测试麦克风，一二三四五六，期待效果。",
            "采集下一段诊断音频", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
        if (consent != DialogResult.OK) return;

        try
        {
            using (EventWaitHandle handle = EventWaitHandle.OpenExisting("Local\\VibeMicCaptureAudioDiagnostic"))
                handle.Set();
            Log("One-shot audio diagnostic armed by user.");
            Toast("已就绪，请按住录音键说提示短句");
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            Toast("语音桥接尚未就绪，请稍后再试");
        }
        catch (Exception ex)
        {
            Log("Audio diagnostic arm failed: " + ex.Message);
            Toast("无法启动音频诊断");
        }
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
            report.Append(BuildProblemSummary());
            report.AppendLine();
            report.AppendLine("Technical diagnostics");
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
            ShowToast("诊断已导出，不包含录音和识别文字", "success");
        }
        catch (Exception ex) { ShowToast("诊断导出失败", "error"); Log("Diagnostics export failed: " + ex.Message); }
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

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string className, string windowName);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr window, out ClientRect rectangle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint code, uint mapType);

    [DllImport("winmm.dll")]
    private static extern uint waveOutGetNumDevs();

    [DllImport("winmm.dll", CharSet = CharSet.Auto)]
    private static extern uint waveOutGetDevCaps(UIntPtr deviceId, out WaveOutCaps caps, uint size);

    [DllImport("winmm.dll")]
    private static extern uint waveInGetNumDevs();

    [DllImport("winmm.dll", CharSet = CharSet.Auto)]
    private static extern uint waveInGetDevCaps(UIntPtr deviceId, out WaveInCaps caps, uint size);

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

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct WaveInCaps
    {
        public ushort manufacturerId, productId;
        public uint driverVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string name;
        public uint formats;
        public ushort channels, reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ClientRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private sealed class SelfCheckItem
    {
        public string Id;
        public string Title;
        public string State;
        public string Detail;
        public string ActionText;
        public string Action;
        public SelfCheckItem(string id, string title, string state, string detail, string actionText, string action)
        {
            Id = id;
            Title = title;
            State = state;
            Detail = detail;
            ActionText = actionText;
            Action = action;
        }
    }

    private sealed class SelfCheckReport
    {
        public readonly List<SelfCheckItem> Items = new List<SelfCheckItem>();
        public int PassedCount;
        public int WarningCount;
        public int FailedCount;
        public string Headline = "正在检查";
        public string Detail = "";
    }

    private sealed class SessionHealth
    {
        public int Generation;
        public int SessionId;
        public string Provider = "wechat";
        public bool Started;
        public bool Ready;
        public bool StreamStopped;
        public bool Drained;
        public bool RouteAcquired;
        public bool RouteRestored;
        public bool RouteRestorePending;
        public bool Completed;
        public bool AudioDelivered;
        public bool Success;
        public bool Failed;
        public int AudioMs;
        public int TriggerToReadyMs;
        public int MaxGapMs;
        public int QueueDrops;
        public int SinkQueueDrops;
        public int PendingAfterDrain;
        public int DrainWaitMs;
        public double RawRmsPercent;
        public double OutputRmsPercent;
        public DateTime StartedAt;
        public DateTime EndedAt;
        public string Error = "";
        public string NextAction = "";
    }

    private sealed class VibeMicConfig
    {
        public int schemaVersion { get; set; }
        public int stableVoiceProfileVersion { get; set; }
        public int captureSeconds { get; set; }
        public double gain { get; set; }
        public bool autoLevel { get; set; }
        public string voiceMode { get; set; }
        public bool setupCompleted { get; set; }
        public int onboardingVersion { get; set; }
        public bool launchAtStartup { get; set; }
        public bool startBridgeOnLaunch { get; set; }
        public bool minimizeToTray { get; set; }
        public string audioEndpointName { get; set; }
        public string inputMethod { get; set; }
        public string inputMethodHotkey { get; set; }
        public string inputMethodTrigger { get; set; }
        public int providerStartupDelayMs { get; set; }
        public string audioProcessingMode { get; set; }
        public bool autoRouteVirtualMicrophone { get; set; }
        public bool soundFeedbackEnabled { get; set; }
        public int drainMs { get; set; }
        public string mappingPreset { get; set; }
        public Dictionary<string, string> mappings { get; set; }

        public static VibeMicConfig Default()
        {
            var c = new VibeMicConfig();
            c.schemaVersion = ConfigSchemaVersion;
            c.stableVoiceProfileVersion = StableVoiceProfileVersion;
            c.captureSeconds = 0;
            c.gain = 1.0;
            c.autoLevel = true;
            c.voiceMode = "hold";
            c.setupCompleted = false;
            c.onboardingVersion = CurrentOnboardingVersion;
            c.launchAtStartup = false;
            c.startBridgeOnLaunch = false;
            c.minimizeToTray = true;
            c.audioEndpointName = "CABLE Input";
            c.inputMethod = "wechat";
            c.inputMethodHotkey = "ctrl+win";
            c.inputMethodTrigger = "toggle";
            c.providerStartupDelayMs = 80;
            c.audioProcessingMode = "speech";
            c.autoRouteVirtualMicrophone = true;
            c.soundFeedbackEnabled = true;
            c.drainMs = 180;
            c.mappingPreset = "coding";
            c.mappings = new Dictionary<string, string>();
            c.mappings["确认键"] = "enter";
            c.mappings["Home"] = "win+d";
            c.mappings["TV"] = "task-switcher";
            c.mappings["功能键"] = "launch-client:chatgpt";
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
    private const int DesignWidth = 112;
    private const int DesignHeight = 440;
    public Color AccentColor = Color.FromArgb(126, 139, 174);
    public bool IsActive;
    public bool IsRecording;
    public bool ShowCallouts;
    public string HighlightedControl = "";
    public float AnimationPhase;
    public RemoteVisual()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        e.Graphics.CompositingQuality = CompositingQuality.HighQuality;

        float scale = Math.Min(1.15f, Math.Max(0.48f, (Height - 12f) / DesignHeight));
        int bodyWidth = (int)Math.Round(DesignWidth * scale);
        int x = Width / 2 - bodyWidth / 2;
        Func<int, int> sx = delegate(int value) { return x + (int)(value * scale); };
        Func<int, int> sy = delegate(int value) { return 6 + (int)(value * scale); };
        Func<int, int> sr = delegate(int value) { return Math.Max(2, (int)Math.Round(value * scale)); };
        var body = new Rectangle(x, 6, bodyWidth, Math.Min(Height - 12, (int)Math.Round(DesignHeight * scale)));

        DrawBodyAura(e.Graphics, body);
        DrawMetalBody(e.Graphics, body, sr(6));

        int voiceX = sx(83);
        int voiceY = sy(29);
        int topRadius = sr(13);
        if (IsRecording) DrawRecordingRipples(e.Graphics, voiceX, voiceY, topRadius);

        DrawTopButton(e.Graphics, sx(29), sy(29), topRadius, "power", IsHighlighted("power"), AccentColor, scale);
        DrawTopButton(e.Graphics, voiceX, voiceY, topRadius, "voice", IsHighlighted("voice") || IsRecording, AccentColor, scale);
        DrawDirectionalPad(e.Graphics, sx(56), sy(96), sr(43), sr(22), AccentColor, scale);
        DrawDarkButton(e.Graphics, sx(29), sy(158), sr(17), "back", IsHighlighted("back"), AccentColor, scale);
        DrawVolumeRocker(e.Graphics, sx(83), sy(141), sy(219), sr(17), AccentColor, scale);
        DrawDarkButton(e.Graphics, sx(29), sy(199), sr(17), "home", IsHighlighted("home"), AccentColor, scale);
        DrawDarkButton(e.Graphics, sx(29), sy(240), sr(17), "menu", IsHighlighted("menu"), AccentColor, scale);
        DrawDarkButton(e.Graphics, sx(83), sy(240), sr(17), "tv", IsHighlighted("tv"), AccentColor, scale);
        DrawBranding(e.Graphics, sx(56), sy(316), sy(405), scale);

        if (ShowCallouts) DrawCallouts(e.Graphics, body, sx, sy);
    }

    private bool IsHighlighted(string control)
    {
        return string.Equals(HighlightedControl, control, StringComparison.OrdinalIgnoreCase);
    }

    private void DrawBodyAura(Graphics g, Rectangle body)
    {
        if (!IsActive) return;
        float breath = (float)((Math.Sin(AnimationPhase * 0.72f) + 1.0) / 2.0);
        for (int i = 3; i >= 0; i--)
        {
            int spread = 7 + i * 7 + (int)(breath * (IsRecording ? 9 : 4));
            int alpha = Math.Max(8, (IsRecording ? 55 : 34) - i * 9);
            var aura = new Rectangle(body.X - spread / 2, body.Y - spread / 2, body.Width + spread, body.Height + spread);
            using (var pen = new Pen(Color.FromArgb(alpha, AccentColor), IsRecording ? 2.3f : 1.5f))
                g.DrawRoundedRectangle(pen, aura, Math.Max(7, 9 + spread / 4));
        }
    }

    private static void DrawMetalBody(Graphics g, Rectangle body, int radius)
    {
        var shadowRect = new Rectangle(body.X + 4, body.Y + 5, body.Width, body.Height);
        using (var shadowPath = RoundedPath(shadowRect, radius + 1))
        using (var shadow = new SolidBrush(Color.FromArgb(38, 31, 42, 67)))
            g.FillPath(shadow, shadowPath);

        using (var bodyPath = RoundedPath(body, radius))
        using (var metal = new LinearGradientBrush(body, Color.White, Color.Silver, 0f))
        {
            metal.InterpolationColors = new ColorBlend
            {
                Colors = new Color[] {
                    Color.FromArgb(128, 134, 145), Color.FromArgb(237, 240, 243),
                    Color.FromArgb(253, 253, 253), Color.FromArgb(219, 222, 227),
                    Color.FromArgb(246, 247, 248), Color.FromArgb(185, 190, 199),
                    Color.FromArgb(118, 124, 135)
                },
                Positions = new float[] { 0f, 0.055f, 0.17f, 0.52f, 0.82f, 0.95f, 1f }
            };
            g.FillPath(metal, bodyPath);

            GraphicsState state = g.Save();
            g.SetClip(bodyPath);
            for (int y = body.Top + 8; y < body.Bottom - 6; y += 5)
            {
                int alpha = 5 + ((y - body.Top) % 4);
                using (var grain = new Pen(Color.FromArgb(alpha, 45, 52, 64), 1f))
                    g.DrawLine(grain, body.Left + 5, y, body.Right - 5, y);
            }
            using (var topSheen = new Pen(Color.FromArgb(180, 255, 255, 255), 1f))
                g.DrawLine(topSheen, body.Left + radius, body.Top + 1, body.Right - radius, body.Top + 1);
            g.Restore(state);
        }

        int railWidth = Math.Max(2, body.Width / 18);
        var leftRail = new Rectangle(body.Left + 1, body.Top + 2, railWidth, body.Height - 4);
        var rightRail = new Rectangle(body.Right - railWidth - 1, body.Top + 2, railWidth, body.Height - 4);
        using (var leftBrush = new LinearGradientBrush(leftRail, Color.FromArgb(112, 119, 131), Color.FromArgb(244, 246, 248), 0f))
            g.FillRoundedRectangle(leftBrush, leftRail, Math.Max(1, railWidth / 2));
        using (var rightBrush = new LinearGradientBrush(rightRail, Color.FromArgb(238, 240, 243), Color.FromArgb(104, 111, 123), 0f))
            g.FillRoundedRectangle(rightBrush, rightRail, Math.Max(1, railWidth / 2));

        using (var border = new Pen(Color.FromArgb(185, 82, 88, 100), 1f))
            g.DrawRoundedRectangle(border, body, radius);
        using (var faceEdge = new Pen(Color.FromArgb(112, 255, 255, 255), 1f))
        {
            g.DrawLine(faceEdge, body.Left + railWidth + 1, body.Top + radius, body.Left + railWidth + 1, body.Bottom - radius);
            g.DrawLine(faceEdge, body.Right - railWidth - 2, body.Top + radius, body.Right - railWidth - 2, body.Bottom - radius);
        }
    }

    private void DrawRecordingRipples(Graphics g, int cx, int cy, int radius)
    {
        for (int i = 0; i < 4; i++)
        {
            float phase = (AnimationPhase * 0.36f + i * 0.25f) % 1f;
            int ringRadius = radius + 4 + (int)(phase * 25f);
            int alpha = Math.Max(8, (int)((1f - phase) * 118f));
            using (var ring = new Pen(Color.FromArgb(alpha, AccentColor), 1.5f + (1f - phase) * 1.4f))
                g.DrawEllipse(ring, cx - ringRadius, cy - ringRadius, ringRadius * 2, ringRadius * 2);
        }
        int coreRadius = radius + 3 + (int)(((Math.Sin(AnimationPhase * 1.7f) + 1.0) / 2.0) * 3);
        using (var core = new Pen(Color.FromArgb(120, AccentColor), 3f))
            g.DrawEllipse(core, cx - coreRadius, cy - coreRadius, coreRadius * 2, coreRadius * 2);
    }

    private static void DrawTopButton(Graphics g, int cx, int cy, int radius, string icon, bool highlighted, Color accent, float scale)
    {
        if (highlighted)
        {
            using (var halo = new Pen(Color.FromArgb(78, accent), Math.Max(2f, 4f * scale)))
                g.DrawEllipse(halo, cx - radius - 3, cy - radius - 3, (radius + 3) * 2, (radius + 3) * 2);
        }
        var bounds = new Rectangle(cx - radius, cy - radius, radius * 2, radius * 2);
        Color upper = highlighted ? Lighten(accent, 24) : Color.FromArgb(250, 251, 252);
        Color lower = highlighted ? accent : Color.FromArgb(190, 194, 201);
        using (var fill = new LinearGradientBrush(bounds, upper, lower, 90f)) g.FillEllipse(fill, bounds);
        using (var border = new Pen(highlighted ? Color.FromArgb(210, accent) : Color.FromArgb(225, 44, 47, 52), Math.Max(1f, 1.15f * scale)))
            g.DrawEllipse(border, bounds);
        using (var sheen = new Pen(Color.FromArgb(145, 255, 255, 255), 1f))
            g.DrawArc(sheen, bounds.X + 2, bounds.Y + 2, bounds.Width - 4, bounds.Height - 4, 205, 130);
        Color iconColor = highlighted ? Color.White : Color.FromArgb(46, 48, 53);
        if (icon == "power") DrawPowerIcon(g, cx, cy, radius, iconColor, scale);
        else DrawMicrophoneIcon(g, cx, cy, radius, iconColor, scale, highlighted);
    }

    private static void DrawPowerIcon(Graphics g, int cx, int cy, int radius, Color color, float scale)
    {
        int iconRadius = Math.Max(3, (int)Math.Round(radius * 0.46f));
        using (var pen = new Pen(color, Math.Max(1.15f, 1.65f * scale)))
        {
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            g.DrawLine(pen, cx, cy - iconRadius - 2, cx, cy - 1);
            g.DrawArc(pen, cx - iconRadius, cy - iconRadius, iconRadius * 2, iconRadius * 2, -43, 266);
        }
    }

    private static void DrawMicrophoneIcon(Graphics g, int cx, int cy, int radius, Color color, float scale, bool active)
    {
        float width = Math.Max(1.1f, 1.55f * scale);
        using (var pen = new Pen(color, width))
        {
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            int capsuleWidth = Math.Max(4, (int)Math.Round(radius * 0.48f));
            int capsuleHeight = Math.Max(6, (int)Math.Round(radius * 0.88f));
            g.DrawRoundedRectangle(pen, new Rectangle(cx - capsuleWidth / 2, cy - capsuleHeight / 2 - 1, capsuleWidth, capsuleHeight), Math.Max(2, capsuleWidth / 2));
            g.DrawArc(pen, cx - capsuleWidth / 2 - 2, cy - 2, capsuleWidth + 4, capsuleHeight, 0, 180);
            g.DrawLine(pen, cx, cy + capsuleHeight / 2 + 2, cx, cy + capsuleHeight / 2 + 5);
            g.DrawLine(pen, cx - 2, cy + capsuleHeight / 2 + 5, cx + 2, cy + capsuleHeight / 2 + 5);
            if (active)
            {
                g.DrawArc(pen, cx - radius - 3, cy - radius / 2, 4, radius, -70, 140);
                g.DrawArc(pen, cx + radius - 1, cy - radius / 2, 4, radius, 110, 140);
            }
        }
    }

    private void DrawDirectionalPad(Graphics g, int cx, int cy, int radius, int innerRadius, Color accent, float scale)
    {
        var outer = new Rectangle(cx - radius, cy - radius, radius * 2, radius * 2);
        using (var shadow = new SolidBrush(Color.FromArgb(55, 20, 23, 30)))
            g.FillEllipse(shadow, outer.X + 1, outer.Y + 3, outer.Width, outer.Height);
        using (var fill = new LinearGradientBrush(outer, Color.FromArgb(67, 69, 75), Color.FromArgb(24, 26, 31), 90f))
            g.FillEllipse(fill, outer);

        bool allDirections = IsHighlighted("directions");
        string[] names = { "up", "right", "down", "left" };
        float[] starts = { -135f, -45f, 45f, 135f };
        for (int i = 0; i < names.Length; i++)
        {
            if (!allDirections && !IsHighlighted(names[i])) continue;
            using (var highlight = new SolidBrush(Color.FromArgb(205, accent)))
                g.FillPie(highlight, outer.X + 3, outer.Y + 3, outer.Width - 6, outer.Height - 6, starts[i], 90f);
        }

        using (var rim = new Pen(Color.FromArgb(220, 20, 22, 27), Math.Max(1f, 1.4f * scale)))
            g.DrawEllipse(rim, outer);
        using (var sheen = new Pen(Color.FromArgb(92, 255, 255, 255), 1f))
            g.DrawArc(sheen, outer.X + 2, outer.Y + 2, outer.Width - 4, outer.Height - 4, 205, 128);

        int markerRadius = Math.Max(1, (int)(1.7f * scale));
        Point[] markers = {
            new Point(cx, cy - radius + Math.Max(6, radius / 5)),
            new Point(cx + radius - Math.Max(6, radius / 5), cy),
            new Point(cx, cy + radius - Math.Max(6, radius / 5)),
            new Point(cx - radius + Math.Max(6, radius / 5), cy)
        };
        for (int i = 0; i < markers.Length; i++)
        {
            bool lit = allDirections || IsHighlighted(names[i]);
            using (var marker = new SolidBrush(lit ? Color.White : Color.FromArgb(11, 13, 17)))
                g.FillEllipse(marker, markers[i].X - markerRadius, markers[i].Y - markerRadius, markerRadius * 2, markerRadius * 2);
        }

        bool ok = IsHighlighted("ok");
        var centerBounds = new Rectangle(cx - innerRadius, cy - innerRadius, innerRadius * 2, innerRadius * 2);
        using (var center = new LinearGradientBrush(centerBounds, ok ? Lighten(accent, 20) : Color.FromArgb(66, 68, 73), ok ? accent : Color.FromArgb(30, 32, 37), 90f))
            g.FillEllipse(center, centerBounds);
        using (var centerBorder = new Pen(ok ? Color.White : Color.FromArgb(135, 11, 13, 17), Math.Max(1f, 1.4f * scale)))
            g.DrawEllipse(centerBorder, centerBounds);
        using (var centerSheen = new Pen(Color.FromArgb(ok ? 105 : 45, 255, 255, 255), 1f))
            g.DrawArc(centerSheen, centerBounds.X + 2, centerBounds.Y + 2, centerBounds.Width - 4, centerBounds.Height - 4, 205, 130);
    }

    private static void DrawDarkButton(Graphics g, int cx, int cy, int radius, string icon, bool highlighted, Color accent, float scale)
    {
        if (highlighted)
        {
            using (var halo = new Pen(Color.FromArgb(74, accent), Math.Max(2f, 4f * scale)))
                g.DrawEllipse(halo, cx - radius - 3, cy - radius - 3, (radius + 3) * 2, (radius + 3) * 2);
        }
        var bounds = new Rectangle(cx - radius, cy - radius, radius * 2, radius * 2);
        using (var shadow = new SolidBrush(Color.FromArgb(45, 18, 20, 27)))
            g.FillEllipse(shadow, bounds.X + 1, bounds.Y + 2, bounds.Width, bounds.Height);
        using (var fill = new LinearGradientBrush(bounds, highlighted ? Lighten(accent, 15) : Color.FromArgb(63, 65, 70), highlighted ? accent : Color.FromArgb(25, 27, 32), 90f))
            g.FillEllipse(fill, bounds);
        using (var border = new Pen(highlighted ? Color.FromArgb(220, accent) : Color.FromArgb(214, 20, 22, 27), Math.Max(1f, 1.2f * scale)))
            g.DrawEllipse(border, bounds);
        using (var sheen = new Pen(Color.FromArgb(65, 255, 255, 255), 1f))
            g.DrawArc(sheen, bounds.X + 2, bounds.Y + 2, bounds.Width - 4, bounds.Height - 4, 205, 128);

        Color iconColor = Color.FromArgb(244, 247, 250);
        float penWidth = Math.Max(1.05f, 1.55f * scale);
        using (var pen = new Pen(iconColor, penWidth))
        {
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            pen.LineJoin = LineJoin.Round;
            int unit = Math.Max(3, (int)Math.Round(5.4f * scale));
            if (icon == "back")
            {
                g.DrawLines(pen, new Point[] { new Point(cx + unit / 2, cy - unit), new Point(cx - unit / 2, cy), new Point(cx + unit / 2, cy + unit) });
            }
            else if (icon == "home")
            {
                using (var path = new GraphicsPath())
                {
                    path.AddLines(new PointF[] {
                        new PointF(cx - unit, cy - 1), new PointF(cx, cy - unit), new PointF(cx + unit, cy - 1),
                        new PointF(cx + unit, cy + unit), new PointF(cx - unit, cy + unit)
                    });
                    path.CloseFigure();
                    g.DrawPath(pen, path);
                }
            }
            else if (icon == "menu")
            {
                for (int i = -1; i <= 1; i++) g.DrawLine(pen, cx - unit, cy + i * Math.Max(3, unit / 2), cx + unit, cy + i * Math.Max(3, unit / 2));
            }
            else
            {
                int tvWidth = Math.Max(10, (int)Math.Round(14 * scale));
                int tvHeight = Math.Max(7, (int)Math.Round(10 * scale));
                g.DrawRoundedRectangle(pen, new Rectangle(cx - tvWidth / 2, cy - tvHeight / 2, tvWidth, tvHeight), Math.Max(2, (int)(3 * scale)));
                using (var font = new Font("Segoe UI", Math.Max(4.8f, 5.6f * scale), FontStyle.Bold))
                using (var brush = new SolidBrush(iconColor))
                using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString("TV", font, brush, new RectangleF(cx - tvWidth / 2f, cy - tvHeight / 2f, tvWidth, tvHeight), format);
            }
        }
    }

    private void DrawVolumeRocker(Graphics g, int cx, int top, int bottom, int halfWidth, Color accent, float scale)
    {
        var bounds = new Rectangle(cx - halfWidth, top, halfWidth * 2, bottom - top);
        var shadowBounds = new Rectangle(bounds.X + 1, bounds.Y + 3, bounds.Width, bounds.Height);
        using (var shadowPath = RoundedPath(shadowBounds, halfWidth))
        using (var shadow = new SolidBrush(Color.FromArgb(48, 18, 20, 27)))
            g.FillPath(shadow, shadowPath);

        using (var path = RoundedPath(bounds, halfWidth))
        using (var fill = new LinearGradientBrush(bounds, Color.FromArgb(62, 64, 69), Color.FromArgb(24, 26, 31), 90f))
        {
            g.FillPath(fill, path);
            GraphicsState state = g.Save();
            g.SetClip(path);
            int middle = bounds.Top + bounds.Height / 2;
            if (IsHighlighted("volumeup"))
            {
                using (var active = new SolidBrush(Color.FromArgb(230, accent)))
                    g.FillRectangle(active, bounds.Left, bounds.Top, bounds.Width, middle - bounds.Top + 1);
            }
            if (IsHighlighted("volumedown"))
            {
                using (var active = new SolidBrush(Color.FromArgb(230, accent)))
                    g.FillRectangle(active, bounds.Left, middle, bounds.Width, bounds.Bottom - middle);
            }
            g.Restore(state);
        }

        using (var border = new Pen(Color.FromArgb(214, 20, 22, 27), Math.Max(1f, 1.2f * scale)))
            g.DrawRoundedRectangle(border, bounds, halfWidth);
        using (var separator = new Pen(Color.FromArgb(38, 255, 255, 255), 1f))
            g.DrawLine(separator, bounds.Left + 4, bounds.Top + bounds.Height / 2, bounds.Right - 4, bounds.Top + bounds.Height / 2);

        int plusY = bounds.Top + bounds.Height / 4;
        int minusY = bounds.Top + bounds.Height * 3 / 4;
        int unit = Math.Max(3, (int)Math.Round(5.5f * scale));
        using (var icon = new Pen(Color.FromArgb(245, 247, 250), Math.Max(1.05f, 1.55f * scale)))
        {
            icon.StartCap = LineCap.Round;
            icon.EndCap = LineCap.Round;
            g.DrawLine(icon, cx - unit, plusY, cx + unit, plusY);
            g.DrawLine(icon, cx, plusY - unit, cx, plusY + unit);
            g.DrawLine(icon, cx - unit, minusY, cx + unit, minusY);
        }
    }

    private void DrawBranding(Graphics g, int cx, int markY, int textY, float scale)
    {
        Color markColor = IsActive ? Color.FromArgb(180, AccentColor) : Color.FromArgb(135, 53, 57, 65);
        int markWidth = Math.Max(8, (int)Math.Round(13 * scale));
        int markHeight = Math.Max(7, (int)Math.Round(11 * scale));
        using (var pen = new Pen(markColor, Math.Max(1f, 1.25f * scale)))
        {
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            pen.LineJoin = LineJoin.Round;
            g.DrawRectangle(pen, cx - markWidth / 2, markY - markHeight / 2, markWidth, markHeight);
            g.DrawLines(pen, new Point[] {
                new Point(cx - markWidth / 3, markY + markHeight / 3),
                new Point(cx - markWidth / 3, markY - markHeight / 3),
                new Point(cx + markWidth / 3, markY + markHeight / 3),
                new Point(cx + markWidth / 3, markY - markHeight / 3)
            });
        }
        using (var font = new Font("Segoe UI", Math.Max(5.2f, 7.2f * scale), FontStyle.Bold))
        using (var brush = new SolidBrush(Color.FromArgb(150, 45, 49, 57)))
        using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near })
            g.DrawString("xiaomi", font, brush, new RectangleF(cx - 34, textY, 68, 14), format);
    }

    private void DrawCallouts(Graphics g, Rectangle body, Func<int, int> sx, Func<int, int> sy)
    {
        DrawCallout(g, "录音键", sx(83), sy(29), body.Right + 10, sy(19), IsHighlighted("voice") || IsRecording, false);
        DrawCallout(g, "方向 / 确认", sx(56), sy(96), Math.Max(2, body.Left - 88), sy(86), IsHighlighted("directions") || IsHighlighted("ok"), true);
        DrawCallout(g, "返回", sx(29), sy(158), Math.Max(2, body.Left - 58), sy(150), IsHighlighted("back"), true);
        DrawCallout(g, "音量 +/-", sx(83), sy(180), body.Right + 10, sy(170), IsHighlighted("volumeup") || IsHighlighted("volumedown"), false);
        DrawCallout(g, "Home", sx(29), sy(199), Math.Max(2, body.Left - 62), sy(192), IsHighlighted("home"), true);
        DrawCallout(g, "功能键", sx(29), sy(240), Math.Max(2, body.Left - 68), sy(237), IsHighlighted("menu"), true);
        DrawCallout(g, "TV", sx(83), sy(240), body.Right + 10, sy(237), IsHighlighted("tv"), false);
    }

    private void DrawCallout(Graphics g, string text, int targetX, int targetY, int textX, int textY, bool highlighted, bool leftSide)
    {
        Color color = highlighted ? AccentColor : Color.FromArgb(112, 123, 148);
        using (var pen = new Pen(Color.FromArgb(highlighted ? 180 : 85, color), highlighted ? 1.8f : 1f))
        {
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            int lineEnd = leftSide ? textX + 52 : textX - 6;
            g.DrawLine(pen, targetX, targetY, lineEnd, textY + 9);
            using (var dot = new SolidBrush(Color.FromArgb(highlighted ? 220 : 120, color)))
                g.FillEllipse(dot, targetX - 2, targetY - 2, 4, 4);
        }
        using (var font = new Font("Microsoft YaHei UI", 7.6f, highlighted ? FontStyle.Bold : FontStyle.Regular))
        using (var brush = new SolidBrush(color)) g.DrawString(text, font, brush, textX, textY);
    }

    private static GraphicsPath RoundedPath(Rectangle rectangle, int radius)
    {
        int safeRadius = Math.Max(1, Math.Min(radius, Math.Min(rectangle.Width, rectangle.Height) / 2));
        int diameter = safeRadius * 2;
        var path = new GraphicsPath();
        path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static Color Lighten(Color color, int amount)
    {
        return Color.FromArgb(color.A, Math.Min(255, color.R + amount), Math.Min(255, color.G + amount), Math.Min(255, color.B + amount));
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
