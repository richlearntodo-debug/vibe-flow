using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

internal static class FocusTargetMultiWindowTests
{
    [STAThread]
    private static int Main(string[] args)
    {
        Process first = null;
        Process second = null;
        try
        {
            if (args.Length != 1 || !File.Exists(args[0]))
                throw new InvalidOperationException("Focus smoke fixture path is missing");

            TestRecordingCommitGateAtFocusSideEffects();
            TestRecordingCancellationWinsBeforeAdmission();
            TestRecordingCancellationRevokesReservedAction();
            TestRecordingCancellationDoesNotWaitForInFlightAction();
            TestOnlyOneExternalActionCanHoldAdmission();
            TestOneCommitEpochSpansActivationAndFocus();
            TestForegroundChangeCancelsFocus();

            first = StartFixture(args[0]);
            second = StartFixture(args[0]);
            if (!WaitForWindow(first, 5000) || !WaitForWindow(second, 5000))
                throw new InvalidOperationException("Focus smoke fixtures did not expose two windows");

            var target = new FocusTargetDescriptor
            {
                Id = "duplicate-window-target",
                Name = "Duplicate window target",
                ProcessName = Path.GetFileNameWithoutExtension(args[0]),
                AutomationId = "smartFocusSmokeInput",
                ControlType = "Edit",
                Strategy = "uia",
                LastVerifiedUtc = DateTime.UtcNow
            };
            var liveGate = new RecordingPriorityCommitGate(delegate { return false; });
            var backend = new WindowsUiaFocusAutomationBackend(liveGate);
            FocusAutomationStepResult result = backend.ActivateApplication(target, 3000,
                liveGate.CaptureEpoch(),
                delegate { return false; });
            if (result.IsSuccess || result.ErrorCode != "FOCUS-TARGET-AMBIGUOUS")
                throw new InvalidOperationException("Smart Focus did not reject the same target in two application windows");

            Console.WriteLine("Smart Focus multi-window test passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Smart Focus multi-window test failed: " + ex.Message);
            return 1;
        }
        finally
        {
            StopFixture(second);
            StopFixture(first);
        }
    }

    private static void TestRecordingCommitGateAtFocusSideEffects()
    {
        var gate = new RecordingPriorityCommitGate(delegate { return false; });
        long epoch = gate.CaptureEpoch();
        int showCalls = 0;
        int foregroundCalls = 0;
        bool activated = WindowsUiaFocusAutomationBackend.TryShowAndActivateWindow(
            new IntPtr(123), gate, epoch, delegate { return false; },
            delegate(IntPtr handle, int command)
            {
                showCalls++;
                gate.CancelForRecording();
                return true;
            },
            delegate(IntPtr handle)
            {
                foregroundCalls++;
                return true;
            });
        if (activated || showCalls != 1 || foregroundCalls != 0)
            throw new InvalidOperationException(
                "Smart Focus crossed the recording boundary between window activation calls");

        epoch = gate.CaptureEpoch();
        gate.CancelForRecording();
        int setFocusCalls = 0;
        bool focused = WindowsUiaFocusAutomationBackend.TryCommitFocusAction(
            gate, epoch, delegate { return false; }, delegate { setFocusCalls++; });
        if (focused || setFocusCalls != 0)
            throw new InvalidOperationException(
                "Smart Focus executed UIA SetFocus after recording invalidated the request");
    }

    private static void TestOneCommitEpochSpansActivationAndFocus()
    {
        var gate = new RecordingPriorityCommitGate(delegate { return false; });
        var backend = new RecordingEpochFocusBackend(gate);
        var service = new FocusTargetService(backend, delegate { return false; }, null, gate,
            delegate { return "fixture"; });
        var target = new FocusTargetDescriptor
        {
            Id = "epoch-target",
            Name = "Epoch target",
            ProcessName = "fixture",
            AutomationId = "chat-input",
            ControlType = "Edit",
            Strategy = "uia",
            LastVerifiedUtc = DateTime.UtcNow
        };

        ActionResult result = service.Execute(target, 1000);

        if (result.IsSuccess || backend.ActivationEpoch != backend.FocusEpoch ||
            backend.FocusSideEffectCalls != 0)
            throw new InvalidOperationException(
                "Smart Focus recaptured its recording commit epoch between activation and SetFocus");
    }

