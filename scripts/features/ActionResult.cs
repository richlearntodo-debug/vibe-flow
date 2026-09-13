using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;

internal enum ActionState
{
    Idle = 0,
    Checking = 1,
    Running = 2,
    Success = 3,
    Warning = 4,
    Error = 5,
    Canceled = 6
}

internal sealed class RecordingPriorityCommitGate
{
    private const long ReservationBit = 1L;
    private const long ExecutingBit = 2L;
    private const long EpochIncrement = 4L;
    private readonly Func<bool> recordingActive;
    private long admissionState;

    internal RecordingPriorityCommitGate(Func<bool> isRecording)
    {
        recordingActive = isRecording ?? delegate { return true; };
    }

    internal long CaptureEpoch()
    {
        return Interlocked.Read(ref admissionState) / EpochIncrement;
    }

    internal void CancelForRecording()
    {
        while (true)
        {
            long observed = Interlocked.Read(ref admissionState);
            long updated = (observed & ExecutingBit) != 0
                ? unchecked(observed + EpochIncrement)
                : unchecked((observed & ~ReservationBit) + EpochIncrement);
            if (Interlocked.CompareExchange(ref admissionState, updated, observed) == observed) return;
        }
    }

    internal bool TryCommit(long expectedEpoch, Func<bool> cancellationRequested, Action action)
    {
        return TryCommit(expectedEpoch, cancellationRequested, action, null);
    }

    internal bool TryCommit(long expectedEpoch, Func<bool> cancellationRequested,
        Action action, Action reservationAcquired)
    {
        if (action == null) return false;
        if (!TryReserve(expectedEpoch, cancellationRequested)) return false;
        bool executing = false;
        try
        {
            if (reservationAcquired != null) reservationAcquired();
            executing = TryBeginExecution(expectedEpoch);
            if (!executing) return false;
            action();
            return true;
        }
        finally
        {
            if (executing) ReleaseExecution();
            else ReleaseReservation();
        }
    }

    private bool TryReserve(long expectedEpoch, Func<bool> cancellationRequested)
    {
        if (IsTrue(recordingActive) || IsTrue(cancellationRequested)) return false;
        while (true)
        {
            long observed = Interlocked.Read(ref admissionState);
            if ((observed & (ReservationBit | ExecutingBit)) != 0 ||
                observed / EpochIncrement != expectedEpoch)
                return false;
            if (Interlocked.CompareExchange(ref admissionState, observed | ReservationBit, observed) == observed)
                return true;
        }
    }

    private bool TryBeginExecution(long expectedEpoch)
    {
        while (true)
        {
            long observed = Interlocked.Read(ref admissionState);
            if ((observed & ReservationBit) == 0 ||
                (observed & ExecutingBit) != 0 || observed / EpochIncrement != expectedEpoch)
                return false;
            long updated = (observed & ~ReservationBit) | ExecutingBit;
            if (Interlocked.CompareExchange(ref admissionState, updated, observed) == observed)
                return true;
        }
    }

    private void ReleaseExecution()
    {
        while (true)
        {
            long observed = Interlocked.Read(ref admissionState);
            if ((observed & ExecutingBit) == 0) return;
            if (Interlocked.CompareExchange(ref admissionState, observed & ~ExecutingBit, observed) == observed)
                return;
        }
    }

    private void ReleaseReservation()
    {
        while (true)
        {
            long observed = Interlocked.Read(ref admissionState);
            if ((observed & ReservationBit) == 0) return;
            if (Interlocked.CompareExchange(ref admissionState, observed & ~ReservationBit, observed) == observed)
                return;
        }
    }

    private static bool IsTrue(Func<bool> predicate)
    {
        try { return predicate == null || predicate(); }
        catch { return true; }
    }
}

internal sealed class ActionResult
{
    public string ActionName { get; private set; }
    public string Target { get; private set; }
    public ActionState State { get; private set; }
    public string Stage { get; private set; }
    public string Message { get; private set; }
    public string ErrorReason { get; private set; }
    public string RecoveryAction { get; private set; }
    public string ErrorCode { get; private set; }
    public DateTime TimestampUtc { get; private set; }

    public bool IsSuccess { get { return State == ActionState.Success; } }

    private ActionResult() { }

    public static ActionResult Create(string actionName, string target, ActionState state,
        string message, string errorReason, string recoveryAction, string errorCode)
    {
        return new ActionResult
        {
            ActionName = OverlayText.SanitizeOverlayText(actionName),
            Target = OverlayText.SanitizeOverlayText(target),
            State = state,
            Stage = StateText(state),
            Message = OverlayText.SanitizeOverlayText(message),
            ErrorReason = OverlayText.SanitizeOverlayText(errorReason),
            RecoveryAction = OverlayText.SanitizeOverlayText(recoveryAction),
            ErrorCode = OverlayText.SanitizeCode(errorCode),
            TimestampUtc = DateTime.UtcNow
        };
    }

