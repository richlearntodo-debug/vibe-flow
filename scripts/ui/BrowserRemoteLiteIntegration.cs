using System;
using System.Collections.Generic;
using System.IO;

internal sealed partial class VibeMicForm
{
    private BrowserRemoteUndoStore browserRemoteUndoStore;
    private BrowserRemoteTestService browserRemoteTestService;
    private BrowserRemoteLiteForm browserRemoteLiteForm;

    private void InitializeBrowserRemoteLite()
    {
        browserRemoteUndoStore = new BrowserRemoteUndoStore(userStateRoot);
        browserRemoteTestService = new BrowserRemoteTestService(
            new WindowsBrowserRemoteTestBackend(), BrowserRemoteRecordingHasPriority,
            DispatchBrowserRemoteTest, HostLog);
    }

    private void ShowBrowserRemoteLite()
    {
        if (browserRemoteLiteForm != null && !browserRemoteLiteForm.IsDisposed)
        {
            browserRemoteLiteForm.ApplyTheme(darkTheme);
            browserRemoteLiteForm.Show();
            browserRemoteLiteForm.Activate();
            return;
        }
        browserRemoteLiteForm = new BrowserRemoteLiteForm(CurrentBrowserRemoteMappings,
            ApplyBrowserRemotePlan, UndoBrowserRemotePlan, CanUndoBrowserRemotePlan,
            BeginBrowserRemoteActionTest, ShowActionToast, darkTheme);
        browserRemoteLiteForm.FormClosed += delegate
        {
            CancelPendingBrowserRemoteTest();
            browserRemoteLiteForm = null;
        };
        browserRemoteLiteForm.Show(this);
    }

    private ActionResult BeginBrowserRemoteActionTest(string browserId, string label, string action,
        Action<ActionResult> completion)
    {
        if (uiSmokeMode)
            return ActionResult.Create("测试浏览器动作", label, ActionState.Error,
                "UI 验证模式未启动按键服务，本次未派发按键", "当前只检查界面状态",
                "在正常运行的 Vibe Flow 中重新测试", "BROWSER-TEST-SMOKE-NO-BRIDGE");
        if (browserRemoteTestService == null)
            return ActionResult.Create("测试浏览器动作", label, ActionState.Error,
                "按键测试未派发", "浏览器测试服务尚未初始化",
                "关闭窗口后重试", "BROWSER-TEST-UNAVAILABLE");
        return browserRemoteTestService.Begin(browserId, label, action, completion);
    }

    private bool DispatchBrowserRemoteTest(string label, string action,
        BrowserRemoteDispatchTarget target,
        Action<ActionResult> completion)
    {
        if (BrowserRemoteRecordingHasPriority() ||
            !string.IsNullOrWhiteSpace(pendingMappingTestToken)) return false;
        string token = Guid.NewGuid().ToString("N");
        try
        {
            var request = new Dictionary<string, object>();
            request["name"] = "browser_remote_lite_test";
            request["label"] = label;
            request["action"] = action;
            request["token"] = token;
            request["created_at"] = DateTime.UtcNow.ToString("o");
            request["expected_process_id"] = target == null ? 0 : target.ProcessId;
            request["expected_window_handle"] = target == null ? 0L : target.WindowHandle.ToInt64();
            request["expected_process_name"] = target == null ? "" : target.ProcessName;
            return BrowserRemoteDispatchCoordinator.TryDispatch(
                BrowserRemoteRecordingHasPriority,
                BrowserRemoteExistingBridgeReady,
                delegate
                {
                    pendingBrowserRemoteTestToken = token;
                    PrepareMappingActionTest(token, label, delegate(MappingActionTestResult bridgeResult)
                    {
                        string bridgeErrorCode = bridgeResult == null ? "" : bridgeResult.error_code ?? "";
                        ActionResult result;
                        if (bridgeResult != null && bridgeResult.success)
                            result = ActionResult.Create("测试浏览器动作", label, ActionState.Warning,
                                label + "动作已派发，请在浏览器中目视确认页面变化", "",
                                "如无变化，确认浏览器窗口仍在前台后重试",
                                "BROWSER-TEST-VISUAL-CONFIRMATION");
                        else if (string.Equals(bridgeErrorCode, "BROWSER-TEST-CANCELED-VOICE",
                            StringComparison.Ordinal))
                            result = ActionResult.Create("测试浏览器动作", label, ActionState.Canceled,
                                "录音已开始，本次未派发按键", "录音操作优先",
                                "录音结束后重新测试", bridgeErrorCode);
                        else
                            result = ActionResult.Create("测试浏览器动作", label, ActionState.Error,
                                "按键动作未派发", bridgeResult == null ||
                                    string.IsNullOrWhiteSpace(bridgeResult.message)
                                        ? "按键服务没有返回可用回执" : bridgeResult.message,
                                "确认正确的浏览器窗口仍在前台后重试",
                                string.IsNullOrWhiteSpace(bridgeErrorCode)
                                    ? "BROWSER-TEST-BRIDGE-FAILED" : bridgeErrorCode);
                        if (completion != null) completion(result);
                    });
                    return BrowserRemoteRequestFile.TryWriteAtomic(
                        Path.Combine(root, "custom-button-test.json"),
                        new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(request));
                },
                delegate { ClearPendingMappingActionTest(token); });
        }
        catch (Exception ex)
        {
            ClearPendingMappingActionTest(token);
            HostLog("BROWSER TEST dispatch_failed=true error=" + SafeLogValue(ex.Message));
            return false;
        }
    }

