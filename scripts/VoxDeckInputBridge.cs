using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("Vibe Flow RC003 input bridge")]
[assembly: System.Reflection.AssemblyProduct("Vibe Flow Remote")]
[assembly: System.Reflection.AssemblyCompany("Vibe Flow Contributors")]
[assembly: System.Reflection.AssemblyVersion("1.5.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.5.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersion("1.5.0")]

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
    private const uint KEYEVENTF_SCANCODE = 0x0008;
    private const int CUSTOM_CAPTURE_REQUEST_TIMEOUT_SECONDS = 15;
    private const int DEFAULT_LONG_PRESS_MS = 650;
    private const int HOLD_REPEAT_INITIAL_DELAY_MS = 420;
    private const int HOLD_REPEAT_INTERVAL_MS = 80;
    private const int VOICE_RESTART_GUARD_MS = 500;
    private const int SMART_PROFILE_POLL_MS = 250;
    private const int SMART_PROFILE_DEBOUNCE_MS = 350;

    private static readonly object stateLock = new object();
    private static readonly object logLock = new object();
    private static readonly Dictionary<string, bool> sourceDown = new Dictionary<string, bool>();
    private static readonly Dictionary<string, bool> shortcutDown = new Dictionary<string, bool>();
    private static readonly Dictionary<string, ShortLongGestureState> gestureStates = new Dictionary<string, ShortLongGestureState>();
    private static readonly Dictionary<string, System.Threading.Timer> gestureTimers = new Dictionary<string, System.Threading.Timer>();
    private static readonly Dictionary<string, System.Threading.Timer> holdRepeatTimers = new Dictionary<string, System.Threading.Timer>();
    private static readonly Dictionary<string, int> holdRepeatGenerations = new Dictionary<string, int>();

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
    private static readonly object voiceTransitionLock = new object();
    private static DateTime lastVoiceReleaseUtc = DateTime.MinValue;
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
            customTestTimer = new System.Threading.Timer(delegate { ProcessCustomButtonTest(); }, null, 250, 250);
            Application.Run(form);
            StopRc003FilterClient();
            StopKeyboardHookThread();
            rawKeyboardEdgeTracker.Reset();
            mappingQueue.CompleteAdding();
            if (mappingWorker != null) mappingWorker.Join(1500);
            if (bridgeHealthTimer != null) { bridgeHealthTimer.Dispose(); bridgeHealthTimer = null; }
            if (smartProfileTimer != null) { smartProfileTimer.Dispose(); smartProfileTimer = null; }
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
        keyboardHookThread.Name = "Vibe Flow device-aware keyboard hook";
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
                    // Keep a separate hook heartbeat. Raw Input is device-scoped,
                    // while the low-level hook is the reliable fallback after a
                    // Bluetooth HID reconnect.
                    if (data.vkCode == 0x74 || data.vkCode == 0xF5)
                    {
                        MarkRemoteInput("keyboard_hook");
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
                        if (isVoiceMapping)
                        {
                            // The low-level keyboard hook remains the single authoritative
                            // fallback for ordinary F5 delivery. RC003 Raw Input also calls
                            // this same transition method, so reconnects cannot duplicate
                            // a start/stop signal or leave the held state out of order.
                            HandleVoicePhysicalTransition(isDown, "keyboard_hook", data.vkCode, data.scanCode);
                            return mapping.suppress ? (IntPtr)1 : CallNextHookEx(hookHandle, nCode, wParam, lParam);
                        }
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
        if (healthy)
        {
            rawKeyboardEdgeTracker.Reset();
            Log("RC003 device filter ready; ordinary keyboards are passthrough detail=" + (detail ?? "ready"));
            BridgeForm.SetStatusText("RC003 专属按键通道已就绪");
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
        if (mapping == null && IsVoiceRawCandidate(virtualKey, input.MakeCode))
            mapping = FindVoiceMapping();
        bool isVoice = mapping != null &&
            (mapping.name ?? "").Equals("voice", StringComparison.OrdinalIgnoreCase);

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
                " delivery=native_passthrough revision=" +
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
            " delivery=native_passthrough revision=" +
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
        try
        {
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
                ShouldBypassHookForRc003Filter(false, true, true))
                throw new InvalidOperationException("RC003 filter hook passthrough policy failed");
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
            Console.WriteLine("Vibe Flow input bridge self-test passed.");
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
                bool success = ExecuteMappingAction(item.mapping, item.testAction, "测试", "ui_test");
                WriteCustomButtonTestResult(item.testToken, item.testAction, success,
                    success ? "动作已由按键桥接执行" : "动作未执行，请检查应用路径或动作配置");
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
                    BridgeForm.SetStatusText(mapping.labelOrName() + " 已接管");
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
                        shortcutDown[name] = true;
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
        string action = null;
        string phase = null;
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
                Log("Key " + mapping.labelOrName() + " DOWN gesture=shortlong threshold_ms=" + threshold);
                BridgeForm.SetStatusText(mapping.labelOrName() + " 已按下");
                return;
            }

            if (!wasSourceDown)
            {
                Log("Gesture " + mapping.labelOrName() + " duplicate UP ignored");
                return;
            }
            sourceDown[name] = false;
            DisposeGestureTimer(name);
            ShortLongGestureState current;
            bool fireShort = gestureStates.TryGetValue(name, out current) && current.Release();
            Log("Key " + mapping.labelOrName() + " UP gesture=" + (fireShort ? "short" : "long_or_cancelled"));
            if (fireShort)
            {
                action = mapping.shortShortcut;
                phase = "短按";
            }
        }
        if (action != null) ExecuteMappingAction(mapping, action, phase, source);
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
            DisposeGestureTimer(request.Name);
        }
        if (fire) ExecuteMappingAction(request.Mapping, request.Mapping.longShortcut, "长按", request.Source);
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
            BridgeForm.SetStatusText(source.labelOrName() + " " + phase + "未设置");
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
        bool delivered = false;
        try
        {
            delivered = voiceKeyPressedEvent != null && voiceKeyPressedEvent.Set();
            Log("Voice key signal delivered=" + delivered + " source=bridge_transition");
        }
        catch (Exception ex) { Log("Voice key signal failed: " + ex.Message); }
        SignalVoiceWakeRequested(delivered ? "capture_signal_delivered" : "capture_not_ready");
        if (!delivered) EnsureVoiceHostRunning();
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

    private static void HandleVoicePhysicalTransition(bool isDown, string source, int vk, int scan)
    {
        lock (voiceTransitionLock)
        {
            bool alreadyHeld = Volatile.Read(ref voiceKeyHeldState) == 1;
            if (isDown)
            {
                if (alreadyHeld)
                {
                    Log("Voice key duplicate DOWN ignored source=" + source + " vk=0x" + vk.ToString("X2") +
                        " scan=0x" + scan.ToString("X2"));
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
                Log("Key 录音键 DOWN vk=0x" + vk.ToString("X2") + " scan=0x" + scan.ToString("X2") +
                    " source=" + source);
                SignalVoiceKeyPressed();
                return;
            }

            if (!alreadyHeld)
            {
                Log("Voice key duplicate UP ignored source=" + source + " vk=0x" + vk.ToString("X2") +
                    " scan=0x" + scan.ToString("X2"));
                return;
            }
            SetVoiceKeyHeld(false);
            lastVoiceReleaseUtc = DateTime.UtcNow;
            Log("Key 录音键 UP vk=0x" + vk.ToString("X2") + " scan=0x" + scan.ToString("X2") +
                " source=" + source);
        }
    }

    private static void SignalVoiceWakeRequested(string reason)
    {
        try
        {
            bool delivered = voiceWakeRequestEvent != null && voiceWakeRequestEvent.Set();
            Log("Voice service wake requested=" + delivered + " reason=" + reason);
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
        Log("SendShortcut " + (keyUp ? "UP " : "DOWN ") + mapping.labelOrName() + " " + mapping.shortcut + " " + ModeName() + " sent=" + sent + " error=" + error);
        return complete;
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
            normalized.StartsWith("shortcut:", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HandleCustomAction(ShortcutMapping mapping)
    {
        string action = (mapping.shortcut ?? "").Trim();
        string label = mapping.labelOrName();
        try
        {
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
                    BridgeForm.SetStatusText(label + " 已切换");
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
                    BridgeForm.SetStatusText(label + " 应用不存在");
                    return false;
                }
                Process.Start(new ProcessStartInfo { FileName = executable, UseShellExecute = true });
                Log("Custom app action started label=" + label + " path=" + executable);
                BridgeForm.SetStatusText(label + " 已启动");
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
                BridgeForm.SetStatusText(appLabel + " 当前未运行");
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
                BridgeForm.SetStatusText(label + " 快捷键为空");
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

            if (TryLaunchInstalledStartApp(startAppNames) || TryLaunchExecutable(executableNames))
            {
                BridgeForm.SetStatusText("正在启动 " + label);
                Log("Client launcher started target=" + provider);
                return true;
            }

            BridgeForm.SetStatusText("未找到 " + label + " 客户端");
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
            if (process == null || !process.WaitForExit(6000)) return false;
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
            command == "confirm" ? "进入所选程序" : "关闭任务视图");
        CloseTaskSwitcherState(command == "confirm" ? "已切换程序" : "已关闭任务视图");
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
                if (mapping == null && IsVoiceRawCandidate(keyboard.VKey, keyboard.MakeCode))
                {
                    ShortcutMapping fallbackVoice = FindVoiceMapping();
                    if (fallbackVoice != null && fallbackVoice.enabled)
                    {
                        mapping = fallbackVoice;
                        Log("Voice raw VK fallback vk=0x" + keyboard.VKey.ToString("X2") +
                            " scan=0x" + keyboard.MakeCode.ToString("X2"));
                    }
                }
                bool isVoice = mapping != null && mapping.enabled &&
                    (mapping.name ?? "").Equals("voice", StringComparison.OrdinalIgnoreCase);
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
            if (config == null || config.mappings == null)
            {
                return;
            }

            foreach (ShortcutMapping mapping in config.mappings)
            {
                string name = mapping.name ?? mapping.vk ?? "unknown";
                if (shortcutDown.ContainsKey(name) && shortcutDown[name])
                {
                    SendShortcut(mapping, true);
                    shortcutDown[name] = false;
                }
            }
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
            BridgeForm.SetStatusText("配置已加载：" + count + " 项映射");
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
        var next = new BridgeConfig
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
            mappings = target.mappings
        };
        config = next;
        RefreshRc003FilterPolicy();
        Log("Smart Profile switched profile=" + (target.id ?? "") + " state=" + (matchState ?? "") +
            " foreground=" + (foregroundProcess ?? ""));
        BridgeForm.SetStatusText("Profile 已切换：" + (target.name ?? target.id ?? "快捷键方案"));
        WriteHealth("running");
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
        BridgeForm.SetStatusText("注入模式：" + ModeName());
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

    private static bool IsVoiceRawCandidate(int vk, int scan)
    {
        // RC003 has emitted both the translated F5 form and the raw HID form
        // (VK 0xFF / scan 0x5E) across Bluetooth reconnects.
        return vk == 0x74 || vk == 0xF5 || (vk == 0xFF && scan == 0x5E);
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
            try { File.Delete(CustomTestPath); } catch { }
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
                Log("Button action test ignored slot=" + request.slot + " reason=not_configured");
                WriteCustomButtonTestResult(request.token, request.action, false, "按键尚未配置有效动作");
                return;
            }
            mappingQueue.Add(new MappingEvent
            {
                mapping = target,
                testToken = request.token,
                testAction = target.shortcut
            });
            Log("Button action test queued name=" + target.name + " action=" + target.shortcut);
            BridgeForm.SetStatusText("正在测试 " + target.labelOrName());
        }
        catch (Exception ex) { Log("Custom button test failed: " + ex.Message); }
    }

    private static void WriteCustomButtonTestResult(string token, string action, bool success, string message)
    {
        if (string.IsNullOrWhiteSpace(token)) return;
        try
        {
            var result = new Dictionary<string, object>();
            result["token"] = token;
            result["action"] = action ?? "";
            result["success"] = success;
            result["message"] = message ?? "";
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
            description.Text = "按 voxdeck-shortcuts.json 映射遥控器键。录音键由 ATVV 语音组件独立接管。";
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
            reload.Text = "重载配置";
            reload.Left = 188;
            reload.Top = 222;
            reload.Width = 110;
            reload.Height = 36;
            reload.Click += delegate { LoadConfig(); };

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
            close.Text = "停止桥接";
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
            rawInputHealthTimer.Interval = 30000;
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
                else
                {
                    Log("Raw Input health check ok device_present=true");
                }
            };
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
                if (changeType == 2 && Volatile.Read(ref voiceKeyHeldState) == 1)
                    HandleVoicePhysicalTransition(false, "raw_device_removed", 0, 0);
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
            dispatchThread.Name = "Vibe Flow RC003 filter dispatch";
            dispatchThread.Start();

            ioThread = new Thread(IoLoop);
            ioThread.IsBackground = true;
            ioThread.Name = "Vibe Flow RC003 filter heartbeat";
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
                mappings = new ShortcutMapping[]
                {
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
