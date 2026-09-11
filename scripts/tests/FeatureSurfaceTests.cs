using System;
using System.Reflection;

internal static class FeatureSurfaceTests
{
    private static int Main()
    {
        try
        {
            TestNotesNavigationIsRemoved();
            TestVoiceTargetObservationFailsClosed();
            TestTransientFocusedVoiceTargetPolicy();
            TestVoiceFocusPreflightRequiresCurrentLease();
            TestVoiceWakeRecoveryRequiresFocusWhenCold();
            TestRecordingKeepsVerifiedVoiceFocusLock();
            TestVoiceFocusRestoreOnlyRepairsProviderTakeover();
            TestVoiceFocusRestoresWhenProviderIsReady();
            TestVoiceWakeNeverExpeditesProviderLaunchDuringRecording();
            TestFocusRecoveryRetriesOnlyTransientTargetLoss();
            TestFocusRecoveryLeaseSurvivesTargetRefreshOnly();
            Console.WriteLine("Feature surface tests passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Feature surface tests failed: " + ex.Message);
            return 1;
        }
    }

    private static void TestNotesNavigationIsRemoved()
    {
        FieldInfo field = typeof(VibeMicForm).GetField("NavigationText",
            BindingFlags.Static | BindingFlags.NonPublic);
        if (field == null) throw new InvalidOperationException("Navigation text is unavailable");
        string[] labels = field.GetValue(null) as string[];
        if (labels == null || Array.Exists(labels, label => string.Equals(label, "便签", StringComparison.Ordinal)))
            throw new InvalidOperationException("便签仍然是主导航入口");
    }

    // The per-application friendly target name (SuggestedTargetName) lived on the retired
    // FocusTargetDialog. The favourite-app flow now takes its display name from the installed-application
    // catalog instead, which is asserted separately, so this fixture went with the dialog.

    private static void TestVoiceTargetObservationFailsClosed()
    {
        Type type = typeof(FocusTargetService);
        MethodInfo method = type.GetMethod("EvaluateVoiceTargetObservation",
            BindingFlags.Static | BindingFlags.NonPublic);
        if (method == null) throw new InvalidOperationException("Voice target observation policy is unavailable");

        object ready = method.Invoke(null, new object[] { true, true, true, true });
        object missingTarget = method.Invoke(null, new object[] { false, true, true, true });
        object wrongProcess = method.Invoke(null, new object[] { true, false, true, true });
        object nonEditable = method.Invoke(null, new object[] { true, true, false, true });
        object focusLost = method.Invoke(null, new object[] { true, true, true, false });

        if (!Convert.ToBoolean(ready) || Convert.ToBoolean(missingTarget) ||
            Convert.ToBoolean(wrongProcess) || Convert.ToBoolean(nonEditable) ||
            Convert.ToBoolean(focusLost))
            throw new InvalidOperationException("Voice target observation did not fail closed");
    }

    private static void TestRecordingKeepsVerifiedVoiceFocusLock()
    {
        MethodInfo method = typeof(VibeMicForm).GetMethod("ShouldPreserveVoiceFocusLock",
            BindingFlags.Static | BindingFlags.NonPublic);
        if (method == null) throw new InvalidOperationException("Recording focus-lock lease policy is unavailable");

        bool heldAndArmed = Convert.ToBoolean(method.Invoke(null, new object[] { true, false, true }));
        bool recordingAndArmed = Convert.ToBoolean(method.Invoke(null, new object[] { false, true, true }));
        bool idleAndArmed = Convert.ToBoolean(method.Invoke(null, new object[] { false, false, true }));
        bool heldWithoutLock = Convert.ToBoolean(method.Invoke(null, new object[] { true, false, false }));
        if (!heldAndArmed || !recordingAndArmed || idleAndArmed || heldWithoutLock)
            throw new InvalidOperationException("Recording focus-lock lease was not preserved only for an armed active voice session");
    }

    private static void TestVoiceFocusPreflightRequiresCurrentLease()
    {
        MethodInfo method = typeof(VibeMicForm).GetMethod("ShouldCommitVoiceFocusPreflight",
            BindingFlags.Static | BindingFlags.NonPublic);
        if (method == null) throw new InvalidOperationException(
            "Voice focus preflight lease policy is unavailable");
        bool currentAndHeld = Convert.ToBoolean(method.Invoke(null,
            new object[] { true, true, true }));
        bool staleLockAndHeld = Convert.ToBoolean(method.Invoke(null,
            new object[] { true, true, false }));
        bool rejectedAndHeld = Convert.ToBoolean(method.Invoke(null,
            new object[] { false, true, true }));
        bool currentButReleased = Convert.ToBoolean(method.Invoke(null,
            new object[] { true, false, true }));
        if (!currentAndHeld || staleLockAndHeld || rejectedAndHeld || currentButReleased)
            throw new InvalidOperationException(
                "Voice focus preflight accepted a stale, rejected, or released lease");
    }