    private bool BrowserRemoteRecordingHasPriority()
    {
        return IsVoiceKeyHeld() || string.Equals(currentVisualState, "recording",
            StringComparison.OrdinalIgnoreCase);
    }

    private bool BrowserRemoteExistingBridgeReady()
    {
        if (BrowserRemoteRecordingHasPriority()) return false;
        BridgeHealthSnapshot health = ReadKeyboardBridgeHealth();
        if (health == null || health.ProcessId <= 0 ||
            !BridgeHealthSnapshotAcknowledgesRevision(health,
                expectedKeyboardConfigRevision, health.ProcessId)) return false;
        return KeyboardBridgeProcessMatchesExpectedRoot(health.ProcessId) &&
            !BrowserRemoteRecordingHasPriority();
    }

    private Dictionary<string, string> CurrentBrowserRemoteMappings()
    {
        ShortcutProfileConfig profile = FindShortcutProfile(config, BrowserProfileTemplate.ProfileId);
        return CloneMappings(profile == null ? null : profile.mappings);
    }

    private ActionResult ApplyBrowserRemotePlan(BrowserRemotePlan plan)
    {
        string revision = "";
        ActionResult result = ApplyBrowserRemotePlanCore(config, plan,
            delegate(BrowserRemoteUndoSnapshot snapshot)
            {
                return browserRemoteUndoStore != null && browserRemoteUndoStore.Save(snapshot);
            }, delegate { return SaveConfig(out revision); },
            delegate
            {
                return BrowserRemoteBridgeAcknowledged(uiSmokeMode, delegate
                {
                    return StartKeyboardBridgeForRevision(revision,
                        BrowserRemoteRecordingHasPriority);
                });
            }, BrowserRemoteRecordingHasPriority);
        HostLog("BROWSER REMOTE apply state=" + result.State.ToString().ToLowerInvariant() +
            " code=" + SafeLogValue(result.ErrorCode));
        return result;
    }

    private ActionResult UndoBrowserRemotePlan()
    {
        BrowserRemoteUndoLoadResult loaded = browserRemoteUndoStore == null
            ? BrowserRemoteUndoLoadResult.Failure("BROWSER-UNDO-MISSING") : browserRemoteUndoStore.Load();
        string revision = "";
        ActionResult result = UndoBrowserRemotePlanCore(config, loaded.Snapshot,
            delegate { return SaveConfig(out revision); },
            delegate
            {
                return BrowserRemoteBridgeAcknowledged(uiSmokeMode, delegate
                {
                    return StartKeyboardBridgeForRevision(revision,
                        BrowserRemoteRecordingHasPriority);
                });
            }, BrowserRemoteRecordingHasPriority);
        if (result.IsSuccess || result.ErrorCode == "BROWSER-UNDO-ACK-PENDING")
            browserRemoteUndoStore.Clear();
        HostLog("BROWSER REMOTE undo state=" + result.State.ToString().ToLowerInvariant() +
            " code=" + SafeLogValue(result.ErrorCode));
        return result;
    }

    private bool CanUndoBrowserRemotePlan()
    {
        if (browserRemoteUndoStore == null) return false;
        BrowserRemoteUndoLoadResult loaded = browserRemoteUndoStore.Load();
        ShortcutProfileConfig profile = loaded.Snapshot == null
            ? null : FindShortcutProfile(config, loaded.Snapshot.ProfileId);
        Dictionary<string, string> restored;
        string errorCode;
        return profile != null && loaded.Snapshot.TryRestore(profile.mappings, out restored, out errorCode);
    }

    private void RefreshBrowserRemoteTheme()
    {
        if (browserRemoteLiteForm != null && !browserRemoteLiteForm.IsDisposed)
            browserRemoteLiteForm.ApplyTheme(darkTheme);
    }

    private void ShutdownBrowserRemoteLite()
    {
        CancelPendingBrowserRemoteTest();
        BrowserRemoteLiteForm open = browserRemoteLiteForm;
        browserRemoteLiteForm = null;
        if (open != null && !open.IsDisposed) open.Close();
    }

