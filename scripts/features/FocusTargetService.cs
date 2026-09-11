using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;

internal sealed class FocusAutomationStepResult
{
    public bool IsSuccess { get; private set; }
    public string ErrorCode { get; private set; }
    public int ProcessId { get; private set; }
    public IntPtr WindowHandle { get; private set; }

    private FocusAutomationStepResult() { }

    internal static FocusAutomationStepResult Success(int processId, IntPtr windowHandle)
    {
        return new FocusAutomationStepResult
        {
            IsSuccess = true,
            ErrorCode = "",
            ProcessId = processId,
            WindowHandle = windowHandle
        };
    }

    internal static FocusAutomationStepResult Failure(string errorCode)
    {
        return new FocusAutomationStepResult
        {
            IsSuccess = false,
            ErrorCode = FocusTargetService.SafeErrorCode(errorCode, "FOCUS-FAILED"),
            ProcessId = 0,
            WindowHandle = IntPtr.Zero
        };
    }
}

internal sealed class FocusLearningCaptureResult
{
    public bool IsSuccess { get; private set; }
    public string ErrorCode { get; private set; }
    public FocusTargetDescriptor Descriptor { get; private set; }

    private FocusLearningCaptureResult() { }

    internal static FocusLearningCaptureResult Success(FocusTargetDescriptor descriptor)
    {
        return new FocusLearningCaptureResult
        {
            IsSuccess = true,
            ErrorCode = "",
            Descriptor = descriptor
        };
    }

    internal static FocusLearningCaptureResult Failure(string errorCode)
    {
        return new FocusLearningCaptureResult
        {
            IsSuccess = false,
            ErrorCode = FocusTargetService.SafeErrorCode(errorCode, "FOCUS-LEARN-FAILED"),
            Descriptor = null
        };
    }
}

internal sealed class FocusApplicationChoice
{
    public string ProcessName { get; private set; }
    public string DisplayName { get; private set; }

    internal FocusApplicationChoice(string processName)
    {
        ProcessName = FocusTargetDescriptor.NormalizeProcessName(processName);
        DisplayName = FriendlyProcessName(ProcessName);
    }

    public override string ToString()
    {
        return DisplayName + "  (" + ProcessName + ")";
    }

    private static string FriendlyProcessName(string processName)
    {
        if (string.Equals(processName, "cursor", StringComparison.OrdinalIgnoreCase)) return "Cursor";
        if (string.Equals(processName, "code", StringComparison.OrdinalIgnoreCase)) return "VS Code";
        if (string.Equals(processName, "chatgpt", StringComparison.OrdinalIgnoreCase)) return "ChatGPT";
        if (string.Equals(processName, "notepad", StringComparison.OrdinalIgnoreCase)) return "记事本";
        if (string.Equals(processName, "chrome", StringComparison.OrdinalIgnoreCase)) return "Chrome";
        if (string.Equals(processName, "msedge", StringComparison.OrdinalIgnoreCase)) return "Edge";
        return string.IsNullOrWhiteSpace(processName) ? "未知应用" : processName;
    }
}

internal interface IFocusAutomationBackend
{
    FocusAutomationStepResult ActivateApplication(FocusTargetDescriptor target, int timeoutMs,
        long commitEpoch, Func<bool> cancellationRequested);
    FocusAutomationStepResult FocusAndVerify(FocusTargetDescriptor target,
        FocusAutomationStepResult activation, int timeoutMs, long commitEpoch,
        Func<bool> cancellationRequested);
}

internal sealed class FocusTargetService
{
    private readonly IFocusAutomationBackend backend;
    private readonly Func<bool> isVoiceKeyHeld;
    private readonly RecordingPriorityCommitGate recordingCommitGate;
    private readonly Action<string> log;
    private readonly Func<string> foregroundProcessNameProvider;
    private int requestActive;
    private long cancellationEpoch;
    private long voiceCancellationEpoch;

    internal FocusTargetService(IFocusAutomationBackend backend, Func<bool> isVoiceKeyHeld,
        Action<string> safeLog, RecordingPriorityCommitGate commitGate = null,
        Func<string> foregroundProcessNameProvider = null)
    {
        if (backend == null) throw new ArgumentNullException("backend");
        this.backend = backend;
        this.isVoiceKeyHeld = isVoiceKeyHeld ?? delegate { return false; };
        recordingCommitGate = commitGate ?? new RecordingPriorityCommitGate(this.isVoiceKeyHeld);
        log = safeLog ?? delegate { };
        this.foregroundProcessNameProvider = foregroundProcessNameProvider ?? GetForegroundProcessName;
    }

    // Voice startup must never infer a safe destination from a window title,
    // clipboard contents, or screen coordinates.  It is ready only when the
    // saved target is present, the foreground process still matches, the
    // focused element matches the descriptor and carries the proof its strategy
    // requires (writable for a normal target, the focused text surface for a
    // focus-only console or terminal target), and the element still owns
    // keyboard focus.
    internal static bool EvaluateVoiceTargetObservation(bool targetConfigured,
        bool foregroundProcessMatches, bool focusedEditable, bool hasKeyboardFocus)
    {
        return targetConfigured && foregroundProcessMatches && focusedEditable && hasKeyboardFocus;
    }

    // A single global default target is not enough: a user who learned a target for
    // Chrome still had dictation pulled into the ChatGPT default window while working
    // in Chrome. The wake path therefore prefers a verified target that belongs to the
    // current foreground process and only falls back to the configured default when no
    // such target exists.
    internal static FocusTargetDescriptor SelectVoiceTarget(IList<FocusTargetDescriptor> targets,
        string defaultTargetId, string foregroundProcess)
    {
        if (targets == null) return null;
        FocusTargetDescriptor configured = FindTargetById(targets, defaultTargetId);
        string foreground = FocusTargetDescriptor.NormalizeProcessName(foregroundProcess);
        if (foreground.Length == 0) return configured;
        FocusTargetDescriptor match = null;
        foreach (FocusTargetDescriptor candidate in targets)
        {
            if (candidate == null) continue;
            if (!string.Equals(FocusTargetDescriptor.NormalizeProcessName(candidate.ProcessName),
                    foreground, StringComparison.OrdinalIgnoreCase)) continue;
            if (!candidate.LastVerifiedUtc.HasValue) continue;
            if (match == null || string.Equals(candidate.Id, defaultTargetId, StringComparison.OrdinalIgnoreCase))
                match = candidate;
        }
        return match ?? configured;
    }