    public static ActionResult FromSessionFeedback(string state, string message)
    {
        bool waiting = string.Equals(state, "waiting", StringComparison.OrdinalIgnoreCase);
        ActionResult result = Create("录音状态", "语音工具", waiting ? ActionState.Warning : ActionState.Error,
            message, waiting ? "" : "未确认语音工具收到音频",
            waiting ? "检查最终文字" : "重新按住录音键重试；仍失败请打开自检",
            waiting ? "VOICE-WAITING" : "VOICE-NO-AUDIO");
        result.Stage = waiting ? "等待语音工具处理" : "需要检查";
        return result;
    }

    public static ActionResult FromLegacyFeedback(string message, string kind)
    {
        string normalized = (kind ?? "").Trim().ToLowerInvariant();
        ActionState state = normalized == "success" ? ActionState.Success :
            normalized == "warning" ? ActionState.Warning :
            normalized == "error" ? ActionState.Error :
            normalized == "canceled" ? ActionState.Canceled :
            normalized == "running" ? ActionState.Running :
            normalized == "checking" || (message ?? "").StartsWith("正在", StringComparison.Ordinal)
                ? ActionState.Checking : ActionState.Idle;
        return Create("最近动作", "Vibe Flow", state, message, "",
            state == ActionState.Error ? "打开自检查看原因" : "", "");
    }

    public static ActionResult FromConfigurationApply(string actionName, string target,
        string successMessage, bool saved, bool runtimeAcknowledgementRequired,
        bool runtimeAcknowledged)
    {
        if (!saved)
            return Create(actionName, target, ActionState.Error,
                "设置未保存，未执行后续操作", "无法写入本地配置",
                "重试；仍失败请打开自检", "CONFIG-SAVE-FAILED");
        if (runtimeAcknowledgementRequired && !runtimeAcknowledged)
            return Create(actionName, target, ActionState.Warning,
                "设置已保存，正在等待按键服务生效", "按键服务尚未确认当前配置",
                "打开自检后重新检测", "BRIDGE-ACK-PENDING");
        return Create(actionName, target, ActionState.Success, successMessage, "", "", "");
    }

    internal ActionResult ForOverlay()
    {
        ActionResult copy = Create(ActionName, Target, State, Message, ErrorReason, RecoveryAction, ErrorCode);
        copy.Stage = OverlayText.SanitizeOverlayText(Stage);
        copy.TimestampUtc = TimestampUtc;
        return copy;
    }

    internal string OverlayDetailText()
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(Message)) lines.Add(Message);
        if (!string.IsNullOrWhiteSpace(ErrorReason)) lines.Add("原因：" + ErrorReason);
        if (!string.IsNullOrWhiteSpace(RecoveryAction)) lines.Add("下一步：" + RecoveryAction);
        if (!string.IsNullOrWhiteSpace(ErrorCode)) lines.Add("错误码：" + ErrorCode);
        return string.Join(Environment.NewLine, lines.ToArray());
    }

    private static string StateText(ActionState state)
    {
        switch (state)
        {
            case ActionState.Checking: return "正在检查";
            case ActionState.Running: return "正在执行";
            case ActionState.Success: return "已完成";
            case ActionState.Warning: return "需要确认";
            case ActionState.Error: return "执行失败";
            case ActionState.Canceled: return "已取消";
            default: return "等待操作";
        }
    }
}

internal sealed class VibeUiStatusSnapshot
{
    public string DeviceStatus { get; private set; }
    public string VoiceStatus { get; private set; }
    public string ProfileName { get; private set; }
    public string CurrentApplication { get; private set; }
    public string FocusTargetName { get; private set; }
    public string ProjectSpaceName { get; private set; }
    public string HighlightedControl { get; private set; }
    public bool RealAudioActive { get; private set; }
    public double AudioRmsPercent { get; private set; }
    public ActionResult LatestAction { get; private set; }
    public Dictionary<string, string> Mappings { get; private set; }
    // The selected voice tool. The HUD and the Deck name it so a user can tell at a glance which tool is about to
    // receive the audio, next to the workflow target the text will land in and the current session state.
    public string VoiceToolName { get; private set; }
    public DateTime TimestampUtc { get; private set; }

    private VibeUiStatusSnapshot() { }

    public static VibeUiStatusSnapshot Create(string deviceStatus, string voiceStatus, string profileName,
        string processNameOrPath, string focusTargetName, string projectSpaceName, string highlightedControl,
        bool realAudioActive, ActionResult latestAction, IDictionary<string, string> mappings,
        string voiceToolName = null)
    {
        return Create(deviceStatus, voiceStatus, profileName, processNameOrPath, focusTargetName,
            projectSpaceName, highlightedControl, realAudioActive, 0.0, latestAction, mappings, voiceToolName);
    }