    private static void TestForegroundChangeCancelsFocus()
    {
        bool targetSeen = false;
        if (!FocusTargetService.ShouldCancelForForegroundChange("vibemic", "other-app", "chatgpt", ref targetSeen) || targetSeen)
            throw new InvalidOperationException("Smart Focus did not cancel when the user switched away before activation");
        if (FocusTargetService.ShouldCancelForForegroundChange("vibemic", "chatgpt", "chatgpt", ref targetSeen) || !targetSeen)
            throw new InvalidOperationException("Smart Focus rejected its expected target foreground");
        if (!FocusTargetService.ShouldCancelForForegroundChange("vibemic", "other-app", "chatgpt", ref targetSeen))
            throw new InvalidOperationException("Smart Focus did not cancel after the user switched away from the target");
    }

    private static void TestRecordingCancellationWinsBeforeAdmission()
    {
        var gate = new RecordingPriorityCommitGate(delegate { return false; });
        long epoch = gate.CaptureEpoch();
        var predicateEntered = new ManualResetEvent(false);
        var releasePredicate = new ManualResetEvent(false);
        bool commitAccepted = true;
        int actionCalls = 0;
        var commitThread = new Thread(new ThreadStart(delegate
        {
            commitAccepted = gate.TryCommit(epoch, delegate
            {
                predicateEntered.Set();
                releasePredicate.WaitOne(2000);
                return false;
            }, delegate { actionCalls++; });
        }));
        try
        {
            commitThread.Start();
            if (!predicateEntered.WaitOne(1000))
                throw new InvalidOperationException("Commit gate fixture did not reach admission");
            gate.CancelForRecording();
            releasePredicate.Set();
            if (!commitThread.Join(2500))
                throw new InvalidOperationException("Commit gate fixture did not finish");
        }
        finally
        {
            releasePredicate.Set();
            commitThread.Join(2500);
            predicateEntered.Dispose();
            releasePredicate.Dispose();
        }
        if (commitAccepted || actionCalls != 0)
            throw new InvalidOperationException(
                "Recording cancellation lost the race before external-action admission");
    }

    private static void TestRecordingCancellationDoesNotWaitForInFlightAction()
    {
        var gate = new RecordingPriorityCommitGate(delegate { return false; });
        long epoch = gate.CaptureEpoch();
        var actionStarted = new ManualResetEvent(false);
        var releaseAction = new ManualResetEvent(false);
        var cancellationReturned = new ManualResetEvent(false);
        bool commitAccepted = true;
        var commitThread = new Thread(new ThreadStart(delegate
        {
            commitAccepted = gate.TryCommit(epoch, delegate { return false; }, delegate
            {
                actionStarted.Set();
                releaseAction.WaitOne(2000);
            });
        }));
        var cancellationThread = new Thread(new ThreadStart(delegate
        {
            gate.CancelForRecording();
            cancellationReturned.Set();
        }));
        try
        {
            commitThread.Start();
            if (!actionStarted.WaitOne(1000))
                throw new InvalidOperationException("Commit gate fixture action did not start");
            cancellationThread.Start();
            if (!cancellationReturned.WaitOne(250))
                throw new InvalidOperationException(
                    "Recording cancellation waited for an in-flight external action");
        }
        finally
        {
            releaseAction.Set();
            commitThread.Join(2500);
            cancellationThread.Join(2500);
            actionStarted.Dispose();
            releaseAction.Dispose();
            cancellationReturned.Dispose();
        }
        if (!commitAccepted)
            throw new InvalidOperationException(
                "An already-dispatched external action was reported as not executed");
        int lateCalls = 0;
        if (gate.TryCommit(epoch, delegate { return false; }, delegate { lateCalls++; }) || lateCalls != 0)
            throw new InvalidOperationException(
                "Recording cancellation did not reject later actions from the same request");
    }

