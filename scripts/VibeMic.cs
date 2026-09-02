using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Globalization;
using System.Media;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: System.Reflection.AssemblyTitle("Vibe Flow Remote")]
[assembly: System.Reflection.AssemblyProduct("言灵 · Vibe Flow Remote")]
[assembly: System.Reflection.AssemblyCompany("Vibe Flow Contributors")]
[assembly: System.Reflection.AssemblyVersion("1.4.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.4.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersion("1.4.0-preview")]

internal sealed class VibeMicForm : Form
{
    private const string DisplayProductName = "言灵 · Vibe Flow Remote";
    private const string ProductRelease = "1.4.0";
    private const string StableCaptureBinaryVersion = "1.2.1";
    private const int ConfigSchemaVersion = 31;
    private const int CurrentOnboardingVersion = 9;
    private const int OnboardingStepCount = 5;
    private const int StableVoiceProfileVersion = 11;
    private const int MinimumUsefulAudioMs = 700;
    private const int BridgeHealthStartupGraceSeconds = 12;
    private const int BridgeHealthFailureRecoverySeconds = 15;
    private const int CaptureReadyRecoverySeconds = 45;
    private const int CaptureRecoveryCooldownSeconds = 15;
    private const int CaptureRestartReleaseDelayMs = 2200;
    private const long MaxHostLogBytes = 2 * 1024 * 1024;
    private const string WeChatStableHotkey = "ctrl+win";
    private const string WeChatV12Hotkey = "ctrl+win+shift";
    private const double StableVoiceGain = 1.0;
    private const int StableVoiceDrainMs = 180;
    private const string StableVoiceEndpoint = "CABLE Input";
    private const string StableVoiceProcessing = "speech";
    private const int PageHome = 0;
    private const int PageShortcuts = 1;
    private const int PageVoice = 2;
    private const int PageSelfCheck = 3;
    private const int PageSettings = 4;
    private readonly string root = AppDomain.CurrentDomain.BaseDirectory;
    private readonly string sessionDir;
    private readonly string configPath;
    private readonly string eventsPath;
    private readonly string brandLogoPath;
    private readonly string hostLogPath;
    private readonly Font navigationFont = new Font("Microsoft YaHei UI", 10f, FontStyle.Regular);
    private readonly Font navigationActiveFont = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold);
    private readonly Font connectionBadgeFont = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
    private Color ink = Color.FromArgb(18, 30, 54);
    private Color muted = Color.FromArgb(91, 104, 134);
    private Color violet = Color.FromArgb(104, 82, 244);
    private Color green = Color.FromArgb(10, 164, 104);
    private Color amber = Color.FromArgb(229, 151, 39);
    private Color cyan = Color.FromArgb(0, 153, 190);
    private Color coral = Color.FromArgb(204, 70, 82);
    private Color line = Color.FromArgb(220, 226, 239);
    private Color pageBackground = Color.FromArgb(245, 247, 251);
    private Color sidebarBackground = Color.FromArgb(249, 251, 255);
    private Color cardBackground = Color.White;
    private Color surfaceBackground = Color.FromArgb(246, 249, 253);
    private Color inputBackground = Color.White;
    private bool darkTheme;
    private readonly Panel content = new Panel();
    private readonly List<Button> navButtons = new List<Button>();
    private readonly Label[] overviewStatusValues = new Label[5];
    private readonly Label[] overviewStatusGlyphs = new Label[5];
    private readonly Label connectionBadge = new Label();
    private readonly NotifyIcon tray = new NotifyIcon();
    private readonly bool backgroundLaunch;
    private readonly bool uiSmokeMode;
    private readonly bool uiResourceTestMode;
    private Label heroTitle;
    private Label heroSubtitle;
    private Label heroStateLabel;
    private Label activityLabel;
    private Button bridgeButton;
    private RoundPanel heroPanel;
    private RemoteVisual remoteVisual;
    private Label voiceBridgeStateLabel;
    private Label actionReceiptGlyph;
    private Label actionReceiptTitle;
    private Label actionReceiptDetail;
    private TextBox logBox;
    private Process captureProcess;
    private Process keyboardBridgeProcess;
    private EventWaitHandle showWindowEvent;
    private EventWaitHandle exitApplicationEvent;
    private EventWaitHandle voiceWakeRequestEvent;
    private EventWaitHandle providerHotkeyTapEvent;
    private EventWaitHandle providerHotkeyDownEvent;
    private EventWaitHandle providerHotkeyUpEvent;
    private EventWaitHandle inputTargetMissingEvent;
    private EventWaitHandle recordingStartCueEvent;
    private EventWaitHandle recordingStopCueEvent;
    private Thread recordingCueThread;
    private readonly object providerHotkeySync = new object();
    private WindowsAudioDuckingLease audioDuckingLease;
    private string heldProviderHotkey;
    private VibeMicConfig config;
    private System.Windows.Forms.Timer activityTimer;
    private System.Windows.Forms.Timer reconnectTimer;
    private System.Windows.Forms.Timer visualTimer;
    private System.Windows.Forms.Timer systemRecoveryTimer;
    private long lastEventLength;
    private int reconnectAttempt;
    private int startupRecoveryCount;
    private bool captureStopping;
    private bool applicationExiting;
    private bool providerWarmupActive;
    private int providerWarmupLaunchRequested;
    private readonly object providerWarmupLock = new object();
    private DateTime captureStartedAt = DateTime.MinValue;
    private DateTime captureNotReadySince = DateTime.MinValue;
    private DateTime captureHeartbeatUnhealthySince = DateTime.MinValue;
    private DateTime lastCaptureRecoveryAt = DateTime.MinValue;
    private bool setupWizardOpen;
    private bool bridgeReady;
    private DateTime keyboardBridgeStartedAt = DateTime.MinValue;
    private DateTime lastKeyboardBridgeRecoveryAt = DateTime.MinValue;
    private DateTime keyboardBridgeHealthUnhealthySince = DateTime.MinValue;
    private string expectedKeyboardConfigRevision = "";
    private DateTime lastKeyboardRootConflictLogAt = DateTime.MinValue;
    private DateTime lastSystemRecoveryAt = DateTime.MinValue;
    private string pendingSystemRecoveryReason = "";
    private string pendingCustomCaptureToken = "";
    private string pendingMappingTestToken = "";
    private string pendingMappingTestLabel = "";
    private DateTime pendingMappingTestStartedAt = DateTime.MinValue;
    private readonly Label[] customButtonStatusLabels = new Label[3];
    private DateTime activeStreamStarted = DateTime.MinValue;
    private RoundPanel toastPanel;
    private Label toastIcon;
    private Label toastLabel;
    private System.Windows.Forms.Timer toastTimer;
    private SoundPlayer dictationCompletePlayer;
    private SoundPlayer dictationErrorPlayer;
    private SoundPlayer dictationStopPlayer;
    private MemoryStream dictationCompleteSound;
    private MemoryStream dictationErrorSound;
    private MemoryStream dictationStopSound;
    private long runtimeFeedbackPosition;
    private long inputFeedbackPosition;
    private int lastFeedbackGeneration;
    private int updateOperationActive;
    private int currentPageIndex;
    private DateTime remoteHighlightUntil = DateTime.MinValue;
    private DateTime transientFeedbackUntil = DateTime.MinValue;
    private string transientFeedbackState = "";
    private string transientFeedbackText = "";
    private Color currentVisualAccent = Color.FromArgb(15, 158, 100);
    private string currentVisualState = "connecting";
    private DateTime windowsHardwareProbeAt = DateTime.MinValue;
    private WindowsHardwareProbe windowsHardwareProbe;
    private int windowsHardwareProbeRunning;
    private bool refreshSelfCheckOnActivate;

    [STAThread]
    private static void Main(string[] args)
    {
        if (Array.Exists(args, delegate(string arg) { return arg.Equals("--self-test", StringComparison.OrdinalIgnoreCase); }))
        {
            Environment.ExitCode = RunHostSelfTests();
            return;
        }
        TryEnableHighDpi();
        bool background = Array.Exists(args, delegate(string arg) { return arg.Equals("--background", StringComparison.OrdinalIgnoreCase); });
        bool uiResourceTest = Array.Exists(args, delegate(string arg) { return arg.Equals("--ui-resource-test", StringComparison.OrdinalIgnoreCase); });
        bool uiSmoke = uiResourceTest || Array.Exists(args, delegate(string arg) { return arg.Equals("--ui-smoke", StringComparison.OrdinalIgnoreCase); });
        bool createdNew;
        using (var instance = new Mutex(true, uiSmoke ? "Local\\VibeMicUiSmoke" : "Local\\VibeMic", out createdNew))
        {
            if (!createdNew)
            {
                bool replaceExisting = !uiSmoke && ExistingInstanceUsesDifferentPath();
                if (replaceExisting)
                {
                    SignalEvent("Local\\VibeMicExitForUpdate");
                    try { createdNew = instance.WaitOne(12000, false); }
                    catch (AbandonedMutexException) { createdNew = true; }
                }
                if (!createdNew)
                {
                    if (!background && !uiSmoke) SignalEvent("Local\\VibeMicShowWindow");
                    return;
                }
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new VibeMicForm(background, uiSmoke, uiResourceTest));
        }
    }

    private static void TryEnableHighDpi()
    {
        try
        {
            if (SetProcessDpiAwarenessContext(new IntPtr(-4))) return;
        }
        catch (EntryPointNotFoundException) { }
        catch (DllNotFoundException) { }
        try
        {
            if (SetProcessDpiAwareness(2) == 0) return;
        }
        catch (EntryPointNotFoundException) { }
        catch (DllNotFoundException) { }
        try { SetProcessDPIAware(); }
        catch { }
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

    private VibeMicForm(bool launchInBackground, bool smokeMode, bool resourceTestMode)
    {
        backgroundLaunch = launchInBackground;
        uiSmokeMode = smokeMode;
        uiResourceTestMode = resourceTestMode;
        string stateRoot = uiSmokeMode ? Path.Combine(root, "tmp", "ui-smoke") : GetUserStateRoot();
        Directory.CreateDirectory(stateRoot);
        string migratedConfigSource = uiSmokeMode ? "" : MigrateLegacyUserConfig(root, stateRoot);
        sessionDir = Path.Combine(stateRoot, "remote-voice-session");
        configPath = Path.Combine(stateRoot, "vibe-mic-config.json");
        eventsPath = Path.Combine(sessionDir, "remote-voice-events.jsonl");
        brandLogoPath = Path.Combine(root, "vibe-flow-logo.png");
        hostLogPath = Path.Combine(sessionDir, "vibe-flow-host.log");
        Directory.CreateDirectory(sessionDir);
        config = LoadConfig();
        if (uiSmokeMode)
        {
            config.setupCompleted = true;
            config.resumeSetupAfterRestart = false;
            config.launchAtStartup = false;
            config.startBridgeOnLaunch = false;
            config.minimizeToTray = false;
            File.WriteAllText(configPath, new JavaScriptSerializer().Serialize(config), Encoding.UTF8);
        }
        ApplyThemePalette();
        if (!uiSmokeMode) ClearPendingCustomButtonCapture("startup");
        // The bridge must never start from a stale packaged mapping file. Rebuild
        // it from the migrated user config before any background service starts.
        if (!uiSmokeMode) SyncKeyboardBridgeConfig();
        if (!uiSmokeMode) ReconcileLaunchAtStartupRegistration();
        if (!uiSmokeMode) ReleaseVoiceHotkey();
        RotateLogFile(Path.Combine(sessionDir, "vibe-mic-runtime.log"), 4 * 1024 * 1024);
        RotateLogFile(hostLogPath, 2 * 1024 * 1024);
        RotateLogFile(Path.Combine(root, "input-bridge-log.txt"), 4 * 1024 * 1024);
        if (!uiSmokeMode)
        {
            audioDuckingLease = new WindowsAudioDuckingLease(
                Path.Combine(sessionDir, "windows-audio-ducking-lease.json"), HostLog);
            InitializeFeedbackSounds();
        }
        string existingRuntimeLog = Path.Combine(sessionDir, "vibe-mic-runtime.log");
        runtimeFeedbackPosition = File.Exists(existingRuntimeLog) ? new FileInfo(existingRuntimeLog).Length : 0;
        string existingInputLog = Path.Combine(root, "input-bridge-log.txt");
        inputFeedbackPosition = File.Exists(existingInputLog) ? new FileInfo(existingInputLog).Length : 0;

        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = DisplayProductName + " · V" + ProductRelease;
        Width = 1280;
        Height = 840;
        MinimumSize = new Size(880, 500);
        StartPosition = FormStartPosition.CenterScreen;
        if (backgroundLaunch)
        {
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-32000, -32000);
            ShowInTaskbar = false;
        }
        BackColor = pageBackground;
        Font = new Font("Microsoft YaHei UI", 10f);
        Icon = CreateAppIcon();
        DoubleBuffered = true;

        BuildShell();
        ShowPage(PageHome);
        if (!uiSmokeMode) SetupTray();
        HostLog("HOST START mode=" + (backgroundLaunch ? "background" : "interactive") +
            " provider=" + NormalizeProviderKey(config.inputMethod) + " startup=" + config.launchAtStartup +
            " ui_smoke=" + uiSmokeMode);
        if (!string.IsNullOrWhiteSpace(migratedConfigSource))
            HostLog("CONFIG MIGRATED source=legacy_install destination=user_state source_file=" +
                SafeLogValue(migratedConfigSource));
        if (!uiSmokeMode)
        {
            showWindowEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\VibeMicShowWindow");
            exitApplicationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\VibeMicExitForUpdate");
            voiceWakeRequestEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\VibeMicVoiceWakeRequested");
            providerHotkeyTapEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\VibeMicProviderHotkeyTapRequested");
            providerHotkeyDownEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\VibeMicProviderHotkeyDownRequested");
            providerHotkeyUpEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\VibeMicProviderHotkeyUpRequested");
            inputTargetMissingEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\VibeMicInputTargetMissing");
            recordingStartCueEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\VibeMicRecordingStartCue");
            recordingStopCueEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\VibeMicRecordingStopCue");
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    WaitHandle[] handles = { showWindowEvent, exitApplicationEvent, voiceWakeRequestEvent,
                        providerHotkeyTapEvent, providerHotkeyDownEvent, providerHotkeyUpEvent, inputTargetMissingEvent };
                    while (true)
                    {
                        int signal = WaitHandle.WaitAny(handles);
                        if (IsDisposed || applicationExiting) return;
                        if (signal == 0) BeginInvoke(new Action(ShowMainWindow));
                        else if (signal == 1) BeginInvoke(new Action(delegate { config.minimizeToTray = false; Close(); }));
                        else if (signal == 2) BeginInvoke(new Action(HandleVoiceWakeRequest));
                        else if (signal == 3) HandleProviderHotkeyTapRequest();
                        else if (signal == 4 || signal == 5) HandleProviderHotkeyHoldRequest(signal == 4);
                        else BeginInvoke(new Action(HandleMissingInputTarget));
                    }
                }
                catch { }
            });
            StartRecordingCueWorker();
        }

        if (!uiSmokeMode)
        {
            activityTimer = new System.Windows.Forms.Timer();
            activityTimer.Interval = 500;
            activityTimer.Tick += delegate { PollActivity(); };
            activityTimer.Start();
        }

        visualTimer = new System.Windows.Forms.Timer();
        visualTimer.Interval = 50;
        visualTimer.Tick += delegate
        {
            if (!Visible || WindowState == FormWindowState.Minimized)
            {
                if (visualTimer.Interval != 500) visualTimer.Interval = 500;
                return;
            }
            bool animatedState = currentVisualState == "recording" || currentVisualState == "recovering" ||
                currentVisualState == "processing" || currentVisualState == "connecting";
            if (!animatedState)
            {
                if (visualTimer.Interval != 250) visualTimer.Interval = 250;
                return;
            }
            if (visualTimer.Interval != 50) visualTimer.Interval = 50;
            if (animatedState && remoteVisual != null && !remoteVisual.IsDisposed)
            {
                remoteVisual.AnimationPhase += 0.11f;
                remoteVisual.Invalidate();
            }
            if (heroPanel != null && !heroPanel.IsDisposed &&
                animatedState)
                heroPanel.Invalidate();
        };
        visualTimer.Start();
        if (!uiSmokeMode)
        {
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            SystemEvents.SessionSwitch += OnSessionSwitch;
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            if (config.autoCheckUpdates) ScheduleAutomaticUpdateCheck();
        }
        if (uiResourceTestMode) Shown += delegate { BeginInvoke(new Action(RunPageResourceTest)); };
    }

    private void ClampWindowToWorkingArea()
    {
        Rectangle work = Screen.FromControl(this).WorkingArea;
        int targetWidth = Math.Min(Width, Math.Max(MinimumSize.Width, work.Width - 32));
        int targetHeight = Math.Min(Height, Math.Max(MinimumSize.Height, work.Height - 32));
        if (targetWidth != Width || targetHeight != Height) Size = new Size(targetWidth, targetHeight);
        int x = Math.Max(work.Left, work.Left + (work.Width - Width) / 2);
        int y = Math.Max(work.Top, work.Top + (work.Height - Height) / 2);
        Location = new Point(x, y);
    }

    private uint CurrentWindowDpi()
    {
        try { return GetDpiForWindow(Handle); }
        catch
        {
            using (Graphics graphics = CreateGraphics()) return (uint)Math.Round(graphics.DpiX);
        }
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume) ScheduleSystemRecovery("power_resume");
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionUnlock ||
            e.Reason == SessionSwitchReason.ConsoleConnect ||
            e.Reason == SessionSwitchReason.RemoteConnect)
            ScheduleSystemRecovery("session_" + e.Reason.ToString().ToLowerInvariant());
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (applicationExiting || config == null ||
            !string.Equals(config.theme, "system", StringComparison.OrdinalIgnoreCase)) return;
        try
        {
            BeginInvoke(new Action(delegate
            {
                bool previousDarkTheme = darkTheme;
                ApplyThemePalette();
                if (previousDarkTheme != darkTheme)
                {
                    RebuildShellForTheme();
                    ShowToast("界面已跟随 Windows 切换", "success");
                }
            }));
        }
        catch { }
    }

    private void ScheduleSystemRecovery(string reason)
    {
        if (applicationExiting || !config.setupCompleted || !config.startBridgeOnLaunch) return;
        pendingSystemRecoveryReason = reason;
        if (systemRecoveryTimer != null) return;
        systemRecoveryTimer = new System.Windows.Forms.Timer();
        systemRecoveryTimer.Interval = 2500;
        systemRecoveryTimer.Tick += delegate
        {
            systemRecoveryTimer.Stop();
            systemRecoveryTimer.Dispose();
            systemRecoveryTimer = null;
            string recoveryReason = string.IsNullOrWhiteSpace(pendingSystemRecoveryReason)
                ? "system_resume" : pendingSystemRecoveryReason;
            pendingSystemRecoveryReason = "";
            RecoverServicesAfterSystemChange(recoveryReason);
        };
        systemRecoveryTimer.Start();
        HostLog("SYSTEM RECOVERY scheduled=true reason=" + reason + " delay_ms=2500");
    }

    private void RecoverServicesAfterSystemChange(string reason)
    {
        if (applicationExiting || !config.setupCompleted || !config.startBridgeOnLaunch) return;
        if ((DateTime.UtcNow - lastSystemRecoveryAt).TotalSeconds < 10)
        {
            HostLog("SYSTEM RECOVERY skipped=true reason=throttled trigger=" + reason);
            return;
        }
        lastSystemRecoveryAt = DateTime.UtcNow;
        HostLog("SYSTEM RECOVERY begin=true reason=" + reason + " action=restart_bridge_and_capture");
        bridgeReady = false;
        StopCapture();
        StopKeyboardBridge();
        StartCapture();
        ShowToast("系统恢复后正在重新连接遥控器", "info");
    }

    private void ApplyThemePalette()
    {
        string preference = config == null ? "light" : (config.theme ?? "light").Trim().ToLowerInvariant();
        darkTheme = preference == "dark" || (preference == "system" && WindowsUsesDarkApps());
        if (darkTheme)
        {
            ink = Color.FromArgb(229, 232, 239);
            muted = Color.FromArgb(153, 161, 177);
            violet = Color.FromArgb(126, 118, 213);
            green = Color.FromArgb(76, 174, 127);
            amber = Color.FromArgb(205, 157, 81);
            cyan = Color.FromArgb(79, 163, 181);
            coral = Color.FromArgb(205, 101, 110);
            line = Color.FromArgb(55, 59, 69);
            pageBackground = Color.FromArgb(25, 26, 31);
            sidebarBackground = Color.FromArgb(29, 30, 36);
            cardBackground = Color.FromArgb(35, 37, 44);
            surfaceBackground = Color.FromArgb(41, 43, 51);
            inputBackground = Color.FromArgb(31, 33, 39);
        }
        else
        {
            ink = Color.FromArgb(18, 30, 54);
            muted = Color.FromArgb(91, 104, 134);
            violet = Color.FromArgb(104, 82, 244);
            green = Color.FromArgb(10, 164, 104);
            amber = Color.FromArgb(229, 151, 39);
            cyan = Color.FromArgb(0, 153, 190);
            coral = Color.FromArgb(204, 70, 82);
            line = Color.FromArgb(220, 226, 239);
            pageBackground = Color.FromArgb(245, 247, 251);
            sidebarBackground = Color.FromArgb(249, 251, 255);
            cardBackground = Color.White;
            surfaceBackground = Color.FromArgb(246, 249, 253);
            inputBackground = Color.White;
        }
    }

    private void ApplyThemePreference(string preference)
    {
        string normalized = (preference ?? "light").Trim().ToLowerInvariant();
        string selected = normalized == "dark" ? "dark" : normalized == "system" ? "system" : "light";
        if (string.Equals(config.theme, selected, StringComparison.OrdinalIgnoreCase))
        {
            ShowToast(selected == "dark" ? "当前已是夜间模式" :
                selected == "system" ? "当前已跟随 Windows" : "当前已是白天模式", "info");
            return;
        }

        config.theme = selected;
        SaveConfig();
        ApplyThemePalette();
        RebuildShellForTheme();
        ShowToast(selected == "dark" ? "已切换到夜间模式" :
            selected == "system" ? "已改为跟随 Windows" : "已切换到白天模式", "success");
    }

    private void RebuildShellForTheme()
    {
        int page = currentPageIndex;
        SuspendLayout();
        if (toastTimer != null)
        {
            toastTimer.Stop();
            toastTimer.Dispose();
            toastTimer = null;
        }
        if (connectionBadge.Parent != null) connectionBadge.Parent.Controls.Remove(connectionBadge);

        DisposePageControls();

        var existing = new Control[Controls.Count];
        Controls.CopyTo(existing, 0);
        foreach (Control control in existing)
        {
            if (ReferenceEquals(control, content)) continue;
            Controls.Remove(control);
            DisposeOwnedControlResources(control);
            control.Dispose();
        }

        Controls.Clear();
        navButtons.Clear();
        toastPanel = null;
        toastIcon = null;
        toastLabel = null;
        BackColor = pageBackground;
        BuildShell();
        ShowPage(page);
        ResumeLayout(true);
        Invalidate(true);
    }

    private string GetUserStateRoot()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData)) return root;
        return Path.Combine(localAppData, "Vibe Flow Remote", "UserData");
    }

    private static string MigrateLegacyUserConfig(string legacyRoot, string stateRoot)
    {
        string destination = Path.Combine(stateRoot, "vibe-mic-config.json");
        if (File.Exists(destination)) return "";
        var legacyRoots = new List<string>();
        AddLegacyRootCandidate(legacyRoots, legacyRoot);
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
            AddLegacyRootCandidate(legacyRoots, Path.Combine(localAppData, "Programs", "Vibe Flow Remote"));
        AddLegacyRootCandidate(legacyRoots, ReadStartupExecutableDirectory());

        var candidates = new List<string>();
        foreach (string candidateRoot in legacyRoots)
        {
            candidates.Add(Path.Combine(candidateRoot, "vibe-mic-config.json"));
            candidates.Add(Path.Combine(candidateRoot, "vibe-mic-config.json.bak"));
        }
        foreach (string candidate in candidates)
        {
            try
            {
                if (!File.Exists(candidate)) continue;
                VibeMicConfig parsed = new JavaScriptSerializer().Deserialize<VibeMicConfig>(
                    File.ReadAllText(candidate, Encoding.UTF8));
                if (parsed == null) continue;
                Directory.CreateDirectory(stateRoot);
                File.Copy(candidate, destination, false);
                string legacyBackup = Path.Combine(Path.GetDirectoryName(candidate), "vibe-mic-config.json.bak");
                string destinationBackup = destination + ".bak";
                if (!candidate.Equals(legacyBackup, StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(legacyBackup) && !File.Exists(destinationBackup))
                    File.Copy(legacyBackup, destinationBackup, false);
                return candidate;
            }
            catch { }
        }
        return "";
    }

    private static void AddLegacyRootCandidate(List<string> roots, string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate)) return;
        string normalized;
        try { normalized = Path.GetFullPath(candidate.Trim()); }
        catch { return; }
        foreach (string existing in roots)
            if (existing.Equals(normalized, StringComparison.OrdinalIgnoreCase)) return;
        roots.Add(normalized);
    }

    private static string ReadStartupExecutableDirectory()
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                "Software\\Microsoft\\Windows\\CurrentVersion\\Run", false))
            {
                string command = key == null ? "" : Convert.ToString(key.GetValue("Vibe Flow"));
                int executableEnd = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
                if (executableEnd < 0) return "";
                string executablePath = command.Substring(0, executableEnd + 4).Trim().Trim('"');
                return Path.GetDirectoryName(executablePath) ?? "";
            }
        }
        catch { return ""; }
    }

    private static void DisposeOwnedControlResources(Control control)
    {
        if (control == null) return;
        foreach (Control child in control.Controls) DisposeOwnedControlResources(child);
        PictureBox picture = control as PictureBox;
        if (picture != null && picture.Image != null)
        {
            Image image = picture.Image;
            picture.Image = null;
            image.Dispose();
        }
        Button button = control as Button;
        if (button != null && button.Image != null)
        {
            Image image = button.Image;
            button.Image = null;
            image.Dispose();
        }
        IDisposable ownedTag = control.Tag as IDisposable;
        if (ownedTag != null)
        {
            control.Tag = null;
            ownedTag.Dispose();
        }
    }

    private void DisposePageControls()
    {
        remoteVisual = null;
        heroPanel = null;
        heroTitle = null;
        heroSubtitle = null;
        heroStateLabel = null;
        activityLabel = null;
        bridgeButton = null;
        voiceBridgeStateLabel = null;
        actionReceiptGlyph = null;
        actionReceiptTitle = null;
        actionReceiptDetail = null;
        logBox = null;
        for (int i = 0; i < overviewStatusValues.Length; i++)
        {
            overviewStatusValues[i] = null;
            overviewStatusGlyphs[i] = null;
        }
        while (content.Controls.Count > 0)
        {
            Control control = content.Controls[0];
            content.Controls.RemoveAt(0);
            DisposeOwnedControlResources(control);
            control.Dispose();
        }
    }

    private void RunPageResourceTest()
    {
        using (Process process = Process.GetCurrentProcess())
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            int startUser = GetGuiResources(process.Handle, 1);
            int startGdi = GetGuiResources(process.Handle, 0);
            for (int i = 0; i < 300; i++)
            {
                ShowPage(i % 5);
                if (i % 5 == 0) Application.DoEvents();
            }
            ShowPage(PageHome);
            Application.DoEvents();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Application.DoEvents();
            int endUser = GetGuiResources(process.Handle, 1);
            int endGdi = GetGuiResources(process.Handle, 0);
            int userDelta = endUser - startUser;
            int gdiDelta = endGdi - startGdi;
            string result = "UI resource test: switches=300 USER=" + startUser + "->" + endUser +
                " (delta " + userDelta + ") GDI=" + startGdi + "->" + endGdi + " (delta " + gdiDelta + ")";
            Console.WriteLine(result);
            string reportDirectory = Path.Combine(root, "tmp");
            Directory.CreateDirectory(reportDirectory);
            File.WriteAllText(Path.Combine(reportDirectory, "ui-resource-test.txt"), result, Encoding.UTF8);
            Environment.ExitCode = userDelta <= 120 && gdiDelta <= 50 ? 0 : 1;
        }
        config.minimizeToTray = false;
        Close();
    }

    private static bool IsStableCaptureRuntime(string runtime)
    {
        if (string.IsNullOrWhiteSpace(runtime)) return true;
        return runtime.IndexOf("recording_kernel=v1.0.3", StringComparison.OrdinalIgnoreCase) >= 0 &&
            runtime.IndexOf("voice_state_machine=v11", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static int RunHostSelfTests()
    {
        try
        {
            VibeMicConfig defaults = VibeMicConfig.Default();
            if (defaults.schemaVersion != ConfigSchemaVersion || defaults.voiceMode != "hold" ||
                defaults.onboardingVersion != CurrentOnboardingVersion || defaults.onboardingStep != 0 ||
                defaults.theme != "light" || defaults.resumeSetupAfterRestart ||
                defaults.inputRoutingMode != "strict")
                throw new InvalidOperationException("Default configuration invariant failed");
            if (!HasStableVoiceProfile(defaults) || defaults.gain != StableVoiceGain ||
                defaults.drainMs != StableVoiceDrainMs || defaults.audioEndpointName != StableVoiceEndpoint)
                throw new InvalidOperationException("Stable voice profile invariant failed");
            if (defaults.mappings["功能键:short"] != "ctrl+c" || defaults.mappings["功能键:long"] != "ctrl+v" ||
                defaults.mappings["TV"] != "task-switcher" || defaults.mappings["Home"] != "win+d" ||
                defaults.mappings["Home:short"] != "win+d" || defaults.mappings["Home:long"] != "none" ||
                defaults.mappings["确认键"] != "enter" || defaults.mappings["上键"] != "up" ||
                defaults.mappings["下键"] != "down" || defaults.mappings["左键"] != "left" ||
                defaults.mappings["右键"] != "right" || defaults.mappings.Count != 12)
                throw new InvalidOperationException("Default remote mapping invariant failed");
            if (defaults.shortcutProfiles == null || defaults.shortcutProfiles.Length != 4 ||
                defaults.activeShortcutProfileId != "general" ||
                FindShortcutProfile(defaults, "vibe-coding") == null ||
                FindShortcutProfile(defaults, "browser-ai") == null ||
                FindShortcutProfile(defaults, "terminal-agent") == null ||
                FindShortcutProfile(defaults, "vibe-coding").mappings["上键"] != "ctrl+z" ||
                FindShortcutProfile(defaults, "browser-ai").mappings["上键"] != "pageup" ||
                FindShortcutProfile(defaults, "browser-ai").mappings["左键"] != "browserback" ||
                FindShortcutProfile(defaults, "terminal-agent").mappings["Home:short"] != "launch-client:terminal")
                throw new InvalidOperationException("Official shortcut Profile invariant failed");

            VibeMicConfig browserBackFixture = VibeMicConfig.Default();
            browserBackFixture.schemaVersion = 30;
            ShortcutProfileConfig legacyBrowserProfile = FindShortcutProfile(browserBackFixture, "browser-ai");
            legacyBrowserProfile.mappings["左键"] = "alt+left";
            browserBackFixture.activeShortcutProfileId = "browser-ai";
            browserBackFixture.mappings = CloneMappings(legacyBrowserProfile.mappings);
            if (NormalizePhysicalMappingAction("左键", "alt+left") != "browserback" ||
                NormalizePhysicalMappingAction("上键", "alt+left") != "alt+left" ||
                !MigrateConfig(browserBackFixture) || browserBackFixture.schemaVersion != ConfigSchemaVersion ||
                browserBackFixture.mappings["左键"] != "browserback" ||
                FindShortcutProfile(browserBackFixture, "browser-ai").mappings["左键"] != "browserback")
                throw new InvalidOperationException("Schema 30 browser-back migration retained a conflicting Alt+Left action");

            VibeMicConfig legacyProfileFixture = VibeMicConfig.Default();
            legacyProfileFixture.schemaVersion = 29;
            legacyProfileFixture.shortcutProfiles = null;
            legacyProfileFixture.activeShortcutProfileId = "";
            legacyProfileFixture.mappings["上键"] = "win+shift+s";
            double legacyGain = legacyProfileFixture.gain;
            string legacyEndpoint = legacyProfileFixture.audioEndpointName;
            if (!MigrateConfig(legacyProfileFixture) ||
                legacyProfileFixture.activeShortcutProfileId != "my-shortcuts" ||
                legacyProfileFixture.shortcutProfiles == null || legacyProfileFixture.shortcutProfiles.Length != 5 ||
                legacyProfileFixture.mappings["上键"] != "win+shift+s")
                throw new InvalidOperationException("Schema 29 shortcut Profile migration discarded the active mapping");
            legacyProfileFixture.activeShortcutProfileId = "vibe-coding";
            ProjectActiveShortcutProfile(legacyProfileFixture);
            if (legacyProfileFixture.mappings["上键"] != "ctrl+z" ||
                legacyProfileFixture.gain != legacyGain || legacyProfileFixture.audioEndpointName != legacyEndpoint ||
                !HasStableVoiceProfile(legacyProfileFixture))
                throw new InvalidOperationException("Shortcut Profile switching changed the frozen voice profile");
            legacyProfileFixture.mappings["右键"] = "win+shift+s";
            legacyProfileFixture.mappingPreset = "custom";
            CaptureActiveShortcutProfileMappings(legacyProfileFixture);
            legacyProfileFixture.activeShortcutProfileId = "general";
            ProjectActiveShortcutProfile(legacyProfileFixture);
            legacyProfileFixture.activeShortcutProfileId = "vibe-coding";
            ProjectActiveShortcutProfile(legacyProfileFixture);
            if (legacyProfileFixture.mappings["右键"] != "win+shift+s")
                throw new InvalidOperationException("Profile-specific mapping did not survive a manual switch round trip");
            var profileExportFixture = new ShortcutProfileExport
            {
                format = "vibe-flow-shortcut-profile",
                version = 1,
                profile = CloneShortcutProfile(ActiveShortcutProfile(legacyProfileFixture), "export-test", "Export Test")
            };
            string profileExportJson = new JavaScriptSerializer().Serialize(profileExportFixture);
            if (profileExportJson.IndexOf("audioEndpointName", StringComparison.OrdinalIgnoreCase) >= 0 ||
                profileExportJson.IndexOf("inputMethodHotkey", StringComparison.OrdinalIgnoreCase) >= 0 ||
                new JavaScriptSerializer().Deserialize<ShortcutProfileExport>(profileExportJson).profile.mappings["右键"] != "win+shift+s")
                throw new InvalidOperationException("Profile export leaked voice settings or lost mappings");
            List<ShortcutChoice> directionChoices = ShortcutChoicesFor("上键", "win+shift+s");
            if (!IsSupportedMappingAction("win+shift+s") ||
                FindShortcutChoice(directionChoices, "win+shift+s") <= 0 ||
                CustomActionText("win+shift+s") != "区域截图")
                throw new InvalidOperationException("Direction screenshot action invariant failed");
            if (!IsSupportedMappingAction("open-url:https://example.com") ||
                !IsSupportedMappingAction("open-exe:C:\\Tools\\example.exe") ||
                !IsSupportedMappingAction("shortcut:ctrl+shift+p") ||
                MappingActionChoicesFor("TV", "open-url:https://example.com").Count < 10)
                throw new InvalidOperationException("Custom mapping action invariant failed");
            var startAppsFixture = new List<StartApplicationRecord>();
            var startAppsSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddStartApplicationRecord(startAppsFixture, startAppsSeen, "微信输入法", "测试.应用");
            AddStartApplicationRecord(startAppsFixture, startAppsSeen, "重复项", "测试.应用");
            AddStartApplicationRecord(startAppsFixture, startAppsSeen, "损坏\uFFFD名称", "损坏.应用");
            if (startAppsFixture.Count != 1 || startAppsFixture[0].Name != "微信输入法" ||
                startAppsFixture[0].AppID != "测试.应用")
                throw new InvalidOperationException("Start application Unicode invariant failed");
            string parsedDisplayIcon = NormalizeRegisteredPath("\"C:\\Tools\\Example App\\example.exe\",0", true);
            if (parsedDisplayIcon != "C:\\Tools\\Example App\\example.exe" ||
                NormalizeRegisteredPath("C:\\Tools\\example.dll,-12", true) != "C:\\Tools\\example.dll")
                throw new InvalidOperationException("Registered application icon path normalization failed");
            if (!IsUtilityExecutable("C:\\Tools\\VBCABLE_Setup_x64.exe") ||
                IsUtilityExecutable("C:\\Tools\\GoogleChrome.exe") ||
                !IsPackagedApplicationPath("C:\\Program Files\\WindowsApps\\Example\\app.exe"))
                throw new InvalidOperationException("Installed application catalog accepted an installer or duplicate packaged entry");
            if (!IsStableCaptureRuntime("recording_kernel=v1.0.3 voice_state_machine=v11") ||
                IsStableCaptureRuntime("long_dictation_state_machine=v3"))
                throw new InvalidOperationException("Stable capture runtime detection invariant failed");
            var failedHardwareProbe = new WindowsHardwareProbe
            {
                Completed = true,
                Failed = true,
                Error = "timeout"
            };
            var liveRc003Bridge = new BridgeHealthSnapshot
            {
                Healthy = true,
                RawInputDevicePresent = true
            };
            if (ResolveBluetoothSelfCheckState(failedHardwareProbe, liveRc003Bridge) != "pass" ||
                ResolveBluetoothSelfCheckState(failedHardwareProbe, new BridgeHealthSnapshot()) != "fail" ||
                ResolveBluetoothSelfCheckState(new WindowsHardwareProbe(), new BridgeHealthSnapshot()) != "checking")
                throw new InvalidOperationException("Bluetooth self-check evidence fallback invariant failed");

            VibeMicConfig presetFixture = VibeMicConfig.Default();
            presetFixture.mappings["Home:long"] = "open-url:https://example.com/home";
            presetFixture.mappings["功能键:short"] = "shortcut:ctrl+shift+p";
            ApplyMappingPreset(presetFixture, "editing");
            if (presetFixture.mappings["上键"] != "ctrl+z" ||
                presetFixture.mappings["下键"] != "ctrl+shift+z" ||
                presetFixture.mappings["左键"] != "ctrl+c" ||
                presetFixture.mappings["右键"] != "ctrl+v" ||
                presetFixture.mappings["Home:long"] != "open-url:https://example.com/home" ||
                presetFixture.mappings["功能键:short"] != "shortcut:ctrl+shift+p")
                throw new InvalidOperationException("Vibe Coding preset invariant failed");
            ApplyMappingPreset(presetFixture, "review");
            if (presetFixture.mappings["上键"] != "volumeup" ||
                presetFixture.mappings["下键"] != "volumedown" ||
                presetFixture.mappings["TV"] != "mediaplaypause" ||
                presetFixture.mappings["Home:long"] != "open-url:https://example.com/home" ||
                presetFixture.mappings["功能键:short"] != "shortcut:ctrl+shift+p")
                throw new InvalidOperationException("Media preset invariant failed");
            if (MappingPresetChanges(presetFixture, "review") ||
                !MappingPresetChanges(presetFixture, "coding"))
                throw new InvalidOperationException("Preset change detection invariant failed");

            VibeMicConfig importedFixture = VibeMicConfig.Default();
            importedFixture.schemaVersion = 25;
            importedFixture.gain = 3.0;
            importedFixture.audioEndpointName = "Other endpoint";
            importedFixture.mappings["上键"] = "open-url:https://example.com";
            NormalizeImportedConfig(importedFixture);
            if (!HasStableVoiceProfile(importedFixture) ||
                importedFixture.mappings["上键"] != "open-url:https://example.com")
                throw new InvalidOperationException("Imported config did not preserve mappings and freeze voice settings");

            string sanitizedDiagnostic = SanitizeDiagnosticText(
                "user=C:\\Users\\PrivateUser\\secret address=AA:BB:CC:DD:EE:FF " +
                "hash:ABCDEF123456 name=\\\\?\\HID#PRIVATE action=open-url:https://example.com/private");
            if (sanitizedDiagnostic.IndexOf("PrivateUser", StringComparison.OrdinalIgnoreCase) >= 0 ||
                sanitizedDiagnostic.IndexOf("AA:BB", StringComparison.OrdinalIgnoreCase) >= 0 ||
                sanitizedDiagnostic.IndexOf("HID#PRIVATE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                sanitizedDiagnostic.IndexOf("example.com", StringComparison.OrdinalIgnoreCase) >= 0)
                throw new InvalidOperationException("Diagnostic privacy redaction invariant failed");

            VibeMicConfig continuous = VibeMicConfig.Default();
            continuous.schemaVersion = 24;
            continuous.onboardingVersion = 8;
            continuous.voiceMode = "continuous";
            continuous.onboardingStep = 7;
            continuous.theme = "system";
            continuous.inputMethodHotkey = WeChatV12Hotkey;
            continuous.mappings["电源键:short"] = "open-url:https://example.com";
            if (!MigrateConfig(continuous) || continuous.schemaVersion != ConfigSchemaVersion ||
                continuous.voiceMode != "hold" || continuous.onboardingStep != 3 || continuous.theme != "system" ||
                continuous.inputMethodHotkey != WeChatStableHotkey ||
                continuous.mappings["Home:long"] != "open-url:https://example.com" ||
                continuous.mappings.ContainsKey("电源键:short") ||
                continuous.mappings["TV"] != "task-switcher" || continuous.mappings["上键"] != "up")
                throw new InvalidOperationException("Schema 24 migration did not restore the stable hardware profile");

            VibeMicConfig customized = VibeMicConfig.Default();
            customized.schemaVersion = 25;
            customized.mappings["TV"] = "open-url:https://example.com";
            customized.mappings["Home"] = "open-exe:C:\\Tools\\example.exe";
            customized.mappings["上键"] = "shortcut:ctrl+shift+p";
            customized.mappings.Remove("Home:short");
            customized.mappings.Remove("Home:long");
            if (!MigrateConfig(customized) || customized.schemaVersion != ConfigSchemaVersion ||
                customized.mappings["TV"] != "open-url:https://example.com" ||
                customized.mappings["Home:short"] != "open-exe:C:\\Tools\\example.exe" ||
                customized.mappings["上键"] != "shortcut:ctrl+shift+p")
                throw new InvalidOperationException("Schema 25 migration discarded a valid custom mapping");

            VibeMicConfig schema26 = VibeMicConfig.Default();
            schema26.schemaVersion = 26;
            schema26.mappings["Home:short"] = "open-url:https://example.com/home";
            schema26.mappings["Home:long"] = "shortcut:ctrl+shift+p";
            schema26.mappings["电源键:short"] = "open-exe:C:\\Tools\\example.exe";
            schema26.mappings["电源键:long"] = "launch-client:codex";
            if (!MigrateConfig(schema26) ||
                schema26.mappings["Home:short"] != "open-url:https://example.com/home" ||
                schema26.mappings["Home:long"] != "shortcut:ctrl+shift+p" ||
                schema26.mappings.ContainsKey("电源键:short") ||
                schema26.mappings.ContainsKey("电源键:long"))
                throw new InvalidOperationException("Schema 26 migration discarded a valid gesture mapping");

            VibeMicConfig retiredPower = VibeMicConfig.Default();
            retiredPower.schemaVersion = 27;
            retiredPower.mappings["Home:long"] = "none";
            retiredPower.mappings["电源键:short"] = "open-exe:C:\\Tools\\example.exe";
            retiredPower.mappings["电源键:long"] = "none";
            if (!MigrateConfig(retiredPower) ||
                retiredPower.mappings["Home:long"] != "open-exe:C:\\Tools\\example.exe" ||
                retiredPower.mappings.ContainsKey("电源键:short") ||
                retiredPower.mappings.ContainsKey("电源键:long"))
                throw new InvalidOperationException("Retired power action was not transferred to Home long press");
            string migratedSchema26 = new JavaScriptSerializer().Serialize(schema26);
            if (MigrateConfig(schema26) ||
                new JavaScriptSerializer().Serialize(schema26) != migratedSchema26)
                throw new InvalidOperationException("Configuration migration is not idempotent");

            VibeMicConfig retiredRouting = VibeMicConfig.Default();
            retiredRouting.inputRoutingMode = "compatibility";
            if (!MigrateConfig(retiredRouting) || retiredRouting.inputRoutingMode != "strict" ||
                MigrateConfig(retiredRouting))
                throw new InvalidOperationException("Retired compatibility mode did not migrate to strict exactly once");

            VibeMicConfig mappingFixture = VibeMicConfig.Default();
            mappingFixture.mappings["上键"] = "win+shift+s";
            mappingFixture.mappings["下键"] = "none";
            mappingFixture.mappings["Home:long"] = "shortcut:ctrl+shift+p";
            Dictionary<string, object> bridgeDocument = BuildKeyboardBridgeDocument(mappingFixture);
            Dictionary<string, object> generatedUp = FindGeneratedBridgeMapping(bridgeDocument, "up", "keyboard");
            Dictionary<string, object> generatedDown = FindGeneratedBridgeMapping(bridgeDocument, "down", "keyboard");
            Dictionary<string, object> generatedHome = FindGeneratedBridgeMapping(bridgeDocument, "home", "keyboard");
            string bridgeRevision = Convert.ToString(bridgeDocument["revision"]);
            if (generatedUp == null || Convert.ToString(generatedUp["shortcut"]) != "win+shift+s" ||
                !Convert.ToBoolean(generatedUp["enabled"]) || Convert.ToString(generatedUp["mode"]) != "tap" ||
                generatedDown == null || Convert.ToBoolean(generatedDown["enabled"]) ||
                Convert.ToBoolean(generatedDown["suppress"]) || Convert.ToString(generatedDown["mode"]) != "passthrough" ||
                generatedHome == null || Convert.ToString(generatedHome["shortShortcut"]) != "win+d" ||
                Convert.ToString(generatedHome["longShortcut"]) != "shortcut:ctrl+shift+p" ||
                FindGeneratedBridgeMapping(bridgeDocument, "power", "keyboard") != null ||
                FindGeneratedBridgeMapping(bridgeDocument, "power", "hid") != null ||
                string.IsNullOrWhiteSpace(bridgeRevision) ||
                Convert.ToInt32(bridgeDocument["version"]) != 6 ||
                Convert.ToString(bridgeDocument["activeShortcutProfileId"]) != "general" ||
                Convert.ToString(bridgeDocument["activeShortcutProfileName"]) != "通用导航" ||
                Convert.ToString(bridgeDocument["inputRoutingMode"]) != "strict" ||
                Convert.ToString(BuildKeyboardBridgeDocument(mappingFixture)["revision"]) != bridgeRevision)
                throw new InvalidOperationException("UI configuration did not normalize to the expected bridge actions");
            mappingFixture.inputRoutingMode = "compatibility";
            Dictionary<string, object> compatibilityBridge = BuildKeyboardBridgeDocument(mappingFixture);
            if (Convert.ToString(compatibilityBridge["inputRoutingMode"]) != "strict" ||
                Convert.ToString(compatibilityBridge["revision"]) != bridgeRevision)
                throw new InvalidOperationException("Retired compatibility routing was not normalized to strict");
            mappingFixture.inputRoutingMode = "strict";
            mappingFixture.mappings["上键"] = "ctrl+c";
            if (Convert.ToString(BuildKeyboardBridgeDocument(mappingFixture)["revision"]) == bridgeRevision)
                throw new InvalidOperationException("Bridge configuration revision did not change with its action mapping");

            VibeMicConfig browserBridgeFixture = VibeMicConfig.Default();
            browserBridgeFixture.activeShortcutProfileId = "browser-ai";
            ProjectActiveShortcutProfile(browserBridgeFixture);
            Dictionary<string, object> browserBridgeDocument = BuildKeyboardBridgeDocument(browserBridgeFixture);
            Dictionary<string, object> generatedBrowserBack = FindGeneratedBridgeMapping(
                browserBridgeDocument, "left", "keyboard");
            if (generatedBrowserBack == null ||
                Convert.ToString(generatedBrowserBack["shortcut"]) != "browserback" ||
                !Convert.ToBoolean(generatedBrowserBack["enabled"]) ||
                !Convert.ToBoolean(generatedBrowserBack["suppress"]))
                throw new InvalidOperationException("Browser Profile did not route physical Left to the dedicated browser-back key");

            string fallbackAppId = "Microsoft.WindowsNotepad_8wekyb3d8bbwe!App";
            string applicationAction = BuildOpenApplicationAction(
                "notepad", "C:\\Windows\\System32\\notepad.exe", "Notepad", fallbackAppId);
            string[] applicationActionParts = applicationAction.Substring("open-app:".Length).Split('|');
            if (!IsPersistableMappingAction(applicationAction) || applicationActionParts.Length != 4 ||
                DecodeActionPart(applicationActionParts[3]) != fallbackAppId)
                throw new InvalidOperationException("Resolved local application action was not persistable");
            mappingFixture.mappings["上键"] = applicationAction;
            string mappingPath = Path.Combine(Path.GetTempPath(),
                "vibe-flow-mapping-test-" + Guid.NewGuid().ToString("N") + ".json");
            string mappingBackup = mappingPath + ".bak";
            try
            {
                WriteTextAtomically(mappingPath, new JavaScriptSerializer().Serialize(mappingFixture), mappingBackup);
                VibeMicConfig persistedMapping = new JavaScriptSerializer().Deserialize<VibeMicConfig>(
                    File.ReadAllText(mappingPath, Encoding.UTF8));
                Dictionary<string, object> persistedBridge = BuildKeyboardBridgeDocument(persistedMapping);
                Dictionary<string, object> persistedUp = FindGeneratedBridgeMapping(persistedBridge, "up", "keyboard");
                if (persistedMapping == null || persistedMapping.mappings["上键"] != applicationAction ||
                    persistedUp == null || Convert.ToString(persistedUp["shortcut"]) != applicationAction ||
                    !Convert.ToBoolean(persistedUp["enabled"]))
                    throw new InvalidOperationException("Local application action did not survive config persistence and bridge generation");
            }
            finally
            {
                try { if (File.Exists(mappingPath)) File.Delete(mappingPath); } catch { }
                try { if (File.Exists(mappingBackup)) File.Delete(mappingBackup); } catch { }
                try { if (File.Exists(mappingPath + ".tmp")) File.Delete(mappingPath + ".tmp"); } catch { }
            }
            var acknowledgedHealth = new Dictionary<string, object>();
            acknowledgedHealth["state"] = "running";
            acknowledgedHealth["hook_installed"] = true;
            acknowledgedHealth["raw_input_registered"] = true;
            acknowledgedHealth["raw_input_device_present"] = false;
            acknowledgedHealth["config_revision"] = bridgeRevision;
            acknowledgedHealth["config_error"] = "";
            acknowledgedHealth["pid"] = 42;
            if (!BridgeHealthAcknowledgesRevision(acknowledgedHealth, bridgeRevision, 42) ||
                BridgeHealthAcknowledgesRevision(acknowledgedHealth, bridgeRevision, 43) ||
                BridgeHealthAcknowledgesRevision(acknowledgedHealth, "stale", 42))
                throw new InvalidOperationException("Bridge revision ACK accepted stale configuration or required an awake device");
            acknowledgedHealth["config_error"] = "invalid config";
            if (BridgeHealthAcknowledgesRevision(acknowledgedHealth, bridgeRevision, 42))
                throw new InvalidOperationException("Bridge revision ACK ignored a configuration load error");

            VibeMicConfig invalid = VibeMicConfig.Default();
            invalid.schemaVersion = 24;
            invalid.theme = "neon";
            invalid.onboardingStep = 99;
            invalid.setupCompleted = false;
            invalid.resumeSetupAfterRestart = true;
            MigrateConfig(invalid);
            if (invalid.theme != "light" || invalid.onboardingStep != 0 || !invalid.resumeSetupAfterRestart)
                throw new InvalidOperationException("Onboarding or theme recovery invariant failed");

            string atomicPath = Path.Combine(Path.GetTempPath(), "vibe-flow-config-test-" + Guid.NewGuid().ToString("N") + ".json");
            string atomicBackup = atomicPath + ".bak";
            try
            {
                File.WriteAllText(atomicPath, "first", Encoding.UTF8);
                WriteTextAtomically(atomicPath, "second", atomicBackup);
                if (File.ReadAllText(atomicPath, Encoding.UTF8) != "second" ||
                    File.ReadAllText(atomicBackup, Encoding.UTF8) != "first" || File.Exists(atomicPath + ".tmp"))
                    throw new InvalidOperationException("Atomic configuration replacement failed");
            }
            finally
            {
                try { if (File.Exists(atomicPath)) File.Delete(atomicPath); } catch { }
                try { if (File.Exists(atomicBackup)) File.Delete(atomicBackup); } catch { }
                try { if (File.Exists(atomicPath + ".tmp")) File.Delete(atomicPath + ".tmp"); } catch { }
            }

            string migrationRoot = Path.Combine(Path.GetTempPath(), "vibe-flow-state-test-" + Guid.NewGuid().ToString("N"));
            string legacyRoot = Path.Combine(migrationRoot, "legacy");
            string userRoot = Path.Combine(migrationRoot, "user");
            try
            {
                Directory.CreateDirectory(legacyRoot);
                VibeMicConfig legacyConfig = VibeMicConfig.Default();
                legacyConfig.mappings["Home:long"] = "open-url:https://example.com/migrated";
                string legacyPath = Path.Combine(legacyRoot, "vibe-mic-config.json");
                File.WriteAllText(legacyPath, new JavaScriptSerializer().Serialize(legacyConfig), Encoding.UTF8);
                string migratedFrom = MigrateLegacyUserConfig(legacyRoot, userRoot);
                string migratedPath = Path.Combine(userRoot, "vibe-mic-config.json");
                VibeMicConfig migratedConfig = new JavaScriptSerializer().Deserialize<VibeMicConfig>(
                    File.ReadAllText(migratedPath, Encoding.UTF8));
                if (!migratedFrom.Equals(legacyPath, StringComparison.OrdinalIgnoreCase) ||
                    migratedConfig == null ||
                    migratedConfig.mappings["Home:long"] != "open-url:https://example.com/migrated")
                    throw new InvalidOperationException("Legacy user configuration migration failed");
                legacyConfig.mappings["Home:long"] = "none";
                File.WriteAllText(legacyPath, new JavaScriptSerializer().Serialize(legacyConfig), Encoding.UTF8);
                if (MigrateLegacyUserConfig(legacyRoot, userRoot) != "" ||
                    new JavaScriptSerializer().Deserialize<VibeMicConfig>(File.ReadAllText(migratedPath, Encoding.UTF8))
                        .mappings["Home:long"] != "open-url:https://example.com/migrated")
                    throw new InvalidOperationException("Central user configuration was overwritten by legacy state");
            }
            finally
            {
                try { if (Directory.Exists(migrationRoot)) Directory.Delete(migrationRoot, true); } catch { }
            }

            VibeMicConfig startupFixture = VibeMicConfig.Default();
            if (ShouldRegisterStartup(startupFixture))
                throw new InvalidOperationException("Disabled startup should not retain a stale registration");
            startupFixture.resumeSetupAfterRestart = true;
            if (!ShouldRegisterStartup(startupFixture))
                throw new InvalidOperationException("Incomplete setup restart registration was not preserved");
            startupFixture.setupCompleted = true;
            startupFixture.resumeSetupAfterRestart = false;
            startupFixture.launchAtStartup = true;
            if (!ShouldRegisterStartup(startupFixture))
                throw new InvalidOperationException("Configured startup registration was not preserved");

            Console.WriteLine("Vibe Flow host self-test passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Vibe Flow host self-test failed: " + ex.Message);
            return 1;
        }
    }

    private static bool WindowsUsesDarkApps()
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                "Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize", false))
            {
                object value = key == null ? null : key.GetValue("AppsUseLightTheme");
                return value != null && Convert.ToInt32(value) == 0;
            }
        }
        catch { return false; }
    }

    private Color StatusSurface(string state)
    {
        if (!darkTheme)
        {
            if (state == "recording") return Color.FromArgb(244, 242, 255);
            if (state == "recovering" || state == "processing") return Color.FromArgb(239, 249, 252);
            if (state == "completed" || state == "ready") return Color.FromArgb(238, 250, 244);
            if (state == "error") return Color.FromArgb(255, 242, 242);
            if (state == "connecting") return Color.FromArgb(255, 248, 234);
            return Color.FromArgb(248, 249, 252);
        }
        if (state == "recording") return Color.FromArgb(42, 37, 61);
        if (state == "recovering" || state == "processing") return Color.FromArgb(30, 48, 54);
        if (state == "completed" || state == "ready") return Color.FromArgb(29, 50, 40);
        if (state == "error") return Color.FromArgb(57, 34, 39);
        if (state == "connecting") return Color.FromArgb(57, 46, 29);
        return surfaceBackground;
    }

    private void BuildShell()
    {
        var sidebar = new Panel();
        sidebar.Dock = DockStyle.Left;
        sidebar.Width = 232;
        sidebar.BackColor = sidebarBackground;
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
        var sub = NewLabel("VIBE FLOW · V" + ProductRelease, 7.4f, FontStyle.Bold, violet);
        sub.Location = new Point(84, 58);
        sub.AutoSize = true;

        string[] navText = { "首页", "快捷键", "语音", "自检", "设置" };
        string[] navIcon = { "overview", "shortcuts", "voice", "diagnostics", "settings" };
        for (int i = 0; i < navText.Length; i++)
        {
            int page = i;
            var button = new Button();
            button.Text = navText[i];
            button.Font = navigationFont;
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.Image = CreateNavigationIcon(navIcon[i], muted, false);
            button.ImageAlign = ContentAlignment.MiddleLeft;
            button.TextImageRelation = TextImageRelation.ImageBeforeText;
            button.Padding = new Padding(18, 0, 10, 0);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = darkTheme ? Color.FromArgb(43, 45, 56) : Color.FromArgb(239, 242, 250);
            button.FlatAppearance.MouseDownBackColor = darkTheme ? Color.FromArgb(50, 52, 65) : Color.FromArgb(229, 234, 247);
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
        connectionBadge.BackColor = cardBackground;
        connectionBadge.TextAlign = ContentAlignment.MiddleCenter;
        connectionBadge.Location = new Point(22, 12);
        connectionBadge.Size = new Size(176, 46);
        connectionBadge.Font = connectionBadgeFont;
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
        content.BackColor = pageBackground;
        content.AutoScroll = true;
        content.AutoScrollMinSize = new Size(1000, 744);
        content.Paint -= PaintWorkspaceTexture;
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
        toastPanel.BackColor = cardBackground;
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
            navButtons[i].BackColor = i == currentPageIndex ?
                (darkTheme ? Color.FromArgb(43, 45, 53) : Color.FromArgb(233, 237, 255)) : Color.Transparent;
            navButtons[i].ForeColor = i == currentPageIndex ? violet : ink;
            navButtons[i].Font = i == currentPageIndex ? navigationActiveFont : navigationFont;
            Image previousIcon = navButtons[i].Image;
            navButtons[i].Image = CreateNavigationIcon(navButtons[i].Tag as string, i == currentPageIndex ? violet : muted, i == currentPageIndex);
            if (previousIcon != null) previousIcon.Dispose();
            ApplyRoundedRegion(navButtons[i], 7);
            navButtons[i].Invalidate();
        }
        content.SuspendLayout();
        content.AutoScrollPosition = Point.Empty;
        content.AutoScrollMinSize = currentPageIndex == PageShortcuts ? new Size(1000, 790) : new Size(1000, 744);
        DisposePageControls();
        if (currentPageIndex == PageHome) BuildOverview();
        else if (currentPageIndex == PageShortcuts) BuildMappingsPage();
        else if (currentPageIndex == PageVoice) BuildVoicePage();
        else if (currentPageIndex == PageSelfCheck) BuildDevicePage();
        else BuildSettingsPage();
        content.ResumeLayout();
        ActiveControl = null;
    }

    private void BuildOverview()
    {
        content.AutoScrollMinSize = new Size(1000, 830);
        AddPageTitle("首页", "按住听写、连接状态与遥控器快捷操作");

        var hero = NewCard(new Point(34, 92), new Size(960, 322));
        heroPanel = hero;
        hero.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        hero.Paint += PaintHeroSurface;

        heroStateLabel = NewLabel(IsCapturing ? "PUSH TO TALK" : "VOICE LINK OFF", 8.5f, FontStyle.Bold, violet);
        heroStateLabel.Location = new Point(52, 34);
        heroStateLabel.AutoSize = true;
        heroTitle = NewLabel(IsCapturing ? "正在连接" : "语音桥接已暂停", 27f, FontStyle.Bold, ink);
        heroTitle.Location = new Point(50, 62);
        heroTitle.AutoSize = true;
        heroSubtitle = NewLabel(IsCapturing ? "正在建立遥控器语音通道，请稍候" : "启动后，" + VoiceStartInstruction(config.voiceMode), 10.5f, FontStyle.Regular, muted);
        heroSubtitle.Location = new Point(52, 111);
        heroSubtitle.Size = new Size(560, 30);

        string[,] linkFacts = {
            { "●", "RC003 遥控器" },
            { "●", ProviderDisplayName(config.inputMethod) },
            { "●", "按住说话 · 稳定模式" }
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
            if (IsCapturing) ShowPage(PageVoice);
            else ToggleCapture();
        };
        var scan = SecondaryButton("检查连接", new Point(216, 217), new Size(124, 44));
        scan.Click += delegate { ScanDevice(); };

        var gestureHint = NewLabel("按住说话  ·  松开后交给转写工具整理  ·  确认键发送", 8.7f, FontStyle.Regular, muted);
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
        string[] steps = new string[] { "按住录音键", "持续说出内容", "松开完成转译" };
        string[] icons = { "\uE720", "\uE9D2", "\uE724" };
        for (int i = 0; i < 3; i++)
        {
            int x = 40 + i * 140;
            var circle = new RoundPanel();
            circle.Location = new Point(x, 52);
            circle.Size = new Size(48, 48);
            circle.Radius = 24;
            circle.BackColor = i == 0 ? (darkTheme ? Color.FromArgb(45, 47, 55) : Color.FromArgb(237, 235, 255)) : surfaceBackground;
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
        activityLabel = NewLabel("已就绪，等待按住录音键", 9.5f, FontStyle.Bold, muted);
        activityLabel.Location = new Point(24, 142);
        activityLabel.Size = new Size(420, 22);
        activityLabel.TextAlign = ContentAlignment.MiddleCenter;
        flow.Controls.Add(activityLabel);

        var shortcuts = NewCard(new Point(520, 430), new Size(474, 178));
        shortcuts.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        shortcuts.Controls.Add(SectionTitle("常用按键", "\uE765", new Point(24, 18)));
        string[,] quick = {
            { "录音", "按住听写 / 松开结束" },
            { "确认", MappingCardActionText(GetMapping("确认键", "enter")) },
            { "Home", GestureMappingSummary("Home:short", "Home:long") },
            { "TV", MappingCardActionText(GetMapping("TV", "task-switcher")) },
            { "功能键", GestureMappingSummary("功能键:short", "功能键:long") },
            { "方向键", DirectionMappingSummary() }
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

        var receipt = NewCard(new Point(34, 726), new Size(960, 78));
        receipt.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        receipt.Controls.Add(SectionTitle("最近一次快捷操作", "\uE945", new Point(24, 20)));
        actionReceiptGlyph = NewLabel("\uE946", 13f, FontStyle.Regular, muted);
        actionReceiptGlyph.Font = new Font("Segoe MDL2 Assets", 13f, FontStyle.Regular);
        actionReceiptGlyph.Location = new Point(238, 20);
        actionReceiptGlyph.Size = new Size(34, 34);
        actionReceiptGlyph.TextAlign = ContentAlignment.MiddleCenter;
        actionReceiptTitle = NewLabel("等待一次真实按键操作", 9.6f, FontStyle.Bold, ink);
        actionReceiptTitle.Location = new Point(278, 12);
        actionReceiptTitle.Size = new Size(410, 28);
        actionReceiptDetail = NewLabel("执行结果会在这里显示", 8.3f, FontStyle.Regular, muted);
        actionReceiptDetail.Location = new Point(278, 39);
        actionReceiptDetail.Size = new Size(640, 24);
        receipt.Controls.Add(actionReceiptGlyph);
        receipt.Controls.Add(actionReceiptTitle);
        receipt.Controls.Add(actionReceiptDetail);

        content.Controls.Add(hero);
        content.Controls.Add(flow);
        content.Controls.Add(shortcuts);
        content.Controls.Add(status);
        content.Controls.Add(receipt);
        UpdateActionReceipt(ReadKeyboardBridgeHealth());
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
        UpdateActionReceipt(ReadKeyboardBridgeHealth());
    }

    private string GestureMappingSummary(string shortKey, string longKey)
    {
        string shortText = MappingCardActionText(GetMapping(shortKey, DefaultConfigurableAction(shortKey)));
        string longText = MappingCardActionText(GetMapping(longKey, DefaultConfigurableAction(longKey)));
        return "短 " + shortText + " / 长 " + longText;
    }

    private string DirectionMappingSummary()
    {
        string up = MappingCardActionText(GetMapping("上键", "up"));
        string down = MappingCardActionText(GetMapping("下键", "down"));
        return "↑ " + up + "  ↓ " + down;
    }

    private void UpdateActionReceipt(BridgeHealthSnapshot receipt)
    {
        if (actionReceiptTitle == null || actionReceiptTitle.IsDisposed ||
            actionReceiptDetail == null || actionReceiptDetail.IsDisposed) return;
        ShortcutProfileConfig active = ActiveShortcutProfile(config);
        string activeName = active == null ? "当前 Profile" : active.name;
        if (receipt == null || receipt.LastExecutionSequence <= 0)
        {
            actionReceiptTitle.Text = "等待一次真实按键操作";
            actionReceiptDetail.Text = activeName + " · 按下已配置按键后显示真实执行结果";
            if (actionReceiptGlyph != null && !actionReceiptGlyph.IsDisposed)
            {
                actionReceiptGlyph.Text = "\uE946";
                actionReceiptGlyph.ForeColor = muted;
            }
            return;
        }
        string label = string.IsNullOrWhiteSpace(receipt.LastExecutionLabel)
            ? receipt.LastExecutionButton : receipt.LastExecutionLabel;
        string trigger = string.IsNullOrWhiteSpace(receipt.LastExecutionTrigger)
            ? "单击" : receipt.LastExecutionTrigger;
        string action = CustomActionText(receipt.LastExecutionAction);
        string normalizedAction = (receipt.LastExecutionAction ?? "").Trim().ToLowerInvariant();
        bool disabledAction = normalizedAction.Length == 0 || normalizedAction == "none" || normalizedAction == "passthrough";
        actionReceiptTitle.Text = label + " · " + trigger + " · " + action;
        string profileName = string.IsNullOrWhiteSpace(receipt.LastExecutionProfileName)
            ? activeName : receipt.LastExecutionProfileName;
        string age = receipt.LastExecutionAgeSeconds < 5 ? "刚刚" :
            receipt.LastExecutionAgeSeconds < 60 ? ((int)receipt.LastExecutionAgeSeconds) + " 秒前" :
            receipt.LastExecutionAtUtc.ToLocalTime().ToString("HH:mm:ss");
        actionReceiptDetail.Text = profileName + " · " + (disabledAction ? "未配置动作，不是连接故障" :
            receipt.LastExecutionSuccess ? "执行成功" : "执行失败，请打开自检查看") + " · " + age;
        if (actionReceiptGlyph != null && !actionReceiptGlyph.IsDisposed)
        {
            actionReceiptGlyph.Text = disabledAction ? "\uE946" : receipt.LastExecutionSuccess ? "\uE73E" : "\uEA39";
            actionReceiptGlyph.ForeColor = disabledAction ? amber : receipt.LastExecutionSuccess ? green : coral;
        }
    }

    private void BuildVoicePage()
    {
        AddPageTitle("语音听写", "遥控器负责收音；转写与整理能力由所选工具设置");
        var card = NewCard(new Point(34, 100), new Size(960, 650));
        card.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        card.Controls.Add(SectionTitle("听写通道", "\uE720", new Point(30, 24)));

        var stateBand = new Panel();
        stateBand.Location = new Point(30, 64);
        stateBand.Size = new Size(900, 62);
        stateBand.BackColor = IsCapturing ? StatusSurface("ready") : surfaceBackground;
        voiceBridgeStateLabel = NewLabel(IsCapturing ? "●  已就绪 · " + VoiceReadyInstruction(config.voiceMode) : "●  语音桥接已暂停", 10.5f, FontStyle.Bold,
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
        provider.Items.AddRange(new object[] { "微信输入法", "Typeless", "豆包输入法", "Windows 语音输入", "其他语音工具" });
        provider.SelectedIndex = ProviderIndex(config.inputMethod);
        var providerStatus = NewLabel(ProviderStatusText(config.inputMethod), 9.2f, FontStyle.Bold,
            IsProviderRunning(config.inputMethod) ? green : amber);
        providerStatus.Location = new Point(505, 154);
        providerStatus.Size = new Size(400, 28);
        card.Controls.Add(provider);
        card.Controls.Add(providerStatus);

        AddFieldLabel(card, "启动快捷键", 206);
        var hotkey = StyledTextBox(config.inputMethodHotkey, new Point(220, 202), new Size(220, 34));
        var triggerMode = StyledCombo(new Point(458, 200), new Size(184, 38));
        PopulateTriggerModeOptions(triggerMode, config.inputMethod);
        triggerMode.SelectedIndex = NormalizeProviderKey(config.inputMethod) == "wechat" ? 0 :
            config.inputMethodTrigger == "hold" ? 1 : 0;
        var hotkeyHelp = NewLabel(ProviderHotkeyHelp(config.inputMethod, config.inputMethodTrigger), 9f, FontStyle.Regular, muted);
        hotkeyHelp.Location = new Point(658, 207);
        hotkeyHelp.Size = new Size(242, 25);
        card.Controls.Add(hotkey);
        card.Controls.Add(triggerMode);
        card.Controls.Add(hotkeyHelp);

        AddFieldLabel(card, "录音方式", 260);
        var voiceMode = NewLabel("按住说话 · 松开结束", 10f, FontStyle.Bold, violet);
        voiceMode.Location = new Point(220, 256);
        voiceMode.Size = new Size(270, 38);
        voiceMode.TextAlign = ContentAlignment.MiddleLeft;
        var voiceModeHelp = NewLabel(VoiceModeHelp(config.voiceMode), 9f, FontStyle.Regular, muted);
        voiceModeHelp.Location = new Point(548, 263);
        voiceModeHelp.Size = new Size(370, 25);
        card.Controls.Add(voiceMode);
        card.Controls.Add(voiceModeHelp);

        AddFieldLabel(card, "声音处理", 314);
        var processing = StyledCombo(new Point(220, 310), new Size(260, 38));
        processing.Items.AddRange(new object[] { "清晰增强（推荐）", "原始直通" });
        processing.SelectedIndex = config.audioProcessingMode == "transparent" ? 1 : 0;
        processing.Enabled = advancedAudioUnlocked;
        var processingHelp = NewLabel(stableVoiceProfile
            ? "已锁定为真机验证的清晰增强模式。"
            : config.audioProcessingMode == "transparent"
            ? "仅做格式转换，适合排查原始音频。"
            : "稳定补偿轻声，孤立尖峰不会压低整段语音。", 9f, FontStyle.Regular, muted);
        processingHelp.Location = new Point(505, 317);
        processingHelp.Size = new Size(390, 25);
        card.Controls.Add(processing);
        card.Controls.Add(processingHelp);

        AddFieldLabel(card, "收音灵敏度", 368);
        var gainHelp = NewLabel(stableVoiceProfile
            ? "已锁定为真机验证值 1.0×；普通使用无需调整。"
            : "建议保持 1.0×；仅在排障时小幅调整。", 9.2f, FontStyle.Regular, muted);
        gainHelp.Location = new Point(220, 370);
        gainHelp.Size = new Size(520, 24);
        card.Controls.Add(gainHelp);
        var gain = new TrackBar();
        gain.Location = new Point(212, 396);
        gain.Size = new Size(390, 44);
        gain.Minimum = 5;
        gain.Maximum = 40;
        gain.Value = Math.Max(5, Math.Min(40, (int)(config.gain * 10)));
        gain.Enabled = advancedAudioUnlocked;
        var gainValue = NewLabel((gain.Value / 10.0).ToString("0.0") + "×", 10f, FontStyle.Bold, violet);
        gainValue.Location = new Point(620, 404);
        gainValue.Size = new Size(70, 28);
        gain.Scroll += delegate { gainValue.Text = (gain.Value / 10.0).ToString("0.0") + "×"; };
        card.Controls.Add(gain);
        card.Controls.Add(gainValue);

        var autoRoute = StyledCheck("听写时自动使用遥控器麦克风（推荐）", config.autoRouteVirtualMicrophone, new Point(212, 444));
        autoRoute.Size = new Size(330, 34);
        autoRoute.AutoCheck = advancedAudioUnlocked;
        autoRoute.TabStop = advancedAudioUnlocked;
        if (!advancedAudioUnlocked) autoRoute.ForeColor = muted;
        var routeHelp = NewLabel("结束听写后自动恢复原来的 Windows 麦克风", 8.9f, FontStyle.Regular, muted);
        routeHelp.Location = new Point(548, 451);
        routeHelp.Size = new Size(350, 24);
        card.Controls.Add(autoRoute);
        card.Controls.Add(routeHelp);

        bool cableReady = HasCableInput() && HasCableOutput();
        var cableState = NewLabel(cableReady ? "●  CABLE 音频通道已就绪" : "●  需要安装或检查 VB-CABLE", 10f, FontStyle.Bold,
            cableReady ? green : Color.FromArgb(202, 76, 76));
        cableState.Location = new Point(220, 486);
        cableState.AutoSize = true;
        card.Controls.Add(cableState);

        var start = PrimaryButton(IsCapturing ? "暂停语音桥接" : "启动语音桥接", new Point(220, 524), new Size(152, 44));
        start.Click += delegate { ToggleCapture(); start.Text = IsCapturing ? "暂停语音桥接" : "启动语音桥接"; };
        var test = SecondaryButton("测试所选工具", new Point(386, 524), new Size(148, 44));
        test.Click += delegate { TestVoiceHotkey(); };
        var sound = SecondaryButton(config.inputMethod == "typeless" || config.inputMethod == "doubao" ? "获取所选工具" : "检查麦克风设置",
            new Point(548, 524), new Size(158, 44));
        sound.Click += delegate { OpenProviderHelp(config.inputMethod); };
        var profileAction = SecondaryButton(stableVoiceProfile ? "调整高级参数" : "恢复稳定参数", new Point(720, 524), new Size(170, 44));
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
            ShowPage(PageVoice);
            ShowToast("已恢复真机验证的稳定语音参数", "success");
        };
        card.Controls.Add(start);
        card.Controls.Add(test);
        card.Controls.Add(sound);
        card.Controls.Add(profileAction);

        var note = NewLabel(ProviderRouteInstruction(config.inputMethod, config.autoRouteVirtualMicrophone) + "。言灵只转发遥控器音频，不保存录音、不读取听写文字，也不会自行上传音频。", 9.3f, FontStyle.Regular, muted);
        note.Location = new Point(30, 580);
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
            updating = true;
            ApplyProviderProfile(config, ProviderKeyFromIndex(provider.SelectedIndex));
            hotkey.Text = config.inputMethodHotkey;
            PopulateTriggerModeOptions(triggerMode, config.inputMethod);
            triggerMode.SelectedIndex = NormalizeProviderKey(config.inputMethod) == "wechat" ? 0 :
                config.inputMethodTrigger == "hold" ? 1 : 0;
            hotkeyHelp.Text = ProviderHotkeyHelp(config.inputMethod, config.inputMethodTrigger);
            providerStatus.Text = ProviderStatusText(config.inputMethod);
            providerStatus.ForeColor = IsProviderRunning(config.inputMethod) ? green : amber;
            updating = false;
            SaveConfig();
            RestartCaptureForAudioSettings();
            BeginInvoke(new Action(delegate { ShowPage(PageVoice); }));
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
            if (NormalizeProviderKey(config.inputMethod) == "wechat")
            {
                config.inputMethodTrigger = "toggle";
                updating = true;
                triggerMode.SelectedIndex = 0;
                updating = false;
                hotkeyHelp.Text = ProviderHotkeyHelp(config.inputMethod, config.inputMethodTrigger);
            }
            SaveConfig();
            RestartCaptureForAudioSettings();
        };
        triggerMode.SelectedIndexChanged += delegate
        {
            if (updating) return;
            string value = triggerMode.SelectedIndex == 1 ? "hold" : "toggle";
            if (NormalizeProviderKey(config.inputMethod) == "wechat")
            {
                value = "toggle";
                if (value == config.inputMethodTrigger) return;
            }
            else if (value == config.inputMethodTrigger) return;
            config.inputMethodTrigger = value;
            hotkeyHelp.Text = ProviderHotkeyHelp(config.inputMethod, config.inputMethodTrigger);
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
        content.AutoScrollMinSize = new Size(1000, 900);
        BridgeHealthSnapshot mappingHealth = ReadKeyboardBridgeHealth();
        bool exactDeviceIsolation = mappingHealth.FilterHealthy;
        AddPageTitle("快捷键", "手动切换 Profile；动作只响应已确认的 RC003 设备事件");

        var header = NewCard(new Point(34, 100), new Size(960, 136));
        var headerTitle = NewLabel("快捷键 Profile", 14f, FontStyle.Bold, ink);
        headerTitle.Location = new Point(24, 12);
        headerTitle.Size = new Size(220, 28);
        var headerDetail = NewLabel(exactDeviceIsolation ?
            "当前设备级隔离已启用；Profile 仅保存快捷键，不包含任何语音参数" :
            "安全直通下遥控器原按键效果可能同时发生；Profile 不会修改语音链路", 8.6f, FontStyle.Regular, muted);
        headerDetail.Location = new Point(24, 40);
        headerDetail.Size = new Size(590, 22);
        var sourceBadge = NewLabel(exactDeviceIsolation ? "●  精确隔离" : "●  安全直通",
            8.8f, FontStyle.Bold, exactDeviceIsolation ? green : cyan);
        sourceBadge.Location = new Point(804, 18);
        sourceBadge.Size = new Size(126, 34);
        sourceBadge.TextAlign = ContentAlignment.MiddleCenter;
        sourceBadge.BackColor = StatusSurface("connected");
        ApplyRoundedRegion(sourceBadge, 6);

        var profileLabel = NewLabel("当前 Profile", 8.6f, FontStyle.Bold, muted);
        profileLabel.Location = new Point(24, 79);
        profileLabel.Size = new Size(82, 36);
        profileLabel.TextAlign = ContentAlignment.MiddleLeft;
        var profilePicker = StyledCombo(new Point(108, 78), new Size(208, 38));
        int activeProfileIndex = 0;
        if (config.shortcutProfiles != null)
        {
            for (int i = 0; i < config.shortcutProfiles.Length; i++)
            {
                ShortcutProfileConfig item = config.shortcutProfiles[i];
                profilePicker.Items.Add(new ShortcutProfileChoice(item));
                if (item != null && string.Equals(item.id, config.activeShortcutProfileId,
                    StringComparison.OrdinalIgnoreCase)) activeProfileIndex = i;
            }
        }
        if (profilePicker.Items.Count > 0) profilePicker.SelectedIndex = Math.Min(activeProfileIndex, profilePicker.Items.Count - 1);
        var switchProfile = PrimaryButton("切换", new Point(326, 77), new Size(72, 40));
        switchProfile.Click += delegate
        {
            ShortcutProfileChoice choice = profilePicker.SelectedItem as ShortcutProfileChoice;
            if (choice != null) SwitchShortcutProfile(choice.Profile.id);
        };
        var createProfile = SecondaryButton("新建", new Point(408, 77), new Size(72, 40));
        createProfile.Click += delegate { CreateShortcutProfile(); };
        var renameProfile = SecondaryButton("重命名", new Point(490, 77), new Size(82, 40));
        renameProfile.Click += delegate { RenameActiveShortcutProfile(); };
        var deleteProfile = SecondaryButton("删除", new Point(582, 77), new Size(72, 40));
        deleteProfile.Click += delegate { DeleteActiveShortcutProfile(); };
        var importProfile = SecondaryButton("导入", new Point(664, 77), new Size(72, 40));
        importProfile.Click += delegate { ImportShortcutProfile(); };
        var exportProfile = SecondaryButton("导出", new Point(746, 77), new Size(72, 40));
        exportProfile.Click += delegate { ExportActiveShortcutProfile(); };
        var manualBadge = NewLabel("手动切换", 8.2f, FontStyle.Bold, violet);
        manualBadge.Location = new Point(830, 80);
        manualBadge.Size = new Size(100, 34);
        manualBadge.TextAlign = ContentAlignment.MiddleCenter;
        manualBadge.BackColor = StatusSurface("recording");
        ApplyRoundedRegion(manualBadge, 6);
        header.Controls.Add(headerTitle);
        header.Controls.Add(headerDetail);
        header.Controls.Add(sourceBadge);
        header.Controls.Add(profileLabel);
        header.Controls.Add(profilePicker);
        header.Controls.Add(switchProfile);
        header.Controls.Add(createProfile);
        header.Controls.Add(renameProfile);
        header.Controls.Add(deleteProfile);
        header.Controls.Add(importProfile);
        header.Controls.Add(exportProfile);
        header.Controls.Add(manualBadge);

        var canvas = NewCard(new Point(34, 252), new Size(960, 610));
        var canvasTitle = NewLabel("小米蓝牙遥控器 2 Pro", 10.2f, FontStyle.Bold, ink);
        canvasTitle.Location = new Point(342, 16);
        canvasTitle.Size = new Size(276, 28);
        canvasTitle.TextAlign = ContentAlignment.MiddleCenter;
        var canvasState = NewLabel("按下实体键时，对应位置会被识别并高亮", 8.3f, FontStyle.Regular, muted);
        canvasState.Location = new Point(320, 43);
        canvasState.Size = new Size(320, 22);
        canvasState.TextAlign = ContentAlignment.MiddleCenter;
        var capabilityNote = NewLabel("开机、返回和独立音量键在 Windows 下无稳定事件，不提供映射；APP、网页与截图请绑定到可配置按键。",
            8.0f, FontStyle.Regular, muted);
        capabilityNote.Location = new Point(326, 552);
        capabilityNote.Size = new Size(308, 42);
        capabilityNote.TextAlign = ContentAlignment.MiddleCenter;

        var previewRemote = new RemoteVisual();
        previewRemote.Location = new Point(330, 70);
        previewRemote.Size = new Size(300, 474);
        previewRemote.IsActive = true;
        previewRemote.ShowCallouts = false;
        previewRemote.AccentColor = violet;
        previewRemote.HighlightedControl = "";
        remoteVisual = previewRemote;

        AddMappingOverviewCard(canvas, previewRemote, new Point(18, 70), "上键", "上键", "up",
            "上键", "", false, false);
        AddMappingOverviewCard(canvas, previewRemote, new Point(18, 176), "左键", "左键", "left",
            "左键", "", false, false);
        AddMappingOverviewCard(canvas, previewRemote, new Point(18, 282), "Home", "Home 键", "home",
            "Home:short", "Home:long", true, false);
        AddMappingOverviewCard(canvas, previewRemote, new Point(18, 388), "功能键", "功能键", "menu",
            "功能键:short", "功能键:long", true, false);

        AddFixedVoiceOverviewCard(canvas, previewRemote, new Point(656, 70));
        AddMappingOverviewCard(canvas, previewRemote, new Point(656, 176), "右键", "右键", "right",
            "右键", "", false, false);
        AddMappingOverviewCard(canvas, previewRemote, new Point(656, 282), "确认键", "确认键", "ok",
            "确认键", "", false, false);
        AddMappingOverviewCard(canvas, previewRemote, new Point(656, 388), "下键", "下键", "down",
            "下键", "", false, false);
        AddMappingOverviewCard(canvas, previewRemote, new Point(656, 494), "TV", "TV 键", "tv",
            "TV", "", false, false);

        canvas.Controls.Add(canvasTitle);
        canvas.Controls.Add(canvasState);
        canvas.Controls.Add(capabilityNote);
        canvas.Controls.Add(previewRemote);
        content.Controls.Add(header);
        content.Controls.Add(canvas);
    }

    private void SwitchShortcutProfile(string profileId)
    {
        ShortcutProfileConfig target = FindShortcutProfile(config, profileId);
        if (target == null)
        {
            ShowToast("找不到这个 Profile，请重新选择", "error");
            return;
        }
        if (string.Equals(config.activeShortcutProfileId, target.id, StringComparison.OrdinalIgnoreCase))
        {
            ShowToast("“" + target.name + "”已经在使用", "info");
            return;
        }

        CaptureActiveShortcutProfileMappings(config);
        string previousId = config.activeShortcutProfileId;
        config.activeShortcutProfileId = target.id;
        ProjectActiveShortcutProfile(config);
        HostLog("SHORTCUT PROFILE switch_requested=true from=" + SafeLogValue(previousId) +
            " to=" + SafeLogValue(target.id));
        if (!SaveConfig())
        {
            config.activeShortcutProfileId = previousId;
            ProjectActiveShortcutProfile(config);
            ShowToast("Profile 切换保存失败，仍使用原方案", "error");
            return;
        }
        bool acknowledged = StartKeyboardBridge();
        HostLog("SHORTCUT PROFILE switched=true id=" + SafeLogValue(target.id) +
            " bridge_ack=" + acknowledged);
        ShowPage(PageShortcuts);
        ShowToast(acknowledged ? "已切换到“" + target.name + "”" :
            "已切换到“" + target.name + "”，桥接仍在确认",
            acknowledged ? "success" : "warning");
    }

    private bool PromptNewShortcutProfile(out string template, out string profileName)
    {
        template = "";
        profileName = "";
        using (var dialog = new Form())
        using (var templatePicker = new ComboBox())
        using (var nameInput = new TextBox())
        using (var create = new Button())
        using (var cancel = new Button())
        {
            dialog.Text = "新建快捷键 Profile";
            dialog.StartPosition = FormStartPosition.CenterParent;
            dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
            dialog.MinimizeBox = false;
            dialog.MaximizeBox = false;
            dialog.ShowInTaskbar = false;
            dialog.ClientSize = new Size(520, 246);
            dialog.BackColor = cardBackground;
            dialog.Font = Font;

            var title = NewLabel("从一个可靠起点开始", 14f, FontStyle.Bold, ink);
            title.Location = new Point(24, 18);
            title.Size = new Size(460, 32);
            var help = NewLabel("官方模板只包含快捷键；当前语音工具和音频参数不会改变。", 8.7f, FontStyle.Regular, muted);
            help.Location = new Point(24, 50);
            help.Size = new Size(470, 24);
            var templateLabel = NewLabel("起始模板", 8.8f, FontStyle.Bold, muted);
            templateLabel.Location = new Point(24, 82);
            templateLabel.Size = new Size(90, 24);
            templatePicker.Location = new Point(124, 80);
            templatePicker.Size = new Size(370, 34);
            templatePicker.DropDownStyle = ComboBoxStyle.DropDownList;
            templatePicker.Font = new Font("Microsoft YaHei UI", 9.5f);
            templatePicker.Items.AddRange(new object[] {
                "复制当前 Profile（推荐）", "官方 · 通用导航", "官方 · Vibe Coding",
                "官方 · 浏览器 AI", "官方 · Terminal Agent"
            });
            templatePicker.SelectedIndex = 0;
            var nameLabel = NewLabel("Profile 名称", 8.8f, FontStyle.Bold, muted);
            nameLabel.Location = new Point(24, 130);
            nameLabel.Size = new Size(90, 24);
            nameInput.Location = new Point(124, 128);
            nameInput.Size = new Size(370, 32);
            nameInput.Font = new Font("Microsoft YaHei UI", 9.5f);
            ShortcutProfileConfig active = ActiveShortcutProfile(config);
            nameInput.Text = (active == null ? "我的快捷键" : active.name) + " 副本";
            templatePicker.SelectedIndexChanged += delegate
            {
                string[] defaults = {
                    (active == null ? "我的快捷键" : active.name) + " 副本",
                    "通用导航 副本", "Vibe Coding 副本", "浏览器 AI 副本", "Terminal Agent 副本"
                };
                nameInput.Text = defaults[Math.Max(0, templatePicker.SelectedIndex)];
            };

            create.Text = "创建并切换";
            create.DialogResult = DialogResult.OK;
            create.Location = new Point(286, 190);
            create.Size = new Size(112, 36);
            create.BackColor = violet;
            create.ForeColor = Color.White;
            create.FlatStyle = FlatStyle.Flat;
            create.FlatAppearance.BorderSize = 0;
            cancel.Text = "取消";
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Location = new Point(410, 190);
            cancel.Size = new Size(84, 36);
            cancel.FlatStyle = FlatStyle.Flat;
            cancel.FlatAppearance.BorderColor = line;
            dialog.AcceptButton = create;
            dialog.CancelButton = cancel;
            dialog.Controls.Add(title);
            dialog.Controls.Add(help);
            dialog.Controls.Add(templateLabel);
            dialog.Controls.Add(templatePicker);
            dialog.Controls.Add(nameLabel);
            dialog.Controls.Add(nameInput);
            dialog.Controls.Add(create);
            dialog.Controls.Add(cancel);
            if (dialog.ShowDialog(this) != DialogResult.OK) return false;
            string[] templates = { "duplicate", "general", "vibe-coding", "browser-ai", "terminal-agent" };
            template = templates[Math.Max(0, templatePicker.SelectedIndex)];
            profileName = NormalizeShortcutProfileName(nameInput.Text, "我的快捷键");
            return true;
        }
    }

    private bool ShortcutProfileNameExists(string name, string exceptId)
    {
        if (config.shortcutProfiles == null) return false;
        foreach (ShortcutProfileConfig profile in config.shortcutProfiles)
            if (profile != null && !string.Equals(profile.id, exceptId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(profile.name, name, StringComparison.CurrentCultureIgnoreCase)) return true;
        return false;
    }

    private string MakeUniqueShortcutProfileName(string requested)
    {
        string baseName = NormalizeShortcutProfileName(requested, "导入的快捷键");
        string candidate = baseName;
        int suffix = 2;
        while (ShortcutProfileNameExists(candidate, ""))
        {
            string tail = " (" + suffix++ + ")";
            int keep = Math.Max(1, 32 - tail.Length);
            candidate = (baseName.Length > keep ? baseName.Substring(0, keep) : baseName) + tail;
        }
        return candidate;
    }

    private void CreateShortcutProfile()
    {
        string template;
        string profileName;
        if (!PromptNewShortcutProfile(out template, out profileName)) return;
        if (ShortcutProfileNameExists(profileName, ""))
        {
            ShowToast("已有同名 Profile，请换一个名称", "warning");
            return;
        }
        CaptureActiveShortcutProfileMappings(config);
        ShortcutProfileConfig source = template == "duplicate"
            ? ActiveShortcutProfile(config) : CreateStarterShortcutProfile(template);
        ShortcutProfileConfig created = CloneShortcutProfile(source,
            "profile-" + Guid.NewGuid().ToString("N"), profileName);
        if (template != "duplicate") created.preset = template;
        else created.preset = "custom";
        var profiles = new List<ShortcutProfileConfig>(config.shortcutProfiles ?? new ShortcutProfileConfig[0]);
        profiles.Add(created);
        config.shortcutProfiles = profiles.ToArray();
        config.activeShortcutProfileId = created.id;
        ProjectActiveShortcutProfile(config);
        if (!SaveConfig())
        {
            ShowToast("Profile 创建失败，请重试", "error");
            return;
        }
        bool acknowledged = StartKeyboardBridge();
        ShowPage(PageShortcuts);
        ShowToast(acknowledged ? "已创建并切换到“" + created.name + "”" :
            "Profile 已创建，桥接仍在确认", acknowledged ? "success" : "warning");
    }

    private void RenameActiveShortcutProfile()
    {
        ShortcutProfileConfig active = ActiveShortcutProfile(config);
        if (active == null) return;
        string requested = PromptForText("重命名快捷键 Profile", "Profile 名称", active.name, this);
        if (string.IsNullOrWhiteSpace(requested)) return;
        string name = NormalizeShortcutProfileName(requested, active.name);
        if (ShortcutProfileNameExists(name, active.id))
        {
            ShowToast("已有同名 Profile，请换一个名称", "warning");
            return;
        }
        if (string.Equals(active.name, name, StringComparison.Ordinal)) return;
        active.name = name;
        active.preset = "custom";
        if (!SaveConfig())
        {
            ShowToast("Profile 重命名失败", "error");
            return;
        }
        bool acknowledged = StartKeyboardBridge();
        ShowPage(PageShortcuts);
        ShowToast(acknowledged ? "Profile 已重命名" : "名称已保存，桥接仍在确认",
            acknowledged ? "success" : "warning");
    }

    private void DeleteActiveShortcutProfile()
    {
        ShortcutProfileConfig active = ActiveShortcutProfile(config);
        if (active == null) return;
        if (config.shortcutProfiles == null || config.shortcutProfiles.Length <= 1)
        {
            ShowToast("至少需要保留一个 Profile", "warning");
            return;
        }
        if (MessageBox.Show(this, "删除“" + active.name + "”？此操作不会修改语音设置。",
            "删除快捷键 Profile", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        var profiles = new List<ShortcutProfileConfig>();
        foreach (ShortcutProfileConfig profile in config.shortcutProfiles)
            if (profile != null && !string.Equals(profile.id, active.id, StringComparison.OrdinalIgnoreCase)) profiles.Add(profile);
        config.shortcutProfiles = profiles.ToArray();
        config.activeShortcutProfileId = profiles[0].id;
        ProjectActiveShortcutProfile(config);
        if (!SaveConfig())
        {
            ShowToast("Profile 删除失败，请重试", "error");
            return;
        }
        bool acknowledged = StartKeyboardBridge();
        ShowPage(PageShortcuts);
        ShowToast(acknowledged ? "已删除并切换到“" + profiles[0].name + "”" :
            "Profile 已删除，桥接仍在确认", acknowledged ? "success" : "warning");
    }

    private void ExportActiveShortcutProfile()
    {
        ShortcutProfileConfig active = ActiveShortcutProfile(config);
        if (active == null) return;
        try
        {
            CaptureActiveShortcutProfileMappings(config);
            var dialog = new SaveFileDialog();
            dialog.Filter = "Vibe Flow 快捷键 Profile|*.json";
            string safeName = Regex.Replace(active.name, "[\\\\/:*?\"<>|]", "-");
            dialog.FileName = "vibe-flow-profile-" + safeName + ".json";
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            var export = new ShortcutProfileExport
            {
                format = "vibe-flow-shortcut-profile",
                version = 1,
                profile = CloneShortcutProfile(active, active.id, active.name)
            };
            File.WriteAllText(dialog.FileName, new JavaScriptSerializer().Serialize(export), new UTF8Encoding(false));
            ShowToast("已导出“" + active.name + "”，不包含语音设置", "success");
        }
        catch (Exception ex)
        {
            HostLog("SHORTCUT PROFILE export_failed=true error=" + SafeLogValue(ex.Message));
            ShowToast("Profile 导出失败", "error");
        }
    }

    private void ImportShortcutProfile()
    {
        using (var dialog = new OpenFileDialog())
        {
            dialog.Filter = "Vibe Flow 快捷键 Profile|*.json|所有文件|*.*";
            dialog.Title = "导入快捷键 Profile";
            dialog.CheckFileExists = true;
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                if (new FileInfo(dialog.FileName).Length > 1024 * 1024)
                    throw new InvalidDataException("Profile 文件过大");
                ShortcutProfileExport imported = new JavaScriptSerializer().Deserialize<ShortcutProfileExport>(
                    File.ReadAllText(dialog.FileName, Encoding.UTF8));
                if (imported == null || imported.format != "vibe-flow-shortcut-profile" ||
                    imported.version != 1 || imported.profile == null)
                    throw new InvalidDataException("Profile 格式不受支持");
                if (imported.profile.mappings != null)
                    foreach (KeyValuePair<string, string> pair in imported.profile.mappings)
                        if (!IsPersistableMappingAction(pair.Value))
                            throw new InvalidDataException("Profile 包含不支持的动作");

                CaptureActiveShortcutProfileMappings(config);
                ShortcutProfileConfig profile = CloneShortcutProfile(imported.profile,
                    "profile-" + Guid.NewGuid().ToString("N"),
                    MakeUniqueShortcutProfileName(imported.profile.name));
                profile.preset = "custom";
                var profiles = new List<ShortcutProfileConfig>(config.shortcutProfiles ?? new ShortcutProfileConfig[0]);
                profiles.Add(profile);
                config.shortcutProfiles = profiles.ToArray();
                config.activeShortcutProfileId = profile.id;
                ProjectActiveShortcutProfile(config);
                if (!SaveConfig()) throw new IOException("Profile 无法保存");
                bool acknowledged = StartKeyboardBridge();
                HostLog("SHORTCUT PROFILE imported=true id=" + SafeLogValue(profile.id) +
                    " bridge_ack=" + acknowledged);
                ShowPage(PageShortcuts);
                ShowToast(acknowledged ? "已导入并切换到“" + profile.name + "”" :
                    "Profile 已导入，桥接仍在确认", acknowledged ? "success" : "warning");
            }
            catch (Exception ex)
            {
                HostLog("SHORTCUT PROFILE import_failed=true error=" + SafeLogValue(ex.Message));
                ShowToast("Profile 无法导入，请确认文件来自 Vibe Flow", "error");
            }
        }
    }

    private void AddMappingOverviewCard(Control parent, RemoteVisual preview, Point location,
        string physicalKey, string label, string remoteControl, string shortKey, string longKey,
        bool supportsLongPress, bool requiresHardwareReport)
    {
        var card = NewCard(location, new Size(286, 96));
        bool observed = HasObservedPhysicalButton(physicalKey);
        bool hardwareReady = !requiresHardwareReport || observed;
        var title = NewLabel(label, 9.5f, FontStyle.Bold, ink);
        title.Location = new Point(12, 8);
        title.Size = new Size(126, 23);
        string statusText = observed ? "● 已识别" : requiresHardwareReport ? "● 待识别" : "● 可配置";
        var status = NewLabel(statusText, 7.8f, FontStyle.Bold,
            observed ? green : requiresHardwareReport ? amber : muted);
        status.Location = new Point(142, 8);
        status.Size = new Size(130, 23);
        status.TextAlign = ContentAlignment.MiddleRight;

        string shortAction = GetMapping(shortKey, DefaultConfigurableAction(shortKey));
        var shortEdit = SecondaryButton((supportsLongPress ? "短 · " : "") + MappingCardActionText(shortAction),
            new Point(12, 38), new Size(supportsLongPress ? 104 : 224, 42));
        shortEdit.Font = new Font("Microsoft YaHei UI", 8.0f, FontStyle.Bold);
        shortEdit.Click += delegate { EditMappingAction(shortKey, label + (supportsLongPress ? "短按" : "")); };
        var shortTest = IconButton("▶", new Point(supportsLongPress ? 120 : 242, 42), new Size(32, 34),
            hardwareReady ? violet : muted, "测试" + label + (supportsLongPress ? "短按" : ""));
        shortTest.Click += delegate
        {
            TestMappingAction(label + (supportsLongPress ? "短按" : ""),
                GetMapping(shortKey, DefaultConfigurableAction(shortKey)));
        };
        shortEdit.Enabled = hardwareReady;
        shortTest.Enabled = hardwareReady;

        card.Controls.Add(title);
        card.Controls.Add(status);
        card.Controls.Add(shortEdit);
        card.Controls.Add(shortTest);
        if (supportsLongPress)
        {
            string longAction = GetMapping(longKey, DefaultConfigurableAction(longKey));
            var longEdit = SecondaryButton("长 · " + MappingCardActionText(longAction),
                new Point(154, 38), new Size(92, 42));
            longEdit.Font = new Font("Microsoft YaHei UI", 8.0f, FontStyle.Bold);
            longEdit.Click += delegate { EditMappingAction(longKey, label + "长按"); };
            var longTest = IconButton("▶", new Point(248, 42), new Size(28, 34),
                hardwareReady ? violet : muted, "测试" + label + "长按");
            longTest.Click += delegate
            {
                TestMappingAction(label + "长按", GetMapping(longKey, DefaultConfigurableAction(longKey)));
            };
            longEdit.Enabled = hardwareReady;
            longTest.Enabled = hardwareReady;
            card.Controls.Add(longEdit);
            card.Controls.Add(longTest);
        }

        EventHandler highlight = delegate
        {
            preview.HighlightedControl = remoteControl;
            preview.Invalidate();
        };
        card.Click += highlight;
        title.Click += highlight;
        status.Click += highlight;
        card.MouseEnter += highlight;
        parent.Controls.Add(card);
    }

    private void AddFixedVoiceOverviewCard(Control parent, RemoteVisual preview, Point location)
    {
        var card = NewCard(location, new Size(286, 96));
        var title = NewLabel("录音键", 9.5f, FontStyle.Bold, ink);
        title.Location = new Point(12, 8);
        title.Size = new Size(126, 23);
        var fixedState = NewLabel("固定稳定链路", 7.8f, FontStyle.Bold, violet);
        fixedState.Location = new Point(142, 8);
        fixedState.Size = new Size(130, 23);
        fixedState.TextAlign = ContentAlignment.MiddleRight;
        var detail = NewLabel("按住听写 · 松开结束", 8.4f, FontStyle.Bold, violet);
        detail.Location = new Point(12, 38);
        detail.Size = new Size(260, 42);
        detail.TextAlign = ContentAlignment.MiddleCenter;
        detail.BackColor = StatusSurface("recording");
        ApplyRoundedRegion(detail, 5);
        EventHandler highlight = delegate
        {
            preview.HighlightedControl = "voice";
            preview.Invalidate();
        };
        card.Click += highlight;
        title.Click += highlight;
        detail.Click += highlight;
        card.MouseEnter += highlight;
        card.Controls.Add(title);
        card.Controls.Add(fixedState);
        card.Controls.Add(detail);
        parent.Controls.Add(card);
    }

    private void EditMappingAction(string mappingKey, string label)
    {
        string current = GetMapping(mappingKey, DefaultConfigurableAction(mappingKey));
        List<ShortcutChoice> choices = MappingActionChoicesFor(mappingKey, current);
        string selected = ShowMappingActionPicker(label, choices, current);
        if (string.IsNullOrWhiteSpace(selected)) return;
        string resolved = ResolveCustomActionSelection(selected, this);
        if (string.IsNullOrWhiteSpace(resolved)) return;
        resolved = NormalizePhysicalMappingAction(mappingKey, resolved);
        if (!IsPersistableMappingAction(resolved))
        {
            HostLog("MAPPING SAVE rejected=true key=" + SafeLogValue(mappingKey) +
                " action=" + SafeLogValue(resolved));
            ShowToast(label + "配置无效，请重新选择", "error");
            return;
        }
        SetMapping(mappingKey, resolved);
        if (mappingKey == "Home:short") SetMapping("Home", resolved);
        config.mappingPreset = "custom";
        HostLog("MAPPING SAVE requested=true key=" + SafeLogValue(mappingKey) +
            " action=" + SafeLogValue(resolved));
        if (!SaveConfig() || !PersistedMappingMatches(mappingKey, resolved))
        {
            HostLog("MAPPING SAVE persisted=false key=" + SafeLogValue(mappingKey));
            ShowToast(label + "保存失败，请打开诊断日志后重试", "error");
            return;
        }
        HostLog("MAPPING SAVE persisted=true key=" + SafeLogValue(mappingKey) +
            " action=" + SafeLogValue(resolved));
        bool active = StartKeyboardBridge();
        ShowPage(PageShortcuts);
        ShowToast(active ? label + "已保存并生效" : label + "已保存，桥接仍在确认",
            active ? "success" : "warning");
    }

    private string ShowMappingActionPicker(string label, List<ShortcutChoice> choices, string current)
    {
        using (var dialog = new Form())
        using (var search = new TextBox())
        using (var list = new ListBox())
        using (var choose = new Button())
        using (var cancel = new Button())
        {
            dialog.Text = "配置 " + label;
            dialog.StartPosition = FormStartPosition.CenterParent;
            dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
            dialog.MinimizeBox = false;
            dialog.MaximizeBox = false;
            dialog.ShowInTaskbar = false;
            dialog.ClientSize = new Size(620, 570);
            dialog.BackColor = cardBackground;
            dialog.Font = Font;
            var title = NewLabel("选择要执行的动作", 14f, FontStyle.Bold, ink);
            title.Location = new Point(24, 20);
            title.Size = new Size(560, 32);
            var help = NewLabel("支持应用、网页、编辑、系统、媒体与自定义快捷键", 8.7f, FontStyle.Regular, muted);
            help.Location = new Point(24, 54);
            help.Size = new Size(560, 24);
            search.Location = new Point(24, 88);
            search.Size = new Size(572, 32);
            search.Font = new Font("Microsoft YaHei UI", 10f);
            search.BackColor = surfaceBackground;
            search.ForeColor = ink;
            list.Location = new Point(24, 132);
            list.Size = new Size(572, 360);
            list.BorderStyle = BorderStyle.FixedSingle;
            list.BackColor = surfaceBackground;
            list.ForeColor = ink;
            list.Font = new Font("Microsoft YaHei UI", 10f);
            list.IntegralHeight = false;

            Action refresh = delegate
            {
                string query = (search.Text ?? "").Trim();
                list.BeginUpdate();
                list.Items.Clear();
                foreach (ShortcutChoice choice in choices)
                    if (query.Length == 0 || choice.Label.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0)
                        list.Items.Add(choice);
                list.EndUpdate();
                int index = FindShortcutChoiceInList(list, current);
                if (index >= 0) list.SelectedIndex = index;
                else if (list.Items.Count > 0) list.SelectedIndex = 0;
            };
            refresh();
            search.TextChanged += delegate { refresh(); };

            string result = "";
            Action accept = delegate
            {
                ShortcutChoice selected = list.SelectedItem as ShortcutChoice;
                if (selected == null) return;
                result = selected.Shortcut;
                dialog.DialogResult = DialogResult.OK;
                dialog.Close();
            };
            list.DoubleClick += delegate { accept(); };
            choose.Text = "选择";
            choose.Location = new Point(392, 514);
            choose.Size = new Size(96, 36);
            choose.BackColor = violet;
            choose.ForeColor = Color.White;
            choose.FlatStyle = FlatStyle.Flat;
            choose.FlatAppearance.BorderSize = 0;
            choose.Click += delegate { accept(); };
            cancel.Text = "取消";
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Location = new Point(500, 514);
            cancel.Size = new Size(96, 36);
            cancel.FlatStyle = FlatStyle.Flat;
            cancel.FlatAppearance.BorderColor = line;
            dialog.CancelButton = cancel;
            dialog.Controls.Add(title);
            dialog.Controls.Add(help);
            dialog.Controls.Add(search);
            dialog.Controls.Add(list);
            dialog.Controls.Add(choose);
            dialog.Controls.Add(cancel);
            return dialog.ShowDialog(this) == DialogResult.OK ? result : "";
        }
    }

    private static int FindShortcutChoiceInList(ListBox list, string shortcut)
    {
        for (int i = 0; i < list.Items.Count; i++)
        {
            ShortcutChoice choice = list.Items[i] as ShortcutChoice;
            if (choice != null && choice.Shortcut.Equals(shortcut ?? "", StringComparison.OrdinalIgnoreCase)) return i;
        }
        return -1;
    }

    private void BuildMappingsPageV13Legacy()
    {
        content.AutoScrollMinSize = new Size(1000, 830);
        AddPageTitle("快捷键", "只响应 RC003；普通键盘优先，默认功能不会因定制而丢失");

        var remoteCard = NewCard(new Point(34, 100), new Size(356, 680));
        remoteCard.Controls.Add(SectionTitle("遥控器", "\uE7F4", new Point(24, 20)));
        var remoteHint = NewLabel("实体按键实时反馈", 8.7f, FontStyle.Regular, muted);
        remoteHint.Location = new Point(202, 23);
        remoteHint.Size = new Size(130, 24);
        remoteHint.TextAlign = ContentAlignment.MiddleRight;
        var previewRemote = new RemoteVisual();
        previewRemote.Location = new Point(20, 58);
        previewRemote.Size = new Size(316, 386);
        previewRemote.IsActive = true;
        previewRemote.ShowCallouts = true;
        previewRemote.AccentColor = violet;

        var fixedTitle = NewLabel("可配置按键", 9.3f, FontStyle.Bold, ink);
        fixedTitle.Location = new Point(24, 458);
        fixedTitle.Size = new Size(160, 24);
        string fixedText =
            "方向键   默认导航，可分别定制\r\n" +
            "确认键   默认 Enter / 确认发送\r\n" +
            "Home     短按 / 长按分别配置\r\n" +
            "开机键   检测到硬件报告后可配置\r\n" +
            "TV       默认打开任务视图\r\n" +
            "功能键   短按和长按分别配置\r\n" +
            "录音键   固定按住听写，松开结束";
        var fixedList = NewLabel(fixedText, 8.9f, FontStyle.Regular, muted);
        fixedList.Location = new Point(24, 490);
        fixedList.Size = new Size(306, 158);
        var unsupported = NewLabel("返回和独立音量键仍没有稳定的 Windows 报告；开机键仅在本机识别成功后生效。", 8.2f, FontStyle.Regular, amber);
        unsupported.Location = new Point(24, 646);
        unsupported.Size = new Size(306, 34);
        remoteCard.Controls.Add(remoteHint);
        remoteCard.Controls.Add(previewRemote);
        remoteCard.Controls.Add(fixedTitle);
        remoteCard.Controls.Add(fixedList);
        remoteCard.Controls.Add(unsupported);

        var editor = NewCard(new Point(406, 100), new Size(588, 680));
        editor.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        editor.Controls.Add(SectionTitle("按键定制", "\uE765", new Point(26, 20)));
        var editorHint = NewLabel("应用 · 网页 · 系统 · 媒体 · 自定义快捷键", 8.7f, FontStyle.Regular, muted);
        editorHint.Location = new Point(286, 23);
        editorHint.Size = new Size(270, 24);
        editorHint.TextAlign = ContentAlignment.MiddleRight;
        editor.Controls.Add(editorHint);

        string[] keys = { "电源键", "上键", "下键", "左键", "右键", "确认键", "Home", "TV", "功能键" };
        string[] labels = { "开机键", "上键", "下键", "左键", "右键", "确认键", "Home 键", "TV 键", "功能键" };
        string[] selectorLabels = { "⏻ 开机", "↑ 上", "↓ 下", "← 左", "→ 右", "● 确认", "⌂ Home", "▣ TV", "≡ 功能" };
        string[] controls = { "power", "up", "down", "left", "right", "ok", "home", "tv", "menu" };
        Point[] selectorLocations = {
            new Point(24, 60), new Point(200, 60), new Point(376, 60),
            new Point(24, 108), new Point(200, 108), new Point(376, 108),
            new Point(24, 156), new Point(200, 156), new Point(376, 156)
        };
        var selectorButtons = new Button[keys.Length];
        var configuration = new Panel();
        configuration.Location = new Point(24, 212);
        configuration.Size = new Size(540, 440);
        configuration.BackColor = Color.Transparent;
        editor.Controls.Add(configuration);
        string selectedKey = keys[0];
        Action renderConfiguration = null;

        for (int i = 0; i < keys.Length; i++)
        {
            string key = keys[i];
            var selector = SecondaryButton(selectorLabels[i], selectorLocations[i], new Size(164, 38));
            selectorButtons[i] = selector;
            selector.Click += delegate
            {
                selectedKey = key;
                if (renderConfiguration != null) renderConfiguration();
            };
            editor.Controls.Add(selector);
        }

        renderConfiguration = delegate
        {
            while (configuration.Controls.Count > 0) configuration.Controls[0].Dispose();
            int selectedIndex = Array.IndexOf(keys, selectedKey);
            if (selectedIndex < 0) selectedIndex = 0;
            for (int i = 0; i < selectorButtons.Length; i++)
            {
                selectorButtons[i].BackColor = i == selectedIndex ?
                    (darkTheme ? Color.FromArgb(46, 48, 56) : Color.FromArgb(233, 237, 255)) : cardBackground;
                selectorButtons[i].ForeColor = i == selectedIndex ? violet : ink;
            }
            previewRemote.HighlightedControl = controls[selectedIndex];
            previewRemote.Invalidate();

            bool functionKey = selectedKey == "功能键";
            bool gestureKey = functionKey || selectedKey == "Home" || selectedKey == "电源键";
            string configKey = functionKey ? "功能键:short" :
                selectedKey == "Home" ? "Home:short" :
                selectedKey == "电源键" ? "电源键:short" : selectedKey;
            string defaultAction = DefaultConfigurableAction(configKey);
            string currentAction = GetMapping(configKey, defaultAction);

            var title = NewLabel(labels[selectedIndex], 15f, FontStyle.Bold, ink);
            title.Location = new Point(2, 4);
            title.Size = new Size(170, 32);
            bool observedNow = HasObservedPhysicalButton(selectedKey);
            var physicalState = NewLabel(observedNow ? "●  已识别实体按键" : "●  尚未收到该实体键",
                9f, FontStyle.Bold, observedNow ? green : amber);
            physicalState.Location = new Point(274, 7);
            physicalState.Size = new Size(238, 28);
            physicalState.TextAlign = ContentAlignment.MiddleRight;
            configuration.Controls.Add(title);
            configuration.Controls.Add(physicalState);

            var verify = SecondaryButton("识别实体键", new Point(2, 46), new Size(126, 36));
            verify.Click += delegate
            {
                long baseline = InputBridgeLogLength();
                physicalState.Text = "●  请按一次遥控器上的" + labels[selectedIndex];
                physicalState.ForeColor = cyan;
                var timer = new System.Windows.Forms.Timer();
                timer.Interval = 250;
                int ticks = 0;
                timer.Tick += delegate
                {
                    ticks++;
                    if (physicalState.IsDisposed || HasObservedPhysicalButtonSince(selectedKey, baseline))
                    {
                        timer.Stop();
                        timer.Dispose();
                        if (!physicalState.IsDisposed)
                        {
                            physicalState.Text = "●  实体按键识别成功";
                            physicalState.ForeColor = green;
                            previewRemote.HighlightedControl = controls[selectedIndex];
                            previewRemote.Invalidate();
                        }
                    }
                    else if (ticks >= 40)
                    {
                        timer.Stop();
                        timer.Dispose();
                        physicalState.Text = "●  未收到按键；请打开自检";
                        physicalState.ForeColor = coral;
                    }
                };
                timer.Start();
            };
            configuration.Controls.Add(verify);
            var protectedSource = NewLabel("●  RC003 来源保护已开启", 8.8f, FontStyle.Bold, green);
            protectedSource.Location = new Point(158, 50);
            protectedSource.Size = new Size(220, 28);
            configuration.Controls.Add(protectedSource);

            var actionLabel = NewLabel(gestureKey ? "短按动作" : "单击动作", 9.5f, FontStyle.Bold, ink);
            actionLabel.Location = new Point(2, 100);
            actionLabel.Size = new Size(100, 26);
            var actionBox = StyledCombo(new Point(2, 130), new Size(400, 40));
            List<ShortcutChoice> choices = MappingActionChoicesFor(configKey, currentAction);
            foreach (ShortcutChoice choice in choices) actionBox.Items.Add(choice);
            actionBox.SelectedIndex = FindShortcutChoice(choices, currentAction);
            var actionTest = SecondaryButton("立即测试", new Point(416, 130), new Size(108, 40));
            actionTest.Click += delegate
            {
                TestMappingAction(labels[selectedIndex], GetMapping(configKey, defaultAction));
            };

            actionBox.SelectedIndexChanged += delegate
            {
                ShortcutChoice selected = actionBox.SelectedItem as ShortcutChoice;
                if (selected == null) return;
                string resolved = ResolveCustomActionSelection(selected.Shortcut, this);
                if (string.IsNullOrWhiteSpace(resolved)) { renderConfiguration(); return; }
                SetMapping(configKey, resolved);
                config.mappingPreset = "custom";
                SaveConfig();
                StartKeyboardBridge();
                renderConfiguration();
                ShowToast(labels[selectedIndex] + "配置已保存并生效", "success");
            };

            configuration.Controls.Add(actionLabel);
            configuration.Controls.Add(actionBox);
            configuration.Controls.Add(actionTest);

            int commandTop = 216;
            if (gestureKey)
            {
                string longKey = functionKey ? "功能键:long" :
                    selectedKey == "Home" ? "Home:long" : "电源键:long";
                string longDefault = DefaultConfigurableAction(longKey);
                string longCurrent = GetMapping(longKey, longDefault);
                var longLabel = NewLabel("长按动作", 9.5f, FontStyle.Bold, ink);
                longLabel.Location = new Point(2, 194);
                longLabel.Size = new Size(100, 26);
                var longBox = StyledCombo(new Point(2, 224), new Size(400, 40));
                List<ShortcutChoice> longChoices = MappingActionChoicesFor(longKey, longCurrent);
                foreach (ShortcutChoice choice in longChoices) longBox.Items.Add(choice);
                longBox.SelectedIndex = FindShortcutChoice(longChoices, longCurrent);
                var longTest = SecondaryButton("立即测试", new Point(416, 224), new Size(108, 40));
                longTest.Click += delegate { TestMappingAction(labels[selectedIndex] + "长按", GetMapping(longKey, longDefault)); };
                longBox.SelectedIndexChanged += delegate
                {
                    ShortcutChoice selected = longBox.SelectedItem as ShortcutChoice;
                    if (selected == null) return;
                    string resolved = ResolveCustomActionSelection(selected.Shortcut, this);
                    if (string.IsNullOrWhiteSpace(resolved)) { renderConfiguration(); return; }
                    SetMapping(longKey, resolved);
                    config.mappingPreset = "custom";
                    SaveConfig();
                    StartKeyboardBridge();
                    renderConfiguration();
                    ShowToast(labels[selectedIndex] + "长按配置已保存并生效", "success");
                };
                configuration.Controls.Add(longLabel);
                configuration.Controls.Add(longBox);
                configuration.Controls.Add(longTest);
                commandTop = 310;
            }

            var disable = SecondaryButton("禁用", new Point(2, commandTop), new Size(96, 40));
            disable.Click += delegate
            {
                SetMapping(configKey, "none");
                if (gestureKey)
                    SetMapping(functionKey ? "功能键:long" : selectedKey == "Home" ? "Home:long" : "电源键:long", "none");
                SaveConfig();
                StartKeyboardBridge();
                renderConfiguration();
                ShowToast(labels[selectedIndex] + "已禁用", "success");
            };
            var reset = SecondaryButton("恢复默认", new Point(112, commandTop), new Size(112, 40));
            reset.Click += delegate
            {
                SetMapping(configKey, defaultAction);
                if (gestureKey)
                {
                    string longKey = functionKey ? "功能键:long" : selectedKey == "Home" ? "Home:long" : "电源键:long";
                    SetMapping(longKey, DefaultConfigurableAction(longKey));
                }
                SaveConfig();
                StartKeyboardBridge();
                renderConfiguration();
                ShowToast(labels[selectedIndex] + "已恢复默认", "success");
            };
            var screenshot = SecondaryButton("区域截图", new Point(238, commandTop), new Size(112, 40));
            screenshot.Click += delegate
            {
                SetMapping(configKey, "win+shift+s");
                SaveConfig();
                StartKeyboardBridge();
                renderConfiguration();
                ShowToast(labels[selectedIndex] + "已设为区域截图", "success");
            };
            var chooseApp = PrimaryButton("选择应用或网页", new Point(364, commandTop), new Size(160, 40));
            chooseApp.Click += delegate
            {
                string resolved = ResolveCustomActionSelection("select-app:prompt", this);
                if (string.IsNullOrWhiteSpace(resolved)) return;
                SetMapping(configKey, resolved);
                SaveConfig();
                StartKeyboardBridge();
                renderConfiguration();
                ShowToast(labels[selectedIndex] + "已绑定应用", "success");
            };
            var note = NewLabel("来源保护只处理遥控器事件，普通键盘不会触发这里的映射。录音键继续使用稳定链路，不参与自定义。",
                8.8f, FontStyle.Regular, muted);
            note.Location = new Point(2, commandTop + 58);
            note.Size = new Size(520, 54);
            configuration.Controls.Add(disable);
            configuration.Controls.Add(reset);
            configuration.Controls.Add(screenshot);
            configuration.Controls.Add(chooseApp);
            configuration.Controls.Add(note);
        };

        renderConfiguration();
        content.Controls.Add(remoteCard);
        content.Controls.Add(editor);
    }

    private void BuildMappingsPageLegacy()
    {
        for (int i = 0; i < customButtonStatusLabels.Length; i++) customButtonStatusLabels[i] = null;
        content.AutoScrollMinSize = new Size(1030, 990);
        AddPageTitle("按键快捷方式", "所有真实遥控器按键统一配置；选择后自动保存，可立即测试");
        var mappings = NewCard(new Point(34, 100), new Size(620, 850));
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
            ShowPage(PageShortcuts);
            Toast("按键方案已应用");
        };
        var presetHelp = NewLabel("Home、TV、功能键等均可自定义；可打开本机应用、切换运行中的程序或访问网页。", 8.4f, FontStyle.Regular, muted);
        presetHelp.Location = new Point(24, 96);
        presetHelp.Size = new Size(548, 24);
        mappings.Controls.Add(presetLabel);
        mappings.Controls.Add(preset);
        mappings.Controls.Add(applyPreset);
        mappings.Controls.Add(presetHelp);

        var preview = NewCard(new Point(670, 100), new Size(324, 850));
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
        var mappingSelection = NewLabel("录音键\r\n按住听写 / 松开结束", 9.2f, FontStyle.Bold, violet);
        mappingSelection.Location = new Point(24, 454);
        mappingSelection.Size = new Size(276, 54);
        mappingSelection.TextAlign = ContentAlignment.MiddleCenter;
        mappingSelection.BackColor = Color.FromArgb(243, 241, 255);
        ApplyRoundedRegion(mappingSelection, 6);
        var previewHelp = NewLabel("返回、独立音量和开机键需由遥控器真实上报。\r\n若设备不支持，自检会明确显示。", 7.8f, FontStyle.Regular, amber);
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
            { "方向键", "passthrough", "标准方向键，不附加组合手势" },
            { "返回键", "alt+left", "返回上一页或上一个界面" },
            { "音量 +", "volumeup", "调高 Windows 系统音量" },
            { "音量 -", "volumedown", "调低 Windows 系统音量" },
            { "电源键", "escape", "默认安全动作：取消 / Esc" }
        };
        string[] rowGlyphs = { "\uE720", "\uE73E", "\uE80F", "TV", "\uE765", "\uE7AD", "\uE72B", "\uE767", "\uE767", "\uE7E8" };
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
                string selectedText = rowIndex == 0 ? "按住听写 / 松开结束" : rowIndex == 5 ? "标准方向键" : selected == null ? rows[rowIndex, 2] : selected.Label;
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
                string resolvedAction = ResolveCustomActionSelection(selected.Shortcut, this);
                if (string.IsNullOrWhiteSpace(resolvedAction))
                {
                    input.SelectedIndex = FindShortcutChoice(choices, currentAction);
                    return;
                }
                config.mappingPreset = "custom";
                string selectedKey = rows[rowIndex, 0] == "方向键" ? "上 / 下 / 左 / 右" : rows[rowIndex, 0];
                SetMapping(selectedKey, resolvedAction);
                SaveConfig();
                updatePreview();
                string conflict = FindMappingConflict(selectedKey, resolvedAction);
                ShowToast(string.IsNullOrEmpty(conflict)
                    ? rows[rowIndex, 0] + "已设为“" + selected.Label + "”"
                    : "已保存；" + rows[rowIndex, 0] + "与" + conflict + "使用相同功能",
                    string.IsNullOrEmpty(conflict) ? "success" : "warning");
                if (!string.Equals(resolvedAction, selected.Shortcut, StringComparison.OrdinalIgnoreCase)) ShowPage(PageShortcuts);
            };
            var test = SecondaryButton("测试", new Point(370, 10), new Size(82, 36));
            test.Enabled = input.Enabled;
            test.Click += delegate
            {
                string action = GetMapping(configKey, rows[rowIndex, 1]);
                TestMappingAction(configKey, action);
            };
            var reset = SecondaryButton("恢复", new Point(464, 10), new Size(82, 36));
            reset.Enabled = input.Enabled;
            reset.Click += delegate
            {
                SetMapping(configKey, rows[rowIndex, 1]);
                config.mappingPreset = "custom";
                SaveConfig();
                ShowToast(rows[rowIndex, 0] + "已恢复默认功能", "success");
                ShowPage(PageShortcuts);
            };
            rowBand.Controls.Add(icon);
            rowBand.Controls.Add(name);
            rowBand.Controls.Add(input);
            rowBand.Controls.Add(test);
            rowBand.Controls.Add(reset);
            mappings.Controls.Add(rowBand);
            if (i == 0) updatePreview();
        }
        var save = PrimaryButton("保存并应用", new Point(144, 780), new Size(132, 42));
        save.Click += delegate { SaveConfig(); StartKeyboardBridge(); ShowToast("按键快捷方式已保存并生效", "success"); };
        var openBridge = SecondaryButton("打开高级配置", new Point(290, 780), new Size(150, 42));
        openBridge.Click += delegate { Process.Start(Path.Combine(root, "voxdeck-shortcuts.json")); };
        mappings.Controls.Add(save);
        mappings.Controls.Add(openBridge);
        content.Controls.Add(mappings);
        content.Controls.Add(preview);
    }

    private void BuildDevicePage()
    {
        AddPageTitle("一键自检", "逐项说明正确状态、当前状态、原因和修复入口");
        SelfCheckReport report = BuildSelfCheckReport();
        int checksHeight = 66 + report.Items.Count * 112;
        int diagnosticsY = 302 + checksHeight;
        content.AutoScrollMinSize = new Size(1000, diagnosticsY + 326);

        var overview = NewCard(new Point(34, 100), new Size(960, 120));
        overview.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        overview.BackColor = report.FailedCount > 0 ? StatusSurface("error") :
            report.CheckingCount > 0 ? StatusSurface("recovering") :
            report.WarningCount > 0 ? StatusSurface("connecting") : StatusSurface("ready");
        var score = new RoundPanel();
        score.Location = new Point(24, 22);
        score.Size = new Size(76, 76);
        score.Radius = 38;
        score.BackColor = report.FailedCount > 0 ? StatusSurface("error") :
            report.CheckingCount > 0 ? StatusSurface("recovering") :
            report.WarningCount > 0 ? StatusSurface("connecting") : StatusSurface("ready");
        score.BorderColor = report.FailedCount > 0 ? Color.FromArgb(238, 185, 185) :
            report.CheckingCount > 0 ? Color.FromArgb(155, 215, 226) :
            report.WarningCount > 0 ? Color.FromArgb(242, 211, 151) : Color.FromArgb(164, 225, 193);
        string scoreText = report.FailedCount > 0 ? report.FailedCount + " 错误" :
            report.CheckingCount > 0 ? "待验证" : report.WarningCount > 0 ? "可使用" : "已通过";
        var scoreValue = NewLabel(scoreText, 10.5f, FontStyle.Bold,
            report.FailedCount > 0 ? coral : report.CheckingCount > 0 ? cyan :
            report.WarningCount > 0 ? amber : green);
        scoreValue.Dock = DockStyle.Fill;
        scoreValue.TextAlign = ContentAlignment.MiddleCenter;
        score.Controls.Add(scoreValue);
        var headline = NewLabel(report.Headline, 16f, FontStyle.Bold, ink);
        headline.Location = new Point(124, 22);
        headline.Size = new Size(510, 34);
        var overviewDetail = NewLabel(report.Detail, 9.1f, FontStyle.Regular, muted);
        overviewDetail.Location = new Point(125, 61);
        overviewDetail.Size = new Size(540, 38);
        var rerun = PrimaryButton("重新自检", new Point(696, 38), new Size(112, 42));
        rerun.Click += delegate { RunSelfCheckAndRefresh(); };
        var setup = SecondaryButton("首次设置", new Point(820, 38), new Size(112, 42));
        setup.Click += delegate { ShowSetupWizard(); };
        overview.Controls.Add(score);
        overview.Controls.Add(headline);
        overview.Controls.Add(overviewDetail);
        overview.Controls.Add(rerun);
        overview.Controls.Add(setup);

        var legend = NewCard(new Point(34, 236), new Size(960, 50));
        legend.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        string[] legendText = { "正常", "正在检测", "需要配置", "错误", "不支持" };
        Color[] legendColors = { green, cyan, amber, coral, Color.FromArgb(142, 151, 170) };
        for (int i = 0; i < legendText.Length; i++)
        {
            var item = NewLabel("●  " + legendText[i], 8.6f, FontStyle.Bold, legendColors[i]);
            item.Location = new Point(30 + i * 174, 14);
            item.Size = new Size(150, 24);
            legend.Controls.Add(item);
        }

        var checks = NewCard(new Point(34, 302), new Size(960, checksHeight));
        checks.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        checks.Controls.Add(SectionTitle("检查结果", "\uE9D9", new Point(24, 18)));
        var checkHint = NewLabel("修复后返回言灵会自动重新检测，无需重新开始教程。", 8.7f, FontStyle.Regular, muted);
        checkHint.Location = new Point(520, 21);
        checkHint.Size = new Size(410, 24);
        checkHint.TextAlign = ContentAlignment.MiddleRight;
        checks.Controls.Add(checkHint);
        for (int i = 0; i < report.Items.Count; i++) AddSelfCheckRow(checks, report.Items[i], 54 + i * 112);

        var diagnostics = NewCard(new Point(34, diagnosticsY), new Size(960, 290));
        diagnostics.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        diagnostics.Controls.Add(SectionTitle("高级诊断", "\uE720", new Point(24, 18)));
        var summaryTitle = NewLabel("最近一次听写", 9f, FontStyle.Bold, muted);
        summaryTitle.Location = new Point(24, 55);
        summaryTitle.Size = new Size(160, 24);
        var summaryBand = new Panel();
        summaryBand.Location = new Point(24, 82);
        summaryBand.Size = new Size(548, 132);
        summaryBand.BackColor = surfaceBackground;
        var summary = NewLabel(BuildSessionHealthSummary(), 8.2f, FontStyle.Regular, ink);
        summary.Location = new Point(12, 8);
        summary.Size = new Size(524, 116);
        summaryBand.Controls.Add(summary);
        var rawTitle = NewLabel("技术日志片段", 9f, FontStyle.Bold, muted);
        rawTitle.Location = new Point(594, 55);
        rawTitle.Size = new Size(160, 24);
        logBox = new TextBox();
        logBox.Location = new Point(594, 82);
        logBox.Size = new Size(340, 132);
        logBox.Multiline = true;
        logBox.ScrollBars = ScrollBars.Vertical;
        logBox.ReadOnly = true;
        logBox.BorderStyle = BorderStyle.FixedSingle;
        logBox.BackColor = inputBackground;
        logBox.ForeColor = muted;
        logBox.Font = new Font("Consolas", 7.8f);
        logBox.Text = LoadRecentDiagnostics();
        var copy = PrimaryButton("复制问题摘要", new Point(24, 230), new Size(138, 38));
        copy.Click += delegate
        {
            Clipboard.SetText(BuildProblemSummary());
            ShowToast("问题摘要已复制，可直接粘贴到 GitHub Issue", "success");
        };
        var export = SecondaryButton("导出诊断", new Point(174, 230), new Size(116, 38));
        export.Click += delegate { ExportDiagnostics(); };
        var openLogs = SecondaryButton("打开日志", new Point(302, 230), new Size(108, 38));
        openLogs.Click += delegate { OpenLogFolder(); };
        var captureAudio = SecondaryButton("诊断下一段音频", new Point(422, 230), new Size(150, 38));
        captureAudio.Click += delegate { CaptureNextAudioDiagnostic(); };
        var privacy = NewLabel("日志不包含录音、转译文字或完整设备标识", 8.4f, FontStyle.Regular, muted);
        privacy.Location = new Point(594, 237);
        privacy.Size = new Size(340, 24);
        privacy.TextAlign = ContentAlignment.MiddleRight;
        diagnostics.Controls.Add(summaryTitle);
        diagnostics.Controls.Add(summaryBand);
        diagnostics.Controls.Add(rawTitle);
        diagnostics.Controls.Add(logBox);
        diagnostics.Controls.Add(copy);
        diagnostics.Controls.Add(export);
        diagnostics.Controls.Add(openLogs);
        diagnostics.Controls.Add(captureAudio);
        diagnostics.Controls.Add(privacy);

        content.Controls.Add(overview);
        content.Controls.Add(legend);
        content.Controls.Add(checks);
        content.Controls.Add(diagnostics);
        logBox.SelectionStart = logBox.TextLength;
        logBox.ScrollToCaret();
    }

    private void BuildDevicePageLegacy()
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
        content.AutoScrollMinSize = new Size(1000, 990);
        AddPageTitle("偏好设置", "让言灵按你的习惯在后台运行");
        var startupCard = NewCard(new Point(34, 100), new Size(580, 360));
        startupCard.Controls.Add(SectionTitle("启动与窗口", "\uE713", new Point(28, 22)));
        var start = StyledCheck("打开言灵后自动连接遥控器", config.startBridgeOnLaunch, new Point(32, 70));
        start.CheckedChanged += delegate { config.startBridgeOnLaunch = start.Checked; SaveConfig(); };
        var traySetting = StyledCheck("关闭主窗口后继续在系统托盘运行", config.minimizeToTray, new Point(32, 118));
        traySetting.CheckedChanged += delegate { config.minimizeToTray = traySetting.Checked; SaveConfig(); };
        var startup = StyledCheck("登录 Windows 后自动启动言灵", config.launchAtStartup, new Point(32, 166));
        startup.CheckedChanged += delegate { config.launchAtStartup = startup.Checked; SetLaunchAtStartup(startup.Checked); SaveConfig(); };
        var themeLabel = NewLabel("界面主题", 9.5f, FontStyle.Bold, ink);
        themeLabel.Location = new Point(32, 218);
        themeLabel.Size = new Size(120, 30);
        var lightTheme = SecondaryButton("白天模式", new Point(154, 212), new Size(106, 38));
        var darkThemeButton = SecondaryButton("夜间模式", new Point(268, 212), new Size(106, 38));
        var systemTheme = SecondaryButton("跟随 Windows", new Point(382, 212), new Size(136, 38));
        bool lightSelected = string.Equals(config.theme, "light", StringComparison.OrdinalIgnoreCase);
        bool darkSelected = string.Equals(config.theme, "dark", StringComparison.OrdinalIgnoreCase);
        bool systemSelected = string.Equals(config.theme, "system", StringComparison.OrdinalIgnoreCase);
        lightTheme.BackColor = lightSelected ? violet : surfaceBackground;
        lightTheme.ForeColor = lightSelected ? Color.White : ink;
        darkThemeButton.BackColor = darkSelected ? violet : surfaceBackground;
        darkThemeButton.ForeColor = darkSelected ? Color.White : ink;
        systemTheme.BackColor = systemSelected ? violet : surfaceBackground;
        systemTheme.ForeColor = systemSelected ? Color.White : ink;
        lightTheme.Click += delegate { ApplyThemePreference("light"); };
        darkThemeButton.Click += delegate { ApplyThemePreference("dark"); };
        systemTheme.Click += delegate { ApplyThemePreference("system"); };
        var startupBand = new Panel();
        startupBand.Location = new Point(30, 282);
        startupBand.Size = new Size(520, 58);
        startupBand.BackColor = surfaceBackground;
        var startupState = NewLabel((config.launchAtStartup ? "●  已设置开机启动" : "●  仅在手动打开后运行") + "  ·  " +
            (config.minimizeToTray ? "关闭窗口后保持连接" : "关闭窗口时退出"), 9f, FontStyle.Bold,
            config.launchAtStartup ? green : muted);
        startupState.Location = new Point(16, 17);
        startupState.Size = new Size(486, 26);
        startupBand.Controls.Add(startupState);
        startupCard.Controls.Add(start);
        startupCard.Controls.Add(traySetting);
        startupCard.Controls.Add(startup);
        startupCard.Controls.Add(themeLabel);
        startupCard.Controls.Add(lightTheme);
        startupCard.Controls.Add(darkThemeButton);
        startupCard.Controls.Add(systemTheme);
        startupCard.Controls.Add(startupBand);

        var feedbackCard = NewCard(new Point(630, 100), new Size(364, 360));
        feedbackCard.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        feedbackCard.Controls.Add(SectionTitle("交互反馈", "\uE8BD", new Point(26, 22)));
        var feedbackSound = StyledCheck("录音结束或失败时播放提示音", config.soundFeedbackEnabled, new Point(28, 72));
        feedbackSound.Size = new Size(308, 40);
        feedbackSound.CheckedChanged += delegate
        {
            config.soundFeedbackEnabled = feedbackSound.Checked;
            SaveConfig();
            ShowToast(feedbackSound.Checked ? "听写提示音已开启" : "听写提示音已关闭", "success");
        };
        var previewStopSound = SecondaryButton("试听结束提示音", new Point(28, 126), new Size(284, 40));
        previewStopSound.Click += delegate
        {
            PlayRecordingCue(false);
            ShowToast("已播放录音结束提示音", "success");
        };
        var feedbackNote = NewLabel("开始录音使用首页光效，不播放声音；结束时播放清晰、短促的完成音。", 8.9f, FontStyle.Regular, muted);
        feedbackNote.Location = new Point(28, 194);
        feedbackNote.Size = new Size(304, 52);
        var feedbackState = NewLabel("●  视觉反馈始终开启", 9f, FontStyle.Bold, violet);
        feedbackState.Location = new Point(28, 258);
        feedbackState.Size = new Size(260, 24);
        feedbackCard.Controls.Add(feedbackSound);
        feedbackCard.Controls.Add(previewStopSound);
        feedbackCard.Controls.Add(feedbackNote);
        feedbackCard.Controls.Add(feedbackState);

        BridgeHealthSnapshot routingHealth = ReadKeyboardBridgeHealth();
        bool exactDeviceIsolation = routingHealth.FilterHealthy;
        var routingCard = NewCard(new Point(34, 476), new Size(960, 172));
        routingCard.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        routingCard.Controls.Add(SectionTitle("按键来源保护", "\uE7BA", new Point(28, 22)));
        var sourceProtection = StyledCheck("设备识别：只有带 RC003 身份的事件可以执行遥控器动作",
            true, new Point(32, 62));
        sourceProtection.Size = new Size(620, 40);
        sourceProtection.AutoCheck = false;
        sourceProtection.TabStop = false;
        var sourceProtectionState = NewLabel(exactDeviceIsolation ?
            "●  设备级精确隔离" : "●  Raw Input 安全直通", 9f, FontStyle.Bold,
            exactDeviceIsolation ? green : cyan);
        sourceProtectionState.Location = new Point(660, 68);
        sourceProtectionState.Size = new Size(268, 28);
        var sourceProtectionNote = NewLabel(
            exactDeviceIsolation ?
            "RC003 专属签名通道已就绪：遥控器原按键被设备级拦截，实体键盘保持原行为。" :
            "未安装签名通道时，言灵不会拦截来源未知的键，因此实体键盘保持原行为；自定义遥控器键的原始按键效果可能同时发生。签名通道是可选增强，不影响动作执行。",
            8.8f, FontStyle.Regular, muted);
        sourceProtectionNote.Location = new Point(34, 112);
        sourceProtectionNote.Size = new Size(890, 34);
        routingCard.Controls.Add(sourceProtection);
        routingCard.Controls.Add(sourceProtectionState);
        routingCard.Controls.Add(sourceProtectionNote);

        var privacyCard = NewCard(new Point(34, 664), new Size(960, 318));
        privacyCard.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        var privacyTitle = SectionTitle("隐私与维护", "\uEA18", new Point(28, 22));
        var privacy = StyledCheck("本地安全模式：默认不保存录音、不上传音频、不读取听写文字", true, new Point(32, 62));
        privacy.AutoCheck = false;
        privacy.TabStop = false;
        privacy.ForeColor = muted;
        var privacyNote = NewLabel("普通日志只记录连接状态与聚合指标，单个日志自动限制为 4 MB。诊断音频必须每次明确确认。", 8.8f, FontStyle.Regular, muted);
        privacyNote.Location = new Point(34, 104);
        privacyNote.Size = new Size(830, 28);
        var automaticUpdates = StyledCheck("自动检查 GitHub 正式版更新（安装前始终确认）", config.autoCheckUpdates, new Point(32, 132));
        automaticUpdates.Size = new Size(520, 40);
        automaticUpdates.CheckedChanged += delegate
        {
            config.autoCheckUpdates = automaticUpdates.Checked;
            SaveConfig();
            ShowToast(automaticUpdates.Checked ? "自动更新检查已开启" : "自动更新检查已关闭", "success");
        };
        var setup = PrimaryButton("打开入门指南", new Point(32, 184), new Size(132, 42));
        setup.Click += delegate { ShowSetupWizard(); };
        var export = SecondaryButton("备份配置", new Point(176, 184), new Size(112, 42));
        export.Click += delegate { ExportConfig(); };
        var import = SecondaryButton("导入配置", new Point(300, 184), new Size(112, 42));
        import.Click += delegate { ImportConfig(); };
        var restore = SecondaryButton("恢复上次", new Point(424, 184), new Size(112, 42));
        restore.Click += delegate { RestorePreviousConfig(); };
        var updates = SecondaryButton("安全检查更新", new Point(548, 184), new Size(124, 42));
        updates.Click += delegate { CheckForUpdates(true); };
        var about = NewLabel(DisplayProductName + " · " + ProductRelease + " · Windows 功能候选版\r\nRC003 本地语音传输与快捷操作工具 · 开源版本", 9.5f, FontStyle.Regular, muted);
        about.Location = new Point(690, 184);
        about.Size = new Size(238, 66);
        var profile = NewLabel("稳定语音档案 v" + StableVoiceProfileVersion + "  ·  配置 schema " + ConfigSchemaVersion, 8.7f, FontStyle.Bold, violet);
        profile.Location = new Point(32, 260);
        profile.Size = new Size(400, 24);
        privacyCard.Controls.Add(privacyTitle);
        privacyCard.Controls.Add(privacy);
        privacyCard.Controls.Add(privacyNote);
        privacyCard.Controls.Add(automaticUpdates);
        privacyCard.Controls.Add(setup);
        privacyCard.Controls.Add(export);
        privacyCard.Controls.Add(import);
        privacyCard.Controls.Add(restore);
        privacyCard.Controls.Add(updates);
        privacyCard.Controls.Add(about);
        privacyCard.Controls.Add(profile);

        content.Controls.Add(startupCard);
        content.Controls.Add(feedbackCard);
        content.Controls.Add(routingCard);
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
        release.BackColor = StatusSurface("ready");
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
        panel.BackColor = cardBackground;
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
        b.BackColor = darkTheme ? surfaceBackground : Color.FromArgb(249, 249, 253);
        b.ForeColor = violet;
        b.FlatAppearance.MouseOverBackColor = darkTheme ? Color.FromArgb(47, 49, 57) : Color.FromArgb(238, 240, 255);
        b.FlatAppearance.MouseDownBackColor = darkTheme ? Color.FromArgb(53, 55, 64) : Color.FromArgb(226, 230, 250);
        return b;
    }

    private Button IconButton(string glyph, Point location, Size size, Color color, string tooltipText)
    {
        var button = SecondaryButton(glyph, location, size);
        button.Font = new Font("Segoe UI Symbol", 9f, FontStyle.Bold);
        button.ForeColor = color;
        button.AccessibleName = tooltipText;
        var tooltip = new ToolTip();
        tooltip.SetToolTip(button, tooltipText);
        button.Tag = tooltip;
        return button;
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
        c.BackColor = inputBackground;
        c.ForeColor = ink;
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
        t.BackColor = inputBackground;
        t.ForeColor = ink;
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
        if (!backgroundLaunch) ClampWindowToWorkingArea();
        HostLog("UI DPI awareness=per_monitor_v2 dpi=" + CurrentWindowDpi());
        if (uiSmokeMode) return;
        bool resumeIncompleteSetup = !config.setupCompleted && config.resumeSetupAfterRestart;
        if (backgroundLaunch && !resumeIncompleteSetup)
        {
            Hide();
            ShowInTaskbar = false;
        }
        if (!config.setupCompleted)
        {
            if (resumeIncompleteSetup)
            {
                ShowInTaskbar = true;
                Rectangle area = Screen.PrimaryScreen.WorkingArea;
                Location = new Point(area.Left + Math.Max(0, (area.Width - Width) / 2),
                    area.Top + Math.Max(0, (area.Height - Height) / 2));
                Show();
                Activate();
                HostLog("ONBOARDING RESUME after_restart=true step=" + config.onboardingStep);
                BeginInvoke(new Action(ShowSetupWizard));
            }
            else if (!backgroundLaunch) ShowSetupWizard();
            return;
        }
        if (config.launchAtStartup && !IsLaunchAtStartupRegistered())
        {
            SetLaunchAtStartup(true);
            HostLog("STARTUP REPAIRED=true reason=registry_entry_missing_or_stale");
        }
        // StartCapture owns bridge startup so login initializes the two services
        // once, in order, instead of starting the bridge and immediately probing
        // its still-unwritten health file a second time.
        if (config.startBridgeOnLaunch && !IsCapturing) StartCapture();
        else StartKeyboardBridge();
        WarmConfiguredProviderAsync(false);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (uiSmokeMode)
        {
            applicationExiting = true;
            if (visualTimer != null) { visualTimer.Stop(); visualTimer.Dispose(); visualTimer = null; }
            if (toastTimer != null) { toastTimer.Stop(); toastTimer.Dispose(); toastTimer = null; }
            base.OnFormClosing(e);
            return;
        }
        if (config.minimizeToTray && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        applicationExiting = true;
        ClearPendingCustomButtonCapture("application_exit");
        if (activityTimer != null) { activityTimer.Stop(); activityTimer.Dispose(); activityTimer = null; }
        if (visualTimer != null) { visualTimer.Stop(); visualTimer.Dispose(); visualTimer = null; }
        if (toastTimer != null) { toastTimer.Stop(); toastTimer.Dispose(); toastTimer = null; }
        if (systemRecoveryTimer != null) { systemRecoveryTimer.Stop(); systemRecoveryTimer.Dispose(); systemRecoveryTimer = null; }
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        StopCapture();
        StopKeyboardBridge();
        ReleaseHeldProviderHotkey("app_exit");
        if (audioDuckingLease != null) { audioDuckingLease.Dispose(); audioDuckingLease = null; }
        try { if (recordingStartCueEvent != null) recordingStartCueEvent.Set(); } catch { }
        try { if (recordingStopCueEvent != null) recordingStopCueEvent.Set(); } catch { }
        if (recordingCueThread != null) { try { recordingCueThread.Join(800); } catch { } recordingCueThread = null; }
        if (dictationCompletePlayer != null) { dictationCompletePlayer.Dispose(); dictationCompletePlayer = null; }
        if (dictationErrorPlayer != null) { dictationErrorPlayer.Dispose(); dictationErrorPlayer = null; }
        if (dictationStopPlayer != null) { dictationStopPlayer.Dispose(); dictationStopPlayer = null; }
        if (dictationCompleteSound != null) { dictationCompleteSound.Dispose(); dictationCompleteSound = null; }
        if (dictationErrorSound != null) { dictationErrorSound.Dispose(); dictationErrorSound = null; }
        if (dictationStopSound != null) { dictationStopSound.Dispose(); dictationStopSound = null; }
        try { if (showWindowEvent != null) { showWindowEvent.Set(); showWindowEvent.Dispose(); } } catch { }
        try { if (exitApplicationEvent != null) { exitApplicationEvent.Set(); exitApplicationEvent.Dispose(); } } catch { }
        try { if (voiceWakeRequestEvent != null) { voiceWakeRequestEvent.Set(); voiceWakeRequestEvent.Dispose(); } } catch { }
        try { if (providerHotkeyTapEvent != null) { providerHotkeyTapEvent.Set(); providerHotkeyTapEvent.Dispose(); } } catch { }
        try { if (providerHotkeyDownEvent != null) { providerHotkeyDownEvent.Set(); providerHotkeyDownEvent.Dispose(); } } catch { }
        try { if (providerHotkeyUpEvent != null) { providerHotkeyUpEvent.Set(); providerHotkeyUpEvent.Dispose(); } } catch { }
        try { if (inputTargetMissingEvent != null) { inputTargetMissingEvent.Set(); inputTargetMissingEvent.Dispose(); } } catch { }
        try { if (recordingStartCueEvent != null) { recordingStartCueEvent.Dispose(); recordingStartCueEvent = null; } } catch { }
        try { if (recordingStopCueEvent != null) { recordingStopCueEvent.Dispose(); recordingStopCueEvent = null; } } catch { }
        tray.Visible = false;
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        navigationFont.Dispose();
        navigationActiveFont.Dispose();
        connectionBadgeFont.Dispose();
        tray.Dispose();
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        if (!refreshSelfCheckOnActivate || currentPageIndex != PageSelfCheck || IsDisposed) return;
        refreshSelfCheckOnActivate = false;
        BeginInvoke(new Action(delegate
        {
            windowsHardwareProbeAt = DateTime.MinValue;
            ShowPage(PageSelfCheck);
            ShowToast("已根据 Windows 当前设置重新检测", "info");
        }));
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
        if (IsCapturing)
        {
            HostLog("CAPTURE START skipped=true reason=already_running pid=" + captureProcess.Id);
            return;
        }
        config = LoadConfig();
        captureStopping = false;
        bridgeReady = false;
        captureNotReadySince = DateTime.MinValue;
        captureHeartbeatUnhealthySince = DateTime.MinValue;
        string script = Path.Combine(root, "scripts", "remote-voice-capture.ps1");
        string nativeCapture = Path.Combine(root, "VibeMicAtvvCapture.exe");
        if (!File.Exists(nativeCapture) && !File.Exists(script)) { Toast("语音组件不完整，请重新安装言灵"); return; }
        try
        {
            // A host restart must not tear down a healthy ATVV session. Windows
            // can keep the old GATT handle reserved for tens of seconds after
            // a process exits, which makes an otherwise valid startup look
            // broken. Attach to the already-running session first.
            StartKeyboardBridge();
            if (TryAttachExistingCapture())
            {
                UpdateCaptureUi();
                return;
            }
            StopOrphanCaptureCore();
            // Windows may keep the previous Bluetooth GATT session alive for a
            // short period after the process exits. Starting immediately can
            // return AccessDenied for the new write characteristic.
            Thread.Sleep(CaptureRestartReleaseDelayMs);
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
                    SafeCaptureArgument(config.audioProcessingMode) + " " + config.autoRouteVirtualMicrophone + " " +
                    SafeCaptureArgument(config.voiceMode);
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
            HostLog("CAPTURE START pid=" + captureProcess.Id + " provider=" + NormalizeProviderKey(config.inputMethod) +
                " voice_mode=" + NormalizeVoiceMode(config.voiceMode));
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
        captureNotReadySince = DateTime.MinValue;
        captureHeartbeatUnhealthySince = DateTime.MinValue;
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
        finally { ReleaseHeldProviderHotkey("capture_stop"); }
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
        ReleaseHeldProviderHotkey("capture_exit");
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
        if (!captureStopping && audioDuckingLease != null)
        {
            bool protectedEarly = audioDuckingLease.Acquire("voice_wake_request");
            if (protectedEarly) audioDuckingLease.ReleaseAfter(35000, "voice_wake_timeout");
            HostLog("VOICE WAKE ducking_protected=" + protectedEarly + " phase=before_provider_session");
        }
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
            if (UsesLongDictation(config.voiceMode) && IsCapturing)
                ReplayLongDictationVoiceKeyAfterCaptureStart(captureProcess);
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

    private void HandleMissingInputTarget()
    {
        const string message = "请先点击目标应用的输入框，再按住录音键";
        HostLog("INPUT TARGET MISSING user_action=focus_editable_text_box");
        if (Visible && WindowState != FormWindowState.Minimized) ShowToast(message, "warning");
        else
        {
            tray.BalloonTipTitle = "言灵尚未开始听写";
            tray.BalloonTipText = message;
            tray.ShowBalloonTip(3500);
        }
    }

    private void HandleProviderHotkeyTapRequest()
    {
        if (applicationExiting) return;
        lock (providerHotkeySync)
        {
            VibeMicConfig current = LoadConfig();
            string shortcut = string.IsNullOrWhiteSpace(current.inputMethodHotkey)
                ? DefaultHotkeyForProvider(current.inputMethod) : current.inputMethodHotkey;
            var timer = System.Diagnostics.Stopwatch.StartNew();
            if (!string.IsNullOrWhiteSpace(heldProviderHotkey))
            {
                HostLog("PROVIDER HOTKEY TAP ignored=true reason=hold_active shortcut=" + SafeLogValue(heldProviderHotkey));
                return;
            }
            bool duckingProtected = audioDuckingLease == null || audioDuckingLease.Acquire("provider_hotkey_tap");
            bool down = SendProviderHotkeyState(shortcut, false);
            if (down) Thread.Sleep(IsCtrlWinShortcut(shortcut) ? 180 : 80);
            bool up = down && SendProviderHotkeyState(shortcut, true);
            if (!up) ReleaseVoiceHotkey();
            if (audioDuckingLease != null)
            {
                if (down) audioDuckingLease.ReleaseAfter(1200, "provider_hotkey_tap_complete");
                else audioDuckingLease.ReleaseNow("provider_hotkey_tap_failed");
            }
            timer.Stop();
            HostLog("PROVIDER HOTKEY TAP injection=" +
                (IsCtrlWinShortcut(shortcut) ? "keybd_event_vk_control" : "keybd_event_configured") +
                " shortcut=" + SafeLogValue(shortcut) +
                " down=" + down + " up=" + up + " ducking_protected=" + duckingProtected +
                " elapsed_ms=" + timer.ElapsedMilliseconds);
        }
    }

    private void HandleProviderHotkeyHoldRequest(bool keyDown)
    {
        if (applicationExiting && keyDown) return;
        lock (providerHotkeySync)
        {
            VibeMicConfig current = LoadConfig();
            string configured = string.IsNullOrWhiteSpace(current.inputMethodHotkey)
                ? DefaultHotkeyForProvider(current.inputMethod) : current.inputMethodHotkey;
            if (keyDown)
            {
                if (!string.IsNullOrWhiteSpace(heldProviderHotkey))
                {
                    HostLog("PROVIDER HOTKEY HOLD action=down sent=True duplicate=True shortcut=" +
                        SafeLogValue(heldProviderHotkey));
                    return;
                }
                bool duckingProtected = audioDuckingLease == null || audioDuckingLease.Acquire("provider_hotkey_hold");
                bool sent = SendProviderHotkeyState(configured, false);
                if (sent) heldProviderHotkey = configured;
                else if (audioDuckingLease != null) audioDuckingLease.ReleaseNow("provider_hotkey_down_failed");
                HostLog("PROVIDER HOTKEY HOLD action=down sent=" + sent + " duplicate=False shortcut=" +
                    SafeLogValue(configured) + " injection=" +
                    (IsCtrlWinShortcut(configured) ? "keybd_event_vk_control" : "keybd_event_configured") +
                    " ducking_protected=" + duckingProtected);
                return;
            }

            string releaseShortcut = string.IsNullOrWhiteSpace(heldProviderHotkey) ? configured : heldProviderHotkey;
            bool wasHeld = !string.IsNullOrWhiteSpace(heldProviderHotkey);
            bool released = SendProviderHotkeyState(releaseShortcut, true);
            heldProviderHotkey = null;
            if (audioDuckingLease != null)
                audioDuckingLease.ReleaseAfter(1200, "provider_hotkey_hold_complete");
            HostLog("PROVIDER HOTKEY HOLD action=up sent=" + released + " was_held=" + wasHeld +
                " shortcut=" + SafeLogValue(releaseShortcut) + " injection=" +
                (IsCtrlWinShortcut(releaseShortcut) ? "keybd_event_vk_control" : "keybd_event_configured"));
        }
    }

    private static bool SendProviderHotkeyState(string shortcut, bool keyUp)
    {
        if (!IsCtrlWinShortcut(shortcut)) return SendConfiguredHotkey(shortcut, keyUp);
        if (keyUp)
        {
            ReleaseVoiceHotkey();
            return true;
        }
        keybd_event(0x11, 0x1D, 0, UIntPtr.Zero);
        keybd_event(0x5B, 0x5B, 0, UIntPtr.Zero);
        return true;
    }

    private void ReleaseHeldProviderHotkey(string reason)
    {
        lock (providerHotkeySync)
        {
            if (string.IsNullOrWhiteSpace(heldProviderHotkey))
            {
                ReleaseVoiceHotkey();
                if (audioDuckingLease != null) audioDuckingLease.ReleaseNow(reason + "_without_held_key");
                return;
            }
            string shortcut = heldProviderHotkey;
            bool released = SendProviderHotkeyState(shortcut, true);
            heldProviderHotkey = null;
            if (audioDuckingLease != null) audioDuckingLease.ReleaseNow(reason);
            HostLog("PROVIDER HOTKEY HOLD action=release sent=" + released + " reason=" + reason +
                " shortcut=" + SafeLogValue(shortcut));
        }
    }

    private static bool IsCtrlWinShortcut(string shortcut)
    {
        string[] parts = (shortcut ?? "").Split(new char[] { '+', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        bool control = false;
        bool windows = false;
        foreach (string raw in parts)
        {
            string part = raw.Trim().ToLowerInvariant();
            if (part == "ctrl" || part == "control" || part == "leftctrl" || part == "lctrl") control = true;
            else if (part == "win" || part == "meta" || part == "leftwin" || part == "lwin") windows = true;
            else return false;
        }
        return parts.Length == 2 && control && windows;
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

    private void ReplayLongDictationVoiceKeyAfterCaptureStart(Process expectedCapture)
    {
        ThreadPool.QueueUserWorkItem(delegate
        {
            for (int attempt = 0; attempt < 100 && !applicationExiting; attempt++)
            {
                try
                {
                    if (expectedCapture == null || expectedCapture.HasExited || !ReferenceEquals(captureProcess, expectedCapture)) return;
                    using (EventWaitHandle handle = EventWaitHandle.OpenExisting("Local\\VibeMicVoiceKeyPressed"))
                    {
                        bool delivered = handle.Set();
                        HostLog("VOICE WAKE replay mode=push_to_talk delivered=" + delivered +
                            " waited_ms=" + (attempt * 25));
                        return;
                    }
                }
                catch (WaitHandleCannotBeOpenedException) { }
                catch (Exception ex)
                {
                    HostLog("VOICE WAKE replay failed mode=push_to_talk error=" + ex.Message);
                    return;
                }
                Thread.Sleep(25);
            }
            HostLog("VOICE WAKE replay failed mode=push_to_talk reason=capture_event_timeout");
        });
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
        string expected = Path.GetFullPath(Path.Combine(root, "VibeMicAtvvCapture.exe"));
        var ownedOrphans = new List<Process>();
        foreach (Process process in Process.GetProcessesByName("VibeMicAtvvCapture"))
        {
            try
            {
                string runningPath = Path.GetFullPath(process.MainModule.FileName);
                if (!process.HasExited && runningPath.Equals(expected, StringComparison.OrdinalIgnoreCase) &&
                    (captureProcess == null || process.Id != captureProcess.Id))
                {
                    ownedOrphans.Add(process);
                    continue;
                }
            }
            catch { }
            process.Dispose();
        }
        if (ownedOrphans.Count > 0) SignalEvent("Local\\VibeMicStopCapture");
        foreach (Process process in ownedOrphans)
        {
            try
            {
                if (!process.WaitForExit(3000))
                {
                    process.Kill();
                    process.WaitForExit(1500);
                }
            }
            catch { }
            finally { process.Dispose(); }
        }
    }

    private bool TryAttachExistingCapture()
    {
        string executable = Path.GetFullPath(Path.Combine(root, "VibeMicAtvvCapture.exe"));
        Process[] running = Process.GetProcessesByName("VibeMicAtvvCapture");
        try
        {
            foreach (Process process in running)
            {
                try
                {
                    if (process.HasExited) continue;
                    string runningPath = Path.GetFullPath(process.MainModule.FileName);
                    if (!runningPath.Equals(executable, StringComparison.OrdinalIgnoreCase)) continue;
                    Dictionary<string, object> captureHealth;
                    string captureHealthError;
                    if (!TryReadCaptureHeartbeat(process, out captureHealth, out captureHealthError))
                    {
                        HostLog("CAPTURE ATTACH rejected=true pid=" + process.Id +
                            " reason=" + (captureHealthError ?? "heartbeat_invalid"));
                        continue;
                    }

                    Process attached = process;
                    captureProcess = attached;
                    captureProcess.EnableRaisingEvents = true;
                    captureProcess.Exited += delegate
                    {
                        try { BeginInvoke(new Action(delegate { CaptureExited(attached); })); }
                        catch { }
                    };
                    captureStartedAt = attached.StartTime.ToLocalTime();
                    bridgeReady = RuntimeLogReadySince(captureStartedAt);
                    reconnectAttempt = 0;
                    HostLog("CAPTURE ATTACHED existing=true pid=" + attached.Id +
                        " atvv_ready=" + bridgeReady + " reason=preserve_gatt_session");
                    return true;
                }
                catch (Exception ex)
                {
                    HostLog("CAPTURE ATTACH skipped=true error=" + ex.GetType().Name);
                }
            }
        }
        finally
        {
            foreach (Process process in running)
            {
                if (!object.ReferenceEquals(process, captureProcess)) process.Dispose();
            }
        }
        return false;
    }

    private bool TryReadCaptureHeartbeat(Process process, out Dictionary<string, object> health, out string error)
    {
        health = null;
        error = null;
        if (process == null)
        {
            error = "process_missing";
            return false;
        }
        string path = Path.Combine(sessionDir, "capture-health.json");
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                if (!File.Exists(path))
                {
                    error = "heartbeat_missing";
                }
                else
                {
                    using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete))
                    using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                    {
                        health = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(reader.ReadToEnd());
                    }
                    if (health == null)
                    {
                        error = "heartbeat_empty";
                    }
                    else if ((DateTime.UtcNow - File.GetLastWriteTimeUtc(path)).TotalSeconds > 7)
                    {
                        error = "heartbeat_stale";
                    }
                    else
                    {
                        object value;
                        int pid = health.TryGetValue("pid", out value) ? Convert.ToInt32(value) : 0;
                        if (pid != process.Id) error = "heartbeat_pid_mismatch";
                        else
                        {
                            string state = health.ContainsKey("state") ? Convert.ToString(health["state"]) : "";
                            if (state.Equals("stopped", StringComparison.OrdinalIgnoreCase) ||
                                state.Equals("error", StringComparison.OrdinalIgnoreCase)) error = "heartbeat_state_" + state;
                            else
                            {
                                DateTime processStart = process.StartTime.ToUniversalTime();
                                DateTime recordedStart;
                                string recorded = health.ContainsKey("process_started_utc")
                                    ? Convert.ToString(health["process_started_utc"]) : "";
                                if (!DateTime.TryParse(recorded, CultureInfo.InvariantCulture,
                                    DateTimeStyles.RoundtripKind, out recordedStart))
                                    error = "heartbeat_start_missing";
                                else if (Math.Abs((recordedStart.ToUniversalTime() - processStart).TotalSeconds) > 3)
                                    error = "heartbeat_start_mismatch";
                                else return true;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { error = ex.GetType().Name; }
            if (attempt < 2) Thread.Sleep(25);
        }
        return false;
    }

    private bool RuntimeLogReadySince(DateTime startedAt)
    {
        try
        {
            string path = Path.Combine(sessionDir, "vibe-mic-runtime.log");
            if (!File.Exists(path)) return false;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete))
            {
                long length = stream.Length;
                long start = Math.Max(0, length - 128 * 1024);
                stream.Seek(start, SeekOrigin.Begin);
                using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.IndexOf("ATVV READY", StringComparison.OrdinalIgnoreCase) < 0 || line.Length < 23)
                            continue;
                        DateTime timestamp;
                        if (DateTime.TryParse(line.Substring(0, 23), CultureInfo.InvariantCulture,
                            DateTimeStyles.None, out timestamp) && timestamp >= startedAt.AddSeconds(-1))
                            return true;
                    }
                }
            }
        }
        catch { return false; }
        return false;
    }

    private bool StartKeyboardBridge()
    {
        if (uiSmokeMode)
        {
            HostLog("KEYBOARD BRIDGE skipped=true reason=ui_smoke");
            return true;
        }
        try
        {
            string expectedRevision = SyncKeyboardBridgeConfig();
            string executable = Path.GetFullPath(Path.Combine(root, "VoxDeckInputBridge.exe"));
            if (!File.Exists(executable)) { HostLog("KEYBOARD BRIDGE missing=true"); return false; }
            if (string.IsNullOrWhiteSpace(expectedRevision))
            {
                HostLog("KEYBOARD BRIDGE start_aborted=true reason=config_sync_failed");
                return false;
            }
            Process[] running = Process.GetProcessesByName("VoxDeckInputBridge");
            Process reusable = null;
            bool duplicateOwnedProcess = false;
            bool foreignProcess = false;
            foreach (Process process in running)
            {
                bool keep = false;
                try
                {
                    string runningPath = Path.GetFullPath(process.MainModule.FileName);
                    if (!process.HasExited && runningPath.Equals(executable, StringComparison.OrdinalIgnoreCase))
                    {
                        if (reusable == null)
                        {
                            reusable = process;
                            keep = true;
                        }
                        else duplicateOwnedProcess = true;
                    }
                    else foreignProcess = true;
                }
                catch { foreignProcess = true; }
                if (!keep) process.Dispose();
            }

            if (reusable != null && !duplicateOwnedProcess)
            {
                bool reusableStarting = false;
                try
                {
                    DateTime processStartedUtc = reusable.StartTime.ToUniversalTime();
                    reusableStarting = (DateTime.UtcNow - processStartedUtc).TotalSeconds < BridgeHealthStartupGraceSeconds;
                    keyboardBridgeStartedAt = processStartedUtc;
                }
                catch { reusableStarting = true; }

                SignalEvent("Local\\VibeMicReloadKeyboardConfig");
                if (WaitForBridgeConfigRevision(expectedRevision, reusableStarting ? 3000 : 1500, reusable.Id))
                {
                    keyboardBridgeProcess = reusable;
                    HostLog("KEYBOARD BRIDGE reused=true pid=" + reusable.Id +
                        " config_ack=" + expectedRevision);
                    return true;
                }
                if (reusableStarting)
                {
                    keyboardBridgeProcess = reusable;
                    HostLog("KEYBOARD BRIDGE reused=true pid=" + reusable.Id +
                        " reason=startup_grace config_ack=pending");
                    return false;
                }
                HostLog("KEYBOARD BRIDGE reuse_rejected=true reason=config_ack_timeout expected_revision=" + expectedRevision);
                reusable.Dispose();
                StopKeyboardBridge();
            }
            else if (reusable != null)
            {
                reusable.Dispose();
                HostLog("KEYBOARD BRIDGE duplicate_same_root=true action=stop_owned");
                StopKeyboardBridge();
            }

            if (foreignProcess)
            {
                HostLog("KEYBOARD BRIDGE root_conflict=true expected=" + executable + " action=manual_resolution_required");
                return false;
            }

            var start = new ProcessStartInfo(executable, "--background");
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.WindowStyle = ProcessWindowStyle.Hidden;
            keyboardBridgeProcess = Process.Start(start);
            keyboardBridgeStartedAt = DateTime.UtcNow;
            bool acknowledged = WaitForBridgeConfigRevision(expectedRevision, 3000, keyboardBridgeProcess.Id);
            HostLog("KEYBOARD BRIDGE started=true pid=" + keyboardBridgeProcess.Id +
                " config_ack=" + (acknowledged ? expectedRevision : "pending"));
            return acknowledged;
        }
        catch (Exception ex)
        {
            HostLog("KEYBOARD BRIDGE start_failed=true error=" + ex.Message);
            return false;
        }
    }

    private void RestartKeyboardBridge(string reason)
    {
        if (applicationExiting || (DateTime.UtcNow - lastKeyboardBridgeRecoveryAt).TotalSeconds < 10) return;
        lastKeyboardBridgeRecoveryAt = DateTime.UtcNow;
        HostLog("KEYBOARD BRIDGE recovery=start reason=" + reason);
        StopKeyboardBridge();
        StartKeyboardBridge();
    }

    private void StopKeyboardBridge()
    {
        try
        {
            string expected = Path.GetFullPath(Path.Combine(root, "VoxDeckInputBridge.exe"));
            var owned = new List<Process>();
            foreach (Process process in Process.GetProcessesByName("VoxDeckInputBridge"))
            {
                try
                {
                    string runningPath = Path.GetFullPath(process.MainModule.FileName);
                    if (!process.HasExited && runningPath.Equals(expected, StringComparison.OrdinalIgnoreCase))
                    {
                        owned.Add(process);
                        continue;
                    }
                }
                catch { }
                process.Dispose();
            }
            if (owned.Count > 0) SignalEvent("Local\\VibeMicStopKeyboardBridge");
            foreach (Process process in owned)
            {
                try { if (!process.WaitForExit(2500)) process.Kill(); }
                catch { }
                finally { process.Dispose(); }
            }
            keyboardBridgeProcess = null;
        }
        catch { }
    }

    private bool WaitForBridgeConfigRevision(string expectedRevision, int timeoutMilliseconds, int expectedProcessId)
    {
        if (string.IsNullOrWhiteSpace(expectedRevision)) return false;
        string path = Path.Combine(root, "input-bridge-health.json");
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(100, timeoutMilliseconds));
        do
        {
            Dictionary<string, object> health;
            string error;
            if (TryReadBridgeHealth(path, out health, out error) &&
                BridgeHealthAcknowledgesRevision(health, expectedRevision, expectedProcessId)) return true;
            Thread.Sleep(50);
        }
        while (DateTime.UtcNow < deadline);
        return false;
    }

    private static bool BridgeHealthAcknowledgesRevision(
        Dictionary<string, object> health, string expectedRevision, int expectedProcessId)
    {
        if (health == null || string.IsNullOrWhiteSpace(expectedRevision)) return false;
        object state;
        object hook;
        object rawInput;
        object revision;
        object configError;
        object processId;
        bool baseReady = health.TryGetValue("state", out state) &&
            string.Equals(Convert.ToString(state), "running", StringComparison.OrdinalIgnoreCase) &&
            health.TryGetValue("hook_installed", out hook) && Convert.ToBoolean(hook) &&
            health.TryGetValue("raw_input_registered", out rawInput) && Convert.ToBoolean(rawInput);
        bool revisionReady = health.TryGetValue("config_revision", out revision) &&
            string.Equals(Convert.ToString(revision), expectedRevision, StringComparison.OrdinalIgnoreCase);
        bool configReady = !health.TryGetValue("config_error", out configError) ||
            string.IsNullOrWhiteSpace(Convert.ToString(configError));
        bool processReady = expectedProcessId <= 0 ||
            (health.TryGetValue("pid", out processId) && Convert.ToInt32(processId) == expectedProcessId);
        return baseReady && revisionReady && configReady && processReady;
    }

    private void PollKeyboardBridgeHealth()
    {
        if (applicationExiting || !config.setupCompleted) return;
        string healthPath = Path.Combine(root, "input-bridge-health.json");
        ProcessTopologySnapshot topology = InspectProcessTopology("VoxDeckInputBridge");
        if (topology.CurrentRootCount == 0)
        {
            keyboardBridgeHealthUnhealthySince = DateTime.MinValue;
            if (topology.ForeignCount > 0 || topology.InaccessibleCount > 0)
            {
                if ((DateTime.UtcNow - lastKeyboardRootConflictLogAt).TotalSeconds >= 60)
                {
                    lastKeyboardRootConflictLogAt = DateTime.UtcNow;
                    HostLog("KEYBOARD BRIDGE recovery=blocked reason=foreign_root_process foreign=" +
                        topology.ForeignCount + " inaccessible=" + topology.InaccessibleCount);
                }
                return;
            }
            RestartKeyboardBridge("process_missing");
            return;
        }
        if (topology.CurrentRootCount > 1)
        {
            keyboardBridgeHealthUnhealthySince = DateTime.MinValue;
            RestartKeyboardBridge("duplicate_same_root");
            return;
        }
        if (keyboardBridgeStartedAt != DateTime.MinValue &&
            (DateTime.UtcNow - keyboardBridgeStartedAt).TotalSeconds < BridgeHealthStartupGraceSeconds) return;

        Dictionary<string, object> health;
        string readError;
        bool read = TryReadBridgeHealth(healthPath, out health, out readError);
        bool unhealthy = !read;
        object state = null;
        object hook = null;
        object rawInput = null;
        object rawDevice = null;
        object configRevision = null;
        object configError = null;
        if (read)
        {
            unhealthy = health == null ||
                !health.TryGetValue("state", out state) ||
                !string.Equals(Convert.ToString(state), "running", StringComparison.OrdinalIgnoreCase) ||
                !health.TryGetValue("hook_installed", out hook) || !Convert.ToBoolean(hook) ||
                !health.TryGetValue("raw_input_registered", out rawInput) || !Convert.ToBoolean(rawInput) ||
                (!string.IsNullOrWhiteSpace(expectedKeyboardConfigRevision) &&
                    (!health.TryGetValue("config_revision", out configRevision) ||
                    !string.Equals(Convert.ToString(configRevision), expectedKeyboardConfigRevision, StringComparison.OrdinalIgnoreCase))) ||
                (health.TryGetValue("config_error", out configError) &&
                    !string.IsNullOrWhiteSpace(Convert.ToString(configError)));
            health.TryGetValue("raw_input_device_present", out rawDevice);
        }
        if (!unhealthy)
        {
            keyboardBridgeHealthUnhealthySince = DateTime.MinValue;
            return;
        }

        DateTime now = DateTime.UtcNow;
        if (keyboardBridgeHealthUnhealthySince == DateTime.MinValue)
        {
            keyboardBridgeHealthUnhealthySince = now;
            HostLog("KEYBOARD BRIDGE health_degraded=true error=" + (readError ?? "invalid heartbeat") +
                " state=" + (health == null ? "missing" : Convert.ToString(state)) +
                " hook=" + (health != null && health.TryGetValue("hook_installed", out hook) ? Convert.ToString(hook) : "missing") +
                " raw_input=" + (health != null && health.TryGetValue("raw_input_registered", out rawInput) ? Convert.ToString(rawInput) : "missing") +
                " raw_device=" + (health != null && health.TryGetValue("raw_input_device_present", out rawDevice) ? Convert.ToString(rawDevice) : "missing") +
                " config_revision=" + (health != null && health.TryGetValue("config_revision", out configRevision) ? Convert.ToString(configRevision) : "missing") +
                " expected_revision=" + expectedKeyboardConfigRevision +
                " config_error=" + (health != null && health.TryGetValue("config_error", out configError) ? Convert.ToString(configError) : "missing"));
        }
        if ((now - keyboardBridgeHealthUnhealthySince).TotalSeconds >= BridgeHealthFailureRecoverySeconds)
        {
            HostLog("KEYBOARD BRIDGE health_invalid=true duration_s=" +
                (now - keyboardBridgeHealthUnhealthySince).TotalSeconds.ToString("0") +
                " action=recover");
            keyboardBridgeHealthUnhealthySince = DateTime.MinValue;
            RestartKeyboardBridge("heartbeat_stale");
        }
    }

    private static bool TryReadBridgeHealth(string path, out Dictionary<string, object> health, out string error)
    {
        health = null;
        error = null;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                if (!File.Exists(path))
                {
                    error = "heartbeat_missing";
                }
                else
                {
                    using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete))
                    using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                    {
                        health = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(reader.ReadToEnd());
                    }
                    if (health != null)
                    {
                        DateTime modified = File.GetLastWriteTimeUtc(path);
                        if ((DateTime.UtcNow - modified).TotalSeconds <= 7) return true;
                        error = "heartbeat_stale";
                    }
                    else error = "heartbeat_empty";
                }
            }
            catch (Exception ex) { error = ex.Message; }
            if (attempt < 2) Thread.Sleep(25);
        }
        return false;
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
        Toast("已测试 " + ProviderDisplayName(provider) + " 快捷键 " + shortcut.Replace("+", " + ") +
            (hold ? "（按住触发）" : "（切换触发，测试会自动结束）"));
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
                wizard.Text = "首次设置 · " + DisplayProductName;
                wizard.ClientSize = new Size(1000, 680);
                wizard.FormBorderStyle = FormBorderStyle.FixedDialog;
                wizard.MaximizeBox = false;
                wizard.MinimizeBox = false;
                wizard.StartPosition = FormStartPosition.CenterParent;
                wizard.BackColor = pageBackground;
                wizard.Font = Font;
                wizard.Icon = Icon;

                var rail = new Panel();
                rail.Dock = DockStyle.Left;
                rail.Width = 224;
                rail.BackColor = sidebarBackground;
                rail.Paint += delegate(object sender, PaintEventArgs e)
                {
                    using (var progress = new Pen(line, 2f)) e.Graphics.DrawLine(progress, 37, 132, 37, 420);
                    using (var border = new Pen(line)) e.Graphics.DrawLine(border, rail.Width - 1, 0, rail.Width - 1, rail.Height);
                };
                var setupLogo = new PictureBox();
                setupLogo.Image = LoadBrandLogo();
                setupLogo.SizeMode = PictureBoxSizeMode.Zoom;
                setupLogo.BackColor = Color.Transparent;
                setupLogo.Location = new Point(24, 22);
                setupLogo.Size = new Size(42, 42);
                var railBrand = NewLabel("言灵", 17f, FontStyle.Bold, ink);
                railBrand.Location = new Point(78, 21);
                railBrand.AutoSize = true;
                var railEnglish = NewLabel("VIBE FLOW · V" + ProductRelease, 7.1f, FontStyle.Bold, violet);
                railEnglish.Location = new Point(79, 52);
                railEnglish.AutoSize = true;
                rail.Controls.Add(setupLogo);
                rail.Controls.Add(railBrand);
                rail.Controls.Add(railEnglish);
                wizard.Disposed += delegate { if (setupLogo.Image != null) setupLogo.Image.Dispose(); };

                string[] stepNames =
                {
                    "确认设备与用法", "连接并测试遥控器", "准备本地音频通道",
                    "选择工具并完成听写", "开机即用"
                };
                var numberLabels = new Label[OnboardingStepCount];
                var progressLabels = new Label[OnboardingStepCount];
                for (int i = 0; i < OnboardingStepCount; i++)
                {
                    var number = NewLabel((i + 1).ToString(), 8.4f, FontStyle.Bold, muted);
                    number.Location = new Point(25, 118 + i * 72);
                    number.Size = new Size(26, 26);
                    number.TextAlign = ContentAlignment.MiddleCenter;
                    number.BackColor = darkTheme ? surfaceBackground : Color.FromArgb(237, 240, 248);
                    ApplyRoundedRegion(number, 13);
                    var stepLabel = NewLabel(stepNames[i], 9f, FontStyle.Regular, muted);
                    stepLabel.Location = new Point(64, 119 + i * 72);
                    stepLabel.Size = new Size(146, 28);
                    numberLabels[i] = number;
                    progressLabels[i] = stepLabel;
                    rail.Controls.Add(number);
                    rail.Controls.Add(stepLabel);
                }
                var privacyRail = NewLabel("本地传输 · 不保存录音\r\n不读取或记录转译文字", 8.2f, FontStyle.Regular, muted);
                privacyRail.Location = new Point(26, 586);
                privacyRail.Size = new Size(180, 48);
                rail.Controls.Add(privacyRail);

                var body = new Panel();
                body.Dock = DockStyle.Fill;
                body.BackColor = pageBackground;
                var pageContent = new Panel();
                pageContent.Location = new Point(34, 24);
                pageContent.Size = new Size(708, 572);
                pageContent.BackColor = Color.Transparent;
                var back = SecondaryButton("上一步", new Point(34, 616), new Size(112, 42));
                var next = PrimaryButton("完成本步，继续", new Point(554, 616), new Size(188, 42));
                var stepCounter = NewLabel("任务 1 / 5", 8.8f, FontStyle.Bold, violet);
                stepCounter.Location = new Point(164, 626);
                stepCounter.Size = new Size(110, 24);
                var wizardFeedback = NewLabel("", 8.8f, FontStyle.Bold, muted);
                wizardFeedback.Location = new Point(280, 622);
                wizardFeedback.Size = new Size(260, 30);
                wizardFeedback.TextAlign = ContentAlignment.MiddleCenter;
                body.Controls.Add(pageContent);
                body.Controls.Add(back);
                body.Controls.Add(next);
                body.Controls.Add(stepCounter);
                body.Controls.Add(wizardFeedback);
                wizard.Controls.Add(body);
                wizard.Controls.Add(rail);

                int currentStep = config.setupCompleted ? 0 : Math.Max(0, Math.Min(OnboardingStepCount - 1, config.onboardingStep));
                string selectedProvider = NormalizeProviderKey(config.inputMethod);
                string selectedHotkey = config.inputMethodHotkey;
                string selectedTrigger = config.inputMethodTrigger;
                bool startupChoice = config.launchAtStartup;
                bool bridgeChoice = true;
                bool trayChoice = true;
                DateTime keyBaselineUtc = currentStep == 1 ? DateTime.UtcNow : DateTime.MinValue;
                int dictationBaselineGeneration = currentStep == 3 ? GetLatestSessionHealth().Generation : 0;
                bool textInsertionConfirmed = false;
                int confirmedTextLength = 0;
                ComboBox providerChoice = null;
                ComboBox hotkeyTrigger = null;
                TextBox hotkeyBox = null;
                TextBox testInput = null;
                Action<int> renderStep = null;

                Action persistProgress = delegate
                {
                    config.voiceMode = "hold";
                    config.inputMethod = NormalizeProviderKey(selectedProvider);
                    config.inputMethodHotkey = selectedHotkey;
                    config.inputMethodTrigger = selectedTrigger == "hold" ? "hold" : "toggle";
                    config.onboardingVersion = CurrentOnboardingVersion;
                    config.onboardingStep = currentStep;
                    SaveConfig();
                };
                Action<string, bool> showFeedback = delegate(string message, bool success)
                {
                    wizardFeedback.Text = message;
                    wizardFeedback.ForeColor = success ? green : coral;
                };

                renderStep = delegate(int requestedStep)
                {
                    int previousStep = currentStep;
                    currentStep = Math.Max(0, Math.Min(OnboardingStepCount - 1, requestedStep));
                    if (currentStep == 1 && previousStep != 1) keyBaselineUtc = DateTime.UtcNow;
                    if (currentStep == 3 && previousStep != 3)
                    {
                        dictationBaselineGeneration = GetLatestSessionHealth().Generation;
                        textInsertionConfirmed = false;
                        confirmedTextLength = 0;
                    }
                    persistProgress();
                    while (pageContent.Controls.Count > 0) pageContent.Controls[0].Dispose();
                    providerChoice = null;
                    hotkeyTrigger = null;
                    hotkeyBox = null;
                    testInput = null;
                    wizardFeedback.Text = "";

                    for (int i = 0; i < OnboardingStepCount; i++)
                    {
                        bool completed = i < currentStep;
                        numberLabels[i].Text = completed ? "✓" : (i + 1).ToString();
                        numberLabels[i].ForeColor = i <= currentStep ? Color.White : muted;
                        numberLabels[i].BackColor = completed ? green : i == currentStep ? violet :
                            (darkTheme ? surfaceBackground : Color.FromArgb(237, 240, 248));
                        progressLabels[i].ForeColor = i == currentStep ? violet : completed ? green : muted;
                        progressLabels[i].Font = new Font("Microsoft YaHei UI", 9f,
                            i == currentStep ? FontStyle.Bold : FontStyle.Regular);
                    }
                    stepCounter.Text = "任务 " + (currentStep + 1) + " / " + OnboardingStepCount;
                    back.Enabled = currentStep > 0;
                    next.Text = currentStep == OnboardingStepCount - 1 ? "完成设置" : "完成本步，继续";

                    string subtitleText = currentStep == 0 ? "先确认设备和固定操作方式，整个设置通常只需几分钟。" :
                        currentStep == 1 ? "配对 RC003，并用一个真实方向键证明 Windows 已收到遥控器事件。" :
                        currentStep == 2 ? "检查 VB-CABLE 和遥控器麦克风，让声音稳定进入本地语音工具。" :
                        currentStep == 3 ? "选择常用语音工具，核对快捷键，并完成一次真实文字回填。" :
                        "保存后台与开机设置。以后登录 Windows 后拿起遥控器即可使用。";
                    var heading = NewLabel(stepNames[currentStep], 20f, FontStyle.Bold, ink);
                    heading.Location = new Point(0, 4);
                    heading.Size = new Size(690, 38);
                    var subtitle = NewLabel(subtitleText, 9.5f, FontStyle.Regular, muted);
                    subtitle.Location = new Point(2, 47);
                    subtitle.Size = new Size(690, 44);
                    pageContent.Controls.Add(heading);
                    pageContent.Controls.Add(subtitle);

                    if (currentStep == 0)
                    {
                        string[,] flow =
                        {
                            { "1", "按住录音键", "开始收音，只创建一个会话" },
                            { "2", "持续自然说话", "单次最长 60 秒" },
                            { "3", "松开录音键", "结束并等待语音工具转译" },
                            { "4", "检查文字后按确认键", "由你决定何时发送" }
                        };
                        for (int i = 0; i < 4; i++)
                        {
                            int y = 112 + i * 82;
                            var number = NewLabel(flow[i, 0], 9.5f, FontStyle.Bold, Color.White);
                            number.Location = new Point(8, y + 5);
                            number.Size = new Size(32, 32);
                            number.TextAlign = ContentAlignment.MiddleCenter;
                            number.BackColor = i == 0 ? violet : i == 1 ? cyan : i == 2 ? green : amber;
                            ApplyRoundedRegion(number, 16);
                            var title = NewLabel(flow[i, 1], 10.5f, FontStyle.Bold, ink);
                            title.Location = new Point(58, y);
                            title.Size = new Size(250, 27);
                            var detail = NewLabel(flow[i, 2], 8.9f, FontStyle.Regular, muted);
                            detail.Location = new Point(58, y + 30);
                            detail.Size = new Size(540, 26);
                            pageContent.Controls.Add(number);
                            pageContent.Controls.Add(title);
                            pageContent.Controls.Add(detail);
                        }
                        var device = NewLabel("适用：Windows 10 / 11 · 小米蓝牙语音遥控器 Pro 2 / RC003", 9f, FontStyle.Bold, violet);
                        device.Location = new Point(8, 458);
                        device.Size = new Size(650, 30);
                        pageContent.Controls.Add(device);
                    }
                    else if (currentStep == 1)
                    {
                        BridgeHealthSnapshot snapshot = ReadKeyboardBridgeHealth();
                        bool remoteReady = bridgeReady || snapshot.RawInputDevicePresent;
                        bool keyObserved = snapshot.LastInputAtUtc > keyBaselineUtc &&
                            !string.Equals(snapshot.LastInputKind, "keyboard_hook", StringComparison.OrdinalIgnoreCase);
                        var status = NewLabel(remoteReady ? "●  RC003 已连接" : "●  尚未识别 RC003", 13f, FontStyle.Bold,
                            remoteReady ? green : amber);
                        status.Location = new Point(8, 112);
                        status.Size = new Size(600, 36);
                        var keyState = NewLabel(keyObserved ? "✓ 已收到刚才的 RC003 设备事件" : "现在按一次遥控器方向键，然后点击重新检测",
                            9.4f, FontStyle.Bold, keyObserved ? green : cyan);
                        keyState.Location = new Point(8, 158);
                        keyState.Size = new Size(650, 34);
                        var detail = NewLabel(remoteReady ?
                            "按键桥接会在遥控器休眠、蓝牙晚启动或电脑唤醒后自动恢复。" :
                            "打开 Windows 蓝牙设置，添加 RC003；完成后按方向键唤醒遥控器。", 9f, FontStyle.Regular, muted);
                        detail.Location = new Point(8, 204);
                        detail.Size = new Size(660, 48);
                        var pair = PrimaryButton("打开蓝牙设置", new Point(8, 276), new Size(160, 42));
                        pair.Click += delegate { OpenUri("ms-settings:bluetooth"); };
                        var connect = SecondaryButton(IsCapturing ? "重新检测" : "启动连接", new Point(182, 276), new Size(132, 42));
                        connect.Click += delegate { if (!IsCapturing) StartCapture(); renderStep(1); };
                        var repair = SecondaryButton("重建按键监听", new Point(328, 276), new Size(154, 42));
                        repair.Click += delegate { RestartKeyboardBridge("onboarding_key_check"); keyBaselineUtc = DateTime.UtcNow; renderStep(1); };
                        var note = NewLabel("正确状态：RC003 已连接，并且重新检测后显示“已收到刚才的 RC003 设备事件”。普通键盘不能完成此验证。", 8.9f, FontStyle.Regular, muted);
                        note.Location = new Point(8, 354);
                        note.Size = new Size(660, 38);
                        pageContent.Controls.Add(status);
                        pageContent.Controls.Add(keyState);
                        pageContent.Controls.Add(detail);
                        pageContent.Controls.Add(pair);
                        pageContent.Controls.Add(connect);
                        pageContent.Controls.Add(repair);
                        pageContent.Controls.Add(note);
                    }
                    else if (currentStep == 2)
                    {
                        bool inputReady = HasCableInput();
                        bool outputReady = HasCableOutput();
                        string runtime = ReadCurrentRuntimeSegment();
                        bool microphoneReady = bridgeReady || runtime.IndexOf("ATVV READY", StringComparison.OrdinalIgnoreCase) >= 0;
                        var status = NewLabel(inputReady && outputReady ? "●  本地音频通道已就绪" : "●  需要安装或启用 VB-CABLE",
                            13f, FontStyle.Bold, inputReady && outputReady ? green : amber);
                        status.Location = new Point(8, 108);
                        status.Size = new Size(620, 36);
                        var route = NewCard(new Point(8, 160), new Size(680, 188));
                        var routeTitle = NewLabel("RC003  →  CABLE Input  →  CABLE Output  →  语音工具", 10f, FontStyle.Bold, ink);
                        routeTitle.Location = new Point(20, 18);
                        routeTitle.Size = new Size(630, 28);
                        var inputState = NewLabel((inputReady ? "✓" : "!") + "  CABLE Input（播放端）" + (inputReady ? " 已检测" : " 未检测"),
                            9.3f, FontStyle.Bold, inputReady ? green : coral);
                        inputState.Location = new Point(20, 62);
                        inputState.Size = new Size(360, 28);
                        var outputState = NewLabel((outputReady ? "✓" : "!") + "  CABLE Output（录音端）" + (outputReady ? " 已检测" : " 未检测"),
                            9.3f, FontStyle.Bold, outputReady ? green : coral);
                        outputState.Location = new Point(20, 98);
                        outputState.Size = new Size(360, 28);
                        var microphoneState = NewLabel((microphoneReady ? "✓" : "!") + "  RC003 麦克风服务" + (microphoneReady ? " 已就绪" : " 等待连接"),
                            9.3f, FontStyle.Bold, microphoneReady ? green : amber);
                        microphoneState.Location = new Point(20, 134);
                        microphoneState.Size = new Size(360, 28);
                        route.Controls.Add(routeTitle);
                        route.Controls.Add(inputState);
                        route.Controls.Add(outputState);
                        route.Controls.Add(microphoneState);
                        var install = PrimaryButton(inputReady && outputReady ? "打开声音设置" : "安装官方 VB-CABLE", new Point(8, 374), new Size(190, 42));
                        install.Click += delegate
                        {
                            if (inputReady && outputReady) OpenUri("ms-settings:sound");
                            else
                            {
                                config.onboardingStep = 2;
                                config.resumeSetupAfterRestart = true;
                                SaveConfig();
                                SetLaunchAtStartup(true);
                                LaunchVBCableInstaller();
                                showFeedback("安装后如需重启，将自动继续", true);
                            }
                        };
                        var recheck = SecondaryButton("重新检测", new Point(212, 374), new Size(124, 42));
                        recheck.Click += delegate { renderStep(2); };
                        var permission = SecondaryButton("麦克风权限", new Point(350, 374), new Size(130, 42));
                        permission.Click += delegate { OpenUri("ms-settings:privacy-microphone"); };
                        var note = NewLabel("VB-CABLE 只在本机传递声音，不上传录音。安装后若要求重启，本向导会回到当前任务。",
                            8.8f, FontStyle.Regular, muted);
                        note.Location = new Point(8, 448);
                        note.Size = new Size(670, 42);
                        pageContent.Controls.Add(status);
                        pageContent.Controls.Add(route);
                        pageContent.Controls.Add(install);
                        pageContent.Controls.Add(recheck);
                        pageContent.Controls.Add(permission);
                        pageContent.Controls.Add(note);
                    }
                    else if (currentStep == 3)
                    {
                        SessionHealth health = GetLatestSessionHealth();
                        bool audioSubmissionSucceeded = health.Generation > dictationBaselineGeneration && health.Success;
                        bool dictationSucceeded = audioSubmissionSucceeded && textInsertionConfirmed;
                        var providerLabel = NewLabel("默认语音工具", 8.9f, FontStyle.Bold, ink);
                        providerLabel.Location = new Point(8, 104);
                        providerLabel.Size = new Size(140, 24);
                        providerChoice = StyledCombo(new Point(8, 132), new Size(238, 40));
                        providerChoice.Items.AddRange(new object[] { "微信输入法", "Typeless", "豆包输入法", "Windows 语音输入", "其他自定义工具" });
                        providerChoice.SelectedIndex = ProviderIndex(selectedProvider);
                        var shortcutLabel = NewLabel("全局快捷键", 8.9f, FontStyle.Bold, ink);
                        shortcutLabel.Location = new Point(264, 104);
                        shortcutLabel.Size = new Size(120, 24);
                        hotkeyBox = StyledTextBox(selectedHotkey, new Point(264, 132), new Size(202, 36));
                        hotkeyTrigger = StyledCombo(new Point(484, 130), new Size(194, 40));
                        PopulateTriggerModeOptions(hotkeyTrigger, selectedProvider);
                        hotkeyTrigger.SelectedIndex = NormalizeProviderKey(selectedProvider) == "wechat" ? 0 :
                            selectedTrigger == "hold" ? 1 : 0;
                        string testState = dictationSucceeded ? "●  已确认文字进入测试框（" + confirmedTextLength + " 字）" :
                            audioSubmissionSucceeded ? "●  音频与工具唤起已通过，请确认文字" : "●  等待一次真实听写";
                        var status = NewLabel(testState, 11.5f, FontStyle.Bold, dictationSucceeded ? green : cyan);
                        status.Location = new Point(8, 188);
                        status.Size = new Size(620, 30);
                        testInput = StyledTextBox("", new Point(8, 228), new Size(670, 112));
                        testInput.Multiline = true;
                        testInput.Font = new Font("Microsoft YaHei UI", 10.5f);
                        testInput.TextChanged += delegate
                        {
                            string observedText = testInput.Text.Trim();
                            if (observedText.Length == 0) return;
                            textInsertionConfirmed = true;
                            confirmedTextLength = observedText.Length;
                            status.Text = "●  已确认文字进入测试框（" + confirmedTextLength + " 字）";
                            status.ForeColor = green;
                        };
                        var focus = PrimaryButton(IsCapturing ? "聚焦输入框并测试" : "启动桥接并测试", new Point(8, 360), new Size(188, 42));
                        focus.Click += delegate
                        {
                            selectedHotkey = hotkeyBox.Text.Trim();
                            selectedTrigger = hotkeyTrigger.SelectedIndex == 1 ? "hold" : "toggle";
                            if (!IsValidTranscriptionHotkey(selectedHotkey)) { showFeedback("快捷键格式无效", false); return; }
                            if (!uiSmokeMode) SaveWizardProviderConfig(selectedProvider, selectedHotkey, selectedTrigger, true);
                            if (!IsCapturing && !uiSmokeMode) StartCapture();
                            testInput.Focus();
                            showFeedback("现在按住录音键说话，松开后等待文字", true);
                        };
                        var testHotkey = SecondaryButton("测试工具快捷键", new Point(210, 360), new Size(156, 42));
                        testHotkey.Click += delegate
                        {
                            selectedHotkey = hotkeyBox.Text.Trim();
                            selectedTrigger = hotkeyTrigger.SelectedIndex == 1 ? "hold" : "toggle";
                            if (!IsValidTranscriptionHotkey(selectedHotkey)) { showFeedback("快捷键格式无效", false); return; }
                            if (!uiSmokeMode) SaveWizardProviderConfig(selectedProvider, selectedHotkey, selectedTrigger, true);
                            TestVoiceHotkey();
                            showFeedback("已发送工具快捷键", true);
                        };
                        var recheck = SecondaryButton("检查结果", new Point(380, 360), new Size(120, 42));
                        recheck.Click += delegate { renderStep(3); };
                        var help = SecondaryButton("配置帮助", new Point(514, 360), new Size(118, 42));
                        help.Click += delegate { OpenProviderHelp(selectedProvider); };
                        var instruction = NewLabel(ProviderSetupInstruction(selectedProvider), 8.7f, FontStyle.Regular, muted);
                        instruction.Location = new Point(8, 426);
                        instruction.Size = new Size(670, 54);
                        providerChoice.SelectedIndexChanged += delegate
                        {
                            string nextProvider = ProviderKeyFromIndex(providerChoice.SelectedIndex);
                            if (nextProvider == selectedProvider) return;
                            selectedProvider = nextProvider;
                            selectedHotkey = DefaultHotkeyForProvider(selectedProvider);
                            selectedTrigger = DefaultTriggerForProvider(selectedProvider);
                            renderStep(3);
                        };
                        hotkeyBox.TextChanged += delegate { selectedHotkey = hotkeyBox.Text.Trim(); };
                        hotkeyTrigger.SelectedIndexChanged += delegate { selectedTrigger = hotkeyTrigger.SelectedIndex == 1 ? "hold" : "toggle"; };
                        pageContent.Controls.Add(providerLabel);
                        pageContent.Controls.Add(providerChoice);
                        pageContent.Controls.Add(shortcutLabel);
                        pageContent.Controls.Add(hotkeyBox);
                        pageContent.Controls.Add(hotkeyTrigger);
                        pageContent.Controls.Add(status);
                        pageContent.Controls.Add(testInput);
                        pageContent.Controls.Add(focus);
                        pageContent.Controls.Add(testHotkey);
                        pageContent.Controls.Add(recheck);
                        pageContent.Controls.Add(help);
                        pageContent.Controls.Add(instruction);
                    }
                    else
                    {
                        var startup = StyledCheck("登录 Windows 后自动启动言灵（推荐）", startupChoice, new Point(8, 108));
                        startup.Size = new Size(520, 38);
                        var bridge = StyledCheck("启动后自动连接遥控器与语音桥接", bridgeChoice, new Point(8, 156));
                        bridge.Size = new Size(520, 38);
                        var tray = StyledCheck("关闭主窗口后继续在系统托盘运行", trayChoice, new Point(8, 204));
                        tray.Size = new Size(520, 38);
                        startup.CheckedChanged += delegate { startupChoice = startup.Checked; };
                        bridge.CheckedChanged += delegate { bridgeChoice = bridge.Checked; };
                        tray.CheckedChanged += delegate { trayChoice = tray.Checked; };
                        SelfCheckReport report = BuildSelfCheckReport();
                        bool coreReady = report.FailedCount == 0;
                        var summary = NewCard(new Point(8, 270), new Size(680, 158));
                        var summaryTitle = NewLabel(coreReady ? "●  核心链路已准备好" : "●  仍有项目需要处理", 13f, FontStyle.Bold,
                            coreReady ? green : amber);
                        summaryTitle.Location = new Point(22, 18);
                        summaryTitle.Size = new Size(620, 34);
                        var summaryText = NewLabel("语音工具：" + ProviderDisplayName(selectedProvider) +
                            "\r\n快捷键：方向键保持导航；可在完成后进入“快捷键”配置 APP、网页或截图。",
                            9f, FontStyle.Regular, muted);
                        summaryText.Location = new Point(22, 58);
                        summaryText.Size = new Size(620, 58);
                        var shortcuts = SecondaryButton("配置快捷键", new Point(22, 114), new Size(128, 34));
                        shortcuts.Click += delegate
                        {
                            persistProgress();
                            wizard.Close();
                            BeginInvoke(new Action(delegate { ShowPage(PageShortcuts); }));
                        };
                        var diagnostics = SecondaryButton("打开完整自检", new Point(164, 114), new Size(142, 34));
                        diagnostics.Click += delegate
                        {
                            persistProgress();
                            wizard.Close();
                            BeginInvoke(new Action(delegate { ShowPage(PageSelfCheck); }));
                        };
                        summary.Controls.Add(summaryTitle);
                        summary.Controls.Add(summaryText);
                        summary.Controls.Add(shortcuts);
                        summary.Controls.Add(diagnostics);
                        pageContent.Controls.Add(startup);
                        pageContent.Controls.Add(bridge);
                        pageContent.Controls.Add(tray);
                        pageContent.Controls.Add(summary);
                    }
                };

                back.Click += delegate { renderStep(currentStep - 1); };
                next.Click += delegate
                {
                    if (currentStep == 1)
                    {
                        BridgeHealthSnapshot snapshot = ReadKeyboardBridgeHealth();
                        if (!uiSmokeMode && (!snapshot.RawInputDevicePresent || snapshot.LastInputAtUtc <= keyBaselineUtc ||
                            string.Equals(snapshot.LastInputKind, "keyboard_hook", StringComparison.OrdinalIgnoreCase)))
                        {
                            showFeedback(!snapshot.RawInputDevicePresent ? "请先连接并唤醒 RC003" : "还没有收到刚才的方向键", false);
                            return;
                        }
                    }
                    if (currentStep == 2 && !uiSmokeMode && (!HasCableInput() || !HasCableOutput()))
                    {
                        showFeedback("请先安装并检测到 VB-CABLE", false);
                        return;
                    }
                    if (currentStep == 3)
                    {
                        if (hotkeyBox != null) selectedHotkey = hotkeyBox.Text.Trim();
                        if (hotkeyTrigger != null) selectedTrigger = hotkeyTrigger.SelectedIndex == 1 ? "hold" : "toggle";
                        if (!IsValidTranscriptionHotkey(selectedHotkey))
                        {
                            showFeedback("请填写有效的全局快捷键", false);
                            return;
                        }
                        if (!uiSmokeMode)
                        {
                            SaveWizardProviderConfig(selectedProvider, selectedHotkey, selectedTrigger, true);
                            SessionHealth health = GetLatestSessionHealth();
                            if (health.Generation <= dictationBaselineGeneration || !health.Success)
                            {
                                showFeedback("音频与语音工具唤起尚未通过，请重新测试", false);
                                return;
                            }
                            if (!textInsertionConfirmed)
                            {
                                showFeedback("请确认转译文字已进入上方测试框", false);
                                return;
                            }
                        }
                    }
                    if (currentStep < OnboardingStepCount - 1)
                    {
                        renderStep(currentStep + 1);
                        return;
                    }

                    config.voiceMode = "hold";
                    config.setupCompleted = true;
                    config.onboardingVersion = CurrentOnboardingVersion;
                    config.onboardingStep = OnboardingStepCount - 1;
                    config.resumeSetupAfterRestart = false;
                    config.launchAtStartup = startupChoice;
                    config.startBridgeOnLaunch = bridgeChoice;
                    config.minimizeToTray = trayChoice;
                    ApplyStableVoiceProfile(config);
                    if (!uiSmokeMode) SetLaunchAtStartup(startupChoice);
                    SaveConfig();
                    if (!uiSmokeMode && config.startBridgeOnLaunch && !IsCapturing) StartCapture();
                    wizard.DialogResult = DialogResult.OK;
                    wizard.Close();
                    ShowToast("设置完成，言灵已经可以使用", "success");
                    ShowPage(PageHome);
                };

                wizard.FormClosing += delegate { if (!config.setupCompleted) persistProgress(); };
                renderStep(currentStep);
                wizard.ShowDialog(this);
            }
        }
        finally { setupWizardOpen = false; }
    }

    private void ShowSetupWizardElevenStepLegacy()
    {
        if (setupWizardOpen) return;
        setupWizardOpen = true;
        try
        {
            using (var wizard = new Form())
            {
                wizard.Text = "首次设置 · " + DisplayProductName;
                wizard.ClientSize = new Size(1040, 720);
                wizard.FormBorderStyle = FormBorderStyle.FixedDialog;
                wizard.MaximizeBox = false;
                wizard.MinimizeBox = false;
                wizard.StartPosition = FormStartPosition.CenterParent;
                wizard.BackColor = pageBackground;
                wizard.Font = Font;
                wizard.Icon = Icon;

                var rail = new Panel();
                rail.Dock = DockStyle.Left;
                rail.Width = 250;
                rail.BackColor = sidebarBackground;
                rail.Paint += delegate(object sender, PaintEventArgs e)
                {
                    using (var progress = new Pen(Color.FromArgb(218, 224, 239), 2f))
                        e.Graphics.DrawLine(progress, 39, 118, 39, 572);
                    using (var border = new Pen(line))
                        e.Graphics.DrawLine(border, rail.Width - 1, 0, rail.Width - 1, rail.Height);
                };
                var setupLogo = new PictureBox();
                setupLogo.Image = LoadBrandLogo();
                setupLogo.SizeMode = PictureBoxSizeMode.Zoom;
                setupLogo.BackColor = Color.Transparent;
                setupLogo.Location = new Point(24, 22);
                setupLogo.Size = new Size(42, 42);
                var railBrand = NewLabel("言灵", 17f, FontStyle.Bold, ink);
                railBrand.Location = new Point(78, 21);
                railBrand.AutoSize = true;
                var railEnglish = NewLabel("VIBE FLOW REMOTE · V" + ProductRelease, 7.3f, FontStyle.Bold, violet);
                railEnglish.Location = new Point(80, 52);
                railEnglish.AutoSize = true;
                rail.Controls.Add(setupLogo);
                rail.Controls.Add(railBrand);
                rail.Controls.Add(railEnglish);
                wizard.Disposed += delegate { if (setupLogo.Image != null) setupLogo.Image.Dispose(); };

                string[] stepNames =
                {
                    "了解按住说话", "检查 Windows 蓝牙", "配对并连接遥控器", "验证实体按键",
                    "检查遥控器麦克风", "安装 VB-CABLE", "选择语音工具", "完成真实转译",
                    "配置四个方向键", "设置开机自动可用", "完成与检查结果"
                };
                var numberLabels = new Label[OnboardingStepCount];
                var progressLabels = new Label[OnboardingStepCount];
                for (int i = 0; i < OnboardingStepCount; i++)
                {
                    var number = NewLabel((i + 1).ToString(), 8.2f, FontStyle.Bold, muted);
                    number.Location = new Point(27, 98 + i * 45);
                    number.Size = new Size(25, 25);
                    number.TextAlign = ContentAlignment.MiddleCenter;
                    number.BackColor = darkTheme ? surfaceBackground : Color.FromArgb(237, 240, 248);
                    ApplyRoundedRegion(number, 12);
                    var stepLabel = NewLabel(stepNames[i], 8.8f, FontStyle.Regular, muted);
                    stepLabel.Location = new Point(65, 100 + i * 45);
                    stepLabel.Size = new Size(170, 25);
                    numberLabels[i] = number;
                    progressLabels[i] = stepLabel;
                    rail.Controls.Add(number);
                    rail.Controls.Add(stepLabel);
                }
                var privacyRail = NewLabel("全程本地传输 · 默认不保存录音\r\n不读取或记录你的转译文字", 8.2f, FontStyle.Regular, muted);
                privacyRail.Location = new Point(26, 620);
                privacyRail.Size = new Size(204, 48);
                rail.Controls.Add(privacyRail);

                var body = new Panel();
                body.Dock = DockStyle.Fill;
                body.BackColor = pageBackground;
                var pageContent = new Panel();
                pageContent.Location = new Point(34, 24);
                pageContent.Size = new Size(720, 610);
                pageContent.BackColor = Color.Transparent;
                var back = SecondaryButton("上一步", new Point(34, 656), new Size(112, 42));
                var next = PrimaryButton("下一步", new Point(588, 656), new Size(166, 42));
                var stepCounter = NewLabel("第 1 步，共 11 步", 8.6f, FontStyle.Bold, violet);
                stepCounter.Location = new Point(164, 666);
                stepCounter.Size = new Size(128, 24);
                var wizardFeedback = NewLabel("", 8.8f, FontStyle.Bold, muted);
                wizardFeedback.Location = new Point(296, 662);
                wizardFeedback.Size = new Size(278, 30);
                wizardFeedback.TextAlign = ContentAlignment.MiddleCenter;
                body.Controls.Add(pageContent);
                body.Controls.Add(back);
                body.Controls.Add(next);
                body.Controls.Add(stepCounter);
                body.Controls.Add(wizardFeedback);
                wizard.Controls.Add(body);
                wizard.Controls.Add(rail);

                int currentStep = config.setupCompleted ? 0 : Math.Max(0, Math.Min(OnboardingStepCount - 1, config.onboardingStep));
                string selectedProvider = NormalizeProviderKey(config.inputMethod);
                string selectedHotkey = config.inputMethodHotkey;
                string selectedTrigger = config.inputMethodTrigger;
                bool startupChoice = config.launchAtStartup;
                bool bridgeChoice = true;
                bool trayChoice = true;
                bool bluetoothConfirmed = false;
                DateTime keyBaselineUtc = currentStep == 3 ? DateTime.UtcNow : DateTime.MinValue;
                int dictationBaselineGeneration = currentStep == 7 ? GetLatestSessionHealth().Generation : 0;
                bool firstDictationSucceeded = false;
                Label liveStatus = null;
                Label liveDetail = null;
                TextBox testInput = null;
                ComboBox hotkeyTrigger = null;
                TextBox hotkeyBox = null;
                Action<int> renderStep = null;

                Action persistProgress = delegate
                {
                    config.voiceMode = "hold";
                    config.inputMethod = NormalizeProviderKey(selectedProvider);
                    config.inputMethodHotkey = selectedHotkey;
                    config.inputMethodTrigger = selectedTrigger == "hold" ? "hold" : "toggle";
                    config.onboardingVersion = CurrentOnboardingVersion;
                    config.onboardingStep = currentStep;
                    SaveConfig();
                };
                Action<string, bool> showFeedback = delegate(string message, bool success)
                {
                    wizardFeedback.Text = message;
                    wizardFeedback.ForeColor = success ? green : coral;
                };

                renderStep = delegate(int requestedStep)
                {
                    int previousStep = currentStep;
                    currentStep = Math.Max(0, Math.Min(OnboardingStepCount - 1, requestedStep));
                    if (currentStep == 3 && previousStep != 3) keyBaselineUtc = DateTime.UtcNow;
                    if (currentStep == 7 && previousStep != 7)
                    {
                        dictationBaselineGeneration = GetLatestSessionHealth().Generation;
                        firstDictationSucceeded = false;
                    }
                    persistProgress();
                    while (pageContent.Controls.Count > 0) pageContent.Controls[0].Dispose();
                    liveStatus = null;
                    liveDetail = null;
                    testInput = null;
                    hotkeyTrigger = null;
                    hotkeyBox = null;
                    wizardFeedback.Text = "";

                    for (int i = 0; i < OnboardingStepCount; i++)
                    {
                        bool completed = i < currentStep;
                        numberLabels[i].Text = completed ? "✓" : (i + 1).ToString();
                        numberLabels[i].ForeColor = i <= currentStep ? Color.White : muted;
                        numberLabels[i].BackColor = completed ? green : i == currentStep ? violet :
                            (darkTheme ? surfaceBackground : Color.FromArgb(237, 240, 248));
                        progressLabels[i].ForeColor = i == currentStep ? violet : completed ? green : muted;
                        progressLabels[i].Font = new Font("Microsoft YaHei UI", 8.8f,
                            i == currentStep ? FontStyle.Bold : FontStyle.Regular);
                    }
                    stepCounter.Text = "第 " + (currentStep + 1) + " 步，共 " + OnboardingStepCount + " 步";
                    back.Enabled = currentStep > 0;
                    next.Text = currentStep == OnboardingStepCount - 1 ? "完成设置" : "完成本步，继续";

                    string headingText = stepNames[currentStep];
                    string subtitleText = currentStep == 0 ? "只需一次设置。以后拿起遥控器，按住说话、松开结束，再按确认键发送。" :
                        currentStep == 1 ? "确认电脑有可用蓝牙，并在 Windows 中保持蓝牙开启。" :
                        currentStep == 2 ? "在 Windows 中完成 RC003 配对；言灵会自动等待、连接并在休眠后恢复。" :
                        currentStep == 3 ? "按一次方向键。收到真实实体按键后，本步才会通过。" :
                        currentStep == 4 ? "先确认 RC003 的 ATVV 麦克风服务可用；真实声音会在第 8 步验证。" :
                        currentStep == 5 ? "VB-CABLE 是遥控器音频进入语音工具的本地通道，只需安装一次。" :
                        currentStep == 6 ? "选择一个全局默认工具，并让这里的快捷键与工具设置完全一致。" :
                        currentStep == 7 ? "把光标放在下方输入框，按住录音键说话，松开后等待文字出现。" :
                        currentStep == 8 ? "上下左右是四个真实可用的自定义键；默认保持方向导航，每键只执行一个动作。" :
                        currentStep == 9 ? "推荐后台自启动。Windows 登录后不弹主窗口，遥控器就绪后直接可用。" :
                        "最后确认关键链路。以后遇到问题，可在“自检”页直接定位和修复。";
                    var heading = NewLabel(headingText, 20f, FontStyle.Bold, ink);
                    heading.Location = new Point(0, 4);
                    heading.Size = new Size(700, 38);
                    var subtitle = NewLabel(subtitleText, 9.6f, FontStyle.Regular, muted);
                    subtitle.Location = new Point(2, 47);
                    subtitle.Size = new Size(700, 48);
                    pageContent.Controls.Add(heading);
                    pageContent.Controls.Add(subtitle);

                    if (currentStep == 0)
                    {
                        string[,] flow =
                        {
                            { "1", "按住录音键", "只创建一个听写会话" },
                            { "2", "持续自然说话", "真实音频到达后才显示收音" },
                            { "3", "松开录音键", "可靠结束并由工具转译" },
                            { "4", "按中间确认键", "确认内容后再发送" }
                        };
                        for (int i = 0; i < 4; i++)
                        {
                            int y = 118 + i * 94;
                            var number = NewLabel(flow[i, 0], 10f, FontStyle.Bold, Color.White);
                            number.Location = new Point(8, y + 8);
                            number.Size = new Size(34, 34);
                            number.TextAlign = ContentAlignment.MiddleCenter;
                            number.BackColor = i == 0 ? violet : i == 1 ? cyan : i == 2 ? green : amber;
                            ApplyRoundedRegion(number, 17);
                            var title = NewLabel(flow[i, 1], 11f, FontStyle.Bold, ink);
                            title.Location = new Point(62, y);
                            title.Size = new Size(220, 28);
                            var detail = NewLabel(flow[i, 2], 9f, FontStyle.Regular, muted);
                            detail.Location = new Point(62, y + 32);
                            detail.Size = new Size(560, 28);
                            pageContent.Controls.Add(number);
                            pageContent.Controls.Add(title);
                            pageContent.Controls.Add(detail);
                        }
                        var fixedRule = NewLabel("固定规则：录音键不支持切换模式；一次按下只开始一次，一次松开只结束一次。", 9.2f, FontStyle.Bold, violet);
                        fixedRule.Location = new Point(8, 508);
                        fixedRule.Size = new Size(680, 30);
                        pageContent.Controls.Add(fixedRule);
                    }
                    else if (currentStep == 1)
                    {
                        BridgeHealthSnapshot snapshot = ReadKeyboardBridgeHealth();
                        bool detected = bridgeReady || snapshot.RawInputDevicePresent ||
                            ReadCurrentRuntimeSegment().IndexOf("BLE status=Connected", StringComparison.OrdinalIgnoreCase) >= 0;
                        liveStatus = NewLabel(detected ? "●  已检测到可用蓝牙链路" : "●  请在 Windows 中确认蓝牙已开启", 13f, FontStyle.Bold,
                            detected ? green : amber);
                        liveStatus.Location = new Point(8, 124);
                        liveStatus.Size = new Size(540, 36);
                        liveDetail = NewLabel(detected ? "当前已能看到遥控器或蓝牙语音连接。" : "言灵不会修改蓝牙开关；请在系统设置中打开后返回。", 9.2f, FontStyle.Regular, muted);
                        liveDetail.Location = new Point(8, 168);
                        liveDetail.Size = new Size(650, 40);
                        var openBluetooth = PrimaryButton("打开 Windows 蓝牙设置", new Point(8, 230), new Size(210, 42));
                        openBluetooth.Click += delegate { OpenUri("ms-settings:bluetooth"); };
                        var recheck = SecondaryButton("重新检测", new Point(232, 230), new Size(120, 42));
                        recheck.Click += delegate { renderStep(1); };
                        var confirmed = StyledCheck("我已确认 Windows 蓝牙处于开启状态", bluetoothConfirmed, new Point(8, 304));
                        confirmed.CheckedChanged += delegate { bluetoothConfirmed = confirmed.Checked; };
                        pageContent.Controls.Add(liveStatus);
                        pageContent.Controls.Add(liveDetail);
                        pageContent.Controls.Add(openBluetooth);
                        pageContent.Controls.Add(recheck);
                        pageContent.Controls.Add(confirmed);
                    }
                    else if (currentStep == 2)
                    {
                        BridgeHealthSnapshot snapshot = ReadKeyboardBridgeHealth();
                        bool detected = bridgeReady || snapshot.RawInputDevicePresent ||
                            ReadCurrentRuntimeSegment().IndexOf("ATVV READY", StringComparison.OrdinalIgnoreCase) >= 0;
                        liveStatus = NewLabel(detected ? "●  RC003 已被言灵识别" : "●  尚未识别 RC003", 13f, FontStyle.Bold,
                            detected ? green : amber);
                        liveStatus.Location = new Point(8, 124);
                        liveStatus.Size = new Size(560, 36);
                        liveDetail = NewLabel(detected ? "配对信息已存在，后台会继续建立按键与麦克风链路。" :
                            "点击“添加设备”，选择蓝牙并完成 RC003 配对；完成后按方向键唤醒。", 9.2f, FontStyle.Regular, muted);
                        liveDetail.Location = new Point(8, 168);
                        liveDetail.Size = new Size(650, 48);
                        var pair = PrimaryButton("打开添加蓝牙设备", new Point(8, 238), new Size(190, 42));
                        pair.Click += delegate { OpenUri("ms-settings:bluetooth"); };
                        var connect = SecondaryButton(IsCapturing ? "重新检测" : "启动连接", new Point(212, 238), new Size(132, 42));
                        connect.Click += delegate { if (!IsCapturing) StartCapture(); renderStep(2); };
                        var note = NewLabel("遥控器休眠、电脑睡眠或蓝牙晚启动时无需重新配置，言灵会自动等待并恢复。", 9f, FontStyle.Regular, muted);
                        note.Location = new Point(8, 316);
                        note.Size = new Size(660, 44);
                        pageContent.Controls.Add(liveStatus);
                        pageContent.Controls.Add(liveDetail);
                        pageContent.Controls.Add(pair);
                        pageContent.Controls.Add(connect);
                        pageContent.Controls.Add(note);
                    }
                    else if (currentStep == 3)
                    {
                        BridgeHealthSnapshot snapshot = ReadKeyboardBridgeHealth();
                        bool observed = snapshot.LastInputAtUtc > keyBaselineUtc;
                        liveStatus = NewLabel(observed ? "●  已收到真实遥控器按键" : "●  等待方向键", 13f, FontStyle.Bold,
                            observed ? green : cyan);
                        liveStatus.Location = new Point(8, 124);
                        liveStatus.Size = new Size(560, 36);
                        liveDetail = NewLabel(observed ? "按键监听正常，最近事件：" + snapshot.LastInputKind :
                            "现在按一下遥控器上的上、下、左或右键。普通方向键保持 Windows 原生行为。", 9.2f, FontStyle.Regular, muted);
                        liveDetail.Location = new Point(8, 168);
                        liveDetail.Size = new Size(650, 48);
                        var recheck = PrimaryButton("检查刚才的按键", new Point(8, 240), new Size(170, 42));
                        recheck.Click += delegate { renderStep(3); };
                        var repair = SecondaryButton("重建按键监听", new Point(192, 240), new Size(160, 42));
                        repair.Click += delegate { RestartKeyboardBridge("onboarding_key_check"); keyBaselineUtc = DateTime.UtcNow; renderStep(3); };
                        pageContent.Controls.Add(liveStatus);
                        pageContent.Controls.Add(liveDetail);
                        pageContent.Controls.Add(recheck);
                        pageContent.Controls.Add(repair);
                    }
                    else if (currentStep == 4)
                    {
                        string runtime = ReadCurrentRuntimeSegment();
                        bool serviceReady = bridgeReady || runtime.IndexOf("ATVV READY", StringComparison.OrdinalIgnoreCase) >= 0;
                        SessionHealth latest = GetLatestSessionHealth();
                        bool realAudio = latest.AudioMs >= MinimumUsefulAudioMs && latest.OutputRmsPercent > 0;
                        liveStatus = NewLabel(realAudio ? "●  已检测到真实遥控器声音" : serviceReady ? "●  遥控器麦克风服务已就绪" : "●  麦克风服务尚未就绪",
                            13f, FontStyle.Bold, realAudio || serviceReady ? green : amber);
                        liveStatus.Location = new Point(8, 124);
                        liveStatus.Size = new Size(580, 36);
                        liveDetail = NewLabel(realAudio ? "最近音频 " + FormatMillisecondsAsSeconds(latest.AudioMs) + " · 输出电平 " + FormatPercent(latest.OutputRmsPercent) :
                            serviceReady ? "已找到 RC003 ATVV 16 kHz 麦克风。第 8 步会验证真实音频与文字回填。" :
                            "先唤醒遥控器并启动语音桥接；如果 VB-CABLE 尚未安装，可继续到下一步。", 9.2f, FontStyle.Regular, muted);
                        liveDetail.Location = new Point(8, 168);
                        liveDetail.Size = new Size(660, 50);
                        var connect = PrimaryButton(IsCapturing ? "重新检测麦克风" : "启动语音桥接", new Point(8, 242), new Size(180, 42));
                        connect.Click += delegate { if (!IsCapturing) StartCapture(); renderStep(4); };
                        var permissions = SecondaryButton("麦克风权限", new Point(202, 242), new Size(130, 42));
                        permissions.Click += delegate { OpenUri("ms-settings:privacy-microphone"); };
                        pageContent.Controls.Add(liveStatus);
                        pageContent.Controls.Add(liveDetail);
                        pageContent.Controls.Add(connect);
                        pageContent.Controls.Add(permissions);
                    }
                    else if (currentStep == 5)
                    {
                        bool inputReady = HasCableInput();
                        bool outputReady = HasCableOutput();
                        liveStatus = NewLabel(inputReady && outputReady ? "●  VB-CABLE 已安装并可用" : "●  需要安装 VB-CABLE", 13f, FontStyle.Bold,
                            inputReady && outputReady ? green : amber);
                        liveStatus.Location = new Point(8, 118);
                        liveStatus.Size = new Size(580, 36);
                        var route = NewCard(new Point(8, 170), new Size(680, 190));
                        var routeTitle = NewLabel("RC003  →  CABLE Input  →  CABLE Output  →  语音工具", 10f, FontStyle.Bold, ink);
                        routeTitle.Location = new Point(22, 20);
                        routeTitle.Size = new Size(620, 28);
                        var inputState = NewLabel((inputReady ? "✓" : "!") + "  CABLE Input（播放端）" + (inputReady ? " 已检测" : " 未检测"), 9.4f, FontStyle.Bold, inputReady ? green : coral);
                        inputState.Location = new Point(22, 66);
                        inputState.Size = new Size(330, 28);
                        var outputState = NewLabel((outputReady ? "✓" : "!") + "  CABLE Output（录音端）" + (outputReady ? " 已检测" : " 未检测"), 9.4f, FontStyle.Bold, outputReady ? green : coral);
                        outputState.Location = new Point(22, 104);
                        outputState.Size = new Size(330, 28);
                        var routeNote = NewLabel("驱动安装后若提示重启，言灵会在下次登录时自动回到本步骤。", 8.8f, FontStyle.Regular, muted);
                        routeNote.Location = new Point(22, 148);
                        routeNote.Size = new Size(620, 28);
                        route.Controls.Add(routeTitle);
                        route.Controls.Add(inputState);
                        route.Controls.Add(outputState);
                        route.Controls.Add(routeNote);
                        var install = PrimaryButton(inputReady && outputReady ? "打开声音设置" : "安装官方 VB-CABLE", new Point(8, 386), new Size(190, 42));
                        install.Click += delegate
                        {
                            if (inputReady && outputReady) OpenUri("ms-settings:sound");
                            else
                            {
                                config.onboardingStep = 5;
                                config.resumeSetupAfterRestart = true;
                                SaveConfig();
                                SetLaunchAtStartup(true);
                                LaunchVBCableInstaller();
                                showFeedback("安装后如需重启，将自动继续", true);
                            }
                        };
                        var recheck = SecondaryButton("重新检测", new Point(212, 386), new Size(124, 42));
                        recheck.Click += delegate { renderStep(5); };
                        pageContent.Controls.Add(liveStatus);
                        pageContent.Controls.Add(route);
                        pageContent.Controls.Add(install);
                        pageContent.Controls.Add(recheck);
                    }
                    else if (currentStep == 6)
                    {
                        var providerLabel = NewLabel("默认语音工具", 9.4f, FontStyle.Bold, ink);
                        providerLabel.Location = new Point(8, 112);
                        providerLabel.Size = new Size(150, 26);
                        var providerChoice = StyledCombo(new Point(8, 142), new Size(300, 40));
                        providerChoice.Items.AddRange(new object[] { "微信输入法", "Typeless", "豆包输入法", "Windows 语音输入", "其他自定义工具" });
                        providerChoice.SelectedIndex = ProviderIndex(selectedProvider);
                        var providerState = NewLabel(ProviderStatusText(selectedProvider), 9.2f, FontStyle.Bold,
                            IsProviderRunning(selectedProvider) ? green : amber);
                        providerState.Location = new Point(330, 148);
                        providerState.Size = new Size(340, 28);
                        var shortcutLabel = NewLabel("全局启动快捷键", 9.4f, FontStyle.Bold, ink);
                        shortcutLabel.Location = new Point(8, 210);
                        shortcutLabel.Size = new Size(150, 26);
                        hotkeyBox = StyledTextBox(selectedHotkey, new Point(8, 242), new Size(260, 36));
                        hotkeyTrigger = StyledCombo(new Point(286, 240), new Size(190, 40));
                        PopulateTriggerModeOptions(hotkeyTrigger, selectedProvider);
                        hotkeyTrigger.SelectedIndex = NormalizeProviderKey(selectedProvider) == "wechat" ? 0 :
                            selectedTrigger == "hold" ? 1 : 0;
                        var help = NewLabel(ProviderSetupInstruction(selectedProvider), 9f, FontStyle.Regular, muted);
                        help.Location = new Point(8, 312);
                        help.Size = new Size(670, 68);
                        var test = PrimaryButton("保存并测试快捷键", new Point(8, 406), new Size(180, 42));
                        test.Click += delegate
                        {
                            selectedHotkey = hotkeyBox.Text.Trim();
                            selectedTrigger = hotkeyTrigger.SelectedIndex == 1 ? "hold" : "toggle";
                            if (!IsValidTranscriptionHotkey(selectedHotkey))
                            {
                                showFeedback("快捷键格式无效", false);
                                return;
                            }
                            SaveWizardProviderConfig(selectedProvider, selectedHotkey, selectedTrigger, true);
                            TestVoiceHotkey();
                            showFeedback("已发送测试快捷键", true);
                        };
                        var installHelp = SecondaryButton("查看工具安装与设置", new Point(202, 406), new Size(180, 42));
                        installHelp.Click += delegate { OpenProviderHelp(selectedProvider); };
                        providerChoice.SelectedIndexChanged += delegate
                        {
                            string nextProvider = ProviderKeyFromIndex(providerChoice.SelectedIndex);
                            if (nextProvider == selectedProvider) return;
                            selectedProvider = nextProvider;
                            selectedHotkey = DefaultHotkeyForProvider(selectedProvider);
                            selectedTrigger = DefaultTriggerForProvider(selectedProvider);
                            renderStep(6);
                        };
                        hotkeyBox.TextChanged += delegate { selectedHotkey = hotkeyBox.Text.Trim(); };
                        hotkeyTrigger.SelectedIndexChanged += delegate { selectedTrigger = hotkeyTrigger.SelectedIndex == 1 ? "hold" : "toggle"; };
                        pageContent.Controls.Add(providerLabel);
                        pageContent.Controls.Add(providerChoice);
                        pageContent.Controls.Add(providerState);
                        pageContent.Controls.Add(shortcutLabel);
                        pageContent.Controls.Add(hotkeyBox);
                        pageContent.Controls.Add(hotkeyTrigger);
                        pageContent.Controls.Add(help);
                        pageContent.Controls.Add(test);
                        pageContent.Controls.Add(installHelp);
                    }
                    else if (currentStep == 7)
                    {
                        SessionHealth health = GetLatestSessionHealth();
                        firstDictationSucceeded = health.Generation > dictationBaselineGeneration && health.Success;
                        liveStatus = NewLabel(firstDictationSucceeded ? "●  真实转译测试通过" : "●  等待一次完整听写", 13f, FontStyle.Bold,
                            firstDictationSucceeded ? green : cyan);
                        liveStatus.Location = new Point(8, 110);
                        liveStatus.Size = new Size(580, 34);
                        liveDetail = NewLabel(firstDictationSucceeded ?
                            "音频 " + FormatMillisecondsAsSeconds(health.AudioMs) + " · 输出 " + FormatPercent(health.OutputRmsPercent) + " · 工具响应 " + FormatMilliseconds(health.TriggerToReadyMs) :
                            "按住录音键说“测试麦克风一二三四五六”，松开后等待转译。言灵不会读取输入框中的文字。", 9f, FontStyle.Regular, muted);
                        liveDetail.Location = new Point(8, 150);
                        liveDetail.Size = new Size(680, 44);
                        testInput = StyledTextBox("", new Point(8, 214), new Size(680, 120));
                        testInput.Multiline = true;
                        testInput.Font = new Font("Microsoft YaHei UI", 11f);
                        var focus = PrimaryButton(IsCapturing ? "聚焦输入框并开始测试" : "启动桥接并开始测试", new Point(8, 360), new Size(218, 42));
                        focus.Click += delegate
                        {
                            if (!IsCapturing) StartCapture();
                            testInput.Focus();
                            showFeedback("现在按住录音键说话", true);
                        };
                        var recheck = SecondaryButton("检查测试结果", new Point(240, 360), new Size(150, 42));
                        recheck.Click += delegate { renderStep(7); };
                        var route = SecondaryButton("检查声音设置", new Point(404, 360), new Size(150, 42));
                        route.Click += delegate { OpenUri("ms-settings:sound"); };
                        var privacy = NewLabel("文字由所选工具直接写入当前输入框；言灵只依据音频指标判断链路是否成功。", 8.8f, FontStyle.Regular, muted);
                        privacy.Location = new Point(8, 430);
                        privacy.Size = new Size(670, 36);
                        pageContent.Controls.Add(liveStatus);
                        pageContent.Controls.Add(liveDetail);
                        pageContent.Controls.Add(testInput);
                        pageContent.Controls.Add(focus);
                        pageContent.Controls.Add(recheck);
                        pageContent.Controls.Add(route);
                        pageContent.Controls.Add(privacy);
                    }
                    else if (currentStep == 8)
                    {
                        string[] keys = { "上键", "下键", "左键", "右键" };
                        string[] labels = { "上键", "下键", "左键", "右键" };
                        string[] defaults = { "up", "down", "left", "right" };
                        for (int i = 0; i < keys.Length; i++)
                        {
                            string mappingKey = keys[i];
                            string displayLabel = labels[i];
                            string defaultAction = defaults[i];
                            int y = 112 + i * 88;
                            var keyLabel = NewLabel(displayLabel, 10f, FontStyle.Bold, ink);
                            keyLabel.Location = new Point(8, y + 8);
                            keyLabel.Size = new Size(116, 28);
                            var observed = NewLabel(HasObservedPhysicalButton(mappingKey) ? "● 已识别" : "● 待验证",
                                8f, FontStyle.Bold, HasObservedPhysicalButton(mappingKey) ? green : amber);
                            observed.Location = new Point(8, y + 38);
                            observed.Size = new Size(116, 22);
                            string currentAction = GetMapping(mappingKey, defaultAction);
                            var actionChoice = StyledCombo(new Point(142, y), new Size(360, 40));
                            List<ShortcutChoice> choices = ShortcutChoicesFor(mappingKey, currentAction);
                            foreach (ShortcutChoice choice in choices) actionChoice.Items.Add(choice);
                            actionChoice.SelectedIndex = FindShortcutChoice(choices, currentAction);
                            var test = SecondaryButton("立即测试", new Point(520, y), new Size(112, 40));
                            actionChoice.SelectedIndexChanged += delegate
                            {
                                ShortcutChoice selected = actionChoice.SelectedItem as ShortcutChoice;
                                if (selected == null) return;
                                SetMapping(mappingKey, selected.Shortcut);
                                config.mappingPreset = "custom";
                                SaveConfig();
                                showFeedback(displayLabel + "配置已保存", true);
                            };
                            test.Click += delegate
                            {
                                TestMappingAction(displayLabel, GetMapping(mappingKey, defaultAction));
                            };
                            pageContent.Controls.Add(keyLabel);
                            pageContent.Controls.Add(observed);
                            pageContent.Controls.Add(actionChoice);
                            pageContent.Controls.Add(test);
                        }
                        var restore = SecondaryButton("全部恢复方向导航", new Point(8, 480), new Size(178, 40));
                        restore.Click += delegate
                        {
                            SetMapping("上键", "up");
                            SetMapping("下键", "down");
                            SetMapping("左键", "left");
                            SetMapping("右键", "right");
                            SaveConfig();
                            renderStep(8);
                            showFeedback("四个方向键已恢复默认", true);
                        };
                        var note = NewLabel("建议先保持默认方向。只有明确需要时再改动；每个方向键只执行一个动作。",
                            8.8f, FontStyle.Regular, muted);
                        note.Location = new Point(204, 482);
                        note.Size = new Size(464, 40);
                        pageContent.Controls.Add(restore);
                        pageContent.Controls.Add(note);
                    }
                    else if (currentStep == 9)
                    {
                        var startup = StyledCheck("登录 Windows 后自动启动言灵（推荐）", startupChoice, new Point(8, 124));
                        startup.Size = new Size(520, 38);
                        var bridge = StyledCheck("启动后自动连接遥控器与语音桥接", bridgeChoice, new Point(8, 182));
                        bridge.Size = new Size(520, 38);
                        var tray = StyledCheck("关闭主窗口后继续在系统托盘运行", trayChoice, new Point(8, 240));
                        tray.Size = new Size(520, 38);
                        var behavior = NewCard(new Point(8, 318), new Size(680, 138));
                        var behaviorTitle = NewLabel("下次登录时", 10f, FontStyle.Bold, ink);
                        behaviorTitle.Location = new Point(22, 18);
                        behaviorTitle.Size = new Size(180, 28);
                        var behaviorText = NewLabel("不弹出主窗口打扰你；自动等待蓝牙，恢复按键监听和音频配置。\r\n遥控器休眠、电脑睡眠或蓝牙重启后也会自动恢复。", 9.2f, FontStyle.Regular, muted);
                        behaviorText.Location = new Point(22, 54);
                        behaviorText.Size = new Size(620, 58);
                        behavior.Controls.Add(behaviorTitle);
                        behavior.Controls.Add(behaviorText);
                        startup.CheckedChanged += delegate { startupChoice = startup.Checked; };
                        bridge.CheckedChanged += delegate { bridgeChoice = bridge.Checked; };
                        tray.CheckedChanged += delegate { trayChoice = tray.Checked; };
                        pageContent.Controls.Add(startup);
                        pageContent.Controls.Add(bridge);
                        pageContent.Controls.Add(tray);
                        pageContent.Controls.Add(behavior);
                    }
                    else
                    {
                        SelfCheckReport report = BuildSelfCheckReport();
                        string state = report.FailedCount == 0 ? report.WarningCount == 0 ? "●  已准备好" : "●  核心链路已准备好" : "●  仍有项目需要处理";
                        Color stateColor = report.FailedCount == 0 ? green : coral;
                        var summary = NewLabel(state, 16f, FontStyle.Bold, stateColor);
                        summary.Location = new Point(8, 112);
                        summary.Size = new Size(600, 38);
                        string[] names = { "录音方式", "默认语音工具", "VB-CABLE", "开机启动", "后台桥接", "隐私" };
                        string[] values =
                        {
                            "按住说话，松开结束", ProviderDisplayName(selectedProvider),
                            HasCableInput() && HasCableOutput() ? "已检测" : "需要处理",
                            startupChoice ? "自动启动" : "手动启动", bridgeChoice ? "自动连接" : "手动连接",
                            "不保存录音和转译文字"
                        };
                        for (int i = 0; i < names.Length; i++)
                        {
                            int y = 176 + i * 48;
                            var name = NewLabel(names[i], 9.2f, FontStyle.Bold, ink);
                            name.Location = new Point(12, y);
                            name.Size = new Size(170, 26);
                            var value = NewLabel(values[i], 9.2f, FontStyle.Regular,
                                values[i] == "需要处理" ? coral : muted);
                            value.Location = new Point(194, y);
                            value.Size = new Size(430, 26);
                            pageContent.Controls.Add(name);
                            pageContent.Controls.Add(value);
                        }
                        var selfCheck = SecondaryButton("打开完整自检", new Point(8, 492), new Size(150, 40));
                        selfCheck.Click += delegate
                        {
                            persistProgress();
                            wizard.Close();
                            BeginInvoke(new Action(delegate { ShowPage(PageSelfCheck); }));
                        };
                        pageContent.Controls.Add(summary);
                        pageContent.Controls.Add(selfCheck);
                    }
                };

                back.Click += delegate { renderStep(currentStep - 1); };
                next.Click += delegate
                {
                    BridgeHealthSnapshot snapshot = ReadKeyboardBridgeHealth();
                    string runtime = ReadCurrentRuntimeSegment();
                    if (!uiSmokeMode && currentStep == 1 && !bluetoothConfirmed && !bridgeReady && !snapshot.RawInputDevicePresent &&
                        runtime.IndexOf("BLE status=Connected", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        showFeedback("请先确认 Windows 蓝牙已开启", false);
                        return;
                    }
                    if (!uiSmokeMode && currentStep == 2 && !bridgeReady && !snapshot.RawInputDevicePresent &&
                        runtime.IndexOf("ATVV READY", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        showFeedback("尚未识别 RC003，请先配对并唤醒", false);
                        return;
                    }
                    if (!uiSmokeMode && currentStep == 3 && snapshot.LastInputAtUtc <= keyBaselineUtc)
                    {
                        showFeedback("还没有收到刚才的方向键", false);
                        return;
                    }
                    if (!uiSmokeMode && currentStep == 5 && (!HasCableInput() || !HasCableOutput()))
                    {
                        showFeedback("请先安装并检测到 VB-CABLE", false);
                        return;
                    }
                    if (currentStep == 6)
                    {
                        if (hotkeyBox != null) selectedHotkey = hotkeyBox.Text.Trim();
                        if (hotkeyTrigger != null) selectedTrigger = hotkeyTrigger.SelectedIndex == 1 ? "hold" : "toggle";
                        if (!IsValidTranscriptionHotkey(selectedHotkey))
                        {
                            showFeedback("请填写有效的全局快捷键", false);
                            return;
                        }
                        if (uiSmokeMode)
                        {
                            config.inputMethod = NormalizeProviderKey(selectedProvider);
                            config.inputMethodHotkey = selectedHotkey;
                            config.inputMethodTrigger = selectedTrigger;
                            SaveConfig();
                        }
                        else SaveWizardProviderConfig(selectedProvider, selectedHotkey, selectedTrigger, true);
                    }
                    if (currentStep == 7)
                    {
                        SessionHealth health = GetLatestSessionHealth();
                        firstDictationSucceeded = health.Generation > dictationBaselineGeneration && health.Success;
                        if (!uiSmokeMode && !firstDictationSucceeded)
                        {
                            showFeedback("需要先完成一次真实听写测试", false);
                            return;
                        }
                    }
                    if (currentStep == 9)
                    {
                        config.launchAtStartup = startupChoice;
                        config.startBridgeOnLaunch = bridgeChoice;
                        config.minimizeToTray = trayChoice;
                        if (!uiSmokeMode) SetLaunchAtStartup(startupChoice);
                        SaveConfig();
                    }
                    if (currentStep < OnboardingStepCount - 1)
                    {
                        renderStep(currentStep + 1);
                        return;
                    }

                    config.voiceMode = "hold";
                    config.setupCompleted = true;
                    config.onboardingVersion = CurrentOnboardingVersion;
                    config.onboardingStep = OnboardingStepCount - 1;
                    config.resumeSetupAfterRestart = false;
                    config.launchAtStartup = startupChoice;
                    config.startBridgeOnLaunch = bridgeChoice;
                    config.minimizeToTray = trayChoice;
                    ApplyStableVoiceProfile(config);
                    if (!uiSmokeMode) SetLaunchAtStartup(startupChoice);
                    SaveConfig();
                    if (!uiSmokeMode && config.startBridgeOnLaunch && !IsCapturing) StartCapture();
                    wizard.DialogResult = DialogResult.OK;
                    wizard.Close();
                    ShowToast("首次设置已完成，言灵已经可以使用", "success");
                    ShowPage(PageHome);
                };

                wizard.FormClosing += delegate
                {
                    if (!config.setupCompleted) persistProgress();
                };
                renderStep(currentStep);
                wizard.ShowDialog(this);
            }
        }
        finally { setupWizardOpen = false; }
    }

    private void ShowSetupWizardLegacy()
    {
        if (setupWizardOpen) return;
        setupWizardOpen = true;
        try
        {
            using (var wizard = new Form())
            {
                wizard.Text = "欢迎使用 " + DisplayProductName + " · V" + ProductRelease;
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
                        e.Graphics.DrawLine(progress, 43, 146, 43, 426);
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

                string[] stepNames = { "选择转写工具", "安装音频通道", "连接遥控器", "匹配快捷键", "配置常用按键", "完成首次听写" };
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
                var stepCounter = NewLabel("第 1 步，共 6 步", 8.5f, FontStyle.Bold, violet);
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

                int currentStep = config.setupCompleted ? 0 : Math.Max(0, Math.Min(5, config.onboardingStep));
                string selectedProvider = NormalizeProviderKey(config.inputMethod);
                string selectedHotkey = config.inputMethodHotkey;
                string selectedTrigger = config.inputMethodTrigger;
                bool startupChoiceValue = config.launchAtStartup;
                bool autoRouteChoiceValue = config.autoRouteVirtualMicrophone;
                TextBox shortcutBox = null;
                ComboBox triggerBox = null;
                Label liveConnectionStatus = null;
                Label firstDictationStatus = null;
                Label remoteKeyStatus = null;
                int firstDictationBaselineGeneration = 0;
                bool firstDictationSucceeded = false;
                bool remoteKeyObserved = false;
                DateTime remoteKeyBaselineAt = DateTime.MinValue;
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
                Action persistProgress = delegate
                {
                    config.inputMethod = NormalizeProviderKey(selectedProvider);
                    config.inputMethodHotkey = selectedHotkey;
                    config.inputMethodTrigger = selectedTrigger == "hold" ? "hold" : "toggle";
                    config.launchAtStartup = startupChoiceValue;
                    config.autoRouteVirtualMicrophone = autoRouteChoiceValue;
                    config.onboardingStep = currentStep;
                    SaveConfig();
                };

                renderStep = delegate(int step)
                {
                    currentStep = Math.Max(0, Math.Min(5, step));
                    if (currentStep == 2)
                    {
                        remoteKeyBaselineAt = DateTime.UtcNow;
                        remoteKeyObserved = false;
                    }
                    persistProgress();
                    while (pageContent.Controls.Count > 0) pageContent.Controls[0].Dispose();
                    shortcutBox = null;
                    triggerBox = null;
                    liveConnectionStatus = null;
                    remoteKeyStatus = null;
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
                    stepCounter.Text = "第 " + (currentStep + 1) + " 步，共 6 步";
                    wizardFeedback.Text = "";
                    back.Enabled = currentStep > 0;
                    next.Text = currentStep == 0 ? "确认选择" : currentStep == 1 ? "确认音频通道" :
                        currentStep == 2 ? "确认连接" : currentStep == 3 ? "保存并测试" :
                        currentStep == 4 ? "保存按键配置" : "完成设置";

                    string headingText = currentStep == 0 ? "先选择你每天使用的转写工具" :
                        currentStep == 1 ? "安装一次本地音频通道" :
                         currentStep == 2 ? "连接并唤醒遥控器" :
                         currentStep == 3 ? "让快捷键与工具保持一致" : currentStep == 4 ? "配置三个真实遥控器按键" : "完成第一次遥控器听写";
                    string subtitleText = currentStep == 0 ? "言灵负责传输遥控器声音，所选工具负责识别和整理文字。" :
                        currentStep == 1 ? "VB-CABLE 是当前语音链路唯一需要额外安装的本地驱动；检测通过后无需重复安装。" :
                         currentStep == 2 ? "先在 Windows 中完成蓝牙配对，再由言灵建立语音链路。" :
                         currentStep == 3 ? "这里的快捷键必须与转写工具内部设置完全相同。" : currentStep == 4 ? "按一下遥控器按键即可识别；识别后选择常用动作。" : UsesLongDictation(config.voiceMode)
                        ? "点击输入框，单击录音键开始；说完后再按一次结束。"
                        : "点击输入框，按住录音键说完一句话后松开。";
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
                        var install = SecondaryButton(inputReady && outputReady ? "打开声音设置" : "安装 VB-CABLE", new Point(400, 67), new Size(164, 38));
                        install.Click += delegate { if (inputReady && outputReady) OpenUri("ms-settings:sound"); else LaunchVBCableInstaller(); };
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
                        BridgeHealthSnapshot initialBridgeHealth = ReadKeyboardBridgeHealth();
                        remoteKeyObserved = initialBridgeHealth.LastInputAtUtc > remoteKeyBaselineAt;
                        liveConnectionStatus = NewLabel(IsCapturing && bridgeReady && remoteKeyObserved ? "●  已连接，可以使用" :
                            IsCapturing && bridgeReady ? "●  语音已连接，等待按键验证" : IsCapturing ? "●  正在建立语音链路" : "●  等待开始检测", 13f, FontStyle.Bold,
                            IsCapturing && bridgeReady && remoteKeyObserved ? green : IsCapturing ? amber : muted);
                        liveConnectionStatus.Location = new Point(24, 28);
                        liveConnectionStatus.Size = new Size(500, 34);
                        var model = NewLabel("小米蓝牙语音遥控器 2 Pro · RC003", 10f, FontStyle.Regular, ink);
                        model.Location = new Point(25, 76);
                        model.Size = new Size(480, 28);
                        var connectionHelp = NewLabel("若一直连接中，请按任意方向键唤醒遥控器，或在 Windows 中重新连接。", 9.2f, FontStyle.Regular, muted);
                        connectionHelp.Location = new Point(25, 102);
                        connectionHelp.Size = new Size(535, 46);
                        remoteKeyStatus = NewLabel(remoteKeyObserved ? "●  已收到遥控器按键事件" : "●  请按一次遥控器方向键，验证按键桥接", 9.2f, FontStyle.Bold,
                            remoteKeyObserved ? green : violet);
                        remoteKeyStatus.Location = new Point(25, 142);
                        remoteKeyStatus.Size = new Size(535, 24);
                        var bluetooth = SecondaryButton("打开蓝牙设置", new Point(24, 176), new Size(150, 40));
                        bluetooth.Click += delegate { OpenUri("ms-settings:bluetooth"); };
                        var detect = PrimaryButton(IsCapturing ? "重新检测" : "开始检测", new Point(188, 176), new Size(140, 40));
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
                        connection.Controls.Add(remoteKeyStatus);
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
                        PopulateTriggerModeOptions(triggerBox, selectedProvider);
                        triggerBox.SelectedIndex = NormalizeProviderKey(selectedProvider) == "wechat" ? 0 :
                            selectedTrigger == "hold" ? 1 : 0;
                        triggerBox.SelectedIndexChanged += delegate
                        {
                            selectedTrigger = triggerBox.SelectedIndex == 1 ? "hold" : "toggle";
                            if (NormalizeProviderKey(selectedProvider) == "wechat")
                            {
                                selectedTrigger = "toggle";
                                selectedHotkey = WeChatStableHotkey;
                                shortcutBox.Text = selectedHotkey;
                            }
                        };
                        var reset = SecondaryButton("恢复推荐配置", new Point(366, 111), new Size(166, 38));
                        reset.Click += delegate
                        {
                            selectedHotkey = DefaultHotkeyForProvider(selectedProvider);
                            selectedTrigger = DefaultTriggerForProvider(selectedProvider);
                            shortcutBox.Text = selectedHotkey;
                            triggerBox.SelectedIndex = NormalizeProviderKey(selectedProvider) == "wechat" ? 0 :
                                selectedTrigger == "hold" ? 1 : 0;
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
                    else if (currentStep == 4)
                    {
                        var customIntro = NewLabel("先配置 3 个常用实体按键。它们与按键页中的遥控器一一对应，之后可随时修改。", 9.2f, FontStyle.Regular, muted);
                        customIntro.Location = new Point(4, 102);
                        customIntro.Size = new Size(594, 38);
                        pageContent.Controls.Add(customIntro);
                        string[,] starterButtons = {
                            { "Home", "win+d", "显示桌面" },
                            { "TV", "task-switcher", "切换程序" },
                            { "功能键", "launch-client:chatgpt", "打开应用" }
                        };
                        for (int i = 0; i < starterButtons.GetLength(0); i++)
                        {
                            int slot = i;
                            string mappingKey = starterButtons[slot, 0];
                            string currentAction = GetMapping(mappingKey, starterButtons[slot, 1]);
                            var row = new Panel();
                            row.Location = new Point(4, 148 + slot * 84);
                            row.Size = new Size(600, 72);
                            row.BackColor = Color.White;
                            row.Paint += delegate(object sender, PaintEventArgs e)
                            {
                                using (var pen = new Pen(Color.FromArgb(232, 236, 245))) e.Graphics.DrawRectangle(pen, 0, 0, row.Width - 1, row.Height - 1);
                            };
                            var label = NewLabel(mappingKey, 9.2f, FontStyle.Bold, ink);
                            label.Location = new Point(14, 10);
                            label.Size = new Size(116, 24);
                            var source = NewLabel("实体遥控器 · " + starterButtons[slot, 2], 8f, FontStyle.Regular, muted);
                            source.Location = new Point(14, 38);
                            source.Size = new Size(270, 22);
                            source.AutoEllipsis = true;
                            var actionBox = StyledCombo(new Point(294, 9), new Size(184, 34));
                            List<ShortcutChoice> choices = ShortcutChoicesFor(mappingKey, currentAction);
                            foreach (ShortcutChoice choice in choices) actionBox.Items.Add(choice);
                            actionBox.SelectedIndex = FindShortcutChoice(choices, currentAction);
                            var test = SecondaryButton("测试", new Point(492, 9), new Size(92, 34));
                            test.Click += delegate { TestMappingAction(mappingKey, GetMapping(mappingKey, starterButtons[slot, 1])); };
                            actionBox.SelectedIndexChanged += delegate
                            {
                                ShortcutChoice selected = actionBox.SelectedItem as ShortcutChoice;
                                if (selected == null) return;
                                string resolvedAction = ResolveCustomActionSelection(selected.Shortcut, wizard);
                                if (string.IsNullOrWhiteSpace(resolvedAction)) return;
                                SetMapping(mappingKey, resolvedAction);
                                config.mappingPreset = "custom";
                                SaveConfig();
                                if (!string.Equals(resolvedAction, selected.Shortcut, StringComparison.OrdinalIgnoreCase)) renderStep(4);
                            };
                            row.Controls.Add(label);
                            row.Controls.Add(source);
                            row.Controls.Add(actionBox);
                            row.Controls.Add(test);
                            pageContent.Controls.Add(row);
                        }
                        var customNote = NewLabel("至少 3 个真实按键已可用。需要打开其他程序或网址时，可直接从下拉列表选择。", 8.8f, FontStyle.Regular, muted);
                        customNote.Location = new Point(4, 410);
                        customNote.Size = new Size(594, 30);
                        pageContent.Controls.Add(customNote);
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
                        firstDictationStatus = NewLabel(firstDictationSucceeded ? "●  首次听写成功，已经可以开始使用" : "●  " + VoiceReadyInstruction(config.voiceMode), 10f, FontStyle.Bold,
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
                            firstDictationStatus.Text = "●  已就绪 · " + VoiceReadyInstruction(config.voiceMode);
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
                    config.onboardingStep = 6;
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
                        if (!remoteKeyObserved)
                        {
                            showWizardFeedback("请按一次遥控器方向键完成按键验证", false);
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
                    else if (currentStep == 4)
                    {
                        int customCount = CountConfiguredPhysicalButtons();
                        if (customCount < 3)
                        {
                            showWizardFeedback("请为至少 3 个真实遥控器按键选择功能（" + customCount + "/3）", false);
                            return;
                        }
                        SaveConfig();
                        renderStep(5);
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
                        BridgeHealthSnapshot currentBridgeHealth = ReadKeyboardBridgeHealth();
                        if (!remoteKeyObserved && currentBridgeHealth.LastInputAtUtc > remoteKeyBaselineAt)
                            remoteKeyObserved = true;
                        liveConnectionStatus.Text = IsCapturing && bridgeReady && remoteKeyObserved ? "●  已连接，可以使用" :
                            IsCapturing && bridgeReady ? "●  语音已连接，等待按键验证" : IsCapturing ? "●  正在建立语音链路" : "●  等待开始检测";
                        liveConnectionStatus.ForeColor = IsCapturing && bridgeReady && remoteKeyObserved ? green : IsCapturing ? amber : muted;
                        if (remoteKeyStatus != null && !remoteKeyStatus.IsDisposed)
                        {
                            remoteKeyStatus.Text = remoteKeyObserved ? "●  已收到遥控器按键事件" : "●  请按一次遥控器方向键，验证按键桥接";
                            remoteKeyStatus.ForeColor = remoteKeyObserved ? green : violet;
                        }
                        if (IsCapturing && bridgeReady && remoteKeyObserved) showWizardFeedback("连接和按键验证成功", true);
                        else if (IsCapturing) showWizardInfo("正在检测，请稍候");
                    }
                    if (currentStep == 5 && firstDictationStatus != null && !firstDictationStatus.IsDisposed)
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
                            firstDictationStatus.Text = UsesLongDictation(config.voiceMode)
                                ? "●  正在连续听写 · 说完后再按一次录音键"
                                : "●  正在听写 · 请自然说话，完成后松开录音键";
                            firstDictationStatus.ForeColor = violet;
                        }
                    }
                };
                wizardTimer.Start();
                // Resume the last incomplete step. A completed setup still opens
                // at the first step when the user explicitly reopens the guide.
                renderStep(config.setupCompleted ? 0 : currentStep);
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
            heroSubtitle.Text = !IsCapturing ? "启动后，" + VoiceStartInstruction(config.voiceMode) : bridgeReady ? VoiceStartInstruction(config.voiceMode) : "正在建立遥控器语音通道，请稍候";
        if (heroStateLabel != null && !heroStateLabel.IsDisposed)
            heroStateLabel.Text = !IsCapturing ? "VOICE LINK OFF" : bridgeReady ? "PUSH TO TALK READY" : "CONNECTING";
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
            surface = StatusSurface("recording");
            voiceText = "●  正在听写 · 遥控器音频正在到达";
        }
        else if (state == "recovering")
        {
            accent = cyan;
            surface = StatusSurface("recovering");
            voiceText = "●  当前按住会话保持中 · 正在恢复遥控器音频";
        }
        else if (state == "completed")
        {
            accent = green;
            surface = StatusSurface("completed");
            voiceText = "●  听写已完成 · 文字已交给转写工具";
        }
        else if (state == "processing")
        {
            accent = cyan;
            surface = StatusSurface("processing");
            voiceText = "●  录音已结束 · 正在整理并回填文字";
        }
        else if (state == "error")
        {
            accent = Color.FromArgb(202, 76, 76);
            surface = StatusSurface("error");
            voiceText = "●  本次听写未完成 · 请打开诊断查看原因";
        }
        else if (state == "ready")
        {
            accent = green;
            surface = StatusSurface("ready");
            voiceText = "●  已就绪 · " + VoiceReadyInstruction(config.voiceMode);
        }
        else if (state == "connecting")
        {
            accent = amber;
            surface = StatusSurface("connecting");
            voiceText = "●  正在连接遥控器麦克风";
        }
        else
        {
            accent = muted;
            surface = StatusSurface("stopped");
            voiceText = "●  语音桥接已暂停";
        }
        currentVisualAccent = accent;
        currentVisualState = state;
        if (visualTimer != null && (state == "recording" || state == "recovering" ||
            state == "processing" || state == "connecting") && visualTimer.Interval != 50)
            visualTimer.Interval = 50;
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
            ApplyCustomButtonCaptureResult();
            ApplyMappingActionTestResult();
            PollRuntimeFeedback();
            PollInputFeedback();
            PollKeyboardBridgeHealth();
            PollCaptureHealth();
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

    private void PollCaptureHealth()
    {
        if (applicationExiting || !config.setupCompleted || !config.startBridgeOnLaunch) return;
        if (!IsCapturing || captureStartedAt == DateTime.MinValue)
        {
            captureNotReadySince = DateTime.MinValue;
            captureHeartbeatUnhealthySince = DateTime.MinValue;
            return;
        }

        Dictionary<string, object> captureHealth;
        string captureHealthError;
        if (!TryReadCaptureHeartbeat(captureProcess, out captureHealth, out captureHealthError))
        {
            DateTime healthNow = DateTime.UtcNow;
            if (captureHeartbeatUnhealthySince == DateTime.MinValue)
            {
                captureHeartbeatUnhealthySince = healthNow;
                HostLog("CAPTURE health_degraded=true reason=" + (captureHealthError ?? "heartbeat_invalid"));
                return;
            }
            if ((healthNow - captureHeartbeatUnhealthySince).TotalSeconds < CaptureRecoveryCooldownSeconds) return;
            captureHeartbeatUnhealthySince = DateTime.MinValue;
            HostLog("CAPTURE health_invalid=true reason=" + (captureHealthError ?? "heartbeat_invalid") +
                " action=restart_capture_and_bridge");
            StopCapture();
            StartCapture();
            return;
        }
        captureHeartbeatUnhealthySince = DateTime.MinValue;
        if (bridgeReady)
        {
            captureNotReadySince = DateTime.MinValue;
            return;
        }

        // GATT reconnects can legitimately report a healthy heartbeat while
        // the capture worker is backing off from a transient AccessDenied.
        // Restarting it here tears down the very session that needs time to
        // release its Bluetooth handle and causes an endless startup loop.
        object captureStateValue;
        string captureState = captureHealth != null &&
            captureHealth.TryGetValue("state", out captureStateValue)
            ? Convert.ToString(captureStateValue) : "";
        if (captureState.Equals("recovering", StringComparison.OrdinalIgnoreCase))
        {
            captureNotReadySince = DateTime.MinValue;
            return;
        }

        DateTime now = DateTime.UtcNow;
        if (captureNotReadySince == DateTime.MinValue)
        {
            captureNotReadySince = now;
            HostLog("CAPTURE health_degraded=true reason=atvv_not_ready");
            return;
        }
        if ((now - captureNotReadySince).TotalSeconds < CaptureReadyRecoverySeconds ||
            (now - lastCaptureRecoveryAt).TotalSeconds < CaptureRecoveryCooldownSeconds) return;

        lastCaptureRecoveryAt = now;
        captureNotReadySince = DateTime.MinValue;
        HostLog("CAPTURE health_invalid=true reason=atvv_ready_timeout duration_s=" +
            (now - captureStartedAt.ToUniversalTime()).TotalSeconds.ToString("0") +
            " action=restart_capture_and_bridge");
        StopCapture();
        StartCapture();
    }

    private SessionHealth GetLatestSessionHealth()
    {
        var health = new SessionHealth();
        health.Provider = NormalizeProviderKey(config.inputMethod);
        health.NextAction = "按住遥控器录音键完成一次测试，松开后等待转译";
        string path = Path.Combine(sessionDir, "vibe-mic-runtime.log");
        if (!File.Exists(path)) return health;

        string[] lines;
        try { lines = ReadLogTailLines(path, 512 * 1024); }
        catch { return health; }
        int searchStart = Math.Max(0, lines.Length - 2400);
        int sessionStart = -1;
        bool longHealth = UsesLongDictation(config.voiceMode);
        string sessionStartMarker = longHealth ? "LONG DICTATION START generation=" : "REMOTE STREAM START session=";
        for (int i = lines.Length - 1; i >= searchStart; i--)
        {
            if (lines[i].IndexOf(sessionStartMarker, StringComparison.OrdinalIgnoreCase) < 0) continue;
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

        double weightedRawRmsSquare = 0;
        double weightedOutputRmsSquare = 0;
        long rmsWeightMs = 0;
        for (int i = sessionStart; i < lines.Length; i++)
        {
            string item = lines[i];
            if (i > sessionStart && item.IndexOf(sessionStartMarker, StringComparison.OrdinalIgnoreCase) >= 0) break;
            if (longHealth && item.IndexOf("ATVV MIC_EXTEND written", StringComparison.OrdinalIgnoreCase) >= 0)
                health.MicExtendWrites++;
            int itemGeneration;
            bool hasGeneration = int.TryParse(ExtractMetric(item, "generation"), out itemGeneration);
            int itemLogicalGeneration;
            bool hasLogicalGeneration = int.TryParse(ExtractMetric(item, "logical_generation"), out itemLogicalGeneration);
            if (hasGeneration && itemGeneration != health.Generation &&
                (!hasLogicalGeneration || itemLogicalGeneration != health.Generation)) continue;

            if (item.IndexOf("INPUT TARGET CAPTURE", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                health.InputTargetObserved = true;
                health.InputTargetCaptured = item.IndexOf("source=none", StringComparison.OrdinalIgnoreCase) < 0;
            }
            else if (item.IndexOf("INPUT TARGET RESTORE", StringComparison.OrdinalIgnoreCase) >= 0 &&
                item.IndexOf("delivery_ready=True", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                health.InputTargetObserved = true;
                health.InputTargetReady = true;
            }
            else if (item.IndexOf("TRANSCRIPTION READY", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                health.Ready = true;
                health.Provider = NormalizeProviderKey(ExtractMetric(item, "provider"));
                int.TryParse(ExtractMetric(item, "trigger_to_ready_ms"), out health.TriggerToReadyMs);
            }
            else if (item.IndexOf("DEFAULT CAPTURE ROUTE ACQUIRED", StringComparison.OrdinalIgnoreCase) >= 0) health.RouteAcquired = true;
            else if (item.IndexOf("DEFAULT CAPTURE ROUTE RESTORED", StringComparison.OrdinalIgnoreCase) >= 0) health.RouteRestored = true;
            else if (item.IndexOf("DEFAULT CAPTURE ROUTE RESTORE PENDING", StringComparison.OrdinalIgnoreCase) >= 0) health.RouteRestorePending = true;
            else if (item.IndexOf("AUDIO TRANSPORT HEALTH", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                health.AudioLive = string.Equals(ExtractMetric(item, "audio_live"), "True",
                    StringComparison.OrdinalIgnoreCase);
                int.TryParse(ExtractMetric(item, "packet_age_ms"), out health.LastPacketAgeMs);
                health.WasapiState = ExtractMetric(item, "wasapi_state");
                health.EndpointState = ExtractMetric(item, "endpoint_state");
                health.DefaultRouteState = ExtractMetric(item, "default_route");
            }
            else if (item.IndexOf("AUDIO TRANSPORT STALLED", StringComparison.OrdinalIgnoreCase) >= 0)
                health.AudioStallCount++;
            else if (item.IndexOf("LONG DICTATION TRANSPORT RECOVERY START", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                int recovery;
                if (int.TryParse(ExtractMetric(item, "recovery"), out recovery))
                    health.TransportRecoveryCount = Math.Max(health.TransportRecoveryCount, recovery);
            }
            else if (item.IndexOf("REMOTE STREAM STOP session=", StringComparison.OrdinalIgnoreCase) >= 0 ||
                item.IndexOf("REMOTE STREAM SEGMENT STOP session=", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (item.IndexOf("REMOTE STREAM STOP session=", StringComparison.OrdinalIgnoreCase) >= 0)
                    health.StreamStopped = true;
                int segmentAudioMs;
                if (int.TryParse(ExtractMetric(item, "audio_ms"), out segmentAudioMs)) health.AudioMs += segmentAudioMs;
                int segmentGap;
                if (int.TryParse(ExtractMetric(item, "max_gap_ms"), out segmentGap))
                    health.MaxGapMs = Math.Max(health.MaxGapMs, segmentGap);
                int segmentQueueDrops;
                if (int.TryParse(ExtractMetric(item, "queue_drops"), out segmentQueueDrops))
                    health.QueueDrops += Math.Max(0, segmentQueueDrops);
                int segmentSinkDrops;
                if (int.TryParse(ExtractMetric(item, "sink_queue_drops"), out segmentSinkDrops))
                    health.SinkQueueDrops = Math.Max(health.SinkQueueDrops, segmentSinkDrops);
                double segmentRawRms;
                double segmentOutputRms;
                if (segmentAudioMs > 0 &&
                    double.TryParse(ExtractMetric(item, "raw_rms_pct"), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out segmentRawRms) &&
                    double.TryParse(ExtractMetric(item, "output_rms_pct"), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out segmentOutputRms))
                {
                    weightedRawRmsSquare += segmentRawRms * segmentRawRms * segmentAudioMs;
                    weightedOutputRmsSquare += segmentOutputRms * segmentOutputRms * segmentAudioMs;
                    rmsWeightMs += segmentAudioMs;
                }
            }
            else if (item.IndexOf("LONG DICTATION END generation=", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                health.StreamStopped = true;
                int totalAudioMs;
                if (int.TryParse(ExtractMetric(item, "audio_ms"), out totalAudioMs)) health.AudioMs = totalAudioMs;
                int.TryParse(ExtractMetric(item, "segments"), out health.SegmentCount);
                int.TryParse(ExtractMetric(item, "elapsed_ms"), out health.ElapsedMs);
                TryParseRuntimeTimestamp(item, out health.EndedAt);
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
                health.DeliveryMode = ExtractMetric(item, "delivery_mode");
                health.DeliveryFailed = string.Equals(health.DeliveryMode, "provider_direct_unconfirmed",
                    StringComparison.OrdinalIgnoreCase) || string.Equals(health.DeliveryMode, "not_submitted",
                    StringComparison.OrdinalIgnoreCase);
                if (item.IndexOf("input_target_ready=", StringComparison.OrdinalIgnoreCase) >= 0)
                    health.InputTargetObserved = true;
                if (item.IndexOf("input_target_ready=True", StringComparison.OrdinalIgnoreCase) >= 0)
                    health.InputTargetReady = true;
                TryParseRuntimeTimestamp(item, out health.EndedAt);
            }
            if (item.IndexOf("AUDIO TRANSPORT FAILED", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                health.TransportFailed = true;
                health.Failed = true;
                health.Error = item.Length > 180 ? item.Substring(0, 180) : item;
            }
            else if (item.IndexOf("AUDIO LIVE FAILED", StringComparison.OrdinalIgnoreCase) >= 0 ||
                item.IndexOf("SESSION ERROR", StringComparison.OrdinalIgnoreCase) >= 0 ||
                item.IndexOf("DEFAULT CAPTURE ROUTE FAILED", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                health.Failed = true;
                health.Error = item.Length > 180 ? item.Substring(0, 180) : item;
            }
        }

        if (rmsWeightMs > 0)
        {
            health.RawRmsPercent = Math.Sqrt(weightedRawRmsSquare / rmsWeightMs);
            health.OutputRmsPercent = Math.Sqrt(weightedOutputRmsSquare / rmsWeightMs);
        }
        if (health.ElapsedMs > 0)
            health.AudioCoveragePercent = Math.Min(100.0, health.AudioMs * 100.0 / health.ElapsedMs);
        if (longHealth && health.Completed && health.ElapsedMs >= 30000 && health.AudioCoveragePercent < 80.0)
            health.TransportFailed = true;
        health.Success = health.Completed && health.AudioDelivered && health.StreamStopped &&
            health.AudioMs >= MinimumUsefulAudioMs &&
            !health.Failed && !health.TransportFailed && !health.DeliveryFailed;
        if (health.TransportFailed) health.NextAction = "真实音频覆盖不足或续流失败，请打开诊断记录并重新连接遥控器";
        else if (health.Failed) health.NextAction = "打开诊断记录并复制问题摘要";
        else if (health.DeliveryFailed) health.NextAction = "工具未确认目标输入框；请保持原输入框聚焦后重新测试";
        else if (!health.Ready) health.NextAction = "转写工具没有进入听写状态，请先测试工具快捷键";
        else if (!health.StreamStopped) health.NextAction = "仍在听写；说完后松开录音键并等待完成";
        else if (health.AudioMs > 0 && health.AudioMs < MinimumUsefulAudioMs)
            health.NextAction = "按住时间太短，请完整说完一句话后再松开";
        else if (health.QueueDrops > 0 || health.SinkQueueDrops > 0) health.NextAction = "音频队列出现丢包，请重新连接蓝牙后再试";
        else if (health.OutputRmsPercent > 0 && health.OutputRmsPercent < 0.8) health.NextAction = "声音偏小，请靠近遥控器麦克风并自然说话";
        else if (health.MaxGapMs > 250) health.NextAction = "蓝牙音频间隔偏大，请减少距离或重新连接遥控器";
        else if (config.autoRouteVirtualMicrophone && !health.RouteAcquired) health.NextAction = "没有切换到 CABLE Output，请重新检测本地音频通道";
        else if (health.Success && NormalizeProviderKey(health.Provider) == "wechat" && health.InputTargetObserved && !health.InputTargetReady)
            health.NextAction = "音频与转写成功，但原输入框焦点未恢复；请重新聚焦输入框后再试";
        else if (health.Success && health.RouteRestorePending) health.NextAction = "音频已送达工具，但请检查 Windows 默认麦克风是否已恢复";
        else if (health.Success) health.NextAction = "音频与工具唤起链路正常；请确认目标输入框中出现文字";
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
            result.AppendLine("下一步：" + VoiceStartInstruction(config.voiceMode) + "，说一句完整的话。 ");
            return result.ToString();
        }

        string state = health.Success ? "成功" : health.Failed || health.TransportFailed ? "失败" :
            health.Completed ? "需要检查" : "进行中";
        result.AppendLine("最近一次听写：" + state + "  ·  会话 #" + health.Generation);
        result.AppendLine("转写工具：" + ProviderDisplayName(health.Provider));
        result.AppendLine("录音时长：" + FormatMillisecondsAsSeconds(health.AudioMs) +
            (health.SegmentCount > 1 ? "（" + health.SegmentCount + " 个连续分段）" : "") +
            "  ·  工具响应：" + FormatMilliseconds(health.TriggerToReadyMs) +
            "  ·  输出电平：" + FormatPercent(health.OutputRmsPercent));
        if (UsesLongDictation(config.voiceMode) && health.ElapsedMs > 0)
            result.AppendLine("真实音频覆盖：" + health.AudioCoveragePercent.ToString("0.0") + "%（" +
                FormatMillisecondsAsSeconds(health.AudioMs) + " / " + FormatMillisecondsAsSeconds(health.ElapsedMs) +
                "）  ·  恢复 " + health.TransportRecoveryCount + " 次  ·  停滞 " + health.AudioStallCount +
                " 次  ·  MIC_EXTEND " + health.MicExtendWrites + " 次");
        result.AppendLine("蓝牙最大间隔：" + FormatMilliseconds(health.MaxGapMs) +
            "  ·  音频丢包：" + Math.Max(0, health.QueueDrops + health.SinkQueueDrops) +
            "  ·  排空：" + (health.Drained ? FormatMilliseconds(health.DrainWaitMs) : "等待中"));
        result.AppendLine("麦克风路由：" + (!config.autoRouteVirtualMicrophone ? "手动" : health.RouteAcquired ? "已切换到 CABLE Output" : "未确认切换") +
            "  ·  恢复：" + (!config.autoRouteVirtualMicrophone ? "不适用" : health.RouteRestored ? "已恢复" : health.RouteRestorePending ? "待确认" : "等待中"));
        if (NormalizeProviderKey(health.Provider) == "wechat")
            result.AppendLine("输入目标跟踪（不读取文字）：" + (!health.InputTargetObserved ? "升级后尚未复测（下次听写自动验证）" :
                health.DeliveryFailed ? "工具未确认直接写入路径" :
                health.InputTargetReady ? "焦点保持正常（仍需目视确认文字）" :
                health.InputTargetCaptured ? "已记录目标，等待工具直填" : "未记录输入目标"));
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
        result.AppendLine("遥控器录音：按住说话 · 松开结束 · 稳定单会话模式");
        result.AppendLine("语音桥接：" + (IsCapturing ? "运行中" : "已暂停") + "  ·  遥控器语音：" + (bridgeReady ? "已就绪" : "未就绪"));
        BridgeHealthSnapshot bridgeHealth = ReadKeyboardBridgeHealth();
        result.AppendLine("按键路由：" + (bridgeHealth.FilterHealthy ? "设备级精确隔离" :
            string.Equals(bridgeHealth.RoutingAuthority, "raw_input", StringComparison.OrdinalIgnoreCase) ?
            "Raw Input 安全直通" : "未确认") + "  ·  设备事件 " + bridgeHealth.RawRemoteEdges +
            "  ·  动作事件 " + (bridgeHealth.RawActionEdges + bridgeHealth.FilterActionEdges) +
            (string.IsNullOrWhiteSpace(bridgeHealth.LastRawAction) ? "" : "  ·  最近 " + bridgeHealth.LastRawAction));
        result.AppendLine("VB-CABLE：Input " + (HasCableInput() ? "已检测" : "未检测") + " / Output " + (HasCableOutput() ? "已检测" : "未检测"));
        result.AppendLine("稳定语音档案：" + (HasStableVoiceProfile(config) ? "v" + StableVoiceProfileVersion + " 已应用" : "参数已自定义"));
        SelfCheckReport selfCheck = BuildSelfCheckReport();
        result.AppendLine("自检：通过 " + selfCheck.PassedCount + " / " + selfCheck.Items.Count + "  ·  检测中 " + selfCheck.CheckingCount +
            "  ·  建议 " + selfCheck.WarningCount + "  ·  待修复 " + selfCheck.FailedCount);
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

    private WindowsHardwareProbe GetWindowsHardwareProbe()
    {
        WindowsHardwareProbe cached = windowsHardwareProbe;
        bool probeRunning = Interlocked.CompareExchange(ref windowsHardwareProbeRunning, 0, 0) != 0;
        if (cached != null && (probeRunning ||
            (DateTime.UtcNow - windowsHardwareProbeAt).TotalSeconds < 20)) return cached;

        if (Interlocked.CompareExchange(ref windowsHardwareProbeRunning, 1, 0) != 0)
            return cached ?? new WindowsHardwareProbe();

        var pending = new WindowsHardwareProbe();
        pending.Error = "正在读取 Windows 蓝牙设备状态";
        windowsHardwareProbe = pending;
        windowsHardwareProbeAt = DateTime.UtcNow;
        ThreadPool.QueueUserWorkItem(delegate
        {
            WindowsHardwareProbe result = ProbeWindowsHardware();
            windowsHardwareProbe = result;
            windowsHardwareProbeAt = DateTime.UtcNow;
            Interlocked.Exchange(ref windowsHardwareProbeRunning, 0);
            try
            {
                if (!IsDisposed && IsHandleCreated)
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (!IsDisposed && currentPageIndex == PageSelfCheck) ShowPage(PageSelfCheck);
                    });
                }
            }
            catch { }
        });
        return pending;
    }

    private WindowsHardwareProbe ProbeWindowsHardware()
    {
        var result = new WindowsHardwareProbe();
        try
        {
            string script =
                "$ProgressPreference='SilentlyContinue';$WarningPreference='SilentlyContinue';" +
                "$bt=@(Get-PnpDevice -Class Bluetooth -PresentOnly -ErrorAction SilentlyContinue);" +
                "$remote=@(Get-PnpDevice -Class HIDClass -PresentOnly -ErrorAction SilentlyContinue|Where-Object{" +
                "$_.InstanceId -match 'VID(&|_)012717.*PID(&|_)32B8|VID_2717.*PID_32B8' -or " +
                "$_.FriendlyName -match 'RC003|小米.*遥控|Xiaomi.*Remote'});" +
                "'bluetooth_present=' + [int]($bt.Count -gt 0);" +
                "'bluetooth_ok=' + [int](@($bt|Where-Object{$_.Status -eq 'OK'}).Count -gt 0);" +
                "'remote_present=' + [int]($remote.Count -gt 0);" +
                "'remote_ok=' + [int](@($remote|Where-Object{$_.Status -eq 'OK'}).Count -gt 0)";
            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            var start = new ProcessStartInfo("powershell.exe");
            start.Arguments = "-NoProfile -NonInteractive -WindowStyle Hidden -EncodedCommand " + encoded;
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;
            using (Process process = Process.Start(start))
            {
                if (process == null) throw new InvalidOperationException("PowerShell probe did not start");
                System.Threading.Tasks.Task<string> outputRead = process.StandardOutput.ReadToEndAsync();
                System.Threading.Tasks.Task<string> errorRead = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(10000))
                {
                    try { process.Kill(); } catch { }
                    throw new TimeoutException("Windows hardware probe timed out");
                }
                process.WaitForExit();
                string output = outputRead.Result;
                string probeError = errorRead.Result;
                if (!string.IsNullOrWhiteSpace(probeError))
                    HostLog("WINDOWS HARDWARE PROBE stderr=" + SafeLogValue(probeError));
                string[] lines = output.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string lineText in lines)
                {
                    string lineValue = lineText.Trim();
                    if (lineValue == "bluetooth_present=1") result.BluetoothPresent = true;
                    else if (lineValue == "bluetooth_ok=1") result.BluetoothOk = true;
                    else if (lineValue == "remote_present=1") result.RemotePresent = true;
                    else if (lineValue == "remote_ok=1") result.RemoteOk = true;
                }
                result.Completed = true;
            }
        }
        catch (Exception ex)
        {
            result.Completed = true;
            result.Failed = true;
            result.Error = ex.Message;
            HostLog("WINDOWS HARDWARE PROBE failed=true error=" + SafeLogValue(ex.Message));
        }
        return result;
    }

    private static string ResolveBluetoothSelfCheckState(WindowsHardwareProbe hardware,
        BridgeHealthSnapshot bridge)
    {
        if (bridge != null && bridge.Healthy && bridge.RawInputDevicePresent) return "pass";
        if (hardware == null || !hardware.Completed) return "checking";
        if (hardware.Failed) return "fail";
        if (!hardware.BluetoothPresent) return "unsupported";
        return hardware.BluetoothOk ? "pass" : "fail";
    }

    private static string MicrophonePermissionState()
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                "Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\microphone", false))
            {
                string value = key == null ? "" : Convert.ToString(key.GetValue("Value"));
                if (value.Equals("Deny", StringComparison.OrdinalIgnoreCase)) return "deny";
                if (value.Equals("Allow", StringComparison.OrdinalIgnoreCase)) return "allow";
            }
        }
        catch { }
        return "unknown";
    }

    private SelfCheckReport BuildSelfCheckReport()
    {
        var report = new SelfCheckReport();
        string runtime = ReadCurrentRuntimeSegment();
        WindowsHardwareProbe hardware = GetWindowsHardwareProbe();
        BridgeHealthSnapshot bridge = ReadKeyboardBridgeHealth();
        SessionHealth session = GetLatestSessionHealth();

        string componentError;
        bool versionsReady = AreCoreComponentsCurrent(out componentError);
        bool runtimeReady = !IsCapturing || IsStableCaptureRuntime(runtime);
        bool filesReady = File.Exists(Path.Combine(root, "VibeMicAtvvCapture.exe")) &&
            File.Exists(Path.Combine(root, "VoxDeckInputBridge.exe")) &&
            File.Exists(Path.Combine(root, "NAudio.Core.dll")) && File.Exists(Path.Combine(root, "NAudio.Wasapi.dll"));
        ProcessTopologySnapshot captureTopology = InspectProcessTopology("VibeMicAtvvCapture");
        ProcessTopologySnapshot bridgeTopology = InspectProcessTopology("VoxDeckInputBridge");
        bool processTopologyReady = captureTopology.ForeignCount == 0 && captureTopology.InaccessibleCount == 0 &&
            captureTopology.CurrentRootCount <= 1 && bridgeTopology.ForeignCount == 0 &&
            bridgeTopology.InaccessibleCount == 0 && bridgeTopology.CurrentRootCount <= 1;
        bool componentsReady = filesReady && versionsReady && runtimeReady && processTopologyReady;
        report.Items.Add(new SelfCheckItem("components", "本地核心组件与单实例状态",
            componentsReady ? "pass" : "fail",
            "主程序、语音捕获、按键桥接和 WASAPI 运行库版本一致，且只运行一个捕获会话",
            componentsReady ? "组件完整，稳定录音内核 v1.0.3 / 状态机 v11 已就绪" :
                !filesReady ? "安装目录缺少必要组件" : !versionsReady ? componentError : !runtimeReady ?
                "运行中的捕获组件不是当前状态机" : "检测到重复进程、其他安装目录进程或无法确认来源的进程",
            componentsReady ? "未发现组件缺失或版本争用" : !processTopologyReady ?
                "捕获进程：当前目录 " + captureTopology.CurrentRootCount + " / 其他目录 " + captureTopology.ForeignCount +
                "；按键桥接：当前目录 " + bridgeTopology.CurrentRootCount + " / 其他目录 " + bridgeTopology.ForeignCount :
                "文件缺失或版本不一致",
            componentsReady ? "无需操作" : processTopologyReady ? "重新安装完整发布包，然后重新自检" :
                "退出其他目录中的言灵和旧版桥接，再重新启动当前版本",
            componentsReady ? "" : processTopologyReady ? "重新下载" : "任务管理器",
            componentsReady ? "" : processTopologyReady ? "download-release" : "open-task-manager"));

        bool bluetoothConfirmedByBridge = bridge.Healthy && bridge.RawInputDevicePresent;
        string bluetoothState = ResolveBluetoothSelfCheckState(hardware, bridge);
        report.Items.Add(new SelfCheckItem("bluetooth", "Windows 蓝牙",
            bluetoothState,
            "电脑存在可用蓝牙适配器，Windows 蓝牙设备栈状态正常",
            bluetoothConfirmedByBridge ? "RC003 已通过 Windows HID / Raw Input 链路连接" :
                !hardware.Completed ? "正在读取 Windows 蓝牙设备状态" : hardware.Failed ? "Windows 硬件检测失败：" + hardware.Error :
                !hardware.BluetoothPresent ? "未检测到蓝牙适配器" : hardware.BluetoothOk ? "蓝牙适配器与设备栈可用" : "检测到蓝牙硬件，但当前状态异常",
            bluetoothConfirmedByBridge ? (hardware.Failed ? "通用设备查询失败，但实时 RC003 设备证据已确认蓝牙链路可用" : "未发现异常") :
                !hardware.Completed ? "硬件探测正在后台运行，页面不会被阻塞" : hardware.Failed ? "Windows 设备查询超时或被系统策略阻止" : !hardware.BluetoothPresent ? "当前电脑可能没有蓝牙，或驱动尚未安装" :
                hardware.BluetoothOk ? "未发现异常" : "蓝牙被禁用、驱动异常或设备管理器尚未完成初始化",
            bluetoothState == "pass" ? "无需操作" : bluetoothState == "checking" ? "等待检测完成，结果会自动刷新" : "打开 Windows 蓝牙设置，确认开关与驱动状态后返回",
            bluetoothState == "pass" || bluetoothState == "checking" ? "" : "蓝牙设置",
            bluetoothState == "pass" || bluetoothState == "checking" ? "" : "bluetooth"));

        bool runtimeConnected = bridgeReady || runtime.IndexOf("ATVV READY", StringComparison.OrdinalIgnoreCase) >= 0 ||
            runtime.IndexOf("status=Connected", StringComparison.OrdinalIgnoreCase) >= 0;
        bool remotePaired = hardware.RemotePresent || bridge.RawInputDevicePresent || runtimeConnected;
        string remoteState = runtimeConnected ? "pass" : remotePaired && IsCapturing ? "checking" : remotePaired ? "warning" : "fail";
        report.Items.Add(new SelfCheckItem("remote", "RC003 配对与连接",
            remoteState,
            "小米蓝牙语音遥控器已配对、已唤醒，并建立 ATVV 麦克风服务",
            runtimeConnected ? "RC003 已连接，ATVV 麦克风服务已就绪" : remotePaired ?
                "Windows 已识别遥控器，但言灵尚未确认实时语音连接" : "Windows 中未找到已配对的小米语音遥控器",
            runtimeConnected ? "未发现异常" : remotePaired ? "遥控器可能休眠，或蓝牙/GATT 正在恢复" : "遥控器尚未配对或配对记录已丢失",
            runtimeConnected ? "无需操作" : remotePaired ? "按方向键唤醒并等待自动连接" : "打开添加设备页面完成配对",
            runtimeConnected ? "" : remotePaired ? "重新连接" : "添加设备",
            runtimeConnected ? "" : remotePaired ? "start-bridge" : "pair-device"));

        bool keyboardRunning = IsCurrentProcessRunningFromRoot("VoxDeckInputBridge");
        bool recentInput = bridge.LastInputAgeSeconds <= 120 &&
            !string.Equals(bridge.LastInputKind, "keyboard_hook", StringComparison.OrdinalIgnoreCase);
        bool recentAction = bridge.LastRawActionAgeSeconds <= 120 &&
            bridge.RawActionEdges + bridge.FilterActionEdges > 0;
        bool mappingReady = string.IsNullOrWhiteSpace(expectedKeyboardConfigRevision) ||
            string.Equals(bridge.ConfigRevision, expectedKeyboardConfigRevision, StringComparison.OrdinalIgnoreCase);
        string configuredRoutingMode = NormalizeInputRoutingMode(config.inputRoutingMode);
        bool routingReady = string.Equals(bridge.InputRoutingMode, configuredRoutingMode, StringComparison.OrdinalIgnoreCase);
        bool authorityReady = string.Equals(bridge.RoutingAuthority, "raw_input", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(bridge.RoutingAuthority, "device_filter", StringComparison.OrdinalIgnoreCase);
        string isolationLabel = bridge.FilterHealthy ? "设备级精确隔离" : "Raw Input 安全直通";
        string keyState = !keyboardRunning || !bridge.Healthy || !mappingReady || !routingReady || !authorityReady
            ? "fail" : recentAction ? "pass" : "checking";
        report.Items.Add(new SelfCheckItem("keys", "遥控器按键监听",
            keyState,
            "配置 revision 已确认；动作只能由带 RC003 设备身份的 Raw Input 或专属过滤通道执行",
            !keyboardRunning ? "按键桥接进程未运行" : !bridge.Healthy ? "Hook 或 Raw Input 尚未就绪" : !mappingReady ?
                "界面配置尚未被桥接确认，期望 " + expectedKeyboardConfigRevision + "，实际 " + bridge.ConfigRevision :
                !routingReady ? "桥接按键模式尚未更新，期望 " + configuredRoutingMode + "，实际 " + bridge.InputRoutingMode :
                !authorityReady ? "桥接没有声明可验证的设备级动作来源" : recentAction ?
                "映射闭环正常 · " + isolationLabel + " · 最近动作 " + bridge.LastRawAction :
                recentInput ? "已收到 RC003 设备事件，正在等待一次已配置动作" :
                "桥接健康 · " + isolationLabel + " · 正在等待实体按键",
            !keyboardRunning ? "后台桥接没有启动" : !bridge.Healthy ? "蓝牙重连后监听尚未恢复" : !mappingReady ?
                "桥接仍在使用旧配置，或配置重新加载失败" :
                !routingReady ? "桥接尚未确认当前按键来源模式" :
                !authorityReady ? "运行中的按键桥接版本过旧，仍可能出现界面与真机行为不一致" :
                recentInput ? "设备识别正常，但还没有动作执行证据" : "尚无可用于验证的近期 RC003 按键",
            keyState == "pass" ? "无需操作" : keyState == "checking" ?
                "按一次已配置的 Home、TV、功能键或自定义方向键，然后返回自动复检" :
                "重建按键监听后再按一次已配置按键",
            keyState == "pass" ? "" : keyState == "checking" ? "验证按键" : "重建监听",
            keyState == "pass" ? "" : "test-remote"));

        string permission = MicrophonePermissionState();
        bool realAudio = session.AudioMs >= MinimumUsefulAudioMs && session.OutputRmsPercent > 0;
        string microphoneState = permission == "deny" ? "fail" : realAudio ? "pass" : "checking";
        report.Items.Add(new SelfCheckItem("microphone", "遥控器麦克风真实音频",
            microphoneState,
            "Windows 允许麦克风访问，UI 仅在真实音频到达时显示“正在收音”，并记录有效电平",
            permission == "deny" ? "Windows 当前拒绝麦克风访问" : realAudio ?
                "最近收到 " + FormatMillisecondsAsSeconds(session.AudioMs) + " 真实音频，输出电平 " + FormatPercent(session.OutputRmsPercent) :
                runtimeConnected ? "麦克风服务已连接，但还没有可验证的真实声音" : "正在等待 RC003 麦克风连接",
            permission == "deny" ? "Windows 隐私设置阻止麦克风" : realAudio ? "未发现无声或假波形" :
                runtimeConnected ? "尚未完成一次按住说话测试" : "上游蓝牙/ATVV 链路尚未就绪",
            permission == "deny" ? "打开麦克风权限并允许桌面应用访问" : realAudio ? "无需操作" : "完成一次按住说话、松开结束的真实测试",
            microphoneState == "pass" ? "" : permission == "deny" ? "麦克风权限" : "开始测试",
            microphoneState == "pass" ? "" : permission == "deny" ? "microphone-permission" : "test-dictation"));

        bool cableInput = HasCableInput();
        bool cableOutput = HasCableOutput();
        bool cableReady = cableInput && cableOutput;
        report.Items.Add(new SelfCheckItem("cable", "VB-CABLE 本地音频通道",
            cableReady ? "pass" : "fail",
            "同时检测到 CABLE Input（播放端）与 CABLE Output（录音端）",
            cableReady ? "CABLE Input 与 CABLE Output 均已启用" :
                "缺少 " + (!cableInput && !cableOutput ? "CABLE Input 和 CABLE Output" : !cableInput ? "CABLE Input" : "CABLE Output"),
            cableReady ? "未发现异常" : "VB-CABLE 未安装、安装后未重启，或音频设备被禁用",
            cableReady ? "无需操作" : "安装官方驱动；如提示重启，登录后会自动继续教程",
            cableReady ? "" : "安装 VB-CABLE", cableReady ? "" : "install-cable"));

        bool stableProfile = HasStableVoiceProfile(config);
        report.Items.Add(new SelfCheckItem("profile", "已验证稳定语音参数",
            stableProfile ? "pass" : "warning",
            "1.0×、清晰增强、180 ms 排空、CABLE Input、自动路由和按住模式保持发布基线",
            stableProfile ? "稳定语音档案 v" + StableVoiceProfileVersion + " 已应用" : "当前音频参数已偏离稳定基线",
            stableProfile ? "未发现异常" : "高级参数被修改，可能影响灵敏度、延迟或路由恢复",
            stableProfile ? "无需操作" : "一键恢复已反复验证的稳定参数",
            stableProfile ? "" : "恢复稳定参数", stableProfile ? "" : "restore-profile"));

        string provider = NormalizeProviderKey(config.inputMethod);
        bool validHotkey = IsValidTranscriptionHotkey(config.inputMethodHotkey);
        bool providerRunning = IsProviderRunning(provider);
        bool providerKnown = provider != "custom";
        string providerState = !validHotkey ? "fail" : providerKnown && providerRunning ? "pass" : "warning";
        report.Items.Add(new SelfCheckItem("provider", "默认语音工具与快捷键",
            providerState,
            "所选工具已安装或运行，言灵快捷键与工具中的全局快捷键完全一致",
            !validHotkey ? "快捷键格式无效" : providerRunning ? ProviderDisplayName(provider) + " 已就绪 · " +
                config.inputMethodHotkey.Replace("+", " + ") + " · " + (config.inputMethodTrigger == "hold" ? "按住触发" : "单击切换") :
                "未检测到运行中的 " + ProviderDisplayName(provider),
            !validHotkey ? "无法可靠启动和结束语音工具" : providerRunning ?
                providerKnown ? "未发现异常" : "自定义工具无法自动确认其内部快捷键" : "工具未启动、未安装或进程名无法识别",
            providerState == "pass" ? "无需操作" : "打开语音页核对工具、快捷键和触发方式",
            providerState == "pass" ? "" : "检查语音工具", providerState == "pass" ? "" : "provider"));

        bool startupReady = IsLaunchAtStartupRegistered();
        string startupState = startupReady ? "pass" : config.launchAtStartup ? "fail" : "warning";
        report.Items.Add(new SelfCheckItem("startup", "Windows 登录后自动可用",
            startupState,
            "登录后后台启动，不弹主窗口，并自动恢复遥控器、按键监听和音频配置",
            startupReady ? "启动项路径有效，后台启动参数正确" : config.launchAtStartup ?
                "已选择自动启动，但注册表启动项缺失或路径失效" : "当前设置为手动启动",
            startupReady ? "未发现异常" : config.launchAtStartup ? "安装目录移动或启动项写入失败" : "用户当前选择手动启动，不影响本次使用",
            startupReady ? "无需操作" : config.launchAtStartup ? "一键修复当前安装路径的后台启动项" : "如需开机即用，可一键开启",
            startupReady ? "" : config.launchAtStartup ? "修复自启动" : "开启自启动",
            startupReady ? "" : "startup"));

        string sessionState;
        if (!session.Started) sessionState = "checking";
        else if (session.Failed || session.TransportFailed || session.DeliveryFailed) sessionState = "fail";
        else if (session.Success) sessionState = "pass";
        else sessionState = "checking";
        string sessionActual = !session.Started ? "尚无真实端到端听写记录" : session.Success ?
            "最近自动链路通过 · 音频 " + FormatMillisecondsAsSeconds(session.AudioMs) + " · 工具响应 " + FormatMilliseconds(session.TriggerToReadyMs) :
            session.Completed ? "最近会话已结束，但链路指标未全部通过" : "最近会话仍在进行或等待转译完成";
        report.Items.Add(new SelfCheckItem("session", "音频与语音工具唤起链路",
            sessionState,
            "按下一次只创建一个会话；真实音频送达；松开只结束一次；语音工具收到开始与结束指令",
            sessionActual,
            sessionState == "pass" ? "未发现双会话、音频丢失、路由恢复或工具唤起异常；应用不会读取输入框文字" :
                !session.Started ? "尚未进行发布版真实测试" : session.NextAction,
            sessionState == "pass" ? "请在目标输入框目视确认文字与所选工具的整理效果" : "聚焦输入框，按住录音键说一句完整的话，松开后等待转译",
            sessionState == "pass" ? "" : "真实链路测试", sessionState == "pass" ? "" : "test-dictation"));

        foreach (SelfCheckItem item in report.Items)
        {
            if (item.State == "pass") report.PassedCount++;
            else if (item.State == "fail") report.FailedCount++;
            else if (item.State == "checking") report.CheckingCount++;
            else if (item.State == "unsupported") report.UnsupportedCount++;
            else report.WarningCount++;
        }
        if (report.FailedCount > 0)
        {
            report.Headline = "发现 " + report.FailedCount + " 项错误";
            report.Detail = "从第一项错误开始修复；返回言灵后会自动复检，不读取你的转译文字。";
        }
        else if (report.CheckingCount > 0)
        {
            report.Headline = "核心链路可用，等待 " + report.CheckingCount + " 项真实验证";
            report.Detail = (report.WarningCount > 0 ? "另有 " + report.WarningCount + " 项可选设置。" : "") +
                "按对应卡片操作一次，即可确认按键、音频或工具唤起状态。";
        }
        else if (report.WarningCount > 0 || report.UnsupportedCount > 0)
        {
            report.Headline = "核心链路可用，还有配置需要确认";
            report.Detail = "橙色项目不会隐藏问题；完成建议配置后可获得开机即用体验。";
        }
        else
        {
            report.Headline = "自动检查全部通过，可以稳定使用";
            report.Detail = "蓝牙、遥控器、真实音频、VB-CABLE、工具唤起与启动恢复均正常；最终文字请目视确认。";
        }
        return report;
    }

    private SelfCheckReport BuildSelfCheckReportLegacy()
    {
        var report = new SelfCheckReport();
        string currentRuntime = ReadCurrentRuntimeSegment();
        bool stableRuntime = !IsCapturing || string.IsNullOrWhiteSpace(currentRuntime) ||
            currentRuntime.IndexOf("voice_state_machine=v11", StringComparison.OrdinalIgnoreCase) >= 0;
        bool continuousRuntimeReady = !UsesLongDictation(config.voiceMode) || !IsCapturing ||
            string.IsNullOrWhiteSpace(currentRuntime) ||
            currentRuntime.IndexOf("long_dictation_state_machine=v2", StringComparison.OrdinalIgnoreCase) >= 0 ||
            currentRuntime.IndexOf("long_dictation_state_machine=v3", StringComparison.OrdinalIgnoreCase) >= 0;
        string componentVersionError;
        bool componentVersionsReady = AreCoreComponentsCurrent(out componentVersionError);
        bool componentsReady = File.Exists(Path.Combine(root, "VibeMicAtvvCapture.exe")) &&
            File.Exists(Path.Combine(root, "VoxDeckInputBridge.exe")) &&
            File.Exists(Path.Combine(root, "NAudio.Core.dll")) &&
            File.Exists(Path.Combine(root, "NAudio.Wasapi.dll")) && stableRuntime && continuousRuntimeReady && componentVersionsReady;
        report.Items.Add(new SelfCheckItem("components", "本地核心组件",
            componentsReady ? "pass" : "fail",
            componentsReady ? "语音捕获、按键桥接与 WASAPI 运行库完整，语音状态机已就绪" :
                !componentVersionsReady ? componentVersionError :
                !stableRuntime ? "当前捕获组件不是已验证的 v11，请重新安装完整发布包" :
                !continuousRuntimeReady ? "持续听写组件不是当前音频实况状态机，请重新安装完整发布包" :
                "安装目录缺少必要组件，请重新解压完整发布包",
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

        bool startupReady = IsLaunchAtStartupRegistered();
        report.Items.Add(new SelfCheckItem("startup", "Windows 登录后自动启动",
            startupReady ? "pass" : config.launchAtStartup ? "fail" : "warning",
            startupReady ? "已注册后台启动，登录后会自动等待蓝牙、音频和按键服务" :
                config.launchAtStartup ? "已选择开机启动，但 Windows 启动项未找到或路径已失效" :
                "尚未开启开机启动；每次登录后需要手动打开言灵",
            startupReady ? "" : "开启自启动", startupReady ? "" : "startup"));

        bool keyboardRunning = IsCurrentProcessRunningFromRoot("VoxDeckInputBridge");
        BridgeHealthSnapshot bridgeHealth = ReadKeyboardBridgeHealth();
        bool servicesReady = IsCapturing && keyboardRunning && bridgeHealth.Healthy;
        bool recentRemoteInput = bridgeHealth.LastInputAgeSeconds <= 120;
        string serviceState = !servicesReady ? "fail" : recentRemoteInput ? "pass" : "warning";
        string bridgeDetail = !keyboardRunning ? "按键桥接进程未运行" :
            !bridgeHealth.Healthy ? "按键桥接正在恢复（健康文件、Hook 或 Raw Input 尚未就绪）" :
            recentRemoteInput ?
                "语音桥接与遥控器按键桥接均在运行，最近收到 " + bridgeHealth.LastInputKind + " 事件" :
                "桥接进程健康，但尚未收到最近遥控器事件；请按一次任意遥控器键验证";
        report.Items.Add(new SelfCheckItem("services", "后台桥接服务",
            serviceState,
            servicesReady ? bridgeDetail :
            (!IsCapturing ? "语音桥接未运行" : bridgeDetail),
            serviceState == "fail" ? "启动桥接" : serviceState == "warning" ? "验证按键" : "",
            serviceState == "fail" ? "start-bridge" : serviceState == "warning" ? "test-remote" : ""));

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
            sessionDetail = UsesLongDictation(config.voiceMode)
                ? "尚无真实听写记录；请单击录音键开始，说一句话后再按一次结束"
                : "尚无真实听写记录；需要按住录音键说一句话才能验证完整链路";
            sessionAction = "test-dictation";
            sessionActionText = "开始测试";
        }
        else
        {
            bool routeHealthy = !config.autoRouteVirtualMicrophone ||
                (health.RouteAcquired && health.RouteRestored && !health.RouteRestorePending);
            bool transportHealthy = health.QueueDrops == 0 && health.SinkQueueDrops == 0 &&
                health.MaxGapMs <= 250 && health.PendingAfterDrain == 0 && health.Drained &&
                !health.TransportFailed &&
                (!UsesLongDictation(config.voiceMode) || health.ElapsedMs < 30000 ||
                    health.AudioCoveragePercent >= 80.0);
            bool levelHealthy = health.OutputRmsPercent >= 0.8;
            bool timingHealthy = health.TriggerToReadyMs <= 1500;
            bool durationHealthy = health.AudioMs >= MinimumUsefulAudioMs;
            bool inputTargetHealthy = provider != "wechat" || !health.InputTargetObserved || health.InputTargetReady;
            if (health.Failed || (health.Completed && (!routeHealthy || !transportHealthy))) sessionState = "fail";
            else if (health.Success && durationHealthy && levelHealthy && timingHealthy && inputTargetHealthy) sessionState = "pass";
            else sessionState = "warning";
            sessionDetail = "响应 " + FormatMilliseconds(health.TriggerToReadyMs) + " · 输出 " + FormatPercent(health.OutputRmsPercent) +
                " · 蓝牙间隔 " + FormatMilliseconds(health.MaxGapMs) + " · 丢包 " + Math.Max(0, health.QueueDrops + health.SinkQueueDrops) +
                (UsesLongDictation(config.voiceMode) && health.ElapsedMs > 0 ?
                    " · 音频覆盖 " + health.AudioCoveragePercent.ToString("0.0") + "%" : "") +
                (health.Success ? " · 音频已送达" + (provider == "wechat" && health.InputTargetObserved ? health.InputTargetReady ? " · 回填目标已恢复" : " · 回填目标待恢复" : "") :
                " · " + health.NextAction);
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

    private bool AreCoreComponentsCurrent(out string error)
    {
        error = "";
        string[] paths =
        {
            Application.ExecutablePath,
            Path.Combine(root, "VibeMicAtvvCapture.exe"),
            Path.Combine(root, "VoxDeckInputBridge.exe")
        };
        foreach (string path in paths)
        {
            try
            {
                if (!File.Exists(path))
                {
                    error = "核心组件缺失：" + Path.GetFileName(path) + "，请重新安装完整发布包";
                    return false;
                }
                FileVersionInfo info = FileVersionInfo.GetVersionInfo(path);
                string version = string.IsNullOrWhiteSpace(info.ProductVersion) ? info.FileVersion : info.ProductVersion;
                string expectedVersion = Path.GetFileName(path).Equals("VibeMicAtvvCapture.exe",
                    StringComparison.OrdinalIgnoreCase) ? StableCaptureBinaryVersion : ProductRelease;
                if (string.IsNullOrWhiteSpace(version) ||
                    !version.StartsWith(expectedVersion, StringComparison.OrdinalIgnoreCase))
                {
                    error = "核心组件版本不一致：" + Path.GetFileName(path) + "（" + (version ?? "未知") +
                        "，应为 " + expectedVersion + "），请重新安装完整发布包";
                    return false;
                }
            }
            catch (Exception ex)
            {
                error = "无法校验核心组件：" + Path.GetFileName(path) + "（" + ex.Message + "）";
                return false;
            }
        }
        return true;
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
        row.Size = new Size(924, 104);
        row.BackColor = item.State == "fail" ? StatusSurface("error") :
            item.State == "warning" ? StatusSurface("connecting") :
            item.State == "checking" ? StatusSurface("recovering") :
            item.State == "unsupported" ? surfaceBackground : Color.Transparent;
        row.Paint += delegate(object sender, PaintEventArgs e)
        {
            using (var pen = new Pen(line))
                e.Graphics.DrawLine(pen, 0, row.Height - 1, row.Width, row.Height - 1);
        };
        Color statusColor = item.State == "pass" ? green : item.State == "fail" ? coral :
            item.State == "checking" ? cyan : item.State == "unsupported" ? Color.FromArgb(142, 151, 170) : amber;
        string statusGlyph = item.State == "pass" ? "✓" : item.State == "fail" ? "!" :
            item.State == "checking" ? "…" : item.State == "unsupported" ? "–" : "·";
        string statusText = item.State == "pass" ? "正常" : item.State == "fail" ? "错误" :
            item.State == "checking" ? "正在检测" : item.State == "unsupported" ? "不支持" : "需要配置";
        var mark = NewLabel(statusGlyph, 10f, FontStyle.Bold, Color.White);
        mark.Location = new Point(6, 12);
        mark.Size = new Size(30, 30);
        mark.TextAlign = ContentAlignment.MiddleCenter;
        mark.BackColor = statusColor;
        ApplyRoundedRegion(mark, 15);
        var title = NewLabel(item.Title, 9.6f, FontStyle.Bold, ink);
        title.Location = new Point(50, 5);
        title.Size = new Size(500, 24);
        var state = NewLabel("●  " + statusText, 8.2f, FontStyle.Bold, statusColor);
        state.Location = new Point(564, 5);
        state.Size = new Size(126, 24);
        state.TextAlign = ContentAlignment.MiddleRight;
        var expected = NewLabel("正确状态：" + item.Expected, 7.9f, FontStyle.Regular, muted);
        expected.Location = new Point(50, 29);
        expected.Size = new Size(690, 19);
        expected.AutoEllipsis = true;
        var actual = NewLabel("当前状态：" + item.Actual, 7.9f, FontStyle.Bold, item.State == "fail" ? coral : ink);
        actual.Location = new Point(50, 48);
        actual.Size = new Size(690, 19);
        actual.AutoEllipsis = true;
        var cause = NewLabel("原因：" + item.Cause, 7.9f, FontStyle.Regular, muted);
        cause.Location = new Point(50, 67);
        cause.Size = new Size(690, 19);
        cause.AutoEllipsis = true;
        var nextStep = NewLabel("下一步：" + item.NextStep, 7.9f, FontStyle.Bold, statusColor);
        nextStep.Location = new Point(50, 85);
        nextStep.Size = new Size(690, 19);
        nextStep.AutoEllipsis = true;
        row.Controls.Add(mark);
        row.Controls.Add(title);
        row.Controls.Add(state);
        row.Controls.Add(expected);
        row.Controls.Add(actual);
        row.Controls.Add(cause);
        row.Controls.Add(nextStep);
        if (!string.IsNullOrEmpty(item.Action))
        {
            var action = SecondaryButton(item.ActionText, new Point(762, 31), new Size(144, 40));
            action.Font = new Font("Microsoft YaHei UI", 8.3f, FontStyle.Bold);
            action.Click += delegate { HandleSelfCheckAction(item.Action); };
            row.Controls.Add(action);
        }
        parent.Controls.Add(row);
    }

    private void AddSelfCheckRowLegacy(Control parent, SelfCheckItem item, int y)
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
        else if (action == "startup")
        {
            config.launchAtStartup = true;
            SetLaunchAtStartup(true);
            SaveConfig();
            ShowToast(IsLaunchAtStartupRegistered() ? "已开启 Windows 登录后自动启动" : "启动项写入失败，请检查 Windows 权限", IsLaunchAtStartupRegistered() ? "success" : "error");
            ShowPage(PageSettings);
        }
        else if (action == "download-release") OpenUri("https://github.com/richlearntodo-debug/vibe-flow/releases/latest");
        else if (action == "open-task-manager")
        {
            try { Process.Start(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true }); }
            catch (Exception ex) { ShowToast("无法打开任务管理器：" + ex.Message, "error"); }
        }
        else if (action == "install-cable")
        {
            config.onboardingStep = 2;
            config.resumeSetupAfterRestart = !config.setupCompleted;
            SaveConfig();
            if (!config.setupCompleted) SetLaunchAtStartup(true);
            LaunchVBCableInstaller();
        }
        else if (action == "restore-profile")
        {
            ApplyStableVoiceProfile(config);
            SaveConfig();
            RestartCaptureForAudioSettings();
            ShowPage(PageSelfCheck);
            ShowToast("已恢复真机验证的稳定语音参数 v" + StableVoiceProfileVersion, "success");
        }
        else if (action == "start-bridge")
        {
            StartKeyboardBridge();
            if (!IsCapturing) StartCapture();
            ShowToast("正在启动后台桥接，请稍候后重新自检", "info");
        }
        else if (action == "bluetooth" || action == "pair-device")
        {
            refreshSelfCheckOnActivate = true;
            OpenUri("ms-settings:bluetooth");
        }
        else if (action == "microphone-permission")
        {
            refreshSelfCheckOnActivate = true;
            OpenUri("ms-settings:privacy-microphone");
        }
        else if (action == "sound-settings")
        {
            refreshSelfCheckOnActivate = true;
            OpenUri("ms-settings:sound");
        }
        else if (action == "provider") ShowPage(PageVoice);
        else if (action == "test-remote")
        {
            RestartKeyboardBridge("self_check_remote");
            if (!IsCapturing) StartCapture();
            ShowPage(PageSelfCheck);
            ShowToast("按键桥接已重建，请按一次遥控器方向键完成验证", "info");
        }
        else if (action == "test-dictation")
        {
            ShowPage(PageHome);
            ShowToast("聚焦任意输入框，按住遥控器录音键说话，松开后等待转译", "info");
        }
    }

    private void RunSelfCheckAndRefresh()
    {
        windowsHardwareProbeAt = DateTime.MinValue;
        SelfCheckReport report = BuildSelfCheckReport();
        ShowPage(PageSelfCheck);
        if (report.FailedCount == 0 && report.WarningCount == 0 && report.CheckingCount == 0 && report.UnsupportedCount == 0)
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

    private bool IsCurrentProcessRunningFromRoot(string processName)
    {
        string expected = Path.GetFullPath(Path.Combine(root, processName + ".exe"));
        bool found = false;
        foreach (Process process in Process.GetProcessesByName(processName))
        {
            try
            {
                if (!process.HasExited && Path.GetFullPath(process.MainModule.FileName)
                    .Equals(expected, StringComparison.OrdinalIgnoreCase)) found = true;
            }
            catch { }
            finally { process.Dispose(); }
        }
        return found;
    }

    private ProcessTopologySnapshot InspectProcessTopology(string processName)
    {
        var snapshot = new ProcessTopologySnapshot();
        string expected = Path.GetFullPath(Path.Combine(root, processName + ".exe"));
        foreach (Process process in Process.GetProcessesByName(processName))
        {
            snapshot.TotalCount++;
            try
            {
                string actual = Path.GetFullPath(process.MainModule.FileName);
                if (actual.Equals(expected, StringComparison.OrdinalIgnoreCase)) snapshot.CurrentRootCount++;
                else snapshot.ForeignCount++;
            }
            catch { snapshot.InaccessibleCount++; }
            finally { process.Dispose(); }
        }
        return snapshot;
    }

    private BridgeHealthSnapshot ReadKeyboardBridgeHealth()
    {
        var snapshot = new BridgeHealthSnapshot();
        string path = Path.Combine(root, "input-bridge-health.json");
        try
        {
            if (!File.Exists(path)) return snapshot;
            snapshot.FileAgeSeconds = Math.Max(0, (DateTime.UtcNow - File.GetLastWriteTimeUtc(path)).TotalSeconds);
            Dictionary<string, object> data;
            string readError;
            TryReadBridgeHealth(path, out data, out readError);
            if (data == null) return snapshot;
            object value;
            if (data.TryGetValue("state", out value)) snapshot.State = Convert.ToString(value);
            if (data.TryGetValue("hook_installed", out value)) snapshot.HookInstalled = Convert.ToBoolean(value);
            if (data.TryGetValue("raw_input_registered", out value)) snapshot.RawInputRegistered = Convert.ToBoolean(value);
            if (data.TryGetValue("raw_input_device_present", out value)) snapshot.RawInputDevicePresent = Convert.ToBoolean(value);
            if (data.TryGetValue("input_routing_mode", out value)) snapshot.InputRoutingMode = Convert.ToString(value);
            if (data.TryGetValue("routing_authority", out value)) snapshot.RoutingAuthority = Convert.ToString(value);
            if (data.TryGetValue("routing_isolation", out value)) snapshot.RoutingIsolation = Convert.ToString(value);
            if (data.TryGetValue("raw_remote_edges", out value)) snapshot.RawRemoteEdges = Convert.ToInt64(value);
            if (data.TryGetValue("raw_action_edges", out value)) snapshot.RawActionEdges = Convert.ToInt64(value);
            if (data.TryGetValue("filter_action_edges", out value)) snapshot.FilterActionEdges = Convert.ToInt64(value);
            if (data.TryGetValue("hook_candidate_passthroughs", out value)) snapshot.HookCandidatePassthroughs = Convert.ToInt64(value);
            if (data.TryGetValue("last_raw_action", out value)) snapshot.LastRawAction = Convert.ToString(value);
            if (data.TryGetValue("last_action_source", out value)) snapshot.LastActionSource = Convert.ToString(value);
            if (data.TryGetValue("last_execution_sequence", out value)) snapshot.LastExecutionSequence = Convert.ToInt64(value);
            if (data.TryGetValue("last_execution_button", out value)) snapshot.LastExecutionButton = Convert.ToString(value);
            if (data.TryGetValue("last_execution_label", out value)) snapshot.LastExecutionLabel = Convert.ToString(value);
            if (data.TryGetValue("last_execution_trigger", out value)) snapshot.LastExecutionTrigger = Convert.ToString(value);
            if (data.TryGetValue("last_execution_action", out value)) snapshot.LastExecutionAction = Convert.ToString(value);
            if (data.TryGetValue("last_execution_source", out value)) snapshot.LastExecutionSource = Convert.ToString(value);
            if (data.TryGetValue("last_execution_profile_id", out value)) snapshot.LastExecutionProfileId = Convert.ToString(value);
            if (data.TryGetValue("last_execution_profile_name", out value)) snapshot.LastExecutionProfileName = Convert.ToString(value);
            if (data.TryGetValue("last_execution_revision", out value)) snapshot.LastExecutionRevision = Convert.ToString(value);
            if (data.TryGetValue("last_execution_success", out value)) snapshot.LastExecutionSuccess = Convert.ToBoolean(value);
            if (data.TryGetValue("last_execution_at", out value))
            {
                DateTime parsed;
                if (DateTime.TryParse(Convert.ToString(value), CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out parsed))
                {
                    snapshot.LastExecutionAtUtc = parsed.ToUniversalTime();
                    snapshot.LastExecutionAgeSeconds = Math.Max(0,
                        (DateTime.UtcNow - snapshot.LastExecutionAtUtc).TotalSeconds);
                }
            }
            if (data.TryGetValue("rc003_filter_available", out value)) snapshot.FilterAvailable = Convert.ToBoolean(value);
            if (data.TryGetValue("rc003_filter_healthy", out value)) snapshot.FilterHealthy = Convert.ToBoolean(value);
            if (data.TryGetValue("rc003_filter_state", out value)) snapshot.FilterState = Convert.ToString(value);
            if (data.TryGetValue("config_version", out value)) snapshot.ConfigVersion = Convert.ToInt32(value);
            if (data.TryGetValue("config_revision", out value)) snapshot.ConfigRevision = Convert.ToString(value);
            if (data.TryGetValue("config_mapping_count", out value)) snapshot.ConfigMappingCount = Convert.ToInt32(value);
            if (data.TryGetValue("config_error", out value)) snapshot.ConfigError = Convert.ToString(value);
            if (data.TryGetValue("install_root", out value)) snapshot.InstallRoot = Convert.ToString(value);
            if (data.TryGetValue("config_loaded_at", out value))
            {
                DateTime loadedAt;
                if (DateTime.TryParse(Convert.ToString(value), CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out loadedAt)) snapshot.ConfigLoadedAtUtc = loadedAt.ToUniversalTime();
            }
            if (data.TryGetValue("last_input_kind", out value)) snapshot.LastInputKind = Convert.ToString(value);
            if (data.TryGetValue("last_input_at", out value))
            {
                DateTime parsed;
                if (DateTime.TryParse(Convert.ToString(value), CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out parsed))
                {
                    snapshot.LastInputAtUtc = parsed.ToUniversalTime();
                    snapshot.LastInputAgeSeconds = Math.Max(0, (DateTime.UtcNow - snapshot.LastInputAtUtc).TotalSeconds);
                }
            }
            if (data.TryGetValue("last_raw_action_at", out value))
            {
                DateTime parsed;
                if (DateTime.TryParse(Convert.ToString(value), CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out parsed))
                {
                    snapshot.LastRawActionAtUtc = parsed.ToUniversalTime();
                    snapshot.LastRawActionAgeSeconds = Math.Max(0,
                        (DateTime.UtcNow - snapshot.LastRawActionAtUtc).TotalSeconds);
                }
            }
            snapshot.Healthy = snapshot.FileAgeSeconds <= 7 &&
                string.Equals(snapshot.State, "running", StringComparison.OrdinalIgnoreCase) &&
                snapshot.HookInstalled && snapshot.RawInputRegistered &&
                (string.Equals(snapshot.RoutingAuthority, "raw_input", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(snapshot.RoutingAuthority, "device_filter", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            Log("Bridge health read failed: " + ex.Message);
        }
        return snapshot;
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
        int longStartIndex = -1;
        int longEndIndex = -1;
        int segmentStopIndex = -1;
        int audioLiveIndex = -1;
        int transportHealthIndex = -1;
        bool transportHealthLive = false;
        int captureBoundaryIndex = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].IndexOf(" START endpoint=", StringComparison.OrdinalIgnoreCase) >= 0)
                captureBoundaryIndex = i;
        }
        for (int i = Math.Max(captureBoundaryIndex, Math.Max(0, lines.Length - 500)); i < lines.Length; i++)
        {
            if (lines[i].IndexOf("REMOTE STREAM START session=", StringComparison.OrdinalIgnoreCase) >= 0) startIndex = i;
            if (lines[i].IndexOf("REMOTE STREAM STOP session=", StringComparison.OrdinalIgnoreCase) >= 0) stopIndex = i;
            if (lines[i].IndexOf("LONG DICTATION START generation=", StringComparison.OrdinalIgnoreCase) >= 0) longStartIndex = i;
            if (lines[i].IndexOf("LONG DICTATION END generation=", StringComparison.OrdinalIgnoreCase) >= 0) longEndIndex = i;
            if (lines[i].IndexOf("REMOTE STREAM SEGMENT STOP session=", StringComparison.OrdinalIgnoreCase) >= 0) segmentStopIndex = i;
            if (lines[i].IndexOf("AUDIO LIVE START session=", StringComparison.OrdinalIgnoreCase) >= 0) audioLiveIndex = i;
            if (lines[i].IndexOf("AUDIO TRANSPORT HEALTH", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                transportHealthIndex = i;
                transportHealthLive = string.Equals(ExtractMetric(lines[i], "audio_live"), "True",
                    StringComparison.OrdinalIgnoreCase);
            }
        }

        bool longSessionActive = UsesLongDictation(config.voiceMode) && longStartIndex > longEndIndex;
        bool longAudioLive = longSessionActive &&
            ((transportHealthIndex > segmentStopIndex && transportHealthLive) ||
             (audioLiveIndex > segmentStopIndex && audioLiveIndex > transportHealthIndex));
        if (IsCapturing && (longAudioLive || (!longSessionActive && startIndex > stopIndex)))
        {
            DateTime parsed;
            int timingIndex = longSessionActive ? longStartIndex : startIndex;
            if (TryParseRuntimeTimestamp(lines[timingIndex], out parsed)) activeStreamStarted = parsed;
            TimeSpan elapsed = activeStreamStarted == DateTime.MinValue ? TimeSpan.Zero : DateTime.Now - activeStreamStarted;
            activityLabel.Text = "●  正在听写  " + Math.Max(0, (int)elapsed.TotalMinutes).ToString("00") + ":" + Math.Max(0, elapsed.Seconds).ToString("00") + "  ·  遥控器音频正在到达";
            activityLabel.ForeColor = violet;
            if (heroTitle != null && !heroTitle.IsDisposed) heroTitle.Text = "正在听写";
            if (heroSubtitle != null && !heroSubtitle.IsDisposed)
                heroSubtitle.Text = "请自然说话，松开录音键后由 " + ProviderDisplayName(config.inputMethod) + " 整理文字";
            if (heroStateLabel != null && !heroStateLabel.IsDisposed) heroStateLabel.Text = "LISTENING";
            ApplyVisualState("recording");
            return;
        }

        if (IsCapturing && longSessionActive)
        {
            DateTime parsed;
            if (TryParseRuntimeTimestamp(lines[longStartIndex], out parsed)) activeStreamStarted = parsed;
            TimeSpan elapsed = activeStreamStarted == DateTime.MinValue ? TimeSpan.Zero : DateTime.Now - activeStreamStarted;
            activityLabel.Text = "●  按住说话  " + Math.Max(0, (int)elapsed.TotalMinutes).ToString("00") + ":" +
                Math.Max(0, elapsed.Seconds).ToString("00") + "  ·  音频恢复中，请稍候";
            activityLabel.ForeColor = cyan;
            if (heroTitle != null && !heroTitle.IsDisposed) heroTitle.Text = "正在恢复遥控器音频";
            if (heroSubtitle != null && !heroSubtitle.IsDisposed)
                heroSubtitle.Text = "转写工具保持开启；检测到真实音频后会自动继续输入";
            if (heroStateLabel != null && !heroStateLabel.IsDisposed) heroStateLabel.Text = "RECONNECTING AUDIO";
            ApplyVisualState("recovering");
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
            SessionHealth latest = UsesLongDictation(config.voiceMode) ? GetLatestSessionHealth() : null;
            if (latest != null && latest.AudioMs > 0)
            {
                activityLabel.Text = "上一段听写 " + FormatMillisecondsAsSeconds(latest.AudioMs) +
                    (latest.SegmentCount > 1 ? "  ·  " + latest.SegmentCount + " 个音频分段" : "") +
                    "  ·  输出电平 " + FormatPercent(latest.OutputRmsPercent);
                activityLabel.ForeColor = muted;
                return;
            }
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
        try
        {
            RotateLogIfNeeded(hostLogPath, MaxHostLogBytes);
            File.AppendAllText(hostLogPath, timestamp + " " + message + Environment.NewLine, new UTF8Encoding(false));
        }
        catch { }
    }

    private static void RotateLogIfNeeded(string path, long maximumBytes)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        if (new FileInfo(path).Length <= maximumBytes) return;
        string previous = path + ".1";
        if (File.Exists(previous)) File.Delete(previous);
        File.Move(path, previous);
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
        Color accent = kind == "error" ? Color.FromArgb(202, 76, 76) : kind == "success" ? green : kind == "warning" ? amber : violet;
        toastIcon.Text = kind == "error" ? "\uEA39" : kind == "success" ? "\uE73E" : kind == "warning" ? "\uE7BA" : "\uE946";
        toastIcon.ForeColor = accent;
        toastPanel.BorderColor = Color.FromArgb(accent.R, accent.G, accent.B);
        toastPanel.BackColor = kind == "error" ? StatusSurface("error") : kind == "success" ?
            StatusSurface("ready") : kind == "warning" ? StatusSurface("connecting") : cardBackground;
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
            dictationStopSound = CreateFeedbackWave("stop");
            dictationCompleteSound = CreateFeedbackWave("success");
            dictationErrorSound = CreateFeedbackWave("error");
            dictationStopPlayer = new SoundPlayer(dictationStopSound);
            dictationCompletePlayer = new SoundPlayer(dictationCompleteSound);
            dictationErrorPlayer = new SoundPlayer(dictationErrorSound);
            dictationStopPlayer.Load();
            dictationCompletePlayer.Load();
            dictationErrorPlayer.Load();
        }
        catch
        {
            dictationStopPlayer = null;
            dictationCompletePlayer = null;
            dictationErrorPlayer = null;
        }
    }

    private static MemoryStream CreateFeedbackWave(string cue)
    {
        const int sampleRate = 22050;
        int durationMs = cue == "stop" ? 320 : cue == "success" ? 180 : 260;
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
            double phase = 0.0;
            for (int i = 0; i < sampleCount; i++)
            {
                double elapsed = i * 1000.0 / sampleRate;
                double progress = elapsed / durationMs;
                double frequency;
                double envelope;
                double amplitude;
                double harmonic;
                if (cue == "stop")
                {
                    frequency = elapsed < 145.0 ? 660.0 : 880.0;
                    envelope = Math.Max(0.0, Math.Min(Math.Min(1.0, elapsed / 24.0),
                        (durationMs - elapsed) / 70.0));
                    amplitude = 3600.0;
                    harmonic = 0.0;
                }
                else if (cue == "success")
                {
                    frequency = 610.0;
                    envelope = Math.Max(0.0, Math.Min(Math.Min(1.0, elapsed / 10.0),
                        (durationMs - elapsed) / 70.0));
                    amplitude = 1500.0;
                    harmonic = 0.06;
                }
                else
                {
                    frequency = elapsed < 130 ? 420.0 : 315.0;
                    envelope = Math.Max(0.0, Math.Min(Math.Min(1.0, elapsed / 18.0),
                        (durationMs - elapsed) / 58.0));
                    amplitude = 3600.0;
                    harmonic = 0.08;
                }
                phase += 2.0 * Math.PI * frequency / sampleRate;
                double shimmer = harmonic * Math.Sin(phase * 2.0 + 0.25);
                short sample = (short)((Math.Sin(phase) + shimmer) * amplitude * envelope);
                writer.Write(sample);
            }
        }
        stream.Position = 0;
        return stream;
    }

    private void StartRecordingCueWorker()
    {
        recordingCueThread = new Thread(new ThreadStart(delegate
        {
            try
            {
                WaitHandle[] handles = { recordingStartCueEvent, recordingStopCueEvent };
                while (!applicationExiting)
                {
                    int signal = WaitHandle.WaitAny(handles);
                    if (applicationExiting) return;
                    if (signal == 0)
                    {
                        HostLog("RECORDING CUE kind=start playback=suppressed reason=end_only_feedback");
                        continue;
                    }
                    if (!config.soundFeedbackEnabled) continue;
                    PlayRecordingCueSync(false);
                }
            }
            catch { }
        }));
        recordingCueThread.IsBackground = true;
        recordingCueThread.Name = "Vibe Flow recording cue player";
        recordingCueThread.Start();
    }

    private void PlayRecordingCueSync(bool starting)
    {
        try
        {
            if (starting)
            {
                HostLog("RECORDING CUE kind=start playback=suppressed reason=end_only_feedback");
                return;
            }
            SoundPlayer player = dictationStopPlayer;
            if (player == null) return;
            var timer = System.Diagnostics.Stopwatch.StartNew();
            HostLog("RECORDING CUE kind=" + (starting ? "start" : "stop") + " playback=begin");
            player.PlaySync();
            timer.Stop();
            HostLog("RECORDING CUE kind=" + (starting ? "start" : "stop") +
                " playback=end duration_ms=" + timer.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            HostLog("RECORDING CUE kind=" + (starting ? "start" : "stop") +
                " playback=failed error=" + SafeLogValue(ex.GetType().Name));
        }
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
        if (lineText.IndexOf("BLE status=Disconnected", StringComparison.OrdinalIgnoreCase) >= 0 ||
            lineText.IndexOf("ATVV SESSION RETRY scheduled", StringComparison.OrdinalIgnoreCase) >= 0 ||
            lineText.IndexOf("ATVV SESSION RETRY exhausted", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            if (bridgeReady)
                HostLog("CAPTURE READY invalidated=true reason=transport_recovery");
            bridgeReady = false;
            UpdateCaptureUi();
        }
        if (lineText.IndexOf("ATVV READY", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            bridgeReady = true;
            captureNotReadySince = DateTime.MinValue;
            reconnectAttempt = 0;
            startupRecoveryCount = 0;
            HostLog("CAPTURE READY startup_ms=" +
                (captureStartedAt == DateTime.MinValue ? "unknown" : ((int)(DateTime.Now - captureStartedAt).TotalMilliseconds).ToString()));
            UpdateCaptureUi();
            return;
        }
        if (lineText.IndexOf("AUDIO TRANSPORT FAILED", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            if (generation > 0) lastFeedbackGeneration = Math.Max(lastFeedbackGeneration, generation);
            SetSessionFeedback("error", "遥控器音频链路恢复失败，当前按住会话已安全结束；请打开自检");
            transientFeedbackUntil = DateTime.Now.AddSeconds(12);
            return;
        }
        if (lineText.IndexOf("VOICE KEY audio timeout", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            if (generation > 0) lastFeedbackGeneration = Math.Max(lastFeedbackGeneration, generation);
            transientFeedbackState = "error";
            transientFeedbackText = "本次没有收到遥控器音频 · 可直接再次按录音键重试";
            transientFeedbackUntil = DateTime.Now.AddSeconds(10);
            if (heroTitle != null && !heroTitle.IsDisposed) heroTitle.Text = "没有收到音频";
            if (heroSubtitle != null && !heroSubtitle.IsDisposed) heroSubtitle.Text = transientFeedbackText;
            if (heroStateLabel != null && !heroStateLabel.IsDisposed) heroStateLabel.Text = "AUDIO RETRY READY";
            ApplyVisualState("error");
            return;
        }
        if (lineText.IndexOf("AUDIO LIVE START session=", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            transientFeedbackUntil = DateTime.MinValue;
            transientFeedbackState = "recording";
            transientFeedbackText = "正在听写 · 遥控器音频正在到达";
            ApplyVisualState("recording");
            return;
        }
        bool continuousRecovery = UsesLongDictation(config.voiceMode) &&
            (lineText.IndexOf("REMOTE STREAM START ", StringComparison.OrdinalIgnoreCase) >= 0 ||
             lineText.IndexOf("REMOTE STREAM SEGMENT STOP session=", StringComparison.OrdinalIgnoreCase) >= 0 ||
             lineText.IndexOf("LONG DICTATION CONTINUE", StringComparison.OrdinalIgnoreCase) >= 0 ||
             lineText.IndexOf("LONG DICTATION REOPEN AUDIO TIMEOUT", StringComparison.OrdinalIgnoreCase) >= 0 ||
             lineText.IndexOf("LONG DICTATION TRANSPORT RECOVERY START", StringComparison.OrdinalIgnoreCase) >= 0 ||
             lineText.IndexOf("AUDIO TRANSPORT STALLED", StringComparison.OrdinalIgnoreCase) >= 0);
        if (continuousRecovery)
        {
            transientFeedbackUntil = DateTime.MinValue;
            transientFeedbackState = "recovering";
            transientFeedbackText = "当前按住会话保持中 · 正在恢复遥控器音频";
            if (heroTitle != null && !heroTitle.IsDisposed) heroTitle.Text = "正在恢复遥控器音频";
            if (heroSubtitle != null && !heroSubtitle.IsDisposed)
                heroSubtitle.Text = "转写工具保持开启；检测到真实音频后会自动继续输入";
            if (heroStateLabel != null && !heroStateLabel.IsDisposed) heroStateLabel.Text = "RECONNECTING AUDIO";
            ApplyVisualState("recovering");
            return;
        }
        if (lineText.IndexOf("REMOTE STREAM START ", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            transientFeedbackUntil = DateTime.MinValue;
            transientFeedbackState = "connecting";
            transientFeedbackText = "遥控器已响应 · 正在等待真实音频";
            ApplyVisualState("connecting");
            return;
        }
        if (lineText.IndexOf("LONG DICTATION FINALIZING", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            if (generation > 0 && generation <= lastFeedbackGeneration && transientFeedbackState == "error") return;
            transientFeedbackUntil = DateTime.Now.AddSeconds(12);
            transientFeedbackState = "processing";
            transientFeedbackText = "长听写已结束 · 正在整理并回填文字";
            if (heroTitle != null && !heroTitle.IsDisposed) heroTitle.Text = "正在整理文字";
            if (heroSubtitle != null && !heroSubtitle.IsDisposed) heroSubtitle.Text = transientFeedbackText;
            if (heroStateLabel != null && !heroStateLabel.IsDisposed) heroStateLabel.Text = "PROCESSING";
            ApplyVisualState("processing");
            return;
        }
        if (lineText.IndexOf("REMOTE STREAM STOP session=", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            if (generation > 0 && generation <= lastFeedbackGeneration && transientFeedbackState == "error") return;
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
            bool weTypeSession = lineText.IndexOf("WETYPE SESSION END", StringComparison.OrdinalIgnoreCase) >= 0;
            bool deliveryFailed = lineText.IndexOf("delivery_mode=provider_direct_unconfirmed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                lineText.IndexOf("delivery_mode=not_submitted", StringComparison.OrdinalIgnoreCase) >= 0;
            bool targetReady = !weTypeSession ||
                lineText.IndexOf("input_target_ready=True", StringComparison.OrdinalIgnoreCase) >= 0;
            bool completed = delivered && targetReady && !deliveryFailed;
            SetSessionFeedback(completed ? "completed" : "error",
                completed ? "听写已完成，文字已由工具直接写入原输入框" :
                delivered ? "转写完成，但工具未能直接写入原输入框" : "本次听写没有送出音频");
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
        if (config.soundFeedbackEnabled && state != "completed")
            PlayFeedbackSound(false);
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

    private void LaunchVBCableInstaller()
    {
        string script = Path.Combine(root, "scripts", "Install-VBCable.ps1");
        if (!File.Exists(script))
        {
            ShowToast("安装引导组件缺失，请重新下载完整安装包", "error");
            HostLog("VB-CABLE INSTALL unavailable reason=script_missing");
            return;
        }
        try
        {
            var start = new ProcessStartInfo("powershell.exe");
            start.Arguments = "-NoProfile -ExecutionPolicy Bypass -File " + SafeCaptureArgument(script) + " -Install";
            start.UseShellExecute = true;
            start.Verb = "runas";
            start.WorkingDirectory = root;
            Process.Start(start);
            HostLog("VB-CABLE INSTALL launched source=vb-audio-official sha256=b950e39f01af1d04ea623c8f6d8eb9b6ea5c477c637295fabf20631c85116bfb");
            ShowToast("已启动官方 VB-CABLE 安装，请确认管理员权限", "info");
        }
        catch (Win32Exception ex)
        {
            if (ex.NativeErrorCode == 1223)
            {
                HostLog("VB-CABLE INSTALL cancelled=true reason=uac_cancelled");
                ShowToast("已取消安装，稍后仍可重新安装", "info");
            }
            else
            {
                HostLog("VB-CABLE INSTALL failed=true error=" + ex.Message);
                ShowToast("VB-CABLE 安装启动失败，请查看诊断记录", "error");
            }
        }
        catch (Exception ex)
        {
            HostLog("VB-CABLE INSTALL failed=true error=" + ex.Message);
            ShowToast("VB-CABLE 安装启动失败，请查看诊断记录", "error");
        }
    }

    private void ScheduleAutomaticUpdateCheck()
    {
        ThreadPool.QueueUserWorkItem(delegate
        {
            Thread.Sleep(8000);
            if (!applicationExiting && config.autoCheckUpdates) CheckForUpdates(false);
        });
    }

    private void CheckForUpdates(bool userInitiated)
    {
        if (Interlocked.CompareExchange(ref updateOperationActive, 1, 0) != 0)
        {
            if (userInitiated) ShowToast("更新检查正在进行，请稍候", "info");
            return;
        }
        DispatchUi(delegate
        {
            if (userInitiated) ShowToast("正在从 GitHub 安全检查最新正式版", "info");
        });
        ThreadPool.QueueUserWorkItem(delegate
        {
            try
            {
                SecureUpdateInfo update = SecureUpdateClient.GetLatest(ProductRelease);
                DispatchUi(delegate { HandleUpdateCheckResult(update, userInitiated); });
            }
            catch (Exception ex)
            {
                HostLog("UPDATE CHECK failed=" + SafeLogValue(ex.Message));
                Interlocked.Exchange(ref updateOperationActive, 0);
                DispatchUi(delegate
                {
                    if (userInitiated) ShowToast("暂时无法检查更新，请稍后重试", "error");
                });
            }
        });
    }

    private void HandleUpdateCheckResult(SecureUpdateInfo update, bool userInitiated)
    {
        if (applicationExiting)
        {
            Interlocked.Exchange(ref updateOperationActive, 0);
            return;
        }
        if (update == null || !update.IsNewer)
        {
            Interlocked.Exchange(ref updateOperationActive, 0);
            if (userInitiated) ShowToast("当前已是最新正式版 V" + ProductRelease, "success");
            HostLog("UPDATE CHECK current=" + ProductRelease + " result=up_to_date");
            return;
        }

        HostLog("UPDATE CHECK current=" + ProductRelease + " latest=" + update.Version + " result=available");
        DialogResult choice = MessageBox.Show(this,
            "发现新版本 V" + update.Version + "。\r\n\r\n" +
            "言灵将从官方 GitHub Release 下载安装包与 SHA256SUMS.txt，校验一致后才允许安装。是否继续？",
            "言灵安全更新", MessageBoxButtons.YesNo, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
        if (choice != DialogResult.Yes)
        {
            Interlocked.Exchange(ref updateOperationActive, 0);
            ShowToast("已暂缓本次更新", "info");
            return;
        }
        ShowToast("正在下载并校验 V" + update.Version + "，请稍候", "info");
        ThreadPool.QueueUserWorkItem(delegate { DownloadVerifiedUpdate(update); });
    }

    private void DownloadVerifiedUpdate(SecureUpdateInfo update)
    {
        try
        {
            string updatesRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Vibe Flow Remote", "Updates");
            string installer = SecureUpdateClient.DownloadAndVerify(update, updatesRoot);
            HostLog("UPDATE DOWNLOAD version=" + update.Version + " sha256=" + update.ExpectedSha256 + " verified=True");
            DispatchUi(delegate { ConfirmAndInstallVerifiedUpdate(update, installer); });
        }
        catch (Exception ex)
        {
            HostLog("UPDATE DOWNLOAD version=" + update.Version + " verified=False error=" + SafeLogValue(ex.Message));
            Interlocked.Exchange(ref updateOperationActive, 0);
            DispatchUi(delegate { ShowToast("更新包校验失败，未运行任何文件", "error"); });
        }
    }

    private void ConfirmAndInstallVerifiedUpdate(SecureUpdateInfo update, string installerPath)
    {
        if (applicationExiting)
        {
            Interlocked.Exchange(ref updateOperationActive, 0);
            return;
        }
        DialogResult choice = MessageBox.Show(this,
            "V" + update.Version + " 已下载，SHA-256 校验通过。\r\n\r\n" +
            "现在安装？言灵会安全退出，安装完成后自动重新打开；现有配置会保留。",
            "更新已验证", MessageBoxButtons.YesNo, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
        if (choice != DialogResult.Yes)
        {
            Interlocked.Exchange(ref updateOperationActive, 0);
            ShowToast("安装包已验证，可稍后再次检查更新", "info");
            return;
        }

        try
        {
            var start = new ProcessStartInfo(installerPath,
                "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /VIBEFLOWUPDATE");
            start.UseShellExecute = true;
            Process.Start(start);
            HostLog("UPDATE INSTALL version=" + update.Version + " launched=True");
            config.minimizeToTray = false;
            applicationExiting = true;
            Application.Exit();
        }
        catch (Exception ex)
        {
            HostLog("UPDATE INSTALL version=" + update.Version + " launched=False error=" + SafeLogValue(ex.Message));
            Interlocked.Exchange(ref updateOperationActive, 0);
            ShowToast("无法启动安装程序，更新未应用", "error");
        }
    }

    private void DispatchUi(Action action)
    {
        if (action == null || applicationExiting || IsDisposed) return;
        try
        {
            if (InvokeRequired) BeginInvoke(action);
            else action();
        }
        catch { }
    }

    private static string SafeLogValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "unknown";
        return value.Replace('\r', '_').Replace('\n', '_').Replace(' ', '_');
    }

    private void PlayRecordingCue(bool starting)
    {
        try
        {
            EventWaitHandle cue = starting ? recordingStartCueEvent : recordingStopCueEvent;
            if (cue != null) cue.Set();
            else PlayRecordingCueSync(starting);
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
        if (provider == "doubao" || provider == "豆包" || provider == "doubao-ime") return "doubao";
        if (provider == "windows" || provider == "win+h") return "windows";
        if (provider == "voquill" || provider == "vokie") return "custom";
        return provider == "custom" ? "custom" : "wechat";
    }

    private static string NormalizeVoiceMode(string value)
    {
        return "hold";
    }

    private static bool UsesLongDictation(string value)
    {
        return false;
    }

    private static string VoiceReadyInstruction(string value)
    {
        return "聚焦输入框后按住录音键说话，松开后完成转译";
    }

    private static string VoiceModeHelp(string value)
    {
        return "固定为首版稳定模式；松开立即结束，单次最长约 60 秒。";
    }

    private static string VoiceStartInstruction(string value)
    {
        return "聚焦输入框，按住录音键说话，松开结束";
    }

    private static string ProviderDisplayName(string provider)
    {
        switch (NormalizeProviderKey(provider))
        {
            case "typeless": return "Typeless";
            case "doubao": return "豆包输入法";
            case "windows": return "Windows 语音输入";
            case "custom": return "其他语音工具";
            default: return "微信输入法";
        }
    }

    private static string ProviderSummary(string provider)
    {
        switch (NormalizeProviderKey(provider))
        {
            case "typeless": return "适合跨应用长文本听写，可继续使用 Typeless 自己的润色、格式整理和词典能力。";
            case "doubao": return "适合中文语音输入；请先在豆包输入法中确认全局语音快捷键。";
            case "windows": return "Windows 自带，无需安装额外客户端，适合快速开始和基础听写。";
            case "custom": return "连接任意支持全局快捷键启动和结束的本地语音输入工具。";
            default: return "适合中文输入。是否进行 AI 整理取决于微信输入法内部当前选择的语音模式，言灵不会代替微信开启润色。";
        }
    }

    private static string ProviderSetupInstruction(string provider)
    {
        switch (NormalizeProviderKey(provider))
        {
            case "typeless": return "在 Typeless 设置中确认录音快捷键。常见默认值是 Right Alt，按一下开始、再按一下结束。";
            case "doubao": return "在豆包输入法中设置全局语音快捷键，再把完全相同的快捷键填写到言灵并执行真实测试。";
            case "windows": return "Windows 语音输入使用 Win + H。首次使用时请先在任意输入框中手动按一次完成系统初始化。";
            case "custom": return "先在目标工具中设置一个不超过四个按键的全局快捷键，再把相同内容填写到这里。";
            default: return "在微信输入法中启用语音输入，把全局快捷键设为 Ctrl + Win；如需 AI 整理，还要在微信输入法内选择对应模式。录音前先聚焦目标输入框。";
        }
    }

    private static string ProviderShortcutDescription(string provider)
    {
        string trigger = DefaultTriggerForProvider(provider) == "hold" ? "按住触发" : "单击切换";
        return DefaultHotkeyForProvider(provider).Replace("+", " + ") + " · " + trigger;
    }

    private static void PopulateTriggerModeOptions(ComboBox target, string provider)
    {
        target.Items.Clear();
        if (NormalizeProviderKey(provider) == "wechat")
            target.Items.Add("单击切换（稳定）");
        else
            target.Items.AddRange(new object[] { "单击切换", "按住触发" });
    }

    private static string ProviderHotkeyHelp(string provider, string trigger)
    {
        if (NormalizeProviderKey(provider) != "wechat") return "须与所选工具中的快捷键一致";
        return "稳定参数：Ctrl + Win · 单击切换";
    }

    private static string DefaultHotkeyForProvider(string provider)
    {
        switch (NormalizeProviderKey(provider))
        {
            case "typeless": return "rightalt";
            case "doubao": return "ctrl+win";
            case "windows": return "win+h";
            case "custom": return "ctrl+win";
            default: return WeChatStableHotkey;
        }
    }

    private static string DefaultTriggerForProvider(string provider)
    {
        return "toggle";
    }

    private static int DefaultStartupDelayForProvider(string provider)
    {
        switch (NormalizeProviderKey(provider))
        {
            case "windows": return 300;
            case "typeless": return 120;
            case "doubao": return 180;
            case "custom": return 150;
            default: return 80;
        }
    }

    private static int ProviderIndex(string provider)
    {
        switch (NormalizeProviderKey(provider))
        {
            case "typeless": return 1;
            case "doubao": return 2;
            case "windows": return 3;
            case "custom": return 4;
            default: return 0;
        }
    }

    private static string ProviderKeyFromIndex(int index)
    {
        return index == 1 ? "typeless" : index == 2 ? "doubao" : index == 3 ? "windows" : index == 4 ? "custom" : "wechat";
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
            case "doubao": return IsProcessRunning("Doubao") || IsProcessRunning("DoubaoInput") ||
                IsProcessRunning("DoubaoIME");
            case "windows": return true;
            default: return true;
        }
    }

    private string ProviderStatusText(string provider)
    {
        string normalized = NormalizeProviderKey(provider);
        if (normalized == "windows") return "●  系统内置";
        if (normalized == "custom") return "●  请确保客户端已启动";
        if (normalized == "wechat" && IsProviderRunning(provider))
            return "●  客户端运行 · AI 整理由微信内模式决定";
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
            case "doubao": OpenUri("https://www.doubao.com/"); break;
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
            if (MigrateConfig(loaded)) WriteConfigAtomically(loaded);
            return loaded;
        }
        catch (Exception primaryError)
        {
            string backupPath = configPath + ".bak";
            try
            {
                if (File.Exists(backupPath))
                {
                    VibeMicConfig recovered = new JavaScriptSerializer().Deserialize<VibeMicConfig>(
                        File.ReadAllText(backupPath, Encoding.UTF8));
                    if (recovered != null)
                    {
                        MigrateConfig(recovered);
                        WriteConfigAtomically(recovered);
                        HostLog("CONFIG RECOVERED source=backup error=" + SafeLogValue(primaryError.Message));
                        return recovered;
                    }
                }
            }
            catch (Exception backupError)
            {
                HostLog("CONFIG RECOVERY failed=true primary=" + SafeLogValue(primaryError.Message) +
                    " backup=" + SafeLogValue(backupError.Message));
            }
            VibeMicConfig defaults = VibeMicConfig.Default();
            try { WriteConfigAtomically(defaults); } catch { }
            return defaults;
        }
    }

    private static bool HasStableVoiceProfile(VibeMicConfig value)
    {
        if (value == null) return false;
        return value.captureSeconds == 0 &&
            Math.Abs(value.gain - StableVoiceGain) < 0.001 &&
            value.autoLevel &&
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
        value.audioEndpointName = StableVoiceEndpoint;
        value.audioProcessingMode = StableVoiceProcessing;
        value.autoRouteVirtualMicrophone = true;
        value.drainMs = StableVoiceDrainMs;
        if (NormalizeProviderKey(value.inputMethod) != "custom")
            value.providerStartupDelayMs = DefaultStartupDelayForProvider(value.inputMethod);
        value.stableVoiceProfileVersion = StableVoiceProfileVersion;
    }

    private static bool IsSafeShortcutProfileId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 64) return false;
        foreach (char character in value)
            if (!(char.IsLetterOrDigit(character) || character == '-' || character == '_')) return false;
        return true;
    }

    private static bool EnsureShortcutProfiles(VibeMicConfig value, int previousSchema)
    {
        bool changed = false;
        if (previousSchema < 30)
        {
            var migrated = new List<ShortcutProfileConfig>();
            migrated.Add(new ShortcutProfileConfig
            {
                id = "my-shortcuts",
                name = "我的快捷键",
                preset = "custom",
                mappings = NormalizeShortcutProfileMappings(value.mappings)
            });
            foreach (ShortcutProfileConfig starter in DefaultShortcutProfiles()) migrated.Add(starter);
            value.shortcutProfiles = migrated.ToArray();
            value.activeShortcutProfileId = "my-shortcuts";
            value.mappingPreset = "custom";
            value.mappings = CloneMappings(migrated[0].mappings);
            return true;
        }

        var profiles = new List<ShortcutProfileConfig>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (value.shortcutProfiles != null)
        {
            foreach (ShortcutProfileConfig candidate in value.shortcutProfiles)
            {
                if (candidate == null) { changed = true; continue; }
                string id = IsSafeShortcutProfileId(candidate.id) && !ids.Contains(candidate.id)
                    ? candidate.id : "profile-" + Guid.NewGuid().ToString("N");
                if (!string.Equals(candidate.id, id, StringComparison.Ordinal))
                {
                    candidate.id = id;
                    changed = true;
                }
                ids.Add(id);
                string fallbackName = "快捷键方案 " + (profiles.Count + 1);
                string name = NormalizeShortcutProfileName(candidate.name, fallbackName);
                if (!string.Equals(candidate.name, name, StringComparison.Ordinal))
                {
                    candidate.name = name;
                    changed = true;
                }
                string preset = NormalizeShortcutProfilePreset(candidate.preset);
                if (!string.Equals(candidate.preset, preset, StringComparison.Ordinal))
                {
                    candidate.preset = preset;
                    changed = true;
                }
                Dictionary<string, string> mappings = NormalizeShortcutProfileMappings(candidate.mappings);
                if (!MappingDictionariesEqual(candidate.mappings, mappings))
                {
                    candidate.mappings = mappings;
                    changed = true;
                }
                profiles.Add(candidate);
            }
        }

        if (profiles.Count == 0)
        {
            ShortcutProfileConfig recovered = new ShortcutProfileConfig
            {
                id = "recovered-shortcuts",
                name = "恢复的快捷键",
                preset = "custom",
                mappings = NormalizeShortcutProfileMappings(value.mappings)
            };
            profiles.Add(recovered);
            value.activeShortcutProfileId = recovered.id;
            changed = true;
        }
        if (value.shortcutProfiles == null || value.shortcutProfiles.Length != profiles.Count)
        {
            value.shortcutProfiles = profiles.ToArray();
            changed = true;
        }
        else value.shortcutProfiles = profiles.ToArray();

        ShortcutProfileConfig active = FindShortcutProfile(value, value.activeShortcutProfileId);
        if (active == null)
        {
            value.activeShortcutProfileId = profiles[0].id;
            changed = true;
        }
        if (ProjectActiveShortcutProfile(value)) changed = true;
        return changed;
    }

    private static bool MigrateConfig(VibeMicConfig value)
    {
        int previousSchema = value.schemaVersion;
        int previousOnboardingVersion = value.onboardingVersion;
        bool changed = previousSchema < ConfigSchemaVersion;
        value.schemaVersion = ConfigSchemaVersion;
        if (value.captureSeconds < 0) { value.captureSeconds = 0; changed = true; }
        if (value.gain <= 0 || value.gain > 4) { value.gain = 1.0; changed = true; }
        if (previousSchema < 11) { value.autoLevel = true; changed = true; }
        string normalizedVoiceMode = NormalizeVoiceMode(value.voiceMode);
        if (!string.Equals(value.voiceMode, normalizedVoiceMode, StringComparison.OrdinalIgnoreCase))
        {
            value.voiceMode = normalizedVoiceMode;
            changed = true;
        }
        if (string.IsNullOrWhiteSpace(value.audioEndpointName)) { value.audioEndpointName = "CABLE Input"; changed = true; }
        if (string.IsNullOrWhiteSpace(value.inputMethod)) { value.inputMethod = "wechat"; changed = true; }
        if (string.IsNullOrWhiteSpace(value.inputMethodHotkey)) { value.inputMethodHotkey = DefaultHotkeyForProvider(value.inputMethod); changed = true; }
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
        if (normalizedProvider == "wechat" && value.inputMethodTrigger != "toggle")
        {
            value.inputMethodTrigger = "toggle";
            changed = true;
        }
        if (previousSchema < 25 && normalizedProvider == "wechat" &&
            string.Equals(value.inputMethodHotkey, WeChatV12Hotkey, StringComparison.OrdinalIgnoreCase) &&
            value.inputMethodTrigger == "toggle")
        {
            value.inputMethodHotkey = WeChatStableHotkey;
            value.providerStartupDelayMs = DefaultStartupDelayForProvider("wechat");
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
        if (previousSchema < 17)
        {
            value.autoCheckUpdates = true;
            changed = true;
        }
        if (value.drainMs <= 0) { value.drainMs = 180; changed = true; }
        string normalizedRoutingMode = NormalizeInputRoutingMode(value.inputRoutingMode);
        if (!string.Equals(value.inputRoutingMode, normalizedRoutingMode, StringComparison.OrdinalIgnoreCase))
        {
            value.inputRoutingMode = normalizedRoutingMode;
            changed = true;
        }
        if (string.IsNullOrWhiteSpace(value.mappingPreset)) { value.mappingPreset = "coding"; changed = true; }
        if (value.mappings == null) { value.mappings = new Dictionary<string, string>(); changed = true; }
        string retiredPowerAction = "";
        string[] retiredPowerKeys = { "电源键:short", "电源键", "电源键:long" };
        foreach (string retiredPowerKey in retiredPowerKeys)
        {
            string candidate;
            if (value.mappings.TryGetValue(retiredPowerKey, out candidate) &&
                IsApplicationOrWebAction(candidate))
            {
                retiredPowerAction = candidate;
                break;
            }
        }
        if (previousSchema < 23)
        {
            string legacyPower = value.mappings.ContainsKey("电源键") ? value.mappings["电源键"] : "";
            bool preservePowerTarget = IsApplicationOrWebAction(legacyPower);
            value.mappings["功能键:short"] = "ctrl+c";
            value.mappings["功能键:long"] = "ctrl+v";
            value.mappings["返回键:short"] = "backspace";
            value.mappings["返回键:long"] = "browserback";
            value.mappings["TV:short"] = "alt+tab";
            value.mappings["TV:long"] = "mediaplaypause";
            value.mappings["电源键:short"] = preservePowerTarget ? legacyPower : "launch-client:chatgpt";
            value.mappings["电源键:long"] = "none";
            value.mappings["Home"] = "win+d";
            value.mappings["确认键"] = "enter";
            value.mappings["上 / 下 / 左 / 右"] = "passthrough";
            value.mappings["音量 +"] = "volumeup";
            value.mappings["音量 -"] = "volumedown";
            changed = true;
        }
        if (value.customButtons != null)
        {
            foreach (CustomButtonConfig legacyButton in value.customButtons)
            {
                string physicalKey = ResolveLegacyCustomButtonKey(legacyButton);
                if (string.IsNullOrWhiteSpace(physicalKey) || legacyButton == null || !legacyButton.enabled ||
                    string.IsNullOrWhiteSpace(legacyButton.action) ||
                    legacyButton.action.Equals("none", StringComparison.OrdinalIgnoreCase)) continue;
                value.mappings[physicalKey] = legacyButton.action;
            }
            value.customButtons = null;
            changed = true;
        }
        if (previousSchema < 25)
        {
            value.voiceMode = "hold";
            value.mappings["TV"] = "task-switcher";
            value.mappings["上键"] = "up";
            value.mappings["下键"] = "down";
            value.mappings["左键"] = "left";
            value.mappings["右键"] = "right";
            string[] retiredKeys = {
                "上 / 下 / 左 / 右", "TV:short", "TV:long",
                "返回键", "返回键:short", "返回键:long",
                "音量 +", "音量 -", "电源键", "电源键:short", "电源键:long"
            };
            foreach (string key in retiredKeys) value.mappings.Remove(key);
            changed = true;
        }
        if (previousSchema < 27)
        {
            string legacyHome = value.mappings.ContainsKey("Home") ? value.mappings["Home"] : "win+d";
            string homeShort = value.mappings.ContainsKey("Home:short") ? value.mappings["Home:short"] : legacyHome;
            string homeLong = value.mappings.ContainsKey("Home:long") ? value.mappings["Home:long"] : "none";
            string powerShort = value.mappings.ContainsKey("电源键:short") ? value.mappings["电源键:short"] : "none";
            string powerLong = value.mappings.ContainsKey("电源键:long") ? value.mappings["电源键:long"] : "none";
            value.mappings["Home:short"] = IsSupportedMappingAction(homeShort) ? homeShort :
                IsSupportedMappingAction(legacyHome) ? legacyHome : "win+d";
            value.mappings["Home:long"] = IsSupportedMappingAction(homeLong) ? homeLong : "none";
            value.mappings["电源键:short"] = IsSupportedMappingAction(powerShort) ? powerShort : "none";
            value.mappings["电源键:long"] = IsSupportedMappingAction(powerLong) ? powerLong : "none";
            changed = true;
        }
        if (previousSchema < 28)
        {
            string homeLong = value.mappings.ContainsKey("Home:long") ? value.mappings["Home:long"] : "none";
            if ((string.IsNullOrWhiteSpace(homeLong) || homeLong.Equals("none", StringComparison.OrdinalIgnoreCase)) &&
                !string.IsNullOrWhiteSpace(retiredPowerAction))
                value.mappings["Home:long"] = retiredPowerAction;
            value.mappings.Remove("电源键");
            value.mappings.Remove("电源键:short");
            value.mappings.Remove("电源键:long");
            changed = true;
        }
        Dictionary<string, string> defaults = VibeMicConfig.Default().mappings;
        foreach (KeyValuePair<string, string> pair in defaults)
        {
            if (!value.mappings.ContainsKey(pair.Key)) { value.mappings[pair.Key] = pair.Value; changed = true; }
        }
        string[] configurableKeys = {
            "确认键", "Home", "Home:short", "Home:long",
            "TV", "功能键:short", "功能键:long",
            "上键", "下键", "左键", "右键"
        };
        foreach (string key in configurableKeys)
        {
            if (!value.mappings.ContainsKey(key) || !IsSupportedMappingAction(value.mappings[key]))
            {
                value.mappings[key] = defaults[key];
                changed = true;
            }
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
        string[] unsupportedMappings = { "音量 + / -", "返回操作", "换行 / 删除" };
        foreach (string key in unsupportedMappings)
            if (value.mappings.Remove(key)) changed = true;
        if (EnsureShortcutProfiles(value, previousSchema)) changed = true;
        int profileVersion = HasStableVoiceProfile(value) ? StableVoiceProfileVersion : 0;
        if (value.stableVoiceProfileVersion != profileVersion)
        {
            value.stableVoiceProfileVersion = profileVersion;
            changed = true;
        }
        if (previousOnboardingVersion < CurrentOnboardingVersion)
        {
            if (!value.setupCompleted)
            {
                int legacyStep = value.onboardingStep;
                value.onboardingStep = legacyStep <= 0 ? 0 : legacyStep <= 3 ? 1 :
                    legacyStep <= 5 ? 2 : legacyStep <= 7 ? 3 : 4;
            }
            value.onboardingVersion = CurrentOnboardingVersion;
            changed = true;
        }
        if (value.onboardingStep < 0 || value.onboardingStep >= OnboardingStepCount)
        {
            value.onboardingStep = value.setupCompleted ? OnboardingStepCount - 1 : 0;
            changed = true;
        }
        else if (value.setupCompleted && value.onboardingStep < OnboardingStepCount - 1)
        {
            value.onboardingStep = OnboardingStepCount - 1;
            changed = true;
        }
        if (string.IsNullOrWhiteSpace(value.theme))
        {
            value.theme = "light";
            changed = true;
        }
        else if (value.theme != "system" && value.theme != "light" && value.theme != "dark")
        {
            value.theme = "light";
            changed = true;
        }
        if (value.setupCompleted && value.resumeSetupAfterRestart)
        {
            value.resumeSetupAfterRestart = false;
            changed = true;
        }
        return changed;
    }

    private static bool IsApplicationOrWebAction(string action)
    {
        string value = (action ?? "").Trim();
        return value.StartsWith("launch-client:", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("open-exe:", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("open-url:", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("open-app:", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("start-app:", StringComparison.OrdinalIgnoreCase);
    }

    private void EnsureConfig()
    {
        if (File.Exists(configPath)) return;
        string backupPath = configPath + ".bak";
        if (File.Exists(backupPath))
        {
            File.Copy(backupPath, configPath, false);
            return;
        }
        WriteConfigAtomically(VibeMicConfig.Default());
    }

    private void WriteConfigAtomically(VibeMicConfig value)
    {
        WriteTextAtomically(configPath, new JavaScriptSerializer().Serialize(value), configPath + ".bak");
    }

    private static void WriteTextAtomically(string path, string content, string backupPath)
    {
        string tempPath = path + ".tmp";
        File.WriteAllText(tempPath, content, Encoding.UTF8);
        if (File.Exists(path)) File.Replace(tempPath, path, backupPath);
        else File.Move(tempPath, path);
    }

    private bool SaveConfig()
    {
        try
        {
            config.schemaVersion = ConfigSchemaVersion;
            CaptureActiveShortcutProfileMappings(config);
            EnsureShortcutProfiles(config, ConfigSchemaVersion);
            config.autoLevel = config.audioProcessingMode == "speech";
            config.stableVoiceProfileVersion = HasStableVoiceProfile(config) ? StableVoiceProfileVersion : 0;
            WriteConfigAtomically(config);
            if (!uiSmokeMode) SyncKeyboardBridgeConfig();
            return true;
        }
        catch (Exception ex)
        {
            Log("Config save failed: " + ex.Message);
            HostLog("CONFIG SAVE failed=true error=" + SafeLogValue(ex.Message));
            return false;
        }
    }

    private bool PersistedMappingMatches(string key, string expectedAction)
    {
        try
        {
            VibeMicConfig persisted = new JavaScriptSerializer().Deserialize<VibeMicConfig>(
                File.ReadAllText(configPath, Encoding.UTF8));
            string actual = GetConfigMapping(persisted, key, "");
            return string.Equals(actual, expectedAction, StringComparison.Ordinal);
        }
        catch (Exception ex)
        {
            HostLog("MAPPING SAVE verify_failed=true key=" + SafeLogValue(key) +
                " error=" + SafeLogValue(ex.Message));
            return false;
        }
    }

    private void SetLaunchAtStartup(bool enabled)
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run"))
            {
                if (key == null) return;
                key.DeleteValue("Vibe Mic", false);
                key.DeleteValue("声启 MIC", false);
                if (enabled) key.SetValue("Vibe Flow", "\"" + Application.ExecutablePath + "\" --background");
                else key.DeleteValue("Vibe Flow", false);
                string actual = key.GetValue("Vibe Flow") as string;
                if (enabled && string.IsNullOrWhiteSpace(actual)) Log("Startup setting verification failed");
            }
        }
        catch (Exception ex) { Log("Startup setting failed: " + ex.Message); }
    }

    private bool StartupRegistrationRequired()
    {
        return ShouldRegisterStartup(config);
    }

    private static bool ShouldRegisterStartup(VibeMicConfig value)
    {
        return value != null && (value.launchAtStartup || (!value.setupCompleted && value.resumeSetupAfterRestart));
    }

    private void ReconcileLaunchAtStartupRegistration()
    {
        bool required = StartupRegistrationRequired();
        SetLaunchAtStartup(required);
        HostLog("STARTUP RECONCILE required=" + required + " configured=" + config.launchAtStartup +
            " onboarding_resume=" + (!config.setupCompleted && config.resumeSetupAfterRestart));
    }

    private bool IsLaunchAtStartupRegistered()
    {
        if (!config.launchAtStartup) return false;
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", false))
            {
                string value = key == null ? "" : key.GetValue("Vibe Flow") as string;
                return !string.IsNullOrWhiteSpace(value) &&
                    value.IndexOf(Application.ExecutablePath, StringComparison.OrdinalIgnoreCase) >= 0 &&
                    value.IndexOf("--background", StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }
        catch (Exception ex)
        {
            Log("Startup setting read failed: " + ex.Message);
            return false;
        }
    }

    private string SyncKeyboardBridgeConfig()
    {
        if (config == null) return "";
        try
        {
            Dictionary<string, object> document = BuildKeyboardBridgeDocument(config);
            string revision = Convert.ToString(document["revision"]);
            string bridgeConfigPath = Path.Combine(root, "voxdeck-shortcuts.json");
            WriteTextAtomically(bridgeConfigPath, new JavaScriptSerializer().Serialize(document), bridgeConfigPath + ".bak");
            expectedKeyboardConfigRevision = revision;
            if (IsCurrentProcessRunningFromRoot("VoxDeckInputBridge"))
                SignalEvent("Local\\VibeMicReloadKeyboardConfig");
            return revision;
        }
        catch (Exception ex)
        {
            Log("Keyboard config sync failed: " + ex.Message);
            return "";
        }
    }

    private static Dictionary<string, object> BuildKeyboardBridgeDocument(VibeMicConfig source)
    {
        var mappings = new List<Dictionary<string, object>>();
        mappings.Add(BridgeMapping("voice", "录音键", "F5", "0x3F", true, true, "suppress", ""));
        mappings.Add(GestureMapping("home", "Home 键", "Home", "0x47",
            GetConfigMapping(source, "Home:short", GetConfigMapping(source, "Home", "win+d")),
            GetConfigMapping(source, "Home:long", "none")));
        mappings.Add(ConfiguredMapping("tv", "TV 键", "Oemtilde", "0x29",
            GetConfigMapping(source, "TV", "task-switcher"), "oemtilde"));
        mappings.Add(GestureMapping("menu", "功能键", "Apps", "0x5D",
            GetConfigMapping(source, "功能键:short", "ctrl+c"), GetConfigMapping(source, "功能键:long", "ctrl+v")));
        mappings.Add(ConfiguredMapping("ok", "确认键", "Enter", "0x1C", GetConfigMapping(source, "确认键", "enter"), "enter"));
        mappings.Add(ConfiguredMapping("up", "上键", "Up", "0x48", GetConfigMapping(source, "上键", "up"), "up"));
        mappings.Add(ConfiguredMapping("down", "下键", "Down", "0x50", GetConfigMapping(source, "下键", "down"), "down"));
        mappings.Add(ConfiguredMapping("left", "左键", "Left", "0x4B", GetConfigMapping(source, "左键", "left"), "left"));
        mappings.Add(ConfiguredMapping("right", "右键", "Right", "0x4D", GetConfigMapping(source, "右键", "right"), "right"));
        var document = new Dictionary<string, object>();
        document["version"] = 6;
        string routingMode = NormalizeInputRoutingMode(source == null ? "strict" : source.inputRoutingMode);
        ShortcutProfileConfig activeProfile = ActiveShortcutProfile(source);
        string activeProfileId = activeProfile == null ? "" : activeProfile.id ?? "";
        string activeProfileName = activeProfile == null ? "" : activeProfile.name ?? "";
        document["inputRoutingMode"] = routingMode;
        document["activeShortcutProfileId"] = activeProfileId;
        document["activeShortcutProfileName"] = activeProfileName;
        document["revision"] = ComputeBridgeConfigRevision(new object[] {
            routingMode, activeProfileId, activeProfileName, mappings.ToArray()
        });
        document["notes"] = "Generated by Vibe Flow. Non-voice actions are device-scoped Raw Input; the Hook passes unidentified keyboard events through. The optional signed filter adds exact-device suppression.";
        document["mappings"] = mappings.ToArray();
        return document;
    }

    private static string NormalizeInputRoutingMode(string value)
    {
        return "strict";
    }

    private static string GetConfigMapping(VibeMicConfig source, string key, string fallback)
    {
        return source != null && source.mappings != null && source.mappings.ContainsKey(key)
            ? source.mappings[key] : fallback;
    }

    private static string ComputeBridgeConfigRevision(object mappings)
    {
        string payload = new JavaScriptSerializer().Serialize(mappings);
        using (SHA256 algorithm = SHA256.Create())
        {
            byte[] digest = algorithm.ComputeHash(Encoding.UTF8.GetBytes(payload));
            return BitConverter.ToString(digest).Replace("-", "").ToLowerInvariant();
        }
    }

    private static Dictionary<string, object> FindGeneratedBridgeMapping(
        Dictionary<string, object> document, string name, string sourceType)
    {
        if (document == null || !document.ContainsKey("mappings")) return null;
        Dictionary<string, object>[] mappings = document["mappings"] as Dictionary<string, object>[];
        if (mappings == null) return null;
        foreach (Dictionary<string, object> mapping in mappings)
        {
            if (!mapping.ContainsKey("name") || !mapping.ContainsKey("sourceType")) continue;
            if (string.Equals(Convert.ToString(mapping["name"]), name, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Convert.ToString(mapping["sourceType"]), sourceType, StringComparison.OrdinalIgnoreCase))
                return mapping;
        }
        return null;
    }

    private static Dictionary<string, object> ConfiguredMapping(string name, string label, string vk, string scan, string action, string nativeAction)
    {
        string normalized = (action ?? "").Trim().ToLowerInvariant();
        bool passthrough = normalized.Length == 0 || normalized == "none" ||
            normalized == "passthrough" || normalized == nativeAction;
        return BridgeMapping(name, label, vk, scan, !passthrough, !passthrough, passthrough ? "passthrough" : "tap", passthrough ? nativeAction : action);
    }

    private static Dictionary<string, object> HidConfiguredMapping(string name, string label, string action, string sourceType, int usagePage, int usage)
    {
        string normalized = (action ?? "").Trim().ToLowerInvariant();
        bool enabled = normalized.Length > 0 && normalized != "none" && normalized != "passthrough";
        return BridgeMapping(name, label, "", "", enabled, enabled, "tap", action, sourceType, usagePage, usage);
    }

    private static Dictionary<string, object> ConsumerConfiguredMapping(string name, string label, string action, int usage)
    {
        return HidConfiguredMapping(name, label, action, "consumer", 0x0C, usage);
    }

    private static Dictionary<string, object> GestureMapping(string name, string label, string vk, string scan,
        string shortAction, string longAction)
    {
        return GestureMapping(name, label, vk, scan, shortAction, longAction, "keyboard", 0, 0);
    }

    private static Dictionary<string, object> HidGestureMapping(string name, string label,
        string shortAction, string longAction, string sourceType, int usagePage, int usage)
    {
        return GestureMapping(name, label, "", "", shortAction, longAction, sourceType, usagePage, usage);
    }

    private static Dictionary<string, object> GestureMapping(string name, string label, string vk, string scan,
        string shortAction, string longAction, string sourceType, int usagePage, int usage)
    {
        bool enabled = !IsDisabledAction(shortAction) || !IsDisabledAction(longAction);
        Dictionary<string, object> value = BridgeMapping(name, label, vk, scan, enabled, enabled,
            "shortlong", "", sourceType, usagePage, usage);
        value["shortShortcut"] = shortAction ?? "none";
        value["longShortcut"] = longAction ?? "none";
        value["longPressMs"] = 650;
        return value;
    }

    private static Dictionary<string, object> HidHoldMapping(string name, string label, string action,
        string sourceType, int usagePage, int usage)
    {
        bool enabled = !IsDisabledAction(action);
        return BridgeMapping(name, label, "", "", enabled, enabled, "hold", action,
            sourceType, usagePage, usage);
    }

    private static bool IsDisabledAction(string action)
    {
        string value = (action ?? "").Trim();
        return value.Length == 0 || value.Equals("none", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("passthrough", StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, object> BridgeMapping(string name, string label, string vk, string scan, bool enabled, bool suppress, string mode, string shortcut)
    {
        return BridgeMapping(name, label, vk, scan, enabled, suppress, mode, shortcut, "keyboard", 0, 0);
    }

    private static Dictionary<string, object> BridgeMapping(string name, string label, string vk, string scan, bool enabled, bool suppress, string mode, string shortcut, string sourceType, int usagePage, int usage)
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
        value["sourceType"] = sourceType;
        value["usagePage"] = usagePage;
        value["usage"] = usage;
        return value;
    }

    private static Dictionary<string, object> CustomBridgeMapping(CustomButtonConfig custom)
    {
        if (custom == null || !custom.enabled || string.IsNullOrWhiteSpace(custom.action) ||
            custom.action.Equals("none", StringComparison.OrdinalIgnoreCase)) return null;
        bool consumer = string.Equals(custom.sourceType, "consumer", StringComparison.OrdinalIgnoreCase) && custom.usage > 0;
        bool hid = string.Equals(custom.sourceType, "hid", StringComparison.OrdinalIgnoreCase) && custom.usagePage > 0 && custom.usage > 0;
        bool keyboard = !consumer && !hid && (!string.IsNullOrWhiteSpace(custom.vk) || !string.IsNullOrWhiteSpace(custom.scan));
        if (!consumer && !hid && !keyboard) return null;
        return BridgeMapping(custom.slot, custom.label, consumer || hid ? "" : custom.vk, consumer || hid ? "" : custom.scan,
            true, true, "tap", custom.action, consumer ? "consumer" : hid ? "hid" : "keyboard", custom.usagePage, custom.usage);
    }

    private static Dictionary<string, string> DefaultRemoteMappings()
    {
        var mappings = new Dictionary<string, string>();
        mappings["确认键"] = "enter";
        mappings["Home"] = "win+d";
        mappings["Home:short"] = "win+d";
        mappings["Home:long"] = "none";
        mappings["TV"] = "task-switcher";
        mappings["功能键"] = "ctrl+c";
        mappings["功能键:short"] = "ctrl+c";
        mappings["功能键:long"] = "ctrl+v";
        mappings["上键"] = "up";
        mappings["下键"] = "down";
        mappings["左键"] = "left";
        mappings["右键"] = "right";
        return mappings;
    }

    private static Dictionary<string, string> CloneMappings(Dictionary<string, string> source)
    {
        var clone = new Dictionary<string, string>();
        if (source == null) return clone;
        foreach (KeyValuePair<string, string> pair in source) clone[pair.Key] = pair.Value;
        return clone;
    }

    private static Dictionary<string, string> NormalizeShortcutProfileMappings(Dictionary<string, string> source)
    {
        Dictionary<string, string> defaults = DefaultRemoteMappings();
        var normalized = new Dictionary<string, string>();
        string[] keys = {
            "确认键", "Home", "Home:short", "Home:long", "TV", "功能键",
            "功能键:short", "功能键:long", "上键", "下键", "左键", "右键"
        };
        foreach (string key in keys)
        {
            string action = source != null && source.ContainsKey(key) ? source[key] : defaults[key];
            action = NormalizePhysicalMappingAction(key, action);
            normalized[key] = IsSupportedMappingAction(action) ? action : defaults[key];
        }
        normalized["Home"] = normalized["Home:short"];
        normalized["功能键"] = normalized["功能键:short"];
        return normalized;
    }

    private static bool MappingDictionariesEqual(Dictionary<string, string> left, Dictionary<string, string> right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left == null || right == null || left.Count != right.Count) return false;
        foreach (KeyValuePair<string, string> pair in left)
        {
            string value;
            if (!right.TryGetValue(pair.Key, out value) ||
                !string.Equals(value, pair.Value, StringComparison.Ordinal)) return false;
        }
        return true;
    }

    private static string StarterProfileName(string preset)
    {
        if (preset == "vibe-coding") return "Vibe Coding";
        if (preset == "browser-ai") return "浏览器 AI";
        if (preset == "terminal-agent") return "Terminal Agent";
        return "通用导航";
    }

    private static Dictionary<string, string> StarterProfileMappings(string preset)
    {
        Dictionary<string, string> mappings = DefaultRemoteMappings();
        if (preset == "vibe-coding")
        {
            mappings["上键"] = "ctrl+z";
            mappings["下键"] = "ctrl+shift+z";
            mappings["左键"] = "ctrl+c";
            mappings["右键"] = "ctrl+v";
            mappings["Home:long"] = "launch-client:cursor";
        }
        else if (preset == "browser-ai")
        {
            mappings["上键"] = "pageup";
            mappings["下键"] = "pagedown";
            mappings["左键"] = "browserback";
            mappings["右键"] = "tab";
            mappings["Home:long"] = "launch-client:chatgpt";
        }
        else if (preset == "terminal-agent")
        {
            mappings["Home:short"] = "launch-client:terminal";
            mappings["Home"] = "launch-client:terminal";
            mappings["Home:long"] = "launch-client:codex";
        }
        return mappings;
    }

    private static ShortcutProfileConfig CreateStarterShortcutProfile(string preset)
    {
        string normalized = preset == "vibe-coding" || preset == "browser-ai" || preset == "terminal-agent"
            ? preset : "general";
        return new ShortcutProfileConfig
        {
            id = normalized,
            name = StarterProfileName(normalized),
            preset = normalized,
            mappings = StarterProfileMappings(normalized)
        };
    }

    private static ShortcutProfileConfig[] DefaultShortcutProfiles()
    {
        return new ShortcutProfileConfig[] {
            CreateStarterShortcutProfile("general"),
            CreateStarterShortcutProfile("vibe-coding"),
            CreateStarterShortcutProfile("browser-ai"),
            CreateStarterShortcutProfile("terminal-agent")
        };
    }

    private static ShortcutProfileConfig CloneShortcutProfile(ShortcutProfileConfig source, string id, string name)
    {
        return new ShortcutProfileConfig
        {
            id = id,
            name = name,
            preset = source == null || string.IsNullOrWhiteSpace(source.preset) ? "custom" : source.preset,
            mappings = NormalizeShortcutProfileMappings(source == null ? null : source.mappings)
        };
    }

    private static string NormalizeShortcutProfileName(string value, string fallback)
    {
        string name = (value ?? "").Trim();
        var clean = new StringBuilder();
        foreach (char character in name)
            if (!char.IsControl(character)) clean.Append(character);
        name = clean.ToString().Trim();
        if (name.Length == 0) name = fallback;
        if (name.Length > 32) name = name.Substring(0, 32);
        return name;
    }

    private static string NormalizeShortcutProfilePreset(string value)
    {
        string preset = (value ?? "").Trim().ToLowerInvariant();
        return preset == "general" || preset == "vibe-coding" || preset == "browser-ai" ||
            preset == "terminal-agent" ? preset : "custom";
    }

    private static ShortcutProfileConfig FindShortcutProfile(VibeMicConfig value, string id)
    {
        if (value == null || value.shortcutProfiles == null || string.IsNullOrWhiteSpace(id)) return null;
        foreach (ShortcutProfileConfig profile in value.shortcutProfiles)
            if (profile != null && string.Equals(profile.id, id, StringComparison.OrdinalIgnoreCase)) return profile;
        return null;
    }

    private static ShortcutProfileConfig ActiveShortcutProfile(VibeMicConfig value)
    {
        ShortcutProfileConfig active = FindShortcutProfile(value, value == null ? "" : value.activeShortcutProfileId);
        if (active != null) return active;
        return value != null && value.shortcutProfiles != null && value.shortcutProfiles.Length > 0
            ? value.shortcutProfiles[0] : null;
    }

    private static bool CaptureActiveShortcutProfileMappings(VibeMicConfig value)
    {
        ShortcutProfileConfig active = ActiveShortcutProfile(value);
        if (active == null) return false;
        Dictionary<string, string> normalized = NormalizeShortcutProfileMappings(value.mappings);
        bool changed = !MappingDictionariesEqual(active.mappings, normalized);
        if (changed) active.mappings = normalized;
        string preset = NormalizeShortcutProfilePreset(value.mappingPreset);
        if (!string.Equals(active.preset, preset, StringComparison.Ordinal))
        {
            active.preset = preset;
            changed = true;
        }
        return changed;
    }

    private static bool ProjectActiveShortcutProfile(VibeMicConfig value)
    {
        ShortcutProfileConfig active = ActiveShortcutProfile(value);
        if (active == null) return false;
        Dictionary<string, string> projected = NormalizeShortcutProfileMappings(active.mappings);
        bool changed = !MappingDictionariesEqual(value.mappings, projected);
        if (changed) value.mappings = projected;
        if (!string.Equals(value.activeShortcutProfileId, active.id, StringComparison.Ordinal))
        {
            value.activeShortcutProfileId = active.id;
            changed = true;
        }
        string preset = NormalizeShortcutProfilePreset(active.preset);
        if (!string.Equals(value.mappingPreset, preset, StringComparison.Ordinal))
        {
            value.mappingPreset = preset;
            changed = true;
        }
        return changed;
    }

    private string GetMapping(string key, string fallback)
    {
        if (config.mappings != null && config.mappings.ContainsKey(key)) return config.mappings[key];
        return fallback;
    }

    private long InputBridgeLogLength()
    {
        try
        {
            string path = Path.Combine(root, "input-bridge-log.txt");
            return File.Exists(path) ? new FileInfo(path).Length : 0;
        }
        catch { return 0; }
    }

    private bool HasObservedPhysicalButton(string key)
    {
        return HasObservedPhysicalButtonSince(key, 0);
    }

    private bool HasObservedPhysicalButtonSince(string key, long startPosition)
    {
        string path = Path.Combine(root, "input-bridge-log.txt");
        if (!File.Exists(path)) return false;
        try
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete))
            {
                stream.Position = Math.Max(0, Math.Min(startPosition, stream.Length));
                using (var reader = new StreamReader(stream, Encoding.UTF8, true, 4096, false))
                {
                    string text = reader.ReadToEnd();
                    if (key == "上键")
                        return text.IndexOf("RC003 RAW KEY DOWN vk=0x26", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            text.IndexOf("Key 上键 DOWN", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (key == "下键")
                        return text.IndexOf("RC003 RAW KEY DOWN vk=0x28", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            text.IndexOf("Key 下键 DOWN", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (key == "左键")
                        return text.IndexOf("RC003 RAW KEY DOWN vk=0x25", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            text.IndexOf("Key 左键 DOWN", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (key == "右键")
                        return text.IndexOf("RC003 RAW KEY DOWN vk=0x27", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            text.IndexOf("Key 右键 DOWN", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (key == "电源键")
                        return text.IndexOf("Key 开机键", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            text.IndexOf("usage=0x66", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            text.IndexOf("vk=0x83", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (key == "Home")
                        return text.IndexOf("Key Home 键", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            text.IndexOf("RC003 RAW KEY DOWN vk=0x24", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (key == "确认键")
                        return text.IndexOf("Key 确认键", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            text.IndexOf("RC003 RAW KEY DOWN vk=0x0D", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (key == "功能键")
                        return text.IndexOf("Key 功能键", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            text.IndexOf("RC003 RAW KEY DOWN vk=0x5D", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (key == "返回键")
                        return text.IndexOf("Key 返回键", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            text.IndexOf("usage=0xF1", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (key == "TV")
                        return text.IndexOf("Key TV 键", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            text.IndexOf("vk=Oemtilde", StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
        }
        catch { }
        return false;
    }

    private static string RemoteControlForMappingKey(string key)
    {
        if (key == "录音键") return "voice";
        if (key == "确认键") return "ok";
        if (key == "Home") return "home";
        if (key == "TV") return "tv";
        if (key == "功能键") return "menu";
        if (key == "返回键") return "back";
        if (key == "音量 +") return "volumeup";
        if (key == "音量 -") return "volumedown";
        if (key == "电源键") return "power";
        if (key == "上键") return "up";
        if (key == "下键") return "down";
        if (key == "左键") return "left";
        if (key == "右键") return "right";
        return "directions";
    }

    private static string DefaultDirectionAction(string key)
    {
        if (key == "上键") return "up";
        if (key == "下键") return "down";
        if (key == "左键") return "left";
        if (key == "右键") return "right";
        return "passthrough";
    }

    private static string DefaultConfigurableAction(string key)
    {
        if (key == "确认键") return "enter";
        if (key == "Home") return "win+d";
        if (key == "Home:short") return "win+d";
        if (key == "Home:long") return "none";
        if (key == "电源键:short" || key == "电源键:long") return "none";
        if (key == "TV") return "task-switcher";
        if (key == "功能键:short") return "ctrl+c";
        if (key == "功能键:long") return "ctrl+v";
        return DefaultDirectionAction(key);
    }

    private static List<ShortcutChoice> MappingActionChoicesFor(string key, string current)
    {
        string native = DefaultConfigurableAction(key);
        List<ShortcutChoice> choices = CustomActionChoices(current);
        choices.RemoveAll(delegate(ShortcutChoice choice)
        {
            return choice.Shortcut.Equals(native, StringComparison.OrdinalIgnoreCase);
        });
        choices.Insert(0, new ShortcutChoice("保持默认 · " + CustomActionText(native), native));
        return choices;
    }

    private static bool IsSupportedMappingAction(string action)
    {
        string value = (action ?? "").Trim().ToLowerInvariant();
        if (value.StartsWith("launch-client:", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("open-exe:", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("open-url:", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("open-app:", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("start-app:", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("shortcut:", StringComparison.OrdinalIgnoreCase)) return true;
        string[] supported = {
            "none", "passthrough", "up", "down", "left", "right",
            "ctrl+c", "ctrl+x", "ctrl+v", "ctrl+z", "ctrl+shift+z",
            "ctrl+s", "ctrl+a", "ctrl+f", "enter", "escape", "tab",
            "shift+tab", "pageup", "pagedown", "backspace", "alt+left", "browserback",
            "win+d", "win+shift+s", "task-switcher", "volumeup", "volumedown",
            "volumemute", "mediaplaypause"
        };
        return Array.IndexOf(supported, value) >= 0;
    }

    private static bool IsPersistableMappingAction(string action)
    {
        string value = (action ?? "").Trim();
        return IsSupportedMappingAction(value) &&
            !value.EndsWith(":prompt", StringComparison.OrdinalIgnoreCase) &&
            value.IndexOf('\r') < 0 && value.IndexOf('\n') < 0;
    }

    private static List<ShortcutChoice> ShortcutChoicesFor(string key, string current)
    {
        string native = DefaultDirectionAction(key);
        var choices = new List<ShortcutChoice>
        {
            new ShortcutChoice(native == "up" ? "保持上方向（推荐）" :
                native == "down" ? "保持下方向（推荐）" :
                native == "left" ? "保持左方向（推荐）" :
                native == "right" ? "保持右方向（推荐）" : "保持原按键", native),
            new ShortcutChoice("不执行动作", "none"),
            new ShortcutChoice("编辑 · 复制", "ctrl+c"),
            new ShortcutChoice("编辑 · 剪切", "ctrl+x"),
            new ShortcutChoice("编辑 · 粘贴", "ctrl+v"),
            new ShortcutChoice("编辑 · 撤销", "ctrl+z"),
            new ShortcutChoice("编辑 · 重做", "ctrl+shift+z"),
            new ShortcutChoice("编辑 · 保存", "ctrl+s"),
            new ShortcutChoice("编辑 · 全选", "ctrl+a"),
            new ShortcutChoice("导航 · 查找", "ctrl+f"),
            new ShortcutChoice("通用 · 确认 / 换行", "enter"),
            new ShortcutChoice("通用 · Esc / 取消", "escape"),
            new ShortcutChoice("导航 · 下一个焦点", "tab"),
            new ShortcutChoice("导航 · 上一个焦点", "shift+tab"),
            new ShortcutChoice("导航 · 上一页", "pageup"),
            new ShortcutChoice("导航 · 下一页", "pagedown"),
            new ShortcutChoice("系统 · 区域截图", "win+shift+s"),
            new ShortcutChoice("系统 · 显示桌面", "win+d")
        };
        if (IsSupportedMappingAction(current) && FindShortcutChoice(choices, current) == 0 &&
            !string.Equals(current, native, StringComparison.OrdinalIgnoreCase))
            choices.Add(new ShortcutChoice(CustomActionText(current), current));
        return choices;
    }

    private string FindMappingConflict(string key, string shortcut)
    {
        if (config.mappings == null || string.IsNullOrWhiteSpace(shortcut)) return "";
        string[] configurable = { "上键", "下键", "左键", "右键" };
        foreach (string candidate in configurable)
        {
            if (candidate == key || !config.mappings.ContainsKey(candidate)) continue;
            if (string.Equals(config.mappings[candidate], shortcut, StringComparison.OrdinalIgnoreCase)) return candidate;
        }
        return "";
    }

    private static int FindShortcutChoice(List<ShortcutChoice> choices, string shortcut)
    {
        for (int i = 0; i < choices.Count; i++)
            if (choices[i].Shortcut.Equals(shortcut ?? "", StringComparison.OrdinalIgnoreCase)) return i;
        return 0;
    }

    private void ApplyMappingPreset(string preset)
    {
        ApplyMappingPreset(config, preset);
    }

    private bool ConfirmMappingPresetChange(string preset, string displayName)
    {
        if (!MappingPresetChanges(config, preset)) return true;
        return MessageBox.Show(this,
            "应用“" + displayName + "”将修改上、下、左、右和 TV 键。\r\n\r\n" +
            "Home 与功能键的自定义配置会保留。是否继续？",
            "应用快捷键方案", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2) == DialogResult.Yes;
    }

    private static bool MappingPresetChanges(VibeMicConfig target, string preset)
    {
        Dictionary<string, string> actions = MappingPresetActions(preset);
        foreach (KeyValuePair<string, string> action in actions)
            if (!string.Equals(GetConfigMapping(target, action.Key, ""), action.Value,
                StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static Dictionary<string, string> MappingPresetActions(string preset)
    {
        var actions = new Dictionary<string, string>();
        if (preset == "editing")
        {
            actions["上键"] = "ctrl+z";
            actions["下键"] = "ctrl+shift+z";
            actions["左键"] = "ctrl+c";
            actions["右键"] = "ctrl+v";
            actions["TV"] = "task-switcher";
        }
        else if (preset == "review")
        {
            actions["上键"] = "volumeup";
            actions["下键"] = "volumedown";
            actions["左键"] = "left";
            actions["右键"] = "right";
            actions["TV"] = "mediaplaypause";
        }
        else
        {
            actions["上键"] = "up";
            actions["下键"] = "down";
            actions["左键"] = "left";
            actions["右键"] = "right";
            actions["TV"] = "task-switcher";
        }
        return actions;
    }

    private static void ApplyMappingPreset(VibeMicConfig target, string preset)
    {
        if (target.mappings == null) target.mappings = new Dictionary<string, string>();
        string normalized = preset == "editing" ? "editing" : preset == "review" ? "review" : "coding";
        target.mappingPreset = normalized;
        foreach (KeyValuePair<string, string> action in MappingPresetActions(normalized))
            target.mappings[action.Key] = action.Value;
    }

    private void SetMapping(string key, string value)
    {
        if (config.mappings == null) config.mappings = new Dictionary<string, string>();
        config.mappings[key] = NormalizePhysicalMappingAction(key, value);
    }

    private static string NormalizePhysicalMappingAction(string key, string action)
    {
        if (key == "左键" && string.Equals(action, "alt+left", StringComparison.OrdinalIgnoreCase))
            return "browserback";
        return action;
    }

    private static CustomButtonConfig DefaultCustomButton(int index)
    {
        return new CustomButtonConfig
        {
            slot = "custom" + (index + 1),
            label = "自定义按键 " + (index + 1),
            sourceType = "",
            vk = "",
            scan = "",
            usagePage = 0,
            usage = 0,
            action = "none",
            enabled = false
        };
    }

    private static CustomButtonConfig[] DefaultCustomButtons()
    {
        return new CustomButtonConfig[] { DefaultCustomButton(0), DefaultCustomButton(1), DefaultCustomButton(2) };
    }

    private CustomButtonConfig GetCustomButton(int index)
    {
        if (config.customButtons == null || index < 0 || index >= config.customButtons.Length) return null;
        return config.customButtons[index];
    }

    private int CountConfiguredPhysicalButtons()
    {
        int count = 0;
        string[] keys = { "确认键", "Home", "TV", "功能键", "返回键", "音量 +", "音量 -", "电源键" };
        foreach (string key in keys)
        {
            string action = GetMapping(key, "none");
            if (string.IsNullOrWhiteSpace(action) || action.Equals("none", StringComparison.OrdinalIgnoreCase)) continue;
            count++;
        }
        return count;
    }

    private static string ResolveLegacyCustomButtonKey(CustomButtonConfig button)
    {
        if (button == null) return "";
        string source = (button.sourceType ?? "keyboard").Trim().ToLowerInvariant();
        int vk = ParseConfigNumber(button.vk);
        int scan = ParseConfigNumber(button.scan);
        if (source == "consumer")
        {
            if (button.usage == 0xE9) return "音量 +";
            if (button.usage == 0xEA) return "音量 -";
        }
        if (source == "hid" && button.usagePage == 0x07)
        {
            if (button.usage == 0x80) return "音量 +";
            if (button.usage == 0x81) return "音量 -";
            if (button.usage == 0x66) return "电源键";
            if (button.usage == 0xF1) return "返回键";
        }
        if (vk == 0x24 || scan == 0x47) return "Home";
        if (vk == 0xC0 || scan == 0x29) return "TV";
        if (vk == 0x5D || scan == 0x5D) return "功能键";
        if (vk == 0x0D || scan == 0x1C) return "确认键";
        if (vk == 0xA6 || vk == 0x08 || scan == 0x0E) return "返回键";
        if (vk == 0xAF) return "音量 +";
        if (vk == 0xAE) return "音量 -";
        return "";
    }

    private static int ParseConfigNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return -1;
        try
        {
            string normalized = value.Trim();
            return normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? Convert.ToInt32(normalized.Substring(2), 16)
                : Convert.ToInt32(normalized);
        }
        catch { return -1; }
    }

    private static string CustomButtonSourceText(CustomButtonConfig button)
    {
        if (button == null || string.IsNullOrWhiteSpace(button.sourceType)) return "尚未识别按键";
        if (button.sourceType.Equals("consumer", StringComparison.OrdinalIgnoreCase))
            return "Consumer Control · 0x" + button.usage.ToString("X2");
        if (button.sourceType.Equals("hid", StringComparison.OrdinalIgnoreCase))
            return "HID Usage Page 0x" + button.usagePage.ToString("X2") + " · 0x" + button.usage.ToString("X2");
        return "键盘 · " + (button.vk ?? "") + " · Scan " + (button.scan ?? "");
    }

    private static string CustomActionText(string action)
    {
        string value = (action ?? "none").Trim().ToLowerInvariant();
        if (value == "none") return "不执行动作";
        if (value == "launch-client:chatgpt") return "打开 / 切换 ChatGPT";
        if (value == "launch-client:claude") return "打开 / 切换 Claude";
        if (value == "launch-client:deepseek") return "打开 / 切换 DeepSeek";
        if (value == "launch-client:cursor") return "打开 / 切换 Cursor";
        if (value == "launch-client:vscode") return "打开 / 切换 VS Code";
        if (value == "launch-client:codex") return "打开 / 切换 Codex";
        if (value == "ctrl+c") return "复制";
        if (value == "ctrl+x") return "剪切";
        if (value == "ctrl+v") return "粘贴";
        if (value == "ctrl+z") return "撤销";
        if (value == "ctrl+shift+z") return "重做";
        if (value == "ctrl+s") return "保存";
        if (value == "ctrl+a") return "全选";
        if (value == "ctrl+f") return "查找";
        if (value == "enter") return "确认 / 换行";
        if (value == "backspace") return "删除";
        if (value == "up") return "上方向";
        if (value == "down") return "下方向";
        if (value == "left") return "左方向";
        if (value == "right") return "右方向";
        if (value == "task-switcher") return "任务视图";
        if (value == "browserback" || value == "alt+left") return "返回上一页";
        if (value == "win+d") return "显示桌面";
        if (value == "win+shift+s") return "区域截图";
        if (value == "volumeup") return "音量增加";
        if (value == "volumedown") return "音量减少";
        if (value == "volumemute") return "静音切换";
        if (value == "mediaplaypause") return "播放 / 暂停";
        if (value.StartsWith("open-exe:", StringComparison.OrdinalIgnoreCase)) return "打开本地应用";
        if (value.StartsWith("open-url:", StringComparison.OrdinalIgnoreCase)) return "打开网页";
        if (value.StartsWith("open-app:", StringComparison.OrdinalIgnoreCase))
        {
            string[] parts = action.Substring("open-app:".Length).Split('|');
            string label = parts.Length > 2 ? DecodeActionPart(parts[2]) : "";
            return string.IsNullOrWhiteSpace(label) ? "打开 / 切换本机应用" : "打开 / 切换 " + label;
        }
        if (value.StartsWith("start-app:", StringComparison.OrdinalIgnoreCase))
        {
            string[] parts = action.Substring("start-app:".Length).Split('|');
            string label = parts.Length > 1 ? DecodeActionPart(parts[1]) : "";
            return string.IsNullOrWhiteSpace(label) ? "打开已安装应用" : "打开 " + label;
        }
        if (value.StartsWith("shortcut:", StringComparison.OrdinalIgnoreCase)) return "发送自定义快捷键";
        return action;
    }

    private static string MappingCardActionText(string action)
    {
        string text = CustomActionText(action);
        if (text == "不执行动作") return "未设置";
        if (text == "打开 / 切换本机应用" || text == "打开本地应用") return "本机应用";
        if (text == "发送自定义快捷键") return "自定义键";
        if (text.StartsWith("打开 / 切换 ", StringComparison.Ordinal))
            text = text.Substring("打开 / 切换 ".Length);
        return text.Length > 8 ? text.Substring(0, 7) + "…" : text;
    }

    private static List<ShortcutChoice> CustomActionChoices(string current)
    {
        var choices = new List<ShortcutChoice>
        {
            new ShortcutChoice("不执行动作", "none"),
            new ShortcutChoice("选择本机应用（运行中 / 已安装）…", "select-app:prompt"),
            new ShortcutChoice("打开网页…", "open-url:prompt"),
            new ShortcutChoice("客户端 · ChatGPT", "launch-client:chatgpt"),
            new ShortcutChoice("客户端 · Claude", "launch-client:claude"),
            new ShortcutChoice("客户端 · DeepSeek", "launch-client:deepseek"),
            new ShortcutChoice("开发工具 · Cursor", "launch-client:cursor"),
            new ShortcutChoice("开发工具 · VS Code", "launch-client:vscode"),
            new ShortcutChoice("开发工具 · Codex", "launch-client:codex"),
            new ShortcutChoice("编辑 · 复制", "ctrl+c"),
            new ShortcutChoice("编辑 · 剪切", "ctrl+x"),
            new ShortcutChoice("编辑 · 粘贴", "ctrl+v"),
            new ShortcutChoice("编辑 · 撤销", "ctrl+z"),
            new ShortcutChoice("编辑 · 重做", "ctrl+shift+z"),
            new ShortcutChoice("编辑 · 保存", "ctrl+s"),
            new ShortcutChoice("编辑 · 全选", "ctrl+a"),
            new ShortcutChoice("编辑 · 查找", "ctrl+f"),
            new ShortcutChoice("确认 · 换行", "enter"),
            new ShortcutChoice("编辑 · 删除", "backspace"),
            new ShortcutChoice("导航 · 浏览器返回上一页", "browserback"),
            new ShortcutChoice("系统 · 任务视图", "task-switcher"),
            new ShortcutChoice("系统 · 区域截图", "win+shift+s"),
            new ShortcutChoice("系统 · 显示桌面", "win+d"),
            new ShortcutChoice("系统 · 音量增加", "volumeup"),
            new ShortcutChoice("系统 · 音量减少", "volumedown"),
            new ShortcutChoice("系统 · 静音切换", "volumemute"),
            new ShortcutChoice("媒体 · 播放 / 暂停", "mediaplaypause"),
            new ShortcutChoice("浏览其他 EXE…", "open-exe:prompt"),
            new ShortcutChoice("发送自定义快捷键…", "shortcut:prompt")
        };
        if (!string.IsNullOrWhiteSpace(current) && !choices.Exists(delegate(ShortcutChoice choice)
            { return choice.Shortcut.Equals(current, StringComparison.OrdinalIgnoreCase); }))
            choices.Add(new ShortcutChoice(CustomActionText(current), current));
        return choices;
    }

    private string ResolveCustomActionSelection(string action)
    {
        return ResolveCustomActionSelection(action, this);
    }

    private string ResolveCustomActionSelection(string action, IWin32Window owner)
    {
        if (string.IsNullOrWhiteSpace(action)) return "";
        if (action.Equals("select-app:prompt", StringComparison.OrdinalIgnoreCase))
        {
            HostLog("APPLICATION PICKER open requested=true");
            try { return SelectApplicationAction(owner); }
            catch (Exception ex)
            {
                HostLog("APPLICATION PICKER failed=true error=" +
                    SafeLogValue(ex.GetType().Name + ":" + ex.Message));
                ShowToast("无法读取本机应用，请稍后重试或使用“浏览其他 EXE”", "error");
                return "";
            }
        }
        if (action.Equals("open-exe:prompt", StringComparison.OrdinalIgnoreCase))
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "选择要打开的应用程序";
                dialog.Filter = "应用程序 (*.exe)|*.exe|所有文件 (*.*)|*.*";
                dialog.CheckFileExists = true;
                if (dialog.ShowDialog(owner ?? this) != DialogResult.OK) return "";
                return "open-exe:" + dialog.FileName;
            }
        }
        if (action.Equals("open-url:prompt", StringComparison.OrdinalIgnoreCase))
        {
            string value = PromptForText("打开网页", "输入 http 或 https 网页地址", "https://", owner);
            Uri uri;
            if (string.IsNullOrWhiteSpace(value) || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                if (!string.IsNullOrWhiteSpace(value)) ShowToast("请输入有效的 http 或 https 地址", "warning");
                return "";
            }
            return "open-url:" + uri.AbsoluteUri;
        }
        if (action.Equals("shortcut:prompt", StringComparison.OrdinalIgnoreCase))
        {
            string value = PromptForText("自定义快捷键", "例如 ctrl+shift+p、alt+left 或 f6", "", owner);
            value = (value ?? "").Trim().ToLowerInvariant();
            if (!IsValidTranscriptionHotkey(value))
            {
                if (!string.IsNullOrWhiteSpace(value)) ShowToast("快捷键格式不正确", "warning");
                return "";
            }
            return "shortcut:" + value;
        }
        return action;
    }

    private string SelectApplicationAction(IWin32Window owner)
    {
        using (var dialog = new Form())
        using (var tabs = new TabControl())
        using (var runningPage = new TabPage("正在运行"))
        using (var installedPage = new TabPage("已安装"))
        using (var search = new TextBox())
        using (var runningList = new ListView())
        using (var installedList = new ListView())
        using (var images = new ImageList())
        using (var choose = new Button())
        using (var browse = new Button())
        using (var cancel = new Button())
        {
            dialog.Text = "选择要打开或切换的应用";
            dialog.StartPosition = FormStartPosition.CenterParent;
            dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
            dialog.MinimizeBox = false;
            dialog.MaximizeBox = false;
            dialog.ShowInTaskbar = false;
            dialog.ClientSize = new Size(760, 570);
            dialog.BackColor = cardBackground;
            dialog.Font = Font;

            var title = NewLabel("选择本机应用", 14f, FontStyle.Bold, ink);
            title.Location = new Point(24, 20);
            title.Size = new Size(700, 32);
            var help = NewLabel("自动汇总正在运行、Windows 应用目录、开始菜单和本机安装记录。", 8.8f, FontStyle.Regular, muted);
            help.Location = new Point(24, 54);
            help.Size = new Size(700, 26);

            var searchLabel = NewLabel("搜索", 8.6f, FontStyle.Bold, muted);
            searchLabel.Location = new Point(24, 88);
            searchLabel.Size = new Size(54, 32);
            searchLabel.TextAlign = ContentAlignment.MiddleLeft;
            search.Location = new Point(82, 88);
            search.Size = new Size(654, 32);
            search.Font = new Font("Microsoft YaHei UI", 10f);
            search.BackColor = inputBackground;
            search.ForeColor = ink;

            tabs.Location = new Point(24, 132);
            tabs.Size = new Size(712, 360);
            tabs.TabPages.Add(runningPage);
            tabs.TabPages.Add(installedPage);
            tabs.BackColor = cardBackground;
            runningPage.BackColor = surfaceBackground;
            installedPage.BackColor = surfaceBackground;
            images.ColorDepth = ColorDepth.Depth32Bit;
            images.ImageSize = new Size(24, 24);
            images.Images.Add("application", SystemIcons.Application.ToBitmap());
            ConfigureApplicationList(runningList, images);
            ConfigureApplicationList(installedList, images);
            var applicationLoadTimer = Stopwatch.StartNew();
            StartApplicationRecord[] startApps = GetStartApplicationRecords();
            List<ApplicationActionChoice> runningChoices = GetRunningApplicationChoices(startApps);
            List<ApplicationActionChoice> installedChoices = GetInstalledApplicationChoices(startApps);
            runningPage.Text = "正在运行 (" + runningChoices.Count + ")";
            installedPage.Text = "已安装 (" + installedChoices.Count + ")";
            applicationLoadTimer.Stop();
            HostLog("APPLICATION PICKER loaded=true running=" + runningChoices.Count +
                " installed=" + installedChoices.Count +
                " elapsed_ms=" + applicationLoadTimer.ElapsedMilliseconds);
            Action refresh = delegate
            {
                PopulateApplicationList(runningList, runningChoices, search.Text, images);
                PopulateApplicationList(installedList, installedChoices, search.Text, images);
            };
            refresh();
            search.TextChanged += delegate { refresh(); };
            runningPage.Controls.Add(runningList);
            installedPage.Controls.Add(installedList);

            string selectedAction = "";
            Action acceptSelection = delegate
            {
                ListView active = tabs.SelectedIndex == 0 ? runningList : installedList;
                ApplicationActionChoice selected = active.SelectedItems.Count == 0 ? null :
                    active.SelectedItems[0].Tag as ApplicationActionChoice;
                if (selected == null)
                {
                    ShowToast(tabs.SelectedIndex == 0 ? "当前没有可切换的应用窗口" : "请选择一个已安装应用", "info");
                    return;
                }
                selectedAction = selected.Action;
                dialog.DialogResult = DialogResult.OK;
                dialog.Close();
            };
            runningList.DoubleClick += delegate { acceptSelection(); };
            installedList.DoubleClick += delegate { acceptSelection(); };

            browse.Text = "浏览其他 EXE";
            browse.Location = new Point(24, 510);
            browse.Size = new Size(112, 36);
            browse.FlatStyle = FlatStyle.Flat;
            browse.BackColor = surfaceBackground;
            browse.ForeColor = ink;
            browse.FlatAppearance.BorderColor = line;
            browse.Click += delegate
            {
                using (var picker = new OpenFileDialog())
                {
                    picker.Title = "选择应用程序";
                    picker.Filter = "应用程序 (*.exe)|*.exe|所有文件 (*.*)|*.*";
                    picker.CheckFileExists = true;
                    if (picker.ShowDialog(dialog) != DialogResult.OK) return;
                    selectedAction = "open-exe:" + picker.FileName;
                    dialog.DialogResult = DialogResult.OK;
                    dialog.Close();
                }
            };
            choose.Text = "选择并保存";
            choose.Location = new Point(500, 510);
            choose.Size = new Size(124, 36);
            choose.BackColor = violet;
            choose.ForeColor = Color.White;
            choose.FlatStyle = FlatStyle.Flat;
            choose.FlatAppearance.BorderSize = 0;
            choose.Click += delegate { acceptSelection(); };
            cancel.Text = "取消";
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Location = new Point(636, 510);
            cancel.Size = new Size(100, 36);
            cancel.BackColor = surfaceBackground;
            cancel.ForeColor = ink;
            cancel.FlatStyle = FlatStyle.Flat;
            cancel.FlatAppearance.BorderColor = line;
            dialog.CancelButton = cancel;
            dialog.Controls.Add(title);
            dialog.Controls.Add(help);
            dialog.Controls.Add(searchLabel);
            dialog.Controls.Add(search);
            dialog.Controls.Add(tabs);
            dialog.Controls.Add(browse);
            dialog.Controls.Add(choose);
            dialog.Controls.Add(cancel);
            return dialog.ShowDialog(owner ?? this) == DialogResult.OK ? selectedAction : "";
        }
    }

    private void ConfigureApplicationList(ListView list, ImageList images)
    {
        list.Dock = DockStyle.Fill;
        list.View = View.Details;
        list.FullRowSelect = true;
        list.MultiSelect = false;
        list.HideSelection = false;
        list.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        list.BorderStyle = BorderStyle.None;
        list.Font = new Font("Microsoft YaHei UI", 9.2f);
        list.BackColor = surfaceBackground;
        list.ForeColor = ink;
        list.SmallImageList = images;
        list.Columns.Add("应用", 250, HorizontalAlignment.Left);
        list.Columns.Add("路径 / 来源", 420, HorizontalAlignment.Left);
    }

    private static void PopulateApplicationList(ListView list, List<ApplicationActionChoice> choices,
        string query, ImageList images)
    {
        string value = (query ?? "").Trim();
        list.BeginUpdate();
        list.Items.Clear();
        foreach (ApplicationActionChoice choice in choices)
        {
            if (value.Length > 0 &&
                choice.Label.IndexOf(value, StringComparison.CurrentCultureIgnoreCase) < 0 &&
                choice.Detail.IndexOf(value, StringComparison.CurrentCultureIgnoreCase) < 0) continue;
            string iconKey = AddApplicationChoiceImage(images, choice.IconReference);
            var item = new ListViewItem(choice.Label, iconKey);
            item.SubItems.Add(choice.Detail);
            item.Tag = choice;
            list.Items.Add(item);
        }
        if (list.Items.Count > 0) list.Items[0].Selected = true;
        list.EndUpdate();
    }

    private static string AddApplicationChoiceImage(ImageList images, string reference)
    {
        if (images == null || string.IsNullOrWhiteSpace(reference)) return "application";
        reference = reference.Trim();
        string key = "app-" + ComputeShortHash(reference);
        if (images.Images.ContainsKey(key)) return key;
        try
        {
            if (File.Exists(reference))
            {
                using (Icon icon = Icon.ExtractAssociatedIcon(reference))
                {
                    if (icon != null) images.Images.Add(key, icon.ToBitmap());
                }
            }
            else if (reference.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
            {
                using (Bitmap bitmap = GetShellItemIcon(reference))
                {
                    if (bitmap != null) images.Images.Add(key, new Bitmap(bitmap));
                }
            }
        }
        catch { }
        return images.Images.ContainsKey(key) ? key : "application";
    }

    private static Bitmap GetShellItemIcon(string parsingName)
    {
        IntPtr itemIdList = IntPtr.Zero;
        IntPtr iconHandle = IntPtr.Zero;
        try
        {
            uint attributes;
            if (SHParseDisplayName(parsingName, IntPtr.Zero, out itemIdList, 0, out attributes) != 0 ||
                itemIdList == IntPtr.Zero) return null;
            ShellFileInfo info;
            IntPtr result = SHGetFileInfo(itemIdList, 0, out info,
                (uint)Marshal.SizeOf(typeof(ShellFileInfo)), ShellFileInfoPidl | ShellFileInfoIcon | ShellFileInfoSmallIcon);
            if (result == IntPtr.Zero || info.IconHandle == IntPtr.Zero) return null;
            iconHandle = info.IconHandle;
            using (Icon icon = (Icon)Icon.FromHandle(iconHandle).Clone()) return icon.ToBitmap();
        }
        catch { return null; }
        finally
        {
            if (iconHandle != IntPtr.Zero) DestroyIcon(iconHandle);
            if (itemIdList != IntPtr.Zero) CoTaskMemFree(itemIdList);
        }
    }

    private static string ComputeShortHash(string value)
    {
        using (SHA256 algorithm = SHA256.Create())
        {
            byte[] digest = algorithm.ComputeHash(Encoding.UTF8.GetBytes(value ?? ""));
            return BitConverter.ToString(digest, 0, 8).Replace("-", "").ToLowerInvariant();
        }
    }

    private static List<ApplicationActionChoice> GetRunningApplicationChoices(StartApplicationRecord[] startApps)
    {
        var choices = new List<ApplicationActionChoice>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Process process in Process.GetProcesses())
        {
            try
            {
                process.Refresh();
                if (process.MainWindowHandle == IntPtr.Zero || string.IsNullOrWhiteSpace(process.MainWindowTitle) ||
                    !seen.Add(process.ProcessName)) continue;
                string executable = "";
                try { executable = process.MainModule == null ? "" : process.MainModule.FileName; } catch { }
                StartApplicationRecord startRecord = FindMatchingStartApplication(startApps, process.ProcessName, executable);
                string label = GetApplicationDisplayName(executable, process.ProcessName);
                if (string.IsNullOrWhiteSpace(label)) label = process.MainWindowTitle.Trim();
                label = TruncateApplicationText(label, 72);
                string windowTitle = TruncateApplicationText(process.MainWindowTitle.Trim(), 96);
                string detail = "正在运行";
                if (!windowTitle.Equals(label, StringComparison.CurrentCultureIgnoreCase)) detail += " · " + windowTitle;
                if (!string.IsNullOrWhiteSpace(executable)) detail += " · " + executable;
                string startAppId = startRecord == null ? "" : startRecord.AppID;
                string action = BuildOpenApplicationAction(process.ProcessName, executable, label, startAppId);
                string iconReference = !string.IsNullOrWhiteSpace(executable) ? executable :
                    startRecord == null ? "" : startRecord.IconReference;
                choices.Add(new ApplicationActionChoice(label, detail, action, iconReference));
            }
            catch { }
            finally { process.Dispose(); }
        }
        choices.Sort(delegate(ApplicationActionChoice left, ApplicationActionChoice right)
        {
            return string.Compare(left.Label, right.Label, StringComparison.CurrentCultureIgnoreCase);
        });
        return choices;
    }

    private static List<ApplicationActionChoice> GetInstalledApplicationChoices(StartApplicationRecord[] startApps)
    {
        var choices = new List<ApplicationActionChoice>();
        var seenTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ordered = new List<StartApplicationRecord>(startApps ?? new StartApplicationRecord[0]);
        ordered.Sort(delegate(StartApplicationRecord left, StartApplicationRecord right)
        {
            int quality = ApplicationRecordQuality(right).CompareTo(ApplicationRecordQuality(left));
            return quality != 0 ? quality : string.Compare(left.Name, right.Name, StringComparison.CurrentCultureIgnoreCase);
        });
        foreach (StartApplicationRecord record in ordered)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.Name) || string.IsNullOrWhiteSpace(record.AppID) ||
                !seenTargets.Add(ApplicationRecordIdentity(record))) continue;
            Uri uri;
            if (Uri.TryCreate(record.AppID.Trim(), UriKind.Absolute, out uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)) continue;
            bool directExecutable = IsExistingExecutable(record.ExecutablePath);
            string label = directExecutable ? GetApplicationDisplayName(record.ExecutablePath, record.Name) : record.Name.Trim();
            string labelToken = NormalizeApplicationToken(label);
            if (labelToken.Length == 0 || !seenNames.Add(labelToken)) continue;
            string source = string.IsNullOrWhiteSpace(record.Source) ? "本机应用" : record.Source;
            string detailPath = !string.IsNullOrWhiteSpace(record.ExecutablePath) ? record.ExecutablePath :
                Path.IsPathRooted(record.AppID.Trim()) ? record.AppID.Trim() : "";
            string detail = source + (string.IsNullOrWhiteSpace(detailPath) ? "" : " · " + detailPath);
            choices.Add(new ApplicationActionChoice(label, detail,
                "start-app:" + EncodeActionPart(record.AppID.Trim()) + "|" + EncodeActionPart(label),
                record.IconReference));
        }
        choices.Sort(delegate(ApplicationActionChoice left, ApplicationActionChoice right)
        {
            return string.Compare(left.Label, right.Label, StringComparison.CurrentCultureIgnoreCase);
        });
        return choices;
    }

    private static StartApplicationRecord[] GetStartApplicationRecords()
    {
        var records = new List<StartApplicationRecord>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddAppsFolderApplicationRecords(records, seen);
        AddStartMenuApplicationRecords(records, seen);
        AddRegisteredApplicationRecords(records, seen);
        records.Sort(delegate(StartApplicationRecord left, StartApplicationRecord right)
        {
            return string.Compare(left.Name, right.Name, StringComparison.CurrentCultureIgnoreCase);
        });
        return records.ToArray();
    }

    private static void AddAppsFolderApplicationRecords(List<StartApplicationRecord> records, HashSet<string> seen)
    {
        object shell = null;
        object folder = null;
        object items = null;
        try
        {
            // shell:AppsFolder is Windows' Unicode application catalog. It avoids
            // console code pages and the process timeouts seen with Get-StartApps.
            Type shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType == null) return;
            shell = Activator.CreateInstance(shellType);
            folder = shellType.InvokeMember("NameSpace", System.Reflection.BindingFlags.InvokeMethod,
                null, shell, new object[] { "shell:AppsFolder" });
            if (folder == null) return;
            items = folder.GetType().InvokeMember("Items", System.Reflection.BindingFlags.InvokeMethod,
                null, folder, null);
            if (items == null) return;

            Type itemsType = items.GetType();
            int count = Convert.ToInt32(itemsType.InvokeMember("Count",
                System.Reflection.BindingFlags.GetProperty, null, items, null));
            for (int index = 0; index < count; index++)
            {
                object item = null;
                try
                {
                    item = itemsType.InvokeMember("Item", System.Reflection.BindingFlags.InvokeMethod,
                        null, items, new object[] { index });
                    if (item == null) continue;
                    Type itemType = item.GetType();
                    string name = Convert.ToString(itemType.InvokeMember("Name",
                        System.Reflection.BindingFlags.GetProperty, null, item, null));
                    string appId = Convert.ToString(itemType.InvokeMember("Path",
                        System.Reflection.BindingFlags.GetProperty, null, item, null));
                    string executable = Path.IsPathRooted(appId ?? "") &&
                        string.Equals(Path.GetExtension(appId), ".exe", StringComparison.OrdinalIgnoreCase) ? appId : "";
                    string iconReference = Path.IsPathRooted(appId ?? "") ? appId :
                        "shell:AppsFolder\\" + (appId ?? "").Trim();
                    AddStartApplicationRecord(records, seen, name, appId, iconReference,
                        "Windows 应用目录", executable);
                }
                catch { }
                finally { ReleaseComObject(item); }
            }
        }
        catch { }
        finally
        {
            ReleaseComObject(items);
            ReleaseComObject(folder);
            ReleaseComObject(shell);
        }
    }

    private static void AddStartMenuApplicationRecords(List<StartApplicationRecord> records, HashSet<string> seen)
    {
        string[] roots = {
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms)
        };
        Type shellType = Type.GetTypeFromProgID("WScript.Shell");
        object shell = null;
        try
        {
            if (shellType != null) shell = Activator.CreateInstance(shellType);
            foreach (string rootPath in roots)
            {
                if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath)) continue;
                string[] shortcuts;
                try { shortcuts = Directory.GetFiles(rootPath, "*.lnk", SearchOption.AllDirectories); }
                catch { continue; }
                foreach (string shortcutPath in shortcuts)
                {
                    string name = Path.GetFileNameWithoutExtension(shortcutPath);
                    if (ShouldSkipApplicationEntry(name)) continue;
                    string target = "";
                    string iconReference = "";
                    object shortcut = null;
                    try
                    {
                        if (shell != null)
                        {
                            shortcut = shellType.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod,
                                null, shell, new object[] { shortcutPath });
                            if (shortcut != null)
                            {
                                Type shortcutType = shortcut.GetType();
                                target = NormalizeRegisteredPath(Convert.ToString(shortcutType.InvokeMember("TargetPath",
                                    System.Reflection.BindingFlags.GetProperty, null, shortcut, null)), false);
                                iconReference = NormalizeRegisteredPath(Convert.ToString(shortcutType.InvokeMember("IconLocation",
                                    System.Reflection.BindingFlags.GetProperty, null, shortcut, null)), true);
                            }
                        }
                    }
                    catch { }
                    finally { ReleaseComObject(shortcut); }
                    string executable = IsExistingExecutable(target) ? target : "";
                    if (!File.Exists(iconReference)) iconReference = !string.IsNullOrWhiteSpace(executable) ? executable : shortcutPath;
                    AddStartApplicationRecord(records, seen, name, shortcutPath, iconReference,
                        "开始菜单", executable);
                }
            }
        }
        catch { }
        finally { ReleaseComObject(shell); }
    }

    private static void AddRegisteredApplicationRecords(List<StartApplicationRecord> records, HashSet<string> seen)
    {
        RegistryView[] views = Environment.Is64BitOperatingSystem
            ? new RegistryView[] { RegistryView.Registry64, RegistryView.Registry32 }
            : new RegistryView[] { RegistryView.Registry32 };
        RegistryHive[] hives = { RegistryHive.CurrentUser, RegistryHive.LocalMachine };
        foreach (RegistryHive hive in hives)
        {
            foreach (RegistryView view in views)
            {
                RegistryKey baseKey = null;
                try
                {
                    baseKey = RegistryKey.OpenBaseKey(hive, view);
                    AddAppPathRegistryRecords(baseKey, records, seen);
                    AddUninstallRegistryRecords(baseKey, records, seen);
                }
                catch { }
                finally { if (baseKey != null) baseKey.Dispose(); }
            }
        }
    }

    private static void AddAppPathRegistryRecords(RegistryKey baseKey, List<StartApplicationRecord> records,
        HashSet<string> seen)
    {
        if (baseKey == null) return;
        using (RegistryKey appPaths = baseKey.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\App Paths"))
        {
            if (appPaths == null) return;
            foreach (string subKeyName in appPaths.GetSubKeyNames())
            {
                try
                {
                    using (RegistryKey app = appPaths.OpenSubKey(subKeyName))
                    {
                        if (app == null) continue;
                        string executable = NormalizeRegisteredPath(Convert.ToString(app.GetValue("")), false);
                        if (!IsExistingExecutable(executable) || IsPackagedApplicationPath(executable) ||
                            IsUtilityExecutable(executable)) continue;
                        string fallback = Path.GetFileNameWithoutExtension(executable);
                        string name = GetApplicationDisplayName(executable, fallback);
                        if (ShouldSkipApplicationEntry(name)) continue;
                        AddStartApplicationRecord(records, seen, name, executable, executable,
                            "本机安装记录", executable);
                    }
                }
                catch { }
            }
        }
    }

    private static void AddUninstallRegistryRecords(RegistryKey baseKey, List<StartApplicationRecord> records,
        HashSet<string> seen)
    {
        if (baseKey == null) return;
        using (RegistryKey uninstall = baseKey.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall"))
        {
            if (uninstall == null) return;
            foreach (string subKeyName in uninstall.GetSubKeyNames())
            {
                try
                {
                    using (RegistryKey app = uninstall.OpenSubKey(subKeyName))
                    {
                        if (app == null || Convert.ToInt32(app.GetValue("SystemComponent", 0)) == 1) continue;
                        string name = Convert.ToString(app.GetValue("DisplayName", "")).Trim();
                        if (ShouldSkipApplicationEntry(name)) continue;
                        string iconReference = NormalizeRegisteredPath(Convert.ToString(app.GetValue("DisplayIcon", "")), true);
                        string installLocation = NormalizeDirectoryPath(Convert.ToString(app.GetValue("InstallLocation", "")));
                        string executable = FindInstalledApplicationExecutable(name, iconReference, installLocation);
                        if (!IsExistingExecutable(executable) || IsPackagedApplicationPath(executable)) continue;
                        if (!File.Exists(iconReference)) iconReference = executable;
                        AddStartApplicationRecord(records, seen, name, executable, iconReference,
                            "本机安装记录", executable);
                    }
                }
                catch { }
            }
        }
    }

    private static string FindInstalledApplicationExecutable(string displayName, string displayIcon, string installLocation)
    {
        if (IsExistingExecutable(displayIcon) && !IsUtilityExecutable(displayIcon)) return displayIcon;
        if (string.IsNullOrWhiteSpace(installLocation) || !Directory.Exists(installLocation)) return "";
        string best = "";
        int bestScore = int.MinValue;
        int inspected = 0;
        int usableCandidates = 0;
        try
        {
            foreach (string candidate in Directory.EnumerateFiles(installLocation, "*.exe", SearchOption.TopDirectoryOnly))
            {
                if (++inspected > 256) break;
                if (IsUtilityExecutable(candidate)) continue;
                usableCandidates++;
                int score = ScoreInstalledExecutable(displayName, candidate);
                if (score <= bestScore) continue;
                bestScore = score;
                best = candidate;
            }
        }
        catch { return ""; }
        return bestScore >= 80 || (usableCandidates == 1 && bestScore > 0) ? best : "";
    }

    private static int ScoreInstalledExecutable(string displayName, string executable)
    {
        string fileName = Path.GetFileNameWithoutExtension(executable) ?? "";
        string fileToken = NormalizeApplicationToken(fileName);
        string displayToken = NormalizeApplicationToken(displayName);
        int score = 10;
        if (displayToken.Length > 0 && fileToken == displayToken) score += 160;
        else if (displayToken.Length > 2 && (fileToken.Contains(displayToken) || displayToken.Contains(fileToken))) score += 90;
        string metadataName = NormalizeApplicationToken(GetApplicationDisplayName(executable, ""));
        if (metadataName.Length > 0 && metadataName == displayToken) score += 180;
        else if (metadataName.Length > 2 && displayToken.Length > 2 &&
            (metadataName.Contains(displayToken) || displayToken.Contains(metadataName))) score += 100;
        return score;
    }

    private static bool IsUtilityExecutable(string executable)
    {
        string fileName = (Path.GetFileNameWithoutExtension(executable ?? "") ?? "").ToLowerInvariant();
        string[] utilityTokens = {
            "unins", "uninstall", "setup", "installer", "update", "updater", "crash",
            "report", "elevate", "repair", "modify", "vcredist", "vc_redist"
        };
        foreach (string token in utilityTokens) if (fileName.Contains(token)) return true;
        return false;
    }

    private static bool IsPackagedApplicationPath(string executable)
    {
        return (executable ?? "").IndexOf("\\WindowsApps\\", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool ShouldSkipApplicationEntry(string name)
    {
        string value = (name ?? "").Trim();
        if (value.Length == 0 || value.IndexOf('\uFFFD') >= 0) return true;
        string lower = value.ToLowerInvariant();
        return lower.Contains("uninstall") || lower.Contains("unins") || lower.Contains("卸载") ||
            lower.Contains("security update") || lower.StartsWith("update for ");
    }

    private static string NormalizeRegisteredPath(string value, bool stripIconIndex)
    {
        string expanded = Environment.ExpandEnvironmentVariables((value ?? "").Trim());
        if (expanded.StartsWith("@", StringComparison.Ordinal)) expanded = expanded.Substring(1).Trim();
        if (expanded.Length == 0) return "";
        string path = expanded;
        if (expanded[0] == '"')
        {
            int closingQuote = expanded.IndexOf('"', 1);
            if (closingQuote > 1) path = expanded.Substring(1, closingQuote - 1);
        }
        else
        {
            Match match = Regex.Match(expanded,
                @"^(.+?\.(?:exe|dll|ico|lnk|appref-ms))(?:(?:\s*,\s*-?\d+)|(?:\s+.*))?$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (match.Success) path = match.Groups[1].Value;
            else if (stripIconIndex)
            {
                int comma = expanded.LastIndexOf(',');
                int ignoredIndex;
                if (comma > 0 && int.TryParse(expanded.Substring(comma + 1).Trim(), out ignoredIndex))
                    path = expanded.Substring(0, comma);
            }
        }
        return path.Trim().Trim('"');
    }

    private static string NormalizeDirectoryPath(string value)
    {
        return Environment.ExpandEnvironmentVariables((value ?? "").Trim().Trim('"'));
    }

    private static bool IsExistingExecutable(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(value);
    }

    private static string GetApplicationDisplayName(string executable, string fallback)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(executable) && File.Exists(executable))
            {
                FileVersionInfo info = FileVersionInfo.GetVersionInfo(executable);
                string product = CleanApplicationName(info.ProductName);
                if (!string.IsNullOrWhiteSpace(product) &&
                    NormalizeApplicationToken(product).IndexOf("windowsoperatingsystem",
                        StringComparison.OrdinalIgnoreCase) < 0) return product;
                string description = CleanApplicationName(info.FileDescription);
                if (!string.IsNullOrWhiteSpace(description)) return description;
            }
        }
        catch { }
        return CleanApplicationName(fallback);
    }

    private static string CleanApplicationName(string value)
    {
        string name = (value ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
        return name.IndexOf('\uFFFD') >= 0 ? "" : Regex.Replace(name, @"\s+", " ");
    }

    private static string NormalizeApplicationToken(string value)
    {
        var token = new StringBuilder();
        foreach (char character in (value ?? "").ToLowerInvariant())
            if (char.IsLetterOrDigit(character)) token.Append(character);
        return token.ToString();
    }

    private static string TruncateApplicationText(string value, int maximumLength)
    {
        string text = CleanApplicationName(value);
        return text.Length <= maximumLength ? text : text.Substring(0, Math.Max(1, maximumLength - 3)) + "...";
    }

    private static string ApplicationRecordIdentity(StartApplicationRecord record)
    {
        string value = !string.IsNullOrWhiteSpace(record.ExecutablePath) ? record.ExecutablePath : record.AppID;
        if (Path.IsPathRooted(value ?? ""))
        {
            try { value = Path.GetFullPath(value); } catch { }
        }
        return (value ?? "").Trim();
    }

    private static int ApplicationRecordQuality(StartApplicationRecord record)
    {
        if (record == null) return 0;
        if (IsExistingExecutable(record.ExecutablePath)) return 400;
        if (Path.IsPathRooted(record.AppID ?? "") && File.Exists(record.AppID)) return 300;
        if ((record.IconReference ?? "").StartsWith("shell:", StringComparison.OrdinalIgnoreCase)) return 220;
        return 180;
    }

    private static void AddStartApplicationRecord(List<StartApplicationRecord> records,
        HashSet<string> seen, string name, string appId)
    {
        AddStartApplicationRecord(records, seen, name, appId, "", "", "");
    }

    private static void AddStartApplicationRecord(List<StartApplicationRecord> records,
        HashSet<string> seen, string name, string appId, string iconReference, string source, string executablePath)
    {
        name = CleanApplicationName(name);
        appId = (appId ?? "").Trim();
        string identity = appId;
        if (Path.IsPathRooted(appId))
        {
            try { identity = Path.GetFullPath(appId); } catch { }
        }
        if (ShouldSkipApplicationEntry(name) || appId.Length == 0 || appId.IndexOf('\uFFFD') >= 0 || !seen.Add(identity)) return;
        records.Add(new StartApplicationRecord
        {
            Name = name,
            AppID = appId,
            IconReference = (iconReference ?? "").Trim(),
            Source = (source ?? "").Trim(),
            ExecutablePath = (executablePath ?? "").Trim()
        });
    }

    private static void ReleaseComObject(object value)
    {
        if (value == null || !System.Runtime.InteropServices.Marshal.IsComObject(value)) return;
        try { System.Runtime.InteropServices.Marshal.FinalReleaseComObject(value); }
        catch { }
    }

    private static StartApplicationRecord FindMatchingStartApplication(StartApplicationRecord[] records,
        string processName, string executable)
    {
        string executableName = "";
        try { executableName = Path.GetFileName(executable ?? ""); } catch { }
        foreach (StartApplicationRecord record in records ?? new StartApplicationRecord[0])
        {
            if (record == null || string.IsNullOrWhiteSpace(record.AppID)) continue;
            string appId = record.AppID.Trim();
            if ((!string.IsNullOrWhiteSpace(executable) &&
                 string.Equals(record.ExecutablePath, executable, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(executableName) &&
                 (appId.EndsWith("\\" + executableName, StringComparison.OrdinalIgnoreCase) ||
                  (record.ExecutablePath ?? "").EndsWith("\\" + executableName, StringComparison.OrdinalIgnoreCase))) ||
                appId.Equals(processName ?? "", StringComparison.OrdinalIgnoreCase) ||
                appId.StartsWith((processName ?? "") + "_", StringComparison.OrdinalIgnoreCase))
                return record;
        }
        return null;
    }

    private static string EncodeActionPart(string value)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? ""));
    }

    private static string BuildOpenApplicationAction(string processName, string executable, string label)
    {
        return BuildOpenApplicationAction(processName, executable, label, "");
    }

    private static string BuildOpenApplicationAction(string processName, string executable, string label, string startAppId)
    {
        return "open-app:" + EncodeActionPart(processName) + "|" +
            EncodeActionPart(executable) + "|" + EncodeActionPart(label) + "|" +
            EncodeActionPart(startAppId);
    }

    private static string DecodeActionPart(string value)
    {
        try { return Encoding.UTF8.GetString(Convert.FromBase64String(value ?? "")); }
        catch { return ""; }
    }

    private string PromptForText(string title, string prompt, string initial)
    {
        return PromptForText(title, prompt, initial, this);
    }

    private string PromptForText(string title, string prompt, string initial, IWin32Window owner)
    {
        using (var dialog = new Form())
        using (var label = new Label())
        using (var input = new TextBox())
        using (var ok = new Button())
        using (var cancel = new Button())
        {
            dialog.Text = title;
            dialog.StartPosition = FormStartPosition.CenterParent;
            dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
            dialog.MinimizeBox = false;
            dialog.MaximizeBox = false;
            dialog.ShowInTaskbar = false;
            dialog.ClientSize = new Size(520, 148);
            dialog.Font = Font;
            label.Text = prompt;
            label.Location = new Point(18, 16);
            label.Size = new Size(480, 28);
            input.Text = initial ?? "";
            input.Location = new Point(18, 50);
            input.Size = new Size(480, 30);
            ok.Text = "确定";
            ok.DialogResult = DialogResult.OK;
            ok.Location = new Point(320, 100);
            ok.Size = new Size(84, 30);
            cancel.Text = "取消";
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Location = new Point(414, 100);
            cancel.Size = new Size(84, 30);
            dialog.AcceptButton = ok;
            dialog.CancelButton = cancel;
            dialog.Controls.Add(label);
            dialog.Controls.Add(input);
            dialog.Controls.Add(ok);
            dialog.Controls.Add(cancel);
            return dialog.ShowDialog(owner ?? this) == DialogResult.OK ? input.Text : "";
        }
    }

    private void BeginCustomButtonCapture(int slot, Label status)
    {
        if (slot < 0 || slot >= 3) return;
        string token = Guid.NewGuid().ToString("N");
        pendingCustomCaptureToken = token;
        try
        {
            var request = new Dictionary<string, object>();
            request["active"] = true;
            request["token"] = token;
            request["slot"] = slot;
            request["created_at"] = DateTime.UtcNow.ToString("o");
            File.WriteAllText(Path.Combine(root, "custom-button-capture-request.json"),
                new JavaScriptSerializer().Serialize(request), Encoding.UTF8);
            File.Delete(Path.Combine(root, "custom-button-capture-result.json"));
            StartKeyboardBridge();
            if (status != null) status.Text = "请在 10 秒内按一下遥控器按键…";
            ShowToast("正在等待第 " + (slot + 1) + " 个自定义按键", "info");
        }
        catch (Exception ex)
        {
            pendingCustomCaptureToken = "";
            if (status != null) status.Text = "识别请求失败，请重试";
            Log("Custom button capture request failed: " + ex.Message);
        }
    }

    private void ClearPendingCustomButtonCapture(string reason)
    {
        try
        {
            string requestPath = Path.Combine(root, "custom-button-capture-request.json");
            string resultPath = Path.Combine(root, "custom-button-capture-result.json");
            bool cleared = false;
            if (File.Exists(requestPath)) { File.Delete(requestPath); cleared = true; }
            if (File.Exists(resultPath)) { File.Delete(resultPath); cleared = true; }
            pendingCustomCaptureToken = "";
            if (cleared) Log("Custom button capture cancelled reason=" + (reason ?? "unknown"));
        }
        catch (Exception ex) { Log("Custom button capture cleanup failed: " + ex.Message); }
    }

    private void TestCustomButtonAction(int slot)
    {
        try
        {
            string token = Guid.NewGuid().ToString("N");
            var request = new Dictionary<string, object>();
            request["slot"] = slot;
            request["token"] = token;
            request["created_at"] = DateTime.UtcNow.ToString("o");
            PrepareMappingActionTest(token, "自定义按键 " + (slot + 1));
            File.WriteAllText(Path.Combine(root, "custom-button-test.json"),
                new JavaScriptSerializer().Serialize(request), Encoding.UTF8);
            StartKeyboardBridge();
            ShowToast("正在验证自定义按键动作…", "info");
        }
        catch (Exception ex) { Log("Custom button test request failed: " + ex.Message); }
    }

    private void TestMappingAction(string key, string action)
    {
        if (string.IsNullOrWhiteSpace(action) || action.Equals("none", StringComparison.OrdinalIgnoreCase) ||
            action.Equals("managed", StringComparison.OrdinalIgnoreCase) ||
            action.Equals("direction-volume-fallback", StringComparison.OrdinalIgnoreCase))
        {
            ShowToast("这个按键使用系统托管功能，请直接按遥控器测试", "info");
            return;
        }
        try
        {
            string token = Guid.NewGuid().ToString("N");
            var request = new Dictionary<string, object>();
            request["name"] = "ui_mapping_test";
            request["label"] = key;
            request["action"] = action;
            request["token"] = token;
            request["created_at"] = DateTime.UtcNow.ToString("o");
            PrepareMappingActionTest(token, key);
            File.WriteAllText(Path.Combine(root, "custom-button-test.json"),
                new JavaScriptSerializer().Serialize(request), Encoding.UTF8);
            StartKeyboardBridge();
            ShowToast("正在验证“" + key + "”的当前功能…", "info");
        }
        catch (Exception ex) { Log("Mapping action test request failed: " + ex.Message); }
    }

    private void PrepareMappingActionTest(string token, string label)
    {
        pendingMappingTestToken = token ?? "";
        pendingMappingTestLabel = label ?? "按键";
        pendingMappingTestStartedAt = DateTime.UtcNow;
        try
        {
            string resultPath = Path.Combine(root, "custom-button-test-result.json");
            if (File.Exists(resultPath)) File.Delete(resultPath);
        }
        catch { }
    }

    private void ApplyMappingActionTestResult()
    {
        if (string.IsNullOrWhiteSpace(pendingMappingTestToken)) return;
        string path = Path.Combine(root, "custom-button-test-result.json");
        if (!File.Exists(path))
        {
            if (pendingMappingTestStartedAt != DateTime.MinValue &&
                (DateTime.UtcNow - pendingMappingTestStartedAt).TotalSeconds > 8)
            {
                ShowToast(pendingMappingTestLabel + "测试超时，请检查按键桥接是否正在运行", "error");
                pendingMappingTestToken = "";
            }
            return;
        }
        try
        {
            MappingActionTestResult result = new JavaScriptSerializer().Deserialize<MappingActionTestResult>(
                File.ReadAllText(path, Encoding.UTF8));
            if (result == null || !string.Equals(result.token, pendingMappingTestToken,
                StringComparison.OrdinalIgnoreCase)) return;
            string message = result.success ? pendingMappingTestLabel + "测试成功 · " + result.message :
                pendingMappingTestLabel + "测试失败 · " + result.message;
            ShowToast(message, result.success ? "success" : "error");
            HostLog("MAPPING TEST label=" + SafeLogValue(pendingMappingTestLabel) +
                " action=" + SafeLogValue(result.action) + " success=" + result.success);
            pendingMappingTestToken = "";
            pendingMappingTestLabel = "";
            pendingMappingTestStartedAt = DateTime.MinValue;
            File.Delete(path);
        }
        catch (Exception ex) { Log("Mapping action test result failed: " + ex.Message); }
    }

    private void ApplyCustomButtonCaptureResult()
    {
        if (string.IsNullOrWhiteSpace(pendingCustomCaptureToken)) return;
        string path = Path.Combine(root, "custom-button-capture-result.json");
        if (!File.Exists(path)) return;
        try
        {
            CustomButtonCaptureResult result = new JavaScriptSerializer().Deserialize<CustomButtonCaptureResult>(File.ReadAllText(path, Encoding.UTF8));
            if (result == null || !string.Equals(result.token, pendingCustomCaptureToken, StringComparison.OrdinalIgnoreCase)) return;
            if (result.slot < 0 || result.slot >= 3) { pendingCustomCaptureToken = ""; return; }
            CustomButtonConfig button = GetCustomButton(result.slot);
            if (button == null) { pendingCustomCaptureToken = ""; return; }
            button.sourceType = result.sourceType ?? "keyboard";
            button.vk = result.vk ?? "";
            button.scan = result.scan ?? "";
            button.usagePage = result.usagePage;
            button.usage = result.usage;
            button.enabled = !string.IsNullOrWhiteSpace(button.action) && !button.action.Equals("none", StringComparison.OrdinalIgnoreCase);
            SaveConfig();
            if (customButtonStatusLabels[result.slot] != null && !customButtonStatusLabels[result.slot].IsDisposed)
                customButtonStatusLabels[result.slot].Text = CustomButtonSourceText(button);
            ShowToast("已识别 " + CustomButtonSourceText(button) + "，请选择动作后保存", "success");
            pendingCustomCaptureToken = "";
            File.Delete(path);
        }
        catch (Exception ex) { Log("Custom button capture result failed: " + ex.Message); }
    }

    private void ExportConfig()
    {
        var dialog = new SaveFileDialog();
        dialog.Filter = "JSON 配置|*.json";
        dialog.FileName = "vibe-flow-config.json";
        if (dialog.ShowDialog() != DialogResult.OK) return;
        File.Copy(configPath, dialog.FileName, true);
        ShowToast("配置备份已保存", "success");
    }

    private void ImportConfig()
    {
        using (var dialog = new OpenFileDialog())
        {
            dialog.Filter = "Vibe Flow 配置|*.json|所有文件|*.*";
            dialog.Title = "导入 Vibe Flow 配置";
            dialog.CheckFileExists = true;
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                VibeMicConfig imported = new JavaScriptSerializer().Deserialize<VibeMicConfig>(
                    File.ReadAllText(dialog.FileName, Encoding.UTF8));
                if (imported == null) throw new InvalidDataException("配置内容为空");
                ApplyImportedConfig(imported, "导入配置");
            }
            catch (Exception ex)
            {
                HostLog("CONFIG IMPORT failed=true error=" + SafeLogValue(ex.Message));
                ShowToast("配置无法导入，请确认文件来自 Vibe Flow", "error");
            }
        }
    }

    private void RestorePreviousConfig()
    {
        string backupPath = configPath + ".bak";
        if (!File.Exists(backupPath))
        {
            ShowToast("当前没有可恢复的上次配置", "info");
            return;
        }
        if (MessageBox.Show(this, "恢复最近一次配置备份？当前配置会自动成为新的备份。",
            "恢复上次配置", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
        try
        {
            VibeMicConfig recovered = new JavaScriptSerializer().Deserialize<VibeMicConfig>(
                File.ReadAllText(backupPath, Encoding.UTF8));
            if (recovered == null) throw new InvalidDataException("备份内容为空");
            ApplyImportedConfig(recovered, "恢复上次配置");
        }
        catch (Exception ex)
        {
            HostLog("CONFIG RESTORE failed=true error=" + SafeLogValue(ex.Message));
            ShowToast("上次配置无法恢复", "error");
        }
    }

    private void ApplyImportedConfig(VibeMicConfig imported, string source)
    {
        bool captureWasRunning = IsCapturing;
        NormalizeImportedConfig(imported);
        config = imported;
        WriteConfigAtomically(config);
        SetLaunchAtStartup(config.launchAtStartup);
        SyncKeyboardBridgeConfig();
        if (captureWasRunning) RestartCaptureForAudioSettings();
        ApplyThemePalette();
        RebuildShellForTheme();
        HostLog("CONFIG IMPORT applied=true source=" + SafeLogValue(source) +
            " schema=" + config.schemaVersion + " stable_voice=" + HasStableVoiceProfile(config));
        ShowToast(source + "成功，稳定语音参数已保留", "success");
    }

    private static void NormalizeImportedConfig(VibeMicConfig imported)
    {
        MigrateConfig(imported);
        ApplyStableVoiceProfile(imported);
        if (!IsValidTranscriptionHotkey(imported.inputMethodHotkey))
            imported.inputMethodHotkey = DefaultHotkeyForProvider(imported.inputMethod);
    }

    private void CaptureNextAudioDiagnostic()
    {
        if (!IsCapturing)
        {
            Toast("请先启动语音桥接");
            return;
        }

        string diagnosticGesture = "下一次按住录音键时";
        DialogResult consent = MessageBox.Show(this,
            "仅" + diagnosticGesture + "，言灵会在本机保存三份音频：遥控器解码原声、处理后声音和 CABLE Output。最长 30 秒，完成后自动关闭，可随时删除。\r\n\r\n请说：测试麦克风，一二三四五六，期待效果。",
            "采集下一段诊断音频", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
        if (consent != DialogResult.OK) return;

        try
        {
            using (EventWaitHandle handle = EventWaitHandle.OpenExisting("Local\\VibeMicCaptureAudioDiagnostic"))
                handle.Set();
            Log("One-shot audio diagnostic armed by user.");
            Toast("已就绪，请按住录音键说提示短句，完成后松开");
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
            report.AppendLine("Mappings: " + BuildDiagnosticMappingSummary(config.mappings));
            report.AppendLine();
            AppendLogTail(report, Path.Combine(sessionDir, "vibe-mic-runtime.log"), "Runtime log", 200);
            AppendLogTail(report, Path.Combine(root, "input-bridge-log.txt"), "Input bridge log", 200);
            string captureReport = Path.Combine(sessionDir, "remote-voice-report.json");
            if (File.Exists(captureReport))
            {
                report.AppendLine("Capture report");
                report.AppendLine(SanitizeDiagnosticText(File.ReadAllText(captureReport, Encoding.UTF8)));
            }
            string captureHealthPath = Path.Combine(sessionDir, "capture-health.json");
            if (File.Exists(captureHealthPath))
            {
                report.AppendLine("Capture heartbeat");
                report.AppendLine(SanitizeDiagnosticText(File.ReadAllText(captureHealthPath, Encoding.UTF8)));
            }
            File.WriteAllText(dialog.FileName, SanitizeDiagnosticText(report.ToString()), new UTF8Encoding(false));
            ShowToast("诊断已导出，不包含录音和识别文字", "success");
        }
        catch (Exception ex) { ShowToast("诊断导出失败", "error"); Log("Diagnostics export failed: " + ex.Message); }
    }

    private void AppendLogTail(StringBuilder output, string path, string title, int maximumLines)
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
        for (int i = start; i < lines.Length; i++) output.AppendLine(SanitizeDiagnosticText(lines[i]));
        output.AppendLine();
    }

    private static string BuildDiagnosticMappingSummary(Dictionary<string, string> mappings)
    {
        if (mappings == null || mappings.Count == 0) return "none";
        var parts = new List<string>();
        foreach (KeyValuePair<string, string> pair in mappings)
        {
            string action = pair.Value ?? "none";
            string kind = action.IndexOf(':') > 0 ? action.Substring(0, action.IndexOf(':')) : action;
            parts.Add(pair.Key + "=" + kind);
        }
        parts.Sort(StringComparer.Ordinal);
        return string.Join(", ", parts.ToArray());
    }

    private static string SanitizeDiagnosticText(string value)
    {
        string result = value ?? "";
        result = Regex.Replace(result, @"(?i)[A-Z]:\\Users\\[^\\\r\n]+", "%USERPROFILE%");
        result = Regex.Replace(result, @"(?im)(name=)\\\\\?\\HID#[^\r\n]+", "$1<HID_DEVICE_REDACTED>");
        result = Regex.Replace(result, @"(?i)hash:[0-9A-F]{6,}", "hash:<redacted>");
        result = Regex.Replace(result, @"(?i)(address|device_address|bluetooth_address)=([0-9A-F:-]{8,})", "$1=<redacted>");
        result = Regex.Replace(result, @"(?i)(action=)(open-app|open-exe|open-url|start-app):[^\s]+", "$1$2:<redacted>");
        result = Regex.Replace(result, @"(?i)https?://[^\s\r\n]+", "<URL_REDACTED>");
        string userName = Environment.UserName;
        string machineName = Environment.MachineName;
        if (!string.IsNullOrWhiteSpace(userName))
            result = Regex.Replace(result, Regex.Escape(userName), "<USER>", RegexOptions.IgnoreCase);
        if (!string.IsNullOrWhiteSpace(machineName))
            result = Regex.Replace(result, Regex.Escape(machineName), "<PC>", RegexOptions.IgnoreCase);
        return result;
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

    private const uint ShellFileInfoIcon = 0x00000100;
    private const uint ShellFileInfoSmallIcon = 0x00000001;
    private const uint ShellFileInfoPidl = 0x00000008;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHParseDisplayName(string name, IntPtr bindingContext,
        out IntPtr itemIdList, uint attributesIn, out uint attributesOut);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(IntPtr path, uint fileAttributes,
        out ShellFileInfo fileInfo, uint fileInfoSize, uint flags);

    [DllImport("ole32.dll")]
    private static extern void CoTaskMemFree(IntPtr memory);

    [DllImport("user32.dll")]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

    [DllImport("shcore.dll")]
    private static extern int SetProcessDpiAwareness(int awareness);

    [DllImport("user32.dll")]
    private static extern bool SetProcessDPIAware();

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern int GetGuiResources(IntPtr process, int flags);

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

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShellFileInfo
    {
        public IntPtr IconHandle;
        public int IconIndex;
        public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string TypeName;
    }

    private sealed class BridgeHealthSnapshot
    {
        public bool Healthy;
        public bool HookInstalled;
        public bool RawInputRegistered;
        public bool RawInputDevicePresent;
        public string InputRoutingMode = "";
        public string RoutingAuthority = "";
        public string RoutingIsolation = "";
        public long RawRemoteEdges;
        public long RawActionEdges;
        public long FilterActionEdges;
        public long HookCandidatePassthroughs;
        public string LastRawAction = "";
        public string LastActionSource = "";
        public DateTime LastRawActionAtUtc = DateTime.MinValue;
        public double LastRawActionAgeSeconds = double.MaxValue;
        public long LastExecutionSequence;
        public string LastExecutionButton = "";
        public string LastExecutionLabel = "";
        public string LastExecutionTrigger = "";
        public string LastExecutionAction = "";
        public string LastExecutionSource = "";
        public string LastExecutionProfileId = "";
        public string LastExecutionProfileName = "";
        public string LastExecutionRevision = "";
        public bool LastExecutionSuccess;
        public DateTime LastExecutionAtUtc = DateTime.MinValue;
        public double LastExecutionAgeSeconds = double.MaxValue;
        public bool FilterAvailable;
        public bool FilterHealthy;
        public string FilterState = "";
        public int ConfigVersion;
        public string ConfigRevision = "";
        public int ConfigMappingCount;
        public string ConfigError = "";
        public string InstallRoot = "";
        public DateTime ConfigLoadedAtUtc = DateTime.MinValue;
        public string State = "";
        public string LastInputKind = "";
        public DateTime LastInputAtUtc = DateTime.MinValue;
        public double FileAgeSeconds = double.MaxValue;
        public double LastInputAgeSeconds = double.MaxValue;
    }

    private sealed class ProcessTopologySnapshot
    {
        public int TotalCount;
        public int CurrentRootCount;
        public int ForeignCount;
        public int InaccessibleCount;
    }

    private sealed class SelfCheckItem
    {
        public string Id;
        public string Title;
        public string State;
        public string Detail;
        public string Expected;
        public string Actual;
        public string Cause;
        public string NextStep;
        public string ActionText;
        public string Action;
        public SelfCheckItem(string id, string title, string state, string detail, string actionText, string action)
        {
            Id = id;
            Title = title;
            State = state;
            Detail = detail;
            Expected = "此项符合发布版要求";
            Actual = detail;
            Cause = state == "pass" ? "未发现异常" : detail;
            NextStep = state == "pass" ? "无需操作" : actionText;
            ActionText = actionText;
            Action = action;
        }

        public SelfCheckItem(string id, string title, string state, string expected, string actual,
            string cause, string nextStep, string actionText, string action)
        {
            Id = id;
            Title = title;
            State = state;
            Expected = expected;
            Actual = actual;
            Cause = cause;
            NextStep = nextStep;
            Detail = actual;
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
        public int CheckingCount;
        public int UnsupportedCount;
        public string Headline = "正在检查";
        public string Detail = "";
    }

    private sealed class WindowsHardwareProbe
    {
        public bool Completed;
        public bool Failed;
        public bool BluetoothPresent;
        public bool BluetoothOk;
        public bool RemotePresent;
        public bool RemoteOk;
        public string Error = "";
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
        public bool InputTargetObserved;
        public bool InputTargetCaptured;
        public bool InputTargetReady;
        public bool DeliveryFailed;
        public bool TransportFailed;
        public bool AudioLive;
        public string DeliveryMode = "";
        public bool Success;
        public bool Failed;
        public int AudioMs;
        public int ElapsedMs;
        public int SegmentCount;
        public int TransportRecoveryCount;
        public int AudioStallCount;
        public int MicExtendWrites;
        public int LastPacketAgeMs = -1;
        public int TriggerToReadyMs;
        public int MaxGapMs;
        public int QueueDrops;
        public int SinkQueueDrops;
        public int PendingAfterDrain;
        public int DrainWaitMs;
        public double RawRmsPercent;
        public double OutputRmsPercent;
        public double AudioCoveragePercent;
        public string WasapiState = "";
        public string EndpointState = "";
        public string DefaultRouteState = "";
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
        public int onboardingStep { get; set; }
        public bool resumeSetupAfterRestart { get; set; }
        public string theme { get; set; }
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
        public bool autoCheckUpdates { get; set; }
        public int drainMs { get; set; }
        public string inputRoutingMode { get; set; }
        public string mappingPreset { get; set; }
        public Dictionary<string, string> mappings { get; set; }
        public string activeShortcutProfileId { get; set; }
        public ShortcutProfileConfig[] shortcutProfiles { get; set; }
        public CustomButtonConfig[] customButtons { get; set; }

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
            c.onboardingStep = 0;
            c.resumeSetupAfterRestart = false;
            c.theme = "light";
            c.launchAtStartup = false;
            c.startBridgeOnLaunch = false;
            c.minimizeToTray = true;
            c.audioEndpointName = "CABLE Input";
            c.inputMethod = "wechat";
            c.inputMethodHotkey = WeChatStableHotkey;
            c.inputMethodTrigger = "toggle";
            c.providerStartupDelayMs = 80;
            c.audioProcessingMode = "speech";
            c.autoRouteVirtualMicrophone = true;
            c.soundFeedbackEnabled = true;
            c.autoCheckUpdates = true;
            c.drainMs = 180;
            c.inputRoutingMode = "strict";
            c.mappingPreset = "general";
            c.shortcutProfiles = DefaultShortcutProfiles();
            c.activeShortcutProfileId = "general";
            c.mappings = CloneMappings(c.shortcutProfiles[0].mappings);
            c.customButtons = null;
            return c;
        }
    }

    private sealed class ShortcutProfileConfig
    {
        public string id { get; set; }
        public string name { get; set; }
        public string preset { get; set; }
        public Dictionary<string, string> mappings { get; set; }
    }

    private sealed class ShortcutProfileExport
    {
        public string format { get; set; }
        public int version { get; set; }
        public ShortcutProfileConfig profile { get; set; }
    }

    private sealed class CustomButtonConfig
    {
        public string slot { get; set; }
        public string label { get; set; }
        public string sourceType { get; set; }
        public string vk { get; set; }
        public string scan { get; set; }
        public int usagePage { get; set; }
        public int usage { get; set; }
        public string action { get; set; }
        public bool enabled { get; set; }
    }

    private sealed class CustomButtonCaptureResult
    {
        public string token { get; set; }
        public int slot { get; set; }
        public string sourceType { get; set; }
        public string vk { get; set; }
        public string scan { get; set; }
        public int usagePage { get; set; }
        public int usage { get; set; }
    }

    private sealed class MappingActionTestResult
    {
        public string token { get; set; }
        public string action { get; set; }
        public bool success { get; set; }
        public string message { get; set; }
        public string completed_at { get; set; }
    }

    private sealed class ShortcutChoice
    {
        public readonly string Label;
        public readonly string Shortcut;
        public ShortcutChoice(string label, string shortcut) { Label = label; Shortcut = shortcut; }
        public override string ToString() { return Label; }
    }

    private sealed class ShortcutProfileChoice
    {
        public readonly ShortcutProfileConfig Profile;
        public ShortcutProfileChoice(ShortcutProfileConfig profile) { Profile = profile; }
        public override string ToString()
        {
            if (Profile == null) return "未命名 Profile";
            bool official = Profile.id == "general" || Profile.id == "vibe-coding" ||
                Profile.id == "browser-ai" || Profile.id == "terminal-agent";
            return (Profile.name ?? "未命名 Profile") + (official ? "  ·  官方" : "");
        }
    }

    private sealed class ApplicationActionChoice
    {
        public readonly string Label;
        public readonly string Detail;
        public readonly string Action;
        public readonly string IconReference;
        public ApplicationActionChoice(string label, string detail, string action)
            : this(label, detail, action, "")
        {
        }
        public ApplicationActionChoice(string label, string detail, string action, string iconReference)
        {
            Label = label;
            Detail = detail;
            Action = action;
            IconReference = iconReference ?? "";
        }
        public override string ToString() { return Label + "    " + Detail; }
    }

    private sealed class StartApplicationRecord
    {
        public string Name { get; set; }
        public string AppID { get; set; }
        public string IconReference { get; set; }
        public string Source { get; set; }
        public string ExecutablePath { get; set; }
    }
}

internal sealed class SecureUpdateInfo
{
    public string Version;
    public string InstallerUrl;
    public string ChecksumUrl;
    public string ExpectedSha256;
    public bool IsNewer;
}

internal static class SecureUpdateClient
{
    private const string LatestReleaseApi = "https://api.github.com/repos/richlearntodo-debug/vibe-flow/releases/latest";
    private const string InstallerAssetName = "VibeFlow-Setup.exe";
    private const string ChecksumAssetName = "SHA256SUMS.txt";

    public static SecureUpdateInfo GetLatest(string currentVersion)
    {
        ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
        try
        {
            string payload;
            using (TimeoutWebClient client = CreateClient(20000)) payload = client.DownloadString(LatestReleaseApi);
            GitHubRelease release = new JavaScriptSerializer().Deserialize<GitHubRelease>(payload);
            if (release == null || release.draft || release.prerelease || string.IsNullOrWhiteSpace(release.tag_name))
                throw new InvalidDataException("GitHub latest release metadata is invalid");

            string latestVersion = NormalizeVersion(release.tag_name);
            string installedVersion = NormalizeVersion(currentVersion);
            GitHubAsset installer = FindAsset(release.assets, InstallerAssetName);
            GitHubAsset checksums = FindAsset(release.assets, ChecksumAssetName);
            if (installer == null || checksums == null)
                throw new InvalidDataException("Latest release is missing the installer or checksum manifest");
            ValidateAssetUrl(installer.browser_download_url);
            ValidateAssetUrl(checksums.browser_download_url);

            return new SecureUpdateInfo
            {
                Version = latestVersion,
                InstallerUrl = installer.browser_download_url,
                ChecksumUrl = checksums.browser_download_url,
                IsNewer = ParseVersion(latestVersion).CompareTo(ParseVersion(installedVersion)) > 0
            };
        }
        catch (WebException)
        {
            return GetLatestFromReleaseRedirect(currentVersion);
        }
        catch (ArgumentException)
        {
            return GetLatestFromReleaseRedirect(currentVersion);
        }
        catch (InvalidOperationException)
        {
            return GetLatestFromReleaseRedirect(currentVersion);
        }
    }

    private static SecureUpdateInfo GetLatestFromReleaseRedirect(string currentVersion)
    {
        const string latestPage = "https://github.com/richlearntodo-debug/vibe-flow/releases/latest";
        Uri resolved = ResolveRedirect(latestPage);
        string marker = "/releases/tag/";
        int markerIndex = resolved.AbsolutePath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0) throw new InvalidDataException("GitHub latest release redirect did not contain a tag");
        string tag = Uri.UnescapeDataString(resolved.AbsolutePath.Substring(markerIndex + marker.Length));
        string latestVersion = NormalizeVersion(tag);
        string installerUrl = latestPage + "/download/" + InstallerAssetName;
        string checksumUrl = latestPage + "/download/" + ChecksumAssetName;
        EnsureAssetAvailable(installerUrl);
        EnsureAssetAvailable(checksumUrl);
        return new SecureUpdateInfo
        {
            Version = latestVersion,
            InstallerUrl = installerUrl,
            ChecksumUrl = checksumUrl,
            IsNewer = ParseVersion(latestVersion).CompareTo(ParseVersion(currentVersion)) > 0
        };
    }

    private static Uri ResolveRedirect(string value)
    {
        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(value);
        request.Method = "HEAD";
        request.AllowAutoRedirect = true;
        request.Timeout = 20000;
        request.ReadWriteTimeout = 20000;
        request.UserAgent = "Vibe-Flow-Remote-Updater/1.0";
        using (HttpWebResponse response = (HttpWebResponse)request.GetResponse()) return response.ResponseUri;
    }

    private static void EnsureAssetAvailable(string value)
    {
        Uri final = ResolveRedirect(value);
        if (final == null || final.Scheme != Uri.UriSchemeHttps)
            throw new InvalidDataException("Release asset did not resolve over HTTPS");
    }

    public static string DownloadAndVerify(SecureUpdateInfo update, string updatesRoot)
    {
        if (update == null || string.IsNullOrWhiteSpace(update.Version))
            throw new ArgumentException("Update metadata is missing");
        ValidateAssetUrl(update.InstallerUrl);
        ValidateAssetUrl(update.ChecksumUrl);
        Directory.CreateDirectory(updatesRoot);
        string versionDirectory = Path.Combine(updatesRoot, "v" + NormalizeVersion(update.Version));
        Directory.CreateDirectory(versionDirectory);
        string installerPath = Path.Combine(versionDirectory, InstallerAssetName);
        string checksumPath = Path.Combine(versionDirectory, ChecksumAssetName);
        string installerDownload = installerPath + ".download";
        string checksumDownload = checksumPath + ".download";

        TryDelete(installerDownload);
        TryDelete(checksumDownload);
        try
        {
            using (TimeoutWebClient client = CreateClient(120000))
            {
                client.DownloadFile(update.ChecksumUrl, checksumDownload);
                client.DownloadFile(update.InstallerUrl, installerDownload);
            }
            var checksumFile = new FileInfo(checksumDownload);
            var installerFile = new FileInfo(installerDownload);
            if (checksumFile.Length <= 0 || checksumFile.Length > 1024 * 1024)
                throw new InvalidDataException("Checksum manifest size is invalid");
            if (installerFile.Length < 250000)
                throw new InvalidDataException("Downloaded installer is unexpectedly small");

            string expected = ReadExpectedSha256(File.ReadAllText(checksumDownload, Encoding.UTF8), InstallerAssetName);
            string actual = ComputeSha256(installerDownload);
            if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Installer SHA-256 does not match the release manifest");
            update.ExpectedSha256 = expected.ToUpperInvariant();

            TryDelete(installerPath);
            TryDelete(checksumPath);
            File.Move(installerDownload, installerPath);
            File.Move(checksumDownload, checksumPath);
            if (!ComputeSha256(installerPath).Equals(expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Installer changed after verification");
            return installerPath;
        }
        finally
        {
            TryDelete(installerDownload);
            TryDelete(checksumDownload);
        }
    }

    internal static string ReadExpectedSha256(string manifest, string fileName)
    {
        foreach (string rawLine in (manifest ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string[] fields = rawLine.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 2) continue;
            string candidate = fields[fields.Length - 1].TrimStart('*');
            if (!candidate.Equals(fileName, StringComparison.OrdinalIgnoreCase)) continue;
            string hash = fields[0].Trim();
            if (hash.Length != 64 || !IsHexString(hash))
                throw new InvalidDataException("Installer checksum is malformed");
            return hash;
        }
        throw new InvalidDataException("Installer checksum is missing from the manifest");
    }

    internal static Version ParseVersion(string value)
    {
        string normalized = NormalizeVersion(value);
        string[] parts = normalized.Split('.');
        int[] numbers = new int[4];
        if (parts.Length == 0 || parts.Length > 4) throw new InvalidDataException("Release version is invalid");
        for (int i = 0; i < parts.Length; i++)
            if (!int.TryParse(parts[i], out numbers[i]) || numbers[i] < 0)
                throw new InvalidDataException("Release version is invalid");
        return new Version(numbers[0], numbers[1], numbers[2], numbers[3]);
    }

    private static string NormalizeVersion(string value)
    {
        string normalized = (value ?? "").Trim();
        if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase)) normalized = normalized.Substring(1);
        int suffix = normalized.IndexOf('-');
        if (suffix >= 0) normalized = normalized.Substring(0, suffix);
        if (string.IsNullOrWhiteSpace(normalized)) throw new InvalidDataException("Release version is missing");
        return normalized;
    }

    private static GitHubAsset FindAsset(GitHubAsset[] assets, string name)
    {
        if (assets == null) return null;
        foreach (GitHubAsset asset in assets)
            if (asset != null && string.Equals(asset.name, name, StringComparison.OrdinalIgnoreCase)) return asset;
        return null;
    }

    private static void ValidateAssetUrl(string value)
    {
        Uri uri;
        if (!Uri.TryCreate(value, UriKind.Absolute, out uri) || uri.Scheme != Uri.UriSchemeHttps ||
            !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Release asset URL is not an official GitHub HTTPS URL");
    }

    private static TimeoutWebClient CreateClient(int timeoutMs)
    {
        var client = new TimeoutWebClient(timeoutMs);
        client.Encoding = Encoding.UTF8;
        client.Headers[HttpRequestHeader.UserAgent] = "Vibe-Flow-Remote-Updater/1.0";
        client.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
        return client;
    }

    private static string ComputeSha256(string path)
    {
        using (FileStream stream = File.OpenRead(path))
        using (SHA256 algorithm = SHA256.Create())
            return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "");
    }

    private static bool IsHexCharacter(char value)
    {
        return (value >= '0' && value <= '9') || (value >= 'a' && value <= 'f') || (value >= 'A' && value <= 'F');
    }

    private static bool IsHexString(string value)
    {
        foreach (char character in value) if (!IsHexCharacter(character)) return false;
        return true;
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { }
    }

    private sealed class GitHubRelease
    {
        public string tag_name { get; set; }
        public bool draft { get; set; }
        public bool prerelease { get; set; }
        public GitHubAsset[] assets { get; set; }
    }

    private sealed class GitHubAsset
    {
        public string name { get; set; }
        public string browser_download_url { get; set; }
    }

    private sealed class TimeoutWebClient : WebClient
    {
        private readonly int timeoutMs;
        public TimeoutWebClient(int timeout) { timeoutMs = timeout; }

        protected override WebRequest GetWebRequest(Uri address)
        {
            WebRequest request = base.GetWebRequest(address);
            request.Timeout = timeoutMs;
            HttpWebRequest http = request as HttpWebRequest;
            if (http != null) http.ReadWriteTimeout = timeoutMs;
            return request;
        }
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

internal sealed class WindowsAudioDuckingLease : IDisposable
{
    private const string RegistryPath = "Software\\Microsoft\\Multimedia\\Audio";
    private const string PreferenceName = "UserDuckingPreference";
    private const int DoNothingPreference = 3;

    private readonly object sync = new object();
    private readonly string markerPath;
    private readonly Action<string> log;
    private System.Threading.Timer restoreTimer;
    private bool active;
    private bool changedPreference;
    private bool originalExists;
    private int originalValue;
    private int leaseVersion;
    private bool disposed;

    public WindowsAudioDuckingLease(string recoveryMarkerPath, Action<string> logger)
    {
        markerPath = recoveryMarkerPath;
        log = logger ?? delegate { };
        RecoverStaleLease();
    }

    public bool Acquire(string reason)
    {
        lock (sync)
        {
            if (disposed) return false;
            CancelRestoreTimer();
            leaseVersion++;

            if (active)
            {
                int current;
                bool exists;
                if (TryReadPreference(out exists, out current) && exists && current == DoNothingPreference)
                {
                    log("WINDOWS AUDIO DUCKING PROTECTED retained=True reason=" + Safe(reason));
                    return true;
                }

                log("WINDOWS AUDIO DUCKING PROTECTION LOST reason=user_or_system_changed_preference");
                DeleteMarker();
                active = false;
                changedPreference = false;
                return false;
            }

            if (!TryReadPreference(out originalExists, out originalValue))
            {
                log("WINDOWS AUDIO DUCKING PROTECTION FAILED phase=read_preference reason=" + Safe(reason));
                return false;
            }

            if (originalExists && originalValue == DoNothingPreference)
            {
                active = true;
                changedPreference = false;
                log("WINDOWS AUDIO DUCKING PROTECTED changed=False original=do_nothing reason=" + Safe(reason));
                return true;
            }

            if (!WriteMarker())
            {
                log("WINDOWS AUDIO DUCKING PROTECTION FAILED phase=write_recovery_marker reason=" + Safe(reason));
                return false;
            }

            if (!TryWritePreference(DoNothingPreference))
            {
                DeleteMarker();
                log("WINDOWS AUDIO DUCKING PROTECTION FAILED phase=write_preference reason=" + Safe(reason));
                return false;
            }

            bool verifyExists;
            int verifyValue;
            if (!TryReadPreference(out verifyExists, out verifyValue) || !verifyExists || verifyValue != DoNothingPreference)
            {
                RestoreOriginalPreference("acquire_verify_failed");
                log("WINDOWS AUDIO DUCKING PROTECTION FAILED phase=verify_preference reason=" + Safe(reason));
                return false;
            }

            active = true;
            changedPreference = true;
            log("WINDOWS AUDIO DUCKING PROTECTED changed=True original=" +
                (originalExists ? originalValue.ToString() : "missing") + " reason=" + Safe(reason));
            return true;
        }
    }

    public void ReleaseAfter(int delayMs, string reason)
    {
        lock (sync)
        {
            if (!active || disposed) return;
            CancelRestoreTimer();
            int version = ++leaseVersion;
            restoreTimer = new System.Threading.Timer(delegate { RestoreIfCurrent(version, reason); }, null,
                Math.Max(0, delayMs), Timeout.Infinite);
            log("WINDOWS AUDIO DUCKING RESTORE SCHEDULED delay_ms=" + Math.Max(0, delayMs) +
                " reason=" + Safe(reason));
        }
    }

    public void ReleaseNow(string reason)
    {
        lock (sync)
        {
            leaseVersion++;
            CancelRestoreTimer();
            RestoreOriginalPreference(reason);
        }
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed) return;
            leaseVersion++;
            CancelRestoreTimer();
            RestoreOriginalPreference("dispose");
            disposed = true;
        }
    }

    private void RestoreIfCurrent(int version, string reason)
    {
        lock (sync)
        {
            if (disposed || version != leaseVersion) return;
            CancelRestoreTimer();
            RestoreOriginalPreference(reason);
        }
    }

    private void RestoreOriginalPreference(string reason)
    {
        if (!active)
        {
            DeleteMarker();
            return;
        }

        if (!changedPreference)
        {
            active = false;
            log("WINDOWS AUDIO DUCKING RESTORED changed=False reason=" + Safe(reason));
            return;
        }

        bool currentExists;
        int currentValue;
        bool read = TryReadPreference(out currentExists, out currentValue);
        if (!read || !currentExists || currentValue != DoNothingPreference)
        {
            log("WINDOWS AUDIO DUCKING RESTORE SKIPPED reason=user_or_system_changed_preference current=" +
                (!read ? "unreadable" : currentExists ? currentValue.ToString() : "missing"));
        }
        else if (RestorePreference(originalExists, originalValue))
        {
            log("WINDOWS AUDIO DUCKING PREFERENCE RESTORED original=" +
                (originalExists ? originalValue.ToString() : "missing") + " reason=" + Safe(reason));
        }
        else
        {
            log("WINDOWS AUDIO DUCKING RESTORE FAILED reason=" + Safe(reason));
            return;
        }

        active = false;
        changedPreference = false;
        DeleteMarker();
    }

    private void RecoverStaleLease()
    {
        lock (sync)
        {
            if (string.IsNullOrWhiteSpace(markerPath) || !File.Exists(markerPath)) return;
            try
            {
                DuckingMarker marker = new JavaScriptSerializer().Deserialize<DuckingMarker>(File.ReadAllText(markerPath));
                if (marker == null) throw new InvalidDataException("empty marker");

                bool currentExists;
                int currentValue;
                if (!TryReadPreference(out currentExists, out currentValue))
                {
                    log("WINDOWS AUDIO DUCKING STARTUP RECOVERY FAILED phase=read_preference");
                    return;
                }

                if (currentExists && currentValue == DoNothingPreference)
                {
                    bool restored = RestorePreference(marker.OriginalExists, marker.OriginalValue);
                    log("WINDOWS AUDIO DUCKING STARTUP RECOVERY restored=" + restored + " original=" +
                        (marker.OriginalExists ? marker.OriginalValue.ToString() : "missing"));
                    if (!restored) return;
                }
                else
                {
                    log("WINDOWS AUDIO DUCKING STARTUP RECOVERY skipped=user_or_system_changed_preference current=" +
                        (currentExists ? currentValue.ToString() : "missing"));
                }
                DeleteMarker();
            }
            catch (Exception ex)
            {
                log("WINDOWS AUDIO DUCKING STARTUP RECOVERY FAILED phase=marker error=" + Safe(ex.Message));
            }
        }
    }

    private bool WriteMarker()
    {
        try
        {
            string directory = Path.GetDirectoryName(markerPath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            string json = new JavaScriptSerializer().Serialize(new DuckingMarker
            {
                OriginalExists = originalExists,
                OriginalValue = originalValue
            });
            using (var stream = new FileStream(markerPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(true);
            }
            return true;
        }
        catch (Exception ex)
        {
            log("WINDOWS AUDIO DUCKING MARKER WRITE FAILED error=" + Safe(ex.Message));
            return false;
        }
    }

    private bool RestorePreference(bool exists, int value)
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
            {
                if (key == null) return false;
                if (exists) key.SetValue(PreferenceName, value, RegistryValueKind.DWord);
                else key.DeleteValue(PreferenceName, false);
            }
            BroadcastPreferenceChange();
            return true;
        }
        catch (Exception ex)
        {
            log("WINDOWS AUDIO DUCKING PREFERENCE RESTORE FAILED error=" + Safe(ex.Message));
            return false;
        }
    }

    private bool TryReadPreference(out bool exists, out int value)
    {
        exists = false;
        value = 0;
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath, false))
            {
                if (key == null) return true;
                object raw = key.GetValue(PreferenceName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                if (raw == null) return true;
                value = Convert.ToInt32(raw, CultureInfo.InvariantCulture);
                exists = true;
                return true;
            }
        }
        catch (Exception ex)
        {
            log("WINDOWS AUDIO DUCKING PREFERENCE READ FAILED error=" + Safe(ex.Message));
            return false;
        }
    }

    private bool TryWritePreference(int value)
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
            {
                if (key == null) return false;
                key.SetValue(PreferenceName, value, RegistryValueKind.DWord);
            }
            BroadcastPreferenceChange();
            return true;
        }
        catch (Exception ex)
        {
            log("WINDOWS AUDIO DUCKING PREFERENCE WRITE FAILED error=" + Safe(ex.Message));
            return false;
        }
    }

    private void CancelRestoreTimer()
    {
        if (restoreTimer == null) return;
        restoreTimer.Dispose();
        restoreTimer = null;
    }

    private void DeleteMarker()
    {
        try { if (!string.IsNullOrWhiteSpace(markerPath) && File.Exists(markerPath)) File.Delete(markerPath); }
        catch (Exception ex) { log("WINDOWS AUDIO DUCKING MARKER DELETE FAILED error=" + Safe(ex.Message)); }
    }

    private static string Safe(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "none" : value.Replace('\r', '_').Replace('\n', '_').Replace(' ', '_');
    }

    private sealed class DuckingMarker
    {
        public bool OriginalExists { get; set; }
        public int OriginalValue { get; set; }
    }

    private void BroadcastPreferenceChange()
    {
        try
        {
            UIntPtr result;
            IntPtr sent = SendMessageTimeout(new IntPtr(0xFFFF), 0x001A, UIntPtr.Zero, RegistryPath,
                0x0002, 250, out result);
            log("WINDOWS AUDIO DUCKING PREFERENCE BROADCAST sent=" + (sent != IntPtr.Zero));
        }
        catch (Exception ex)
        {
            log("WINDOWS AUDIO DUCKING PREFERENCE BROADCAST FAILED error=" + Safe(ex.Message));
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(IntPtr window, uint message, UIntPtr wParam,
        string lParam, uint flags, uint timeout, out UIntPtr result);
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
