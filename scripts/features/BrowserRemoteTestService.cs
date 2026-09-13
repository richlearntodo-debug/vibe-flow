using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

internal sealed class BrowserRemoteActivationResult
{
    public bool IsSuccess { get; private set; }
    public int ProcessId { get; private set; }
    public IntPtr WindowHandle { get; private set; }
    public string ProcessName { get; private set; }
    public string ErrorCode { get; private set; }

    internal static BrowserRemoteActivationResult Success(int processId, IntPtr windowHandle,
        string processName)
    {
        return new BrowserRemoteActivationResult
        {
            IsSuccess = true,
            ProcessId = processId,
            WindowHandle = windowHandle,
            ProcessName = NormalizeProcessName(processName),
            ErrorCode = ""
        };
    }

    internal static BrowserRemoteActivationResult Failure(string errorCode)
    {
        return new BrowserRemoteActivationResult
        {
            IsSuccess = false,
            ProcessName = "",
            ErrorCode = errorCode ?? "BROWSER-FOREGROUND-MISMATCH"
        };
    }

    internal static string NormalizeProcessName(string value)
    {
        string normalized = (value ?? "").Trim().ToLowerInvariant();
        return normalized.EndsWith(".exe", StringComparison.Ordinal)
            ? normalized.Substring(0, normalized.Length - 4) : normalized;
    }
}

internal sealed class BrowserRemoteDispatchTarget
{
    public int ProcessId { get; private set; }
    public IntPtr WindowHandle { get; private set; }
    public string ProcessName { get; private set; }

    internal BrowserRemoteDispatchTarget(int processId, IntPtr windowHandle, string processName)
    {
        ProcessId = processId;
        WindowHandle = windowHandle;
        ProcessName = BrowserRemoteActivationResult.NormalizeProcessName(processName);
    }
}

internal static class BrowserRemoteRequestFile
{
    private const string RequestMutexName = "Local\\VibeFlowBrowserRemoteRequest";
    private const string ClaimSuffix = ".claimed";
    private const string CancelSuffix = ".cancel";