    private static void TestVoiceWakeRecoveryRequiresFocusWhenCold()
    {
        MethodInfo method = typeof(VibeMicForm).GetMethod(
            "ShouldRecoverCaptureForVoiceWake",
            BindingFlags.Static | BindingFlags.NonPublic);
        if (method == null) throw new InvalidOperationException(
            "Voice wake recovery focus policy is unavailable");
        bool verifiedCold = Convert.ToBoolean(method.Invoke(null,
            new object[] { true, false }));
        bool unverifiedCold = Convert.ToBoolean(method.Invoke(null,
            new object[] { false, false }));
        bool unverifiedRunning = Convert.ToBoolean(method.Invoke(null,
            new object[] { false, true }));
        if (!verifiedCold || unverifiedCold || !unverifiedRunning)
            throw new InvalidOperationException(
                "A cold voice wake started Capture without a verified input target");
    }


    private static void TestTransientFocusedVoiceTargetPolicy()
    {
        MethodInfo method = typeof(VibeMicForm).GetMethod("ShouldUseTransientVoiceTarget",
            BindingFlags.Static | BindingFlags.NonPublic);
        if (method == null) throw new InvalidOperationException("Transient focused voice target policy is unavailable");

        var chatGpt = new FocusTargetDescriptor
        {
            ProcessName = "chatgpt",
            ControlType = "Edit",
            ClassName = "ProseMirror ProseMirror-focused"
        };
        var generic = new FocusTargetDescriptor
        {
            ProcessName = "notepad",
            ControlType = "Edit",
            ClassName = "Edit"
        };
        bool chatGptAllowed = Convert.ToBoolean(method.Invoke(null,
            new object[] { "ChatGPT", "", chatGpt }));
        bool configuredProcessAllowed = Convert.ToBoolean(method.Invoke(null,
            new object[] { "notepad", "notepad", generic }));
        bool manuallyFocusedEditableAllowed = Convert.ToBoolean(method.Invoke(null,
            new object[] { "notepad", "", generic }));
        bool processMismatchDenied = Convert.ToBoolean(method.Invoke(null,
            new object[] { "wordpad", "", generic }));
        bool nonEditableChatGptDenied = Convert.ToBoolean(method.Invoke(null,
            new object[] { "chatgpt", "", new FocusTargetDescriptor
            {
                ProcessName = "chatgpt",
                ControlType = "Button",
                ClassName = "ProseMirror"
            } }));
        if (!chatGptAllowed || !configuredProcessAllowed || !manuallyFocusedEditableAllowed ||
            processMismatchDenied || nonEditableChatGptDenied)
            throw new InvalidOperationException("Transient focused voice target policy was not fail-closed");
    }

    private static void TestVoiceWakeNeverExpeditesProviderLaunchDuringRecording()
    {
        MethodInfo method = typeof(VibeMicForm).GetMethod("ShouldExpediteProviderLaunchForVoiceWake",
            BindingFlags.Static | BindingFlags.NonPublic);
        if (method == null) throw new InvalidOperationException("Voice wake provider launch policy is unavailable");
        MethodInfo backgroundMethod = typeof(VibeMicForm).GetMethod("ShouldLaunchProviderNow",
            BindingFlags.Static | BindingFlags.NonPublic);
        if (backgroundMethod == null) throw new InvalidOperationException("Provider warmup launch policy is unavailable");

        bool providerReady = Convert.ToBoolean(method.Invoke(null, new object[] { true, false, true }));
        bool focusLocked = Convert.ToBoolean(method.Invoke(null, new object[] { false, true, true }));
        bool voiceHeld = Convert.ToBoolean(method.Invoke(null, new object[] { false, false, true }));
        bool recoveryWithoutVoice = Convert.ToBoolean(method.Invoke(null, new object[] { false, false, false }));
        bool warmupDuringFocus = Convert.ToBoolean(backgroundMethod.Invoke(null, new object[] { false, true, false }));
        bool warmupDuringVoice = Convert.ToBoolean(backgroundMethod.Invoke(null, new object[] { false, false, true }));
        bool idleWarmup = Convert.ToBoolean(backgroundMethod.Invoke(null, new object[] { false, false, false }));
        if (providerReady || focusLocked || voiceHeld || !recoveryWithoutVoice ||
            warmupDuringFocus || warmupDuringVoice || !idleWarmup)
            throw new InvalidOperationException("Voice wake provider launch policy can steal the active input target");
    }