    private void CancelPendingBrowserRemoteTest()
    {
        string token = pendingBrowserRemoteTestToken;
        if (!string.IsNullOrWhiteSpace(token)) ClearPendingMappingActionTest(token);
    }

    private static ActionResult ApplyBrowserRemotePlanCore(VibeMicConfig value,
        BrowserRemotePlan plan, Func<BrowserRemoteUndoSnapshot, bool> saveUndo,
        Func<bool> saveConfiguration, Func<bool> acknowledgeBridge,
        Func<bool> cancellationRequested = null)
    {
        if (BrowserRemoteCancellationRequested(cancellationRequested))
            return BrowserRemoteApplyCanceled(false);
        ShortcutProfileConfig target = value == null || plan == null
            ? null : FindShortcutProfile(value, plan.ProfileId);
        if (target == null || !plan.IsValid)
            return ActionResult.Create("应用浏览器遥控", "浏览器 AI", ActionState.Error,
                "未修改按键配置", "浏览器 Profile 或推荐选项不可用",
                "打开按键页并检查浏览器 Profile", plan == null || plan.IsValid
                    ? "BROWSER-PROFILE-MISSING" : plan.ErrorCode);
        if (!plan.MatchesCurrent(target.mappings))
            return ActionResult.Create("应用浏览器遥控", target.name, ActionState.Warning,
                "配置已变化，本次未覆盖", "预览后有其他按键设置发生变化",
                "重新打开差异预览", "BROWSER-PROFILE-CONFLICT");
        if (plan.Changes.Count == 0)
            return ActionResult.Create("应用浏览器遥控", target.name, ActionState.Warning,
                "当前已是所选推荐配置", "没有需要修改的按键",
                "可以逐项测试或关闭", "BROWSER-TEMPLATE-NO-CHANGES");

        BrowserRemoteUndoSnapshot undo = BrowserRemoteUndoSnapshot.FromPlan(plan);
        if (BrowserRemoteCancellationRequested(cancellationRequested))
            return BrowserRemoteApplyCanceled(false);
        bool undoSaved;
        try { undoSaved = saveUndo != null && saveUndo(undo); }
        catch { undoSaved = false; }
        if (!undoSaved)
            return ActionResult.Create("应用浏览器遥控", target.name, ActionState.Error,
                "推荐配置未应用", "无法保存精确撤销快照",
                "检查用户数据目录后重试", "BROWSER-UNDO-SAVE-FAILED");

        Dictionary<string, string> previousTarget = CloneMappings(target.mappings);
        Dictionary<string, string> previousProjection = CloneMappings(value.mappings);
        bool targetIsActive = string.Equals(value.activeShortcutProfileId, target.id,
            StringComparison.OrdinalIgnoreCase);
        if (BrowserRemoteCancellationRequested(cancellationRequested))
            return BrowserRemoteApplyCanceled(false);
        target.mappings = plan.ApplyTo(target.mappings);
        if (targetIsActive) value.mappings = CloneMappings(target.mappings);

        ConfigurationMutationOutcome saveOutcome = PersistConfigurationMutationCore(
            saveConfiguration, delegate
            {
                target.mappings = previousTarget;
                value.mappings = previousProjection;
            });
        if (saveOutcome != ConfigurationMutationOutcome.Committed)
        {
            return ActionResult.Create("应用浏览器遥控", target.name, ActionState.Error,
                saveOutcome == ConfigurationMutationOutcome.RolledBack
                    ? "推荐配置未保存，仍使用原方案"
                    : "推荐配置未保存，原方案恢复仍需确认",
                "本地配置写入失败",
                "检查用户数据目录后重试", "BROWSER-PROFILE-SAVE-FAILED");
        }

        if (BrowserRemoteCancellationRequested(cancellationRequested))
            return BrowserRemoteApplyCanceled(true);
        bool acknowledged;
        try { acknowledged = acknowledgeBridge != null && acknowledgeBridge(); }
        catch { acknowledged = false; }
        if (!acknowledged)
            return ActionResult.Create("应用浏览器遥控", target.name, ActionState.Warning,
                "推荐配置已保存，按键服务尚未确认生效", "尚未收到 Bridge revision ACK",
                "打开自检并重新检测", "BROWSER-PROFILE-ACK-PENDING");
        return ActionResult.Create("应用浏览器遥控", target.name, ActionState.Success,
            "按键服务已确认 Browser Remote Lite 配置", "", "", "");
    }