    internal static bool TryDeleteIfTokenMatches(string path, string expectedToken)
    {
        return WithRequestLock(delegate
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(expectedToken))
                return false;
            string claimedPath = GetClaimPath(path, expectedToken);
            if (TokensEqual(TryReadToken(path), expectedToken))
            {
                try { File.Delete(path); } catch { return false; }
                return !File.Exists(path);
            }
            if (TokensEqual(TryReadToken(claimedPath), expectedToken))
            {
                try { File.Delete(claimedPath); } catch { return false; }
                DeleteCancelMarker(path, expectedToken);
                return !File.Exists(claimedPath);
            }
            return false;
        });
    }

    internal static bool TryWriteAtomic(string path, string contents)
    {
        return WithRequestLock(delegate
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporaryPath, contents ?? "", Encoding.UTF8);
                if (File.Exists(path))
                {
                    try { File.Replace(temporaryPath, path, null); }
                    catch
                    {
                        File.Delete(path);
                        File.Move(temporaryPath, path);
                    }
                }
                else File.Move(temporaryPath, path);
                return true;
            }
            catch { return false; }
            finally
            {
                try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); } catch { }
            }
        });
    }

    internal static bool TryClaim(string path, string expectedToken, out string claimedPath)
    {
        string localClaimedPath = "";
        bool operationResult = WithRequestLock(delegate
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(expectedToken) ||
                !IsSafeToken(expectedToken) || !TokensEqual(TryReadToken(path), expectedToken)) return false;
            localClaimedPath = GetClaimPath(path, expectedToken);
            try
            {
                if (File.Exists(localClaimedPath)) return false;
                DeleteCancelMarker(path, expectedToken);
                File.Move(path, localClaimedPath);
                return true;
            }
            catch
            {
                localClaimedPath = "";
                return false;
            }
        });
        claimedPath = operationResult ? localClaimedPath : "";
        return operationResult;
    }

    internal static bool TryCancel(string path, string expectedToken)
    {
        return WithRequestLock(delegate
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(expectedToken) ||
                !IsSafeToken(expectedToken)) return false;
            if (TokensEqual(TryReadToken(path), expectedToken))
            {
                try { File.Delete(path); } catch { return false; }
                return true;
            }
            string claimedPath = GetClaimPath(path, expectedToken);
            if (!TokensEqual(TryReadToken(claimedPath), expectedToken)) return false;
            try
            {
                File.WriteAllText(GetCancelPath(path, expectedToken), expectedToken, Encoding.UTF8);
                return true;
            }
            catch { return false; }
        });
    }

    internal static bool IsClaimed(string claimedPath, string expectedToken)
    {
        return WithRequestLock(delegate { return TokensEqual(TryReadToken(claimedPath), expectedToken) &&
            !File.Exists(GetCancelPathFromClaim(claimedPath)); });
    }

    internal static bool TryExecuteClaimed(string claimedPath, string requestPath,
        string expectedToken, Func<bool> action, out bool canceled)
    {
        bool localCanceled = false;
        bool operationResult = WithRequestLock(delegate
        {
            if (string.IsNullOrWhiteSpace(claimedPath) || string.IsNullOrWhiteSpace(requestPath) ||
                string.IsNullOrWhiteSpace(expectedToken) || !TokensEqual(TryReadToken(claimedPath), expectedToken))
            {
                localCanceled = true;
                return false;
            }
            string cancelPath = GetCancelPath(requestPath, expectedToken);
            if (File.Exists(cancelPath))
            {
                localCanceled = true;
                CleanupClaim(claimedPath, cancelPath);
                return false;
            }
            bool actionResult;
            try { actionResult = action != null && action(); }
            catch { actionResult = false; }
            CleanupClaim(claimedPath, cancelPath);
            return actionResult;
        });
        canceled = localCanceled;
        return operationResult;
    }

    private static string TryReadToken(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return "";
        try
        {
            Dictionary<string, object> document = new JavaScriptSerializer()
                .Deserialize<Dictionary<string, object>>(File.ReadAllText(path, Encoding.UTF8));
            object tokenValue;
            string actualToken = document != null && document.TryGetValue("token", out tokenValue)
                ? Convert.ToString(tokenValue) : "";
            return actualToken ?? "";
        }
        catch { return ""; }
    }

    private static void CleanupClaim(string claimedPath, string cancelPath)
    {
        try { if (File.Exists(claimedPath)) File.Delete(claimedPath); } catch { }
        try { if (File.Exists(cancelPath)) File.Delete(cancelPath); } catch { }
    }

    private static void DeleteCancelMarker(string requestPath, string token)
    {
        try
        {
            string cancelPath = GetCancelPath(requestPath, token);
            if (File.Exists(cancelPath)) File.Delete(cancelPath);
        }
        catch { }
    }

    private static string GetClaimPath(string path, string token)
    {
        return path + "." + token + ClaimSuffix;
    }

    private static string GetCancelPath(string path, string token)
    {
        return path + "." + token + CancelSuffix;
    }

    private static string GetCancelPathFromClaim(string claimedPath)
    {
        int suffixIndex = claimedPath == null ? -1 : claimedPath.LastIndexOf(ClaimSuffix,
            StringComparison.OrdinalIgnoreCase);
        return suffixIndex < 0 ? "" : claimedPath.Substring(0, suffixIndex) + CancelSuffix;
    }

    private static bool IsSafeToken(string token)
    {
        foreach (char value in token)
            if (!(char.IsLetterOrDigit(value) || value == '-' || value == '_')) return false;
        return true;
    }

    private static bool TokensEqual(string left, string right)
    {
        return !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) &&
            string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static bool WithRequestLock(Func<bool> action)
    {
        Mutex mutex = null;
        bool acquired = false;
        try
        {
            mutex = new Mutex(false, RequestMutexName);
            try { acquired = mutex.WaitOne(2000); }
            catch (AbandonedMutexException) { acquired = true; }
            return acquired && action != null && action();
        }
        catch { return false; }
        finally
        {
            if (acquired && mutex != null)
                try { mutex.ReleaseMutex(); } catch { }
            if (mutex != null) mutex.Dispose();
        }
    }
}

internal static class BrowserRemoteDispatchCoordinator
{
    internal static bool TryDispatch(Func<bool> recordingActive,
        Func<bool> existingBridgeReady, Func<bool> exposeRequest, Action clearRequest)
    {
        if (Canceled(recordingActive)) return false;
        bool ready;
        try { ready = existingBridgeReady != null && existingBridgeReady(); }
        catch { ready = false; }
        if (!ready || Canceled(recordingActive)) return false;

        bool exposed;
        try { exposed = exposeRequest != null && exposeRequest(); }
        catch { exposed = false; }
        if (!exposed)
        {
            Clear(clearRequest);
            return false;
        }
        if (!Canceled(recordingActive)) return true;
        Clear(clearRequest);
        return false;
    }

    private static bool Canceled(Func<bool> recordingActive)
    {
        try { return recordingActive == null || recordingActive(); }
        catch { return true; }
    }

    private static void Clear(Action clearRequest)
    {
        try { if (clearRequest != null) clearRequest(); }
        catch { }
    }
}

internal interface IBrowserRemoteTestBackend
{
    BrowserRemoteActivationResult ActivateAndVerify(string processName, int timeoutMs,
        Func<bool> cancellationRequested);
}

