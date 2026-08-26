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
[assembly: System.Reflection.AssemblyVersion("1.2.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.2.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersion("1.2.0")]

internal static class VoxDeckInputBridge
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;
    private const int WM_INPUT = 0x00FF;
    private const int LLKHF_INJECTED = 0x10;
    private const uint RID_INPUT = 0x10000003;
    private const uint RIDI_PREPARSEDDATA = 0x20000005;
    private const uint RIDI_DEVICENAME = 0x20000007;
    private const uint RIM_TYPEKEYBOARD = 1;
    private const uint RIM_TYPEHID = 2;
    private const int RIDEV_INPUTSINK = 0x00000100;
    private const ushort HID_USAGE_PAGE_CONSUMER = 0x0C;
    private const ushort HID_USAGE_VOLUME_INCREMENT = 0xE9;
    private const ushort HID_USAGE_VOLUME_DECREMENT = 0xEA;
    private const ushort HID_USAGE_MUTE = 0xE2;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_SCANCODE = 0x0008;

    private static readonly object stateLock = new object();
    private static readonly object logLock = new object();
    private static readonly Dictionary<string, bool> sourceDown = new Dictionary<string, bool>();
    private static readonly Dictionary<string, bool> shortcutDown = new Dictionary<string, bool>();

    private static IntPtr hookHandle = IntPtr.Zero;
    private static LowLevelKeyboardProc hookProc = HookCallback;
    private static Mutex singleInstance;
    private static EventWaitHandle stopEvent;
    private static EventWaitHandle voiceKeyHeldEvent;
    private static EventWaitHandle voiceKeyReleasedEvent;
    private static EventWaitHandle voiceWakeRequestEvent;
    private static int voiceKeyHeldState;
    private static readonly BlockingCollection<MappingEvent> mappingQueue = new BlockingCollection<MappingEvent>();
    private static Thread mappingWorker;
    private static BridgeConfig config = BridgeConfig.Default();
    private static bool useScanCode = true;
    private static DateTime configLastWriteUtc = DateTime.MinValue;
    private static DateTime lastRawVolumeUtc = DateTime.MinValue;
    private static int rawDirectionVk;
    private static DateTime rawDirectionStartedUtc = DateTime.MinValue;
    private static DateTime lastDirectionVolumeUtc = DateTime.MinValue;
    private static bool rawDirectionVolumeActive;
    private static readonly object taskSwitcherLock = new object();
    private static readonly HashSet<int> taskSwitcherKeysDown = new HashSet<int>();
    private static bool taskSwitcherActive;
    private static bool taskSwitcherAltDown;
    private static System.Threading.Timer taskSwitcherTimer;

    private static readonly string Root = AppDomain.CurrentDomain.BaseDirectory;
    private static readonly string ConfigPath = Path.Combine(Root, "voxdeck-shortcuts.json");
    private static readonly string LogPath = Path.Combine(Root, "input-bridge-log.txt");

    [STAThread]
    private static void Main(string[] args)
    {
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

        LoadConfig();
        using (var form = new BridgeForm(background))
        {
            mappingWorker = new Thread(ProcessMappingQueue);
            mappingWorker.IsBackground = true;
            mappingWorker.Name = "Vibe Mic shortcut queue";
            mappingWorker.Start();
            stopEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\VibeMicStopKeyboardBridge");
            voiceKeyHeldEvent = new EventWaitHandle(false, EventResetMode.ManualReset, "Local\\VibeMicVoiceKeyHeld");
            voiceKeyReleasedEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\VibeMicVoiceKeyReleased");
            voiceWakeRequestEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\VibeMicVoiceWakeRequested");
            voiceKeyHeldEvent.Reset();
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
            hookHandle = SetHook(hookProc);
            Log("SetWindowsHookEx result=" + hookHandle + " error=" + Marshal.GetLastWin32Error());
            Application.Run(form);
            mappingQueue.CompleteAdding();
            if (mappingWorker != null) mappingWorker.Join(1500);
            SetVoiceKeyHeld(false);
            ReleaseAllShortcuts();
            if (hookHandle != IntPtr.Zero)
            {
                UnhookWindowsHookEx(hookHandle);
                Log("Hook uninstalled");
            }
            try { stopEvent.Set(); } catch { }
            stopEvent.Dispose();
            voiceKeyHeldEvent.Dispose();
            voiceKeyReleasedEvent.Dispose();
            voiceWakeRequestEvent.Dispose();
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
                    ReloadConfigIfChanged();
                    ShortcutMapping mapping = FindMapping(data.vkCode, data.scanCode);
                    if (mapping != null && mapping.enabled &&
                        (mapping.shortcut ?? "").Equals("task-switcher", StringComparison.OrdinalIgnoreCase))
                    {
                        HandleTaskSwitcherToggle(isDown, isUp);
                        return (IntPtr)1;
                    }
                    if (HandleTaskSwitcherNavigation(data.vkCode, isDown, isUp)) return (IntPtr)1;
                    if (mapping != null && mapping.enabled)
                    {
                        if ((mapping.name ?? "").Equals("voice", StringComparison.OrdinalIgnoreCase))
                        {
                            if (isDown) SetVoiceKeyHeld(true);
                            else if (isUp) SetVoiceKeyHeld(false);
                        }
                        QueueMapping(mapping, isUp);
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

    private static ShortcutMapping FindMapping(int vkCode, int scanCode)
    {
        BridgeConfig snapshot = config;
        if (snapshot == null || snapshot.mappings == null)
        {
            return null;
        }

        foreach (ShortcutMapping mapping in snapshot.mappings)
        {
            int expectedVk = VkFromName(mapping.vk);
            int expectedScan = ParseHexOrDecimal(mapping.scan);
            if (expectedVk == vkCode && (expectedScan < 0 || expectedScan == scanCode))
            {
                return mapping;
            }
        }
        return null;
    }

    private static void QueueMapping(ShortcutMapping mapping, bool keyUp)
    {
        try { mappingQueue.Add(new MappingEvent { mapping = mapping, keyUp = keyUp }); }
        catch (InvalidOperationException) { }
    }

    private static void QueueTaskSwitcherCommand(string command)
    {
        try { mappingQueue.Add(new MappingEvent { command = command }); }
        catch (InvalidOperationException) { }
    }

    private static void ProcessMappingQueue()
    {
        foreach (MappingEvent item in mappingQueue.GetConsumingEnumerable())
        {
            if (!string.IsNullOrWhiteSpace(item.command)) HandleTaskSwitcherCommand(item.command);
            else HandleMapping(item.mapping, item.keyUp);
        }
    }

    private static void HandleTaskSwitcherToggle(bool isDown, bool isUp)
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
                QueueTaskSwitcherCommand("confirm");
            }
            else
            {
                taskSwitcherActive = true;
                QueueTaskSwitcherCommand("open");
            }
        }
    }

    private static bool HandleTaskSwitcherNavigation(int virtualKey, bool isDown, bool isUp)
    {
        lock (taskSwitcherLock)
        {
            if (!taskSwitcherActive) return false;
            bool supported = virtualKey == 0x25 || virtualKey == 0x27 || virtualKey == 0x0D || virtualKey == 0x1B || virtualKey == 0x08 || virtualKey == 0xA6;
            if (!supported) return false;
            if (isUp)
            {
                taskSwitcherKeysDown.Remove(virtualKey);
                return true;
            }
            if (!isDown || taskSwitcherKeysDown.Contains(virtualKey)) return true;
            taskSwitcherKeysDown.Add(virtualKey);
            if (virtualKey == 0x25) QueueTaskSwitcherCommand("previous");
            else if (virtualKey == 0x27) QueueTaskSwitcherCommand("next");
            else
            {
                taskSwitcherActive = false;
                QueueTaskSwitcherCommand(virtualKey == 0x1B || virtualKey == 0x08 || virtualKey == 0xA6 ? "cancel" : "confirm");
            }
            return true;
        }
    }

    private static void HandleMapping(ShortcutMapping mapping, bool keyUp)
    {
        lock (stateLock)
        {
            string name = mapping.name ?? mapping.vk ?? "unknown";
            bool isVoice = name.Equals("voice", StringComparison.OrdinalIgnoreCase);
            bool wasSourceDown = sourceDown.ContainsKey(name) && sourceDown[name];
            string mode = (mapping.mode ?? "tap").ToLowerInvariant();

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
                    SendShortcut(mapping, false);
                    shortcutDown[name] = true;
                    BridgeForm.SetStatusText(mapping.labelOrName() + " 按下 -> " + mapping.shortcut);
                }
                else if (IsAiLauncherAction(mapping.shortcut))
                {
                    LaunchAiTarget(mapping.shortcut);
                }
                else
                {
                    TapShortcut(mapping);
                    BridgeForm.SetStatusText(mapping.labelOrName() + " 轻触 -> " + mapping.shortcut);
                }
                return;
            }

            sourceDown[name] = false;
            Log("Key " + mapping.labelOrName() + " UP vk=" + mapping.vk + " scan=" + mapping.scan);
            if (mode == "hold" && shortcutDown.ContainsKey(name) && shortcutDown[name])
            {
                SendShortcut(mapping, true);
                shortcutDown[name] = false;
                BridgeForm.SetStatusText(mapping.labelOrName() + " 松开 -> " + mapping.shortcut);
            }
        }
    }

    private static void SignalVoiceKeyPressed()
    {
        bool delivered = false;
        try
        {
            using (EventWaitHandle handle = EventWaitHandle.OpenExisting("Local\\VibeMicVoiceKeyPressed"))
            {
                delivered = handle.Set();
                Log("Voice key signal delivered=" + delivered);
            }
        }
        catch (WaitHandleCannotBeOpenedException) { Log("Voice key signal unavailable: capture_not_running"); }
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
        if (HasRunningProcess("VibeFlow") || HasRunningProcess("VibeMic")) return;
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

    private static bool HasRunningProcess(string name)
    {
        Process[] processes = Process.GetProcessesByName(name);
        try { return processes.Length > 0; }
        finally { foreach (Process process in processes) process.Dispose(); }
    }

    private static void TapShortcut(ShortcutMapping mapping)
    {
        SendShortcut(mapping, false);
        Thread.Sleep(100);
        SendShortcut(mapping, true);
    }

    private static void SendShortcut(ShortcutMapping mapping, bool keyUp)
    {
        List<int> keys = ParseShortcut(mapping.shortcut);
        if (keys.Count == 0)
        {
            Log("Shortcut empty for " + mapping.labelOrName());
            return;
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
        Log("SendShortcut " + (keyUp ? "UP " : "DOWN ") + mapping.labelOrName() + " " + mapping.shortcut + " " + ModeName() + " sent=" + sent + " error=" + error);
    }

    private static bool IsAiLauncherAction(string action)
    {
        string normalized = (action ?? "").Trim().ToLowerInvariant();
        if (normalized.StartsWith("launch-ai:", StringComparison.Ordinal)) normalized = "launch-client:" + normalized.Substring("launch-ai:".Length);
        return normalized == "launch-client:chatgpt" || normalized == "launch-client:deepseek" ||
            normalized == "launch-client:claude" || normalized == "launch-client:cursor" ||
            normalized == "launch-client:vscode" || normalized == "launch-client:windsurf" ||
            normalized == "launch-client:terminal";
    }

    private static void LaunchAiTarget(string action)
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
            if (TryFocusClientWindow(processNames, out focusedProcess))
            {
                BridgeForm.SetStatusText("已切换到 " + label);
                Log("Client launcher focused target=" + provider + " process=" + focusedProcess);
                return;
            }

            if (TryLaunchInstalledStartApp(startAppNames) || TryLaunchExecutable(executableNames))
            {
                BridgeForm.SetStatusText("正在启动 " + label);
                Log("Client launcher started target=" + provider);
                return;
            }

            BridgeForm.SetStatusText("未找到 " + label + " 客户端");
            Log("Client launcher unavailable target=" + provider);
        }
        catch (Exception ex)
        {
            BridgeForm.SetStatusText(label + " 打开失败");
            Log("Client launcher failed target=" + provider + " error=" + ex.Message);
        }
    }

    private static bool TryFocusClientWindow(string[] processNames, out string focusedProcess)
    {
        focusedProcess = "";
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
                    ShowWindowAsync(window, 9);
                    SwitchToThisWindow(window, true);
                    BringWindowToTop(window);
                    SetForegroundWindow(window);
                    focusedProcess = process.ProcessName;
                    Log("Client window activation handle=" + window + " foreground=" + (GetForegroundWindow() == window));
                    return true;
                }
            }
            finally { foreach (Process process in processes) process.Dispose(); }
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

    private static void TapVirtualKey(int virtualKey, string label)
    {
        INPUT[] down = new INPUT[] { KeyInput(virtualKey, false, IsExtendedKey(virtualKey)) };
        INPUT[] up = new INPUT[] { KeyInput(virtualKey, true, IsExtendedKey(virtualKey)) };
        uint sentDown = SendInput(1, down, Marshal.SizeOf(typeof(INPUT)));
        Thread.Sleep(24);
        uint sentUp = SendInput(1, up, Marshal.SizeOf(typeof(INPUT)));
        Log("Virtual key " + label + " sent=" + sentDown + "/" + sentUp);
        BridgeForm.SetStatusText(label);
    }

    private static void SetVirtualKeyState(int virtualKey, bool keyUp)
    {
        INPUT[] input = new INPUT[] { KeyInput(virtualKey, keyUp, IsExtendedKey(virtualKey)) };
        SendInput(1, input, Marshal.SizeOf(typeof(INPUT)));
    }

    private static void HandleTaskSwitcherCommand(string command)
    {
        if (command == "open")
        {
            if (!taskSwitcherAltDown)
            {
                SetVirtualKeyState(0xA4, false);
                taskSwitcherAltDown = true;
            }
            Thread.Sleep(40);
            TapVirtualKey(0x09, "任务切换器已打开");
            ArmTaskSwitcherTimeout();
            return;
        }
        if (command == "previous")
        {
            SetVirtualKeyState(0xA0, false);
            TapVirtualKey(0x09, "选择上一个程序");
            SetVirtualKeyState(0xA0, true);
            ArmTaskSwitcherTimeout();
            return;
        }
        if (command == "next")
        {
            TapVirtualKey(0x09, "选择下一个程序");
            ArmTaskSwitcherTimeout();
            return;
        }
        if (command == "cancel") TapVirtualKey(0x1B, "取消任务切换");
        ReleaseTaskSwitcherAlt(command == "confirm" ? "已切换程序" : "已取消任务切换");
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
                    QueueTaskSwitcherCommand("confirm");
                }
            }, null, 10000, System.Threading.Timeout.Infinite);
        }
    }

    private static void ReleaseTaskSwitcherAlt(string status)
    {
        lock (taskSwitcherLock)
        {
            if (taskSwitcherTimer != null) { taskSwitcherTimer.Dispose(); taskSwitcherTimer = null; }
            taskSwitcherActive = false;
            taskSwitcherKeysDown.Clear();
        }
        if (taskSwitcherAltDown)
        {
            SetVirtualKeyState(0xA4, true);
            taskSwitcherAltDown = false;
        }
        BridgeForm.SetStatusText(status);
        Log(status);
    }

    private static void RegisterRawInput(IntPtr windowHandle)
    {
        RAWINPUTDEVICE[] devices = new RAWINPUTDEVICE[]
        {
            new RAWINPUTDEVICE { usUsagePage = 0x01, usUsage = 0x06, dwFlags = RIDEV_INPUTSINK, hwndTarget = windowHandle },
            new RAWINPUTDEVICE { usUsagePage = HID_USAGE_PAGE_CONSUMER, usUsage = 0x01, dwFlags = RIDEV_INPUTSINK, hwndTarget = windowHandle }
        };
        bool registered = RegisterRawInputDevices(devices, (uint)devices.Length, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE)));
        Log("Raw Input keyboard+consumer registered=" + registered + " error=" + (registered ? 0 : Marshal.GetLastWin32Error()));
    }

    private static void HandleRawInput(IntPtr hRawInput)
    {
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
            if (!IsRc003Device(deviceName)) return;

            IntPtr data = IntPtr.Add(buffer, Marshal.SizeOf(typeof(RAWINPUTHEADER)));
            if (header.dwType == RIM_TYPEKEYBOARD)
            {
                RAWKEYBOARD keyboard = (RAWKEYBOARD)Marshal.PtrToStructure(data, typeof(RAWKEYBOARD));
                bool keyUp = IsRawKeyUp(keyboard.Message);
                if (keyboard.VKey == 0x74)
                {
                    bool changed = SetVoiceKeyHeld(!keyUp);
                    if (!keyUp && changed) SignalVoiceKeyPressed();
                }
                bool firstDirectionDown = !keyUp && (keyboard.VKey == 0x26 || keyboard.VKey == 0x28) &&
                    (rawDirectionVk != keyboard.VKey || rawDirectionStartedUtc == DateTime.MinValue);
                HandleDirectionVolumeFallback(keyboard.VKey, keyUp);
                if (keyUp || (keyboard.VKey != 0x26 && keyboard.VKey != 0x28) || firstDirectionDown)
                {
                    Log("RC003 RAW KEY " + (keyUp ? "UP" : "DOWN") +
                        " vk=0x" + keyboard.VKey.ToString("X2") + " scan=0x" + keyboard.MakeCode.ToString("X2") +
                        " flags=0x" + keyboard.Flags.ToString("X2"));
                }

                return;
            }

            if (header.dwType != RIM_TYPEHID) return;
            RAWHID hid = (RAWHID)Marshal.PtrToStructure(data, typeof(RAWHID));
            IntPtr reports = IntPtr.Add(data, Marshal.SizeOf(typeof(RAWHID)));
            for (uint i = 0; i < hid.dwCount; i++)
            {
                IntPtr report = IntPtr.Add(reports, checked((int)(i * hid.dwSizeHid)));
                ushort[] usages = GetConsumerUsages(header.hDevice, report, hid.dwSizeHid);
                Log("RC003 RAW HID size=" + hid.dwSizeHid + " usages=" + FormatUsages(usages));
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

    private static void DispatchRawVolume(int virtualKey, string label)
    {
        DateTime now = DateTime.UtcNow;
        if ((now - lastRawVolumeUtc).TotalMilliseconds < 35) return;
        lastRawVolumeUtc = now;
        QueueRawAction(delegate { TapVirtualKey(virtualKey, label); });
    }

    private static void HandleDirectionVolumeFallback(int virtualKey, bool keyUp)
    {
        if (virtualKey != 0x26 && virtualKey != 0x28) return;
        if (keyUp)
        {
            if (virtualKey == rawDirectionVk) ResetDirectionVolume();
            return;
        }

        DateTime now = DateTime.UtcNow;
        if (rawDirectionVk != virtualKey || rawDirectionStartedUtc == DateTime.MinValue)
        {
            rawDirectionVk = virtualKey;
            rawDirectionStartedUtc = now;
            rawDirectionVolumeActive = false;
            return;
        }
        if ((now - rawDirectionStartedUtc).TotalMilliseconds < 480) return;
        if ((now - lastDirectionVolumeUtc).TotalMilliseconds < 110) return;
        rawDirectionVolumeActive = true;
        lastDirectionVolumeUtc = now;
        DispatchRawVolume(virtualKey == 0x26 ? 0xAF : 0xAE,
            virtualKey == 0x26 ? "长按上 -> 音量 +" : "长按下 -> 音量 -");
    }

    private static void ResetDirectionVolume()
    {
        if (rawDirectionVolumeActive) Log("RC003 direction volume fallback released");
        rawDirectionVk = 0;
        rawDirectionStartedUtc = DateTime.MinValue;
        rawDirectionVolumeActive = false;
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
            int status = HidP_GetUsages(0, HID_USAGE_PAGE_CONSUMER, 0, usages, ref usageLength, preparsed, report, reportLength);
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
        ReleaseTaskSwitcherAltIfHeld();
        lock (stateLock)
        {
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

    private static void ReleaseTaskSwitcherAltIfHeld()
    {
        lock (taskSwitcherLock)
        {
            if (taskSwitcherTimer != null) { taskSwitcherTimer.Dispose(); taskSwitcherTimer = null; }
            taskSwitcherActive = false;
            taskSwitcherKeysDown.Clear();
        }
        if (!taskSwitcherAltDown) return;
        SetVirtualKeyState(0xA4, true);
        taskSwitcherAltDown = false;
        Log("任务切换器已关闭并释放 Alt");
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
        if (string.IsNullOrWhiteSpace(shortcut))
        {
            return keys;
        }

        string[] parts = shortcut.Split(new char[] { '+', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string raw in parts)
        {
            int vk = VkFromName(raw.Trim());
            if (vk > 0)
            {
                keys.Add(vk);
            }
        }
        return keys;
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
            return ParseHexOrDecimal(value);
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
            {"rctrl", 0xA3}, {"win", 0x5B}, {"lwin", 0x5B}, {"leftwin", 0x5B},
            {"rwin", 0x5C}, {"alt", 0xA4}, {"lalt", 0xA4}, {"shift", 0xA0},
            {"enter", 0x0D}, {"return", 0x0D}, {"esc", 0x1B}, {"escape", 0x1B},
            {"back", 0x08}, {"backspace", 0x08}, {"tab", 0x09}, {"space", 0x20},
            {"left", 0x25}, {"up", 0x26}, {"right", 0x27}, {"down", 0x28},
            {"home", 0x24}, {"end", 0x23}, {"pageup", 0x21}, {"pagedown", 0x22},
            {"apps", 0x5D}, {"menu", 0x5D}, {"oemtilde", 0xC0}, {"oem3", 0xC0},
            {"oemcomma", 0xBC}, {"oemperiod", 0xBE}, {"oemquestion", 0xBF},
            {"volumeup", 0xAF}, {"volumedown", 0xAE}, {"volumemute", 0xAD},
            {"browserback", 0xA6}, {"browserforward", 0xA7}
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
        return vk == 0x5B || vk == 0x5C || vk == 0x5D || vk == 0x25 || vk == 0x26 || vk == 0x27 || vk == 0x28 || vk == 0x21 || vk == 0x22 || vk == 0x23 || vk == 0x24 || vk == 0xA6 || vk == 0xA7 || vk == 0xAD || vk == 0xAE || vk == 0xAF;
    }

    private static bool IsDiagnosticCandidate(int vk)
    {
        return vk == 0x08 || vk == 0x1B || vk == 0xA6 || vk == 0xA7 || vk == 0xAC || vk == 0xAD || vk == 0xAE || vk == 0xAF;
    }

    private static void LoadConfig()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                config = BridgeConfig.Default();
                SaveDefaultConfig();
            }
            else
            {
                string json = File.ReadAllText(ConfigPath);
                config = new JavaScriptSerializer().Deserialize<BridgeConfig>(json) ?? BridgeConfig.Default();
            }
            if (File.Exists(ConfigPath))
            {
                configLastWriteUtc = File.GetLastWriteTimeUtc(ConfigPath);
            }
            int count = config.mappings == null ? 0 : config.mappings.Length;
            Log("Config loaded mappings=" + count);
            BridgeForm.SetStatusText("配置已加载：" + count + " 项映射");
        }
        catch (Exception ex)
        {
            config = BridgeConfig.Default();
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
        try
        {
            if (!File.Exists(ConfigPath))
            {
                return;
            }

            DateTime lastWrite = File.GetLastWriteTimeUtc(ConfigPath);
            if (lastWrite <= configLastWriteUtc)
            {
                return;
            }

            lock (stateLock)
            {
                lastWrite = File.GetLastWriteTimeUtc(ConfigPath);
                if (lastWrite <= configLastWriteUtc)
                {
                    return;
                }
                ReleaseAllShortcuts();
                LoadConfig();
                Log("Config hot reloaded");
            }
        }
        catch (Exception ex)
        {
            Log("Config hot reload failed: " + ex.Message);
        }
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
            RegisterRawInput(Handle);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_INPUT) HandleRawInput(m.LParam);
            base.WndProc(ref m);
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

    public sealed class BridgeConfig
    {
        public int version { get; set; }
        public string notes { get; set; }
        public ShortcutMapping[] mappings { get; set; }

        public static BridgeConfig Default()
        {
            return new BridgeConfig
            {
                version = 1,
                notes = "Default VoxDeck RC003 mapping.",
                mappings = new ShortcutMapping[]
                {
                    new ShortcutMapping { name = "voice", label = "录音键", vk = "F5", scan = "0x3F", enabled = true, suppress = true, mode = "suppress", shortcut = "" },
                    new ShortcutMapping { name = "home", label = "Home 键", vk = "Home", scan = "0x47", enabled = false, suppress = true, mode = "tap", shortcut = "ctrl+alt+h" },
                    new ShortcutMapping { name = "tv", label = "TV 键", vk = "Oemtilde", scan = "0x29", enabled = false, suppress = true, mode = "tap", shortcut = "ctrl+alt+t" },
                    new ShortcutMapping { name = "menu", label = "功能键", vk = "Apps", scan = "0x5D", enabled = false, suppress = true, mode = "tap", shortcut = "apps" },
                    new ShortcutMapping { name = "back", label = "返回 / 删除键", vk = "Backspace", scan = "0x0E", enabled = false, suppress = false, mode = "passthrough", shortcut = "backspace" },
                    new ShortcutMapping { name = "ok", label = "确认键", vk = "Enter", scan = "0x1C", enabled = false, suppress = false, mode = "passthrough", shortcut = "enter" },
                    new ShortcutMapping { name = "up", label = "上键", vk = "Up", scan = "0x48", enabled = false, suppress = false, mode = "passthrough", shortcut = "up" },
                    new ShortcutMapping { name = "down", label = "下键", vk = "Down", scan = "0x50", enabled = false, suppress = false, mode = "passthrough", shortcut = "down" },
                    new ShortcutMapping { name = "left", label = "左键", vk = "Left", scan = "0x4B", enabled = false, suppress = false, mode = "passthrough", shortcut = "left" },
                    new ShortcutMapping { name = "right", label = "右键", vk = "Right", scan = "0x4D", enabled = false, suppress = false, mode = "passthrough", shortcut = "right" }
                }
            };
        }
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

        public string labelOrName()
        {
            return string.IsNullOrWhiteSpace(label) ? (name ?? vk ?? "mapping") : label;
        }
    }

    private sealed class MappingEvent
    {
        public ShortcutMapping mapping;
        public bool keyUp;
        public string command;
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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

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
    private static extern void SwitchToThisWindow(IntPtr window, bool altTab);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] devices, uint deviceCount, uint size);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputData(IntPtr rawInput, uint command, IntPtr data, ref uint size, uint headerSize);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint GetRawInputDeviceInfo(IntPtr device, uint command, IntPtr data, ref uint size);

    [DllImport("hid.dll")]
    private static extern int HidP_GetCaps(IntPtr preparsedData, out HIDP_CAPS capabilities);

    [DllImport("hid.dll")]
    private static extern int HidP_GetUsages(int reportType, ushort usagePage, ushort linkCollection, [Out] ushort[] usageList,
        ref uint usageLength, IntPtr preparsedData, IntPtr report, uint reportLength);
}