    private static void TestVoiceFocusRestoreOnlyRepairsProviderTakeover()
    {
        MethodInfo method = typeof(VibeMicForm).GetMethod("ShouldRestoreVerifiedVoiceFocus",
            BindingFlags.Static | BindingFlags.NonPublic);
        if (method == null) throw new InvalidOperationException(
            "Verified voice-focus restoration policy is unavailable");

        bool targetLostToProvider = Convert.ToBoolean(method.Invoke(null,
            new object[] { true, true, false, true, false }));
        bool targetStillFocused = Convert.ToBoolean(method.Invoke(null,
            new object[] { true, true, true, false, true }));
        bool unrelatedForeground = Convert.ToBoolean(method.Invoke(null,
            new object[] { true, true, false, false, false }));
        bool noLease = Convert.ToBoolean(method.Invoke(null,
            new object[] { false, true, false, true, false }));
        bool idle = Convert.ToBoolean(method.Invoke(null,
            new object[] { true, false, false, true, false }));
        if (!targetLostToProvider || targetStillFocused || unrelatedForeground || noLease || idle)
            throw new InvalidOperationException(
                "Voice-focus restoration was not limited to an armed recording target taken by the provider");
    }

    private static void TestVoiceFocusRestoresWhenProviderIsReady()
    {
        MethodInfo method = typeof(VibeMicForm).GetMethod(
            "ShouldRestoreVoiceFocusWhenProviderReady", BindingFlags.Static | BindingFlags.NonPublic);
        if (method == null) throw new InvalidOperationException(
            "Provider-ready voice-focus restoration policy is unavailable");
        bool held = Convert.ToBoolean(method.Invoke(null,
            new object[] { true, true, "connecting" }));
        bool recordingState = Convert.ToBoolean(method.Invoke(null,
            new object[] { true, false, "recording" }));
        bool processingState = Convert.ToBoolean(method.Invoke(null,
            new object[] { true, false, "processing" }));
        bool idle = Convert.ToBoolean(method.Invoke(null,
            new object[] { true, false, "ready" }));
        bool noLease = Convert.ToBoolean(method.Invoke(null,
            new object[] { false, true, "recording" }));
        if (!held || !recordingState || !processingState || idle || noLease)
            throw new InvalidOperationException(
                "Provider-ready focus restoration did not remain bounded to an active verified voice session");
    }

    private static void TestFocusRecoveryRetriesOnlyTransientTargetLoss()
    {
        MethodInfo method = typeof(WindowsUiaFocusAutomationBackend).GetMethod("ShouldRetryTargetScan",
            BindingFlags.Static | BindingFlags.NonPublic);
        if (method == null) throw new InvalidOperationException(
            "Focus recovery retry policy is unavailable");

        bool staleTarget = Convert.ToBoolean(method.Invoke(null,
            new object[] { "FOCUS-TARGET-STALE", 120, 900 }));
        bool notEditableDuringRefresh = Convert.ToBoolean(method.Invoke(null,
            new object[] { "FOCUS-TARGET-NOT-EDITABLE", 120, 900 }));
        bool ambiguousTarget = Convert.ToBoolean(method.Invoke(null,
            new object[] { "FOCUS-TARGET-AMBIGUOUS", 120, 900 }));
        bool canceled = Convert.ToBoolean(method.Invoke(null,
            new object[] { "FOCUS-CANCELED", 120, 900 }));
        bool timedOut = Convert.ToBoolean(method.Invoke(null,
            new object[] { "FOCUS-TARGET-STALE", 900, 900 }));
        if (!staleTarget || !notEditableDuringRefresh || ambiguousTarget || canceled || timedOut)
            throw new InvalidOperationException(
                "Focus recovery retried a non-transient target failure or continued after timeout");
    }

    private static void TestFocusRecoveryLeaseSurvivesTargetRefreshOnly()
    {
        MethodInfo method = typeof(VibeMicForm).GetMethod(
            "ShouldRetainVoiceFocusRecoveryLease", BindingFlags.Static | BindingFlags.NonPublic);
        if (method == null) throw new InvalidOperationException(
            "Voice-focus refresh recovery lease policy is unavailable");
        bool targetRefresh = Convert.ToBoolean(method.Invoke(null,
            new object[] { true, true, false }));
        bool noPreviousLease = Convert.ToBoolean(method.Invoke(null,
            new object[] { false, true, false }));
        bool unrelatedForeground = Convert.ToBoolean(method.Invoke(null,
            new object[] { true, false, false }));
        bool alreadyVerified = Convert.ToBoolean(method.Invoke(null,
            new object[] { true, true, true }));
        if (!targetRefresh || noPreviousLease || unrelatedForeground || alreadyVerified)
            throw new InvalidOperationException(
                "Voice-focus recovery lease was retained outside a target-app refresh");
    }
}