internal sealed class BrowserRemoteTestService
{
    private readonly IBrowserRemoteTestBackend backend;
    private readonly Func<bool> recordingActive;
    private readonly Func<string, string, BrowserRemoteDispatchTarget, Action<ActionResult>, bool> dispatch;
    private readonly Action<string> log;

    internal BrowserRemoteTestService(IBrowserRemoteTestBackend backend, Func<bool> recordingActive,
        Func<string, string, BrowserRemoteDispatchTarget, Action<ActionResult>, bool> dispatch,
        Action<string> log)
    {
        this.backend = backend;
        this.recordingActive = recordingActive ?? delegate { return true; };
        this.dispatch = dispatch;
        this.log = log ?? delegate { };
    }

    internal ActionResult Begin(string browserId, string actionLabel, string action,
        Action<ActionResult> completion)
    {
        string processName;
        string browserName;
        if (!TryResolveBrowser(browserId, out processName, out browserName))
            return Failure(actionLabel, "浏览器未验证，本次未派发按键",
                "逐项测试当前只允许 Chrome 或 Edge", "选择 Chrome 或 Edge 后重试",
                "BROWSER-NOT-VERIFIED");
        if (!BrowserProfileTemplate.IsTestableAction(action))
            return Failure(actionLabel, "动作不在 Browser Remote Lite 安全列表中，未派发按键",
                "测试动作无效", "重新选择列表中的动作", "BROWSER-TEST-ACTION-INVALID");
        if (IsRecording()) return VoiceCanceled(actionLabel, browserName);
        if (backend == null)
            return Failure(actionLabel, "无法检查浏览器窗口，未派发按键",
                "浏览器检测服务不可用", "关闭窗口后重试", "BROWSER-BACKEND-MISSING");

        BrowserRemoteActivationResult activation = backend.ActivateAndVerify(processName, 900,
            delegate { return IsRecording(); });
        if (IsRecording()) return VoiceCanceled(actionLabel, browserName);
        if (activation == null || !activation.IsSuccess)
            return ActivationFailure(actionLabel, browserName, activation == null
                ? "BROWSER-FOREGROUND-MISMATCH" : activation.ErrorCode);
        if (!string.Equals(BrowserRemoteActivationResult.NormalizeProcessName(activation.ProcessName),
            processName, StringComparison.OrdinalIgnoreCase))
            return ActivationFailure(actionLabel, browserName, "BROWSER-FOREGROUND-MISMATCH");
        if (IsRecording()) return VoiceCanceled(actionLabel, browserName);
        var target = new BrowserRemoteDispatchTarget(activation.ProcessId,
            activation.WindowHandle, activation.ProcessName);
        if (dispatch == null || !dispatch(actionLabel, action, target, completion))
            return Failure(actionLabel, "按键测试未派发",
                "按键服务当前正忙或请求文件无法写入", "等待当前操作完成后重试",
                "BROWSER-TEST-DISPATCH-FAILED");

        log("BROWSER TEST queued browser=" + processName + " action=" + SafeAction(action));
        return ActionResult.Create("测试浏览器动作", browserName, ActionState.Running,
            actionLabel + "已准备，等待按键服务回执", "", "", "");
    }

    private bool IsRecording()
    {
        try { return recordingActive(); }
        catch { return true; }
    }

    private static ActionResult VoiceCanceled(string actionLabel, string browserName)
    {
        return ActionResult.Create("测试浏览器动作", browserName, ActionState.Canceled,
            "录音正在进行，本次未派发按键", "录音操作优先",
            "录音结束后重新测试 " + actionLabel, "BROWSER-TEST-CANCELED-VOICE");
    }

    private static ActionResult ActivationFailure(string actionLabel, string browserName,
        string errorCode)
    {
        if (string.Equals(errorCode, "BROWSER-TEST-CANCELED-VOICE", StringComparison.Ordinal))
            return VoiceCanceled(actionLabel, browserName);
        if (string.Equals(errorCode, "BROWSER-WINDOW-NOT-FOUND", StringComparison.Ordinal))
            return Failure(actionLabel, "未找到可见的 " + browserName + " 窗口，未派发按键",
                "浏览器未运行或窗口不可见", "打开浏览器窗口后重新测试", errorCode);
        return Failure(actionLabel, "未能锁定正确的 " + browserName + " 窗口，未派发按键",
            "浏览器没有成为已验证的前台进程", "切到浏览器后重新测试",
            "BROWSER-FOREGROUND-MISMATCH");
    }

    private static ActionResult Failure(string actionLabel, string message, string reason,
        string recovery, string code)
    {
        return ActionResult.Create("测试浏览器动作", actionLabel, ActionState.Error,
            message, reason, recovery, code);
    }