    public static VibeUiStatusSnapshot Create(string deviceStatus, string voiceStatus, string profileName,
        string processNameOrPath, string focusTargetName, string projectSpaceName, string highlightedControl,
        bool realAudioActive, double audioRmsPercent, ActionResult latestAction, IDictionary<string, string> mappings,
        string voiceToolName = null)
    {
        var safeMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (mappings != null)
        {
            foreach (KeyValuePair<string, string> pair in mappings)
                safeMappings[OverlayText.SanitizeOverlayText(pair.Key)] = OverlayText.SanitizeMappingAction(pair.Value);
        }
        return new VibeUiStatusSnapshot
        {
            DeviceStatus = OverlayText.SanitizeOverlayText(deviceStatus),
            VoiceStatus = OverlayText.SanitizeOverlayText(voiceStatus),
            ProfileName = OverlayText.SanitizeOverlayText(profileName),
            CurrentApplication = OverlayText.SafeProcessName(processNameOrPath),
            FocusTargetName = OverlayText.SanitizeOverlayText(focusTargetName),
            ProjectSpaceName = OverlayText.SanitizeOverlayText(projectSpaceName),
            HighlightedControl = OverlayText.SanitizeCode(highlightedControl),
            RealAudioActive = realAudioActive,
            AudioRmsPercent = Math.Max(0.0, Math.Min(100.0, audioRmsPercent)),
            LatestAction = (latestAction ?? ActionResult.Create("状态", "Vibe Flow", ActionState.Idle,
                "等待操作", "", "", "")).ForOverlay(),
            Mappings = safeMappings,
            VoiceToolName = OverlayText.SanitizeOverlayText(voiceToolName),
            TimestampUtc = DateTime.UtcNow
        };
    }
}

internal static class OverlayText
{
    private static readonly Regex UrlPattern = new Regex(@"(?i)\bhttps?://[^\s，；]+", RegexOptions.Compiled);
    private static readonly Regex WindowsPathPattern = new Regex(@"(?i)\b[a-z]:\\[^\r\n，；]+", RegexOptions.Compiled);

    internal static string SanitizeOverlayText(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        string safe = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        safe = UrlPattern.Replace(safe, "[网页地址]");
        safe = WindowsPathPattern.Replace(safe, "[本机路径]");
        return safe.Length <= 180 ? safe : safe.Substring(0, 177) + "...";
    }

    internal static string SanitizeMappingAction(string action)
    {
        string normalized = (action ?? "").Trim();
        if (normalized.StartsWith("open-exe:", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("launch-client:", StringComparison.OrdinalIgnoreCase)) return "打开应用";
        if (normalized.StartsWith("open-url:", StringComparison.OrdinalIgnoreCase)) return "打开网页";
        string key = normalized.ToLowerInvariant();
        if (key == "up") return "上方向";
        if (key == "down") return "下方向";
        if (key == "left") return "左方向";
        if (key == "right") return "右方向";
        if (key == "enter") return "确认 / 换行";
        if (key == "win+d") return "显示桌面";
        if (key == "task-switcher") return "任务视图";
        if (key == "browserback") return "浏览器后退";
        if (key == "pageup") return "向上翻页";
        if (key == "pagedown") return "向下翻页";
        if (key == "ctrl+c") return "复制";
        if (key == "ctrl+v") return "粘贴";
        if (key == "none" || key == "passthrough") return "未设置";
        return SanitizeOverlayText(normalized);
    }

    internal static string SafeProcessName(string processNameOrPath)
    {
        if (string.IsNullOrWhiteSpace(processNameOrPath)) return "未检测";
        string candidate = processNameOrPath.Trim().Trim('"');
        try { candidate = Path.GetFileNameWithoutExtension(candidate); }
        catch { candidate = ""; }
        if (string.IsNullOrWhiteSpace(candidate)) return "未检测";
        if (candidate.Equals("cursor", StringComparison.OrdinalIgnoreCase)) return "Cursor";
        if (candidate.Equals("code", StringComparison.OrdinalIgnoreCase)) return "VS Code";
        if (candidate.Equals("chrome", StringComparison.OrdinalIgnoreCase)) return "Chrome";
        if (candidate.Equals("msedge", StringComparison.OrdinalIgnoreCase)) return "Edge";
        return SanitizeOverlayText(candidate);
    }

    internal static string SanitizeCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        return Regex.Replace(value.Trim(), @"[^A-Za-z0-9_.-]", "");
    }
}