    private static void TestRecordingCancellationRevokesReservedAction()
    {
        var gate = new RecordingPriorityCommitGate(delegate { return false; });
        long epoch = gate.CaptureEpoch();
        var reservationAcquired = new ManualResetEvent(false);
        var releaseReservation = new ManualResetEvent(false);
        bool commitAccepted = true;
        int actionCalls = 0;
        var commitThread = new Thread(new ThreadStart(delegate
        {
            commitAccepted = gate.TryCommit(epoch, delegate { return false; },
                delegate { actionCalls++; }, delegate
                {
                    reservationAcquired.Set();
                    releaseReservation.WaitOne(2000);
                });
        }));
        try
        {
            commitThread.Start();
            if (!reservationAcquired.WaitOne(1000))
                throw new InvalidOperationException("Commit gate fixture did not expose its reserved state");
            gate.CancelForRecording();
            releaseReservation.Set();
            if (!commitThread.Join(2500))
                throw new InvalidOperationException("Reserved commit fixture did not finish");
        }
        finally
        {
            releaseReservation.Set();
            commitThread.Join(2500);
            reservationAcquired.Dispose();
            releaseReservation.Dispose();
        }
        if (commitAccepted || actionCalls != 0)
            throw new InvalidOperationException(
                "Recording cancellation did not revoke an external action reserved before execution");
    }

    private static void TestOnlyOneExternalActionCanHoldAdmission()
    {
        var gate = new RecordingPriorityCommitGate(delegate { return false; });
        long epoch = gate.CaptureEpoch();
        var firstActionStarted = new ManualResetEvent(false);
        var releaseFirstAction = new ManualResetEvent(false);
        bool firstAccepted = false;
        bool secondAccepted = true;
        int secondActionCalls = 0;
        var firstThread = new Thread(new ThreadStart(delegate
        {
            firstAccepted = gate.TryCommit(epoch, delegate { return false; }, delegate
            {
                firstActionStarted.Set();
                releaseFirstAction.WaitOne(2000);
            });
        }));
        try
        {
            firstThread.Start();
            if (!firstActionStarted.WaitOne(1000))
                throw new InvalidOperationException("First external action did not acquire admission");
            secondAccepted = gate.TryCommit(epoch, delegate { return false; },
                delegate { secondActionCalls++; });
        }
        finally
        {
            releaseFirstAction.Set();
            firstThread.Join(2500);
            firstActionStarted.Dispose();
            releaseFirstAction.Dispose();
        }
        if (!firstAccepted || secondAccepted || secondActionCalls != 0)
            throw new InvalidOperationException(
                "Recording commit gate admitted two concurrent external actions");
    }

    private static Process StartFixture(string path)
    {
        return Process.Start(new ProcessStartInfo
        {
            FileName = Path.GetFullPath(path),
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Normal
        });
    }

    private static bool WaitForWindow(Process process, int timeoutMs)
    {
        Stopwatch timer = Stopwatch.StartNew();
        while (process != null && !process.HasExited && timer.ElapsedMilliseconds < timeoutMs)
        {
            process.Refresh();
            if (process.MainWindowHandle != IntPtr.Zero) return true;
            Thread.Sleep(25);
        }
        return false;
    }

    private static void StopFixture(Process process)
    {
        if (process == null) return;
        try
        {
            if (!process.HasExited)
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(1000)) process.Kill();
            }
        }
        catch { }
        finally { process.Dispose(); }
    }
}

internal sealed class RecordingEpochFocusBackend : IFocusAutomationBackend
{
    private readonly RecordingPriorityCommitGate gate;

    internal long ActivationEpoch = -1;
    internal long FocusEpoch = -2;
    internal int FocusSideEffectCalls;

    internal RecordingEpochFocusBackend(RecordingPriorityCommitGate commitGate)
    {
        gate = commitGate;
    }

    public FocusAutomationStepResult ActivateApplication(FocusTargetDescriptor target, int timeoutMs,
        long commitEpoch, Func<bool> cancellationRequested)
    {
        ActivationEpoch = commitEpoch;
        gate.CancelForRecording();
        return FocusAutomationStepResult.Success(123, new IntPtr(456));
    }

    public FocusAutomationStepResult FocusAndVerify(FocusTargetDescriptor target,
        FocusAutomationStepResult activation, int timeoutMs, long commitEpoch,
        Func<bool> cancellationRequested)
    {
        FocusEpoch = commitEpoch;
        bool committed = gate.TryCommit(commitEpoch, cancellationRequested,
            delegate { FocusSideEffectCalls++; });
        return committed
            ? FocusAutomationStepResult.Success(123, new IntPtr(456))
            : FocusAutomationStepResult.Failure("FOCUS-CANCELED");
    }
}