    private static bool TryResolveBrowser(string browserId, out string processName,
        out string browserName)
    {
        string value = (browserId ?? "").Trim().ToLowerInvariant();
        if (value == "chrome")
        {
            processName = "chrome";
            browserName = "Chrome";
            return true;
        }
        if (value == "edge" || value == "msedge")
        {
            processName = "msedge";
            browserName = "Edge";
            return true;
        }
        processName = "";
        browserName = "浏览器";
        return false;
    }

    private static string SafeAction(string action)
    {
        string value = action ?? "";
        return BrowserProfileTemplate.IsTestableAction(value) ? value : "invalid";
    }
}

internal sealed class WindowsBrowserRemoteTestBackend : IBrowserRemoteTestBackend
{
    public BrowserRemoteActivationResult ActivateAndVerify(string processName, int timeoutMs,
        Func<bool> cancellationRequested)
    {
        string expected = BrowserRemoteActivationResult.NormalizeProcessName(processName);
        Process[] processes;
        try { processes = Process.GetProcessesByName(expected); }
        catch { return BrowserRemoteActivationResult.Failure("BROWSER-WINDOW-NOT-FOUND"); }
        try
        {
            List<BrowserWindowCandidate> windows = VisibleWindows(processes);
            if (windows.Count == 0)
                return BrowserRemoteActivationResult.Failure("BROWSER-WINDOW-NOT-FOUND");
            Stopwatch timer = Stopwatch.StartNew();
            while (timer.ElapsedMilliseconds < Math.Max(100, timeoutMs))
            {
                if (Canceled(cancellationRequested))
                    return BrowserRemoteActivationResult.Failure("BROWSER-TEST-CANCELED-VOICE");
                foreach (BrowserWindowCandidate window in windows)
                {
                    if (Canceled(cancellationRequested))
                        return BrowserRemoteActivationResult.Failure("BROWSER-TEST-CANCELED-VOICE");
                    try { ShowWindowAsync(window.Handle, 9); } catch { }
                    try { SetForegroundWindow(window.Handle); } catch { }
                    BrowserRemoteActivationResult verified = VerifyForeground(window, expected);
                    if (verified.IsSuccess) return verified;
                }
                Thread.Sleep(25);
            }
            return BrowserRemoteActivationResult.Failure("BROWSER-FOREGROUND-MISMATCH");
        }
        finally
        {
            foreach (Process process in processes) process.Dispose();
        }
    }

    private static BrowserRemoteActivationResult VerifyForeground(BrowserWindowCandidate expectedWindow,
        string expectedProcessName)
    {
        IntPtr foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero || foreground != expectedWindow.Handle)
            return BrowserRemoteActivationResult.Failure("BROWSER-FOREGROUND-MISMATCH");
        uint processId;
        GetWindowThreadProcessId(foreground, out processId);
        if (processId != (uint)expectedWindow.ProcessId)
            return BrowserRemoteActivationResult.Failure("BROWSER-FOREGROUND-MISMATCH");
        try
        {
            using (Process process = Process.GetProcessById((int)processId))
            {
                string actual = BrowserRemoteActivationResult.NormalizeProcessName(process.ProcessName);
                if (!string.Equals(actual, expectedProcessName, StringComparison.OrdinalIgnoreCase))
                    return BrowserRemoteActivationResult.Failure("BROWSER-FOREGROUND-MISMATCH");
                return BrowserRemoteActivationResult.Success((int)processId, foreground, actual);
            }
        }
        catch { return BrowserRemoteActivationResult.Failure("BROWSER-FOREGROUND-MISMATCH"); }
    }

    private static List<BrowserWindowCandidate> VisibleWindows(Process[] processes)
    {
        var processIds = new HashSet<int>();
        var windows = new List<BrowserWindowCandidate>();
        foreach (Process process in processes)
            try { processIds.Add(process.Id); } catch { }
        try
        {
            EnumWindows(delegate(IntPtr handle, IntPtr parameter)
            {
                if (!IsWindowVisible(handle)) return true;
                uint processId;
                GetWindowThreadProcessId(handle, out processId);
                if (processIds.Contains((int)processId))
                    windows.Add(new BrowserWindowCandidate((int)processId, handle));
                return true;
            }, IntPtr.Zero);
        }
        catch { }
        return windows;
    }

    private static bool Canceled(Func<bool> cancellationRequested)
    {
        try { return cancellationRequested != null && cancellationRequested(); }
        catch { return true; }
    }

    private sealed class BrowserWindowCandidate
    {
        internal int ProcessId;
        internal IntPtr Handle;
        internal BrowserWindowCandidate(int processId, IntPtr handle)
        {
            ProcessId = processId;
            Handle = handle;
        }
    }

    private delegate bool EnumWindowsCallback(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll")]
    private static extern bool ShowWindowAsync(IntPtr window, int command);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
}