    private static FocusTargetDescriptor FindTargetById(IList<FocusTargetDescriptor> targets, string id)
    {
        if (targets == null || string.IsNullOrWhiteSpace(id)) return null;
        foreach (FocusTargetDescriptor candidate in targets)
        {
            if (candidate != null && string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase))
                return candidate;
        }
        return null;
    }

    // ChatGPT renders its composer as a web contenteditable exposed through
    // UI Automation.  The class gains a transient "ProseMirror-focused"
    // token when focused, so matching must keep only the stable base class.
    internal static bool IsKnownEditableWebTarget(string processName, string className,
        string automationId, string controlType)
    {
        string process = FocusTargetDescriptor.NormalizeProcessName(processName);
        string type = (controlType ?? "").Trim();
        if (!string.Equals(type, "Edit", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(type, "ControlType.Edit", StringComparison.OrdinalIgnoreCase)) return false;
        string cls = (className ?? "").Trim();
        string aid = (automationId ?? "").Trim();
        // Only ChatGPT is verified in this checkout. Chromium browser support
        // remains on the manual learning path until a real UI check exists.
        if (process != "chatgpt") return false;
        if (cls.Equals("ProseMirror", StringComparison.OrdinalIgnoreCase) ||
            cls.StartsWith("ProseMirror ", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    internal static string NormalizeStoredClassName(string processName, string className)
    {
        string cls = (className ?? "").Trim();
        if (IsKnownEditableWebTarget(processName, cls, "", "Edit") &&
            cls.StartsWith("ProseMirror", StringComparison.OrdinalIgnoreCase)) return "ProseMirror";
        return cls;
    }

    internal static bool MatchesStoredClassName(string storedClassName, string currentClassName,
        string processName)
    {
        string stored = (storedClassName ?? "").Trim();
        string current = (currentClassName ?? "").Trim();
        if (string.Equals(stored, current, StringComparison.Ordinal)) return true;
        return IsKnownEditableWebTarget(processName, stored, "", "Edit") &&
            current.StartsWith(stored + " ", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool MatchesStoredParentFingerprint(FocusTargetDescriptor target,
        string currentParentFingerprint, string currentClassName)
    {
        if (target == null) return false;
        string stored = (target.ParentFingerprint ?? "").Trim();
        string current = (currentParentFingerprint ?? "").Trim();
        if (stored.Length == 0 || string.Equals(stored, current, StringComparison.Ordinal)) return true;

        // ChatGPT's web shell changes ancestor CSS class tokens between client
        // updates. Keep the verified adapter anchored to the process and the
        // stable ProseMirror edit class; caller-side checks still require one
        // writable candidate, selected-window ownership, and keyboard focus.
        return IsKnownEditableWebTarget(target.ProcessName, currentClassName,
                target.AutomationId, target.ControlType) &&
            MatchesStoredClassName(target.ClassName, currentClassName, target.ProcessName);
    }

    internal bool ObserveFocusedTarget(FocusTargetDescriptor target, out string errorCode)
    {
        errorCode = "FOCUS-TARGET-MISSING";
        if (target == null || !target.TryValidateForExecution(out errorCode)) return false;
        try
        {
            AutomationElement focused = AutomationElement.FocusedElement;
            if (focused == null)
            {
                errorCode = "FOCUS-NO-FOCUS";
                return false;
            }

            AutomationElement.AutomationElementInformation current = focused.Current;
            string foregroundProcess = FocusTargetDescriptor.NormalizeProcessName(CurrentForegroundProcessName());
            bool foregroundMatches = string.Equals(foregroundProcess, target.ProcessName,
                StringComparison.OrdinalIgnoreCase);
            bool descriptorMatches = false;
            bool writable = false;
            try
            {
                descriptorMatches = WindowsUiaFocusAutomationBackend.MatchesForObservation(
                    target, focused, current.ProcessId);
                writable = descriptorMatches &&
                    WindowsUiaFocusAutomationBackend.SatisfiesTargetEvidence(target, focused);
            }
            catch (ElementNotAvailableException)
            {
                errorCode = "FOCUS-TARGET-STALE";
                return false;
            }

            bool ready = EvaluateVoiceTargetObservation(true, foregroundMatches,
                descriptorMatches && writable, current.HasKeyboardFocus);
            if (ready)
            {
                errorCode = "";
                return true;
            }
            if (!foregroundMatches) errorCode = "FOCUS-PROCESS-MISMATCH";
            else if (!descriptorMatches) errorCode = "FOCUS-TARGET-STALE";
            else if (!writable) errorCode = "FOCUS-TARGET-NOT-EDITABLE";
            else if (!current.HasKeyboardFocus) errorCode = "FOCUS-FOCUS-LOST";
            else errorCode = "FOCUS-TARGET-UNCONFIRMED";
            return false;
        }
        catch (ElementNotAvailableException)
        {
            errorCode = "FOCUS-TARGET-STALE";
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            errorCode = "FOCUS-AUTOMATION-DENIED";
            return false;
        }
        catch (InvalidOperationException)
        {
            errorCode = "FOCUS-TARGET-STALE";
            return false;
        }
        catch (COMException)
        {
            errorCode = "FOCUS-AUTOMATION-DENIED";
            return false;
        }
    }

    internal ActionResult Execute(FocusTargetDescriptor target, int timeoutMs)
    {
        return ExecuteCore(target, timeoutMs, false, null);
    }

    internal ActionResult Execute(FocusTargetDescriptor target, int timeoutMs,
        Func<bool> cancellationRequested)
    {
        return ExecuteCore(target, timeoutMs, false, cancellationRequested);
    }

    internal ActionResult ExecuteForVerification(FocusTargetDescriptor target, int timeoutMs)
    {
        return ExecuteCore(target, timeoutMs, true, null);
    }

    internal ActionResult ExecuteForVerification(FocusTargetDescriptor target, int timeoutMs,
        Func<bool> cancellationRequested)
    {
        return ExecuteCore(target, timeoutMs, true, cancellationRequested);
    }

    internal ActionResult RestoreVerifiedTarget(FocusTargetDescriptor target, int timeoutMs,
        bool allowForegroundActivation)
    {
        string validationCode = "FOCUS-TARGET-MISSING";
        if (target == null || !target.TryValidateForExecution(out validationCode))
            return Finish(target, ActionState.Error, "未恢复工作流",
                "工作流描述无效或尚未验证", "重新测试工作流", validationCode, 0);

        WindowsUiaFocusAutomationBackend windowsBackend = backend as WindowsUiaFocusAutomationBackend;
        if (windowsBackend == null)
            return Finish(target, ActionState.Error, "未恢复工作流",
                "当前环境不支持 Windows 焦点恢复", "重新测试工作流",
                "FOCUS-RESTORE-UNAVAILABLE", 0);

        var elapsed = Stopwatch.StartNew();
        FocusAutomationStepResult restored = windowsBackend.RestoreVerifiedTarget(target,
            Math.Max(1, timeoutMs), allowForegroundActivation);
        if (restored == null || !restored.IsSuccess)
        {
            string code = restored == null ? "FOCUS-TARGET-STALE" : restored.ErrorCode;
            return Finish(target, ActionState.Error, "未恢复工作流",
                code == "FOCUS-FOREGROUND-CHANGED" ? "用户已切换到其他应用，本次未强抢焦点" :
                    "语音工具打开后，已验证输入控件未能重新获得焦点",
                code == "FOCUS-FOREGROUND-CHANGED" ? "回到目标输入框后重试" :
                    "重新测试工作流后重试",
                code, elapsed.ElapsedMilliseconds);
        }
        return Finish(target, ActionState.Success, "工作流焦点已恢复",
            "", "", "", elapsed.ElapsedMilliseconds);
    }

    private ActionResult ExecuteCore(FocusTargetDescriptor target, int timeoutMs, bool allowUnverified,
        Func<bool> externalCancellationRequested)
    {
        string validationCode = "FOCUS-TARGET-MISSING";
        if (target == null || !(allowUnverified
                ? target.TryValidateForVerification(out validationCode)
                : target.TryValidateForExecution(out validationCode)))
            return Finish(target, ActionState.Error, "未锁定工作流，未发送按键",
                "工作流描述无效或不受支持", "重新学习工作流", validationCode, 0);
        if (Interlocked.CompareExchange(ref requestActive, 1, 0) != 0)
            return Finish(target, ActionState.Warning, "已有目标锁定操作正在执行",
                "同一时间只能锁定一个工作流", "等待当前操作结束后重试", "FOCUS-BUSY", 0);

        var elapsed = Stopwatch.StartNew();
        long startCancellationEpoch = Interlocked.Read(ref cancellationEpoch);
        long startVoiceEpoch = Interlocked.Read(ref voiceCancellationEpoch);
        long startCommitEpoch = recordingCommitGate.CaptureEpoch();
        string initialForegroundProcess = CurrentForegroundProcessName();
        bool targetForegroundSeen = string.Equals(
            FocusTargetDescriptor.NormalizeProcessName(initialForegroundProcess),
            FocusTargetDescriptor.NormalizeProcessName(target.ProcessName),
            StringComparison.OrdinalIgnoreCase);
        Func<bool> cancellationRequested = delegate
        {
            if (SafeCancellationRequested(externalCancellationRequested)) return true;
            return ShouldCancelForForegroundChange(initialForegroundProcess,
                CurrentForegroundProcessName(), target.ProcessName, ref targetForegroundSeen);
        };
        try
        {
            ActionResult canceled = CancellationResult(target, startCancellationEpoch, startVoiceEpoch,
                cancellationRequested, elapsed.ElapsedMilliseconds);
            if (canceled != null) return canceled;
            if (timeoutMs < 1) timeoutMs = 1;

            FocusAutomationStepResult activation = backend.ActivateApplication(target, timeoutMs,
                startCommitEpoch,
                delegate { return IsCancellationRequested(startCancellationEpoch, startVoiceEpoch,
                    cancellationRequested); });
            canceled = CancellationResult(target, startCancellationEpoch, startVoiceEpoch,
                cancellationRequested, elapsed.ElapsedMilliseconds);
            if (canceled != null) return canceled;
            if (activation == null || !activation.IsSuccess)
            {
                string code = activation == null ? "FOCUS-ACTIVATION-FAILED" : activation.ErrorCode;
                return FailureForCode(target, code, elapsed.ElapsedMilliseconds);
            }
            if (elapsed.ElapsedMilliseconds >= timeoutMs)
                return Finish(target, ActionState.Error, "未锁定工作流，未发送按键",
                    "目标应用未在限定时间内完成激活", "重试；仍失败请仅激活应用后手动点击输入框",
                    "FOCUS-TIMEOUT", elapsed.ElapsedMilliseconds);

            int remainingMs = Math.Max(1, timeoutMs - (int)elapsed.ElapsedMilliseconds);
            FocusAutomationStepResult focused = backend.FocusAndVerify(target, activation, remainingMs,
                startCommitEpoch,
                delegate { return IsCancellationRequested(startCancellationEpoch, startVoiceEpoch,
                    cancellationRequested); });
            canceled = CancellationResult(target, startCancellationEpoch, startVoiceEpoch,
                cancellationRequested, elapsed.ElapsedMilliseconds);
            if (canceled != null) return canceled;
            if (elapsed.ElapsedMilliseconds >= timeoutMs)
                return Finish(target, ActionState.Error, "未锁定工作流，未发送按键",
                    "输入控件未在限定时间内获得焦点", "重新学习工作流后重试",
                    "FOCUS-TIMEOUT", elapsed.ElapsedMilliseconds);
            if (focused == null || !focused.IsSuccess)
            {
                string code = focused == null ? "FOCUS-TARGET-STALE" : focused.ErrorCode;
                return FailureForCode(target, code, elapsed.ElapsedMilliseconds);
            }

            return Finish(target, ActionState.Success, "目标已锁定：" + target.Name,
                "", "", "", elapsed.ElapsedMilliseconds);
        }
        catch
        {
            return Finish(target, ActionState.Error, "未锁定工作流，未发送按键",
                "目标应用拒绝了自动聚焦或已退出", "重新打开应用并测试目标",
                "FOCUS-UNAVAILABLE", elapsed.ElapsedMilliseconds);
        }
        finally
        {
            Interlocked.Exchange(ref requestActive, 0);
        }
    }

    internal void CancelCurrent()
    {
        Interlocked.Increment(ref cancellationEpoch);
    }

    internal void CancelForRecording()
    {
        recordingCommitGate.CancelForRecording();
        Interlocked.Increment(ref voiceCancellationEpoch);
        Interlocked.Increment(ref cancellationEpoch);
    }

    internal static string VerificationStatusText(FocusTargetDescriptor target)
    {
        if (target == null || !target.LastVerifiedUtc.HasValue) return "待测试";
        return IsProcessRunning(target.ProcessName) ? "已验证" : "已验证 · 应用未运行";
    }

    private static bool IsProcessRunning(string processName)
    {
        string normalized = FocusTargetDescriptor.NormalizeProcessName(processName);
        if (normalized.Length == 0) return false;
        try
        {
            Process[] processes = Process.GetProcessesByName(normalized);
            bool running = processes != null && processes.Length > 0;
            if (processes != null)
                foreach (Process process in processes) process.Dispose();
            return running;
        }
        catch { return false; }
    }

    private static string GetForegroundProcessName()
    {
        try
        {
            IntPtr foreground = GetForegroundWindow();
            if (foreground == IntPtr.Zero) return "";
            uint processId;
            GetWindowThreadProcessId(foreground, out processId);
            if (processId == 0) return "";
            using (Process process = Process.GetProcessById((int)processId))
                return process.ProcessName ?? "";
        }
        catch { return ""; }
    }

    private string CurrentForegroundProcessName()
    {
        try { return foregroundProcessNameProvider(); }
        catch { return ""; }
    }

    private bool IsCancellationRequested(long startCancellationEpoch, long startVoiceEpoch,
        Func<bool> externalCancellationRequested)
    {
        return Interlocked.Read(ref cancellationEpoch) != startCancellationEpoch ||
            Interlocked.Read(ref voiceCancellationEpoch) != startVoiceEpoch || SafeVoiceHeld() ||
            SafeCancellationRequested(externalCancellationRequested);
    }

    private ActionResult CancellationResult(FocusTargetDescriptor target, long startCancellationEpoch,
        long startVoiceEpoch, Func<bool> externalCancellationRequested, long elapsedMs)
    {
        bool voice = Interlocked.Read(ref voiceCancellationEpoch) != startVoiceEpoch || SafeVoiceHeld();
        if (!voice && Interlocked.Read(ref cancellationEpoch) == startCancellationEpoch &&
            !SafeCancellationRequested(externalCancellationRequested)) return null;
        return Finish(target, ActionState.Canceled,
            voice ? "录音已开始，目标锁定已取消" : "目标锁定已取消",
            voice ? "录音操作优先，本次未继续抢占焦点" : "本次未继续执行焦点操作",
            voice ? "录音结束后重新锁定目标" : "需要时重新锁定目标",
            voice ? "FOCUS-CANCELED-VOICE" : "FOCUS-CANCELED", elapsedMs);
    }

    private static bool SafeCancellationRequested(Func<bool> cancellationRequested)
    {
        try { return cancellationRequested != null && cancellationRequested(); }
        catch { return true; }
    }

    private bool SafeVoiceHeld()
    {
        try { return isVoiceKeyHeld(); }
        catch { return true; }
    }

    private ActionResult FailureForCode(FocusTargetDescriptor target, string errorCode, long elapsedMs)
    {
        string code = SafeErrorCode(errorCode, "FOCUS-FAILED");
        string reason = code == "FOCUS-APP-NOT-RUNNING" ? "目标应用尚未运行" :
            code == "FOCUS-PROCESS-MISMATCH" ? "无法确认当前前台窗口属于目标应用" :
            code == "FOCUS-TARGET-STALE" ? "已学习的输入控件已经变化或不可用" :
            code == "FOCUS-TARGET-AMBIGUOUS" ? "找到多个匹配的输入控件，无法确认正确目标" :
            code == "FOCUS-TARGET-UNVERIFIED" ? "工作流尚未完成一次真实测试" :
            code == "FOCUS-TARGET-NOT-EDITABLE" ? "目标控件不具备可验证的编辑语义" :
            code == "FOCUS-AUTOMATION-DENIED" ? "Windows 不允许访问该应用的输入控件" :
            "未能验证目标应用和输入控件";
        string recovery = code == "FOCUS-APP-NOT-RUNNING" ? "打开应用后重试或重新选择应用" :
            code == "FOCUS-TARGET-STALE" || code == "FOCUS-TARGET-AMBIGUOUS" ||
            code == "FOCUS-TARGET-UNVERIFIED" || code == "FOCUS-TARGET-NOT-EDITABLE"
                ? "重新学习工作流" : "重试；仍失败请仅激活应用后手动点击输入框";
        return Finish(target, ActionState.Error, "未锁定工作流，未发送按键",
            reason, recovery, code, elapsedMs);
    }

    private ActionResult Finish(FocusTargetDescriptor target, ActionState state, string message,
        string reason, string recovery, string errorCode, long elapsedMs)
    {
        string targetId = target == null ? "missing" : SafeLogCode(target.Id, "invalid");
        string strategy = target == null ? "missing" : SafeLogCode(target.Strategy, "invalid");
        string code = SafeErrorCode(errorCode, state == ActionState.Success ? "OK" : "FOCUS-FAILED");
        try
        {
            log("SMART FOCUS target_id=" + targetId + " strategy=" + strategy +
                " state=" + state.ToString().ToLowerInvariant() + " code=" + code +
                " elapsed_ms=" + Math.Max(0, elapsedMs));
        }
        catch { }
        return ActionResult.Create("锁定工作流", target == null ? "未设置" : target.Name,
            state, message, reason, recovery, state == ActionState.Success ? "" : code);
    }

    internal static string SafeErrorCode(string value, string fallback)
    {
        return SafeLogCode(value, fallback);
    }

    internal static bool ShouldCancelForForegroundChange(string initialProcessName,
        string currentProcessName, string targetProcessName, ref bool targetForegroundSeen)
    {
        string initial = FocusTargetDescriptor.NormalizeProcessName(initialProcessName);
        string current = FocusTargetDescriptor.NormalizeProcessName(currentProcessName);
        string target = FocusTargetDescriptor.NormalizeProcessName(targetProcessName);
        if (target.Length > 0 && string.Equals(current, target, StringComparison.OrdinalIgnoreCase))
        {
            targetForegroundSeen = true;
            return false;
        }
        if (targetForegroundSeen) return true;
        return initial.Length > 0 && current.Length > 0 &&
            !string.Equals(current, initial, StringComparison.OrdinalIgnoreCase);
    }

    private static string SafeLogCode(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        string source = value.Trim();
        var safe = new System.Text.StringBuilder();
        for (int index = 0; index < source.Length && safe.Length < 80; index++)
        {
            char character = source[index];
            if ((character >= 'A' && character <= 'Z') || (character >= 'a' && character <= 'z') ||
                (character >= '0' && character <= '9') || character == '_' || character == '-' || character == '.')
                safe.Append(character);
        }
        return safe.Length == 0 ? fallback : safe.ToString();
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
}

internal sealed class WindowsUiaFocusAutomationBackend : IFocusAutomationBackend
{
    private sealed class FocusApplicationWindow
    {
        internal int ProcessId;
        internal IntPtr WindowHandle;
    }

    private sealed class FocusWindowCandidateScan
    {
        // Candidates that carry the proof their own strategy requires: a writable
        // ValuePattern for a normal target, the focused text surface for a focus-only
        // console or terminal target.
        internal readonly List<AutomationElement> WritableMatches = new List<AutomationElement>();
        internal bool MatchedNonWritableTarget;
        internal bool ScanComplete;
        internal string ErrorCode = "";
    }

    private readonly RecordingPriorityCommitGate recordingCommitGate;

    internal WindowsUiaFocusAutomationBackend(RecordingPriorityCommitGate commitGate = null)
    {
        recordingCommitGate = commitGate ?? new RecordingPriorityCommitGate(delegate { return false; });
    }

    internal IList<FocusApplicationChoice> GetRunningApplications()
    {
        var choices = new List<FocusApplicationChoice>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Process[] processes;
        try { processes = Process.GetProcesses(); }
        catch { return choices; }
        foreach (Process process in processes)
        {
            try
            {
                if (process.MainWindowHandle == IntPtr.Zero) continue;
                string processName = FocusTargetDescriptor.NormalizeProcessName(process.ProcessName);
                if (IsCandidateApplication(processName) && seen.Add(processName))
                    choices.Add(new FocusApplicationChoice(processName));
            }
            catch { }
            finally { process.Dispose(); }
        }
        choices.Sort(delegate(FocusApplicationChoice left, FocusApplicationChoice right)
        {
            int priority = ApplicationPriority(left.ProcessName).CompareTo(ApplicationPriority(right.ProcessName));
            if (priority != 0) return priority;
            return string.Compare(left.DisplayName, right.DisplayName, StringComparison.CurrentCultureIgnoreCase);
        });
        return choices;
    }

    // Vibe Flow's own processes are never a target: the shipped executable is VibeFlow.exe, and the
    // bridge and the capture helper are likewise part of the product. Listing them offered the user
    // "vibeflow" as something to learn, which can only ever capture the app itself. Exposed so the
    // self-test can pin the list instead of trusting the literal.
    internal static readonly string[] ExcludedProcesses = {
        "vibemic", "vibeflow", "voxdeckinputbridge", "vibemicatvvcapture",
        "applicationframehost", "textinputhost", "explorer", "svchost",
        "codex-computer-use", "msedgewebview2", "wetype_update", "shellexperiencehost",
        "searchhost", "startmenuexperiencehost"
    };

    private static bool IsCandidateApplication(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return false;
        return Array.IndexOf(ExcludedProcesses, processName.ToLowerInvariant()) < 0;
    }

    private static int ApplicationPriority(string processName)
    {
        string[] preferred = { "chatgpt", "cursor", "code", "notepad", "chrome", "msedge" };
        int index = Array.IndexOf(preferred, (processName ?? "").ToLowerInvariant());
        return index < 0 ? 100 : index;
    }

    internal FocusLearningCaptureResult CaptureFocusedEditableTarget(string expectedProcessName)
    {
        string expected = FocusTargetDescriptor.NormalizeProcessName(expectedProcessName);
        if (expected.Length == 0)
            return FocusLearningCaptureResult.Failure("FOCUS-PROCESS-INVALID");
        try
        {
            AutomationElement element = AutomationElement.FocusedElement;
            if (element == null)
                return FocusLearningCaptureResult.Failure("FOCUS-LEARN-NO-FOCUS");
            AutomationElement.AutomationElementInformation current = element.Current;
            string actualProcessName = "";
            try
            {
                using (Process process = Process.GetProcessById(current.ProcessId))
                    actualProcessName = FocusTargetDescriptor.NormalizeProcessName(process.ProcessName);
            }
            catch { }
            if (!string.Equals(actualProcessName, expected, StringComparison.OrdinalIgnoreCase))
                return FocusLearningCaptureResult.Failure("FOCUS-PROCESS-MISMATCH");
            // Consoles and terminal editors (Windows Terminal's TermControl, classic
            // console hosts) expose a focused Text/Document element with TextPattern but
            // no writable ValuePattern. They cannot be written through UI Automation,
            // but the voice tool types into whatever holds focus, so they are learned as
            // "focus only" targets: the app may lock and restore focus there and must
            // never claim it wrote text itself.
            bool editableControl = current.ControlType == ControlType.Edit;
            bool focusOnlyControl = current.ControlType == ControlType.Text ||
                current.ControlType == ControlType.Document;
            if ((!editableControl && !focusOnlyControl) || !current.IsEnabled ||
                !current.IsKeyboardFocusable || !current.HasKeyboardFocus || current.IsOffscreen ||
                current.IsPassword)
                return FocusLearningCaptureResult.Failure("FOCUS-TARGET-NOT-EDITABLE");
            if (editableControl && !HasWritableEditablePattern(element))
                return FocusLearningCaptureResult.Failure("FOCUS-TARGET-NOT-EDITABLE");
            if (focusOnlyControl && !HasFocusOnlyPattern(element))
                return FocusLearningCaptureResult.Failure("FOCUS-TARGET-NOT-EDITABLE");

            var descriptor = new FocusTargetDescriptor
            {
                ProcessName = actualProcessName,
                AutomationId = current.AutomationId ?? "",
                ControlType = editableControl ? "Edit" : current.ControlType.ProgrammaticName.Replace("ControlType.", ""),
                ClassName = FocusTargetService.NormalizeStoredClassName(actualProcessName, current.ClassName),
                ParentFingerprint = BuildParentFingerprint(element),
                Strategy = editableControl ? "uia" : FocusOnlyStrategy
            };
            descriptor.NormalizeForStorage();
            if (string.IsNullOrWhiteSpace(descriptor.AutomationId) &&
                string.IsNullOrWhiteSpace(descriptor.ClassName) &&
                string.IsNullOrWhiteSpace(descriptor.ParentFingerprint))
                return FocusLearningCaptureResult.Failure("FOCUS-DESCRIPTOR-UNSTABLE");
            return FocusLearningCaptureResult.Success(descriptor);
        }
        catch (ElementNotAvailableException)
        {
            return FocusLearningCaptureResult.Failure("FOCUS-TARGET-STALE");
        }
        catch (UnauthorizedAccessException)
        {
            return FocusLearningCaptureResult.Failure("FOCUS-AUTOMATION-DENIED");
        }
        catch (InvalidOperationException)
        {
            return FocusLearningCaptureResult.Failure("FOCUS-TARGET-NOT-EDITABLE");
        }
        catch (COMException)
        {
            return FocusLearningCaptureResult.Failure("FOCUS-AUTOMATION-DENIED");
        }
    }

    // Known web editors can be discovered without reading their text. This is
    // deliberately limited to a unique, writable ChatGPT/Chromium composer;
    // all other applications continue to use the user-guided learning path.
    internal FocusLearningCaptureResult DiscoverKnownEditableTarget(string expectedProcessName)
    {
        string expected = FocusTargetDescriptor.NormalizeProcessName(expectedProcessName);
        if (expected != "chatgpt")
            return FocusLearningCaptureResult.Failure("FOCUS-ADAPTER-UNSUPPORTED");
        Process[] processes;
        try { processes = Process.GetProcessesByName(expected); }
        catch { return FocusLearningCaptureResult.Failure("FOCUS-APP-NOT-RUNNING"); }
        try
        {
            if (processes.Length == 0) return FocusLearningCaptureResult.Failure("FOCUS-APP-NOT-RUNNING");
            List<FocusApplicationWindow> windows = GetVisibleApplicationWindows(processes);
            FocusTargetDescriptor found = null;
            foreach (FocusApplicationWindow window in windows)
            {
                AutomationElement root = AutomationElement.FromHandle(window.WindowHandle);
                if (root == null) continue;
                Condition editCondition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit);
                AutomationElementCollection elements = root.FindAll(TreeScope.Descendants, editCondition);
                for (int index = 0; index < elements.Count; index++)
                {
                    AutomationElement candidate = elements[index];
                    AutomationElement.AutomationElementInformation current;
                    try { current = candidate.Current; } catch { continue; }
                    if (!FocusTargetService.IsKnownEditableWebTarget(expected, current.ClassName,
                            current.AutomationId, "Edit") || !current.IsEnabled ||
                        !current.IsKeyboardFocusable || current.IsOffscreen || current.IsPassword ||
                        !HasWritableEditablePattern(candidate)) continue;
                    var descriptor = new FocusTargetDescriptor
                    {
                        ProcessName = expected,
                        AutomationId = current.AutomationId ?? "",
                        ControlType = "Edit",
                        ClassName = FocusTargetService.NormalizeStoredClassName(expected, current.ClassName),
                        ParentFingerprint = BuildParentFingerprint(candidate),
                        Strategy = "uia"
                    };
                    descriptor.NormalizeForStorage();
                    if (found != null && !string.Equals(found.AutomationId, descriptor.AutomationId,
                            StringComparison.Ordinal) || found != null &&
                        !string.Equals(found.ParentFingerprint, descriptor.ParentFingerprint,
                            StringComparison.Ordinal))
                        return FocusLearningCaptureResult.Failure("FOCUS-TARGET-AMBIGUOUS");
                    found = descriptor;
                }
            }
            return found == null
                ? FocusLearningCaptureResult.Failure("FOCUS-TARGET-NOT-FOUND")
                : FocusLearningCaptureResult.Success(found);
        }
        catch (ElementNotAvailableException)
        {
            return FocusLearningCaptureResult.Failure("FOCUS-TARGET-STALE");
        }
        catch (UnauthorizedAccessException)
        {
            return FocusLearningCaptureResult.Failure("FOCUS-AUTOMATION-DENIED");
        }
        catch (COMException)
        {
            return FocusLearningCaptureResult.Failure("FOCUS-AUTOMATION-DENIED");
        }
        finally
        {
            foreach (Process process in processes) process.Dispose();
        }
    }

    public FocusAutomationStepResult ActivateApplication(FocusTargetDescriptor target, int timeoutMs,
        long startCommitEpoch, Func<bool> cancellationRequested)
    {
        Stopwatch timer = Stopwatch.StartNew();
        string processName = FocusTargetDescriptor.NormalizeProcessName(target.ProcessName);
        Process[] processes;
        try { processes = Process.GetProcessesByName(processName); }
        catch { return FocusAutomationStepResult.Failure("FOCUS-APP-NOT-RUNNING"); }
        try
        {
            if (processes.Length == 0)
                return FocusAutomationStepResult.Failure("FOCUS-APP-NOT-RUNNING");
            List<FocusApplicationWindow> windows = GetVisibleApplicationWindows(processes);
            if (windows.Count == 0)
                return FocusAutomationStepResult.Failure("FOCUS-PROCESS-MISMATCH");

            int writableMatchCount = 0;
            bool matchedNonWritableTarget = false;
            bool scanComplete = true;
            FocusApplicationWindow selectedWindow = null;
            foreach (FocusApplicationWindow window in windows)
            {
                if (Canceled(cancellationRequested))
                    return FocusAutomationStepResult.Failure("FOCUS-CANCELED");
                int remainingMs = Math.Max(1, timeoutMs - (int)timer.ElapsedMilliseconds);
                FocusWindowCandidateScan scan = ScanWindowCandidates(target, window.ProcessId,
                    window.WindowHandle, remainingMs, cancellationRequested);
                if (scan.ErrorCode.Length > 0)
                    return FocusAutomationStepResult.Failure(scan.ErrorCode);
                scanComplete = scanComplete && scan.ScanComplete;
                matchedNonWritableTarget = matchedNonWritableTarget || scan.MatchedNonWritableTarget;
                if (scan.WritableMatches.Count > 0 && selectedWindow == null) selectedWindow = window;
                writableMatchCount += scan.WritableMatches.Count;
                if (writableMatchCount > 1) break;
            }
            string selectionError = CandidateSelectionError(writableMatchCount,
                matchedNonWritableTarget, timer.ElapsedMilliseconds >= timeoutMs, scanComplete);
            if (selectionError.Length > 0) return FocusAutomationStepResult.Failure(selectionError);

            while (timer.ElapsedMilliseconds < timeoutMs && !Canceled(cancellationRequested))
            {
                if (selectedWindow == null)
                    return FocusAutomationStepResult.Failure("FOCUS-TARGET-STALE");
                if (Canceled(cancellationRequested))
                    return FocusAutomationStepResult.Failure("FOCUS-CANCELED");
                if (!TryShowAndActivateWindow(selectedWindow.WindowHandle, recordingCommitGate,
                        startCommitEpoch, cancellationRequested, ShowWindow, SetForegroundWindow))
                    return FocusAutomationStepResult.Failure("FOCUS-CANCELED");
                if (ForegroundBelongsTo(selectedWindow.ProcessId))
                    return FocusAutomationStepResult.Success(selectedWindow.ProcessId,
                        selectedWindow.WindowHandle);
                Thread.Sleep(25);
            }
            return Canceled(cancellationRequested)
                ? FocusAutomationStepResult.Failure("FOCUS-CANCELED")
                : FocusAutomationStepResult.Failure("FOCUS-PROCESS-MISMATCH");
        }
        finally
        {
            foreach (Process process in processes) process.Dispose();
        }
    }

    public FocusAutomationStepResult FocusAndVerify(FocusTargetDescriptor target,
        FocusAutomationStepResult activation, int timeoutMs, long startCommitEpoch,
        Func<bool> cancellationRequested)
    {
        if (activation == null || !activation.IsSuccess || activation.ProcessId <= 0 ||
            activation.WindowHandle == IntPtr.Zero)
            return FocusAutomationStepResult.Failure("FOCUS-PROCESS-MISMATCH");
        Stopwatch timer = Stopwatch.StartNew();
        try
        {
            AutomationElement selected = null;
            while (timer.ElapsedMilliseconds < timeoutMs)
            {
                if (Canceled(cancellationRequested))
                    return FocusAutomationStepResult.Failure("FOCUS-CANCELED");
                int remainingMs = Math.Max(1, timeoutMs - (int)timer.ElapsedMilliseconds);
                FocusWindowCandidateScan scan = ScanWindowCandidates(target, activation.ProcessId,
                    activation.WindowHandle, remainingMs, cancellationRequested);
                string selectionError = scan.ErrorCode.Length > 0 ? scan.ErrorCode :
                    CandidateSelectionError(scan.WritableMatches.Count, scan.MatchedNonWritableTarget,
                        timer.ElapsedMilliseconds >= timeoutMs, scan.ScanComplete);
                if (selectionError.Length == 0)
                {
                    selected = scan.WritableMatches[0];
                    break;
                }
                if (!ShouldRetryTargetScan(selectionError, timer.ElapsedMilliseconds, timeoutMs))
                    return FocusAutomationStepResult.Failure(selectionError);
                Thread.Sleep(Math.Min(35, Math.Max(1,
                    timeoutMs - (int)timer.ElapsedMilliseconds)));
            }
            if (selected == null)
                return Canceled(cancellationRequested)
                    ? FocusAutomationStepResult.Failure("FOCUS-CANCELED")
                    : FocusAutomationStepResult.Failure("FOCUS-TARGET-STALE");
            if (Canceled(cancellationRequested))
                return FocusAutomationStepResult.Failure("FOCUS-CANCELED");
            if (!TryCommitFocusAction(recordingCommitGate, startCommitEpoch,
                    cancellationRequested, selected.SetFocus))
                return FocusAutomationStepResult.Failure("FOCUS-CANCELED");
            while (timer.ElapsedMilliseconds < timeoutMs && !Canceled(cancellationRequested))
            {
                AutomationElement focused = AutomationElement.FocusedElement;
                bool descriptorMatches = Matches(target, focused, activation.ProcessId, false);
                bool writable = descriptorMatches && SatisfiesTargetEvidence(target, focused);
                bool belongsToSelectedWindow = descriptorMatches &&
                    BelongsToWindow(focused, activation.WindowHandle);
                bool hasKeyboardFocus = descriptorMatches && focused.Current.HasKeyboardFocus;
                if (IsVerifiedFocusedTarget(descriptorMatches, writable,
                    belongsToSelectedWindow, hasKeyboardFocus))
                    return FocusAutomationStepResult.Success(activation.ProcessId, activation.WindowHandle);
                Thread.Sleep(20);
            }
            return Canceled(cancellationRequested)
                ? FocusAutomationStepResult.Failure("FOCUS-CANCELED")
                : FocusAutomationStepResult.Failure("FOCUS-TARGET-STALE");
        }
        catch (ElementNotAvailableException)
        {
            return FocusAutomationStepResult.Failure("FOCUS-TARGET-STALE");
        }
        catch (UnauthorizedAccessException)
        {
            return FocusAutomationStepResult.Failure("FOCUS-AUTOMATION-DENIED");
        }
        catch (InvalidOperationException)
        {
            return FocusAutomationStepResult.Failure("FOCUS-TARGET-STALE");
        }
        catch (COMException)
        {
            return FocusAutomationStepResult.Failure("FOCUS-AUTOMATION-DENIED");
        }
    }

    internal FocusAutomationStepResult RestoreVerifiedTarget(FocusTargetDescriptor target,
        int timeoutMs, bool allowForegroundActivation)
    {
        string validationCode = "FOCUS-TARGET-MISSING";
        if (target == null || !target.TryValidateForExecution(out validationCode))
            return FocusAutomationStepResult.Failure(validationCode);

        Stopwatch timer = Stopwatch.StartNew();
        string processName = FocusTargetDescriptor.NormalizeProcessName(target.ProcessName);
        Process[] processes;
        try { processes = Process.GetProcessesByName(processName); }
        catch { return FocusAutomationStepResult.Failure("FOCUS-APP-NOT-RUNNING"); }
        try
        {
            if (processes.Length == 0)
                return FocusAutomationStepResult.Failure("FOCUS-APP-NOT-RUNNING");
            List<FocusApplicationWindow> windows = GetVisibleApplicationWindows(processes);
            FocusApplicationWindow selectedWindow = null;
            AutomationElement selectedElement = null;
            while (timer.ElapsedMilliseconds < timeoutMs && selectedElement == null)
            {
                int writableMatchCount = 0;
                bool matchedNonWritableTarget = false;
                bool scanComplete = true;
                string scanError = "";
                foreach (FocusApplicationWindow window in windows)
                {
                    int remainingMs = Math.Max(1, timeoutMs - (int)timer.ElapsedMilliseconds);
                    FocusWindowCandidateScan scan = ScanWindowCandidates(target, window.ProcessId,
                        window.WindowHandle, remainingMs, delegate { return false; });
                    if (scan.ErrorCode.Length > 0)
                    {
                        scanError = scan.ErrorCode;
                        break;
                    }
                    scanComplete = scanComplete && scan.ScanComplete;
                    matchedNonWritableTarget = matchedNonWritableTarget || scan.MatchedNonWritableTarget;
                    if (scan.WritableMatches.Count == 1 && selectedElement == null)
                    {
                        selectedElement = scan.WritableMatches[0];
                        selectedWindow = window;
                    }
                    writableMatchCount += scan.WritableMatches.Count;
                    if (writableMatchCount > 1) break;
                }
                string selectionError = scanError.Length > 0 ? scanError :
                    CandidateSelectionError(writableMatchCount, matchedNonWritableTarget,
                        timer.ElapsedMilliseconds >= timeoutMs, scanComplete);
                if (selectionError.Length > 0)
                {
                    if (!ShouldRetryTargetScan(selectionError, timer.ElapsedMilliseconds, timeoutMs))
                        return FocusAutomationStepResult.Failure(selectionError);
                    selectedWindow = null;
                    selectedElement = null;
                }
                if (selectedElement == null && timer.ElapsedMilliseconds < timeoutMs)
                    Thread.Sleep(Math.Min(35, Math.Max(1,
                        timeoutMs - (int)timer.ElapsedMilliseconds)));
            }
            if (selectedWindow == null || selectedElement == null)
                return FocusAutomationStepResult.Failure("FOCUS-TARGET-STALE");

            if (!ForegroundBelongsTo(selectedWindow.ProcessId))
            {
                if (!allowForegroundActivation)
                    return FocusAutomationStepResult.Failure("FOCUS-FOREGROUND-CHANGED");
                ShowWindow(selectedWindow.WindowHandle, 9);
                if (!SetForegroundWindow(selectedWindow.WindowHandle))
                    return FocusAutomationStepResult.Failure("FOCUS-PROCESS-MISMATCH");
            }

            selectedElement.SetFocus();
            while (timer.ElapsedMilliseconds < timeoutMs)
            {
                AutomationElement focused = AutomationElement.FocusedElement;
                bool descriptorMatches = Matches(target, focused, selectedWindow.ProcessId, false);
                bool writable = descriptorMatches && SatisfiesTargetEvidence(target, focused);
                bool belongsToSelectedWindow = descriptorMatches &&
                    BelongsToWindow(focused, selectedWindow.WindowHandle);
                bool hasKeyboardFocus = descriptorMatches && focused.Current.HasKeyboardFocus;
                if (IsVerifiedFocusedTarget(descriptorMatches, writable,
                    belongsToSelectedWindow, hasKeyboardFocus))
                    return FocusAutomationStepResult.Success(selectedWindow.ProcessId,
                        selectedWindow.WindowHandle);
                Thread.Sleep(15);
            }
            return FocusAutomationStepResult.Failure("FOCUS-TARGET-STALE");
        }
        catch (ElementNotAvailableException)
        {
            return FocusAutomationStepResult.Failure("FOCUS-TARGET-STALE");
        }
        catch (UnauthorizedAccessException)
        {
            return FocusAutomationStepResult.Failure("FOCUS-AUTOMATION-DENIED");
        }
        catch (InvalidOperationException)
        {
            return FocusAutomationStepResult.Failure("FOCUS-TARGET-STALE");
        }
        catch (COMException)
        {
            return FocusAutomationStepResult.Failure("FOCUS-AUTOMATION-DENIED");
        }
        finally
        {
            foreach (Process process in processes) process.Dispose();
        }
    }

    internal static bool TryShowAndActivateWindow(IntPtr window,
        RecordingPriorityCommitGate commitGate, long expectedEpoch,
        Func<bool> cancellationRequested, Func<IntPtr, int, bool> showWindow,
        Func<IntPtr, bool> setForegroundWindow)
    {
        if (window == IntPtr.Zero || commitGate == null || showWindow == null ||
            setForegroundWindow == null) return false;
        if (!TryCommitFocusAction(commitGate, expectedEpoch, cancellationRequested,
                delegate { showWindow(window, 9); })) return false;
        return TryCommitFocusAction(commitGate, expectedEpoch, cancellationRequested,
            delegate { setForegroundWindow(window); });
    }

    internal static bool TryCommitFocusAction(RecordingPriorityCommitGate commitGate,
        long expectedEpoch, Func<bool> cancellationRequested, Action action)
    {
        return commitGate != null && action != null &&
            commitGate.TryCommit(expectedEpoch, cancellationRequested, action);
    }

    private static FocusWindowCandidateScan ScanWindowCandidates(FocusTargetDescriptor target,
        int processId, IntPtr windowHandle, int timeoutMs, Func<bool> cancellationRequested)
    {
        var scan = new FocusWindowCandidateScan();
        Stopwatch timer = Stopwatch.StartNew();
        try
        {
            AutomationElement root = AutomationElement.FromHandle(windowHandle);
            if (root == null)
            {
                scan.ErrorCode = "FOCUS-TARGET-STALE";
                return scan;
            }
            // A focus-only target lives on a Text/Document surface, so the scan must
            // look for the control types that strategy is stored with. Searching only
            // for Edit meant a focus-only target produced no candidates at all and was
            // reported FOCUS-TARGET-STALE without ever being inspected.
            Condition editCondition = IsFocusOnlyTarget(target)
                ? (Condition)new OrCondition(
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document))
                : new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit);
            AutomationElementCollection elements = root.FindAll(TreeScope.Descendants, editCondition);
            int inspected = 0;
            for (int index = 0; index < elements.Count && timer.ElapsedMilliseconds < timeoutMs; index++)
            {
                if (Canceled(cancellationRequested))
                {
                    scan.ErrorCode = "FOCUS-CANCELED";
                    return scan;
                }
                AutomationElement candidate = elements[index];
                inspected = index + 1;
                if (!Matches(target, candidate, processId, false)) continue;
                // "Matches" holds the strategy-appropriate proof: writable for a
                // normal target, focus-only for a console or terminal surface.
                if (!SatisfiesTargetEvidence(target, candidate))
                    scan.MatchedNonWritableTarget = true;
                else
                    scan.WritableMatches.Add(candidate);
            }
            scan.ScanComplete = inspected == elements.Count;
            if (timer.ElapsedMilliseconds >= timeoutMs || !scan.ScanComplete)
                scan.ErrorCode = "FOCUS-TIMEOUT";
            return scan;
        }
        catch (ElementNotAvailableException)
        {
            scan.ErrorCode = "FOCUS-TARGET-STALE";
        }
        catch (UnauthorizedAccessException)
        {
            scan.ErrorCode = "FOCUS-AUTOMATION-DENIED";
        }
        catch (InvalidOperationException)
        {
            scan.ErrorCode = "FOCUS-TARGET-STALE";
        }
        catch (COMException)
        {
            scan.ErrorCode = "FOCUS-AUTOMATION-DENIED";
        }
        return scan;
    }

    private static List<FocusApplicationWindow> GetVisibleApplicationWindows(Process[] processes)
    {
        var windows = new List<FocusApplicationWindow>();
        var processIds = new HashSet<int>();
        var seenHandles = new HashSet<IntPtr>();
        foreach (Process process in processes)
        {
            try { processIds.Add(process.Id); }
            catch { }
        }
        try
        {
            EnumWindows(delegate(IntPtr handle, IntPtr parameter)
            {
                if (!IsWindowVisible(handle)) return true;
                uint processId;
                GetWindowThreadProcessId(handle, out processId);
                if (!processIds.Contains((int)processId) || !seenHandles.Add(handle)) return true;
                windows.Add(new FocusApplicationWindow
                {
                    ProcessId = (int)processId,
                    WindowHandle = handle
                });
                return true;
            }, IntPtr.Zero);
        }
        catch { }
        foreach (Process process in processes)
        {
            try
            {
                IntPtr handle = process.MainWindowHandle;
                if (handle == IntPtr.Zero || !IsWindowVisible(handle) || !seenHandles.Add(handle)) continue;
                windows.Add(new FocusApplicationWindow { ProcessId = process.Id, WindowHandle = handle });
            }
            catch { }
        }
        return windows;
    }

    internal static bool MatchesForObservation(FocusTargetDescriptor target,
        AutomationElement element, int expectedProcessId)
    {
        return Matches(target, element, expectedProcessId, false);
    }

    private static bool Matches(FocusTargetDescriptor target, AutomationElement element,
        int expectedProcessId, bool requireEditablePattern)
    {
        if (element == null) return false;
        AutomationElement.AutomationElementInformation current;
        try { current = element.Current; }
        catch { return false; }
        if (current.ProcessId != expectedProcessId ||
            !AcceptsStoredControlType(target, current.ControlType) ||
            !current.IsEnabled || !current.IsKeyboardFocusable || current.IsOffscreen || current.IsPassword)
            return false;
        if (!string.IsNullOrWhiteSpace(target.AutomationId) &&
            !string.Equals(current.AutomationId, target.AutomationId, StringComparison.Ordinal)) return false;
        if (!string.IsNullOrWhiteSpace(target.ClassName) &&
            !FocusTargetService.MatchesStoredClassName(target.ClassName, current.ClassName,
                target.ProcessName)) return false;
        if (!FocusTargetService.MatchesStoredParentFingerprint(target, BuildParentFingerprint(element),
                current.ClassName)) return false;
        if (!requireEditablePattern) return true;
        return SatisfiesTargetEvidence(target, element);
    }

    internal static bool HasWritableEditablePattern(AutomationElement element)
    {
        if (element == null) return false;
        object pattern;
        bool hasValuePattern = element.TryGetCurrentPattern(ValuePattern.Pattern, out pattern);
        bool valueIsReadOnly = true;
        if (hasValuePattern)
        {
            var valuePattern = pattern as ValuePattern;
            try { valueIsReadOnly = valuePattern == null || valuePattern.Current.IsReadOnly; }
            catch { valueIsReadOnly = true; }
        }
        bool hasTextPattern = element.TryGetCurrentPattern(TextPattern.Pattern, out pattern);
        return HasWritablePatternEvidence(hasValuePattern, valueIsReadOnly, hasTextPattern);
    }

    internal const string FocusOnlyStrategy = "uia_focus";

    // Focus-only targets only need proof that the element really is the focused text
    // surface of the application; the app never writes into them itself.
    private static bool HasFocusOnlyPattern(AutomationElement element)
    {
        if (element == null) return false;
        object pattern;
        if (element.TryGetCurrentPattern(TextPattern.Pattern, out pattern)) return true;
        if (!element.TryGetCurrentPattern(ValuePattern.Pattern, out pattern)) return false;
        var valuePattern = pattern as ValuePattern;
        try { return valuePattern != null && !valuePattern.Current.IsReadOnly; }
        catch { return false; }
    }

    // A focus-only target is stored as a console/terminal/WinUI text surface, so it
    // carries ControlType Text or Document instead of Edit. Matching used to demand
    // ControlType.Edit unconditionally, which made every focus-only target
    // unverifiable: it could be learned and saved, and then reported
    // FOCUS-TARGET-STALE on every single observation while the text was never
    // delivered. The stored control type is what the descriptor says it is, so the
    // accepted set follows the strategy, exactly as TryValidateForVerification does.
    internal static bool IsFocusOnlyTarget(FocusTargetDescriptor target)
    {
        return target != null && string.Equals((target.Strategy ?? "").Trim(),
            FocusOnlyStrategy, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool AcceptsStoredControlType(FocusTargetDescriptor target, ControlType controlType)
    {
        if (controlType == null) return false;
        if (!IsFocusOnlyTarget(target)) return controlType == ControlType.Edit;
        return controlType == ControlType.Edit || controlType == ControlType.Text ||
            controlType == ControlType.Document;
    }

    // The evidence that the stored surface is really in front of us depends on the
    // strategy: a writable target must prove a writable ValuePattern, while a
    // focus-only target only ever proves that it is the application's focused text
    // surface, because the app never writes into it itself.
    internal static bool SatisfiesTargetEvidence(FocusTargetDescriptor target, AutomationElement element)
    {
        return IsFocusOnlyTarget(target) ? HasFocusOnlyPattern(element)
            : HasWritableEditablePattern(element);
    }

    internal static bool HasWritablePatternEvidence(bool hasValuePattern,
        bool valueIsReadOnly, bool hasTextPattern)    {
        return hasValuePattern && !valueIsReadOnly;
    }

    internal static string CandidateSelectionError(int writableMatchCount,
        bool matchedNonWritableTarget, bool timedOut, bool scanComplete)
    {
        if (timedOut || !scanComplete) return "FOCUS-TIMEOUT";
        if (writableMatchCount > 1) return "FOCUS-TARGET-AMBIGUOUS";
        if (writableMatchCount == 1) return "";
        if (matchedNonWritableTarget) return "FOCUS-TARGET-NOT-EDITABLE";
        return "FOCUS-TARGET-STALE";
    }

    // A Chromium/WebView editor can briefly disappear from the UIA tree while
    // a page refresh or provider overlay is closing. Retry only those
    // transient states; ambiguous, canceled, foreground-changed, and timed
    // out requests must fail closed instead of guessing a control.
    internal static bool ShouldRetryTargetScan(string errorCode, long elapsedMs, int timeoutMs)
    {
        if (elapsedMs < 0 || timeoutMs <= 0 || elapsedMs >= timeoutMs) return false;
        return string.Equals(errorCode, "FOCUS-TARGET-STALE", StringComparison.Ordinal) ||
            string.Equals(errorCode, "FOCUS-TARGET-NOT-EDITABLE", StringComparison.Ordinal);
    }

    internal static bool IsVerifiedFocusedTarget(bool descriptorMatches, bool writable,
        bool belongsToSelectedWindow, bool hasKeyboardFocus)
    {
        return descriptorMatches && writable && belongsToSelectedWindow && hasKeyboardFocus;
    }

    private static bool BelongsToWindow(AutomationElement element, IntPtr windowHandle)
    {
        if (element == null || windowHandle == IntPtr.Zero) return false;
        AutomationElement windowRoot;
        try { windowRoot = AutomationElement.FromHandle(windowHandle); }
        catch { return false; }
        if (windowRoot == null) return false;

        AutomationElement current = element;
        for (int depth = 0; current != null && depth < 128; depth++)
        {
            try
            {
                if (Automation.Compare(current, windowRoot)) return true;
                current = TreeWalker.RawViewWalker.GetParent(current);
            }
            catch { return false; }
        }
        return false;
    }

    internal static string BuildParentFingerprint(AutomationElement element)
    {
        var parts = new List<string>();
        AutomationElement current = element;
        for (int depth = 0; depth < 3; depth++)
        {
            try { current = TreeWalker.ControlViewWalker.GetParent(current); }
            catch { break; }
            if (current == null) break;
            try
            {
                string controlType = current.Current.ControlType == null ? "" :
                    current.Current.ControlType.ProgrammaticName.Replace("ControlType.", "");
                parts.Add(SafeFingerprintPart(controlType) + "|" +
                    SafeFingerprintPart(current.Current.AutomationId) + "|" +
                    SafeFingerprintPart(current.Current.ClassName));
            }
            catch { break; }
        }
        return string.Join(">", parts.ToArray());
    }

    private static string SafeFingerprintPart(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        string singleLine = value.Replace('\r', ' ').Replace('\n', ' ').Replace('|', '_').Replace('>', '_').Trim();
        return singleLine.Length <= 96 ? singleLine : singleLine.Substring(0, 96);
    }

    private static bool Canceled(Func<bool> cancellationRequested)
    {
        try { return cancellationRequested != null && cancellationRequested(); }
        catch { return true; }
    }

    private static bool ForegroundBelongsTo(int processId)
    {
        IntPtr foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero) return false;
        uint foregroundProcessId;
        GetWindowThreadProcessId(foreground, out foregroundProcessId);
        return foregroundProcessId == (uint)processId;
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr window);

    private delegate bool EnumWindowsCallback(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr window, int command);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
}

internal sealed class SelfTestFocusAutomationBackend : IFocusAutomationBackend
{
    internal int ActivateCalls;
    internal int FocusCalls;
    internal int InputDispatchCalls;
    internal int ActivateDelayMs;
    internal bool BlockActivation;
    internal Action AfterActivate;
    internal FocusAutomationStepResult ActivateResult;
    internal FocusAutomationStepResult FocusResult;
    internal readonly ManualResetEvent ActivationEntered = new ManualResetEvent(false);
    internal readonly ManualResetEvent ReleaseActivation = new ManualResetEvent(false);

    internal SelfTestFocusAutomationBackend()
    {
        Reset();
    }

    internal void Reset()
    {
        ActivateCalls = 0;
        FocusCalls = 0;
        InputDispatchCalls = 0;
        ActivateDelayMs = 0;
        BlockActivation = false;
        AfterActivate = null;
        ActivateResult = FocusAutomationStepResult.Success(101, new IntPtr(1));
        FocusResult = FocusAutomationStepResult.Success(101, new IntPtr(1));
        ActivationEntered.Reset();
        ReleaseActivation.Reset();
    }

    public FocusAutomationStepResult ActivateApplication(FocusTargetDescriptor target, int timeoutMs,
        long commitEpoch, Func<bool> cancellationRequested)
    {
        Interlocked.Increment(ref ActivateCalls);
        ActivationEntered.Set();
        if (BlockActivation) ReleaseActivation.WaitOne(Math.Max(1, timeoutMs));
        if (ActivateDelayMs > 0) Thread.Sleep(ActivateDelayMs);
        if (AfterActivate != null) AfterActivate();
        return ActivateResult;
    }

    public FocusAutomationStepResult FocusAndVerify(FocusTargetDescriptor target,
        FocusAutomationStepResult activation, int timeoutMs, long commitEpoch,
        Func<bool> cancellationRequested)
    {
        Interlocked.Increment(ref FocusCalls);
        return FocusResult;
    }
}
