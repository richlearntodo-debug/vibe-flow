using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

internal sealed class CaptureAskForegroundObserver : IDisposable
{
    private readonly System.Windows.Forms.Timer timer;
    private readonly Action observe;
    private bool disposed;

    internal CaptureAskForegroundObserver(Action observeForeground)
    {
        observe = observeForeground ?? delegate { };
        timer = new System.Windows.Forms.Timer();
        timer.Interval = 200;
        timer.Tick += delegate
        {
            try { observe(); } catch { }
        };
    }

    internal bool IsRunning { get { return !disposed && timer.Enabled; } }

    internal void Start()
    {
        if (!disposed && !timer.Enabled) timer.Start();
    }

    internal void Stop()
    {
        if (!disposed && timer.Enabled) timer.Stop();
    }

    public void Dispose()
    {
        if (disposed) return;
        timer.Stop();
        timer.Dispose();
        disposed = true;
    }
}

internal static class CaptureAskTempFileCleaner
{
    internal static void Cleanup(string directory)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return;
            foreach (string file in Directory.GetFiles(directory, "*.png"))
            {
                try { File.Delete(file); } catch { }
            }
        }
        catch { }
    }
}

internal sealed partial class VibeMicForm
{
    private WindowsCaptureAskBackend captureAskBackend;
    private CaptureAskService captureAskService;
    private CaptureAskForm captureAskForm;
    private CaptureAskForegroundObserver captureAskForegroundObserver;

    private void InitializeCaptureAsk()
    {
        CaptureAskTempFileCleaner.Cleanup(Path.Combine(userStateRoot, "capture-ask-temp"));
        captureAskBackend = new WindowsCaptureAskBackend();
        captureAskService = new CaptureAskService(
            Path.Combine(userStateRoot, "capture-ask-temp"),
            new WindowsCaptureAskImageClipboard(), new WindowsCaptureAskPasteDispatcher(),
            delegate(FocusTargetDescriptor target, int timeoutMs)
            {
                return focusTargetService.Execute(target, timeoutMs);
            },
            delegate(FocusTargetDescriptor target)
            {
                return captureAskBackend.IsFocusedTarget(target);
            }, CaptureAskRecordingHasPriority, HostLog, recordingPriorityCommitGate);
        captureAskForegroundObserver = new CaptureAskForegroundObserver(
            delegate { captureAskBackend.ObserveForegroundWindow(); });
    }

    private void ShowCaptureAsk()
    {
        if (captureAskService == null || captureAskBackend == null) InitializeCaptureAsk();
        if (CaptureAskRecordingHasPriority())
        {
            ShowActionToast(ActionResult.Create("截图提问", "屏幕截图", ActionState.Canceled,
                "录音正在进行，本次未打开截图提问", "录音操作优先",
                "录音结束后重试", "CAPTURE-ASK-CANCELED-VOICE"));
            return;
        }
        if (captureAskForm != null && !captureAskForm.IsDisposed)
        {
            StartCaptureAskForegroundObservation();
            captureAskForm.Show();
            captureAskForm.Activate();
            return;
        }

        focusTargetDocument = LoadFocusTargetDocument();
        ReloadProjectSpaceDocument();
        CaptureAskTargetResolution resolution = CaptureAskTargetResolver.Resolve(
            focusTargetDocument, projectSpaceDocument, currentProjectSpaceId);
        IList<FocusTargetDescriptor> targets = CaptureAskTargetResolver.VerifiedTargets(focusTargetDocument);
        string preferredTargetId = resolution.Target == null ? "" : resolution.Target.Id;
        if (resolution.Target == null)
            ShowActionToast(CaptureAskTargetProblem(resolution.ErrorCode));

        captureAskForm = new CaptureAskForm(this, captureAskService, captureAskBackend, targets,
            preferredTargetId, CaptureAskRecordingHasPriority, ShowActionToast,
            delegate
            {
                StopCaptureAskForegroundObservation();
                captureAskForm = null;
            });
        captureAskForm.ApplyTheme(darkTheme);
        captureAskForm.Icon = Icon;
        StartCaptureAskForegroundObservation();
        captureAskForm.Show(this);
        captureAskForm.Activate();
    }

    private void StartCaptureAskForegroundObservation()
    {
        if (captureAskBackend != null) captureAskBackend.ObserveForegroundWindow();
        if (captureAskForegroundObserver != null) captureAskForegroundObserver.Start();
    }

    private void StopCaptureAskForegroundObservation()
    {
        if (captureAskForegroundObserver != null) captureAskForegroundObserver.Stop();
    }

    private static ActionResult CaptureAskTargetProblem(string errorCode)
    {
        string code = string.IsNullOrWhiteSpace(errorCode) ? "FOCUS-TARGET-MISSING" : errorCode;
        bool projectTarget = code.StartsWith("CAPTURE-ASK-PROJECT-TARGET-", StringComparison.OrdinalIgnoreCase);
        return ActionResult.Create("截图提问", projectTarget ? "当前项目" : "输入目标", ActionState.Warning,
            projectTarget ? "当前项目的截图目标不可用，不会自动改用其他目标" :
                "尚未设置已验证截图目标；仍可截图并复制",
            projectTarget ? "项目中指定的目标已删除或需要重新验证" : "没有可安全执行粘贴的默认目标",
            projectTarget ? "编辑项目并重新选择截图目标" : "设置并测试输入目标",
            code);
    }

    private bool CaptureAskRecordingHasPriority()
    {
        return IsVoiceKeyHeld() || string.Equals(currentVisualState, "recording",
            StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshCaptureAskTheme()
    {
        if (captureAskForm != null && !captureAskForm.IsDisposed)
            captureAskForm.ApplyTheme(darkTheme);
    }

    private void CancelCaptureAskServiceForRecording()
    {
        if (captureAskService != null) captureAskService.CancelForRecording();
    }

    private void NotifyCaptureAskUiForRecording()
    {
        CaptureAskForm openForm = captureAskForm;
        if (openForm == null || openForm.IsDisposed) return;
        DispatchUi(delegate
        {
            if (!openForm.IsDisposed) openForm.HandleRecordingStarted();
        });
    }

    private void ShutdownCaptureAsk()
    {
        if (captureAskForegroundObserver != null)
        {
            captureAskForegroundObserver.Dispose();
            captureAskForegroundObserver = null;
        }
        if (captureAskService != null) captureAskService.CancelCurrent();
        if (captureAskForm != null && !captureAskForm.IsDisposed)
        {
            captureAskForm.CloseForOwnerShutdown();
            captureAskForm = null;
        }
        CleanupCaptureAskTempDirectory();
    }

    private void CleanupCaptureAskTempDirectory()
    {
        CaptureAskTempFileCleaner.Cleanup(Path.Combine(userStateRoot, "capture-ask-temp"));
    }
}
