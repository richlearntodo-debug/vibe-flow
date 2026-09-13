using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("Vibe Link RC003 input bridge")]
[assembly: System.Reflection.AssemblyProduct("Vibe Flow Remote")]
[assembly: System.Reflection.AssemblyCompany("Vibe Link Contributors")]
[assembly: System.Reflection.AssemblyVersion("2.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("2.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersion("2.0.0-candidate")]

internal static class VoxDeckInputBridge
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;
    private const int WM_INPUT = 0x00FF;
    private const int WM_INPUT_DEVICE_CHANGE = 0x00FE;
    private const int WM_QUIT = 0x0012;
    private const int WM_APP_REINSTALL_HOOK = 0x8001;
    private const int CUSTOM_TEST_MAX_AGE_SECONDS = 8;
    private const int CUSTOM_TEST_MAX_FUTURE_SKEW_SECONDS = 5;
    private const uint PM_NOREMOVE = 0x0000;
    private const int LLKHF_INJECTED = 0x10;
    private const uint RID_INPUT = 0x10000003;
    private const uint RIDI_PREPARSEDDATA = 0x20000005;
    private const uint RIDI_DEVICENAME = 0x20000007;
    private const uint RIM_TYPEKEYBOARD = 1;
    private const uint RIM_TYPEHID = 2;
    private const int RIDEV_INPUTSINK = 0x00000100;
    private const int RIDEV_DEVNOTIFY = 0x00002000;
    private const uint MAPVK_VSC_TO_VK_EX = 3;
    private const ushort HID_USAGE_PAGE_CONSUMER = 0x0C;
    private const ushort HID_USAGE_PAGE_KEYBOARD = 0x07;
    private const ushort HID_USAGE_BACK = 0xF1;
    private const ushort HID_USAGE_POWER = 0x66;
    private const ushort HID_USAGE_VOLUME_MUTE = 0x7F;
    private const ushort HID_USAGE_VOLUME_INCREMENT = 0x80;
    private const ushort HID_USAGE_VOLUME_DECREMENT = 0x81;
    private const ushort HID_USAGE_CONSUMER_VOLUME_INCREMENT = 0xE9;
    private const ushort HID_USAGE_CONSUMER_VOLUME_DECREMENT = 0xEA;
    private const ushort HID_USAGE_CONSUMER_MUTE = 0xE2;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_UNICODE = 0x0004;
    private const uint KEYEVENTF_SCANCODE = 0x0008;
    private const int CUSTOM_CAPTURE_REQUEST_TIMEOUT_SECONDS = 15;
    private const int DEFAULT_LONG_PRESS_MS = 650;
    private const int HOLD_REPEAT_INITIAL_DELAY_MS = 420;
    private const int HOLD_REPEAT_INTERVAL_MS = 80;
    private const int VOICE_RESTART_GUARD_MS = 500;
    private const int SMART_PROFILE_POLL_MS = 250;
    private const int SMART_PROFILE_DEBOUNCE_MS = 350;
    // RC003 voice F5 isolation (no signed device filter). Measured on this
    // machine: the hook edge precedes Raw Input and hook suppression cancels
    // the Raw Input packet, so per-event device attribution is impossible in
    // user mode. V1.5 treated F5 as the voice key and suppressed it at the
    // hook. V2 keeps that suppression but only while the RC003 HID device is
    // physically connected; ordinary-keyboard F5 passes through otherwise.
    private const int RC003_PRESENT_GRACE_MS = 5000;
    private const int RC003_VOICE_STUCK_RELEASE_MS = 900;
    // The remote/ATVV chain ends a session at its own device-controlled boundary (about
    // 60 s), so a genuine hold cannot be reported for much longer than that. The stuck
    // bound sits well past the device's own limit on purpose: it can only ever fire
    // after the audio has already ended, so it can never cut a real recording short.
    private const int VOICE_HOLD_STUCK_BOUND_MS = 90000;
    // A stuck hold is released once and then latched: the repeats keep arriving, and
    // until the key has been quiet for this long there is no evidence it was ever
    // released, so none of them may be treated as a new press.
    private const int VOICE_HOLD_LATCH_CLEAR_MS = 1500;
    // A key held down repeats at the keyboard rate (~30/s). Logging every suppressed
    // edge filled a 2 MB log in about ten minutes and rotated away the real history,
    // so repeats are aggregated and only their count is reported.
    private const int ISOLATION_REPEAT_LOG_INTERVAL_MS = 2000;

    private static readonly object stateLock = new object();
    private static readonly object logLock = new object();
    private static readonly Dictionary<string, bool> sourceDown = new Dictionary<string, bool>();
    private static readonly Dictionary<string, bool> shortcutDown = new Dictionary<string, bool>();
    // Preserve the exact down mapping across config reloads so injected keys
    // can always be released with the same chord that pressed them.
    private static readonly Dictionary<string, ShortcutMapping> activeShortcutMappings =
        new Dictionary<string, ShortcutMapping>();
    private static readonly Dictionary<string, ShortLongGestureState> gestureStates = new Dictionary<string, ShortLongGestureState>();
    private static readonly Dictionary<string, System.Threading.Timer> gestureTimers = new Dictionary<string, System.Threading.Timer>();
    private static readonly Dictionary<string, System.Threading.Timer> holdRepeatTimers = new Dictionary<string, System.Threading.Timer>();
    private static readonly Dictionary<string, int> holdRepeatGenerations = new Dictionary<string, int>();
    // Gesture layering bookkeeping per physical key: when the last short tap ended (which opens
    // the double-tap window) and when the current press started (so the hold time is measured
    // rather than assumed, and a release just under the timer threshold still classifies as long).
    private static readonly Dictionary<string, int> gestureLastTapMs = new Dictionary<string, int>();
    private static readonly Dictionary<string, int> gesturePressStartMs = new Dictionary<string, int>();

    private static IntPtr hookHandle = IntPtr.Zero;
    private static LowLevelKeyboardProc hookProc = HookCallback;
    private static Thread keyboardHookThread;
    private static uint keyboardHookThreadId;
    private static readonly ManualResetEvent keyboardHookReady = new ManualResetEvent(false);
    private static readonly object hookThreadLock = new object();
    private static string pendingHookReinstallReason = "";
    private static readonly RawKeyboardEdgeTracker rawKeyboardEdgeTracker =
        new RawKeyboardEdgeTracker();
    private static Rc003FilterClient rc003FilterClient;
    private static Mutex singleInstance;
    private static EventWaitHandle stopEvent;
    private static EventWaitHandle voiceKeyPressedEvent;
    private static EventWaitHandle voiceKeyHeldEvent;
    private static EventWaitHandle voiceKeyReleasedEvent;
    private static EventWaitHandle voiceWakeRequestEvent;
    private static EventWaitHandle reloadConfigEvent;
    private static RegisteredWaitHandle reloadConfigRegistration;
    private static int voiceKeyHeldState;
    private static int voiceTransitionPending;
    private static int browserRemoteTapActive;
    private static bool selfTestMode;
    private static readonly object voiceTransitionLock = new object();
    private static DateTime lastVoiceReleaseUtc = DateTime.MinValue;
    private static DateTime lastVoiceActivityUtc = DateTime.MinValue;
    // When the microphone was last reported in its translated F5 form, and how long that memory stays useful.
    private static DateTime lastTranslatedVoiceFormUtc = DateTime.MinValue;
    private static readonly TimeSpan SharedFormVoiceWindow = TimeSpan.FromMinutes(10);
    private static DateTime lastDuplicateDownLogUtc = DateTime.MinValue;
    private static DateTime rc003DevicePresentUtc = DateTime.MinValue;
    // How long a sign of the RC003 keeps it "present" for the hook's scoped record-key suppression. The Raw
    // Input health probe refreshes that sign and must run inside this window, or the first key press of a
    // session is not suppressed; Rc003PresenceWindowMs is what ties the two together.
    private const int Rc003PresenceWindowMs = 5000;
    private static long suppressedHookEdgeCount;
    // Stuck-hold bookkeeping. voiceHoldStartedTicks is the start of the CURRENT hold,
    // which is what tells a genuinely held key from one whose release edge was lost:
    // "time since last activity" cannot, because a repeating key always looks active.
    private static long voiceHoldStartedTicks;
    private static int voiceHoldStaleLatched;
    private static int voiceHoldStaleReleaseCount;
    private static int voiceHoldRepeatsSuppressed;
    private static DateTime lastIsolationLogUtc = DateTime.MinValue;
    private static int isolationRepeatCount;
    private static readonly BlockingCollection<MappingEvent> mappingQueue = new BlockingCollection<MappingEvent>();
    private static Thread mappingWorker;
    private static BridgeConfig config = BridgeConfig.Default();
    private static bool useScanCode = true;
    private static DateTime configLastWriteUtc = DateTime.MinValue;
    private static DateTime configLoadedAtUtc = DateTime.MinValue;
    private static string configLoadError = "";
    private static readonly object taskSwitcherLock = new object();
    private const int TASK_SWITCHER_TIMEOUT_MS = 30000;
    private static readonly HashSet<int> taskSwitcherKeysDown = new HashSet<int>();
    private static bool taskSwitcherActive;
    private static System.Threading.Timer taskSwitcherTimer;
    private static System.Threading.Timer bridgeHealthTimer;
    private static System.Threading.Timer smartProfileTimer;
    private static System.Threading.Timer voiceStuckWatchdogTimer;
    private static bool rawInputRegistered;
    private static int rawInputDeviceMisses;
    private static DateTime lastRawInputDeviceChangeLogUtc = DateTime.MinValue;
    private static DateTime bridgeStartedUtc = DateTime.MinValue;
    private static bool startupRawInputRebindCompleted;
    private static string knownRc003DeviceFingerprint = "";
    private static readonly HashSet<int> consumerUsagesDown = new HashSet<int>();
    private static readonly HashSet<int> keyboardUsagesDown = new HashSet<int>();
    private static readonly HashSet<string> ignoredRawCandidateFingerprints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static readonly object customCaptureLock = new object();
    private static DateTime customCaptureRequestLastWriteUtc = DateTime.MinValue;
    private static CustomCaptureRequest customCaptureRequest;
    private static bool customCaptureConsumed;
    private static System.Threading.Timer customTestTimer;
    private static DateTime customTestLastWriteUtc = DateTime.MinValue;
    private static DateTime lastRemoteInputUtc = DateTime.MinValue;
    private static string lastRemoteInputKind = "";
    private static DateTime lastHookInputUtc = DateTime.MinValue;
    private static int lastHookInputVk;
    private static int lastHookInputScan;
    private static long rawRemoteEdgeCount;
    private static long rawActionEdgeCount;
    private static long hookCandidatePassthroughCount;
    private static long filterActionEdgeCount;
    private static DateTime lastRawActionUtc = DateTime.MinValue;
    private static string lastRawAction = "";
    private static string lastActionSource = "";
    private static readonly object actionReceiptLock = new object();
    private static long actionReceiptSequence;
    private static ActionExecutionReceipt lastExecutionReceipt;
    private static string configuredShortcutProfileId = "general";
    private static string configuredShortcutProfileName = "通用导航";
    private static string smartProfileCandidateId = "";
    private static DateTime smartProfileCandidateSinceUtc = DateTime.MinValue;
    private static string smartProfileForegroundProcess = "";
    private static string smartProfileMatchState = "manual";
    private static int smartProfileEvaluationRunning;

    private static readonly string Root = AppDomain.CurrentDomain.BaseDirectory;
    private static readonly string ConfigPath = Path.Combine(Root, "voxdeck-shortcuts.json");
    private static readonly string LogPath = Path.Combine(Root, "input-bridge-log.txt");
    private static readonly string HealthPath = Path.Combine(Root, "input-bridge-health.json");
    private static readonly string CustomCaptureRequestPath = Path.Combine(Root, "custom-button-capture-request.json");
    private static readonly string CustomCaptureResultPath = Path.Combine(Root, "custom-button-capture-result.json");
    private static readonly string CustomTestPath = Path.Combine(Root, "custom-button-test.json");
    private static readonly string CustomTestResultPath = Path.Combine(Root, "custom-button-test-result.json");

    [STAThread]
    private static void Main(string[] args)
    {
        if (Array.Exists(args, delegate(string arg) { return arg.Equals("--self-test", StringComparison.OrdinalIgnoreCase); }))
        {
            Environment.ExitCode = RunSelfTests();
            return;
        }
        bool background = Array.Exists(args, delegate(string arg) { return arg.Equals("--background", StringComparison.OrdinalIgnoreCase); });
        bool createdNew;
        singleInstance = new Mutex(true, "VoxDeckInputBridge.SingleInstance", out createdNew);
        if (!createdNew)
        {
            if (!background) MessageBox.Show("VoxDeck Input Bridge is already running.", "VoxDeck Input Bridge", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        bridgeStartedUtc = DateTime.UtcNow;
        startupRawInputRebindCompleted = false;

        LoadConfig();
        using (var form = new BridgeForm(background))
        {
            mappingWorker = new Thread(ProcessMappingQueue);
            mappingWorker.IsBackground = true;
            mappingWorker.Name = "Vibe Mic shortcut queue";
            mappingWorker.Start();
            stopEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\VibeMicStopKeyboardBridge");
            // Create the press event before the capture process is ready. The
            // capture worker opens this same named event later, so a quick
            // startup/reconnect press is queued instead of being discarded.
            voiceKeyPressedEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\VibeMicVoiceKeyPressed");
            voiceKeyHeldEvent = new EventWaitHandle(false, EventResetMode.ManualReset, "Local\\VibeMicVoiceKeyHeld");
            voiceKeyReleasedEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\VibeMicVoiceKeyReleased");
            voiceWakeRequestEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\VibeMicVoiceWakeRequested");
            reloadConfigEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\VibeMicReloadKeyboardConfig");
            reloadConfigRegistration = ThreadPool.RegisterWaitForSingleObject(reloadConfigEvent,
                delegate
                {
                    ReloadConfig(true, "reload_event");
                    WriteHealth("running");
                }, null, Timeout.Infinite, false);
            voiceKeyHeldEvent.Reset();
            StartRc003FilterClient();
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    stopEvent.WaitOne();
                    if (!form.IsDisposed) form.BeginInvoke(new Action(form.Close));
                }
                catch { }
            });
            Log("Starting VoxDeckInputBridge");
            Log("INPUT size=" + Marshal.SizeOf(typeof(INPUT)));
            StartKeyboardHookThread();
            WriteHealth("starting");
            bridgeHealthTimer = new System.Threading.Timer(delegate { WriteHealth("running"); }, null, 0, 2000);
            smartProfileTimer = new System.Threading.Timer(delegate { EvaluateSmartProfile(false); }, null, 0,
                SMART_PROFILE_POLL_MS);
            voiceStuckWatchdogTimer = new System.Threading.Timer(delegate { ReleaseStuckVoiceHoldIfIdle(); },
                null, 0, 500);
            customTestTimer = new System.Threading.Timer(delegate { ProcessCustomButtonTest(); }, null, 250, 250);
            Application.Run(form);
            StopRc003FilterClient();
            StopKeyboardHookThread();
            rawKeyboardEdgeTracker.Reset();
            mappingQueue.CompleteAdding();
            if (mappingWorker != null) mappingWorker.Join(1500);
            if (bridgeHealthTimer != null) { bridgeHealthTimer.Dispose(); bridgeHealthTimer = null; }
            if (smartProfileTimer != null) { smartProfileTimer.Dispose(); smartProfileTimer = null; }
            if (voiceStuckWatchdogTimer != null)
            {
                voiceStuckWatchdogTimer.Dispose();
                voiceStuckWatchdogTimer = null;
            }
            if (customTestTimer != null) { customTestTimer.Dispose(); customTestTimer = null; }
            if (reloadConfigRegistration != null) { reloadConfigRegistration.Unregister(null); reloadConfigRegistration = null; }
            WriteHealth("stopped");
            SetVoiceKeyHeld(false);
            ReleaseAllShortcuts();
            try { stopEvent.Set(); } catch { }
            stopEvent.Dispose();
            voiceKeyPressedEvent.Dispose();
            voiceKeyHeldEvent.Dispose();
            voiceKeyReleasedEvent.Dispose();
            voiceWakeRequestEvent.Dispose();
            reloadConfigEvent.Dispose();
        }
    }

    private static IntPtr SetHook(LowLevelKeyboardProc proc)
    {
        using (Process currentProcess = Process.GetCurrentProcess())
        using (ProcessModule currentModule = currentProcess.MainModule)
        {
            return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(currentModule.ModuleName), 0);
        }
    }

    private static void StartKeyboardHookThread()
    {
        keyboardHookReady.Reset();
        keyboardHookThread = new Thread(KeyboardHookThreadMain);
        keyboardHookThread.IsBackground = true;
        keyboardHookThread.Name = "Vibe Link device-aware keyboard hook";
        keyboardHookThread.Start();
        if (!keyboardHookReady.WaitOne(2000) || hookHandle == IntPtr.Zero)
            Log("Keyboard hook thread failed to become ready");
    }

    private static void KeyboardHookThreadMain()
    {
        keyboardHookThreadId = GetCurrentThreadId();
        MSG bootstrapMessage;
        PeekMessage(out bootstrapMessage, IntPtr.Zero, 0, 0, PM_NOREMOVE);
        hookHandle = SetHook(hookProc);
        int error = hookHandle == IntPtr.Zero ? Marshal.GetLastWin32Error() : 0;
        Log("SetWindowsHookEx result=" + hookHandle + " thread=" + keyboardHookThreadId + " error=" + error);
        keyboardHookReady.Set();

        MSG message;
        while (GetMessage(out message, IntPtr.Zero, 0, 0) > 0)
        {
            if (message.message == WM_APP_REINSTALL_HOOK)
            {
                string reason;
                lock (hookThreadLock)
                {
                    reason = pendingHookReinstallReason;
                    pendingHookReinstallReason = "";
                }
                InstallKeyboardHookOnCurrentThread(reason);
                continue;
            }
            TranslateMessage(ref message);
            DispatchMessage(ref message);
        }

        IntPtr installed = hookHandle;
        hookHandle = IntPtr.Zero;
        if (installed != IntPtr.Zero) UnhookWindowsHookEx(installed);
        keyboardHookThreadId = 0;
        Log("Hook uninstalled");
    }

    private static void StopKeyboardHookThread()
    {
        uint threadId = keyboardHookThreadId;
        if (threadId != 0) PostThreadMessage(threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        if (keyboardHookThread != null && keyboardHookThread.IsAlive) keyboardHookThread.Join(1500);
        keyboardHookThread = null;
    }

    private static void InstallKeyboardHookOnCurrentThread(string reason)
    {
        try
        {
            IntPtr previous = hookHandle;
            hookHandle = IntPtr.Zero;
            if (previous != IntPtr.Zero) UnhookWindowsHookEx(previous);
            hookHandle = SetHook(hookProc);
            Log("Keyboard hook reinstalled result=" + hookHandle + " reason=" + (reason ?? "unknown") +
                " error=" + (hookHandle == IntPtr.Zero ? Marshal.GetLastWin32Error() : 0));
        }
        catch (Exception ex)
        {
            Log("Keyboard hook reinstall failed reason=" + (reason ?? "unknown") + " error=" + ex.Message);
        }
    }

    private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            KBDLLHOOKSTRUCT data = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
            bool injected = (data.flags & LLKHF_INJECTED) == LLKHF_INJECTED;
            if (!injected)
            {
                int message = wParam.ToInt32();
                bool isDown = message == WM_KEYDOWN || message == WM_SYSKEYDOWN;
                bool isUp = message == WM_KEYUP || message == WM_SYSKEYUP;
                if (isDown || isUp)
                {
                    // Keep hook-only diagnostics for troubleshooting. A low-level
                    // hook has no device identity, so it must not refresh the
                    // user-facing remote activity timestamp.
                    if (data.vkCode == 0x74 || data.vkCode == 0xF5)
                    {
                        lastHookInputUtc = DateTime.UtcNow;
                        lastHookInputVk = data.vkCode;
                        lastHookInputScan = data.scanCode;
                    }
                    ReloadConfigIfChanged();
                    ShortcutMapping mapping = FindMapping(data.vkCode, data.scanCode);
                    // RC003 has consistently exposed its microphone button as
                    // F5, but some reconnects briefly omit or alter the scan
                    // code. Keep the persisted voice mapping authoritative and
                    // do not let a malformed hot-reload disable the key.
                    if (mapping == null && (data.vkCode == 0x74 || data.vkCode == 0xF5))
                    {
                        ShortcutMapping fallbackVoice = FindVoiceMapping();
                        if (fallbackVoice != null && fallbackVoice.enabled)
                        {
                            mapping = fallbackVoice;
                            Log("Voice mapping VK fallback vk=0x" + data.vkCode.ToString("X2") +
                                " scan=0x" + data.scanCode.ToString("X2"));
                        }
                    }
                    bool isVoiceMapping = mapping != null &&
                        (mapping.name ?? "").Equals("voice", StringComparison.OrdinalIgnoreCase);
                    bool taskSwitcherNavigation = IsTaskSwitcherNavigationCandidate(data.vkCode);
                    if (ShouldBypassHookForRc003Filter(IsRc003FilterHealthy(),
                        mapping != null, taskSwitcherNavigation))
                    {
                        // A healthy per-device filter has already removed every
                        // configured RC003 edge before it can reach this hook.
                        // Any matching edge seen here therefore belongs to an
                        // ordinary keyboard and must pass through untouched.
                        return CallNextHookEx(hookHandle, nCode, wParam, lParam);
                    }
                    bool nonVoiceCandidate = !isVoiceMapping &&
                        ((mapping != null && mapping.enabled) || taskSwitcherNavigation);
                    if (nonVoiceCandidate)
                    {
                        // A low-level keyboard hook has no source-device identity. More
                        // importantly, returning 1 here prevents Windows from delivering
                        // the corresponding WM_INPUT packet on this RC003 stack. Let the
                        // event continue so device-scoped Raw Input can execute the action;
                        // matching keys on an ordinary keyboard remain untouched.
                        Interlocked.Increment(ref hookCandidatePassthroughCount);
                        return CallNextHookEx(hookHandle, nCode, wParam, lParam);
                    }
                    if (mapping != null && mapping.enabled)
                    {
                        if (isVoiceMapping && mapping.suppress)
                        {
                            // No signed per-device filter is installed, and this
                            // machine's input stack delivers the low-level hook
                            // edge before Raw Input and cancels the Raw Input
                            // packet when the hook suppresses the event. Precise
                            // per-event device attribution is therefore impossible
                            // in user mode. V1.5 handled this by treating F5 as
                            // the voice key and suppressing it at the hook.
                            //
                            // V2 keeps the same suppression but scopes it to the
                            // moments the RC003 is physically connected: only
                            // then is F5 captured and driven through the voice
                            // state machine. Without the RC003, an ordinary
                            // keyboard F5 always passes through untouched. The
                            // edge is dispatched here because suppressing it
                            // would otherwise cancel the Raw Input packet that
                            // used to drive the state machine.
                            if (ShouldUseScopedHookVoice(IsRc003FilterHealthy(),
                                mapping.enabled, mapping.suppress, Rc003PresentRecently()))
                            {
                                Interlocked.Increment(ref suppressedHookEdgeCount);
                                // Read the held state BEFORE the transition, so this edge
                                // can be recognised as a repeat of the current hold.
                                bool repeatEdge = isDown && Volatile.Read(ref voiceKeyHeldState) == 1;
                                HandleVoicePhysicalTransition(isDown,
                                    "rc003_present_hook", data.vkCode, data.scanCode);
                                LogIsolationEdge(isDown, repeatEdge, data.vkCode, data.scanCode);
                                return (IntPtr)1;
                            }
                            // The RC003 is not connected (or the healthy device
                            // filter owns its edges): this F5 belongs to an
                            // ordinary keyboard and must pass through untouched.
                            Interlocked.Increment(ref hookCandidatePassthroughCount);
                            return CallNextHookEx(hookHandle, nCode, wParam, lParam);
                        }
                        // Enabled non-voice mappings and disabled voice mappings
                        // are not reachable here in normal operation; they are
                        // handled by the passthrough branch above or by Raw
                        // Input. Keep the historical hook dispatch as a safety
                        // net only for explicitly non-suppressed mappings.
                        QueueMapping(mapping, isUp, "keyboard_hook");
                        return mapping.suppress ? (IntPtr)1 : CallNextHookEx(hookHandle, nCode, wParam, lParam);
                    }
                    if (mapping == null && IsDiagnosticCandidate(data.vkCode))
                    {
                        Log("Unmapped candidate " + (isDown ? "DOWN" : "UP") + " vk=0x" + data.vkCode.ToString("X2") + " scan=0x" + data.scanCode.ToString("X2"));
                    }
                }
            }
        }

        return CallNextHookEx(hookHandle, nCode, wParam, lParam);
    }

    private static void StartRc003FilterClient()
    {
        if (rc003FilterClient != null) return;
        rc003FilterClient = new Rc003FilterClient(
            HandleRc003FilterEvent,
            HandleRc003FilterHealthChanged);
        rc003FilterClient.UpdatePolicy(BuildRc003FilterSuppressionMask(config));
        rc003FilterClient.Start();
    }

    private static void StopRc003FilterClient()
    {
        Rc003FilterClient client = rc003FilterClient;
        rc003FilterClient = null;
        if (client == null) return;
        try { client.Dispose(); }
        catch (Exception ex) { Log("RC003 filter client shutdown failed: " + ex.Message); }
    }

    private static void RefreshRc003FilterPolicy()
    {
        Rc003FilterClient client = rc003FilterClient;
        if (client == null) return;
        client.UpdatePolicy(BuildRc003FilterSuppressionMask(config));
    }

    private static bool IsRc003FilterHealthy()
    {
        Rc003FilterClient client = rc003FilterClient;
        return client != null && client.IsHealthy;
    }

    private static bool ShouldBypassHookForRc003Filter(bool filterHealthy,
        bool mappingResolved, bool taskSwitcherNavigation)
    {
        return filterHealthy && (mappingResolved || taskSwitcherNavigation);
    }

    private static bool IsDeviceScopedVoiceSource(string source)
    {
        return string.Equals(source, "raw_input", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(source, "rc003_filter", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsVoiceMapping(ShortcutMapping mapping)
    {
        return mapping != null &&
            (mapping.name ?? "").Equals("voice", StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] BuildRc003FilterSuppressionMask(BridgeConfig snapshot)
    {
        byte[] mask = new byte[Rc003FilterProtocol.ScanCodeCount];
        if (snapshot == null || snapshot.mappings == null) return mask;
        foreach (ShortcutMapping mapping in snapshot.mappings)
        {
            if (mapping == null || !mapping.enabled || !mapping.suppress ||
                !(mapping.sourceType ?? "keyboard").Equals("keyboard", StringComparison.OrdinalIgnoreCase))
                continue;
            int scanCode = ParseHexOrDecimal(mapping.scan);
            if (scanCode >= 0 && scanCode < mask.Length) mask[scanCode] = 1;
            if ((mapping.name ?? "").Equals("voice", StringComparison.OrdinalIgnoreCase))
                mask[0x5E] = 1;
        }
        return mask;
    }

    private static void HandleRc003FilterHealthChanged(bool healthy, string detail)
    {
        // A filter transition invalidates the source for every outstanding edge.
        // Release any injected hold state before the next path (filter or Raw Input)
        // takes ownership, otherwise a filter DOWN can strand Ctrl/Win on a later
        // Raw Input UP that the healthy-filter branch intentionally ignores.
        ReleaseAllShortcuts();
        if (healthy)
        {
            rawKeyboardEdgeTracker.Reset();
            Log("RC003 device filter ready; ordinary keyboards are passthrough detail=" + (detail ?? "ready"));
            BridgeForm.SetStatusText("RC003 device filter ready");
            return;
        }

        if (Volatile.Read(ref voiceKeyHeldState) == 1)
            HandleVoicePhysicalTransition(false, "rc003_filter_unavailable", 0, 0);
        rawKeyboardEdgeTracker.Reset();
        Log("RC003 device filter unavailable; Raw Input native-passthrough fallback active detail=" +
            (detail ?? "unknown"));
    }

    private static void HandleRc003FilterEvent(Rc003FilterKeyEvent input)
    {
        Rc003FilterClient client = rc003FilterClient;
        if (client == null || !client.IsGenerationActive(input.Generation)) return;

        ReloadConfigIfChanged();
        client = rc003FilterClient;
        if (client == null || !client.IsGenerationActive(input.Generation)) return;

        bool keyUp = (input.Flags & Rc003FilterProtocol.KeyBreak) != 0;
        int virtualKey = VirtualKeyFromRc003FilterEvent(input.MakeCode, input.Flags);
        ShortcutMapping mapping = FindRc003FilterMapping(virtualKey, input.MakeCode);
        // A disabled mapping must not block the record fallback. The power key and the record key share the raw form
        // VK 0xFF / scan 0x5E — RC003 has emitted the power form in place of F5 across Bluetooth reconnects, and this
        // machine's own bridge log shows the microphone arriving that way. So an *unassigned* power key leaves the
        // record fallback exactly as it was; once the user assigns it an action the mapping is enabled and wins.
        if ((mapping == null || !mapping.enabled || MappingHasNoAction(mapping)) && IsVoiceRawCandidate(virtualKey, input.MakeCode))
            mapping = FindVoiceMapping();
        bool isVoice = IsVoiceMapping(mapping);

        MarkRemoteInput("rc003_filter");
        if (!keyUp && !isVoice) TryCaptureKeyboardButton(virtualKey, input.MakeCode);
        Log("RC003 FILTER KEY " + (keyUp ? "UP" : "DOWN") +
            " vk=0x" + virtualKey.ToString("X2") +
            " scan=0x" + input.MakeCode.ToString("X2") +
            " flags=0x" + input.Flags.ToString("X2") +
            " sequence=" + input.Sequence +
            " suppressed=" + input.Suppressed);

        // Non-suppressed packets are telemetry only; Windows already received
        // their original input and executing them again would duplicate it.
        if (!input.Suppressed) return;

        if (mapping == null || !mapping.enabled)
        {
            int replayFlags = (input.Flags & Rc003FilterProtocol.KeyE0) != 0 ? 1 : 0;
            bool replayed = ReplayPhysicalKeyboardEvent(virtualKey, input.MakeCode, replayFlags, keyUp);
            if (mapping != null && !keyUp)
                RecordActionExecution(mapping, "单击", mapping.shortcut, replayed, "device_filter_replay");
            Log("RC003 filter event replayed reason=mapping_not_resolved");
            return;
        }

        if (isVoice)
        {
            HandleVoicePhysicalTransition(!keyUp, "rc003_filter", virtualKey, input.MakeCode);
            return;
        }
        if (IsTaskSwitcherNavigationCandidate(virtualKey) &&
            HandleTaskSwitcherNavigation(virtualKey, !keyUp, keyUp))
        {
            RecordFilterAction("task-switcher-navigation", keyUp);
            return;
        }
        if ((mapping.shortcut ?? "").Equals("task-switcher", StringComparison.OrdinalIgnoreCase))
        {
            HandleTaskSwitcherToggle(!keyUp, keyUp, mapping, "device_filter");
            RecordFilterAction(mapping.labelOrName(), keyUp);
            return;
        }
        QueueMapping(mapping, keyUp, "device_filter");
        RecordFilterAction(mapping.labelOrName(), keyUp);
    }

    private static void RecordFilterAction(string actionName, bool keyUp)
    {
        Interlocked.Increment(ref filterActionEdgeCount);
        lastRawActionUtc = DateTime.UtcNow;
        lastRawAction = (actionName ?? "unknown") + (keyUp ? ":up" : ":down");
        lastActionSource = "device_filter";
        Log("RC003 action routed source=device_filter button=" + (actionName ?? "unknown") +
            " edge=" + (keyUp ? "up" : "down") +
            " revision=" + (config == null ? "" : config.revision ?? ""));
    }

    private static int VirtualKeyFromRc003FilterEvent(int makeCode, int flags)
    {
        uint encodedScan = (uint)(makeCode & 0xFF);
        if ((flags & Rc003FilterProtocol.KeyE0) != 0) encodedScan |= 0xE000;
        else if ((flags & Rc003FilterProtocol.KeyE1) != 0) encodedScan |= 0xE100;
        int virtualKey = (int)MapVirtualKey(encodedScan, MAPVK_VSC_TO_VK_EX);
        return virtualKey != 0 ? virtualKey : (int)MapVirtualKey((uint)(makeCode & 0xFF), 1);
    }

    private static ShortcutMapping FindRc003FilterMapping(int virtualKey, int scanCode)
    {
        ShortcutMapping exact = FindMapping(virtualKey, scanCode);
        if (exact != null) return exact;
        BridgeConfig snapshot = config;
        if (snapshot == null || snapshot.mappings == null) return null;
        foreach (ShortcutMapping mapping in snapshot.mappings)
        {
            if (mapping == null ||
                !(mapping.sourceType ?? "keyboard").Equals("keyboard", StringComparison.OrdinalIgnoreCase))
                continue;
            if (ParseHexOrDecimal(mapping.scan) == scanCode) return mapping;
        }
        return null;
    }

    private static bool RouteAuthoritativeRawKeyboard(ShortcutMapping mapping,
        int virtualKey, int scanCode, bool keyUp)
    {
        bool isVoice = mapping != null &&
            (mapping.name ?? "").Equals("voice", StringComparison.OrdinalIgnoreCase);
        if (isVoice) return false;

        bool taskSwitcherNavigation = IsTaskSwitcherNavigationCandidate(virtualKey);
        if (!taskSwitcherNavigation && mapping == null) return false;
        if (!rawKeyboardEdgeTracker.ShouldDispatch(virtualKey, scanCode, keyUp)) return false;

        Interlocked.Increment(ref rawRemoteEdgeCount);
        if (!taskSwitcherNavigation && !mapping.enabled)
        {
            if (!keyUp) RecordActionExecution(mapping, "单击", mapping.shortcut, true, "raw_input_passthrough");
            return false;
        }
        string actionName = taskSwitcherNavigation
            ? "task-switcher-navigation"
            : mapping == null ? "unmapped" : mapping.name ?? mapping.labelOrName();
        lastRawActionUtc = DateTime.UtcNow;
        lastRawAction = actionName + (keyUp ? ":up" : ":down");
        lastActionSource = "raw_input";

        if (taskSwitcherNavigation && ObserveNativeTaskSwitcherNavigation(virtualKey, !keyUp, keyUp))
        {
            Interlocked.Increment(ref rawActionEdgeCount);
            Log("RC003 action routed source=raw_input button=" + actionName +
                " edge=" + (keyUp ? "up" : "down") +
                // The label matters: this path executes the action and deliberately lets the native event through,
                // because a low-level hook that returned 1 here would cancel the Raw Input packet the action is
                // executed from. For a non-voice key, suppressing it is therefore only possible through the signed
                // RC003 filter (see BuildRc003FilterSuppressionMask); without that filter installed, an enabled
                // mapping with suppress=true still reaches Windows. Measured on a machine without the filter: the
                // power key routes here with delivery=native_passthrough, which says exactly that.
                " delivery=native_passthrough suppress=filter_only revision=" +
                (config == null ? "" : config.revision ?? ""));
            return true;
        }

        if (mapping == null || !mapping.enabled) return false;
        if ((mapping.shortcut ?? "").Equals("task-switcher", StringComparison.OrdinalIgnoreCase))
        {
            HandleTaskSwitcherToggle(!keyUp, keyUp, mapping, "raw_input");
        }
        else
            QueueMapping(mapping, keyUp, "raw_input");
        Interlocked.Increment(ref rawActionEdgeCount);
        Log("RC003 action routed source=raw_input button=" + mapping.labelOrName() +
            " edge=" + (keyUp ? "up" : "down") +
            " delivery=native_passthrough suppress=filter_only revision=" +
            (config == null ? "" : config.revision ?? ""));
        return true;
    }

    private static bool ReplayPhysicalKeyboardEvent(int virtualKey, int scanCode, int flags, bool keyUp)
    {
        bool extended = (flags & 0x01) == 0x01;
        INPUT input = KeyInput(virtualKey, keyUp, extended);
        if (useScanCode && scanCode > 0)
        {
            input.u.ki.wVk = 0;
            input.u.ki.wScan = (ushort)scanCode;
            input.u.ki.dwFlags = KEYEVENTF_SCANCODE |
                (keyUp ? KEYEVENTF_KEYUP : 0) |
                (extended ? KEYEVENTF_EXTENDEDKEY : 0);
        }
        uint sent = SendInput(1, new INPUT[] { input }, Marshal.SizeOf(typeof(INPUT)));
        if (sent != 1) Log("Physical keyboard replay failed vk=0x" + virtualKey.ToString("X2") +
            " error=" + Marshal.GetLastWin32Error());
        return sent == 1;
    }

    private static ShortcutMapping FindMapping(int vkCode, int scanCode)
    {
        BridgeConfig snapshot = config;
        if (snapshot == null || snapshot.mappings == null)
        {
            return null;
        }

        ShortcutMapping voiceVkFallback = null;
        foreach (ShortcutMapping mapping in snapshot.mappings)
        {
            if ((mapping.sourceType ?? "keyboard").Equals("consumer", StringComparison.OrdinalIgnoreCase)) continue;
            int expectedVk = VkFromName(mapping.vk);
            int expectedScan = ParseHexOrDecimal(mapping.scan);
            if (expectedVk != vkCode) continue;
            if (expectedScan < 0 || expectedScan == scanCode)
            {
                return mapping;
            }

            // Bluetooth HID reconnects can preserve F5 while changing or omitting
            // its scan code. Keep the voice mapping usable in that case; other
            // mappings remain strict so ordinary keyboard input is not captured.
            if ((mapping.name ?? "").Equals("voice", StringComparison.OrdinalIgnoreCase))
            {
                voiceVkFallback = mapping;
            }
        }

        if (voiceVkFallback != null)
        {
            Log("Voice mapping scan fallback vk=0x" + vkCode.ToString("X2") +
                " scan=0x" + scanCode.ToString("X2") + " configured_scan=" +
                (voiceVkFallback.scan ?? ""));
            return voiceVkFallback;
        }
        return null;
    }

    private static void ReinstallKeyboardHook(string reason)
    {
        uint threadId = keyboardHookThreadId;
        if (threadId == 0)
        {
            Log("Keyboard hook reinstall skipped reason=" + (reason ?? "unknown") + " thread=not_ready");
            return;
        }
        lock (hookThreadLock)
        {
            pendingHookReinstallReason = reason ?? "unknown";
        }
        if (!PostThreadMessage(threadId, WM_APP_REINSTALL_HOOK, IntPtr.Zero, IntPtr.Zero))
            Log("Keyboard hook reinstall post failed reason=" + (reason ?? "unknown") +
                " error=" + Marshal.GetLastWin32Error());
    }

    private static int RunSelfTests()
    {
        selfTestMode = true;
        try
        {
            // Who owns the shared raw form 0xFF/0x5E. A user reported that pressing the power key started and stopped
            // dictation, because an actionless mapping handed the shared form to the record fallback. These four
            // assertions pin the corrected rule: the translated F5 form is always the microphone, the shared form is the
            // power key while that form is fresh, and it becomes the microphone again once the window has passed so a
            // remote that only reports the shared form still dictates.
            if (!IsVoiceRawCandidate(0x74, 0x3F))
                throw new InvalidOperationException("The translated F5 form stopped being a voice candidate");
            if (IsVoiceRawCandidate(0xFF, 0x5E))
                throw new InvalidOperationException("The power key's shared raw form is still treated as the microphone");
            lastTranslatedVoiceFormUtc = DateTime.MinValue;
            var gesture = new ShortLongGestureState();
            int shortActions = 0;
            int longActions = 0;
            for (int cycle = 0; cycle < 100; cycle++)
            {
                int generation = gesture.Begin();
                if (generation <= 0 || gesture.Begin() != 0)
                    throw new InvalidOperationException("Short/long gesture accepted a repeated DOWN edge");
                if (!gesture.Release() || gesture.Release())
                    throw new InvalidOperationException("Short gesture release was not exactly-once");
                shortActions++;

                generation = gesture.Begin();
                if (generation <= 0 || !gesture.TryFireLong(generation) ||
                    gesture.TryFireLong(generation) || gesture.Release())
                    throw new InvalidOperationException("Long gesture was not exactly-once");
                longActions++;
            }
            if (shortActions != 100 || longActions != 100 || gesture.IsDown)
                throw new InvalidOperationException("Short/long 100-cycle invariant failed");
            int staleGeneration = gesture.Begin();
            if (gesture.TryFireLong(staleGeneration - 1) || !gesture.Release())
                throw new InvalidOperationException("Stale long-press timer was accepted");
            // Gesture layering: the mapping the Host ships must resolve to the layer that fired, a
            // disabled layer must read as unbound instead of as a failed step, and every layer must carry
            // exactly one action now that macros are gone.
            var layeredMapping = new ShortcutMapping {
                name = "tv", label = "TV 键", shortShortcut = "task-switcher",
                longShortcut = "none", doubleShortcut = "win+shift+s" };
            GestureLayerEntry layeredEntry = ToGestureEntry(layeredMapping);
            if (layeredEntry == null || layeredEntry.longAction != "" ||
                layeredEntry.shortAction != "task-switcher" || layeredEntry.doubleAction != "win+shift+s")
                throw new InvalidOperationException("A disabled gesture layer was not normalized to unbound");
            if (GestureLayerPolicy.Classify(100, false) != GestureKind.Short ||
                GestureLayerPolicy.Classify(GestureLayerPolicy.LongPressMs, false) != GestureKind.Long ||
                GestureLayerPolicy.Classify(50, true) != GestureKind.Double)
                throw new InvalidOperationException("Bridge gesture layering misclassified a short, long or double press");
            IList<string> layeredSteps = GestureBindingStore.ResolveSteps(layeredEntry, GestureKind.Double);
            if (layeredSteps.Count != 1 || layeredSteps[0] != "win+shift+s")
                throw new InvalidOperationException("Bridge gesture layering did not resolve the fired layer");
            layeredSteps = GestureBindingStore.ResolveSteps(layeredEntry, GestureKind.Long);
            if (layeredSteps.Count != 1 || layeredSteps[0] != "task-switcher")
                throw new InvalidOperationException("An unbound gesture layer did not fall back to the configured shorter layer");
            if (GestureBindingStore.ResolveSteps(
                ToGestureEntry(new ShortcutMapping { name = "up" }), GestureKind.Short).Count != 0)
                throw new InvalidOperationException("A gesture layer with no action resolved to something instead of nothing");
            // One action per layer: a mapping whose double layer carries a single action must resolve to
            // exactly that, and the removal of macros must leave no way for a mapping document to smuggle a
            // multi-step sequence back in.
            var singleLayerMapping = new ShortcutMapping {
                name = "menu", label = "功能键", shortShortcut = "ctrl+c", longShortcut = "ctrl+v",
                doubleShortcut = "ctrl+a" };
            GestureLayerEntry singleLayerEntry = ToGestureEntry(singleLayerMapping);
            IList<string> singleLayerSteps = GestureBindingStore.ResolveSteps(singleLayerEntry, GestureKind.Double);
            if (singleLayerSteps.Count != 1 || singleLayerSteps[0] != "ctrl+a" ||
                GestureBindingStore.HasMacro(singleLayerEntry))
                throw new InvalidOperationException("A gesture layer did not resolve to exactly one action");
            // The runner stays the single dispatch path; its stop-at-the-first-failure contract is pinned
            // even though the product now only ever feeds it one action per layer.
            var bridgeRunTrace = new List<string>();
            GestureRunResult bridgeRunFailed = GestureMacroRunner.RunSteps(
                new List<string> { "ctrl+c", "ctrl+v" },
                delegate(string step) { bridgeRunTrace.Add(step); return bridgeRunTrace.Count < 2; });
            if (bridgeRunFailed.Succeeded || bridgeRunFailed.Executed != 1 || bridgeRunFailed.FailedStep != 1 ||
                bridgeRunFailed.Error != GestureMacroRunner.StepFailedCode ||
                GestureMacroRunner.DescribeResult(bridgeRunFailed).IndexOf("failed=2", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("The layer runner did not stop at its first failing step");
            // Phrase packs: the action is recognised, the phrase resolves from the shipped document, and an
            // unknown or empty id resolves to nothing so a stale binding can never type a wrong phrase.
            // Injection itself is deliberately NOT exercised here — SendInput would type the phrase into
            // whatever is focused on the real desktop.
            if (!IsCustomAction("snippet:snip-test") ||
                SnippetTextFor("snip-missing").Length != 0 || SnippetTextFor("").Length != 0 ||
                BridgeConfig.Default().snippets == null || BridgeConfig.Default().snippets.Length != 0)
                throw new InvalidOperationException("Snippet actions are not recognised or do not default safely");
            BridgeConfig savedSnippetConfig = config;
            var snippetFixtureConfig = BridgeConfig.Default();
            snippetFixtureConfig.snippets = new BridgeSnippet[] {
                new BridgeSnippet { id = "snip-test", name = "greeting", text = "hello" } };
            try
            {
                config = snippetFixtureConfig;
                if (SnippetTextFor("snip-test") != "hello" || SnippetTextFor("SNIP-TEST") != "hello" ||
                    SnippetTextFor("snip-other").Length != 0)
                    throw new InvalidOperationException("A snippet phrase did not resolve from the shipped document");
            }
            finally { config = savedSnippetConfig; }
            // A Profile switch must not empty the phrase table. It did: the projection dropped
            // `snippets`, and because Smart Profiles is normally enabled the very first switch made
            // every bound phrase unknown at dispatch time. Every field of the projection is checked
            // here, so adding a config field without carrying it over fails loudly.
            var profileSource = BridgeConfig.Default();
            profileSource.revision = "revision-marker";
            profileSource.notes = "notes-marker";
            profileSource.inputRoutingMode = "strict";
            profileSource.smartProfilesEnabled = true;
            profileSource.smartProfileLocked = true;
            profileSource.fallbackShortcutProfileId = "general";
            profileSource.snippets = new BridgeSnippet[] {
                new BridgeSnippet { id = "snip-json", name = "n", text = "from-json" } };
            var profileTarget = new BridgeShortcutProfile
            {
                id = "browser-ai",
                name = "浏览器 AI",
                mappings = new ShortcutMapping[] {
                    new ShortcutMapping { name = "tv", label = "TV", vk = "Oemtilde", scan = "0x29",
                        enabled = true, suppress = true, mode = "tap", shortcut = "task-switcher" } }
            };
            profileSource.profiles = new BridgeShortcutProfile[] { profileTarget };
            BridgeConfig projectedProfile = ProjectActiveProfile(profileSource, profileTarget);
            if (projectedProfile == null ||
                projectedProfile.snippets == null || projectedProfile.snippets.Length != 1 ||
                projectedProfile.version != profileSource.version ||
                projectedProfile.revision != profileSource.revision ||
                projectedProfile.notes != profileSource.notes ||
                projectedProfile.inputRoutingMode != profileSource.inputRoutingMode ||
                projectedProfile.smartProfilesEnabled != profileSource.smartProfilesEnabled ||
                projectedProfile.smartProfileLocked != profileSource.smartProfileLocked ||
                projectedProfile.fallbackShortcutProfileId != profileSource.fallbackShortcutProfileId ||
                projectedProfile.profiles != profileSource.profiles ||
                projectedProfile.mappings != profileTarget.mappings ||
                projectedProfile.activeShortcutProfileId != "browser-ai" ||
                projectedProfile.activeShortcutProfileName != "浏览器 AI")
                throw new InvalidOperationException(
                    "Switching a Profile dropped a field of the bridge configuration");
            try
            {
                config = projectedProfile;
                if (SnippetTextFor("snip-json") != "from-json")
                    throw new InvalidOperationException(
                        "A bound phrase stopped resolving after a Profile switch");
            }
            finally { config = savedSnippetConfig; }
            // The phrase table reaches this side as JSON inside the mapping document, so the round trip
            // itself is exercised here. The fixture above is built in memory and therefore cannot catch a
            // document whose shape this side does not read back — which is exactly how a bound phrase can
            // look configured in the Host and still be rejected as unknown at dispatch time.
            BridgeConfig jsonSnippetConfig = new JavaScriptSerializer().Deserialize<BridgeConfig>(
                "{\"version\":7,\"snippets\":[{\"id\":\"snip-json\",\"name\":\"n\",\"text\":\"from-json\"}]}");
            if (jsonSnippetConfig == null || jsonSnippetConfig.snippets == null ||
                jsonSnippetConfig.snippets.Length != 1)
                throw new InvalidOperationException(
                    "A snippet table that arrived as JSON did not deserialize into the Bridge config");
            try
            {
                config = jsonSnippetConfig;
                if (SnippetTextFor("snip-json") != "from-json")
                    throw new InvalidOperationException(
                        "A snippet table that arrived as JSON could not be resolved by id");
            }
            finally { config = savedSnippetConfig; }
            if (VkFromName("pageup") != 0x21 || VkFromName("pagedown") != 0x22 ||
                VkFromName("escape") != 0x1B || VkFromName("browserback") != 0xA6)
                throw new InvalidOperationException("Required direction customization keys are unavailable");
            List<int> screenshotShortcut = ParseShortcut("win+shift+s");
            if (screenshotShortcut.Count != 3 || screenshotShortcut[0] != 0x5B ||
                screenshotShortcut[1] != 0xA0 || screenshotShortcut[2] != 0x53)
                throw new InvalidOperationException("Windows screenshot shortcut parsing failed");
            if (TaskSwitcherCommandForKey(0x25) != "left" || TaskSwitcherCommandForKey(0x26) != "up" ||
                TaskSwitcherCommandForKey(0x27) != "right" || TaskSwitcherCommandForKey(0x28) != "down" ||
                TaskSwitcherCommandForKey(0x0D) != "confirm" || TaskSwitcherCommandForKey(0x1B) != "cancel" ||
                TaskSwitcherCommandForKey(0x41) != null || TASK_SWITCHER_TIMEOUT_MS < 30000)
                throw new InvalidOperationException("Persistent Task View navigation policy failed");
            if (!ShouldBypassHookForRc003Filter(true, true, false) ||
                !ShouldBypassHookForRc003Filter(true, false, true) ||
                ShouldBypassHookForRc003Filter(true, false, false) ||
                ShouldBypassHookForRc003Filter(false, true, false) ||
                ShouldBypassHookForRc003Filter(false, true, true) ||
                ShouldBypassHookForRc003Filter(false, false, true))
                throw new InvalidOperationException("RC003 filter hook passthrough policy failed");
            if (IsDeviceScopedVoiceSource("keyboard_hook") ||
                !IsDeviceScopedVoiceSource("raw_input") ||
                !IsDeviceScopedVoiceSource("rc003_filter"))
                throw new InvalidOperationException("Device-blind keyboard hook remained eligible for the voice transition");
            var disabledVoiceMapping = new ShortcutMapping { name = "voice", enabled = false };
            if (!IsVoiceMapping(disabledVoiceMapping))
                throw new InvalidOperationException("Voice mapping identity was lost when the mapping was disabled");
            if (!ShouldRecoverVoiceHostAfterSignal(true, false) ||
                !ShouldRecoverVoiceHostAfterSignal(false, false) ||
                ShouldRecoverVoiceHostAfterSignal(true, true) ||
                ShouldRecoverVoiceHostAfterSignal(false, true))
                throw new InvalidOperationException("Voice host recovery still treats a named-event Set as a listener acknowledgement");
            // RC003 voice F5 isolation policy: without a healthy device filter,
            // hook capture of F5 is allowed only while the RC003 device is
            // physically connected AND the voice mapping is enabled with
            // suppression. In every other combination an ordinary keyboard F5
            // must pass through untouched.
            if (ShouldUseScopedHookVoice(true, true, true, true) ||
                ShouldUseScopedHookVoice(false, false, true, true) ||
                ShouldUseScopedHookVoice(false, true, false, true) ||
                ShouldUseScopedHookVoice(false, true, true, false))
                throw new InvalidOperationException("RC003 scoped hook voice admitted an ordinary-keyboard edge");
            if (!ShouldUseScopedHookVoice(false, true, true, true))
                throw new InvalidOperationException("RC003 scoped hook voice rejected the connected-remote case");
            string[] executableShortcuts = {
                "up", "down", "left", "right", "ctrl+c", "ctrl+x", "ctrl+v", "ctrl+z",
                "ctrl+shift+z", "ctrl+s", "ctrl+a", "ctrl+f", "enter", "escape", "tab",
                "shift+tab", "pageup", "pagedown", "backspace", "alt+left", "browserback", "win+d",
                "win+shift+s", "volumeup", "volumedown", "volumemute", "mediaplaypause"
            };
            foreach (string shortcut in executableShortcuts)
                if (ParseShortcut(shortcut).Count == 0)
                    throw new InvalidOperationException("Mapping action cannot be injected: " + shortcut);
            if (ParseShortcut("rightctrl+rightshift+rightalt+rightwin+p").Count != 5 ||
                ParseShortcut("ctrl+win").Count != 2 ||
                ParseShortcut("ctrl+0xBA").Count != 2 ||
                ParseShortcut("ctrl+unknown").Count != 0 ||
                ParseShortcut("ctrl+p+k").Count != 0 ||
                ParseShortcut("ctrl+alt+delete").Count != 0)
                throw new InvalidOperationException("Custom shortcut parsing accepted a partial or reserved chord");

            var browserTestRequest = new CustomTestRequest
            {
                name = "browser_remote_lite_test",
                created_at = DateTime.UtcNow.ToString("o"),
                expected_process_id = 42,
                expected_window_handle = 84,
                expected_process_name = "chrome"
            };
            string browserDispatchError;
            DateTime browserRequestNow = DateTime.UtcNow;
            if (!IsFreshCustomTestRequest(browserTestRequest, browserRequestNow) ||
                !ValidateBrowserRemoteDispatch(browserTestRequest, false, new IntPtr(84), 42,
                    "chrome.exe", out browserDispatchError) || browserDispatchError != "" ||
                ValidateBrowserRemoteDispatch(browserTestRequest, true, new IntPtr(84), 42,
                    "chrome", out browserDispatchError) ||
                browserDispatchError != "BROWSER-TEST-CANCELED-VOICE" ||
                ValidateBrowserRemoteDispatch(browserTestRequest, false, new IntPtr(85), 42,
                    "chrome", out browserDispatchError) ||
                browserDispatchError != "BROWSER-FOREGROUND-MISMATCH" ||
                ValidateBrowserRemoteDispatch(browserTestRequest, false, new IntPtr(84), 43,
                    "chrome", out browserDispatchError) ||
                ValidateBrowserRemoteDispatch(browserTestRequest, false, new IntPtr(84), 42,
                    "notepad", out browserDispatchError))
                throw new InvalidOperationException("Browser test dispatch accepted stale focus or active recording");
            browserTestRequest.created_at = browserRequestNow.AddSeconds(-9).ToString("o");
            if (IsFreshCustomTestRequest(browserTestRequest, browserRequestNow))
                throw new InvalidOperationException("Expired custom action request remained executable");
            browserTestRequest.created_at = browserRequestNow.AddMinutes(2).ToString("o");
            if (IsFreshCustomTestRequest(browserTestRequest, browserRequestNow))
                throw new InvalidOperationException("Future-dated custom action request remained executable");
            browserTestRequest.created_at = "";
            if (IsFreshCustomTestRequest(browserTestRequest, browserRequestNow))
                throw new InvalidOperationException("Undated custom action request remained executable");

            var browserTapDownSent = new ManualResetEventSlim(false);
            var browserVoiceTransitionEntered = new ManualResetEventSlim(false);
            int browserTapUps = 0;
            bool browserVoiceEnteredBeforeUp = false;
            bool browserWaitCalled = false;
            bool browserTapCanceled = false;
            bool browserTapResult = true;
            Thread browserTapThread = new Thread(new ThreadStart(delegate
            {
                browserTapResult = RunBrowserRemoteTapWithVoicePriority(
                    delegate { return false; },
                    delegate { return true; },
                    delegate
                    {
                        browserTapDownSent.Set();
                        return true;
                    },
                    delegate
                    {
                        browserWaitCalled = true;
                        return true;
                    },
                    delegate
                    {
                        Thread.Sleep(80);
                        Interlocked.Increment(ref browserTapUps);
                        return true;
                    }, out browserTapCanceled);
            }));
            browserTapThread.Start();
            if (!browserTapDownSent.Wait(500))
                throw new InvalidOperationException("Browser test tap did not send key-down");
            Thread voiceTransitionProbe = new Thread(new ThreadStart(delegate
            {
                lock (voiceTransitionLock)
                {
                    browserVoiceEnteredBeforeUp = Volatile.Read(ref browserTapUps) == 0;
                    browserVoiceTransitionEntered.Set();
                }
            }));
            voiceTransitionProbe.Start();
            if (!browserTapThread.Join(1000) || !voiceTransitionProbe.Join(1000) ||
                browserWaitCalled == false || browserTapResult || !browserTapCanceled ||
                browserTapUps != 1 || !browserVoiceEnteredBeforeUp)
                throw new InvalidOperationException(
                    "Browser test tap blocked recording priority behind key-up");
            SetVoiceKeyHeld(false);
            lastVoiceReleaseUtc = DateTime.MinValue;
            var voiceAttempted = new ManualResetEventSlim(false);
            var voiceCompleted = new ManualResetEventSlim(false);
            Thread voicePriorityThread = null;
            bool voicePriorityCanceled;
            bool voicePriorityResult = RunBrowserRemoteTapWithVoicePriority(
                delegate { return Volatile.Read(ref voiceKeyHeldState) == 1; },
                delegate { return true; },
                delegate
                {
                    voicePriorityThread = new Thread(new ThreadStart(delegate
                    {
                        voiceAttempted.Set();
                        HandleVoicePhysicalTransition(true, "browser_priority_self_test", 0x74, 0x3F);
                        voiceCompleted.Set();
                    }));
                    voicePriorityThread.IsBackground = true;
                    voicePriorityThread.Start();
                    return voiceAttempted.Wait(500);
                },
                delegate
                {
                    Stopwatch priorityTimer = Stopwatch.StartNew();
                    while (Volatile.Read(ref voiceTransitionPending) == 0 &&
                        priorityTimer.ElapsedMilliseconds < 500)
                        Thread.Sleep(1);
                    return Volatile.Read(ref voiceTransitionPending) == 1;
                },
                delegate { return true; }, out voicePriorityCanceled);
            if (voicePriorityThread == null || !voicePriorityThread.Join(1000) ||
                !voiceCompleted.IsSet || voicePriorityResult || !voicePriorityCanceled ||
                Volatile.Read(ref voiceKeyHeldState) != 1)
                throw new InvalidOperationException(
                    "Browser tap delayed the recording transition instead of releasing first result=" +
                    voicePriorityResult + " canceled=" + voicePriorityCanceled +
                    " completed=" + voiceCompleted.IsSet + " held=" +
                    Volatile.Read(ref voiceKeyHeldState));
            HandleVoicePhysicalTransition(false, "browser_priority_self_test", 0x74, 0x3F);
            if (Volatile.Read(ref voiceKeyHeldState) != 0)
                throw new InvalidOperationException("Browser priority self-test left recording held");
            voiceAttempted.Dispose();
            voiceCompleted.Dispose();
            int failedDownUps = 0;
            bool failedDownCanceled;
            bool failedDownResult = RunBrowserRemoteTapWithVoicePriority(
                delegate { return false; },
                delegate { return false; },
                delegate { return false; },
                delegate
                {
                    Interlocked.Increment(ref failedDownUps);
                    return true;
                }, out failedDownCanceled);
            if (failedDownResult || failedDownCanceled || failedDownUps != 1)
                throw new InvalidOperationException("Browser test tap did not release keys after a partial DOWN failure");

            var browserRetryVoiceAttempting = new ManualResetEventSlim(false);
            var browserRetryVoiceEntered = new ManualResetEventSlim(false);
            int failedUpCalls = 0;
            bool browserRetryVoiceEnteredBeforeCleanup = false;
            Thread browserRetryVoiceProbe = null;
            bool failedUpCanceled;
            bool failedUpResult = RunBrowserRemoteTapWithVoicePriority(
                delegate { return false; },
                delegate { return true; },
                delegate { return true; },
                delegate { return false; },
                delegate
                {
                    int call = Interlocked.Increment(ref failedUpCalls);
                    if (call == 1)
                    {
                        browserRetryVoiceProbe = new Thread(new ThreadStart(delegate
                        {
                            browserRetryVoiceAttempting.Set();
                            lock (voiceTransitionLock) browserRetryVoiceEntered.Set();
                        }));
                        browserRetryVoiceProbe.Start();
                        if (!browserRetryVoiceAttempting.Wait(500)) return false;
                        return false;
                    }
                    browserRetryVoiceEnteredBeforeCleanup = browserRetryVoiceEntered.IsSet;
                    return true;
                }, out failedUpCanceled);
            if (browserRetryVoiceProbe == null || !browserRetryVoiceProbe.Join(1000) ||
                !failedUpResult || failedUpCanceled || failedUpCalls != 2 ||
                !browserRetryVoiceEnteredBeforeCleanup)
                throw new InvalidOperationException(
                    "Browser test tap still serialized recording behind key-up retry");
            browserTapDownSent.Dispose();
            browserVoiceTransitionEntered.Dispose();
            browserRetryVoiceAttempting.Dispose();
            browserRetryVoiceEntered.Dispose();

            var protocolConfig = new BridgeConfig
            {
                mappings = new ShortcutMapping[]
                {
                    new ShortcutMapping { name = "voice", scan = "0x3F", enabled = true, suppress = true, sourceType = "keyboard" },
                    new ShortcutMapping { name = "home", scan = "0x47", enabled = true, suppress = true, sourceType = "keyboard" },
                    new ShortcutMapping { name = "up", scan = "0x48", enabled = false, suppress = true, sourceType = "keyboard" },
                    new ShortcutMapping { name = "consumer", scan = "0x3F", enabled = true, suppress = true, sourceType = "consumer" }
                }
            };
            byte[] filterMask = BuildRc003FilterSuppressionMask(protocolConfig);
            byte[] filterPolicy = Rc003FilterProtocol.BuildPolicy(filterMask);
            if (filterMask[0x3F] != 1 || filterMask[0x5E] != 1 ||
                filterMask[0x47] != 1 || filterMask[0x48] != 0 ||
                filterPolicy.Length != Rc003FilterProtocol.PolicySize || filterPolicy[16 + 0x3F] != 1 ||
                BitConverter.ToUInt32(filterPolicy, 0) != Rc003FilterProtocol.Magic ||
                BitConverter.ToUInt32(filterPolicy, 8) != Rc003FilterProtocol.PolicySize ||
                Rc003FilterProtocol.IoctlGetInfo != 0x80006400U ||
                Rc003FilterProtocol.IoctlSetPolicy != 0x8000A404U ||
                Rc003FilterProtocol.IoctlHeartbeat != 0x8000A408U ||
                Rc003FilterProtocol.IoctlReadEvents != 0x8000640CU ||
                Rc003FilterProtocol.IoctlDisarm != 0x8000A410U)
                throw new InvalidOperationException("RC003 filter policy or IOCTL layout mismatch");

            byte[] infoFixture = new byte[Rc003FilterProtocol.InfoSize];
            Buffer.BlockCopy(BitConverter.GetBytes(Rc003FilterProtocol.Magic), 0, infoFixture, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(Rc003FilterProtocol.ApiVersion), 0, infoFixture, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes((uint)Rc003FilterProtocol.InfoSize), 0, infoFixture, 8, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(1U), 0, infoFixture, 12, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(7UL), 0, infoFixture, 32, 8);
            int attachedFixture;
            long droppedFixture;
            Rc003FilterProtocol.ParseInfo(infoFixture, infoFixture.Length, out attachedFixture, out droppedFixture);
            if (attachedFixture != 1 || droppedFixture != 7)
                throw new InvalidOperationException("RC003 filter info parsing failed");

            byte[] eventFixture = new byte[Rc003FilterProtocol.EventBatchHeaderSize + Rc003FilterProtocol.EventSize];
            Buffer.BlockCopy(BitConverter.GetBytes(Rc003FilterProtocol.Magic), 0, eventFixture, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(Rc003FilterProtocol.ApiVersion), 0, eventFixture, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes((uint)Rc003FilterProtocol.EventBatchHeaderSize), 0, eventFixture, 8, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(1U), 0, eventFixture, 12, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(7UL), 0, eventFixture, 16, 8);
            int eventOffset = Rc003FilterProtocol.EventBatchHeaderSize;
            Buffer.BlockCopy(BitConverter.GetBytes(Rc003FilterProtocol.Magic), 0, eventFixture, eventOffset, 4);
            Buffer.BlockCopy(BitConverter.GetBytes((uint)Rc003FilterProtocol.EventSize), 0, eventFixture, eventOffset + 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(42UL), 0, eventFixture, eventOffset + 8, 8);
            Buffer.BlockCopy(BitConverter.GetBytes((ushort)0x47), 0, eventFixture, eventOffset + 24, 2);
            Buffer.BlockCopy(BitConverter.GetBytes((ushort)(Rc003FilterProtocol.KeyBreak | Rc003FilterProtocol.KeyE0)), 0,
                eventFixture, eventOffset + 26, 2);
            List<Rc003FilterKeyEvent> parsedEvents = Rc003FilterProtocol.ParseEvents(
                eventFixture, eventFixture.Length, 9, filterMask, out droppedFixture);
            if (parsedEvents.Count != 1 || parsedEvents[0].Sequence != 42UL ||
                parsedEvents[0].MakeCode != 0x47 || !parsedEvents[0].Suppressed ||
                parsedEvents[0].Generation != 9 || droppedFixture != 7 ||
                VirtualKeyFromRc003FilterEvent(0x47, Rc003FilterProtocol.KeyE0) != 0x24 ||
                VirtualKeyFromRc003FilterEvent(0x3F, 0) != 0x74)
                throw new InvalidOperationException("RC003 filter event parsing or scan translation failed");
            bool malformedBatchRejected = false;
            try
            {
                Rc003FilterProtocol.ParseEvents(eventFixture, eventFixture.Length - 1,
                    9, filterMask, out droppedFixture);
            }
            catch (InvalidDataException) { malformedBatchRejected = true; }
            if (!malformedBatchRejected)
                throw new InvalidOperationException("RC003 filter accepted a truncated event batch");

            BridgeConfig previousConfig = config;
            try
            {
                var fixture = new BridgeConfig
                {
                    version = 7,
                    revision = "config-integrity-fixture",
                    notes = "self-test",
                    inputRoutingMode = "compatibility",
                    activeShortcutProfileId = "self-test",
                    activeShortcutProfileName = "Self Test",
                    smartProfilesEnabled = true,
                    smartProfileLocked = false,
                    fallbackShortcutProfileId = "self-test",
                    mappings = new ShortcutMapping[]
                    {
                        new ShortcutMapping { name = "voice", label = "录音键", vk = "F5", scan = "0x3F", enabled = true, suppress = true, mode = "suppress", shortcut = "" },
                        new ShortcutMapping { name = "up", label = "上键", vk = "Up", scan = "0x48", enabled = true, suppress = true, mode = "tap", shortcut = "win+shift+s", sourceType = "keyboard" }
                    },
                    profiles = new BridgeShortcutProfile[]
                    {
                        new BridgeShortcutProfile { id = "self-test", name = "Self Test", processNames = new string[0], mappings = new ShortcutMapping[0] },
                        new BridgeShortcutProfile { id = "browser", name = "Browser", processNames = new string[] { "chrome", "msedge.exe" }, mappings = new ShortcutMapping[0] }
                    }
                };
                string serialized = new JavaScriptSerializer().Serialize(fixture);
                BridgeConfig loaded = new JavaScriptSerializer().Deserialize<BridgeConfig>(serialized);
                config = loaded;
                ShortcutMapping upMapping = FindMapping(0x26, 0x48);
                string smartState;
                BridgeShortcutProfile smartMatched = ResolveSmartProfileTarget(
                    loaded, "self-test", "chrome.exe", out smartState);
                if (smartMatched == null || smartMatched.id != "browser" || smartState != "matched")
                    throw new InvalidOperationException("Smart Profile did not match the foreground process");
                BridgeShortcutProfile smartFallback = ResolveSmartProfileTarget(
                    loaded, "self-test", "notepad", out smartState);
                if (smartFallback == null || smartFallback.id != "self-test" || smartState != "fallback")
                    throw new InvalidOperationException("Smart Profile fallback was not deterministic");
                loaded.smartProfileLocked = true;
                BridgeShortcutProfile smartLocked = ResolveSmartProfileTarget(
                    loaded, "self-test", "chrome", out smartState);
                loaded.smartProfileLocked = false;
                if (smartLocked == null || smartLocked.id != "self-test" || smartState != "locked")
                    throw new InvalidOperationException("Smart Profile lock did not retain the configured Profile");
                if (loaded == null || loaded.version != 7 || loaded.revision != "config-integrity-fixture" ||
                    loaded.activeShortcutProfileId != "self-test" || loaded.activeShortcutProfileName != "Self Test" ||
                    FindBridgeProfileForProcess(loaded, "chrome.exe").id != "browser" ||
                    FindBridgeProfileForProcess(loaded, "MSedge").id != "browser" ||
                    FindBridgeProfileForProcess(loaded, "notepad") != null ||
                    !IsVibeFlowProcess("VibeFlow.exe") || !IsVibeFlowProcess("VoxDeckInputBridge") ||
                    IsVibeFlowProcess("cursor") ||
                    upMapping == null || upMapping.shortcut != "win+shift+s" || ParseShortcut(upMapping.shortcut).Count != 3 ||
                    NormalizeInputRoutingMode(loaded.inputRoutingMode) != "strict")
                    throw new InvalidOperationException("Persisted bridge configuration did not resolve to its configured runtime actions");
            }
            finally { config = previousConfig; }
            // A corrupt or partially-written reload can leave config.mappings
            // null. Verify the release path still clears cached hold state in
            // that case, without attempting to inject a real shortcut.
            BridgeConfig releaseConfig = config;
            try
            {
                config = new BridgeConfig { mappings = null };
                activeShortcutMappings.Clear();
                shortcutDown.Clear();
                sourceDown.Clear();
                activeShortcutMappings["release-fixture"] = new ShortcutMapping {
                    name = "release-fixture", label = "release fixture", shortcut = ""
                };
                shortcutDown["release-fixture"] = true;
                sourceDown["release-fixture"] = true;
                HandleRc003FilterHealthChanged(false, "self-test-transition");
                if (activeShortcutMappings.Count != 0 || shortcutDown.Count != 0 || sourceDown.Count != 0)
                    throw new InvalidOperationException("Filter transition did not clear held shortcut state");
            }
            finally
            {
                activeShortcutMappings.Clear();
                shortcutDown.Clear();
                sourceDown.Clear();
                config = releaseConfig;
            }
            string processPart = Convert.ToBase64String(Encoding.UTF8.GetBytes("notepad"));
            string pathPart = Convert.ToBase64String(Encoding.UTF8.GetBytes("C:\\Windows\\System32\\notepad.exe"));
            string labelPart = Convert.ToBase64String(Encoding.UTF8.GetBytes("Notepad"));
            string appIdPart = Convert.ToBase64String(Encoding.UTF8.GetBytes("Microsoft.WindowsNotepad_8wekyb3d8bbwe!App"));
            string openAppContract = "open-app:" + processPart + "|" + pathPart + "|" + labelPart + "|" + appIdPart;
            if (!IsCustomAction(openAppContract) || DecodeActionPart(processPart) != "notepad" ||
                !IsSafeProcessName(DecodeActionPart(processPart)) ||
                !IsSafeStartAppId(DecodeActionPart(appIdPart)) ||
                !IsCustomAction("open-url:https://example.com") ||
                !IsCustomAction("open-exe:C:\\Windows\\System32\\notepad.exe") ||
                !IsCustomAction("shortcut:ctrl+shift+p") ||
                !IsAiLauncherAction("launch-client:codex"))
                throw new InvalidOperationException("Application, URL, or custom shortcut contract failed");
            // Every shortcut the Host can store must actually dispatch: it has to parse into
            // injectable keys, and it must have exactly one non-modifier. A value that parses to
            // nothing would look configured in the UI and do nothing when the key is pressed.
            string[] canonicalShortcuts = {
                "ctrl+c", "ctrl+x", "ctrl+v", "ctrl+z", "ctrl+shift+z", "ctrl+s", "ctrl+a", "ctrl+f",
                "enter", "escape", "tab", "shift+tab", "pageup", "pagedown", "backspace", "alt+left",
                "browserback", "up", "down", "left", "right", "win+d", "win+shift+s",
                "volumeup", "volumedown", "volumemute", "mediaplaypause"
            };
            foreach (string shortcut in canonicalShortcuts)
            {
                List<int> parsed = ParseShortcut(shortcut);
                if (parsed.Count == 0)
                    throw new InvalidOperationException("The stored shortcut '" + shortcut +
                        "' no longer parses into injectable keys");
                int mainKeys = 0;
                foreach (int virtualKey in parsed) if (!IsModifierKey(virtualKey)) mainKeys++;
                if (mainKeys != 1)
                    throw new InvalidOperationException("The stored shortcut '" + shortcut +
                        "' does not resolve to exactly one main key");
            }
            // The named system actions are commands, not chords, and "no action" must never
            // resolve to a key press.
            if (ParseShortcut("task-switcher").Count != 0 || ParseShortcut("none").Count != 0 ||
                ParseShortcut("passthrough").Count != 0 || IsCustomAction("none") ||
                IsAiLauncherAction("none") || IsCustomAction("passthrough"))
                throw new InvalidOperationException("A command or no-action value resolves as a keyboard chord");
            // Every launcher the Host offers must be recognised here, or the key would do nothing.
            string[] launchers = {
                "launch-client:chatgpt", "launch-client:claude", "launch-client:deepseek",
                "launch-client:cursor", "launch-client:vscode", "launch-client:codex",
                "launch-client:terminal"
            };
            foreach (string launcher in launchers)
                if (!IsAiLauncherAction(launcher))
                    throw new InvalidOperationException("The launcher '" + launcher +
                        "' can be stored by the Host but is not recognised by the bridge");
            // The third launcher path resolves a start-menu shortcut's own target, which is how
            // an application installed outside Program Files (no App Paths registration) is
            // found. Both halves run against artifacts this test creates, so the result does not
            // depend on which applications the machine happens to have: the search is pointed at
            // a directory the test owns, and the resolution runs on a shortcut the test wrote.
            string probeRoot = Path.Combine(Path.GetTempPath(),
                "vibe-flow-start-menu-probe-" + Guid.NewGuid().ToString("N"));
            string probeShortcut = Path.Combine(probeRoot, "VibeFlowProbe.lnk");
            string probeNested = Path.Combine(probeRoot, "Vendor", "VibeFlowProbe Beta.lnk");
            string probeDecoy = Path.Combine(probeRoot, "VibeFlowProbeTool.lnk");
            string probeTarget = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "notepad.exe");
            try
            {
                Directory.CreateDirectory(Path.Combine(probeRoot, "Vendor"));
                CreateProbeShortcut(probeShortcut, probeTarget);
                CreateProbeShortcut(probeNested, probeTarget);
                CreateProbeShortcut(probeDecoy, probeTarget);
                string resolved = ResolveShortcutTarget(probeShortcut);
                if (!string.Equals(resolved, probeTarget, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Resolving a start-menu shortcut returned '" +
                        resolved + "' instead of '" + probeTarget +
                        "', so an application installed outside Program Files cannot be started");
                List<string> located = FindStartMenuShortcuts("VibeFlowProbe", new string[] { probeRoot });
                if (located.Count != 2 || !StartMenuProbeContains(located, probeShortcut) ||
                    !StartMenuProbeContains(located, probeNested) ||
                    StartMenuProbeContains(located, probeDecoy))
                    throw new InvalidOperationException("The start-menu search found " + located.Count +
                        " shortcuts where one nested shortcut carries the exact name and one is a decoy, " +
                        "so an application installed outside Program Files cannot be started");
            }
            finally
            {
                try { if (Directory.Exists(probeRoot)) Directory.Delete(probeRoot, true); } catch { }
            }
            var edgeTracker = new RawKeyboardEdgeTracker();
            for (int cycle = 0; cycle < 100; cycle++)
            {
                if (!edgeTracker.ShouldDispatch(0x26, 0x48, false) ||
                    edgeTracker.ShouldDispatch(0x26, 0x48, false) ||
                    !edgeTracker.ShouldDispatch(0x26, 0x48, true) ||
                    edgeTracker.ShouldDispatch(0x26, 0x48, true))
                    throw new InvalidOperationException("Raw Input edge tracker accepted a repeat or duplicate release");
            }
            edgeTracker.Reset();
            List<int> partialDownRecovery = PartialShortcutRecoveryKeys(
                new List<int> { 0x11, 0x10, 0x53 }, false, 2);
            List<int> partialUpRecovery = PartialShortcutRecoveryKeys(
                new List<int> { 0x53, 0x10, 0x11 }, true, 1);
            if (partialDownRecovery.Count != 2 || partialDownRecovery[0] != 0x10 ||
                partialDownRecovery[1] != 0x11 || partialUpRecovery.Count != 2 ||
                partialUpRecovery[0] != 0x10 || partialUpRecovery[1] != 0x11)
                throw new InvalidOperationException("Partial shortcut recovery order was not deterministic");

            // A stuck record key: the remote keeps reporting it held and its release edge
            // is never seen. This is the exact shape that used to wedge the machine, because
            // the old watchdog compared "time since last activity" and a repeating key always
            // looks active, so the hold was never released and the frozen Capture re-armed a
            // session on every ATVV reconnect. Assert the release fires even though activity
            // is still arriving, and that the repeats that follow cannot re-arm it.
            long savedHeld = Volatile.Read(ref voiceKeyHeldState);
            long savedStarted = Interlocked.Read(ref voiceHoldStartedTicks);
            DateTime savedActivity = lastVoiceActivityUtc;
            DateTime savedRelease = lastVoiceReleaseUtc;
            int savedLatched = Volatile.Read(ref voiceHoldStaleLatched);
            int savedStaleReleases = voiceHoldStaleReleaseCount;
            int savedRepeats = voiceHoldRepeatsSuppressed;
            int savedIsolationRepeats = isolationRepeatCount;
            try
            {
                // The latch can only be cleared by a quiet gap, so by the time it lifts the
                // release is already at least that far in the past. That must also be longer
                // than the restart guard, otherwise the user's next real press would be
                // rejected as a restart; assert the relationship rather than assume it.
                if (VOICE_HOLD_LATCH_CLEAR_MS <= VOICE_RESTART_GUARD_MS)
                    throw new InvalidOperationException(
                        "The stuck-hold latch can clear before the restart guard expires, so the next press would be lost");
                SetVoiceKeyHeld(true);
                Interlocked.Exchange(ref voiceHoldStartedTicks,
                    DateTime.UtcNow.AddMilliseconds(-(VOICE_HOLD_STUCK_BOUND_MS + 1000)).Ticks);
                lastVoiceActivityUtc = DateTime.UtcNow;   // repeats still arriving right now
                ReleaseStuckVoiceHoldIfIdle();
                if (Volatile.Read(ref voiceKeyHeldState) != 0 || Volatile.Read(ref voiceHoldStaleLatched) != 1 ||
                    voiceHoldStaleReleaseCount != savedStaleReleases + 1)
                    throw new InvalidOperationException(
                        "A record key still reported held past the device bound was not released as stuck");
                // The repeats that keep arriving must not be accepted as a new press.
                HandleVoicePhysicalTransition(true, "self_test_stuck", 0x74, 0x3F);
                if (Volatile.Read(ref voiceKeyHeldState) != 0)
                    throw new InvalidOperationException("A latched stuck hold was re-armed by a repeat");
                // A quiet gap is the only evidence of release, and it lifts the latch. Time is
                // modelled honestly: the latch needs VOICE_HOLD_LATCH_CLEAR_MS of quiet, so the
                // release that preceded it is at least that old.
                lastVoiceActivityUtc = DateTime.UtcNow.AddMilliseconds(-(VOICE_HOLD_LATCH_CLEAR_MS + 100));
                lastVoiceReleaseUtc = DateTime.UtcNow.AddMilliseconds(-(VOICE_HOLD_LATCH_CLEAR_MS + 200));
                ReleaseStuckVoiceHoldIfIdle();
                if (Volatile.Read(ref voiceHoldStaleLatched) != 0)
                    throw new InvalidOperationException("The stuck-hold latch was not cleared by a quiet key");
                // A genuine press after release must still work.
                HandleVoicePhysicalTransition(true, "self_test_after_release", 0x74, 0x3F);
                if (Volatile.Read(ref voiceKeyHeldState) != 1)
                    throw new InvalidOperationException("A genuine press after a stuck release was rejected");
                HandleVoicePhysicalTransition(false, "self_test_after_release", 0x74, 0x3F);
                // Repeats are aggregated: the counter grows but the log stays quiet.
                int repeatsBefore = voiceHoldRepeatsSuppressed;
                for (int repeat = 0; repeat < 50; repeat++) LogIsolationEdge(true, true, 0x74, 0x3F);
                if (voiceHoldRepeatsSuppressed != repeatsBefore + 50)
                    throw new InvalidOperationException("Held-key repeats are not being aggregated");
                if (isolationRepeatCount != savedIsolationRepeats + 50)
                    throw new InvalidOperationException("The repeat counter did not follow the held-key repeats");
            }
            finally
            {
                SetVoiceKeyHeld(savedHeld == 1);
                Interlocked.Exchange(ref voiceHoldStartedTicks, savedStarted);
                lastVoiceActivityUtc = savedActivity;
                lastVoiceReleaseUtc = savedRelease;
                Interlocked.Exchange(ref voiceHoldStaleLatched, savedLatched);
                voiceHoldStaleReleaseCount = savedStaleReleases;
                voiceHoldRepeatsSuppressed = savedRepeats;
                isolationRepeatCount = savedIsolationRepeats;
            }
            Console.WriteLine("Vibe Link input bridge self-test passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Input bridge self-test failed: " + ex.Message);
            return 1;
        }
    }

    private static ShortcutMapping FindConsumerMapping(int usagePage, int usage)
    {
        return FindHidMapping("consumer", usagePage, usage);
    }

    private static ShortcutMapping FindHidMapping(int usagePage, int usage)
    {
        return FindHidMapping(usagePage == HID_USAGE_PAGE_CONSUMER ? "consumer" : "hid", usagePage, usage);
    }

    private static ShortcutMapping FindHidMapping(string sourceType, int usagePage, int usage)
    {
        BridgeConfig snapshot = config;
        if (snapshot == null || snapshot.mappings == null) return null;
        foreach (ShortcutMapping mapping in snapshot.mappings)
        {
            if (!(mapping.sourceType ?? "keyboard").Equals(sourceType, StringComparison.OrdinalIgnoreCase)) continue;
            if (mapping.usagePage == usagePage && mapping.usage == usage) return mapping;
        }
        return null;
    }

    private static void QueueMapping(ShortcutMapping mapping, bool keyUp, string source)
    {
        try { mappingQueue.Add(new MappingEvent { mapping = mapping, keyUp = keyUp, source = source }); }
        catch (InvalidOperationException) { }
    }

    private static void QueueTaskSwitcherCommand(string command)
    {
        try { mappingQueue.Add(new MappingEvent { command = command }); }
        catch (InvalidOperationException) { }
    }

    private static void QueueTaskSwitcherCommand(string command, ShortcutMapping mapping, string source)
    {
        try { mappingQueue.Add(new MappingEvent { command = command, mapping = mapping, source = source }); }
        catch (InvalidOperationException) { }
    }

    private static void ProcessMappingQueue()
    {
        foreach (MappingEvent item in mappingQueue.GetConsumingEnumerable())
        {
            if (!string.IsNullOrWhiteSpace(item.command))
            {
                bool success = HandleTaskSwitcherCommand(item.command);
                if (item.mapping != null)
                    RecordActionExecution(item.mapping, "单击", item.mapping.shortcut, success, item.source);
            }
            else if (!string.IsNullOrWhiteSpace(item.testToken))
            {
                bool success;
                string errorCode = "";
                if (item.browserTestRequest != null)
                {
                    bool canceledBeforeExecution;
                    success = BrowserRemoteRequestFile.TryExecuteClaimed(
                        item.browserTestClaimPath, CustomTestPath,
                        item.browserTestRequest.token,
                        delegate
                        {
                            if (!ValidateBrowserRemoteDispatchNow(item.browserTestRequest,
                                out errorCode)) return false;
                            return ExecuteBrowserRemoteTestAction(item.mapping,
                                item.testAction, item.browserTestRequest, out errorCode);
                        }, out canceledBeforeExecution);
                    if (canceledBeforeExecution) errorCode = "BROWSER-TEST-CANCELED";
                }
                else success = ExecuteMappingAction(item.mapping, item.testAction, "测试", "ui_test");
                WriteCustomButtonTestResult(item.testToken, item.testAction, success,
                    success ? "动作已由按键桥接执行" :
                        errorCode == "BROWSER-TEST-CANCELED-VOICE" ? "录音已开始，浏览器测试未执行" :
                        errorCode == "BROWSER-TEST-CANCELED" ? "测试已取消，浏览器测试未执行" :
                        errorCode == "BROWSER-TEST-REQUEST-EXPIRED" ? "测试请求已过期，请重新测试" :
                        errorCode == "BROWSER-FOREGROUND-MISMATCH" ? "浏览器前台目标已变化，按键动作未执行" :
                        "动作未执行，请检查应用路径或动作配置", errorCode);
            }
            else HandleMapping(item.mapping, item.keyUp, item.source);
        }
    }

    private static void HandleTaskSwitcherToggle(bool isDown, bool isUp, ShortcutMapping mapping, string source)
    {
        lock (taskSwitcherLock)
        {
            const int tvKey = 0xC0;
            if (isUp)
            {
                taskSwitcherKeysDown.Remove(tvKey);
                return;
            }
            if (!isDown || taskSwitcherKeysDown.Contains(tvKey)) return;
            taskSwitcherKeysDown.Add(tvKey);
            if (taskSwitcherActive)
            {
                taskSwitcherActive = false;
                QueueTaskSwitcherCommand("cancel", mapping, source);
            }
            else
            {
                taskSwitcherActive = true;
                QueueTaskSwitcherCommand("open", mapping, source);
            }
        }
    }

    private static bool HandleTaskSwitcherNavigation(int virtualKey, bool isDown, bool isUp)
    {
        lock (taskSwitcherLock)
        {
            if (!taskSwitcherActive) return false;
            string command = TaskSwitcherCommandForKey(virtualKey);
            if (command == null) return false;
            if (isUp)
            {
                taskSwitcherKeysDown.Remove(virtualKey);
                return true;
            }
            if (!isDown || taskSwitcherKeysDown.Contains(virtualKey)) return true;
            taskSwitcherKeysDown.Add(virtualKey);
            if (command == "confirm" || command == "cancel")
            {
                taskSwitcherActive = false;
            }
            QueueTaskSwitcherCommand(command);
            return true;
        }
    }

    private static bool ObserveNativeTaskSwitcherNavigation(int virtualKey, bool isDown, bool isUp)
    {
        lock (taskSwitcherLock)
        {
            if (!taskSwitcherActive) return false;
            string command = TaskSwitcherCommandForKey(virtualKey);
            if (command == null) return false;
            if (isUp)
            {
                taskSwitcherKeysDown.Remove(virtualKey);
                return true;
            }
            if (!isDown || taskSwitcherKeysDown.Contains(virtualKey)) return true;
            taskSwitcherKeysDown.Add(virtualKey);
            if (command == "confirm" || command == "cancel")
            {
                taskSwitcherActive = false;
                if (taskSwitcherTimer != null)
                {
                    taskSwitcherTimer.Dispose();
                    taskSwitcherTimer = null;
                }
                taskSwitcherKeysDown.Clear();
            }
            Log("Task View navigation observed source=raw_input delivery=native command=" + command);
            return true;
        }
    }

    private static bool IsTaskSwitcherNavigationCandidate(int virtualKey)
    {
        lock (taskSwitcherLock)
        {
            return taskSwitcherActive && TaskSwitcherCommandForKey(virtualKey) != null;
        }
    }

    private static string TaskSwitcherCommandForKey(int virtualKey)
    {
        if (virtualKey == 0x25) return "left";
        if (virtualKey == 0x26) return "up";
        if (virtualKey == 0x27) return "right";
        if (virtualKey == 0x28) return "down";
        if (virtualKey == 0x0D) return "confirm";
        if (virtualKey == 0x1B || virtualKey == 0x08 || virtualKey == 0xA6) return "cancel";
        return null;
    }

    private static void HandleMapping(ShortcutMapping mapping, bool keyUp, string source)
    {
        string requestedMode = (mapping.mode ?? "tap").ToLowerInvariant();
        if (requestedMode == "shortlong")
        {
            HandleShortLongMapping(mapping, keyUp, source);
            return;
        }
        lock (stateLock)
        {
            string name = mapping.name ?? mapping.vk ?? "unknown";
            bool isVoice = name.Equals("voice", StringComparison.OrdinalIgnoreCase);
            bool wasSourceDown = sourceDown.ContainsKey(name) && sourceDown[name];
            string mode = requestedMode;

            if (!keyUp)
            {
                if (wasSourceDown)
                {
                    return;
                }
                sourceDown[name] = true;
                Log("Key " + mapping.labelOrName() + " DOWN vk=" + mapping.vk + " scan=" + mapping.scan);

                if (isVoice)
                {
                    if (Volatile.Read(ref voiceKeyHeldState) == 1) SignalVoiceKeyPressed();
                    else Log("Voice key press discarded: released_before_dispatch");
                }

                if (mode == "passthrough")
                {
                    return;
                }
                if (mode == "suppress")
                {
                    BridgeForm.SetStatusText(mapping.labelOrName() + " 派发中");
                    return;
                }
                if (mode == "hold")
                {
                    if (IsRepeatableHoldAction(mapping.shortcut))
                    {
                        bool success = TapShortcut(mapping);
                        RecordActionExecution(mapping, "按住", mapping.shortcut, success, source);
                        StartHoldRepeat(name, mapping);
                        shortcutDown[name] = false;
                    }
                    else
                    {
                        bool success = SendShortcut(mapping, false);
                        RecordActionExecution(mapping, "按下", mapping.shortcut, success, source);
                        shortcutDown[name] = success;
                        if (success)
                            activeShortcutMappings[name] = mapping;
                    }
                    BridgeForm.SetStatusText(mapping.labelOrName() + " 按下 -> " + mapping.shortcut);
                }
                else if (IsAiLauncherAction(mapping.shortcut))
                {
                    bool success = LaunchAiTarget(mapping.shortcut);
                        RecordActionExecution(mapping, "单击", mapping.shortcut, success, source);
                }
                else if (IsCustomAction(mapping.shortcut))
                {
                    bool success = HandleCustomAction(mapping);
                        RecordActionExecution(mapping, "单击", mapping.shortcut, success, source);
                }
                else
                {
                    bool success = TapShortcut(mapping);
                        RecordActionExecution(mapping, "单击", mapping.shortcut, success, source);
                    BridgeForm.SetStatusText(mapping.labelOrName() + " 轻触 -> " + mapping.shortcut);
                }
                return;
            }

            if (!wasSourceDown)
            {
                Log("Key " + mapping.labelOrName() + " duplicate UP ignored vk=" + mapping.vk + " scan=" + mapping.scan);
                return;
            }
            sourceDown[name] = false;
            Log("Key " + mapping.labelOrName() + " UP vk=" + mapping.vk + " scan=" + mapping.scan);
            if (mode == "hold" && IsRepeatableHoldAction(mapping.shortcut))
            {
                StopHoldRepeat(name);
                BridgeForm.SetStatusText(mapping.labelOrName() + " 已松开");
            }
            else if (activeShortcutMappings.ContainsKey(name))
            {
                ShortcutMapping activeMapping = activeShortcutMappings[name];
                SendShortcut(activeMapping, true);
                shortcutDown[name] = false;
                activeShortcutMappings.Remove(name);
                BridgeForm.SetStatusText(activeMapping.labelOrName() + " 松开 -> " + activeMapping.shortcut);
            }
            else if (mode == "hold" && shortcutDown.ContainsKey(name) && shortcutDown[name])
            {
                SendShortcut(mapping, true);
                shortcutDown[name] = false;
                BridgeForm.SetStatusText(mapping.labelOrName() + " 松开 -> " + mapping.shortcut);
            }
        }
    }

    private static void HandleShortLongMapping(ShortcutMapping mapping, bool keyUp, string source)
    {
        string name = mapping.name ?? mapping.vk ?? "unknown";
        // The record key (F5) is welded to the stable voice chain, so it never enters gesture
        // layering: a layered dispatch can never re-route hold-to-talk.
        if (name.Equals("voice", StringComparison.OrdinalIgnoreCase)) return;
        GestureKind kind = GestureKind.Short;
        bool dispatch = false;
        lock (stateLock)
        {
            bool wasSourceDown = sourceDown.ContainsKey(name) && sourceDown[name];
            if (!keyUp)
            {
                if (wasSourceDown)
                {
                    Log("Gesture " + mapping.labelOrName() + " duplicate DOWN ignored");
                    return;
                }
                sourceDown[name] = true;
                gesturePressStartMs[name] = Environment.TickCount;
                ShortLongGestureState state;
                if (!gestureStates.TryGetValue(name, out state))
                {
                    state = new ShortLongGestureState();
                    gestureStates[name] = state;
                }
                int generation = state.Begin();
                DisposeGestureTimer(name);
                int threshold = mapping.longPressMs > 0 ? mapping.longPressMs : DEFAULT_LONG_PRESS_MS;
                var request = new GestureTimerRequest {
                    Name = name, Generation = generation, Mapping = mapping, Source = source
                };
                gestureTimers[name] = new System.Threading.Timer(FireLongGesture, request, threshold, Timeout.Infinite);
                Log("Key " + mapping.labelOrName() + " DOWN gesture=layered threshold_ms=" + threshold);
                BridgeForm.SetStatusText(mapping.labelOrName() + " 派发中");
                return;
            }

            if (!wasSourceDown)
            {
                Log("Gesture " + mapping.labelOrName() + " duplicate UP ignored");
                return;
            }
            sourceDown[name] = false;
            DisposeGestureTimer(name);
            int pressStart;
            int holdMs = gesturePressStartMs.TryGetValue(name, out pressStart)
                ? Environment.TickCount - pressStart : 0;
            gesturePressStartMs.Remove(name);
            ShortLongGestureState current;
            bool releasedBeforeLong = gestureStates.TryGetValue(name, out current) && current.Release();
            if (releasedBeforeLong)
            {
                int previousTapMs;
                bool previousTapWithinWindow = gestureLastTapMs.TryGetValue(name, out previousTapMs) &&
                    previousTapMs != 0 &&
                    Environment.TickCount - previousTapMs <= GestureLayerPolicy.DoubleTapWindowMs;
                kind = GestureLayerPolicy.Classify(holdMs, previousTapWithinWindow);
                // A double tap consumes the pair, so a third press starts a fresh one; a long press
                // never counts as the first tap of a double either.
                gestureLastTapMs[name] = kind == GestureKind.Short ? Environment.TickCount : 0;
                dispatch = true;
            }
            Log("Key " + mapping.labelOrName() + " UP hold_ms=" + holdMs + " gesture=" +
                (dispatch ? GestureLayerPolicy.Describe(kind) : "long"));
        }
        if (dispatch) DispatchGestureLayer(mapping, kind, source);
    }

    private static void FireLongGesture(object stateValue)
    {
        var request = stateValue as GestureTimerRequest;
        if (request == null) return;
        bool fire = false;
        lock (stateLock)
        {
            ShortLongGestureState state;
            if (gestureStates.TryGetValue(request.Name, out state))
                fire = state.TryFireLong(request.Generation);
            if (fire)
            {
                gestureLastTapMs[request.Name] = 0;
                gesturePressStartMs.Remove(request.Name);
            }
            DisposeGestureTimer(request.Name);
        }
        if (fire) DispatchGestureLayer(request.Mapping, GestureKind.Long, request.Source);
    }

    // Runs whatever the fired layer resolved to. A layer can carry a macro, so the sequence runs
    // in order and stops at the first step that fails; the log and the status line then report how
    // far it actually got rather than claiming the whole gesture worked.
    private static void DispatchGestureLayer(ShortcutMapping mapping, GestureKind kind, string source)
    {
        string name = mapping == null ? "unknown" : (mapping.name ?? mapping.vk ?? "unknown");
        string phase = GestureLayerPolicy.Describe(kind);
        IList<string> steps = GestureBindingStore.ResolveSteps(ToGestureEntry(mapping), kind);
        GestureRunResult result = GestureMacroRunner.RunSteps(steps,
            delegate(string step) { return ExecuteMappingAction(mapping, step, phase, source); });
        Log("Gesture " + name + " layer=" + phase + " " + GestureMacroRunner.DescribeResult(result));
        if (result.Succeeded) return;
        if (result.Error == GestureMacroRunner.NoActionCode)
        {
            Log("Gesture " + name + " layer=" + phase + " aborted=true reason=unbound");
            BridgeForm.SetStatusText(phase + "未配置动作 · " +
                (mapping == null ? "" : mapping.labelOrName()));
            return;
        }
        Log("Gesture " + name + " layer=" + phase + " aborted=true failed_step=" + (result.FailedStep + 1));
        BridgeForm.SetStatusText(phase + "中止在第 " + (result.FailedStep + 1) + " 步 · " +
            (mapping == null ? "" : mapping.labelOrName()));
    }

    // The Host ships the three layers inside the bridge's own mapping document, so rebuilding the
    // store entry shape here keeps the button-to-layer resolution in exactly one place. "none",
    // "passthrough" and blank all mean "this layer carries no action", and the shared resolver only
    // understands an empty action as unbound — normalizing first stops a deliberately disabled
    // layer from being misreported as a step that failed.
    private static GestureLayerEntry ToGestureEntry(ShortcutMapping mapping)
    {
        if (mapping == null) return null;
        // One action per layer. The macro fields the store still supports are deliberately never populated:
        // macros were removed from the product, so a stale mapping document cannot resurrect one.
        return new GestureLayerEntry
        {
            key = mapping.name,
            shortAction = NormalizeGestureAction(mapping.shortShortcut),
            longAction = NormalizeGestureAction(mapping.longShortcut),
            doubleAction = NormalizeGestureAction(mapping.doubleShortcut)
        };
    }

    private static string NormalizeGestureAction(string action)
    {
        string value = (action ?? "").Trim();
        if (value.Length == 0 || value.Equals("none", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("passthrough", StringComparison.OrdinalIgnoreCase)) return "";
        return value;
    }

    private static void DisposeGestureTimer(string name)
    {
        System.Threading.Timer timer;
        if (!gestureTimers.TryGetValue(name, out timer)) return;
        gestureTimers.Remove(name);
        try { timer.Dispose(); } catch { }
    }

    private static bool IsRepeatableHoldAction(string action)
    {
        string value = (action ?? "").Trim();
        return value.Equals("volumeup", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("volumedown", StringComparison.OrdinalIgnoreCase);
    }

    private static void StartHoldRepeat(string name, ShortcutMapping mapping)
    {
        StopHoldRepeat(name);
        int generation = holdRepeatGenerations.ContainsKey(name)
            ? holdRepeatGenerations[name] + 1 : 1;
        holdRepeatGenerations[name] = generation;
        var request = new HoldRepeatRequest { Name = name, Generation = generation, Mapping = mapping };
        holdRepeatTimers[name] = new System.Threading.Timer(RepeatHoldAction, request,
            HOLD_REPEAT_INITIAL_DELAY_MS, Timeout.Infinite);
    }

    private static void StopHoldRepeat(string name)
    {
        System.Threading.Timer timer;
        if (holdRepeatTimers.TryGetValue(name, out timer))
        {
            holdRepeatTimers.Remove(name);
            try { timer.Dispose(); } catch { }
        }
        holdRepeatGenerations[name] = holdRepeatGenerations.ContainsKey(name)
            ? holdRepeatGenerations[name] + 1 : 1;
    }

    private static void RepeatHoldAction(object stateValue)
    {
        var request = stateValue as HoldRepeatRequest;
        if (request == null) return;
        lock (stateLock)
        {
            int currentGeneration;
            bool stillHeld = sourceDown.ContainsKey(request.Name) && sourceDown[request.Name];
            if (!stillHeld || !holdRepeatGenerations.TryGetValue(request.Name, out currentGeneration) ||
                currentGeneration != request.Generation) return;
        }

        TapShortcut(request.Mapping);

        lock (stateLock)
        {
            int currentGeneration;
            System.Threading.Timer timer;
            bool stillHeld = sourceDown.ContainsKey(request.Name) && sourceDown[request.Name];
            if (!stillHeld || !holdRepeatGenerations.TryGetValue(request.Name, out currentGeneration) ||
                currentGeneration != request.Generation || !holdRepeatTimers.TryGetValue(request.Name, out timer)) return;
            try { timer.Change(HOLD_REPEAT_INTERVAL_MS, Timeout.Infinite); } catch { }
        }
    }

    private static bool ExecuteMappingAction(ShortcutMapping source, string action, string phase, string actionSource)
    {
        string normalized = (action ?? "").Trim();
        if (normalized.Length == 0 || normalized.Equals("none", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("passthrough", StringComparison.OrdinalIgnoreCase))
        {
            Log("Gesture action skipped label=" + source.labelOrName() + " phase=" + phase + " action=disabled");
            BridgeForm.SetStatusText(source.labelOrName() + " " + phase + "未执行");
            RecordActionExecution(source, phase, normalized, false, actionSource);
            return false;
        }

        var mapping = new ShortcutMapping
        {
            name = source.name,
            label = source.label,
            shortcut = normalized
        };
        bool success;
        if (normalized.Equals("task-switcher", StringComparison.OrdinalIgnoreCase))
        {
            QueueTaskSwitcherCommand("open");
            success = true;
        }
        else if (IsAiLauncherAction(normalized))
            success = LaunchAiTarget(normalized);
        else if (IsCustomAction(normalized))
            success = HandleCustomAction(mapping);
        else
            success = TapShortcut(mapping);
        Log("Gesture action executed label=" + source.labelOrName() + " phase=" + phase + " action=" + normalized +
            " success=" + success);
        RecordActionExecution(source, phase, normalized, success, actionSource);
        BridgeForm.SetStatusText(source.labelOrName() + " " + phase + " -> " + normalized);
        return success;
    }

    private static void SignalVoiceKeyPressed()
    {
        // Keep the V1.5 ordering: dispatch the physical press immediately,
        // then let the Host perform a passive focus observation. The frozen
        // Capture path can also begin from a natural ATVV packet, so a delayed
        // focus lease cannot be used as a recording gate without creating a
        // second recording state machine or dropping the first audio frame.
        bool delivered = false;
        try
        {
            delivered = voiceKeyPressedEvent != null && voiceKeyPressedEvent.Set();
            Log("Voice key signal delivered=" + delivered + " source=bridge_transition");
        }
        catch (Exception ex) { Log("Voice key signal failed: " + ex.Message); }
        SignalVoiceWakeRequested(delivered ? "capture_signal_delivered" : "capture_not_ready");
        bool hostRunning = HasRunningVoiceHostInRoot();
        if (!selfTestMode && ShouldRecoverVoiceHostAfterSignal(delivered, hostRunning))
        {
            // EventWaitHandle.Set reports that the event was signalled, not
            // that a Host is listening. Re-check the owner process so a
            // stopped Host can recover even when the named event still exists.
            Log("Voice host recovery check host_running=" + hostRunning +
                " event_signalled=" + delivered);
            EnsureVoiceHostRunning();
        }
    }

    private static bool ShouldRecoverVoiceHostAfterSignal(bool signalSet, bool hostRunning)
    {
        return !hostRunning;
    }

    private static bool SetVoiceKeyHeld(bool held)
    {
        int next = held ? 1 : 0;
        int previous = Interlocked.Exchange(ref voiceKeyHeldState, next);
        bool changed = previous != next;
        try
        {
            if (voiceKeyHeldEvent != null)
            {
                if (held) voiceKeyHeldEvent.Set();
                else voiceKeyHeldEvent.Reset();
            }
            if (changed && !held && voiceKeyReleasedEvent != null)
            {
                bool delivered = voiceKeyReleasedEvent.Set();
                Log("Voice key release signal delivered=" + delivered);
            }
        }
        catch (Exception ex) { Log("Voice key held state failed: " + ex.Message); }
        return changed;
    }

    // --- RC003 voice F5 isolation helpers (no signed device filter) ---

    // Measured on this machine: the low-level hook edge arrives before the Raw
    // Input packet, and returning 1 from the hook cancels the Raw Input packet
    // for that event. Per-event device attribution in user mode is therefore
    // impossible. The V1.5 stable contract treated F5 as the voice key and
    // suppressed it at the hook. V2 keeps that suppression but scopes it
    // strictly to moments the RC003 HID device is physically connected: only
    // then is F5 captured and driven through the voice state machine from the
    // hook. An ordinary keyboard F5 always passes through untouched while the
    // RC003 is absent, and a healthy signed filter keeps every RC003 edge out
    // of this hook entirely.
    private static void TouchRc003Present()
    {
        rc003DevicePresentUtc = DateTime.UtcNow;
    }

    private static bool Rc003PresentRecently()
    {
        return rc003DevicePresentUtc != DateTime.MinValue &&
            (DateTime.UtcNow - rc003DevicePresentUtc).TotalMilliseconds <= Rc003PresenceWindowMs;
    }

    internal static bool ShouldUseScopedHookVoice(bool filterHealthy,
        bool voiceMappingEnabled, bool mappingSuppress, bool rc003PresentRecently)
    {
        return !filterHealthy && voiceMappingEnabled && mappingSuppress &&
            rc003PresentRecently;
    }

    private static void HandleVoicePhysicalTransition(bool isDown, string source, int vk, int scan)
    {
        if (isDown)
        {
            Interlocked.Exchange(ref voiceTransitionPending, 1);
        }
        try
        {
            lock (voiceTransitionLock)
            {
                lastVoiceActivityUtc = DateTime.UtcNow;
                bool alreadyHeld = Volatile.Read(ref voiceKeyHeldState) == 1;
                if (isDown)
                {
                    if (Volatile.Read(ref voiceHoldStaleLatched) == 1)
                    {
                        // The hold was already released as stuck and its release edge was
                        // never observed. These repeats carry no evidence of a new press, so
                        // they must not re-arm a session. The watchdog clears the latch once
                        // the key has been quiet long enough to prove it was released.
                        return;
                    }
                    if (alreadyHeld)
                    {
                        // Auto-repeat DOWN edges are expected while the remote
                        // is held; the scoped hook path produces one per repeat.
                        // Log them once per hold instead of once per repeat.
                        if (lastDuplicateDownLogUtc == DateTime.MinValue ||
                            (DateTime.UtcNow - lastDuplicateDownLogUtc).TotalMilliseconds >= 2000)
                        {
                            lastDuplicateDownLogUtc = DateTime.UtcNow;
                            Log("Voice key duplicate DOWN ignored source=" + source + " vk=0x" + vk.ToString("X2") +
                                " scan=0x" + scan.ToString("X2"));
                        }
                        return;
                    }
                    double sinceReleaseMs = lastVoiceReleaseUtc == DateTime.MinValue
                        ? double.MaxValue
                        : (DateTime.UtcNow - lastVoiceReleaseUtc).TotalMilliseconds;
                    if (sinceReleaseMs < VOICE_RESTART_GUARD_MS)
                    {
                        Log("Voice key restart DOWN ignored source=" + source +
                            " elapsed_ms=" + Math.Max(0, (int)sinceReleaseMs) +
                            " guard_ms=" + VOICE_RESTART_GUARD_MS);
                        return;
                    }
                    SetVoiceKeyHeld(true);
                    // The start of the hold is what the stuck-key watchdog measures against;
                    // "time since last activity" cannot, because a repeating key never idles.
                    Interlocked.Exchange(ref voiceHoldStartedTicks, DateTime.UtcNow.Ticks);
                    Log("Key " + VoiceKeyLogLabel() + " DOWN vk=0x" + vk.ToString("X2") + " scan=0x" + scan.ToString("X2") +
                        " source=" + source);
                    SignalVoiceKeyPressed();
                    return;
                }

                if (!alreadyHeld)
                {
                    if (Interlocked.Exchange(ref voiceHoldStaleLatched, 0) == 1)
                    {
                        // The release edge finally arrived: the latch can be lifted and the
                        // next press is accepted normally.
                        Interlocked.Exchange(ref voiceHoldStartedTicks, 0);
                        lastVoiceReleaseUtc = DateTime.UtcNow;
                        Log("VOICE STUCK LATCH CLEARED reason=release_edge_seen");
                        return;
                    }
                    Log("Voice key duplicate UP ignored source=" + source + " vk=0x" + vk.ToString("X2") +
                        " scan=0x" + scan.ToString("X2"));
                    return;
                }
                SetVoiceKeyHeld(false);
                Interlocked.Exchange(ref voiceHoldStartedTicks, 0);
                lastVoiceReleaseUtc = DateTime.UtcNow;
                Log("Key " + VoiceKeyLogLabel() + " UP vk=0x" + vk.ToString("X2") + " scan=0x" + scan.ToString("X2") +
                    " source=" + source);
            }
        }
        finally
        {
            if (isDown) Interlocked.Exchange(ref voiceTransitionPending, 0);
        }
    }

    // One line per hold instead of one line per repeat. A held key repeats at the
    // keyboard rate (~30/s), and the previous unconditional log therefore wrote about
    // thirty lines a second: that consumed the whole 2 MB log in roughly ten minutes and
    // rotated away the history that makes the log worth keeping (13862 isolation lines in
    // one log plus 17486 in its predecessor, about four fifths of all recorded lines).
    // The start of a hold, a periodic repeat count, and a closing summary keep the same
    // diagnostic value at a fraction of the volume.
    private static void LogIsolationEdge(bool isDown, bool repeatEdge, int vk, int scan)
    {
        string edge = " vk=0x" + vk.ToString("X2") + " scan=0x" + scan.ToString("X2") +
            " reason=rc003_connected_no_filter";
        if (repeatEdge)
        {
            int repeats = Interlocked.Increment(ref isolationRepeatCount);
            Interlocked.Increment(ref voiceHoldRepeatsSuppressed);
            if (lastIsolationLogUtc != DateTime.MinValue &&
                (DateTime.UtcNow - lastIsolationLogUtc).TotalMilliseconds < ISOLATION_REPEAT_LOG_INTERVAL_MS)
                return;
            lastIsolationLogUtc = DateTime.UtcNow;
            Log("RC003 ISOLATION scoped_suppress=true repeat_held=true repeats=" + repeats + edge);
            return;
        }
        int held = Interlocked.Exchange(ref isolationRepeatCount, 0);
        if (held > 0)
            Log("RC003 ISOLATION hold_summary repeats=" + held + edge);
        Log("RC003 ISOLATION scoped_suppress=true " + (isDown ? "down" : "up") + edge);
    }

    private static string VoiceKeyLogLabel()
    {
        return "录音键";
    }

    // A held voice key repeats DOWN edges while physically held and normally
    // receives its UP edge from the hook. If that UP edge is lost (Bluetooth
    // blip, or a suppressed event that never reaches this state machine),
    // release the stale hold shortly after the last observed activity so the
    // next press is not rejected as a duplicate. The frozen Capture ends its
    // session on the natural ATVV stop regardless; this only restores the
    // bridge/host voice state.
    //
    // The idle test alone can never fire while the key is genuinely stuck: a repeating
    // key refreshes lastVoiceActivityUtc every ~31 ms, so "time since last activity" never
    // reaches the threshold and the hold was never released. That left the held event set
    // forever, and the frozen Capture re-arms a session from it on every ATVV reconnect
    // (RecoverHeldVoiceRequestAtReady), which is what produced a self-sustaining loop of
    // sessions that could not deliver audio: 52 recovered-at-ready sessions and 50
    // no-audio failures in one log. The second test measures the age of the hold itself,
    // which a stuck key cannot hide.
    private static void ReleaseStuckVoiceHoldIfIdle()
    {
        if (Volatile.Read(ref voiceHoldStaleLatched) == 1)
        {
            // Latched: the key is still repeating. Only a real quiet gap proves release.
            if (lastVoiceActivityUtc == DateTime.MinValue ||
                (DateTime.UtcNow - lastVoiceActivityUtc).TotalMilliseconds < VOICE_HOLD_LATCH_CLEAR_MS) return;
            Interlocked.Exchange(ref voiceHoldStaleLatched, 0);
            Interlocked.Exchange(ref voiceHoldStartedTicks, 0);
            // Deliberately NOT stamping lastVoiceReleaseUtc here: the release happened when
            // the key went quiet, at least VOICE_HOLD_LATCH_CLEAR_MS ago, not now. Stamping
            // it now would make the restart guard reject the user's next genuine press.
            Log("VOICE STUCK LATCH CLEARED reason=key_quiet");
            return;
        }
        if (Volatile.Read(ref voiceKeyHeldState) != 1) return;
        double idleMs = lastVoiceActivityUtc == DateTime.MinValue
            ? double.MaxValue
            : (DateTime.UtcNow - lastVoiceActivityUtc).TotalMilliseconds;
        long startedTicks = Interlocked.Read(ref voiceHoldStartedTicks);
        double heldMs = startedTicks == 0
            ? 0
            : (DateTime.UtcNow - new DateTime(startedTicks, DateTimeKind.Utc)).TotalMilliseconds;
        bool quiet = idleMs >= RC003_VOICE_STUCK_RELEASE_MS;
        bool stuck = heldMs >= VOICE_HOLD_STUCK_BOUND_MS;
        if (!quiet && !stuck) return;
        if (stuck)
        {
            Interlocked.Exchange(ref voiceHoldStaleLatched, 1);
            Interlocked.Increment(ref voiceHoldStaleReleaseCount);
            Log("VOICE STUCK RELEASE reason=held_past_device_bound source=watchdog held_ms=" +
                (int)heldMs + " bound_ms=" + VOICE_HOLD_STUCK_BOUND_MS);
        }
        else
        {
            Log("VOICE STUCK RELEASE reason=release_edge_lost source=watchdog idle_ms=" + (int)idleMs);
        }
        HandleVoicePhysicalTransition(false, "release_edge_lost", 0x74, 0x3F);
    }

    private static void SignalVoiceWakeRequested(string reason)
    {
        try
        {
            bool signalSet = voiceWakeRequestEvent != null && voiceWakeRequestEvent.Set();
            Log("Voice service wake event set=" + signalSet + " reason=" + reason);
        }
        catch (Exception ex) { Log("Voice service wake failed: " + ex.Message); }
    }

    private static void EnsureVoiceHostRunning()
    {
        // A previous portable copy can have the same process name. It must not
        // prevent the host that owns this bridge from being started.
        if (HasRunningVoiceHostInRoot()) return;
        string executable = Path.Combine(Root, "VibeFlow.exe");
        if (!File.Exists(executable)) executable = Path.Combine(Root, "VibeMic.exe");
        if (!File.Exists(executable))
        {
            Log("Voice host recovery unavailable: executable_missing");
            return;
        }
        try
        {
            var start = new ProcessStartInfo(executable, "--background");
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.WindowStyle = ProcessWindowStyle.Hidden;
            Process.Start(start);
            Log("Voice host recovery started");
        }
        catch (Exception ex) { Log("Voice host recovery failed: " + ex.Message); }
    }

    private static bool HasRunningVoiceHostInRoot()
    {
        string root = Path.GetFullPath(Root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string[] names = { "VibeFlow", "VibeMic" };
        foreach (string name in names)
        {
            Process[] processes = Process.GetProcessesByName(name);
            try
            {
                foreach (Process process in processes)
                {
                    try
                    {
                        string path = Path.GetFullPath(process.MainModule.FileName)
                            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        if (path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                            path.Equals(Path.Combine(root, name + ".exe"), StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                    catch { }
                }
            }
            finally { foreach (Process process in processes) process.Dispose(); }
        }
        return false;
    }

    private static bool TapShortcut(ShortcutMapping mapping)
    {
        bool down = SendShortcut(mapping, false);
        Thread.Sleep(100);
        bool up = SendShortcut(mapping, true);
        return down && up;
    }

    private static bool SendShortcut(ShortcutMapping mapping, bool keyUp)
    {
        List<int> keys = ParseShortcut(mapping.shortcut);
        if (keys.Count == 0)
        {
            Log("Shortcut empty for " + mapping.labelOrName());
            return false;
        }

        if (keyUp)
        {
            keys.Reverse();
        }

        INPUT[] inputs = new INPUT[keys.Count];
        for (int i = 0; i < keys.Count; i++)
        {
            inputs[i] = KeyInput(keys[i], keyUp, IsExtendedKey(keys[i]));
        }

        uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        int error = sent == inputs.Length ? 0 : Marshal.GetLastWin32Error();
        bool complete = sent == inputs.Length;
        if (!complete)
            RecoverPartialShortcutSend(keys, keyUp, sent, mapping.labelOrName());
        Log("SendShortcut " + (keyUp ? "UP " : "DOWN ") + mapping.labelOrName() + " " + mapping.shortcut + " " + ModeName() + " sent=" + sent + " error=" + error);
        return complete;
    }

    private static List<int> PartialShortcutRecoveryKeys(List<int> keys, bool keyUp, uint sent)
    {
        var recovery = new List<int>();
        int accepted = (int)Math.Min(sent, (uint)keys.Count);
        if (keyUp)
        {
            // Key-up input is sent in reverse order. Complete the suffix that
            // Windows did not accept on the first call.
            for (int i = accepted; i < keys.Count; i++) recovery.Add(keys[i]);
        }
        else
        {
            // Release only the keys whose key-down was actually accepted.
            for (int i = accepted - 1; i >= 0; i--) recovery.Add(keys[i]);
        }
        return recovery;
    }

    private static void RecoverPartialShortcutSend(List<int> keys, bool keyUp,
        uint sent, string label)
    {
        List<int> recovery = PartialShortcutRecoveryKeys(keys, keyUp, sent);
        if (recovery.Count == 0) return;
        INPUT[] inputs = new INPUT[recovery.Count];
        for (int i = 0; i < recovery.Count; i++)
            inputs[i] = KeyInput(recovery[i], true, IsExtendedKey(recovery[i]));
        uint recovered = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        Log("SendShortcut partial recovery " + (label ?? "") + " released=" + recovered + "/" + inputs.Length);
    }

    private static bool IsAiLauncherAction(string action)
    {
        string normalized = (action ?? "").Trim().ToLowerInvariant();
        if (normalized.StartsWith("launch-ai:", StringComparison.Ordinal)) normalized = "launch-client:" + normalized.Substring("launch-ai:".Length);
        return normalized == "launch-client:chatgpt" || normalized == "launch-client:deepseek" ||
            normalized == "launch-client:claude" || normalized == "launch-client:cursor" ||
            normalized == "launch-client:vscode" || normalized == "launch-client:codex" || normalized == "launch-client:windsurf" ||
            normalized == "launch-client:terminal";
    }

    private static bool IsCustomAction(string action)
    {
        string normalized = (action ?? "").Trim();
        return normalized.StartsWith("open-exe:", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("open-url:", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("open-app:", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("start-app:", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("shortcut:", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("snippet:", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HandleCustomAction(ShortcutMapping mapping)
    {
        string action = (mapping.shortcut ?? "").Trim();
        string label = mapping.labelOrName();
        try
        {
            // The user's own phrase packs. The text arrives in the mapping document; the phrase itself is
            // never logged, compared or stored by the Bridge, only typed.
            if (action.StartsWith("snippet:", StringComparison.OrdinalIgnoreCase))
            {
                string snippetId = action.Substring("snippet:".Length).Trim();
                string snippetText = SnippetTextFor(snippetId);
                if (snippetText.Length == 0)
                {
                    Log("Snippet action rejected label=" + label + " reason=unknown_snippet");
                    BridgeForm.SetStatusText(label + " 用语片段不存在");
                    return false;
                }
                bool typed = TypeUnicodeText(snippetText);
                // Only the length is logged, never the phrase, so the log stays metadata-only.
                Log("Snippet action label=" + label + " characters=" + snippetText.Length + " typed=" + typed);
                BridgeForm.SetStatusText(typed ? label + " 已输入用语片段" : label + " 用语片段输入失败");
                return typed;
            }

            if (action.StartsWith("open-exe:", StringComparison.OrdinalIgnoreCase))
            {
                string executable = action.Substring("open-exe:".Length).Trim();
                string processName = "";
                try { processName = Path.GetFileNameWithoutExtension(executable); } catch { }
                string focusedProcess;
                bool existingWindowFound = false;
                if (IsSafeProcessName(processName) &&
                    TryFocusClientWindow(new string[] { processName }, out focusedProcess, out existingWindowFound))
                {
                    Log("Custom app action focused label=" + label + " process=" + focusedProcess +
                        " configured_path_exists=" + File.Exists(executable));
                    BridgeForm.SetStatusText(label + " 已聚焦");
                    return true;
                }
                if (existingWindowFound)
                {
                    Log("Custom app action rejected label=" + label + " reason=existing_window_activation_failed");
                    BridgeForm.SetStatusText(label + " 窗口切换失败");
                    return false;
                }
                if (!Path.IsPathRooted(executable) ||
                    !executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                    !File.Exists(executable))
                {
                    Log("Custom app action rejected label=" + label + " reason=invalid_executable");
                    BridgeForm.SetStatusText(label + " 路径无效，未启动");
                    return false;
                }
                Process.Start(new ProcessStartInfo { FileName = executable, UseShellExecute = true });
                Log("Custom app action started label=" + label + " path=" + executable);
                BridgeForm.SetStatusText(label + " 打开完成");
                return true;
            }

            if (action.StartsWith("open-url:", StringComparison.OrdinalIgnoreCase))
            {
                Uri uri;
                string value = action.Substring("open-url:".Length).Trim();
                if (!Uri.TryCreate(value, UriKind.Absolute, out uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    Log("Custom URL action rejected label=" + label + " reason=invalid_url");
                    BridgeForm.SetStatusText(label + " 地址无效");
                    return false;
                }
                Process.Start(new ProcessStartInfo { FileName = uri.AbsoluteUri, UseShellExecute = true });
                Log("Custom URL action opened label=" + label + " scheme=" + uri.Scheme);
                BridgeForm.SetStatusText(label + " 已打开");
                return true;
            }

            if (action.StartsWith("open-app:", StringComparison.OrdinalIgnoreCase))
            {
                string[] parts = action.Substring("open-app:".Length).Split('|');
                string processName = parts.Length > 0 ? DecodeActionPart(parts[0]) : "";
                string executable = parts.Length > 1 ? DecodeActionPart(parts[1]) : "";
                string appLabel = parts.Length > 2 ? DecodeActionPart(parts[2]) : label;
                string fallbackAppId = parts.Length > 3 ? DecodeActionPart(parts[3]) : "";
                if (!IsSafeProcessName(processName))
                {
                    Log("Configured app action rejected label=" + label + " reason=invalid_process");
                    BridgeForm.SetStatusText(label + " 应用配置无效");
                    return false;
                }
                string focusedProcess;
                bool existingWindowFound = false;
                if (TryFocusClientWindow(new string[] { processName }, out focusedProcess, out existingWindowFound))
                {
                    Log("Configured app action focused label=" + label + " process=" + focusedProcess);
                    BridgeForm.SetStatusText("已切换到 " + appLabel);
                    return true;
                }
                if (existingWindowFound)
                {
                    Log("Configured app action rejected label=" + label +
                        " reason=existing_window_activation_failed process=" + processName);
                    BridgeForm.SetStatusText(appLabel + " 窗口切换失败");
                    return false;
                }
                if (!string.IsNullOrWhiteSpace(executable) && Path.IsPathRooted(executable) &&
                    executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(executable))
                {
                    Process.Start(new ProcessStartInfo { FileName = executable, UseShellExecute = true });
                    Log("Configured app action started label=" + label + " process=" + processName);
                    BridgeForm.SetStatusText("正在启动 " + appLabel);
                    return true;
                }
                string launchMode;
                if (TryLaunchStartApp(fallbackAppId, out launchMode))
                {
                    Log("Configured app action fallback_started label=" + label + " process=" + processName +
                        " mode=" + launchMode);
                    BridgeForm.SetStatusText("正在启动 " + appLabel);
                    return true;
                }
                Log("Configured app action unavailable label=" + label + " process=" + processName +
                    " executable_exists=" + File.Exists(executable) +
                    " fallback_configured=" + !string.IsNullOrWhiteSpace(fallbackAppId));
                BridgeForm.SetStatusText(appLabel + " 已启动");
                return false;
            }

            if (action.StartsWith("start-app:", StringComparison.OrdinalIgnoreCase))
            {
                string[] parts = action.Substring("start-app:".Length).Split('|');
                string appId = parts.Length > 0 ? DecodeActionPart(parts[0]) : "";
                string appLabel = parts.Length > 1 ? DecodeActionPart(parts[1]) : label;
                if (!IsSafeStartAppId(appId))
                {
                    Log("Start app action rejected label=" + label + " reason=invalid_app_id");
                    BridgeForm.SetStatusText(label + " 应用配置无效");
                    return false;
                }
                string launchMode;
                if (!TryLaunchStartApp(appId, out launchMode))
                {
                    Log("Start app action rejected label=" + label + " reason=unsupported_target");
                    BridgeForm.SetStatusText(label + " 应用入口无效");
                    return false;
                }
                Log("Start app action opened label=" + label + " mode=" + launchMode);
                BridgeForm.SetStatusText("正在打开 " + appLabel);
                return true;
            }

            string shortcut = action.Substring("shortcut:".Length).Trim();
            if (shortcut.Length == 0)
            {
                Log("Custom shortcut action rejected label=" + label + " reason=empty");
                BridgeForm.SetStatusText(label + " 未找到，请检查配置");
                return false;
            }
            ShortcutMapping customShortcut = new ShortcutMapping
            {
                name = mapping.name,
                label = label,
                shortcut = shortcut
            };
            bool sent = TapShortcut(customShortcut);
            Log("Custom shortcut action sent label=" + label + " shortcut=" + shortcut + " success=" + sent);
            BridgeForm.SetStatusText(label + " 轻触 -> " + shortcut);
            return sent;
        }
        catch (Exception ex)
        {
            Log("Custom action failed label=" + label + " error=" + ex.Message);
            BridgeForm.SetStatusText(label + " 执行失败");
            return false;
        }
    }

    private static string DecodeActionPart(string value)
    {
        try { return Encoding.UTF8.GetString(Convert.FromBase64String(value ?? "")); }
        catch { return ""; }
    }

    private static bool IsSafeProcessName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128) return false;
        foreach (char character in value)
            if (character == '\\' || character == '/' || character == ':' || character == '"' ||
                char.IsControl(character)) return false;
        return true;
    }

    private static bool IsSafeStartAppId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 2048) return false;
        foreach (char character in value)
            if (character == '"' || character == '\r' || character == '\n' || char.IsControl(character)) return false;
        return true;
    }

    private static bool TryLaunchStartApp(string appId, out string mode)
    {
        mode = "";
        if (!IsSafeStartAppId(appId)) return false;
        string value = appId.Trim();
        Uri uri;
        if (Uri.TryCreate(value, UriKind.Absolute, out uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            Process.Start(new ProcessStartInfo { FileName = uri.AbsoluteUri, UseShellExecute = true });
            mode = "url";
            return true;
        }
        if (Path.IsPathRooted(value))
        {
            if (!File.Exists(value)) return false;
            Process.Start(new ProcessStartInfo { FileName = value, UseShellExecute = true });
            mode = "executable";
            return true;
        }
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = "shell:AppsFolder\\" + value,
            UseShellExecute = true
        });
        mode = "start_app_id";
        return true;
    }

    private static bool LaunchAiTarget(string action)
    {
        string normalized = (action ?? "").Trim().ToLowerInvariant();
        string provider = normalized.Substring(normalized.IndexOf(':') + 1);
        string label;
        string[] processNames;
        string[] startAppNames;
        string[] executableNames;
        if (provider == "deepseek")
        {
            label = "DeepSeek";
            processNames = new string[] { "DeepSeek" };
            startAppNames = new string[] { "DeepSeek" };
            executableNames = new string[] { "DeepSeek.exe" };
        }
        else if (provider == "claude")
        {
            label = "Claude";
            processNames = new string[] { "Claude" };
            startAppNames = new string[] { "Claude" };
            executableNames = new string[] { "Claude.exe" };
        }
        else if (provider == "cursor")
        {
            label = "Cursor";
            processNames = new string[] { "Cursor" };
            startAppNames = new string[] { "Cursor" };
            executableNames = new string[] { "Cursor.exe" };
        }
        else if (provider == "vscode")
        {
            label = "Visual Studio Code";
            processNames = new string[] { "Code" };
            startAppNames = new string[] { "Visual Studio Code", "Visual Studio Code - Insiders" };
            executableNames = new string[] { "Code.exe", "code" };
        }
        else if (provider == "codex")
        {
            label = "Codex";
            processNames = new string[] { "Codex" };
            startAppNames = new string[] { "Codex" };
            executableNames = new string[] { "Codex.exe" };
        }
        else if (provider == "windsurf")
        {
            label = "Windsurf";
            processNames = new string[] { "Windsurf" };
            startAppNames = new string[] { "Windsurf" };
            executableNames = new string[] { "Windsurf.exe" };
        }
        else if (provider == "terminal")
        {
            label = "Windows Terminal";
            processNames = new string[] { "WindowsTerminal" };
            startAppNames = new string[] { "Terminal", "Windows Terminal" };
            executableNames = new string[] { "wt.exe", "wt" };
        }
        else
        {
            label = "ChatGPT";
            processNames = new string[] { "ChatGPT" };
            startAppNames = new string[] { "ChatGPT" };
            executableNames = new string[] { "ChatGPT.exe" };
        }

        try
        {
            string focusedProcess;
            bool existingWindowFound;
            if (TryFocusClientWindow(processNames, out focusedProcess, out existingWindowFound))
            {
                BridgeForm.SetStatusText("已切换到 " + label);
                Log("Client launcher focused target=" + provider + " process=" + focusedProcess);
                return true;
            }
            if (existingWindowFound)
            {
                BridgeForm.SetStatusText(label + " 窗口切换失败");
                Log("Client launcher unavailable target=" + provider +
                    " reason=existing_window_activation_failed");
                return false;
            }

            if (TryLaunchInstalledStartApp(startAppNames) ||
                TryLaunchStartMenuShortcut(startAppNames) ||
                TryLaunchExecutable(executableNames))
            {
                BridgeForm.SetStatusText("正在启动 " + label);
                Log("Client launcher started target=" + provider);
                return true;
            }

            BridgeForm.SetStatusText("启动失败：未找到 " + label);
            Log("Client launcher unavailable target=" + provider);
            return false;
        }
        catch (Exception ex)
        {
            BridgeForm.SetStatusText(label + " 打开失败");
            Log("Client launcher failed target=" + provider + " error=" + ex.Message);
            return false;
        }
    }

    private static bool TryFocusClientWindow(string[] processNames, out string focusedProcess)
    {
        bool ignoredWindowFound;
        return TryFocusClientWindow(processNames, out focusedProcess, out ignoredWindowFound);
    }

    private static bool TryFocusClientWindow(string[] processNames, out string focusedProcess,
        out bool existingWindowFound)
    {
        focusedProcess = "";
        existingWindowFound = false;
        foreach (string processName in processNames)
        {
            Process[] processes = Process.GetProcessesByName(processName);
            try
            {
                foreach (Process process in processes)
                {
                    process.Refresh();
                    IntPtr window = process.MainWindowHandle;
                    if (window == IntPtr.Zero) continue;
                    existingWindowFound = true;
                    string activationMode;
                    if (!TryActivateWindow(window, out activationMode))
                    {
                        Log("Client window activation failed handle=" + window + " process=" + process.ProcessName);
                        continue;
                    }
                    focusedProcess = process.ProcessName;
                    Log("Client window activation handle=" + window + " foreground=true mode=" + activationMode);
                    return true;
                }
            }
            finally { foreach (Process process in processes) process.Dispose(); }
        }
        return false;
    }

    private static bool TryActivateWindow(IntPtr window, out string mode)
    {
        mode = "";
        if (window == IntPtr.Zero) return false;
        ShowWindowAsync(window, 9);
        BringWindowToTop(window);
        SwitchToThisWindow(window, true);
        SetForegroundWindow(window);
        Thread.Sleep(80);
        if (GetForegroundWindow() == window)
        {
            mode = "direct";
            return true;
        }

        IntPtr foreground = GetForegroundWindow();
        uint foregroundThread = foreground == IntPtr.Zero ? 0 : GetWindowThreadProcessId(foreground, IntPtr.Zero);
        uint targetThread = GetWindowThreadProcessId(window, IntPtr.Zero);
        uint currentThread = GetCurrentThreadId();
        bool attachedForeground = false;
        bool attachedTarget = false;
        try
        {
            if (foregroundThread != 0 && foregroundThread != currentThread)
                attachedForeground = AttachThreadInput(currentThread, foregroundThread, true);
            if (targetThread != 0 && targetThread != currentThread)
                attachedTarget = AttachThreadInput(currentThread, targetThread, true);
            BringWindowToTop(window);
            SetForegroundWindow(window);
            SetFocus(window);
        }
        finally
        {
            if (attachedTarget) AttachThreadInput(currentThread, targetThread, false);
            if (attachedForeground) AttachThreadInput(currentThread, foregroundThread, false);
        }
        Thread.Sleep(100);
        if (GetForegroundWindow() == window)
        {
            mode = "attached_input";
            return true;
        }

        SwitchToThisWindow(window, true);
        Thread.Sleep(100);
        if (GetForegroundWindow() == window)
        {
            mode = "switch_window";
            return true;
        }
        return false;
    }

    private static bool TryLaunchInstalledStartApp(string[] applicationNames)
    {
        string[] quoted = new string[applicationNames.Length];
        for (int i = 0; i < applicationNames.Length; i++) quoted[i] = "'" + applicationNames[i].Replace("'", "''") + "'";
        string script = "$names=@(" + string.Join(",", quoted) + ");" +
            "$app=Get-StartApps|Where-Object{$names -contains $_.Name}|Select-Object -First 1;" +
            "if($null -eq $app){exit 2};Start-Process explorer.exe -ArgumentList ('shell:AppsFolder\\'+$app.AppID);exit 0";
        string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        var start = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = "-NoProfile -NonInteractive -WindowStyle Hidden -EncodedCommand " + encoded,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        using (Process process = Process.Start(start))
        {
            // Measured on a real machine, this child (PowerShell start-up + Get-StartApps +
            // the shell launch) took 4766 ms, so the old 6 s budget sat right on the edge of
            // a cold start and reported "unavailable" for an application Windows could start.
            if (process == null || !process.WaitForExit(12000)) return false;
            return process.ExitCode == 0;
        }
    }

    private static bool TryLaunchExecutable(string[] executableNames)
    {
        foreach (string executable in executableNames)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = executable, UseShellExecute = true });
                return true;
            }
            catch { }
        }
        return false;
    }

    // The third path, and the one that matches what Explorer would do. Get-StartApps only
    // answers for the names it chooses, and the executable fallback needs an App Paths
    // registration that a per-user install on a non-default drive does not have. Measured on
    // a real machine: Cursor is installed to D:\cursor\ with a working Start-menu shortcut
    // and no App Paths entry, so a cold start reported "unavailable" even though Windows
    // could start it. Resolving the shortcut's own target closes that gap.
    private static bool TryLaunchStartMenuShortcut(string[] applicationNames)
    {
        foreach (string applicationName in applicationNames)
        {
            if (string.IsNullOrWhiteSpace(applicationName)) continue;
            foreach (string shortcut in FindStartMenuShortcuts(applicationName))
            {
                string target = ResolveShortcutTarget(shortcut);
                if (string.IsNullOrWhiteSpace(target)) continue;
                try
                {
                    Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
                    Log("Client launcher start_menu_shortcut name=" +
                        Path.GetFileNameWithoutExtension(shortcut) + " target=" + Path.GetFileName(target));
                    return true;
                }
                catch (Exception ex)
                {
                    Log("Client launcher start_menu_shortcut failed name=" +
                        Path.GetFileNameWithoutExtension(shortcut) + " error=" + ex.GetType().Name);
                }
            }
        }
        return false;
    }

    // Bounded search of both start menus; the name is matched against the shortcut file name,
    // which is what the user sees in the start menu.
    private static List<string> FindStartMenuShortcuts(string applicationName)
    {
        var roots = new List<string>();
        try
        {
            roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs"));
            roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs"));
        }
        catch { }
        foreach (string root in roots)
        {
            List<string> located = FindStartMenuShortcuts(applicationName, new string[] { root });
            if (located.Count > 0) return located;
        }
        return new List<string>();
    }

    // The search takes its roots as an argument so the self-test can point it at a directory it
    // owns; the shipped call above passes the two real start menus. Without that seam the walk and
    // its name match were the one part of the launcher that no test ever executed.
    internal static List<string> FindStartMenuShortcuts(string applicationName, string[] searchRoots)
    {
        var found = new List<string>();
        if (string.IsNullOrWhiteSpace(applicationName) || searchRoots == null) return found;
        foreach (string root in searchRoots) CollectShortcuts(root, applicationName, found, 0);
        return found;
    }

    private static void CollectShortcuts(string directory, string applicationName, List<string> found, int depth)
    {
        if (depth > 4 || string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return;
        string[] shortcuts;
        try { shortcuts = Directory.GetFiles(directory, "*.lnk"); }
        catch { shortcuts = new string[0]; }
        foreach (string shortcut in shortcuts)
        {
            string name = Path.GetFileNameWithoutExtension(shortcut) ?? "";
            if (name.Equals(applicationName, StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith(applicationName + " ", StringComparison.OrdinalIgnoreCase))
                found.Add(shortcut);
        }
        string[] children;
        try { children = Directory.GetDirectories(directory); }
        catch { children = new string[0]; }
        foreach (string child in children) CollectShortcuts(child, applicationName, found, depth + 1);
    }

    // Resolved through the shell link object, the same way Explorer does it, so this stays
    // independent of any COM interop assembly.
    internal static string ResolveShortcutTarget(string shortcutPath)
    {
        object shell = null;
        object shortcut = null;
        try
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return "";
            shell = Activator.CreateInstance(shellType);
            shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell,
                new object[] { shortcutPath });
            if (shortcut == null) return "";
            object value = shortcut.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null,
                shortcut, null);
            return (value as string ?? "").Trim();
        }
        catch
        {
            return "";
        }
        finally
        {
            if (shortcut != null && Marshal.IsComObject(shortcut)) Marshal.ReleaseComObject(shortcut);
            if (shell != null && Marshal.IsComObject(shell)) Marshal.ReleaseComObject(shell);
        }
    }

    // Writes a throwaway shell link so the launcher's shortcut path can be exercised without
    // depending on which applications this machine happens to have installed.
    private static void CreateProbeShortcut(string shortcutPath, string targetPath)
    {
        object shell = null;
        try
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
                throw new InvalidOperationException("The shell link object is unavailable");
            shell = Activator.CreateInstance(shellType);
            object link = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null,
                shell, new object[] { shortcutPath });
            link.GetType().InvokeMember("TargetPath", BindingFlags.SetProperty, null, link,
                new object[] { targetPath });
            link.GetType().InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null);
        }
        finally
        {
            if (shell != null && Marshal.IsComObject(shell)) Marshal.ReleaseComObject(shell);
        }
    }

    private static bool StartMenuProbeContains(List<string> located, string shortcutPath)
    {
        foreach (string candidate in located)
            if (string.Equals(candidate, shortcutPath, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static bool TapVirtualKey(int virtualKey, string label)
    {
        INPUT[] down = new INPUT[] { KeyInput(virtualKey, false, IsExtendedKey(virtualKey)) };
        INPUT[] up = new INPUT[] { KeyInput(virtualKey, true, IsExtendedKey(virtualKey)) };
        uint sentDown = SendInput(1, down, Marshal.SizeOf(typeof(INPUT)));
        Thread.Sleep(24);
        uint sentUp = SendInput(1, up, Marshal.SizeOf(typeof(INPUT)));
        Log("Virtual key " + label + " sent=" + sentDown + "/" + sentUp);
        BridgeForm.SetStatusText(label);
        return sentDown == 1 && sentUp == 1;
    }

    private static void SetVirtualKeyState(int virtualKey, bool keyUp)
    {
        INPUT[] input = new INPUT[] { KeyInput(virtualKey, keyUp, IsExtendedKey(virtualKey)) };
        SendInput(1, input, Marshal.SizeOf(typeof(INPUT)));
    }

    private static bool HandleTaskSwitcherCommand(string command)
    {
        if (command == "open")
        {
            bool opened = TapKeyChord(0x5B, 0x09, "任务视图已打开");
            ArmTaskSwitcherTimeout();
            return opened;
        }
        if (command == "left" || command == "up" || command == "right" || command == "down")
        {
            int key = command == "left" ? 0x25 : command == "up" ? 0x26 : command == "right" ? 0x27 : 0x28;
            bool moved = TapVirtualKey(key, "任务视图选择" + command);
            ArmTaskSwitcherTimeout();
            return moved;
        }
        bool completed = TapVirtualKey(command == "confirm" ? 0x0D : 0x1B,
            command == "confirm" ? "确认选中并切换" : "取消任务切换");
        CloseTaskSwitcherState(command == "confirm" ? "已切换到所选任务" : "任务切换已取消");
        return completed;
    }

    private static bool TapKeyChord(int modifier, int key, string label)
    {
        INPUT[] inputs = new INPUT[]
        {
            KeyInput(modifier, false, IsExtendedKey(modifier)),
            KeyInput(key, false, IsExtendedKey(key)),
            KeyInput(key, true, IsExtendedKey(key)),
            KeyInput(modifier, true, IsExtendedKey(modifier))
        };
        uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        if (sent != inputs.Length)
        {
            var recovery = new List<int>();
            if (sent >= 2) recovery.Add(key);
            if (sent >= 1) recovery.Add(modifier);
            if (recovery.Count > 0)
            {
                INPUT[] release = new INPUT[recovery.Count];
                for (int i = 0; i < recovery.Count; i++)
                    release[i] = KeyInput(recovery[i], true, IsExtendedKey(recovery[i]));
                uint released = SendInput((uint)release.Length, release, Marshal.SizeOf(typeof(INPUT)));
                Log("Key chord partial recovery " + label + " released=" + released + "/" + release.Length);
            }
        }
        Log("Key chord " + label + " sent=" + sent + "/" + inputs.Length);
        BridgeForm.SetStatusText(label);
        return sent == inputs.Length;
    }

    private static void ArmTaskSwitcherTimeout()
    {
        lock (taskSwitcherLock)
        {
            if (taskSwitcherTimer != null) taskSwitcherTimer.Dispose();
            taskSwitcherTimer = new System.Threading.Timer(delegate
            {
                lock (taskSwitcherLock)
                {
                    if (!taskSwitcherActive) return;
                    taskSwitcherActive = false;
                    QueueTaskSwitcherCommand("cancel");
                }
            }, null, TASK_SWITCHER_TIMEOUT_MS, System.Threading.Timeout.Infinite);
        }
    }

    private static void CloseTaskSwitcherState(string status)
    {
        lock (taskSwitcherLock)
        {
            if (taskSwitcherTimer != null) { taskSwitcherTimer.Dispose(); taskSwitcherTimer = null; }
            taskSwitcherActive = false;
            taskSwitcherKeysDown.Clear();
        }
        BridgeForm.SetStatusText(status);
        Log(status);
    }

    private static void RegisterRawInput(IntPtr windowHandle, string reason)
    {
        RAWINPUTDEVICE[] devices = new RAWINPUTDEVICE[]
        {
            new RAWINPUTDEVICE { usUsagePage = 0x01, usUsage = 0x06, dwFlags = RIDEV_INPUTSINK | RIDEV_DEVNOTIFY, hwndTarget = windowHandle },
            new RAWINPUTDEVICE { usUsagePage = HID_USAGE_PAGE_CONSUMER, usUsage = 0x01, dwFlags = RIDEV_INPUTSINK | RIDEV_DEVNOTIFY, hwndTarget = windowHandle }
        };
        bool registered = RegisterRawInputDevices(devices, (uint)devices.Length, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE)));
        rawInputRegistered = registered;
        Log("Raw Input keyboard+consumer registered=" + registered + " reason=" +
            (reason ?? "unknown") + " error=" + (registered ? 0 : Marshal.GetLastWin32Error()));
        if (registered) LogRawInputDevices(reason ?? "register");
    }

    private static void HandleRawInput(IntPtr hRawInput)
    {
        ReloadConfigIfChanged();
        uint size = 0;
        GetRawInputData(hRawInput, RID_INPUT, IntPtr.Zero, ref size, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER)));
        if (size == 0) return;

        IntPtr buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            uint read = GetRawInputData(hRawInput, RID_INPUT, buffer, ref size, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER)));
            if (read != size) return;
            RAWINPUTHEADER header = (RAWINPUTHEADER)Marshal.PtrToStructure(buffer, typeof(RAWINPUTHEADER));
            string deviceName = GetRawDeviceName(header.hDevice);
            IntPtr data = IntPtr.Add(buffer, Marshal.SizeOf(typeof(RAWINPUTHEADER)));
            if (!IsRc003Device(deviceName))
            {
                if (header.dwType == RIM_TYPEKEYBOARD)
                {
                    RAWKEYBOARD ignored = (RAWKEYBOARD)Marshal.PtrToStructure(data, typeof(RAWKEYBOARD));
                    if (IsDiagnosticCandidate(ignored.VKey))
                    {
                        string fingerprint = ignored.VKey.ToString("X2") + "|" + (deviceName ?? "");
                        if (ignoredRawCandidateFingerprints.Add(fingerprint))
                            Log("RAW KEY DEVICE MISMATCH vk=0x" + ignored.VKey.ToString("X2") +
                                " scan=0x" + ignored.MakeCode.ToString("X2") +
                                " type=" + header.dwType + " name=" + (deviceName ?? ""));
                    }
                }
                return;
            }
            MarkRemoteInput(header.dwType == RIM_TYPEKEYBOARD ? "keyboard" : "consumer");

            if (header.dwType == RIM_TYPEKEYBOARD)
            {
                RAWKEYBOARD keyboard = (RAWKEYBOARD)Marshal.PtrToStructure(data, typeof(RAWKEYBOARD));
                bool keyUp = IsRawKeyUp(keyboard.Message);
                // Any RC003 keyboard packet proves the device is connected and
                // keeps the scoped hook voice isolation armed.
                TouchRc003Present();
                if (IsRc003FilterHealthy())
                {
                    // The signed per-device filter owns RC003 keyboard packets.
                    // Raw Input remains registered for reconnect diagnostics and
                    // consumer-control reports, but must not execute this edge.
                    return;
                }
                // Raw Input is the only user-mode API in this process that carries
                // the originating device handle. Execute RC003 actions here. The
                // low-level hook only suppresses the matching legacy event or
                // replays an unconfirmed physical-keyboard event unchanged.
                ShortcutMapping mapping = FindRc003FilterMapping(keyboard.VKey, keyboard.MakeCode);
                // Same rule as the filter path: an unassigned (disabled) power key hands the shared raw form 0xFF/0x5E
                // back to the record fallback, so a microphone that reports that way keeps working.
                if ((mapping == null || !mapping.enabled || MappingHasNoAction(mapping)) && IsVoiceRawCandidate(keyboard.VKey, keyboard.MakeCode))
                {
                    ShortcutMapping fallbackVoice = FindVoiceMapping();
                    if (fallbackVoice != null && fallbackVoice.enabled)
                    {
                        mapping = fallbackVoice;
                        Log("Voice raw VK fallback vk=0x" + keyboard.VKey.ToString("X2") +
                            " scan=0x" + keyboard.MakeCode.ToString("X2"));
                    }
                }
                bool isVoice = IsVoiceMapping(mapping) && mapping.enabled;
                if (isVoice)
                {
                    HandleVoicePhysicalTransition(!keyUp, "raw_input", keyboard.VKey, keyboard.MakeCode);
                }
                // The microphone key is managed by the voice state machine. It
                // must never be consumed by the optional custom-key learner,
                // including after a stale learning request survives a restart.
                if (!keyUp && !isVoice)
                    TryCaptureKeyboardButton(keyboard.VKey, keyboard.MakeCode);
                bool actionRouted = RouteAuthoritativeRawKeyboard(mapping,
                    keyboard.VKey, keyboard.MakeCode, keyUp);
                Log("RC003 RAW KEY " + (keyUp ? "UP" : "DOWN") +
                    " vk=0x" + keyboard.VKey.ToString("X2") + " scan=0x" + keyboard.MakeCode.ToString("X2") +
                    " flags=0x" + keyboard.Flags.ToString("X2") +
                    " action_routed=" + actionRouted);

                return;
            }

            if (header.dwType != RIM_TYPEHID) return;
            RAWHID hid = (RAWHID)Marshal.PtrToStructure(data, typeof(RAWHID));
            IntPtr reports = IntPtr.Add(data, Marshal.SizeOf(typeof(RAWHID)));
            for (uint i = 0; i < hid.dwCount; i++)
            {
                IntPtr report = IntPtr.Add(reports, checked((int)(i * hid.dwSizeHid)));
                ushort[] keyboardUsages = GetUsages(header.hDevice, report, hid.dwSizeHid, HID_USAGE_PAGE_KEYBOARD);
                ushort[] consumerUsages = GetConsumerUsages(header.hDevice, report, hid.dwSizeHid);
                Log("RC003 RAW HID size=" + hid.dwSizeHid +
                    " keyboard_usages=" + FormatUsages(keyboardUsages) +
                    " consumer_usages=" + FormatUsages(consumerUsages));
                HandleHidUsages(HID_USAGE_PAGE_KEYBOARD, keyboardUsages, keyboardUsagesDown);
                HandleConsumerUsages(consumerUsages);
            }
        }
        catch (Exception ex)
        {
            Log("Raw Input handling failed: " + ex.Message);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static void QueueRawAction(Action action)
    {
        ThreadPool.QueueUserWorkItem(delegate
        {
            try { action(); }
            catch (Exception ex) { Log("Raw action failed: " + ex.Message); }
        });
    }

    private static ushort[] GetConsumerUsages(IntPtr device, IntPtr report, uint reportLength)
    {
        return GetUsages(device, report, reportLength, HID_USAGE_PAGE_CONSUMER);
    }

    private static ushort[] GetUsages(IntPtr device, IntPtr report, uint reportLength, ushort usagePage)
    {
        uint preparsedSize = 0;
        GetRawInputDeviceInfo(device, RIDI_PREPARSEDDATA, IntPtr.Zero, ref preparsedSize);
        if (preparsedSize == 0) return new ushort[0];
        IntPtr preparsed = Marshal.AllocHGlobal((int)preparsedSize);
        try
        {
            uint received = GetRawInputDeviceInfo(device, RIDI_PREPARSEDDATA, preparsed, ref preparsedSize);
            if (received == unchecked((uint)-1)) return new ushort[0];
            HIDP_CAPS caps;
            if (HidP_GetCaps(preparsed, out caps) < 0) return new ushort[0];
            uint usageLength = Math.Max(8u, Math.Min(64u, caps.NumberInputDataIndices));
            ushort[] usages = new ushort[usageLength];
            int status = HidP_GetUsages(0, usagePage, 0, usages, ref usageLength, preparsed, report, reportLength);
            if (status < 0 || usageLength == 0) return new ushort[0];
            ushort[] result = new ushort[usageLength];
            Array.Copy(usages, result, usageLength);
            return result;
        }
        finally
        {
            Marshal.FreeHGlobal(preparsed);
        }
    }

    private static string GetRawDeviceName(IntPtr device)
    {
        uint count = 0;
        GetRawInputDeviceInfo(device, RIDI_DEVICENAME, IntPtr.Zero, ref count);
        if (count == 0) return "";
        IntPtr buffer = Marshal.AllocHGlobal(checked((int)count * 2));
        try
        {
            uint result = GetRawInputDeviceInfo(device, RIDI_DEVICENAME, buffer, ref count);
            return result == unchecked((uint)-1) ? "" : (Marshal.PtrToStringUni(buffer) ?? "");
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    private static bool IsRc003Device(string deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName)) return false;
        string value = deviceName.ToUpperInvariant();
        return (value.Contains("VID&012717") || value.Contains("VID_2717")) &&
               (value.Contains("PID&32B8") || value.Contains("PID_32B8"));
    }

    private static bool IsRawKeyUp(uint message)
    {
        return message == WM_KEYUP || message == WM_SYSKEYUP;
    }

    private static string FormatUsages(ushort[] usages)
    {
        if (usages == null || usages.Length == 0) return "none";
        string[] values = new string[usages.Length];
        for (int i = 0; i < usages.Length; i++) values[i] = "0x" + usages[i].ToString("X2");
        return string.Join(",", values);
    }

    private static void ReleaseAllShortcuts()
    {
        CloseTaskSwitcherIfActive();
        lock (stateLock)
        {
            foreach (string gestureName in new List<string>(gestureTimers.Keys)) DisposeGestureTimer(gestureName);
            foreach (string repeatName in new List<string>(holdRepeatTimers.Keys)) StopHoldRepeat(repeatName);
            foreach (ShortLongGestureState gesture in gestureStates.Values) gesture.Reset();
            sourceDown.Clear();
            var releasedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, ShortcutMapping> active in
                new List<KeyValuePair<string, ShortcutMapping>>(activeShortcutMappings))
            {
                if (active.Value != null)
                    SendShortcut(active.Value, true);
                releasedNames.Add(active.Key);
                shortcutDown[active.Key] = false;
            }
            activeShortcutMappings.Clear();

            ShortcutMapping[] configuredMappings = config == null ? null : config.mappings;
            if (configuredMappings == null)
            {
                shortcutDown.Clear();
                return;
            }

            foreach (ShortcutMapping mapping in configuredMappings)
            {
                if (mapping == null) continue;
                string name = mapping.name ?? mapping.vk ?? "unknown";
                if (!releasedNames.Contains(name) && shortcutDown.ContainsKey(name) && shortcutDown[name])
                {
                    SendShortcut(mapping, true);
                }
                shortcutDown[name] = false;
            }
            shortcutDown.Clear();
        }
    }

    private static void CloseTaskSwitcherIfActive()
    {
        bool wasActive;
        lock (taskSwitcherLock)
        {
            wasActive = taskSwitcherActive;
            if (taskSwitcherTimer != null) { taskSwitcherTimer.Dispose(); taskSwitcherTimer = null; }
            taskSwitcherActive = false;
            taskSwitcherKeysDown.Clear();
        }
        if (!wasActive) return;
        TapVirtualKey(0x1B, "关闭任务视图");
        Log("任务视图已在桥接退出时关闭");
    }

    private static string SnippetTextFor(string snippetId)
    {
        if (string.IsNullOrEmpty(snippetId) || config == null || config.snippets == null) return "";
        foreach (BridgeSnippet snippet in config.snippets)
        {
            if (snippet != null && string.Equals(snippet.id, snippetId, StringComparison.OrdinalIgnoreCase))
                return snippet.text ?? "";
        }
        return "";
    }

    // Types the user's own snippet text one character at a time with Unicode key events. Nothing goes
    // through the clipboard, so a phrase cannot land in clipboard history, cannot be captured by a
    // clipboard manager, and cannot be replayed into the wrong window by a paste race. Unicode events
    // carry the character in wScan and must not be combined with the scan-code flag.
    private static bool TypeUnicodeText(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        bool allSent = true;
        for (int index = 0; index < text.Length; index++)
        {
            char character = text[index];
            if (character == '\r') continue;
            // A newline is sent as a real Enter key so a multi-line phrase starts a new line instead of
            // injecting an invisible control character.
            if (!SendUnicodeCharacter(character == '\n' ? (char)0x0D : character)) allSent = false;
        }
        return allSent;
    }

    private static bool SendUnicodeCharacter(char character)
    {
        var inputs = new INPUT[2];
        inputs[0].type = 1;
        inputs[0].u.ki.wVk = 0;
        inputs[0].u.ki.wScan = (ushort)character;
        inputs[0].u.ki.dwFlags = KEYEVENTF_UNICODE;
        inputs[1].type = 1;
        inputs[1].u.ki.wVk = 0;
        inputs[1].u.ki.wScan = (ushort)character;
        inputs[1].u.ki.dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP;
        uint sent = SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
        if (sent != 2) Log("Snippet character input failed error=" + Marshal.GetLastWin32Error());
        return sent == 2;
    }

    private static INPUT KeyInput(int virtualKey, bool keyUp, bool extended)
    {
        INPUT input = new INPUT();
        input.type = 1;
        uint mappedScanCode = MapVirtualKey((uint)virtualKey, 0);
        if (useScanCode && mappedScanCode != 0)
        {
            input.u.ki.wVk = 0;
            input.u.ki.wScan = (ushort)mappedScanCode;
            input.u.ki.dwFlags = KEYEVENTF_SCANCODE | (keyUp ? KEYEVENTF_KEYUP : 0) | (extended ? KEYEVENTF_EXTENDEDKEY : 0);
        }
        else
        {
            input.u.ki.wVk = (ushort)virtualKey;
            input.u.ki.wScan = 0;
            input.u.ki.dwFlags = (keyUp ? KEYEVENTF_KEYUP : 0) | (extended ? KEYEVENTF_EXTENDEDKEY : 0);
        }
        input.u.ki.time = 0;
        input.u.ki.dwExtraInfo = UIntPtr.Zero;
        return input;
    }

    private static List<int> ParseShortcut(string shortcut)
    {
        List<int> keys = new List<int>();
        if (string.IsNullOrWhiteSpace(shortcut)) return keys;
        string[] parts = shortcut.Split(new char[] { '+', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts.Length > 5) return keys;
        var seen = new HashSet<int>();
        int mainKeyCount = 0;
        foreach (string raw in parts)
        {
            int vk = VkFromName(raw.Trim());
            if (vk <= 0 || vk > 0xFF || !seen.Add(vk)) return new List<int>();
            if (!IsModifierKey(vk) && ++mainKeyCount > 1) return new List<int>();
            keys.Add(vk);
        }
        bool control = keys.Contains(0xA2) || keys.Contains(0xA3);
        bool alt = keys.Contains(0xA4) || keys.Contains(0xA5);
        if (control && alt && keys.Contains(0x2E)) return new List<int>();
        return keys;
    }

    private static bool IsModifierKey(int virtualKey)
    {
        return virtualKey == 0xA0 || virtualKey == 0xA1 || virtualKey == 0xA2 || virtualKey == 0xA3 ||
            virtualKey == 0xA4 || virtualKey == 0xA5 || virtualKey == 0x5B || virtualKey == 0x5C;
    }

    private static int VkFromName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return -1;
        }

        string value = name.Trim().ToLowerInvariant();
        if (value.StartsWith("0x"))
        {
            int parsed = ParseHexOrDecimal(value);
            return parsed > 0 && parsed <= 0xFF ? parsed : -1;
        }
        if (value.Length == 1)
        {
            char ch = char.ToUpperInvariant(value[0]);
            if (ch >= 'A' && ch <= 'Z') return ch;
            if (ch >= '0' && ch <= '9') return ch;
        }
        if (value.StartsWith("f"))
        {
            int number;
            if (int.TryParse(value.Substring(1), out number) && number >= 1 && number <= 24)
            {
                return 0x70 + number - 1;
            }
        }

        Dictionary<string, int> map = new Dictionary<string, int>
        {
            {"ctrl", 0xA2}, {"control", 0xA2}, {"lctrl", 0xA2}, {"leftctrl", 0xA2},
            {"rctrl", 0xA3}, {"rightctrl", 0xA3},
            {"win", 0x5B}, {"meta", 0x5B}, {"lwin", 0x5B}, {"leftwin", 0x5B},
            {"rwin", 0x5C}, {"rightwin", 0x5C},
            {"alt", 0xA4}, {"lalt", 0xA4}, {"leftalt", 0xA4},
            {"ralt", 0xA5}, {"rightalt", 0xA5},
            {"shift", 0xA0}, {"lshift", 0xA0}, {"leftshift", 0xA0},
            {"rshift", 0xA1}, {"rightshift", 0xA1},
            {"enter", 0x0D}, {"return", 0x0D}, {"esc", 0x1B}, {"escape", 0x1B},
            {"back", 0x08}, {"backspace", 0x08}, {"tab", 0x09}, {"space", 0x20},
            {"left", 0x25}, {"up", 0x26}, {"right", 0x27}, {"down", 0x28},
            {"home", 0x24}, {"end", 0x23}, {"pageup", 0x21}, {"pagedown", 0x22},
            {"insert", 0x2D}, {"delete", 0x2E}, {"capslock", 0x14}, {"numlock", 0x90},
            {"scrolllock", 0x91}, {"printscreen", 0x2C}, {"pause", 0x13},
            {"apps", 0x5D}, {"menu", 0x5D}, {"oemtilde", 0xC0}, {"oem3", 0xC0},
            {"oemcomma", 0xBC}, {"oemperiod", 0xBE}, {"oemquestion", 0xBF},
            {"volumeup", 0xAF}, {"volumedown", 0xAE}, {"volumemute", 0xAD},
            {"browserback", 0xA6}, {"browserforward", 0xA7}, {"mediaplaypause", 0xB3}
        };
        return map.ContainsKey(value) ? map[value] : -1;
    }

    private static int ParseHexOrDecimal(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return -1;
        }
        value = value.Trim().ToLowerInvariant();
        try
        {
            if (value.StartsWith("0x"))
            {
                return Convert.ToInt32(value.Substring(2), 16);
            }
            int parsed;
            return int.TryParse(value, out parsed) ? parsed : -1;
        }
        catch
        {
            return -1;
        }
    }

    private static bool IsExtendedKey(int vk)
    {
        return vk == 0x5B || vk == 0x5C || vk == 0x5D || vk == 0xA3 || vk == 0xA5 ||
            vk == 0x25 || vk == 0x26 || vk == 0x27 || vk == 0x28 || vk == 0x21 || vk == 0x22 ||
            vk == 0x23 || vk == 0x24 || vk == 0x2D || vk == 0x2E || vk == 0x2C ||
            vk == 0xA6 || vk == 0xA7 || vk == 0xAD || vk == 0xAE || vk == 0xAF || vk == 0xB3;
    }

    private static bool IsDiagnosticCandidate(int vk)
    {
        return (vk >= 0x70 && vk <= 0x87) || vk == 0x08 || vk == 0x1B || vk == 0xC0 ||
            vk == 0xA6 || vk == 0xA7 || vk == 0xAC || vk == 0xAD || vk == 0xAE || vk == 0xAF;
    }

    private static void LoadConfig()
    {
        try
        {
            BridgeConfig loaded;
            if (!File.Exists(ConfigPath))
            {
                loaded = BridgeConfig.Default();
                config = loaded;
                SaveDefaultConfig();
            }
            else
            {
                string json = File.ReadAllText(ConfigPath);
                loaded = new JavaScriptSerializer().Deserialize<BridgeConfig>(json) ?? BridgeConfig.Default();
                config = loaded;
            }
            config.inputRoutingMode = NormalizeInputRoutingMode(config.inputRoutingMode);
            InitializeSmartProfileRuntime(config);
            if (File.Exists(ConfigPath))
            {
                configLastWriteUtc = File.GetLastWriteTimeUtc(ConfigPath);
            }
            configLoadedAtUtc = DateTime.UtcNow;
            configLoadError = "";
            int count = config.mappings == null ? 0 : config.mappings.Length;
            Log("Config loaded version=" + config.version + " revision=" + (config.revision ?? "") +
                " mappings=" + count + " routing=" + NormalizeInputRoutingMode(config.inputRoutingMode));
            RefreshRc003FilterPolicy();
            BridgeForm.SetStatusText("已加载快捷键 " + count + " 项");
        }
        catch (Exception ex)
        {
            config = BridgeConfig.Default();
            configLoadedAtUtc = DateTime.UtcNow;
            configLoadError = ex.Message;
            RefreshRc003FilterPolicy();
            Log("Config load failed: " + ex.Message);
            BridgeForm.SetStatusText("配置读取失败，已使用默认映射");
        }
    }

    private static void SaveDefaultConfig()
    {
        string json = new JavaScriptSerializer().Serialize(config);
        File.WriteAllText(ConfigPath, json);
        configLastWriteUtc = File.GetLastWriteTimeUtc(ConfigPath);
    }

    private static void ReloadConfigIfChanged()
    {
        ReloadConfig(false, "file_timestamp");
    }

    private static bool ReloadConfig(bool force, string reason)
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                if (force)
                {
                    lock (stateLock)
                    {
                        ReleaseAllShortcuts();
                        LoadConfig();
                        Log("Config reloaded from defaults reason=" + (reason ?? "unknown"));
                    }
                    return true;
                }
                return false;
            }

            DateTime lastWrite = File.GetLastWriteTimeUtc(ConfigPath);
            if (!force && lastWrite <= configLastWriteUtc)
            {
                return false;
            }

            lock (stateLock)
            {
                lastWrite = File.GetLastWriteTimeUtc(ConfigPath);
                if (!force && lastWrite <= configLastWriteUtc)
                {
                    return false;
                }
                ReleaseAllShortcuts();
                rawKeyboardEdgeTracker.Reset();
                LoadConfig();
                Log("Config hot reloaded reason=" + (reason ?? "unknown") +
                    " revision=" + (config == null ? "" : config.revision ?? ""));
            }
            return true;
        }
        catch (Exception ex)
        {
            Log("Config hot reload failed: " + ex.Message);
            configLoadError = ex.Message;
            return false;
        }
    }

    private static void InitializeSmartProfileRuntime(BridgeConfig loaded)
    {
        if (loaded == null) return;
        configuredShortcutProfileId = loaded.activeShortcutProfileId ?? "";
        configuredShortcutProfileName = loaded.activeShortcutProfileName ?? "";
        BridgeShortcutProfile configured = FindBridgeProfile(loaded, configuredShortcutProfileId);
        if (configured == null && loaded.profiles != null && loaded.profiles.Length > 0)
            configured = loaded.profiles[0];
        if (configured != null)
        {
            configuredShortcutProfileId = configured.id ?? "";
            configuredShortcutProfileName = configured.name ?? "";
            loaded.activeShortcutProfileId = configuredShortcutProfileId;
            loaded.activeShortcutProfileName = configuredShortcutProfileName;
            if (configured.mappings != null && configured.mappings.Length > 0)
                loaded.mappings = configured.mappings;
        }
        if (string.IsNullOrWhiteSpace(loaded.fallbackShortcutProfileId) ||
            FindBridgeProfile(loaded, loaded.fallbackShortcutProfileId) == null)
            loaded.fallbackShortcutProfileId = configuredShortcutProfileId;
        if (!loaded.smartProfilesEnabled) loaded.smartProfileLocked = false;
        smartProfileCandidateId = "";
        smartProfileCandidateSinceUtc = DateTime.MinValue;
        smartProfileForegroundProcess = "";
        smartProfileMatchState = loaded.smartProfilesEnabled
            ? loaded.smartProfileLocked ? "locked" : "waiting" : "manual";
    }

    private static void EvaluateSmartProfile(bool immediate)
    {
        if (Interlocked.CompareExchange(ref smartProfileEvaluationRunning, 1, 0) != 0) return;
        try
        {
            BridgeConfig snapshot = config;
            if (snapshot == null) return;
            string foreground = "";
            if (snapshot.smartProfilesEnabled && !snapshot.smartProfileLocked)
            {
                foreground = GetForegroundProcessName();
                if (IsVibeFlowProcess(foreground)) return;
            }
            string matchState;
            BridgeShortcutProfile target = ResolveSmartProfileTarget(snapshot,
                configuredShortcutProfileId, foreground, out matchState);
            if (target == null) return;
            smartProfileForegroundProcess = NormalizeProcessName(foreground);
            smartProfileMatchState = matchState;

            if (string.Equals(snapshot.activeShortcutProfileId, target.id, StringComparison.OrdinalIgnoreCase))
            {
                smartProfileCandidateId = "";
                smartProfileCandidateSinceUtc = DateTime.MinValue;
                return;
            }
            DateTime now = DateTime.UtcNow;
            if (!string.Equals(smartProfileCandidateId, target.id, StringComparison.OrdinalIgnoreCase))
            {
                smartProfileCandidateId = target.id;
                smartProfileCandidateSinceUtc = now;
                if (!immediate) return;
            }
            if (!immediate && (now - smartProfileCandidateSinceUtc).TotalMilliseconds < SMART_PROFILE_DEBOUNCE_MS)
                return;
            ActivateSmartProfile(snapshot, target, matchState, smartProfileForegroundProcess);
            smartProfileCandidateId = "";
            smartProfileCandidateSinceUtc = DateTime.MinValue;
        }
        catch (Exception ex)
        {
            Log("Smart Profile evaluation failed: " + ex.Message);
            smartProfileMatchState = "error";
        }
        finally { Interlocked.Exchange(ref smartProfileEvaluationRunning, 0); }
    }

    private static void ActivateSmartProfile(BridgeConfig source, BridgeShortcutProfile target,
        string matchState, string foregroundProcess)
    {
        if (source == null || target == null || target.mappings == null || target.mappings.Length == 0) return;
        ReleaseAllShortcuts();
        config = ProjectActiveProfile(source, target);
        RefreshRc003FilterPolicy();
        Log("Smart Profile switched profile=" + (target.id ?? "") + " state=" + (matchState ?? "") +
            " foreground=" + (foregroundProcess ?? ""));
        BridgeForm.SetStatusText("Profile 切换到 " + (target.name ?? target.id ?? "未命名配置"));
        WriteHealth("running");
    }

    // Switching a Profile changes the active mappings and nothing else. This used to be a
    // hand-written field copy, and it silently dropped the phrase table: with Smart Profiles
    // enabled — the normal case — the first switch emptied `snippets`, so a bound phrase was
    // rejected as unknown at dispatch time even though the Host had shipped it correctly. The
    // projection is therefore a named, testable function whose every field is checked in the
    // self-test, rather than a copy that has to be remembered whenever a field is added.
    private static BridgeConfig ProjectActiveProfile(BridgeConfig source, BridgeShortcutProfile target)
    {
        return new BridgeConfig
        {
            version = source.version,
            revision = source.revision,
            notes = source.notes,
            inputRoutingMode = source.inputRoutingMode,
            activeShortcutProfileId = target.id,
            activeShortcutProfileName = target.name,
            smartProfilesEnabled = source.smartProfilesEnabled,
            smartProfileLocked = source.smartProfileLocked,
            fallbackShortcutProfileId = source.fallbackShortcutProfileId,
            profiles = source.profiles,
            mappings = target.mappings,
            snippets = source.snippets
        };
    }

    private static BridgeShortcutProfile FindBridgeProfile(BridgeConfig source, string id)
    {
        if (source == null || source.profiles == null || string.IsNullOrWhiteSpace(id)) return null;
        foreach (BridgeShortcutProfile profile in source.profiles)
            if (profile != null && string.Equals(profile.id, id, StringComparison.OrdinalIgnoreCase)) return profile;
        return null;
    }

    private static BridgeShortcutProfile ResolveSmartProfileTarget(BridgeConfig source,
        string configuredId, string foregroundProcess, out string matchState)
    {
        matchState = "manual";
        if (source == null) return null;
        if (!source.smartProfilesEnabled)
            return FindBridgeProfile(source, configuredId);
        if (source.smartProfileLocked)
        {
            matchState = "locked";
            return FindBridgeProfile(source, configuredId);
        }
        BridgeShortcutProfile matched = FindBridgeProfileForProcess(source, foregroundProcess);
        if (matched != null)
        {
            matchState = "matched";
            return matched;
        }
        matchState = string.IsNullOrWhiteSpace(foregroundProcess) ? "waiting" : "fallback";
        BridgeShortcutProfile fallback = FindBridgeProfile(source, source.fallbackShortcutProfileId);
        return fallback ?? FindBridgeProfile(source, configuredId);
    }

    private static BridgeShortcutProfile FindBridgeProfileForProcess(BridgeConfig source, string processName)
    {
        string normalized = NormalizeProcessName(processName);
        if (source == null || source.profiles == null || normalized.Length == 0) return null;
        foreach (BridgeShortcutProfile profile in source.profiles)
        {
            if (profile == null || profile.processNames == null) continue;
            foreach (string candidate in profile.processNames)
                if (string.Equals(NormalizeProcessName(candidate), normalized, StringComparison.OrdinalIgnoreCase))
                    return profile;
        }
        return null;
    }

    private static string GetForegroundProcessName()
    {
        try
        {
            IntPtr window = GetForegroundWindow();
            if (window == IntPtr.Zero) return "";
            uint processId;
            GetWindowThreadProcessIdForSmartProfile(window, out processId);
            if (processId == 0) return "";
            using (Process process = Process.GetProcessById((int)processId)) return process.ProcessName;
        }
        catch { return ""; }
    }

    private static string NormalizeProcessName(string value)
    {
        string name = (value ?? "").Trim();
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            name = name.Substring(0, name.Length - 4);
        return name.ToLowerInvariant();
    }

    private static bool IsVibeFlowProcess(string value)
    {
        string process = NormalizeProcessName(value);
        return process == "vibeflow" || process == "vibemic" || process == "voxdeckinputbridge";
    }

    private static void SetMode(bool scanCode)
    {
        ReleaseAllShortcuts();
        useScanCode = scanCode;
        BridgeForm.SetStatusText("当前模式：" + ModeName());
        Log("Mode changed: " + ModeName());
    }

    private static string ModeName()
    {
        return useScanCode ? "ScanCode" : "VK";
    }

    private static void Log(string message)
    {
        try
        {
            lock (logLock)
            {
                if (File.Exists(LogPath) && new FileInfo(LogPath).Length > 2 * 1024 * 1024)
                {
                    string previous = LogPath + ".1";
                    if (File.Exists(previous)) File.Delete(previous);
                    File.Move(LogPath, previous);
                }
                File.AppendAllText(LogPath, DateTime.Now.ToString("HH:mm:ss.fff") + " " + message + Environment.NewLine);
            }
        }
        catch
        {
        }
    }

    private static void LogRawInputDevices(string reason)
    {
        uint count = 0;
        uint size = (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICELIST));
        uint result = GetRawInputDeviceList(null, ref count, size);
        if (result == unchecked((uint)-1) || count == 0)
        {
            Log("Raw Input device enumeration failed reason=" + reason + " error=" + Marshal.GetLastWin32Error());
            return;
        }

        RAWINPUTDEVICELIST[] devices = new RAWINPUTDEVICELIST[count];
        result = GetRawInputDeviceList(devices, ref count, size);
        if (result == unchecked((uint)-1))
        {
            Log("Raw Input device enumeration failed reason=" + reason + " error=" + Marshal.GetLastWin32Error());
            return;
        }

        int rc003 = 0;
        for (int i = 0; i < result; i++)
        {
            if (devices[i].dwType != RIM_TYPEKEYBOARD && devices[i].dwType != RIM_TYPEHID) continue;
            string name = GetRawDeviceName(devices[i].hDevice);
            if (!IsRc003Device(name)) continue;
            rc003++;
            Log("RC003 RAW DEVICE reason=" + reason + " type=" + devices[i].dwType +
                " name=" + name);
        }
        Log("Raw Input device enumeration reason=" + reason + " total=" + result + " rc003=" + rc003);
    }

    private static void HandleConsumerUsages(ushort[] usages)
    {
        HandleHidUsages(HID_USAGE_PAGE_CONSUMER, usages, consumerUsagesDown);
    }

    private static bool HasRc003RawInputDevice()
    {
        uint count = 0;
        uint size = (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICELIST));
        uint result = GetRawInputDeviceList(null, ref count, size);
        if (result == unchecked((uint)-1) || count == 0) return false;

        RAWINPUTDEVICELIST[] devices = new RAWINPUTDEVICELIST[count];
        result = GetRawInputDeviceList(devices, ref count, size);
        if (result == unchecked((uint)-1)) return false;
        for (int i = 0; i < result; i++)
        {
            if (devices[i].dwType != RIM_TYPEKEYBOARD && devices[i].dwType != RIM_TYPEHID) continue;
            if (IsRc003Device(GetRawDeviceName(devices[i].hDevice))) return true;
        }
        return false;
    }

    private static string GetRc003DeviceFingerprint()
    {
        uint count = 0;
        uint size = (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICELIST));
        uint result = GetRawInputDeviceList(null, ref count, size);
        if (result == unchecked((uint)-1) || count == 0) return "";

        RAWINPUTDEVICELIST[] devices = new RAWINPUTDEVICELIST[count];
        result = GetRawInputDeviceList(devices, ref count, size);
        if (result == unchecked((uint)-1)) return "";

        var matches = new List<string>();
        for (int i = 0; i < result; i++)
        {
            if (devices[i].dwType != RIM_TYPEKEYBOARD && devices[i].dwType != RIM_TYPEHID) continue;
            string name = GetRawDeviceName(devices[i].hDevice);
            if (IsRc003Device(name)) matches.Add(devices[i].dwType + ":" + name);
        }
        matches.Sort(StringComparer.OrdinalIgnoreCase);
        return string.Join("|", matches.ToArray());
    }

    private static void HandleHidUsages(int usagePage, ushort[] usages, HashSet<int> activeUsages)
    {
        ReloadConfigIfChanged();
        var current = new HashSet<int>();
        if (usages != null)
        {
            foreach (ushort usage in usages)
            {
                if (usage == 0) continue;
                int value = usage;
                current.Add(value);
                TryCaptureHidButton(usagePage, value);
                if (activeUsages.Contains(value)) continue;
                ShortcutMapping mapping = FindHidMapping(usagePage, value);
                if (mapping != null && mapping.enabled)
                {
                    activeUsages.Add(value);
                    HandleMapping(mapping, false, "raw_hid");
                }
            }
        }

        int[] previous = new int[activeUsages.Count];
        activeUsages.CopyTo(previous);
        foreach (int value in previous)
        {
            if (current.Contains(value)) continue;
            ShortcutMapping mapping = FindHidMapping(usagePage, value);
            if (mapping != null && mapping.enabled) HandleMapping(mapping, true, "raw_hid");
            activeUsages.Remove(value);
        }
    }

    private static void ReloadCustomCaptureRequest()
    {
        try
        {
            if (!File.Exists(CustomCaptureRequestPath)) return;
            DateTime lastWrite = File.GetLastWriteTimeUtc(CustomCaptureRequestPath);
            lock (customCaptureLock)
            {
                if (lastWrite <= customCaptureRequestLastWriteUtc) return;
                customCaptureRequestLastWriteUtc = lastWrite;
                customCaptureRequest = new JavaScriptSerializer().Deserialize<CustomCaptureRequest>(File.ReadAllText(CustomCaptureRequestPath, Encoding.UTF8));
                if (customCaptureRequest != null && customCaptureRequest.active &&
                    !IsCustomCaptureRequestFresh(customCaptureRequest))
                {
                    Log("Custom capture request expired; ignored stale learning request");
                    customCaptureRequest.active = false;
                    TryDeleteCustomCaptureRequest();
                }
                customCaptureConsumed = false;
                Log("Custom capture request loaded active=" + (customCaptureRequest != null && customCaptureRequest.active) +
                    " slot=" + (customCaptureRequest == null ? -1 : customCaptureRequest.slot));
            }
        }
        catch (Exception ex) { Log("Custom capture request failed: " + ex.Message); }
    }

    private static bool TryBeginCustomCapture(string sourceType, int vk, int scan, int usagePage, int usage)
    {
        ReloadCustomCaptureRequest();
        CustomCaptureRequest request;
        lock (customCaptureLock)
        {
            request = customCaptureRequest;
            if (request == null || !request.active || customCaptureConsumed || string.IsNullOrWhiteSpace(request.token)) return false;
            customCaptureConsumed = true;
        }

        try
        {
            var result = new CustomCaptureResult
            {
                token = request.token,
                slot = request.slot,
                sourceType = sourceType,
                vk = vk <= 0 ? "" : "0x" + vk.ToString("X2"),
                scan = scan <= 0 ? "" : "0x" + scan.ToString("X2"),
                usagePage = usagePage,
                usage = usage
            };
            string json = new JavaScriptSerializer().Serialize(result);
            string temp = CustomCaptureResultPath + ".tmp";
            File.WriteAllText(temp, json, Encoding.UTF8);
            if (File.Exists(CustomCaptureResultPath)) File.Delete(CustomCaptureResultPath);
            File.Move(temp, CustomCaptureResultPath);
            Log("Custom capture completed slot=" + request.slot + " source=" + sourceType +
                " vk=" + result.vk + " scan=" + result.scan + " usage=0x" + usage.ToString("X2"));
            // A capture request is one-shot. Removing it is important because
            // the bridge resets customCaptureConsumed when it is restarted.
            TryDeleteCustomCaptureRequest();
            BridgeForm.SetStatusText("已识别自定义按键 " + (request.slot + 1));
            return true;
        }
        catch (Exception ex)
        {
            lock (customCaptureLock) { customCaptureConsumed = false; }
            Log("Custom capture result failed: " + ex.Message);
            return false;
        }
    }

    private static bool IsCustomCaptureRequestFresh(CustomCaptureRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.created_at)) return false;
        DateTime created;
        if (!DateTime.TryParse(request.created_at, null,
            System.Globalization.DateTimeStyles.RoundtripKind, out created)) return false;
        if (created.Kind == DateTimeKind.Unspecified) created = DateTime.SpecifyKind(created, DateTimeKind.Utc);
        return (DateTime.UtcNow - created.ToUniversalTime()).TotalSeconds <= CUSTOM_CAPTURE_REQUEST_TIMEOUT_SECONDS;
    }

    private static void TryDeleteCustomCaptureRequest()
    {
        try { if (File.Exists(CustomCaptureRequestPath)) File.Delete(CustomCaptureRequestPath); }
        catch (Exception ex) { Log("Custom capture request cleanup failed: " + ex.Message); }
    }

    // A mapping whose every layer is "no action" must not take the key away from the record fallback.
    //
    // The host projects a key as enabled even when the user has cleared its action: after the power key's action was
    // set to "none", the projected mapping still read enabled=true with every layer empty, so the shared raw form
    // 0xFF/0x5E was intercepted and executed as "nothing" — the key was swallowed, and the record fallback, which
    // only runs for a disabled mapping, never fired. An actionless mapping is not an assignment: treat it as absent.
    private static bool MappingHasNoAction(ShortcutMapping mapping)
    {
        if (mapping == null) return true;
        return IsActionless(mapping.shortcut) && IsActionless(mapping.shortShortcut) &&
            IsActionless(mapping.longShortcut) && IsActionless(mapping.doubleShortcut);
    }

    private static bool IsActionless(string action)
    {
        if (string.IsNullOrWhiteSpace(action)) return true;
        string value = action.Trim();
        return value.Equals("none", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("passthrough", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsVoiceRawCandidate(int vk, int scan)
    {
        // RC003 reports the microphone in one of two ways, and the second one collides with the power key.
        //
        // Normally the microphone arrives translated as F5 (VK 0x74 / scan 0x3F). After some Bluetooth reconnects it has
        // arrived as VK 0xFF / scan 0x5E instead — and that is exactly what the power key reports. Measured on this
        // machine the two carry identical vk, scan and flags (0x02 down, 0x03 up), so no user-mode field separates them.
        //
        // The only discriminator left is history. While the microphone is being reported in the translated form, the
        // shared form belongs to the power key: without this, pressing the power key started and stopped dictation,
        // which is what a user reported. Once the translated form has not been seen for SharedFormVoiceWindow, the shared
        // form is treated as the microphone again so a remote that only reports that way still dictates.
        if (vk == 0x74 || vk == 0xF5)
        {
            lastTranslatedVoiceFormUtc = DateTime.UtcNow;
            return true;
        }
        if (vk == 0xFF && scan == 0x5E)
        {
            // Measured on this machine: the microphone always arrives translated as F5 (1571 F5 lines against 385 of
            // the shared form), and every shared-form press was the power key — the fallback fired 103 times and each of
            // those started a dictation the user did not ask for. A history window was tried first and was the wrong
            // shape: with a 10-minute window the shared form was still accepted while the bridge was young.
            //
            // So the shared form is the power key, full stop. If a future remote ever reports the microphone only that
            // way after a reconnect, this is the single line to change: treat it as a voice candidate again.
            Log("Voice shared form vk=0xFF scan=0x5E belongs to the power key; the translated F5 form is the microphone");
            return false;
        }
        return false;
    }

    // Kept pure so the decision can be asserted without waiting for a window to elapse: given how long ago the
    // translated form was last seen, does the shared raw form belong to the microphone?
    internal static bool SharedFormBelongsToVoice(double secondsSinceTranslatedForm)
    {
        return secondsSinceTranslatedForm >= SharedFormVoiceWindow.TotalSeconds;
    }

    private static void TryCaptureKeyboardButton(int vk, int scan)
    {
        TryBeginCustomCapture("keyboard", vk, scan, 0, 0);
    }

    private static void TryCaptureConsumerButton(int usage)
    {
        TryCaptureHidButton(HID_USAGE_PAGE_CONSUMER, usage);
    }

    private static void TryCaptureHidButton(int usagePage, int usage)
    {
        TryBeginCustomCapture(usagePage == HID_USAGE_PAGE_CONSUMER ? "consumer" : "hid",
            0, 0, usagePage, usage);
    }

    private static void ProcessCustomButtonTest()
    {
        try
        {
            if (!File.Exists(CustomTestPath)) return;
            DateTime lastWrite = File.GetLastWriteTimeUtc(CustomTestPath);
            if (lastWrite <= customTestLastWriteUtc) return;
            customTestLastWriteUtc = lastWrite;
            var request = new JavaScriptSerializer().Deserialize<CustomTestRequest>(File.ReadAllText(CustomTestPath, Encoding.UTF8));
            if (request == null) return;
            bool browserRemoteRequest = string.Equals(request.name, "browser_remote_lite_test",
                StringComparison.OrdinalIgnoreCase);
            string browserTestClaimPath = "";
            if (browserRemoteRequest && !BrowserRemoteRequestFile.TryClaim(
                CustomTestPath, request.token, out browserTestClaimPath)) return;
            if (!browserRemoteRequest) try { File.Delete(CustomTestPath); } catch { }
            if (!IsFreshCustomTestRequest(request, DateTime.UtcNow))
            {
                if (browserRemoteRequest) ReleaseBrowserTestClaim(request, browserTestClaimPath);
                WriteCustomButtonTestResult(request.token, request.action, false,
                    "测试请求已过期，请重新测试",
                    browserRemoteRequest ? "BROWSER-TEST-REQUEST-EXPIRED" :
                        "MAPPING-TEST-REQUEST-EXPIRED");
                return;
            }
            ShortcutMapping target = null;
            if (!string.IsNullOrWhiteSpace(request.action))
            {
                target = new ShortcutMapping
                {
                    name = string.IsNullOrWhiteSpace(request.name) ? "ui_mapping_test" : request.name,
                    label = string.IsNullOrWhiteSpace(request.label) ? "按键测试" : request.label,
                    enabled = true,
                    suppress = true,
                    mode = "tap",
                    shortcut = request.action
                };
            }
            else if (request.slot >= 0 && request.slot < 3 && config != null && config.mappings != null)
            {
                string name = "custom" + (request.slot + 1);
                foreach (ShortcutMapping mapping in config.mappings)
                    if (string.Equals(mapping.name, name, StringComparison.OrdinalIgnoreCase)) { target = mapping; break; }
            }
            if (target == null || !target.enabled)
            {
                if (browserRemoteRequest) ReleaseBrowserTestClaim(request, browserTestClaimPath);
                Log("Button action test ignored slot=" + request.slot + " reason=not_configured");
                WriteCustomButtonTestResult(request.token, request.action, false, "按键尚未配置有效动作");
                return;
            }
            mappingQueue.Add(new MappingEvent
            {
                mapping = target,
                testToken = request.token,
                testAction = target.shortcut,
                browserTestRequest = browserRemoteRequest ? request : null,
                browserTestClaimPath = browserRemoteRequest ? browserTestClaimPath : ""
            });
            Log("Button action test queued name=" + target.name + " action=" + target.shortcut);
            BridgeForm.SetStatusText("正在测试 " + target.labelOrName());
        }
        catch (Exception ex) { Log("Custom button test failed: " + ex.Message); }
    }

    private static void ReleaseBrowserTestClaim(CustomTestRequest request, string claimPath)
    {
        if (request == null || string.IsNullOrWhiteSpace(claimPath)) return;
        BrowserRemoteRequestFile.TryCancel(CustomTestPath, request.token);
        bool canceled;
        BrowserRemoteRequestFile.TryExecuteClaimed(claimPath, CustomTestPath,
            request.token, null, out canceled);
    }

    private static bool ExecuteBrowserRemoteTestAction(ShortcutMapping source, string action,
        CustomTestRequest request, out string errorCode)
    {
        string shortcut;
        if (!TryResolveBrowserRemoteTestShortcut(action, out shortcut))
        {
            errorCode = "BROWSER-TEST-ACTION-INVALID";
            return false;
        }
        var mapping = new ShortcutMapping
        {
            name = source == null ? "browser_remote_lite_test" : source.name,
            label = source == null ? "录音键测试" : source.label,
            shortcut = shortcut
        };
        string finalValidationError = "";
        bool canceled;
        bool success = RunBrowserRemoteTapWithVoicePriority(
            delegate { return Volatile.Read(ref voiceKeyHeldState) == 1; },
            delegate { return ValidateBrowserRemoteDispatchTargetNow(request, out finalValidationError); },
            delegate { return SendShortcut(mapping, false); },
            WaitForBrowserRemoteVoicePriority,
            delegate { return SendShortcut(mapping, true); }, out canceled);
        errorCode = canceled ? "BROWSER-TEST-CANCELED-VOICE" : finalValidationError;
        Log("Gesture action executed label=" + mapping.labelOrName() +
            " phase=测试 action=" + (action ?? "") + " success=" + success);
        RecordActionExecution(mapping, "测试", action, success, "browser_remote_lite_test");
        BridgeForm.SetStatusText(mapping.labelOrName() + " 测试 -> " + (action ?? ""));
        return success;
    }

    private static bool TryResolveBrowserRemoteTestShortcut(string action, out string shortcut)
    {
        string value = (action ?? "").Trim().ToLowerInvariant();
        if (value == "enter" || value == "pageup" || value == "pagedown" ||
            value == "browserback" || value == "tab")
        {
            shortcut = value;
            return true;
        }
        if (value == "shortcut:ctrl+r" || value == "shortcut:ctrl+l" ||
            value == "shortcut:ctrl+f" || value == "shortcut:ctrl+tab" ||
            value == "shortcut:browserforward")
        {
            shortcut = value.Substring("shortcut:".Length);
            return true;
        }
        shortcut = "";
        return false;
    }

    private static bool ValidateBrowserRemoteDispatchTargetNow(CustomTestRequest request,
        out string errorCode)
    {
        IntPtr foreground = GetForegroundWindow();
        uint processId = 0;
        string processName = "";
        if (foreground != IntPtr.Zero)
            try
            {
                GetWindowThreadProcessIdForSmartProfile(foreground, out processId);
                if (processId > 0)
                    using (Process process = Process.GetProcessById((int)processId))
                        processName = process.ProcessName;
            }
            catch { processId = 0; processName = ""; }
        if (request == null || foreground == IntPtr.Zero ||
            foreground.ToInt64() != request.expected_window_handle ||
            processId != (uint)request.expected_process_id ||
            !string.Equals(NormalizeProcessName(processName),
                NormalizeProcessName(request.expected_process_name),
                StringComparison.OrdinalIgnoreCase))
        {
            errorCode = "BROWSER-FOREGROUND-MISMATCH";
            return false;
        }
        errorCode = "";
        return true;
    }

    private static bool RunBrowserRemoteTapWithVoicePriority(Func<bool> voiceHeld,
        Func<bool> sendDown, Func<bool> waitForVoice, Func<bool> sendUp, out bool canceled)
    {
        return RunBrowserRemoteTapWithVoicePriority(voiceHeld, delegate { return true; },
            sendDown, waitForVoice, sendUp, out canceled);
    }

    private static bool RunBrowserRemoteTapWithVoicePriority(Func<bool> voiceHeld,
        Func<bool> canStart, Func<bool> sendDown, Func<bool> waitForVoice,
        Func<bool> sendUp, out bool canceled)
    {
        canceled = SafeInvoke(voiceHeld, true);
        if (canceled) return false;
        bool down = false;
        bool up = false;
        lock (voiceTransitionLock)
        {
            canceled = SafeInvoke(voiceHeld, true);
            if (canceled || !SafeInvoke(canStart, false)) return false;
            Interlocked.Exchange(ref browserRemoteTapActive, 1);
            down = SafeInvoke(sendDown, false);
            if (!down) Interlocked.Exchange(ref browserRemoteTapActive, 0);
        }
        if (down)
        {
            bool voiceArrived = SafeInvoke(waitForVoice, false) ||
                SafeInvoke(voiceHeld, true) ||
                Volatile.Read(ref voiceTransitionPending) == 1;
            if (voiceArrived) canceled = true;
        }
        try
        {
            // Key-up is independent of the voice transition lock. Recording
            // must be able to acquire the lock and signal immediately while a
            // browser tap is being released or retried.
            up = SafeInvoke(sendUp, false);
            if (!up) up = SafeInvoke(sendUp, false);
        }
        finally
        {
            Interlocked.Exchange(ref browserRemoteTapActive, 0);
        }
        return down && up && !canceled;
    }

    private static bool WaitForBrowserRemoteVoicePriority()
    {
        Stopwatch timer = Stopwatch.StartNew();
        while (timer.ElapsedMilliseconds < 100)
        {
            if (Volatile.Read(ref voiceKeyHeldState) == 1 ||
                Volatile.Read(ref voiceTransitionPending) == 1) return true;
            int remaining = Math.Max(1, 100 - (int)timer.ElapsedMilliseconds);
            try
            {
                if (voiceKeyHeldEvent != null && voiceKeyHeldEvent.WaitOne(Math.Min(10, remaining)))
                    return true;
            }
            catch { }
            Thread.Sleep(Math.Min(5, remaining));
        }
        return Volatile.Read(ref voiceKeyHeldState) == 1 ||
            Volatile.Read(ref voiceTransitionPending) == 1;
    }

    private static bool SafeInvoke(Func<bool> action, bool failureValue)
    {
        try { return action == null ? failureValue : action(); }
        catch { return failureValue; }
    }

    private static bool ValidateBrowserRemoteDispatchNow(CustomTestRequest request,
        out string errorCode)
    {
        if (!IsFreshCustomTestRequest(request, DateTime.UtcNow))
        {
            errorCode = "BROWSER-TEST-REQUEST-EXPIRED";
            return false;
        }
        IntPtr foreground = GetForegroundWindow();
        uint processId = 0;
        string processName = "";
        if (foreground != IntPtr.Zero)
        {
            try
            {
                GetWindowThreadProcessIdForSmartProfile(foreground, out processId);
                if (processId > 0)
                {
                    using (Process process = Process.GetProcessById((int)processId))
                        processName = process.ProcessName;
                }
            }
            catch { processName = ""; }
        }
        return ValidateBrowserRemoteDispatch(request,
            Volatile.Read(ref voiceKeyHeldState) == 1, foreground, (int)processId,
            processName, out errorCode);
    }

    private static bool ValidateBrowserRemoteDispatch(CustomTestRequest request, bool voiceHeld,
        IntPtr foregroundWindow, int foregroundProcessId, string foregroundProcessName,
        out string errorCode)
    {
        if (voiceHeld)
        {
            errorCode = "BROWSER-TEST-CANCELED-VOICE";
            return false;
        }
        if (request == null || request.expected_process_id <= 0 ||
            request.expected_window_handle <= 0 ||
            string.IsNullOrWhiteSpace(request.expected_process_name) ||
            foregroundWindow.ToInt64() != request.expected_window_handle ||
            foregroundProcessId != request.expected_process_id ||
            !string.Equals(NormalizeProcessName(foregroundProcessName),
                NormalizeProcessName(request.expected_process_name), StringComparison.OrdinalIgnoreCase))
        {
            errorCode = "BROWSER-FOREGROUND-MISMATCH";
            return false;
        }
        errorCode = "";
        return true;
    }

    private static bool IsFreshCustomTestRequest(CustomTestRequest request, DateTime utcNow)
    {
        DateTime createdAt;
        if (request == null || string.IsNullOrWhiteSpace(request.created_at) ||
            !DateTime.TryParse(request.created_at,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind, out createdAt)) return false;
        double ageSeconds = (utcNow.ToUniversalTime() - createdAt.ToUniversalTime()).TotalSeconds;
        return ageSeconds >= -CUSTOM_TEST_MAX_FUTURE_SKEW_SECONDS &&
            ageSeconds <= CUSTOM_TEST_MAX_AGE_SECONDS;
    }

    private static void WriteCustomButtonTestResult(string token, string action, bool success,
        string message, string errorCode = "")
    {
        if (string.IsNullOrWhiteSpace(token)) return;
        try
        {
            var result = new Dictionary<string, object>();
            result["token"] = token;
            result["action"] = action ?? "";
            result["success"] = success;
            result["message"] = message ?? "";
            result["error_code"] = errorCode ?? "";
            result["completed_at"] = DateTime.UtcNow.ToString("o");
            string temp = CustomTestResultPath + ".tmp";
            File.WriteAllText(temp, new JavaScriptSerializer().Serialize(result), Encoding.UTF8);
            if (File.Exists(CustomTestResultPath)) File.Delete(CustomTestResultPath);
            File.Move(temp, CustomTestResultPath);
            Log("Button action test completed success=" + success + " action=" + (action ?? ""));
        }
        catch (Exception ex) { Log("Button action test result failed: " + ex.Message); }
    }

    private static void RecordActionExecution(ShortcutMapping mapping, string trigger,
        string action, bool success, string source)
    {
        BridgeConfig snapshot = config;
        var receipt = new ActionExecutionReceipt
        {
            Sequence = Interlocked.Increment(ref actionReceiptSequence),
            TimestampUtc = DateTime.UtcNow,
            Button = mapping == null ? "" : mapping.name ?? "",
            Label = mapping == null ? "按键" : mapping.labelOrName(),
            Trigger = trigger ?? "单击",
            Action = action ?? "",
            Source = source ?? "unknown",
            ProfileId = snapshot == null ? "" : snapshot.activeShortcutProfileId ?? "",
            ProfileName = snapshot == null ? "" : snapshot.activeShortcutProfileName ?? "",
            ConfigRevision = snapshot == null ? "" : snapshot.revision ?? "",
            Success = success
        };
        lock (actionReceiptLock) lastExecutionReceipt = receipt;
        Log("Action receipt sequence=" + receipt.Sequence + " button=" + receipt.Label +
            " trigger=" + receipt.Trigger + " action=" + receipt.Action +
            " profile=" + receipt.ProfileId + " success=" + receipt.Success +
            " source=" + receipt.Source + " revision=" + receipt.ConfigRevision);
        WriteHealth("running");
    }

    private static void WriteHealth(string state)
    {
        try
        {
            BridgeConfig snapshot = config;
            bool devicePresent = HasRc003RawInputDevice();
            if (devicePresent) TouchRc003Present();
            var health = new Dictionary<string, object>();
            health["updated_at"] = DateTime.UtcNow.ToString("o");
            health["pid"] = Process.GetCurrentProcess().Id;
            health["state"] = state;
            health["install_root"] = Path.GetFullPath(Root);
            health["hook_installed"] = hookHandle != IntPtr.Zero;
            health["raw_input_registered"] = rawInputRegistered;
            health["raw_input_device_present"] = devicePresent;
            health["raw_input_device_state"] = devicePresent ? "ready" : "waiting";
            health["last_input_at"] = lastRemoteInputUtc == DateTime.MinValue ? "" : lastRemoteInputUtc.ToString("o");
            health["last_input_kind"] = lastRemoteInputKind ?? "";
            health["last_hook_input_at"] = lastHookInputUtc == DateTime.MinValue ? "" : lastHookInputUtc.ToString("o");
            health["last_hook_input_vk"] = lastHookInputVk;
            health["last_hook_input_scan"] = lastHookInputScan;
            health["config_version"] = snapshot == null ? 0 : snapshot.version;
            health["config_revision"] = snapshot == null ? "" : snapshot.revision ?? "";
            health["config_loaded_at"] = configLoadedAtUtc == DateTime.MinValue ? "" : configLoadedAtUtc.ToString("o");
            health["config_mapping_count"] = snapshot == null || snapshot.mappings == null ? 0 : snapshot.mappings.Length;
            health["voice_f5_isolation"] = Rc003PresentRecently() ? "rc003_scoped_hook" : "keyboard_passthrough";
            health["voice_f5_suppressed_edges"] = suppressedHookEdgeCount;
            // Published so the self-check can explain a stuck record key instead of
            // leaving the user with a remote that looks like it stopped responding.
            health["voice_hold_repeats_suppressed"] = voiceHoldRepeatsSuppressed;
            health["voice_hold_stale_releases"] = voiceHoldStaleReleaseCount;
            health["voice_hold_stale_latched"] = Volatile.Read(ref voiceHoldStaleLatched) == 1;
            health["smart_profiles_enabled"] = snapshot != null && snapshot.smartProfilesEnabled;
            health["smart_profile_locked"] = snapshot != null && snapshot.smartProfileLocked;
            health["smart_profile_configured_id"] = configuredShortcutProfileId ?? "";
            health["smart_profile_effective_id"] = snapshot == null ? "" : snapshot.activeShortcutProfileId ?? "";
            health["smart_profile_effective_name"] = snapshot == null ? "" : snapshot.activeShortcutProfileName ?? "";
            health["smart_profile_foreground_process"] = smartProfileForegroundProcess ?? "";
            health["smart_profile_match_state"] = smartProfileMatchState ?? "";
            health["input_routing_mode"] = snapshot == null
                ? "strict" : NormalizeInputRoutingMode(snapshot.inputRoutingMode);
            bool filterHealthy = IsRc003FilterHealthy();
            health["routing_authority"] = filterHealthy ? "device_filter" : "raw_input";
            health["routing_isolation"] = filterHealthy ? "exact_device" : "native_passthrough";
            health["raw_remote_edges"] = Interlocked.Read(ref rawRemoteEdgeCount);
            health["raw_action_edges"] = Interlocked.Read(ref rawActionEdgeCount);
            health["filter_action_edges"] = Interlocked.Read(ref filterActionEdgeCount);
            health["hook_candidate_passthroughs"] = Interlocked.Read(ref hookCandidatePassthroughCount);
            health["last_raw_action_at"] = lastRawActionUtc == DateTime.MinValue ? "" : lastRawActionUtc.ToString("o");
            health["last_raw_action"] = lastRawAction ?? "";
            health["last_action_source"] = lastActionSource ?? "";
            ActionExecutionReceipt receipt;
            lock (actionReceiptLock) receipt = lastExecutionReceipt;
            health["last_execution_sequence"] = receipt == null ? 0L : receipt.Sequence;
            health["last_execution_at"] = receipt == null ? "" : receipt.TimestampUtc.ToString("o");
            health["last_execution_button"] = receipt == null ? "" : receipt.Button;
            health["last_execution_label"] = receipt == null ? "" : receipt.Label;
            health["last_execution_trigger"] = receipt == null ? "" : receipt.Trigger;
            health["last_execution_action"] = receipt == null ? "" : receipt.Action;
            health["last_execution_source"] = receipt == null ? "" : receipt.Source;
            health["last_execution_profile_id"] = receipt == null ? "" : receipt.ProfileId;
            health["last_execution_profile_name"] = receipt == null ? "" : receipt.ProfileName;
            health["last_execution_revision"] = receipt == null ? "" : receipt.ConfigRevision;
            health["last_execution_success"] = receipt != null && receipt.Success;
            Rc003FilterClient filterClient = rc003FilterClient;
            health["rc003_filter_available"] = filterClient != null;
            health["rc003_filter_healthy"] = filterClient != null && filterClient.IsHealthy;
            health["rc003_filter_state"] = filterClient == null ? "not_started" : filterClient.State;
            health["rc003_filter_attached_devices"] = filterClient == null ? 0 : filterClient.AttachedDeviceCount;
            health["rc003_filter_dropped_events"] = filterClient == null ? 0L : filterClient.DroppedEventCount;
            health["config_error"] = configLoadError ?? "";
            string json = new JavaScriptSerializer().Serialize(health);
            // Keep readers from observing an empty or half-written heartbeat.
            string tempPath = HealthPath + ".tmp";
            File.WriteAllText(tempPath, json, Encoding.UTF8);
            if (File.Exists(HealthPath)) File.Replace(tempPath, HealthPath, null);
            else File.Move(tempPath, HealthPath);
        }
        catch { }
    }

    private static void MarkRemoteInput(string kind)
    {
        lastRemoteInputUtc = DateTime.UtcNow;
        lastRemoteInputKind = kind ?? "unknown";
    }

    private sealed class BridgeForm : Form
    {
        private static Label statusLabel;
        private static Label configLabel;

        private readonly bool background;

        public BridgeForm(bool launchInBackground)
        {
            background = launchInBackground;
            Text = "VoxDeck Input Bridge";
            Width = 660;
            Height = 340;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(620, 320);
            Font = new Font("Segoe UI", 10f);
            BackColor = Color.FromArgb(245, 246, 251);
            if (background)
            {
                StartPosition = FormStartPosition.Manual;
                Location = new Point(-32000, -32000);
                ShowInTaskbar = false;
            }

            var title = new Label();
            title.Text = "VoxDeck 输入法桥接";
            title.Font = new Font("Segoe UI", 18f, FontStyle.Bold);
            title.AutoSize = true;
            title.Left = 24;
            title.Top = 22;

            var description = new Label();
            description.Text = "按 voxdeck-shortcuts.json 映射遥控器键；录音键由 ATVV 语音组件独立接管。";
            description.AutoSize = false;
            description.Left = 26;
            description.Top = 66;
            description.Width = 580;
            description.Height = 44;
            description.ForeColor = Color.FromArgb(85, 95, 115);

            statusLabel = new Label();
            statusLabel.Text = "等待遥控器按键...";
            statusLabel.AutoSize = false;
            statusLabel.Left = 26;
            statusLabel.Top = 118;
            statusLabel.Width = 580;
            statusLabel.Height = 30;
            statusLabel.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            statusLabel.ForeColor = Color.FromArgb(100, 84, 232);

            configLabel = new Label();
            configLabel.Text = ConfigPath;
            configLabel.AutoSize = false;
            configLabel.Left = 26;
            configLabel.Top = 150;
            configLabel.Width = 580;
            configLabel.Height = 24;
            configLabel.ForeColor = Color.FromArgb(95, 105, 125);

            var scanMode = new RadioButton();
            scanMode.Text = "ScanCode";
            scanMode.Left = 26;
            scanMode.Top = 184;
            scanMode.Width = 100;
            scanMode.Checked = true;
            scanMode.CheckedChanged += delegate { if (scanMode.Checked) SetMode(true); };

            var vkMode = new RadioButton();
            vkMode.Text = "VK";
            vkMode.Left = 136;
            vkMode.Top = 184;
            vkMode.Width = 70;
            vkMode.CheckedChanged += delegate { if (vkMode.Checked) SetMode(false); };

            var testTap = new Button();
            testTap.Text = "测试录音快捷键";
            testTap.Left = 26;
            testTap.Top = 222;
            testTap.Width = 150;
            testTap.Height = 36;
            testTap.Click += delegate
            {
                ShortcutMapping voice = FindVoiceMapping();
                if (voice != null) TapShortcut(voice);
            };

            var reload = new Button();
            reload.Text = "重新加载";
            reload.Left = 188;
            reload.Top = 222;
            reload.Width = 110;
            reload.Height = 36;
            reload.Click += delegate { ReloadConfig(true, "ui_button"); };

            var openConfig = new Button();
            openConfig.Text = "打开配置";
            openConfig.Left = 310;
            openConfig.Top = 222;
            openConfig.Width = 110;
            openConfig.Height = 36;
            openConfig.Click += delegate { Process.Start(ConfigPath); };

            var panic = new Button();
            panic.Text = "释放所有";
            panic.Left = 432;
            panic.Top = 222;
            panic.Width = 100;
            panic.Height = 36;
            panic.Click += delegate { ReleaseAllShortcuts(); };

            var close = new Button();
            close.Text = "关闭";
            close.Left = 26;
            close.Top = 270;
            close.Width = 120;
            close.Height = 36;
            close.Click += delegate { Close(); };

            Controls.Add(title);
            Controls.Add(description);
            Controls.Add(statusLabel);
            Controls.Add(configLabel);
            Controls.Add(scanMode);
            Controls.Add(vkMode);
            Controls.Add(testTap);
            Controls.Add(reload);
            Controls.Add(openConfig);
            Controls.Add(panic);
            Controls.Add(close);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (background) Hide();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            RegisterRawInput(Handle, "handle_created");
            knownRc003DeviceFingerprint = GetRc003DeviceFingerprint();
            // Some Bluetooth LE HID reconnects replace the device handle without
            // delivering a usable WM_INPUT_DEVICE_CHANGE notification. Rebind
            // at a low frequency so the bridge heals itself while idle.
            rawInputHealthTimer = new System.Windows.Forms.Timer();
            // 5 s, not 30 s. The hook's scoped record-key suppression treats the RC003 as present only for
            // Rc003PresenceWindowMs after the last sign of it, and this probe is the only thing that refreshes that
            // sign while the remote is idle. At a 30 s interval the presence had usually expired again by the time a
            // user pressed the record key, so the *first* press of a session — the one that starts dictation — went
            // unsuppressed and the foreground application received F5 as well. The probe now runs inside the window it
            // feeds. Measured on this machine before the change: the ISOLATION lines only ever showed
            // scoped_suppress=true for auto-repeat edges, never for the first press.
            int healthIntervalMs = Rc003PresenceWindowMs;
            rawInputHealthTimer.Interval = healthIntervalMs;
            DateTime lastHealthLogUtc = DateTime.MinValue;
            rawInputHealthTimer.Tick += delegate
            {
                if (IsDisposed || !IsHandleCreated) return;
                // RegisterRawInputDevices is not idempotent on every Windows
                // Bluetooth HID stack: repeating it can emit synthetic device
                // change notifications and briefly disturb an otherwise live
                // RC003 input route. Rebind only when the registration or the
                // device presence check actually says it is needed.
                bool devicePresent = HasRc003RawInputDevice();
                if (devicePresent) rawInputDeviceMisses = 0;
                else rawInputDeviceMisses++;
                // BLE enumeration can be transiently empty during reconnect.
                // Re-register only after consecutive misses; registering on
                // every transient miss creates a device-change burst itself.
                if (!rawInputRegistered || rawInputDeviceMisses >= 3)
                {
                    Log("Raw Input health rebind requested registered=" + rawInputRegistered +
                        " device_present=" + devicePresent + " misses=" + rawInputDeviceMisses);
                    rawInputDeviceMisses = 0;
                    RegisterRawInput(Handle, "periodic_health_rebind");
                }
                else if ((DateTime.UtcNow - lastHealthLogUtc).TotalSeconds >= 30)
                {
                    // The probe runs every few seconds now, so the line is rate-limited to keep the log readable.
                    lastHealthLogUtc = DateTime.UtcNow;
                    Log("Raw Input health check ok device_present=true");
                }
            };
            // One line at startup, so the interval that feeds the suppression window is visible in any log.
            Log("Raw Input health timer interval_ms=" + healthIntervalMs +
                " presence_window_ms=" + Rc003PresenceWindowMs);
            rawInputHealthTimer.Start();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_INPUT) HandleRawInput(m.LParam);
            else if (m.Msg == WM_INPUT_DEVICE_CHANGE)
            {
                string changedDeviceName = GetRawDeviceName(m.LParam);
                bool rc003Change = IsRc003Device(changedDeviceName);
                if (!rc003Change && string.IsNullOrWhiteSpace(changedDeviceName))
                {
                    string currentFingerprint = GetRc003DeviceFingerprint();
                    rc003Change = changeTypeIsArrival(m.WParam) ? !string.IsNullOrWhiteSpace(currentFingerprint) :
                        !string.IsNullOrWhiteSpace(knownRc003DeviceFingerprint);
                }
                if (!rc003Change)
                {
                    base.WndProc(ref m);
                    return;
                }
                consumerUsagesDown.Clear();
                keyboardUsagesDown.Clear();
                rawKeyboardEdgeTracker.Reset();
                long changeType = m.WParam.ToInt64();
                // A Bluetooth reconnect (arrival or removal) can swallow the
                // release edges of gestures that were in flight. Release every
                // held shortcut before the next reconnect generation takes
                // ownership, so injected modifiers can never stay stuck.
                ReleaseAllShortcuts();
                if (changeType == 2 && Volatile.Read(ref voiceKeyHeldState) == 1)
                    HandleVoicePhysicalTransition(false, "raw_device_removed", 0, 0);
                if (changeType == 2)
                {
                    // Expire the scoped hook-voice lease: with the RC003 gone,
                    // an ordinary keyboard F5 must pass through untouched.
                    rc003DevicePresentUtc = DateTime.MinValue;
                }
                DateTime now = DateTime.UtcNow;
                bool shouldLogDeviceChange = (now - lastRawInputDeviceChangeLogUtc).TotalMilliseconds >= 500;
                if (shouldLogDeviceChange)
                {
                    lastRawInputDeviceChangeLogUtc = now;
                    Log("Raw Input device change wParam=0x" + changeType.ToString("X"));
                }
                if (shouldLogDeviceChange) LogRawInputDevices("device_change");
                // Windows normally keeps usage registration across a HID handle
                // replacement. RegisterRawInputDevices is process/usage scoped,
                // not device-handle scoped. Re-register only for the first
                // startup notification or a real RC003 device fingerprint change;
                // registration itself can emit a duplicate arrival notification.
                if (changeType == 2)
                {
                    knownRc003DeviceFingerprint = "";
                    ScheduleRawInputRebind(changeType);
                }
                else if (changeType == 1)
                {
                    string fingerprint = GetRc003DeviceFingerprint();
                    bool startupArrival = !startupRawInputRebindCompleted &&
                        bridgeStartedUtc != DateTime.MinValue &&
                        (DateTime.UtcNow - bridgeStartedUtc).TotalSeconds <= 12;
                    bool deviceChanged = !string.Equals(fingerprint, knownRc003DeviceFingerprint,
                        StringComparison.Ordinal);
                    if (startupArrival || deviceChanged)
                    {
                        knownRc003DeviceFingerprint = fingerprint;
                        ScheduleRawInputRebind(changeType);
                    }
                    else if (shouldLogDeviceChange)
                    {
                        Log("Raw Input arrival ignored reason=already_registered");
                    }
                }
            }
            base.WndProc(ref m);
        }

        private static bool changeTypeIsArrival(IntPtr value)
        {
            return value.ToInt64() == 1;
        }

        private System.Windows.Forms.Timer rawInputRebindTimer;
        private System.Windows.Forms.Timer rawInputHealthTimer;
        private DateTime lastRawInputRebindUtc = DateTime.MinValue;

        private void ScheduleRawInputRebind(long changeType)
        {
            if (changeType != 1 && changeType != 2) return;
            if (changeType == 1)
            {
                // Registering the usage can produce one arrival notification for
                // the already-connected RC003. A single delayed rebind lets the
                // Bluetooth HID stack finish attaching without creating a
                // repeated RegisterRawInputDevices loop during normal use. The
                // caller also uses the device fingerprint to admit late arrivals
                // after Bluetooth services take longer than the startup window.
                startupRawInputRebindCompleted = true;
            }
            if (rawInputRebindTimer != null) return;
            rawInputRebindTimer = new System.Windows.Forms.Timer();
            rawInputRebindTimer.Interval = 350;
            rawInputRebindTimer.Tick += delegate
            {
                rawInputRebindTimer.Stop();
                rawInputRebindTimer.Dispose();
                rawInputRebindTimer = null;
                if ((DateTime.UtcNow - lastRawInputRebindUtc).TotalMilliseconds < 1000)
                {
                    Log("Raw Input rebind skipped reason=debounced");
                    return;
                }
                lastRawInputRebindUtc = DateTime.UtcNow;
                RegisterRawInput(Handle, "device_change_rebind");
                knownRc003DeviceFingerprint = GetRc003DeviceFingerprint();
                if (changeType == 1) ReinstallKeyboardHook("startup_device_arrival");
            };
            rawInputRebindTimer.Start();
            Log("Raw Input rebind scheduled change=0x" + changeType.ToString("X"));
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (rawInputHealthTimer != null)
            {
                rawInputHealthTimer.Stop();
                rawInputHealthTimer.Dispose();
                rawInputHealthTimer = null;
            }
            if (rawInputRebindTimer != null)
            {
                rawInputRebindTimer.Stop();
                rawInputRebindTimer.Dispose();
                rawInputRebindTimer = null;
            }
            base.OnFormClosed(e);
        }

        public static void SetStatusText(string text)
        {
            if (statusLabel == null) return;
            if (statusLabel.InvokeRequired)
            {
                statusLabel.BeginInvoke(new Action<string>(SetStatusText), text);
                return;
            }
            statusLabel.Text = text + " · " + DateTime.Now.ToString("HH:mm:ss");
        }
    }

    private static ShortcutMapping FindVoiceMapping()
    {
        if (config == null || config.mappings == null) return null;
        foreach (ShortcutMapping mapping in config.mappings)
        {
            if ((mapping.name ?? "").Equals("voice", StringComparison.OrdinalIgnoreCase))
            {
                return mapping;
            }
        }
        return null;
    }

    private sealed class Rc003FilterKeyEvent
    {
        public ulong Sequence;
        public ushort MakeCode;
        public ushort Flags;
        public ushort UnitId;
        public int Generation;
        public bool Suppressed;
    }

    private static class Rc003FilterProtocol
    {
        public const uint Magic = 0x43524656U;
        public const uint ApiVersion = 1U;
        public const int ScanCodeCount = 256;
        public const int InfoSize = 40;
        public const int PolicySize = 272;
        public const int EventSize = 32;
        public const int EventBatchHeaderSize = 24;
        public const ushort KeyBreak = 0x0001;
        public const ushort KeyE0 = 0x0002;
        public const ushort KeyE1 = 0x0004;

        public const uint IoctlGetInfo = 0x80006400U;
        public const uint IoctlSetPolicy = 0x8000A404U;
        public const uint IoctlHeartbeat = 0x8000A408U;
        public const uint IoctlReadEvents = 0x8000640CU;
        public const uint IoctlDisarm = 0x8000A410U;

        public static byte[] BuildPolicy(byte[] suppressionMask)
        {
            byte[] policy = new byte[PolicySize];
            WriteUInt32(policy, 0, Magic);
            WriteUInt32(policy, 4, ApiVersion);
            WriteUInt32(policy, 8, PolicySize);
            WriteUInt32(policy, 12, 1U);
            if (suppressionMask != null)
                Buffer.BlockCopy(suppressionMask, 0, policy, 16,
                    Math.Min(ScanCodeCount, suppressionMask.Length));
            return policy;
        }

        public static void ParseInfo(byte[] buffer, int bytesReturned,
            out int attachedDeviceCount, out long droppedEventCount)
        {
            if (buffer == null || bytesReturned < InfoSize || buffer.Length < InfoSize)
                throw new InvalidDataException("RC003 filter returned a short info structure");
            if (ReadUInt32(buffer, 0) != Magic || ReadUInt32(buffer, 4) != ApiVersion ||
                ReadUInt32(buffer, 8) != InfoSize)
                throw new InvalidDataException("RC003 filter info protocol mismatch");
            attachedDeviceCount = checked((int)ReadUInt32(buffer, 12));
            droppedEventCount = checked((long)ReadUInt64(buffer, 32));
        }

        public static List<Rc003FilterKeyEvent> ParseEvents(byte[] buffer, int bytesReturned,
            int generation, byte[] suppressionMask, out long droppedEventCount)
        {
            if (buffer == null || bytesReturned < EventBatchHeaderSize ||
                buffer.Length < bytesReturned)
                throw new InvalidDataException("RC003 filter returned a short event batch");
            if (ReadUInt32(buffer, 0) != Magic || ReadUInt32(buffer, 4) != ApiVersion ||
                ReadUInt32(buffer, 8) != EventBatchHeaderSize)
                throw new InvalidDataException("RC003 filter event protocol mismatch");

            uint eventCount = ReadUInt32(buffer, 12);
            droppedEventCount = checked((long)ReadUInt64(buffer, 16));
            long required = EventBatchHeaderSize + ((long)eventCount * EventSize);
            if (required > bytesReturned)
                throw new InvalidDataException("RC003 filter event batch length mismatch");

            var events = new List<Rc003FilterKeyEvent>(checked((int)eventCount));
            for (int index = 0; index < eventCount; index++)
            {
                int offset = EventBatchHeaderSize + (index * EventSize);
                if (ReadUInt32(buffer, offset) != Magic || ReadUInt32(buffer, offset + 4) != EventSize)
                    throw new InvalidDataException("RC003 filter event structure mismatch");
                ushort makeCode = ReadUInt16(buffer, offset + 24);
                events.Add(new Rc003FilterKeyEvent
                {
                    Sequence = ReadUInt64(buffer, offset + 8),
                    MakeCode = makeCode,
                    Flags = ReadUInt16(buffer, offset + 26),
                    UnitId = ReadUInt16(buffer, offset + 28),
                    Generation = generation,
                    Suppressed = suppressionMask != null && makeCode < suppressionMask.Length &&
                        suppressionMask[makeCode] != 0
                });
            }
            return events;
        }

        private static ushort ReadUInt16(byte[] buffer, int offset)
        {
            return BitConverter.ToUInt16(buffer, offset);
        }

        private static uint ReadUInt32(byte[] buffer, int offset)
        {
            return BitConverter.ToUInt32(buffer, offset);
        }

        private static ulong ReadUInt64(byte[] buffer, int offset)
        {
            return BitConverter.ToUInt64(buffer, offset);
        }

        private static void WriteUInt32(byte[] buffer, int offset, uint value)
        {
            byte[] encoded = BitConverter.GetBytes(value);
            Buffer.BlockCopy(encoded, 0, buffer, offset, encoded.Length);
        }
    }

    private sealed class Rc003FilterClient : IDisposable
    {
        private const string DevicePath = "\\\\.\\VibeFlowRc003Filter";
        private const uint GenericRead = 0x80000000U;
        private const uint GenericWrite = 0x40000000U;
        private const uint OpenExisting = 3U;
        private static readonly IntPtr InvalidHandleValue = new IntPtr(-1);

        private readonly Action<Rc003FilterKeyEvent> eventCallback;
        private readonly Action<bool, string> healthCallback;
        private readonly object policyLock = new object();
        private readonly object stateTextLock = new object();
        private readonly AutoResetEvent wakeEvent = new AutoResetEvent(false);
        private readonly ManualResetEvent stopEvent = new ManualResetEvent(false);
        private readonly BlockingCollection<Rc003FilterKeyEvent> dispatchQueue =
            new BlockingCollection<Rc003FilterKeyEvent>(256);
        private readonly byte[] eventReadBuffer = new byte[
            Rc003FilterProtocol.EventBatchHeaderSize + (Rc003FilterProtocol.EventSize * 64)];

        private Thread ioThread;
        private Thread dispatchThread;
        private IntPtr deviceHandle = InvalidHandleValue;
        private byte[] requestedMask = new byte[Rc003FilterProtocol.ScanCodeCount];
        private byte[] activeMask = new byte[Rc003FilterProtocol.ScanCodeCount];
        private int requestedGeneration = 1;
        private int activeGeneration;
        private int healthy;
        private int attachedDeviceCount;
        private long droppedEventCount;
        private string state = "not_started";
        private string lastNotifiedState = "";
        private int lastHeartbeatTick;

        public Rc003FilterClient(Action<Rc003FilterKeyEvent> onEvent,
            Action<bool, string> onHealthChanged)
        {
            eventCallback = onEvent;
            healthCallback = onHealthChanged;
        }

        public bool IsHealthy { get { return Volatile.Read(ref healthy) == 1; } }
        public int AttachedDeviceCount { get { return Volatile.Read(ref attachedDeviceCount); } }
        public long DroppedEventCount { get { return Interlocked.Read(ref droppedEventCount); } }
        public string State
        {
            get { lock (stateTextLock) return state; }
        }

        public void Start()
        {
            if (ioThread != null) return;
            dispatchThread = new Thread(DispatchLoop);
            dispatchThread.IsBackground = true;
            dispatchThread.Name = "Vibe Link RC003 filter dispatch";
            dispatchThread.Start();

            ioThread = new Thread(IoLoop);
            ioThread.IsBackground = true;
            ioThread.Name = "Vibe Link RC003 filter heartbeat";
            ioThread.Start();
        }

        public void UpdatePolicy(byte[] suppressionMask)
        {
            byte[] normalized = new byte[Rc003FilterProtocol.ScanCodeCount];
            if (suppressionMask != null)
                Buffer.BlockCopy(suppressionMask, 0, normalized, 0,
                    Math.Min(normalized.Length, suppressionMask.Length));
            lock (policyLock)
            {
                requestedMask = normalized;
                requestedGeneration++;
                if (requestedGeneration <= 0) requestedGeneration = 1;
                // Invalidate queued events immediately. Until the I/O thread
                // arms the new generation, dropping an edge is safer than
                // executing it against a configuration that no longer owns
                // that policy.
                Volatile.Write(ref activeGeneration, 0);
            }
            wakeEvent.Set();
        }

        public bool IsGenerationActive(int generation)
        {
            return generation > 0 && IsHealthy && generation == Volatile.Read(ref activeGeneration);
        }

        private void IoLoop()
        {
            while (!stopEvent.WaitOne(0))
            {
                try
                {
                    if (!HasOpenHandle()) OpenAndValidate();
                    ApplyPendingPolicy();
                    if (unchecked((uint)(Environment.TickCount - lastHeartbeatTick)) >= 250U)
                    {
                        InvokeIoControl(Rc003FilterProtocol.IoctlHeartbeat, null, null);
                        lastHeartbeatTick = Environment.TickCount;
                    }
                    ReadAvailableEvents();
                    wakeEvent.WaitOne(10);
                }
                catch (Exception ex)
                {
                    bool hadOpenDevice = HasOpenHandle();
                    TryDisarm();
                    CloseDeviceHandle();
                    SetHealth(false, "fallback:" + ex.Message);
                    // A resume-time heartbeat timeout is recoverable and
                    // should re-arm quickly. A missing driver uses a slower
                    // retry to avoid needless background wakeups.
                    wakeEvent.WaitOne(hadOpenDevice ? 100 : 750);
                }
            }

            TryDisarm();
            CloseDeviceHandle();
            SetHealth(false, "stopped");
        }

        private void DispatchLoop()
        {
            foreach (Rc003FilterKeyEvent input in dispatchQueue.GetConsumingEnumerable())
            {
                if (!IsGenerationActive(input.Generation)) continue;
                try
                {
                    if (eventCallback != null) eventCallback(input);
                }
                catch (Exception ex) { Log("RC003 filter dispatch failed: " + ex.Message); }
            }
        }

        private void OpenAndValidate()
        {
            IntPtr opened = CreateFile(DevicePath, GenericRead | GenericWrite, 0,
                IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
            if (opened == InvalidHandleValue)
                throw new IOException("open_failed_win32_" + Marshal.GetLastWin32Error());
            deviceHandle = opened;

            byte[] info = new byte[Rc003FilterProtocol.InfoSize];
            int bytesReturned = InvokeIoControl(Rc003FilterProtocol.IoctlGetInfo, null, info);
            int attached;
            long dropped;
            Rc003FilterProtocol.ParseInfo(info, bytesReturned, out attached, out dropped);
            if (attached < 1) throw new IOException("filter_loaded_without_rc003_device");
            Volatile.Write(ref attachedDeviceCount, attached);
            Interlocked.Exchange(ref droppedEventCount, dropped);
            Volatile.Write(ref activeGeneration, 0);
            lastHeartbeatTick = Environment.TickCount;
        }

        private void ApplyPendingPolicy()
        {
            int generation;
            byte[] mask;
            lock (policyLock)
            {
                generation = requestedGeneration;
                if (generation == Volatile.Read(ref activeGeneration)) return;
                mask = (byte[])requestedMask.Clone();
            }

            byte[] policy = Rc003FilterProtocol.BuildPolicy(mask);
            InvokeIoControl(Rc003FilterProtocol.IoctlSetPolicy, policy, null);
            lock (policyLock)
            {
                if (generation != requestedGeneration)
                {
                    Volatile.Write(ref activeGeneration, 0);
                    return;
                }
                activeMask = mask;
                Volatile.Write(ref activeGeneration, generation);
            }
            lastHeartbeatTick = Environment.TickCount;
            SetHealth(true, "ready:generation_" + generation);
        }

        private void ReadAvailableEvents()
        {
            int bytesReturned = InvokeIoControl(Rc003FilterProtocol.IoctlReadEvents, null, eventReadBuffer);
            int generation = Volatile.Read(ref activeGeneration);
            long dropped;
            List<Rc003FilterKeyEvent> events = Rc003FilterProtocol.ParseEvents(
                eventReadBuffer, bytesReturned, generation, activeMask, out dropped);
            long previousDropped = Interlocked.Exchange(ref droppedEventCount, dropped);
            if (dropped > previousDropped)
                Log("RC003 filter queue dropped events total=" + dropped);
            foreach (Rc003FilterKeyEvent input in events)
            {
                if (!dispatchQueue.TryAdd(input))
                    throw new IOException("user_mode_event_queue_full");
            }
        }

        private int InvokeIoControl(uint controlCode, byte[] input, byte[] output)
        {
            if (!HasOpenHandle()) throw new IOException("filter_handle_closed");
            int bytesReturned;
            bool success = DeviceIoControl(deviceHandle, controlCode,
                input, input == null ? 0 : input.Length,
                output, output == null ? 0 : output.Length,
                out bytesReturned, IntPtr.Zero);
            if (!success)
                throw new IOException("ioctl_0x" + controlCode.ToString("X8") +
                    "_failed_win32_" + Marshal.GetLastWin32Error());
            return bytesReturned;
        }

        private void TryDisarm()
        {
            if (!HasOpenHandle()) return;
            try { InvokeIoControl(Rc003FilterProtocol.IoctlDisarm, null, null); }
            catch { }
        }

        private bool HasOpenHandle()
        {
            return deviceHandle != IntPtr.Zero && deviceHandle != InvalidHandleValue;
        }

        private void CloseDeviceHandle()
        {
            IntPtr current = deviceHandle;
            deviceHandle = InvalidHandleValue;
            if (current != IntPtr.Zero && current != InvalidHandleValue) CloseHandle(current);
            Volatile.Write(ref activeGeneration, 0);
            Volatile.Write(ref attachedDeviceCount, 0);
        }

        private void SetHealth(bool value, string detail)
        {
            Interlocked.Exchange(ref healthy, value ? 1 : 0);
            string normalized = value ? "ready" : (detail ?? "unavailable");
            bool notify;
            lock (stateTextLock)
            {
                state = normalized;
                notify = !string.Equals(lastNotifiedState, normalized, StringComparison.Ordinal);
                if (notify) lastNotifiedState = normalized;
            }
            if (notify && healthCallback != null)
            {
                try { healthCallback(value, normalized); }
                catch (Exception ex) { Log("RC003 filter health callback failed: " + ex.Message); }
            }
        }

        public void Dispose()
        {
            stopEvent.Set();
            wakeEvent.Set();
            bool ioStopped = ioThread == null || !ioThread.IsAlive || ioThread.Join(2000);
            ioThread = null;

            Volatile.Write(ref activeGeneration, 0);
            dispatchQueue.CompleteAdding();
            if (dispatchThread != null && dispatchThread.IsAlive) dispatchThread.Join(2000);
            dispatchThread = null;
            if (!ioStopped)
            {
                // Leave synchronization objects alive for the blocked worker;
                // process teardown will close them and the kernel heartbeat
                // independently guarantees fail-open suppression.
                Log("RC003 filter I/O thread did not stop within 2000 ms; fail-open timeout remains active");
                return;
            }
            dispatchQueue.Dispose();
            wakeEvent.Dispose();
            stopEvent.Dispose();
        }
    }

    // The user's own text snippets, shipped by the Host inside the mapping document. The Bridge only ever
    // types this text: it never logs it, never reads it back and never compares it to anything.
    public sealed class BridgeSnippet
    {
        public string id { get; set; }
        public string name { get; set; }
        public string text { get; set; }
    }

    public sealed class BridgeConfig
    {
        public int version { get; set; }
        public string revision { get; set; }
        public string notes { get; set; }
        public string inputRoutingMode { get; set; }
        public string activeShortcutProfileId { get; set; }
        public string activeShortcutProfileName { get; set; }
        public bool smartProfilesEnabled { get; set; }
        public bool smartProfileLocked { get; set; }
        public string fallbackShortcutProfileId { get; set; }
        public BridgeShortcutProfile[] profiles { get; set; }
        public ShortcutMapping[] mappings { get; set; }
        public BridgeSnippet[] snippets { get; set; }

        public static BridgeConfig Default()
        {
            return new BridgeConfig
            {
                version = 7,
                revision = "default",
                notes = "Default VoxDeck RC003 mapping.",
                inputRoutingMode = "strict",
                activeShortcutProfileId = "general",
                activeShortcutProfileName = "通用导航",
                smartProfilesEnabled = false,
                smartProfileLocked = false,
                fallbackShortcutProfileId = "general",
                profiles = new BridgeShortcutProfile[0],
                snippets = new BridgeSnippet[0],
                mappings = new ShortcutMapping[]
                {
                    // The remote's power button: VK 0xFF / scan E0 5E, the ACPI power key. The bridge already recognised
                    // that pair (FindMapping and the filter mask both know 0x5E) but no mapping existed, so the key was seen
                    // and dropped. It stays unconfigured until the user assigns an action, so Windows keeps handling it.
                    new ShortcutMapping { name = "power", label = "电源键", vk = "0xFF", scan = "0x5E", enabled = false, suppress = false, sourceType = "keyboard", mode = "passthrough", shortcut = "none" },
                    new ShortcutMapping { name = "voice", label = "录音键", vk = "F5", scan = "0x3F", enabled = true, suppress = true, mode = "suppress", shortcut = "" },
                    new ShortcutMapping { name = "home", label = "Home 键", vk = "Home", scan = "0x47", enabled = true, suppress = true, mode = "shortlong", shortShortcut = "win+d", longShortcut = "none", longPressMs = 650 },
                    new ShortcutMapping { name = "tv", label = "TV 键", vk = "Oemtilde", scan = "0x29", enabled = true, suppress = true, mode = "tap", shortcut = "task-switcher" },
                    new ShortcutMapping { name = "menu", label = "功能键", vk = "Apps", scan = "0x5D", enabled = true, suppress = true, mode = "shortlong", shortShortcut = "ctrl+c", longShortcut = "ctrl+v", longPressMs = 650 },
                    new ShortcutMapping { name = "ok", label = "确认键", vk = "Enter", scan = "0x1C", enabled = false, suppress = false, mode = "passthrough", shortcut = "enter" },
                    new ShortcutMapping { name = "up", label = "上键", vk = "Up", scan = "0x48", enabled = false, suppress = false, mode = "passthrough", shortcut = "up" },
                    new ShortcutMapping { name = "down", label = "下键", vk = "Down", scan = "0x50", enabled = false, suppress = false, mode = "passthrough", shortcut = "down" },
                    new ShortcutMapping { name = "left", label = "左键", vk = "Left", scan = "0x4B", enabled = false, suppress = false, mode = "passthrough", shortcut = "left" },
                    new ShortcutMapping { name = "right", label = "右键", vk = "Right", scan = "0x4D", enabled = false, suppress = false, mode = "passthrough", shortcut = "right" }
                }
            };
        }
    }

    private static string NormalizeInputRoutingMode(string value)
    {
        return "strict";
    }

    public sealed class ShortcutMapping
    {
        public string name { get; set; }
        public string label { get; set; }
        public string vk { get; set; }
        public string scan { get; set; }
        public bool enabled { get; set; }
        public bool suppress { get; set; }
        public string mode { get; set; }
        public string shortcut { get; set; }
        public string shortShortcut { get; set; }
        public string longShortcut { get; set; }
        // Gesture layering: the third (double tap) layer, generated by the Host from gesture-layers.json.
        public string doubleShortcut { get; set; }
        public int longPressMs { get; set; }
        public string sourceType { get; set; }
        public int usagePage { get; set; }
        public int usage { get; set; }

        public string labelOrName()
        {
            return string.IsNullOrWhiteSpace(label) ? (name ?? vk ?? "mapping") : label;
        }
    }

    private sealed class MappingEvent
    {
        public ShortcutMapping mapping;
        public bool keyUp;
        public string source;
        public string command;
        public string testToken;
        public string testAction;
        public CustomTestRequest browserTestRequest;
        public string browserTestClaimPath;
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public int vkCode;
        public int scanCode;
        public int flags;
        public int time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTDEVICE
    {
        public ushort usUsagePage;
        public ushort usUsage;
        public int dwFlags;
        public IntPtr hwndTarget;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTHEADER
    {
        public uint dwType;
        public uint dwSize;
        public IntPtr hDevice;
        public IntPtr wParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWKEYBOARD
    {
        public ushort MakeCode;
        public ushort Flags;
        public ushort Reserved;
        public ushort VKey;
        public uint Message;
        public uint ExtraInformation;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWHID
    {
        public uint dwSizeHid;
        public uint dwCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HIDP_CAPS
    {
        public ushort Usage;
        public ushort UsagePage;
        public ushort InputReportByteLength;
        public ushort OutputReportByteLength;
        public ushort FeatureReportByteLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
        public ushort NumberLinkCollectionNodes;
        public ushort NumberInputButtonCaps;
        public ushort NumberInputValueCaps;
        public ushort NumberInputDataIndices;
        public ushort NumberOutputButtonCaps;
        public ushort NumberOutputValueCaps;
        public ushort NumberOutputDataIndices;
        public ushort NumberFeatureButtonCaps;
        public ushort NumberFeatureValueCaps;
        public ushort NumberFeatureDataIndices;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public INPUTUNION u;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public UIntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
        public uint lPrivate;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUTUNION
    {
        [FieldOffset(0)]
        public MOUSEINPUT mi;
        [FieldOffset(0)]
        public KEYBDINPUT ki;
        [FieldOffset(0)]
        public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    private sealed class HoldRepeatRequest
    {
        public string Name;
        public int Generation;
        public ShortcutMapping Mapping;
    }

    private sealed class ShortLongGestureState
    {
        private int generation;
        private bool longFired;

        public bool IsDown { get; private set; }

        public int Begin()
        {
            if (IsDown) return 0;
            IsDown = true;
            longFired = false;
            generation++;
            if (generation <= 0) generation = 1;
            return generation;
        }

        public bool TryFireLong(int expectedGeneration)
        {
            if (!IsDown || longFired || expectedGeneration <= 0 || expectedGeneration != generation) return false;
            longFired = true;
            return true;
        }

        public bool Release()
        {
            if (!IsDown) return false;
            IsDown = false;
            return !longFired;
        }

        public void Reset()
        {
            IsDown = false;
            longFired = false;
            generation++;
            if (generation <= 0) generation = 1;
        }
    }

    private sealed class RawKeyboardEdgeTracker
    {
        private readonly object sync = new object();
        private readonly HashSet<string> keysDown = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public bool ShouldDispatch(int virtualKey, int scanCode, bool keyUp)
        {
            string identity = scanCode > 0
                ? "scan:" + scanCode.ToString("X4")
                : "vk:" + virtualKey.ToString("X4");
            lock (sync)
            {
                if (keyUp) return keysDown.Remove(identity);
                return keysDown.Add(identity);
            }
        }

        public void Reset()
        {
            lock (sync) keysDown.Clear();
        }
    }

    public sealed class BridgeShortcutProfile
    {
        public string id { get; set; }
        public string name { get; set; }
        public string[] processNames { get; set; }
        public ShortcutMapping[] mappings { get; set; }
    }

    private sealed class GestureTimerRequest
    {
        public string Name;
        public int Generation;
        public ShortcutMapping Mapping;
        public string Source;
    }

    private sealed class ActionExecutionReceipt
    {
        public long Sequence;
        public DateTime TimestampUtc;
        public string Button = "";
        public string Label = "";
        public string Trigger = "";
        public string Action = "";
        public string Source = "";
        public string ProfileId = "";
        public string ProfileName = "";
        public string ConfigRevision = "";
        public bool Success;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTDEVICELIST
    {
        public IntPtr hDevice;
        public uint dwType;
    }

    private sealed class CustomCaptureRequest
    {
        public bool active { get; set; }
        public string token { get; set; }
        public int slot { get; set; }
        public string created_at { get; set; }
    }

    private sealed class CustomCaptureResult
    {
        public string token { get; set; }
        public int slot { get; set; }
        public string sourceType { get; set; }
        public string vk { get; set; }
        public string scan { get; set; }
        public int usagePage { get; set; }
        public int usage { get; set; }
    }

    private sealed class CustomTestRequest
    {
        public string token { get; set; }
        public int slot { get; set; }
        public string name { get; set; }
        public string label { get; set; }
        public string action { get; set; }
        public string created_at { get; set; }
        public int expected_process_id { get; set; }
        public long expected_window_handle { get; set; }
        public string expected_process_name { get; set; }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateFile(string fileName, uint desiredAccess, uint shareMode,
        IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(IntPtr device, uint controlCode,
        [In] byte[] inputBuffer, int inputBufferSize,
        [Out] byte[] outputBuffer, int outputBufferSize,
        out int bytesReturned, IntPtr overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostThreadMessage(uint threadId, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out MSG message, IntPtr window, uint minimumMessage, uint maximumMessage);

    [DllImport("user32.dll")]
    private static extern bool PeekMessage(out MSG message, IntPtr window, uint minimumMessage, uint maximumMessage,
        uint removeMessage);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage([In] ref MSG message);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage([In] ref MSG message);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern bool ShowWindowAsync(IntPtr window, int command);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr window);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, IntPtr processId);

    [DllImport("user32.dll", EntryPoint = "GetWindowThreadProcessId")]
    private static extern uint GetWindowThreadProcessIdForSmartProfile(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint attachThread, uint attachToThread, bool attach);

    [DllImport("user32.dll")]
    private static extern IntPtr SetFocus(IntPtr window);

    [DllImport("user32.dll")]
    private static extern void SwitchToThisWindow(IntPtr window, bool altTab);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] devices, uint deviceCount, uint size);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputData(IntPtr rawInput, uint command, IntPtr data, ref uint size, uint headerSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputDeviceList([Out] RAWINPUTDEVICELIST[] devices, ref uint deviceCount, uint size);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint GetRawInputDeviceInfo(IntPtr device, uint command, IntPtr data, ref uint size);

    [DllImport("hid.dll")]
    private static extern int HidP_GetCaps(IntPtr preparsedData, out HIDP_CAPS capabilities);

    [DllImport("hid.dll")]
    private static extern int HidP_GetUsages(int reportType, ushort usagePage, ushort linkCollection, [Out] ushort[] usageList,
        ref uint usageLength, IntPtr preparsedData, IntPtr report, uint reportLength);
}
