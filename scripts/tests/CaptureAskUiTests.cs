using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

internal static class CaptureAskUiTests
{
    [STAThread]
    private static int Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "vibe-flow-capture-ui-test-" +
            Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            TestForegroundObserverIsOptIn();
            TestStartupCleanupIsLimitedToCapturePng(root);
            var target = new FocusTargetDescriptor
            {
                Id = "fixture-target",
                Name = "Fixture Chat",
                ProcessName = "notepad",
                AutomationId = "editor",
                ControlType = "Edit",
                ClassName = "Edit",
                ParentFingerprint = "Window||Notepad",
                Strategy = "uia",
                LastVerifiedUtc = DateTime.UtcNow
            };
            var service = new CaptureAskService(root, new UiImageClipboard(), new UiPasteDispatcher(),
                delegate { return ActionResult.Create("锁定输入目标", "Fixture Chat", ActionState.Success,
                    "目标已锁定：Fixture Chat", "", "", ""); }, delegate { return true; },
                delegate { return false; }, null);
            using (var owner = new Form())
            using (var form = new CaptureAskForm(owner, service, new WindowsCaptureAskBackend(),
                new List<FocusTargetDescriptor> { target }, "", delegate { return false; },
                delegate { }, null))
            {
                Require(form.AutoScaleMode == AutoScaleMode.Dpi, "Capture & Ask form is not DPI-scaled");
                Require(form.MinimumSize.Width >= 620 && form.MinimumSize.Height >= 520,
                    "Capture & Ask form does not protect its working layout");
                Panel scrollHost = Find(form, "captureAskScrollHost") as Panel;
                Require(scrollHost != null && scrollHost.AutoScroll,
                    "Capture & Ask content cannot scroll in a high-DPI small work area");
                MethodInfo fitWindow = typeof(CaptureAskForm).GetMethod("FitWindowToWorkingArea",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Require(fitWindow != null,
                    "Capture & Ask does not clamp its scaled window to the current work area");
                Size fitted = (Size)fitWindow.Invoke(null, new object[]
                {
                    new Size(1560, 1400), new Rectangle(0, 0, 1366, 728)
                });
                Require(fitted.Width <= 1342 && fitted.Height <= 704,
                    "Capture & Ask scaled bounds can leave footer actions outside 1366x768");
                Require(Find(form, "captureCurrentWindowButton") is Button,
                    "Current-window capture action is missing");
                Require(Find(form, "captureRegionButton") is Button,
                    "Region capture action is missing");
                Require(Find(form, "captureAskPreview") is PictureBox,
                    "Screenshot preview is missing");
                Require(Find(form, "captureAskTarget") is ComboBox,
                    "Verified target selector is missing");
                Require(Find(form, "captureAskState") is Label,
                    "Visible action state is missing");
                Require(Find(form, "captureAskRetakeButton") is Button,
                    "Retake action is missing");
                Require(Find(form, "captureAskCancelButton") is Button,
                    "Cancel action is missing");
                Button copy = Find(form, "captureAskCopyButton") as Button;
                Button paste = Find(form, "captureAskPasteButton") as Button;
                Require(copy != null && paste != null && !copy.Enabled && !paste.Enabled,
                    "Copy or paste was enabled before a screenshot was prepared");
                CaptureAskPrepareResult prepared;
                using (var bitmap = new Bitmap(2, 2))
                    prepared = service.Prepare(bitmap, new Rectangle(0, 0, 2, 2), "window");
                MethodInfo setPrepared = typeof(CaptureAskForm).GetMethod("SetPreparedCapture",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Require(prepared.IsSuccess && setPrepared != null,
                    "Capture & Ask UI test could not prepare a screenshot fixture");
                setPrepared.Invoke(form, new object[] { prepared.Capture });
                ComboBox selector = Find(form, "captureAskTarget") as ComboBox;
                Require(copy.Enabled && !paste.Enabled && selector != null,
                    "A prepared screenshot ignored the explicit no-target state");
                selector.SelectedIndex = 0;
                Require(paste.Enabled,
                    "Selecting a verified target did not enable screenshot paste");

                MethodInfo applyTheme = typeof(CaptureAskForm).GetMethod("ApplyTheme",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Require(applyTheme != null,
                    "Capture & Ask cannot apply the active Host theme");
                applyTheme.Invoke(form, new object[] { true });
                Require(form.BackColor == Color.FromArgb(25, 26, 31) &&
                    selector.BackColor == Color.FromArgb(31, 33, 39) &&
                    selector.ForeColor == Color.FromArgb(229, 232, 239),
                    "Capture & Ask did not apply the dark surface and input palette");
                Require(selector.FlatStyle == FlatStyle.Flat,
                    "Capture & Ask target selector cannot render its dark palette");
                Require(copy.BackColor == Color.FromArgb(41, 43, 51) &&
                    copy.ForeColor == Color.FromArgb(229, 232, 239),
                    "Capture & Ask secondary actions are unreadable in dark mode");

                form.Show();
                form.Scale(new SizeF(2f, 2f));
                MethodInfo applyWorkingArea = typeof(CaptureAskForm).GetMethod(
                    "ApplyWorkingAreaLayout", BindingFlags.Instance | BindingFlags.NonPublic);
                Require(applyWorkingArea != null,
                    "Capture & Ask production working-area layout hook is missing");
                applyWorkingArea.Invoke(form, new object[] { new Rectangle(0, 0, 1366, 728) });
                Application.DoEvents();
                Require(form.Width <= 1342 && form.Height <= 704 &&
                    scrollHost.DisplayRectangle.Height > scrollHost.ClientSize.Height,
                    "Capture & Ask did not expose a scroll range after 200% scaling");
                scrollHost.ScrollControlIntoView(paste);
                Application.DoEvents();
                Point pasteTopLeft = scrollHost.PointToClient(paste.PointToScreen(Point.Empty));
                Require(pasteTopLeft.Y >= 0 && pasteTopLeft.Y + paste.Height <= scrollHost.ClientSize.Height,
                    "Capture & Ask footer cannot be reached by scrolling at 200% scale");
                form.Hide();
                form.Close();
                Application.DoEvents();
            }
            using (var closingOwner = new Form())
            {
                closingOwner.Show();
                using (var form = new CaptureAskForm(closingOwner, service,
                    new WindowsCaptureAskBackend(), new List<FocusTargetDescriptor> { target },
                    target.Id, delegate { return false; }, delegate { }, null))
                {
                    form.Show(closingOwner);
                    closingOwner.Hide();
                    form.CloseForOwnerShutdown();
                    Application.DoEvents();
                    Require(!closingOwner.Visible,
                        "Closing Capture & Ask during Host shutdown reactivated the Host window");
                }
            }
            ActionResult completionResult = null;
            int recordingChecks = 0;
            using (var recordingOwner = new Form())
            using (var form = new CaptureAskForm(recordingOwner, service,
                new WindowsCaptureAskBackend(), new List<FocusTargetDescriptor> { target },
                target.Id, delegate { recordingChecks++; return recordingChecks >= 2; },
                delegate(ActionResult result)
                {
                    completionResult = result;
                }, null))
            {
                recordingOwner.Show();
                form.Show(recordingOwner);
                form.Hide();
                recordingOwner.Hide();
                using (var bitmap = new Bitmap(4, 4))
                {
                    CaptureAskScreenResult captured = CaptureAskScreenResult.Success(
                        new Bitmap(bitmap), new Rectangle(0, 0, 4, 4), new Rectangle(0, 0, 4, 4));
                    MethodInfo complete = typeof(CaptureAskForm).GetMethod("CompleteWindowCapture",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    Require(complete != null, "Capture completion hook is missing");
                    complete.Invoke(form, new object[] { captured });
                }
                Application.DoEvents();
                Require(!form.Visible && !recordingOwner.Visible && completionResult != null &&
                    completionResult.State == ActionState.Canceled &&
                    completionResult.ErrorCode == "CAPTURE-ASK-CANCELED-VOICE",
                    "Recording started during capture completion restored or activated Capture & Ask");
                Require(Directory.GetFiles(root, "*.png").Length == 0,
                    "Recording started after capture preparation left a temporary PNG behind");
            }
            completionResult = null;
            using (var recordingOwner = new Form())
            using (var form = new CaptureAskForm(recordingOwner, service,
                new WindowsCaptureAskBackend(), new List<FocusTargetDescriptor> { target },
                target.Id, delegate { return true; }, delegate(ActionResult result)
                {
                    completionResult = result;
                }, null))
            using (var bitmap = new Bitmap(4, 4))
            {
                CaptureAskPrepareResult regionPrepared = service.Prepare(bitmap,
                    new Rectangle(0, 0, 4, 4), "region");
                MethodInfo abortCompletion = typeof(CaptureAskForm).GetMethod(
                    "AbortCaptureCompletionForRecording", BindingFlags.Instance | BindingFlags.NonPublic);
                Require(regionPrepared.IsSuccess && abortCompletion != null,
                    "Region completion cancellation hook is missing");
                bool aborted = (bool)abortCompletion.Invoke(form,
                    new object[] { regionPrepared.Capture });
                Application.DoEvents();
                Require(aborted && completionResult != null &&
                    completionResult.ErrorCode == "CAPTURE-ASK-CANCELED-VOICE" &&
                    Directory.GetFiles(root, "*.png").Length == 0,
                    "Region capture completion did not clean the prepared image when recording started");
            }
            using (var desktop = new Bitmap(320, 200))
            using (var selector = new CaptureAskRegionForm(desktop,
                new Rectangle(0, 0, 320, 200), delegate { return false; }))
            {
                Require(selector.ShowInTaskbar && selector.Name == "captureAskRegionForm" &&
                    selector.AccessibleName == "选择截图区域",
                    "The region selector cannot be located for keyboard or UI Automation access");
            }
            int voiceCancellationResults = 0;
            using (var form = new CaptureAskForm(null, service,
                new WindowsCaptureAskBackend(), new List<FocusTargetDescriptor> { target },
                target.Id, delegate { return true; }, delegate(ActionResult result)
                {
                    if (result != null && result.ErrorCode == "CAPTURE-ASK-CANCELED-VOICE")
                        voiceCancellationResults++;
                }, null))
            {
                form.HandleRecordingStarted();
                MethodInfo handleFailure = typeof(CaptureAskForm).GetMethod("HandleCaptureFailure",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Require(handleFailure != null, "Capture failure handler is missing");
                handleFailure.Invoke(form, new object[] { "CAPTURE-ASK-CANCELED-VOICE" });
                Require(voiceCancellationResults == 1,
                    "One recording event published duplicate Capture & Ask cancellation results");
            }
            Console.WriteLine("Capture & Ask UI tests passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Capture & Ask UI tests failed: " + ex.Message);
            return 1;
        }
        finally
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
        }
    }

    private static void TestForegroundObserverIsOptIn()
    {
        int observations = 0;
        using (var observer = new CaptureAskForegroundObserver(delegate { observations++; }))
        {
            Require(!observer.IsRunning,
                "Capture & Ask foreground observation started before the feature was opened");
            observer.Start();
            Require(observer.IsRunning,
                "Capture & Ask foreground observation did not start with the feature");
            observer.Stop();
            Require(!observer.IsRunning,
                "Capture & Ask foreground observation continued after the feature closed");
        }
        Require(observations == 0,
            "Capture & Ask observer executed synchronously while changing lifecycle state");
    }

    private static void TestStartupCleanupIsLimitedToCapturePng(string root)
    {
        string cleanupRoot = Path.Combine(root, "startup-cleanup");
        Directory.CreateDirectory(cleanupRoot);
        string stalePng = Path.Combine(cleanupRoot, "stale.png");
        string unrelated = Path.Combine(cleanupRoot, "keep.txt");
        File.WriteAllBytes(stalePng, new byte[] { 1, 2, 3 });
        File.WriteAllText(unrelated, "keep");

        CaptureAskTempFileCleaner.Cleanup(cleanupRoot);

        Require(!File.Exists(stalePng) && File.Exists(unrelated),
            "Capture & Ask startup cleanup did not stay within its PNG ownership boundary");
    }

    private static Control Find(Control root, string name)
    {
        if (root == null) return null;
        if (string.Equals(root.Name, name, StringComparison.Ordinal)) return root;
        foreach (Control child in root.Controls)
        {
            Control found = Find(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

internal sealed class UiImageClipboard : ICaptureAskImageClipboard
{
    public bool TrySetImage(Bitmap image, out string errorCode)
    {
        errorCode = image == null ? "CAPTURE-ASK-IMAGE-MISSING" : "";
        return image != null;
    }
}

internal sealed class UiPasteDispatcher : ICaptureAskPasteDispatcher
{
    public bool TryPasteImage(out string errorCode)
    {
        errorCode = "";
        return true;
    }
}
