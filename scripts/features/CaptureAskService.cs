using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;

internal sealed class CaptureAskService
{
    private readonly string tempRoot;
    private readonly ICaptureAskImageClipboard clipboard;
    private readonly ICaptureAskPasteDispatcher pasteDispatcher;
    private readonly CaptureAskFocusExecutor focusTarget;
    private readonly CaptureAskFocusVerifier verifyFocusedTarget;
    private readonly Func<bool> isRecording;
    private readonly RecordingPriorityCommitGate recordingCommitGate;
    private readonly Action<string> log;
    private int operationActive;
    private long cancellationEpoch;
    private long voiceCancellationEpoch;

    internal CaptureAskService(string tempRoot, ICaptureAskImageClipboard clipboard,
        ICaptureAskPasteDispatcher pasteDispatcher, CaptureAskFocusExecutor focusTarget,
        CaptureAskFocusVerifier verifyFocusedTarget, Func<bool> isRecording, Action<string> safeLog,
        RecordingPriorityCommitGate commitGate = null)
    {
        if (string.IsNullOrWhiteSpace(tempRoot)) throw new ArgumentException("A temp root is required.", "tempRoot");
        if (clipboard == null) throw new ArgumentNullException("clipboard");
        if (pasteDispatcher == null) throw new ArgumentNullException("pasteDispatcher");
        if (focusTarget == null) throw new ArgumentNullException("focusTarget");
        if (verifyFocusedTarget == null) throw new ArgumentNullException("verifyFocusedTarget");
        this.tempRoot = Path.GetFullPath(tempRoot);
        this.clipboard = clipboard;
        this.pasteDispatcher = pasteDispatcher;
        this.focusTarget = focusTarget;
        this.verifyFocusedTarget = verifyFocusedTarget;
        this.isRecording = isRecording ?? delegate { return false; };
        recordingCommitGate = commitGate ?? new RecordingPriorityCommitGate(this.isRecording);
        log = safeLog ?? delegate { };
    }

