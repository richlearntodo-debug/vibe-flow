using System;
using System.Collections.Generic;
using System.IO;

internal static class BrowserRemoteLiteTests
{
    private static int Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "vibe-flow-browser-lite-test-" +
            Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            TestRecommendedPlanPreservesStableBoundaries();
            TestUndoRejectsLaterUserChanges();
            TestUndoStoreBackupAndFutureSchema(root);
            TestBrowserActionRequiresVerifiedForeground();
            TestRecordingPreemptsBrowserActionDispatch();
            TestDispatchUsesExistingBridgeBeforeExposingRequest();
            TestPendingRequestCleanupRequiresMatchingToken(root);
            TestBrowserRequestClaimIsSingleUseAndCancelable(root);
            Console.WriteLine("Browser Remote Lite tests passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Browser Remote Lite tests failed: " + ex.Message);
            return 1;
        }
        finally
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
        }
    }

    private static void TestRecommendedPlanPreservesStableBoundaries()
    {
        Dictionary<string, string> current = BrowserFixture();
        BrowserRemotePlan plan = BrowserProfileTemplate.CreatePlan(
            "browser-ai", current, "next-tab", "address-bar");
        Require(plan.IsValid && plan.Changes.Count > 0, "A valid browser plan was not created");
        Dictionary<string, string> applied = plan.ApplyTo(current);
        Require(applied["确认键"] == "enter" && applied["上键"] == "pageup" &&
            applied["下键"] == "pagedown" && applied["左键"] == "browserback" &&
            applied["右键"] == "shortcut:ctrl+tab" &&
            applied["功能键:short"] == "shortcut:ctrl+r" &&
            applied["功能键:long"] == "shortcut:ctrl+l",
            "The recommended browser actions are incomplete or incorrect");
        Require(applied["Home"] == current["Home"] &&
            applied["Home:short"] == current["Home:short"] &&
            applied["Home:long"] == current["Home:long"] &&
            applied["TV"] == current["TV"],
            "Browser Remote Lite overwrote Home or TV without user approval");
        foreach (BrowserRemoteMappingChange change in plan.Changes)
            Require(change.Key != "录音键" && change.Key.IndexOf("voice",
                StringComparison.OrdinalIgnoreCase) < 0,
                "Browser Remote Lite included the frozen recording key");

        BrowserRemotePlan noOp = BrowserProfileTemplate.CreatePlan(
            "browser-ai", applied, "next-tab", "address-bar");
        Require(noOp.IsValid && noOp.Changes.Count == 0,
            "An already-applied recommendation did not produce a no-op plan");
    }

    private static void TestUndoRejectsLaterUserChanges()
    {
        Dictionary<string, string> current = BrowserFixture();
        BrowserRemotePlan plan = BrowserProfileTemplate.CreatePlan(
            "browser-ai", current, "tab", "find");
        Dictionary<string, string> applied = plan.ApplyTo(current);
        BrowserRemoteUndoSnapshot undo = BrowserRemoteUndoSnapshot.FromPlan(plan);
        Dictionary<string, string> restored;
        string errorCode;
        Require(undo.TryRestore(applied, out restored, out errorCode) &&
            DictionariesEqual(current, restored),
            "The exact Browser Remote Lite snapshot could not be undone");

        applied["功能键:short"] = "ctrl+s";
        Require(!undo.TryRestore(applied, out restored, out errorCode) &&
            errorCode == "BROWSER-UNDO-CONFLICT",
            "Undo silently overwrote a mapping changed after the recommendation was applied");
    }

    private static void TestUndoStoreBackupAndFutureSchema(string root)
    {
        var store = new BrowserRemoteUndoStore(root);
        BrowserRemotePlan firstPlan = BrowserProfileTemplate.CreatePlan(
            "browser-ai", BrowserFixture(), "tab", "find");
        BrowserRemoteUndoSnapshot first = BrowserRemoteUndoSnapshot.FromPlan(firstPlan);
        Require(store.Save(first), "Browser undo snapshot was not saved atomically");

        Dictionary<string, string> secondCurrent = firstPlan.ApplyTo(BrowserFixture());
        secondCurrent["右键"] = "shortcut:ctrl+tab";
        BrowserRemotePlan secondPlan = BrowserProfileTemplate.CreatePlan(
            "browser-ai", secondCurrent, "forward", "address-bar");
        BrowserRemoteUndoSnapshot second = BrowserRemoteUndoSnapshot.FromPlan(secondPlan);
        Require(store.Save(second), "A replacement browser undo snapshot was not saved");
        File.WriteAllText(store.Path, "{broken", System.Text.Encoding.UTF8);
        BrowserRemoteUndoLoadResult recovered = store.Load();
        Require(recovered.Snapshot != null && recovered.UsedBackup &&
            recovered.Snapshot.ProfileId == first.ProfileId,
            "A malformed browser undo document did not recover the valid backup");

        string futureJson =
            "{\"schemaVersion\":99,\"profileId\":\"browser-ai\",\"previousMappings\":{},\"appliedMappings\":{}}";
        File.WriteAllText(store.Path, futureJson, System.Text.Encoding.UTF8);
        if (File.Exists(store.BackupPath)) File.Delete(store.BackupPath);
        BrowserRemoteUndoLoadResult future = store.Load();
        Require(future.Snapshot == null && future.ErrorCode == "BROWSER-UNDO-SCHEMA-NEWER",
            "A future Browser Remote Lite undo schema was treated as executable");
        Require(!store.Save(second) && File.ReadAllText(store.Path, System.Text.Encoding.UTF8) == futureJson,
            "A future Browser Remote Lite undo schema was overwritten by an older writer");
    }

    private static void TestBrowserActionRequiresVerifiedForeground()
    {
        var backend = new BrowserRemoteTestBackendFixture();
        int dispatches = 0;
        BrowserRemoteDispatchTarget dispatchedTarget = null;
        var service = new BrowserRemoteTestService(backend, delegate { return false; },
            delegate(string label, string action, BrowserRemoteDispatchTarget target,
                Action<ActionResult> completion)
            {
                dispatches++;
                dispatchedTarget = target;
                return true;
            }, null);
        backend.ErrorCode = "BROWSER-WINDOW-NOT-FOUND";
        ActionResult missing = service.Begin("chrome", "刷新页面", "shortcut:ctrl+r", null);
        Require(missing.State == ActionState.Error && dispatches == 0,
            "Browser action was dispatched without a visible verified browser window");

        backend.ErrorCode = "";
        backend.VerifiedProcessName = "notepad";
        ActionResult mismatch = service.Begin("edge", "刷新页面", "shortcut:ctrl+r", null);
        Require(mismatch.State == ActionState.Error &&
            mismatch.ErrorCode == "BROWSER-FOREGROUND-MISMATCH" && dispatches == 0,
            "Browser action was dispatched to a mismatched foreground process");

        backend.VerifiedProcessName = "msedge";
        ActionResult started = service.Begin("edge", "刷新页面", "shortcut:ctrl+r", null);
        Require(started.State == ActionState.Running && dispatches == 1 &&
            dispatchedTarget != null && dispatchedTarget.ProcessId == 42 &&
            dispatchedTarget.WindowHandle == new IntPtr(84) &&
            dispatchedTarget.ProcessName == "msedge" &&
            started.Message.IndexOf("等待按键服务回执", StringComparison.Ordinal) >= 0,
            "Verified Edge evidence was not preserved through the dispatch boundary");
        ActionResult unsupported = service.Begin("firefox", "刷新页面", "shortcut:ctrl+r", null);
        Require(unsupported.State == ActionState.Error && dispatches == 1,
            "An unverified browser was accepted for individual action testing");
    }

    private static void TestRecordingPreemptsBrowserActionDispatch()
    {
        bool recording = false;
        int dispatches = 0;
        var backend = new BrowserRemoteTestBackendFixture();
        backend.VerifiedProcessName = "chrome";
        backend.AfterActivate = delegate { recording = true; };
        var service = new BrowserRemoteTestService(backend, delegate { return recording; },
            delegate(string label, string action, BrowserRemoteDispatchTarget target,
                Action<ActionResult> completion)
            {
                dispatches++;
                return true;
            }, null);
        ActionResult result = service.Begin("chrome", "向下翻页", "pagedown", null);
        Require(result.State == ActionState.Canceled &&
            result.ErrorCode == "BROWSER-TEST-CANCELED-VOICE" && dispatches == 0,
            "Recording did not preempt Browser Remote Lite before input dispatch");

        recording = false;
        backend.AfterActivate = null;
        ActionResult unsafeAction = service.Begin("chrome", "未知", "shortcut:ctrl+s", null);
        Require(unsafeAction.State == ActionState.Error && dispatches == 0,
            "Browser Remote Lite accepted an action outside its fixed allowlist");
    }

    private static void TestDispatchUsesExistingBridgeBeforeExposingRequest()
    {
        var order = new List<string>();
        bool recording = false;
        int clears = 0;
        bool dispatched = BrowserRemoteDispatchCoordinator.TryDispatch(
            delegate { return recording; },
            delegate
            {
                order.Add("bridge-ready");
                return false;
            },
            delegate
            {
                order.Add("request-exposed");
                return true;
            },
            delegate { clears++; });
        Require(!dispatched && order.Count == 1 && order[0] == "bridge-ready" && clears == 0,
            "Browser request was exposed before an existing Bridge was confirmed ready");

        order.Clear();
        dispatched = BrowserRemoteDispatchCoordinator.TryDispatch(
            delegate { return recording; },
            delegate
            {
                order.Add("bridge-ready");
                recording = true;
                return true;
            },
            delegate
            {
                order.Add("request-exposed");
                return true;
            },
            delegate { clears++; });
        Require(!dispatched && order.Count == 1 && clears == 0,
            "Recording did not preempt Browser dispatch before the request became visible");

        recording = false;
        order.Clear();
        dispatched = BrowserRemoteDispatchCoordinator.TryDispatch(
            delegate { return recording; },
            delegate
            {
                order.Add("bridge-ready");
                return true;
            },
            delegate
            {
                order.Add("request-exposed");
                recording = true;
                return true;
            },
            delegate
            {
                order.Add("request-cleared");
                clears++;
            });
        Require(!dispatched && order.Count == 3 && order[0] == "bridge-ready" &&
            order[1] == "request-exposed" && order[2] == "request-cleared" && clears == 1,
            "A Browser request exposed as recording started was not canceled exactly once");
    }

    private static void TestPendingRequestCleanupRequiresMatchingToken(string root)
    {
        string path = Path.Combine(root, "custom-button-test.json");
        File.WriteAllText(path, "{\"token\":\"new-token\",\"action\":\"pagedown\"}",
            System.Text.Encoding.UTF8);
        Require(!BrowserRemoteRequestFile.TryDeleteIfTokenMatches(path, "old-token") &&
            File.Exists(path),
            "Cleanup deleted a newer Browser Remote Lite request with a different token");
        Require(BrowserRemoteRequestFile.TryDeleteIfTokenMatches(path, "new-token") &&
            !File.Exists(path),
            "Cleanup did not delete the matching Browser Remote Lite request");

        File.WriteAllText(path, "{broken", System.Text.Encoding.UTF8);
        Require(!BrowserRemoteRequestFile.TryDeleteIfTokenMatches(path, "new-token") &&
            File.Exists(path),
            "Cleanup deleted a malformed request whose token could not be verified");
        File.Delete(path);
    }

    private static void TestBrowserRequestClaimIsSingleUseAndCancelable(string root)
    {
        string path = Path.Combine(root, "claimed-request.json");
        string token = "claim-token-1";
        Require(BrowserRemoteRequestFile.TryWriteAtomic(path,
            "{\"token\":\"" + token + "\",\"action\":\"pagedown\"}"),
            "Browser request was not written atomically");
        string claimedPath;
        Require(BrowserRemoteRequestFile.TryClaim(path, token, out claimedPath) &&
            !File.Exists(path) && File.Exists(claimedPath),
            "Browser request was not claimed atomically");
        string duplicateClaim;
        Require(!BrowserRemoteRequestFile.TryClaim(path, token, out duplicateClaim),
            "A claimed Browser request was available to a second executor");
        int executions = 0;
        bool canceled;
        Require(BrowserRemoteRequestFile.TryExecuteClaimed(claimedPath, path, token,
            delegate { executions++; return true; }, out canceled) && !canceled && executions == 1,
            "A claimed Browser request did not execute exactly once");
        Require(!BrowserRemoteRequestFile.TryExecuteClaimed(claimedPath, path, token,
            delegate { executions++; return true; }, out canceled) && executions == 1,
            "A completed Browser request was executable a second time");

        string cancelToken = "claim-token-2";
        Require(BrowserRemoteRequestFile.TryWriteAtomic(path,
            "{\"token\":\"" + cancelToken + "\",\"action\":\"pagedown\"}"),
            "Cancelable Browser request was not written");
        string cancelClaim;
        Require(BrowserRemoteRequestFile.TryClaim(path, cancelToken, out cancelClaim) &&
            BrowserRemoteRequestFile.TryCancel(path, cancelToken),
            "Browser request cancellation did not mark the claimed token");
        int canceledExecutions = 0;
        Require(!BrowserRemoteRequestFile.TryExecuteClaimed(cancelClaim, path, cancelToken,
            delegate { canceledExecutions++; return true; }, out canceled) && canceled &&
            canceledExecutions == 0 && !File.Exists(cancelClaim),
            "Canceled Browser request was still executed");

        string newToken = "claim-token-3";
        Require(BrowserRemoteRequestFile.TryWriteAtomic(path,
            "{\"token\":\"" + newToken + "\",\"action\":\"pagedown\"}"),
            "Replacement Browser request was not written");
        Require(!BrowserRemoteRequestFile.TryCancel(path, "claim-token-old") && File.Exists(path),
            "Canceling an old Browser token removed a newer request");
        File.Delete(path);
    }

    private static Dictionary<string, string> BrowserFixture()
    {
        return new Dictionary<string, string>
        {
            { "确认键", "ctrl+s" },
            { "Home", "win+d" },
            { "Home:short", "win+d" },
            { "Home:long", "launch-client:chatgpt" },
            { "TV", "task-switcher" },
            { "功能键", "ctrl+c" },
            { "功能键:short", "ctrl+c" },
            { "功能键:long", "ctrl+v" },
            { "上键", "up" },
            { "下键", "down" },
            { "左键", "left" },
            { "右键", "right" }
        };
    }

    private static bool DictionariesEqual(Dictionary<string, string> left,
        Dictionary<string, string> right)
    {
        if (left == null || right == null || left.Count != right.Count) return false;
        foreach (KeyValuePair<string, string> pair in left)
        {
            string value;
            if (!right.TryGetValue(pair.Key, out value) || value != pair.Value) return false;
        }
        return true;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

internal sealed class BrowserRemoteTestBackendFixture : IBrowserRemoteTestBackend
{
    internal string ErrorCode = "";
    internal string VerifiedProcessName = "chrome";
    internal Action AfterActivate;

    public BrowserRemoteActivationResult ActivateAndVerify(string processName, int timeoutMs,
        Func<bool> cancellationRequested)
    {
        if (AfterActivate != null) AfterActivate();
        if (!string.IsNullOrWhiteSpace(ErrorCode))
            return BrowserRemoteActivationResult.Failure(ErrorCode);
        return BrowserRemoteActivationResult.Success(42, new IntPtr(84), VerifiedProcessName);
    }
}
