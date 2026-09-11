using System;
using System.Drawing;
using System.IO;
using System.Threading;

internal static class CaptureAskServiceTests
{
    private static int Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "vibe-flow-capture-ask-test-" +
            Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            TestPreviewClipboardAndCleanup(root);
            TestUnverifiedTargetStopsBeforeFocus(root);
            TestFocusFailureStopsBeforeClipboard(root);
            TestRecordingCancelsAfterFocus(root);
            TestFinalFocusLossStopsBeforeClipboard(root);
            TestServiceCancellationInvalidatesSharedCommitGate(root);
            TestRecordingAfterClipboardDisclosesClipboardSideEffect(root);
            TestFocusLossAfterClipboardDisclosesClipboardSideEffect(root);
            TestRecordingCancelsAtPasteCommit(root);
            TestRecordingCancellationWinsDuringPasteAdmission(root);
            TestSuccessfulPasteDispatch(root);
            TestSingleActiveOperation(root);
            TestVirtualScreenSelectionNormalization();
            TestFocusedTargetEvidenceMustMatchDescriptor();
            TestExplicitProjectTargetDoesNotFallback();
            TestCleanupRejectsPathOutsideDedicatedTempRoot(root);
            TestForegroundMustRemainStableBeforeCapture();
            Console.WriteLine("Capture & Ask service tests passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Capture & Ask service tests failed: " + ex.Message);
            return 1;
        }
        finally
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
        }
    }

    private static void TestPreviewClipboardAndCleanup(string root)
    {
        var clipboard = new RecordingImageClipboard();
        var paste = new RecordingPasteDispatcher();
        var service = NewService(Path.Combine(root, "pixels"), clipboard, paste,
            delegate { return FocusSuccess(); }, delegate { return true; }, delegate { return false; });
        CaptureAskPrepareResult prepared;
        using (Bitmap source = FourByThreeFixture())
            prepared = service.Prepare(source, new Rectangle(1, 1, 2, 2), "region");
        Require(prepared.IsSuccess && prepared.Capture != null, "A valid region was not prepared");
        using (Bitmap preview = prepared.Capture.CreateImageCopy())
        {
            Require(preview.Width == 2 && preview.Height == 2, "Prepared preview has the wrong bounds");
            Require(preview.GetPixel(0, 0).ToArgb() == Color.Magenta.ToArgb(),
                "Prepared preview pixels do not match the selected source region");
        }
        Require(File.Exists(prepared.Capture.TempPath), "Prepared capture did not create a temporary PNG");
        using (Bitmap persisted = new Bitmap(prepared.Capture.TempPath))
            Require(persisted.GetPixel(0, 0).ToArgb() == Color.Magenta.ToArgb(),
                "Temporary PNG does not match the preview image");
        ActionResult copied = service.CopyToClipboard(prepared.Capture);
        Require(copied.IsSuccess && clipboard.SetCalls == 1 && clipboard.LastImage != null,
            "Copy did not write exactly one image to the clipboard boundary");
        Require(clipboard.LastImage.GetPixel(0, 0).ToArgb() == Color.Magenta.ToArgb(),
            "Clipboard image does not match the preview image");
        ActionResult cleaned = service.Cleanup(prepared.Capture);
        Require(cleaned.IsSuccess && !File.Exists(prepared.Capture.TempPath),
            "Capture cleanup left the temporary PNG behind");
        clipboard.Dispose();
    }

    private static void TestUnverifiedTargetStopsBeforeFocus(string root)
    {
        int focusCalls = 0;
        var clipboard = new RecordingImageClipboard();
        var paste = new RecordingPasteDispatcher();
        var service = NewService(Path.Combine(root, "unverified"), clipboard, paste,
            delegate { focusCalls++; return FocusSuccess(); }, delegate { return true; }, delegate { return false; });
        CaptureAskPrepareResult prepared = PrepareOnePixel(service);
        FocusTargetDescriptor target = ValidTarget();
        target.LastVerifiedUtc = null;
        ActionResult result = service.PasteToTarget(prepared.Capture, target, 1000);
        Require(result.State == ActionState.Error && result.ErrorCode == "FOCUS-TARGET-UNVERIFIED",
            "An unverified target did not fail closed");
        Require(focusCalls == 0 && clipboard.SetCalls == 0 && paste.PasteCalls == 0,
            "An unverified target caused focus, clipboard, or paste side effects");
        service.Cleanup(prepared.Capture);
        clipboard.Dispose();
    }

    private static void TestFocusFailureStopsBeforeClipboard(string root)
    {
        var clipboard = new RecordingImageClipboard();
        var paste = new RecordingPasteDispatcher();
        var service = NewService(Path.Combine(root, "focus-failure"), clipboard, paste,
            delegate
            {
                return ActionResult.Create("锁定输入目标", "Fixture", ActionState.Error,
                    "未锁定输入目标，未发送按键", "目标失效", "重新学习输入目标", "FOCUS-TARGET-STALE");
            }, delegate { return true; }, delegate { return false; });
        CaptureAskPrepareResult prepared = PrepareOnePixel(service);
        ActionResult result = service.PasteToTarget(prepared.Capture, ValidTarget(), 1000);
        Require(result.State == ActionState.Error && result.ErrorCode == "FOCUS-TARGET-STALE",
            "A failed Focus result was not preserved");
        Require(clipboard.SetCalls == 0 && paste.PasteCalls == 0,
            "Focus failure still wrote the clipboard or dispatched paste");
        service.Cleanup(prepared.Capture);
        clipboard.Dispose();
    }

    private static void TestRecordingCancelsAfterFocus(string root)
    {
        var clipboard = new RecordingImageClipboard();
        var paste = new RecordingPasteDispatcher();
        CaptureAskService service = null;
        service = NewService(Path.Combine(root, "voice-cancel"), clipboard, paste,
            delegate { service.CancelForRecording(); return FocusSuccess(); },
            delegate { return true; }, delegate { return false; });
        CaptureAskPrepareResult prepared = PrepareOnePixel(service);
        ActionResult result = service.PasteToTarget(prepared.Capture, ValidTarget(), 1000);
        Require(result.State == ActionState.Canceled && result.ErrorCode == "CAPTURE-ASK-CANCELED-VOICE",
            "Recording did not cancel the remaining paste steps");
        Require(clipboard.SetCalls == 0 && paste.PasteCalls == 0,
            "Recording cancellation still wrote the clipboard or dispatched paste");
        service.Cleanup(prepared.Capture);
        clipboard.Dispose();
    }

    private static void TestFinalFocusLossStopsBeforeClipboard(string root)
    {
        var clipboard = new RecordingImageClipboard();
        var paste = new RecordingPasteDispatcher();
        var service = NewService(Path.Combine(root, "focus-loss"), clipboard, paste,
            delegate { return FocusSuccess(); }, delegate { return false; }, delegate { return false; });
        CaptureAskPrepareResult prepared = PrepareOnePixel(service);
        ActionResult result = service.PasteToTarget(prepared.Capture, ValidTarget(), 1000);
        Require(result.State == ActionState.Error && result.ErrorCode == "CAPTURE-ASK-FOCUS-LOST",
            "A lost final focus was not reported");
        Require(clipboard.SetCalls == 0 && paste.PasteCalls == 0,
            "Final focus loss still wrote the clipboard or dispatched paste");
        service.Cleanup(prepared.Capture);
        clipboard.Dispose();
    }

    private static void TestSuccessfulPasteDispatch(string root)
    {
        int verificationCalls = 0;
        var clipboard = new RecordingImageClipboard();
        var paste = new RecordingPasteDispatcher();
        var service = NewService(Path.Combine(root, "success"), clipboard, paste,
            delegate { return FocusSuccess(); }, delegate { verificationCalls++; return true; },
            delegate { return false; });
        CaptureAskPrepareResult prepared = PrepareOnePixel(service);
        ActionResult result = service.PasteToTarget(prepared.Capture, ValidTarget(), 1000);
        Require(result.IsSuccess && result.Message == "粘贴动作已派发，请按住录音键描述问题",
            "Successful paste used an unverified completion claim");
        Require(verificationCalls == 3 && clipboard.SetCalls == 1 && paste.PasteCalls == 1,
            "Successful paste did not revalidate focus at the final commit boundary");
        service.Cleanup(prepared.Capture);
        clipboard.Dispose();
    }

    private static void TestRecordingCancelsAtPasteCommit(string root)
    {
        var clipboard = new RecordingImageClipboard();
        var paste = new RecordingPasteDispatcher();
        var gate = new RecordingPriorityCommitGate(delegate { return false; });
        int verificationCalls = 0;
        CaptureAskService service = null;
        try
        {
            service = Activator.CreateInstance(typeof(CaptureAskService),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, new object[] {
                    Path.Combine(root, "voice-commit-race"), clipboard, paste,
                    new CaptureAskFocusExecutor(delegate { return FocusSuccess(); }),
                    new CaptureAskFocusVerifier(delegate(FocusTargetDescriptor ignored)
                    {
                        verificationCalls++;
                        if (verificationCalls == 2) gate.CancelForRecording();
                        return true;
                    }),
                    new Func<bool>(delegate { return false; }), null, gate
                }, null) as CaptureAskService;
        }
        catch (MissingMethodException)
        {
            throw new InvalidOperationException(
                "Capture & Ask has no shared recording-priority commit boundary");
        }
        Require(service != null, "Capture & Ask recording-priority service could not be created");
        CaptureAskPrepareResult prepared = PrepareOnePixel(service);
        ActionResult result = service.PasteToTarget(prepared.Capture, ValidTarget(), 1000);
        Require(result.State == ActionState.Canceled &&
            result.ErrorCode == "CAPTURE-ASK-CANCELED-VOICE" && paste.PasteCalls == 0,
            "Recording cancellation crossed the final Capture & Ask paste boundary");
        service.Cleanup(prepared.Capture);
        clipboard.Dispose();
    }

    private static void TestRecordingCancellationWinsDuringPasteAdmission(string root)
    {
        var clipboard = new RecordingImageClipboard();
        var paste = new RecordingPasteDispatcher();
        var gate = new RecordingPriorityCommitGate(delegate { return false; });
        var predicateEntered = new ManualResetEvent(false);
        var releasePredicate = new ManualResetEvent(false);
        int verificationCalls = 0;
        var service = new CaptureAskService(Path.Combine(root, "voice-admission-race"), clipboard, paste,
            delegate { return FocusSuccess(); }, delegate
            {
                verificationCalls++;
                if (verificationCalls == 3)
                {
                    predicateEntered.Set();
                    releasePredicate.WaitOne(2000);
                }
                return true;
            }, delegate { return false; }, null, gate);
        CaptureAskPrepareResult prepared = PrepareOnePixel(service);
        ActionResult result = null;
        var pasteThread = new Thread(new ThreadStart(delegate
        {
            result = service.PasteToTarget(prepared.Capture, ValidTarget(), 3000);
        }));
        pasteThread.SetApartmentState(ApartmentState.STA);
        try
        {
            pasteThread.Start();
            Require(predicateEntered.WaitOne(1000),
                "Capture & Ask did not reach the final paste admission boundary");
            service.CancelForRecording();
            releasePredicate.Set();
            Require(pasteThread.Join(3500), "Capture & Ask admission fixture did not finish");
            Require(result != null && result.State == ActionState.Canceled &&
                result.ErrorCode == "CAPTURE-ASK-CANCELED-VOICE" && paste.PasteCalls == 0,
                "Recording cancellation crossed the final paste admission boundary");
        }
        finally
        {
            releasePredicate.Set();
            pasteThread.Join(3500);
            service.Cleanup(prepared.Capture);
            clipboard.Dispose();
            predicateEntered.Dispose();
            releasePredicate.Dispose();
        }
    }

    private static void TestServiceCancellationInvalidatesSharedCommitGate(string root)
    {
        var clipboard = new RecordingImageClipboard();
        var paste = new RecordingPasteDispatcher();
        var gate = new RecordingPriorityCommitGate(delegate { return false; });
        var service = new CaptureAskService(Path.Combine(root, "shared-gate-cancel"), clipboard, paste,
            delegate { return FocusSuccess(); }, delegate { return true; }, delegate { return false; },
            null, gate);
        long epoch = gate.CaptureEpoch();
        int commits = 0;

        service.CancelForRecording();
        bool committed = gate.TryCommit(epoch, delegate { return false; }, delegate { commits++; });

        Require(!committed && commits == 0,
            "Capture & Ask recording cancellation did not invalidate the shared commit gate");
        clipboard.Dispose();
    }

    private static void TestRecordingAfterClipboardDisclosesClipboardSideEffect(string root)
    {
        var clipboard = new RecordingImageClipboard();
        var paste = new RecordingPasteDispatcher();
        CaptureAskService service = null;
        clipboard.AfterSet = delegate { service.CancelForRecording(); };
        service = NewService(Path.Combine(root, "voice-after-clipboard"), clipboard, paste,
            delegate { return FocusSuccess(); }, delegate { return true; }, delegate { return false; });
        CaptureAskPrepareResult prepared = PrepareOnePixel(service);

        ActionResult result = service.PasteToTarget(prepared.Capture, ValidTarget(), 1000);

        Require(result.State == ActionState.Canceled &&
            result.ErrorCode == "CAPTURE-ASK-CANCELED-VOICE" &&
            result.Message.Contains("截图仍在剪贴板") && clipboard.SetCalls == 1 && paste.PasteCalls == 0,
            "Recording cancellation after the clipboard write hid the clipboard side effect");
        service.Cleanup(prepared.Capture);
        clipboard.Dispose();
    }

    private static void TestFocusLossAfterClipboardDisclosesClipboardSideEffect(string root)
    {
        int verificationCalls = 0;
        var clipboard = new RecordingImageClipboard();
        var paste = new RecordingPasteDispatcher();
        var service = NewService(Path.Combine(root, "focus-loss-after-clipboard"), clipboard, paste,
            delegate { return FocusSuccess(); }, delegate
            {
                verificationCalls++;
                return verificationCalls == 1;
            }, delegate { return false; });
        CaptureAskPrepareResult prepared = PrepareOnePixel(service);

        ActionResult result = service.PasteToTarget(prepared.Capture, ValidTarget(), 1000);

        Require(result.State == ActionState.Error && result.ErrorCode == "CAPTURE-ASK-FOCUS-LOST" &&
            result.Message.Contains("截图仍在剪贴板") && clipboard.SetCalls == 1 && paste.PasteCalls == 0,
            "Focus loss after the clipboard write hid the clipboard side effect");
        service.Cleanup(prepared.Capture);
        clipboard.Dispose();
    }

    private static void TestSingleActiveOperation(string root)
    {
        var entered = new ManualResetEvent(false);
        var release = new ManualResetEvent(false);
        var clipboard = new RecordingImageClipboard();
        var paste = new RecordingPasteDispatcher();
        var service = NewService(Path.Combine(root, "single-operation"), clipboard, paste,
            delegate { entered.Set(); release.WaitOne(3000); return FocusSuccess(); },
            delegate { return true; }, delegate { return false; });
        CaptureAskPrepareResult prepared = PrepareOnePixel(service);
        ActionResult first = null;
        var thread = new Thread(new ThreadStart(delegate
        {
            first = service.PasteToTarget(prepared.Capture, ValidTarget(), 3000);
        }));
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Require(entered.WaitOne(1000), "The first paste did not enter Focus execution");
        ActionResult second = service.PasteToTarget(prepared.Capture, ValidTarget(), 1000);
        Require(second.State == ActionState.Warning && second.ErrorCode == "CAPTURE-ASK-BUSY",
            "A second Capture & Ask operation was allowed to run concurrently");
        release.Set();
        Require(thread.Join(3000) && first != null && first.IsSuccess,
            "The reserved Capture & Ask operation did not finish");
        service.Cleanup(prepared.Capture);
        clipboard.Dispose();
        entered.Dispose();
        release.Dispose();
    }

    private static void TestVirtualScreenSelectionNormalization()
    {
        Rectangle relative;
        Require(CaptureAskGeometry.TryToRelativeSelection(
                new Rectangle(-1920, 0, 3840, 1080), new Rectangle(-100, 50, 200, 100), out relative) &&
            relative == new Rectangle(1820, 50, 200, 100),
            "A valid selection on a negative-coordinate monitor was not normalized correctly");
        Require(!CaptureAskGeometry.TryToRelativeSelection(
                new Rectangle(-1920, 0, 3840, 1080), new Rectangle(1900, 50, 60, 100), out relative),
            "A selection outside the virtual screen was accepted");
    }

    private static void TestFocusedTargetEvidenceMustMatchDescriptor()
    {
        FocusTargetDescriptor target = ValidTarget();
        var evidence = new CaptureAskFocusedElementEvidence
        {
            ProcessName = "fixture",
            AutomationId = "chat-input",
            ControlType = "Edit",
            ClassName = "TextBox",
            ParentFingerprint = "Pane||Root",
            IsEnabled = true,
            IsKeyboardFocusable = true,
            HasKeyboardFocus = true,
            IsOffscreen = false,
            IsPassword = false,
            IsWritable = true
        };
        Require(CaptureAskFocusEvidence.Matches(target, evidence),
            "Matching writable focus evidence was rejected");
        evidence.ProcessName = "other";
        Require(!CaptureAskFocusEvidence.Matches(target, evidence),
            "Focus evidence from another process was accepted");
        evidence.ProcessName = "fixture";
        evidence.HasKeyboardFocus = false;
        Require(!CaptureAskFocusEvidence.Matches(target, evidence),
            "An element without keyboard focus was accepted");
        evidence.HasKeyboardFocus = true;
        evidence.IsWritable = false;
        Require(!CaptureAskFocusEvidence.Matches(target, evidence),
            "A read-only element was accepted as the paste target");
    }

    private static void TestExplicitProjectTargetDoesNotFallback()
    {
        var focusDocument = new FocusTargetDocument();
        FocusTargetDescriptor defaultTarget = ValidTarget();
        defaultTarget.Id = "default-target";
        defaultTarget.Name = "Default";
        focusDocument.Targets.Add(defaultTarget);
        focusDocument.DefaultTargetId = defaultTarget.Id;
        var projectDocument = new ProjectSpaceDocument();
        projectDocument.Spaces.Add(new ProjectSpace
        {
            Id = "project-one",
            Name = "Project One",
            EditorKind = "other",
            EditorExecutablePath = "C:\\Windows\\notepad.exe",
            WorkspacePath = "C:\\Workspace",
            CaptureTargetId = "deleted-target",
            Enabled = true
        });

        CaptureAskTargetResolution missing = CaptureAskTargetResolver.Resolve(
            focusDocument, projectDocument, "project-one");
        Require(missing.Target == null && missing.ErrorCode == "CAPTURE-ASK-PROJECT-TARGET-MISSING",
            "A missing explicit project target silently fell back to the default target");

        projectDocument.Spaces[0].CaptureTargetId = "";
        CaptureAskTargetResolution fallback = CaptureAskTargetResolver.Resolve(
            focusDocument, projectDocument, "project-one");
        Require(fallback.Target != null && fallback.Target.Id == "default-target" && fallback.UsedDefault,
            "The default target was not used when the project had no explicit screenshot target");
    }

    private static void TestCleanupRejectsPathOutsideDedicatedTempRoot(string root)
    {
        string serviceRoot = Path.Combine(root, "owned-temp");
        string outsidePath = Path.Combine(root, "must-remain.png");
        using (var outsideImage = new Bitmap(1, 1)) outsideImage.Save(outsidePath);
        var clipboard = new RecordingImageClipboard();
        var service = NewService(serviceRoot, clipboard, new RecordingPasteDispatcher(),
            delegate { return FocusSuccess(); }, delegate { return true; }, delegate { return false; });
        CaptureAskPreparedImage untrusted;
        using (var image = new Bitmap(1, 1))
            untrusted = new CaptureAskPreparedImage("outside", "window", outsidePath, image);
        ActionResult result = service.Cleanup(untrusted);
        Require(result.State == ActionState.Warning &&
            result.ErrorCode == "CAPTURE-ASK-CLEANUP-PATH-REJECTED" && File.Exists(outsidePath),
            "Capture cleanup deleted a PNG outside its dedicated temporary directory");
        clipboard.Dispose();
    }

    private static void TestForegroundMustRemainStableBeforeCapture()
    {
        var stability = new CaptureAskForegroundStability(3, 160);
        Require(!stability.Observe(new IntPtr(11), 0) &&
            !stability.Observe(new IntPtr(11), 80) &&
            stability.Observe(new IntPtr(11), 170),
            "Foreground capture did not wait for a stable target window");
        Require(!stability.Observe(new IntPtr(22), 190) &&
            !stability.Observe(new IntPtr(22), 300) &&
            stability.Observe(new IntPtr(22), 360),
            "Foreground stability did not reset when the target window changed");
    }

    private static CaptureAskService NewService(string root, RecordingImageClipboard clipboard,
        RecordingPasteDispatcher paste, CaptureAskFocusExecutor focus,
        CaptureAskFocusVerifier verifier, Func<bool> recording)
    {
        return new CaptureAskService(root, clipboard, paste, focus, verifier, recording, null);
    }

    private static CaptureAskPrepareResult PrepareOnePixel(CaptureAskService service)
    {
        using (var bitmap = new Bitmap(1, 1))
        {
            bitmap.SetPixel(0, 0, Color.Cyan);
            return service.Prepare(bitmap, new Rectangle(0, 0, 1, 1), "window");
        }
    }

    private static Bitmap FourByThreeFixture()
    {
        var bitmap = new Bitmap(4, 3);
        bitmap.SetPixel(0, 0, Color.Red);
        bitmap.SetPixel(1, 0, Color.Green);
        bitmap.SetPixel(2, 0, Color.Blue);
        bitmap.SetPixel(3, 0, Color.White);
        bitmap.SetPixel(0, 1, Color.Black);
        bitmap.SetPixel(1, 1, Color.Magenta);
        bitmap.SetPixel(2, 1, Color.Yellow);
        bitmap.SetPixel(3, 1, Color.Gray);
        bitmap.SetPixel(0, 2, Color.Orange);
        bitmap.SetPixel(1, 2, Color.Purple);
        bitmap.SetPixel(2, 2, Color.Brown);
        bitmap.SetPixel(3, 2, Color.Pink);
        return bitmap;
    }

    private static FocusTargetDescriptor ValidTarget()
    {
        return new FocusTargetDescriptor
        {
            Id = "fixture-target",
            Name = "Fixture",
            ProcessName = "fixture",
            AutomationId = "chat-input",
            ControlType = "Edit",
            ClassName = "TextBox",
            ParentFingerprint = "Pane||Root",
            Strategy = "uia",
            LastVerifiedUtc = DateTime.UtcNow
        };
    }

    private static ActionResult FocusSuccess()
    {
        return ActionResult.Create("锁定输入目标", "Fixture", ActionState.Success,
            "目标已锁定：Fixture", "", "", "");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

internal sealed class RecordingImageClipboard : ICaptureAskImageClipboard, IDisposable
{
    internal int SetCalls;
    internal Bitmap LastImage;
    internal Action AfterSet;

    public bool TrySetImage(Bitmap image, out string errorCode)
    {
        SetCalls++;
        if (LastImage != null) LastImage.Dispose();
        LastImage = image == null ? null : new Bitmap(image);
        errorCode = image == null ? "CAPTURE-ASK-IMAGE-MISSING" : "";
        if (AfterSet != null) AfterSet();
        return image != null;
    }

    public void Dispose()
    {
        if (LastImage != null) LastImage.Dispose();
        LastImage = null;
    }
}

internal sealed class RecordingPasteDispatcher : ICaptureAskPasteDispatcher
{
    internal int PasteCalls;

    public bool TryPasteImage(out string errorCode)
    {
        PasteCalls++;
        errorCode = "";
        return true;
    }
}