    private static ActionResult UndoBrowserRemotePlanCore(VibeMicConfig value,
        BrowserRemoteUndoSnapshot undo, Func<bool> saveConfiguration,
        Func<bool> acknowledgeBridge, Func<bool> cancellationRequested = null)
    {
        if (BrowserRemoteCancellationRequested(cancellationRequested))
            return BrowserRemoteUndoCanceled(false);
        if (undo == null)
            return ActionResult.Create("撤销浏览器遥控", "浏览器 AI", ActionState.Warning,
                "没有可安全撤销的推荐配置", "撤销记录缺失或不可用",
                "重新打开 Browser Remote Lite", "BROWSER-UNDO-MISSING");
        string validationCode;
        if (!undo.TryValidate(out validationCode))
            return ActionResult.Create("撤销浏览器遥控", "浏览器 AI", ActionState.Warning,
                "没有可安全撤销的推荐配置", "撤销记录缺失或不可用",
                "重新打开 Browser Remote Lite", validationCode);
        ShortcutProfileConfig target = value == null ? null : FindShortcutProfile(value, undo.ProfileId);
        if (target == null)
            return ActionResult.Create("撤销浏览器遥控", "浏览器 AI", ActionState.Warning,
                "未修改按键配置", "原浏览器 Profile 已不存在",
                "在按键页检查 Profile", "BROWSER-PROFILE-MISSING");

        Dictionary<string, string> restored;
        string restoreCode;
        if (!undo.TryRestore(target.mappings, out restored, out restoreCode))
            return ActionResult.Create("撤销浏览器遥控", target.name, ActionState.Warning,
                "检测到后续修改，未执行撤销", "当前按键已不等于当时应用的推荐值",
                "保留当前设置，或重新预览推荐配置", restoreCode);

        Dictionary<string, string> previousTarget = CloneMappings(target.mappings);
        Dictionary<string, string> previousProjection = CloneMappings(value.mappings);
        bool targetIsActive = string.Equals(value.activeShortcutProfileId, target.id,
            StringComparison.OrdinalIgnoreCase);
        if (BrowserRemoteCancellationRequested(cancellationRequested))
            return BrowserRemoteUndoCanceled(false);
        target.mappings = restored;
        if (targetIsActive) value.mappings = CloneMappings(restored);

        ConfigurationMutationOutcome saveOutcome = PersistConfigurationMutationCore(
            saveConfiguration, delegate
            {
                target.mappings = previousTarget;
                value.mappings = previousProjection;
            });
        if (saveOutcome != ConfigurationMutationOutcome.Committed)
        {
            return ActionResult.Create("撤销浏览器遥控", target.name, ActionState.Error,
                saveOutcome == ConfigurationMutationOutcome.RolledBack
                    ? "撤销未保存，仍使用撤销前配置"
                    : "撤销未保存，撤销前配置恢复仍需确认",
                "本地配置写入失败",
                "检查用户数据目录后重试", "BROWSER-UNDO-SAVE-FAILED");
        }

        if (BrowserRemoteCancellationRequested(cancellationRequested))
            return BrowserRemoteUndoCanceled(true);
        bool acknowledged;
        try { acknowledged = acknowledgeBridge != null && acknowledgeBridge(); }
        catch { acknowledged = false; }
        if (!acknowledged)
            return ActionResult.Create("撤销浏览器遥控", target.name, ActionState.Warning,
                "原配置已恢复，按键服务尚未确认生效", "尚未收到 Bridge revision ACK",
                "打开自检并重新检测", "BROWSER-UNDO-ACK-PENDING");
        return ActionResult.Create("撤销浏览器遥控", target.name, ActionState.Success,
            "按键服务已确认恢复应用前配置", "", "", "");
    }

    private static bool BrowserRemoteCancellationRequested(Func<bool> cancellationRequested)
    {
        try { return cancellationRequested != null && cancellationRequested(); }
        catch { return true; }
    }

    private static bool BrowserRemoteBridgeAcknowledged(bool smokeMode,
        Func<bool> acknowledgeBridge)
    {
        if (smokeMode || acknowledgeBridge == null) return false;
        try { return acknowledgeBridge(); }
        catch { return false; }
    }

    private static ActionResult BrowserRemoteApplyCanceled(bool configurationSaved)
    {
        return ActionResult.Create("应用浏览器遥控", "浏览器 AI", ActionState.Canceled,
            configurationSaved ? "配置已保存；录音开始后未重启按键服务" :
                "录音正在进行，本次未修改按键配置",
            "录音操作优先", "录音结束后重新打开 Browser Remote Lite 检查配置",
            "BROWSER-PROFILE-CANCELED-VOICE");
    }

    private static ActionResult BrowserRemoteUndoCanceled(bool configurationSaved)
    {
        return ActionResult.Create("撤销浏览器遥控", "浏览器 AI", ActionState.Canceled,
            configurationSaved ? "原配置已保存；录音开始后未重启按键服务" :
                "录音正在进行，本次未撤销按键配置",
            "录音操作优先", "录音结束后重新打开 Browser Remote Lite 检查配置",
            "BROWSER-UNDO-CANCELED-VOICE");
    }
}