    internal CaptureAskPrepareResult Prepare(Bitmap source, Rectangle selectedBounds, string sourceKind)
    {
        if (source == null || selectedBounds.Width <= 0 || selectedBounds.Height <= 0 ||
            selectedBounds.X < 0 || selectedBounds.Y < 0 || selectedBounds.Right > source.Width ||
            selectedBounds.Bottom > source.Height)
            return CaptureAskPrepareResult.Failure("CAPTURE-ASK-BOUNDS-INVALID");

        string normalizedKind = string.Equals(sourceKind, "region", StringComparison.OrdinalIgnoreCase)
            ? "region" : "window";
        string captureId = Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(tempRoot);
        string tempPath = Path.Combine(tempRoot, captureId + ".png");
        Bitmap prepared = null;
        try
        {
            prepared = source.Clone(selectedBounds, PixelFormat.Format32bppArgb);
            prepared.Save(tempPath, ImageFormat.Png);
            var capture = new CaptureAskPreparedImage(captureId, normalizedKind, tempPath, prepared);
            SafeLog("capture=" + captureId + " source=" + normalizedKind + " state=prepared error=");
            return CaptureAskPrepareResult.Success(capture);
        }
        catch
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            SafeLog("capture=" + captureId + " source=" + normalizedKind +
                " state=error error=CAPTURE-ASK-PREPARE-FAILED");
            return CaptureAskPrepareResult.Failure("CAPTURE-ASK-PREPARE-FAILED");
        }
        finally
        {
            if (prepared != null) prepared.Dispose();
        }
    }

    internal ActionResult CopyToClipboard(CaptureAskPreparedImage capture)
    {
        if (capture == null || !capture.IsAvailable)
            return Result(ActionState.Error, "截图不可用，未写入剪贴板", "截图已被清理或未成功生成",
                "重新截图", "CAPTURE-ASK-IMAGE-MISSING");
        try
        {
            using (Bitmap image = capture.CreateImageCopy())
            {
                string errorCode;
                if (!clipboard.TrySetImage(image, out errorCode))
                    return Result(ActionState.Error, "截图未复制到剪贴板", "Windows 剪贴板暂时不可用",
                        "重试复制", SafeCode(errorCode, "CAPTURE-ASK-CLIPBOARD-FAILED"));
            }
            SafeLog("capture=" + capture.Id + " source=" + capture.SourceKind +
                " state=copied error=");
            return Result(ActionState.Success, "截图已复制到剪贴板", "", "", "");
        }
        catch
        {
            return Result(ActionState.Error, "截图未复制到剪贴板", "Windows 剪贴板拒绝了图像写入",
                "重试复制", "CAPTURE-ASK-CLIPBOARD-FAILED");
        }
    }

    internal ActionResult PasteToTarget(CaptureAskPreparedImage capture,
        FocusTargetDescriptor target, int timeoutMs)
    {
        string validationCode = "FOCUS-TARGET-MISSING";
        if (target == null || !target.TryValidateForExecution(out validationCode))
            return Result(ActionState.Error, "未找到已验证输入目标，未执行粘贴",
                "截图只能粘贴到已测试的输入目标", "设置并测试截图目标", validationCode);
        if (capture == null || !capture.IsAvailable)
            return Result(ActionState.Error, "截图不可用，未执行粘贴", "截图已被清理或未成功生成",
                "重新截图", "CAPTURE-ASK-IMAGE-MISSING");
        if (Interlocked.CompareExchange(ref operationActive, 1, 0) != 0)
            return Result(ActionState.Warning, "已有截图提问操作正在执行", "同一时间只能执行一个截图提问",
                "等待当前操作结束后重试", "CAPTURE-ASK-BUSY");

        var elapsed = Stopwatch.StartNew();
        long startCancellationEpoch = Interlocked.Read(ref cancellationEpoch);
        long startVoiceEpoch = Interlocked.Read(ref voiceCancellationEpoch);
        long startCommitEpoch = recordingCommitGate.CaptureEpoch();
        bool clipboardWritten = false;
        try
        {
            ActionResult canceled = CancellationResult(startCancellationEpoch, startVoiceEpoch);
            if (canceled != null) return canceled;
            ActionResult focused = focusTarget(target, Math.Max(1, timeoutMs));
            canceled = CancellationResult(startCancellationEpoch, startVoiceEpoch);
            if (canceled != null) return canceled;
            if (focused == null || !focused.IsSuccess) return focused ??
                Result(ActionState.Error, "未锁定输入目标，未执行粘贴", "目标聚焦没有返回结果",
                    "重新测试输入目标", "CAPTURE-ASK-FOCUS-FAILED");

            if (!SafeVerify(target))
                return Result(ActionState.Error, "输入目标已失去焦点，未执行粘贴",
                    "目标控件在截图写入前不再接收键盘输入", "重新锁定输入目标后重试",
                    "CAPTURE-ASK-FOCUS-LOST");
            canceled = CancellationResult(startCancellationEpoch, startVoiceEpoch);
            if (canceled != null) return canceled;

            using (Bitmap image = capture.CreateImageCopy())
            {
                string clipboardError;
                if (!clipboard.TrySetImage(image, out clipboardError))
                    return Result(ActionState.Error, "截图未复制，未执行粘贴", "Windows 剪贴板暂时不可用",
                        "重试；仍失败请打开剪贴板设置", SafeCode(clipboardError,
                            "CAPTURE-ASK-CLIPBOARD-FAILED"));
            }
            clipboardWritten = true;
            canceled = CancellationResult(startCancellationEpoch, startVoiceEpoch, true);
            if (canceled != null) return canceled;
            if (!SafeVerify(target))
                return Result(ActionState.Error, "输入目标已失去焦点，未执行粘贴；截图仍在剪贴板",
                    "目标控件在派发粘贴前不再接收键盘输入", "重新锁定输入目标后重试",
                    "CAPTURE-ASK-FOCUS-LOST");
            canceled = CancellationResult(startCancellationEpoch, startVoiceEpoch, true);
            if (canceled != null) return canceled;

            string pasteError = "";
            bool pasteSucceeded = false;
            bool finalFocusValid = true;
            ActionResult commitCancellation = null;
            bool committed = recordingCommitGate.TryCommit(startCommitEpoch, delegate
            {
                commitCancellation = CancellationResult(startCancellationEpoch, startVoiceEpoch, true);
                if (commitCancellation != null) return true;
                finalFocusValid = SafeVerify(target);
                return !finalFocusValid;
            }, delegate
            {
                pasteSucceeded = pasteDispatcher.TryPasteImage(out pasteError);
            });
            if (!committed)
            {
                if (commitCancellation != null) return commitCancellation;
                if (!finalFocusValid)
                    return Result(ActionState.Error, "输入目标已失去焦点，未执行粘贴；截图仍在剪贴板",
                        "目标控件在派发粘贴前不再接收键盘输入",
                        "重新锁定输入目标后重试", "CAPTURE-ASK-FOCUS-LOST");
                return Result(ActionState.Canceled,
                    "录音已开始，粘贴未执行；截图仍在剪贴板", "录音操作优先",
                    "录音结束后重新执行截图提问", "CAPTURE-ASK-CANCELED-VOICE");
            }
            if (!pasteSucceeded)
                return Result(ActionState.Error, "未执行图片粘贴；截图仍在剪贴板", "目标应用拒绝了粘贴按键",
                    "确认目标支持图片后重试", SafeCode(pasteError, "CAPTURE-ASK-PASTE-FAILED"));
            SafeLog("capture=" + capture.Id + " source=" + capture.SourceKind + " target=" +
                SafeCode(target.Id, "unknown") + " state=dispatched error= elapsedMs=" +
                elapsed.ElapsedMilliseconds);
            return Result(ActionState.Success, "粘贴动作已派发，请按住录音键描述问题", "", "", "");
        }
        catch
        {
            return Result(ActionState.Error, clipboardWritten
                    ? "截图未粘贴；截图仍在剪贴板" : "截图未粘贴",
                "截图提问执行时发生异常",
                "重新截图后重试", "CAPTURE-ASK-UNAVAILABLE");
        }
        finally
        {
            Interlocked.Exchange(ref operationActive, 0);
        }
    }

    internal ActionResult Cleanup(CaptureAskPreparedImage capture)
    {
        if (capture == null)
            return Result(ActionState.Success, "截图临时文件已清理", "", "", "");
        capture.Dispose();
        if (!IsOwnedTempPath(capture))
            return Result(ActionState.Warning, "未清理不属于截图临时目录的文件",
                "临时文件路径未通过所有权校验", "打开本地数据目录检查",
                "CAPTURE-ASK-CLEANUP-PATH-REJECTED");
        try
        {
            if (File.Exists(capture.TempPath)) File.Delete(capture.TempPath);
            return Result(ActionState.Success, "截图临时文件已清理", "", "", "");
        }
        catch
        {
            return Result(ActionState.Warning, "截图临时文件稍后清理", "文件仍被其他程序占用",
                "关闭预览后重试", "CAPTURE-ASK-CLEANUP-DEFERRED");
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

    private ActionResult CancellationResult(long startCancellationEpoch, long startVoiceEpoch,
        bool clipboardWritten = false)
    {
        bool voice = Interlocked.Read(ref voiceCancellationEpoch) != startVoiceEpoch || SafeRecording();
        bool canceled = Interlocked.Read(ref cancellationEpoch) != startCancellationEpoch;
        if (!voice && !canceled) return null;
        return voice
            ? Result(ActionState.Canceled, clipboardWritten
                    ? "录音已开始，粘贴未执行；截图仍在剪贴板"
                    : "录音已开始，已停止后续截图粘贴步骤", "录音操作优先",
                "录音结束后重新执行截图提问", "CAPTURE-ASK-CANCELED-VOICE")
            : Result(ActionState.Canceled, clipboardWritten
                    ? "截图提问已取消，粘贴未执行；截图仍在剪贴板"
                    : "截图提问已取消，未执行后续步骤", "用户取消了本次操作",
                "需要时重新截图", "CAPTURE-ASK-CANCELED");
    }

    private bool SafeVerify(FocusTargetDescriptor target)
    {
        try { return verifyFocusedTarget(target); }
        catch { return false; }
    }

    private bool SafeRecording()
    {
        try { return isRecording(); }
        catch { return true; }
    }

    private void SafeLog(string message)
    {
        try { log(message ?? ""); } catch { }
    }

    private static ActionResult Result(ActionState state, string message, string reason,
        string recovery, string code)
    {
        return ActionResult.Create("截图提问", "已配置 AI 目标", state,
            message, reason, recovery, code);
    }

    private static string SafeCode(string code, string fallback)
    {
        string safe = OverlayText.SanitizeCode(code);
        return safe.Length == 0 ? fallback : safe;
    }

    private bool IsOwnedTempPath(CaptureAskPreparedImage capture)
    {
        if (capture == null || string.IsNullOrWhiteSpace(capture.Id) ||
            string.IsNullOrWhiteSpace(capture.TempPath)) return false;
        try
        {
            string expected = Path.GetFullPath(Path.Combine(tempRoot, capture.Id + ".png"));
            string actual = Path.GetFullPath(capture.TempPath);
            return actual.Equals(expected, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }
}
