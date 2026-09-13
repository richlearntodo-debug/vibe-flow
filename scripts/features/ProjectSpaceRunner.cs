using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

internal enum ProjectStepKind
{
    OpenOrActivateApp = 0,
    OpenWorkspaceWithVerifiedAdapter = 1,
    OpenUrl = 2,
    SwitchProfile = 3,
    FocusTarget = 4,
    ShowNotification = 5
}

internal static class ProjectSpaceProcess
{
    internal static string QuoteSingleArgument(string value)
    {
        string source = value ?? "";
        var quoted = new System.Text.StringBuilder();
        quoted.Append('"');
        int slashes = 0;
        foreach (char character in source)
        {
            if (character == '\\')
            {
                slashes++;
                continue;
            }
            if (character == '"')
            {
                quoted.Append('\\', slashes * 2 + 1);
                quoted.Append('"');
            }
            else
            {
                quoted.Append('\\', slashes);
                quoted.Append(character);
            }
            slashes = 0;
        }
        quoted.Append('\\', slashes * 2);
        quoted.Append('"');
        return quoted.ToString();
    }

    internal static bool TryCreateStartInfo(ProjectSpace snapshot, ProjectPlanStep step,
        out ProcessStartInfo startInfo, out string errorCode)
    {
        startInfo = null;
        errorCode = "PROJECT-STEP-UNSUPPORTED";
        if (snapshot == null || step == null) return false;

        string target;
        string ignored;
        if (step.Resource == ProjectStepResource.Editor &&
            string.Equals((snapshot.EditorKind ?? "").Trim(), "chatgpt", System.StringComparison.OrdinalIgnoreCase) &&
            (step.Kind == ProjectStepKind.OpenOrActivateApp ||
             step.Kind == ProjectStepKind.OpenWorkspaceWithVerifiedAdapter))
        {
            if (!ProjectSpaceValidation.TryNormalizeEditorExecutable(snapshot.EditorExecutablePath,
                    "chatgpt", out target, out errorCode)) return false;
            if (!string.IsNullOrWhiteSpace(snapshot.WorkspacePath) &&
                !ProjectSpaceValidation.TryNormalizeWorkspacePath(snapshot.WorkspacePath,
                    out ignored, out errorCode)) return false;
            startInfo = new ProcessStartInfo("explorer.exe",
                "shell:AppsFolder\\" + ProjectSpaceValidation.ChatGptAppId);
            startInfo.UseShellExecute = true;
            startInfo.CreateNoWindow = false;
            errorCode = "";
            return true;
        }
        if (step.Kind == ProjectStepKind.OpenWorkspaceWithVerifiedAdapter &&
            step.Resource == ProjectStepResource.Editor)
        {
            if (!ProjectSpaceValidation.TryNormalizeEditorExecutable(snapshot.EditorExecutablePath,
                    snapshot.EditorKind, out target, out errorCode) ||
                !ProjectSpaceValidation.TryNormalizeWorkspacePath(snapshot.WorkspacePath,
                    out ignored, out errorCode)) return false;
            startInfo = new ProcessStartInfo(target, QuoteSingleArgument(ignored));
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = false;
            errorCode = "";
            return true;
        }

        if (step.Kind == ProjectStepKind.OpenOrActivateApp &&
            (step.Resource == ProjectStepResource.Editor || step.Resource == ProjectStepResource.Terminal))
        {
            bool valid = step.Resource == ProjectStepResource.Editor
                ? ProjectSpaceValidation.TryNormalizeEditorExecutable(snapshot.EditorExecutablePath,
                    snapshot.EditorKind, out target, out errorCode)
                : ProjectSpaceValidation.TryNormalizeApplicationExecutable(snapshot.TerminalExecutablePath,
                    out target, out errorCode);
            if (!valid) return false;
            startInfo = new ProcessStartInfo(target);
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = false;
            errorCode = "";
            return true;
        }

        if (step.Kind == ProjectStepKind.OpenUrl &&
            (step.Resource == ProjectStepResource.Preview ||
             step.Resource == ProjectStepResource.Repository ||
             step.Resource == ProjectStepResource.Documentation))
        {
            string source = step.Resource == ProjectStepResource.Preview ? snapshot.PreviewUrl :
                step.Resource == ProjectStepResource.Repository ? snapshot.RepositoryUrl : snapshot.DocumentationUrl;
            bool valid = step.Resource == ProjectStepResource.Preview
                ? ProjectSpaceValidation.TryNormalizePreviewUrl(source, out target, out errorCode)
                : ProjectSpaceValidation.TryNormalizeHttpsUrl(source, out target, out errorCode);
            if (!valid) return false;
            startInfo = new ProcessStartInfo(target);
            startInfo.UseShellExecute = true;
            startInfo.Arguments = "";
            errorCode = "";
            return true;
        }
        return false;
    }
}

internal enum ProjectStepResource
{
    Editor = 0,
    Terminal = 1,
    Preview = 2,
    Repository = 3,
    Documentation = 4,
    Profile = 5,
    FocusTarget = 6,
    Notification = 7
}

internal sealed class ProjectPlanStep
{
    public ProjectStepKind Kind { get; private set; }
    public ProjectStepResource Resource { get; private set; }

    internal ProjectPlanStep(ProjectStepKind kind, ProjectStepResource resource)
    {
        Kind = kind;
        Resource = resource;
    }
}

internal sealed class ProjectStepResult
{
    public ProjectPlanStep Step { get; private set; }
    public ActionResult Result { get; private set; }
    public long DurationMs { get; private set; }

    internal ProjectStepResult(ProjectPlanStep step, ActionResult result, long durationMs)
    {
        Step = step;
        Result = result;
        DurationMs = Math.Max(0, durationMs);
    }
}

internal sealed class ProjectRunReport
{
    public ProjectSpace Snapshot { get; private set; }
    public List<ProjectStepResult> Steps { get; private set; }
    public ActionResult FinalResult { get; private set; }
    public bool IsSuccess { get { return FinalResult != null && FinalResult.IsSuccess; } }

    internal ProjectRunReport(ProjectSpace snapshot, List<ProjectStepResult> steps, ActionResult finalResult)
    {
        Snapshot = snapshot;
        Steps = steps ?? new List<ProjectStepResult>();
        FinalResult = finalResult;
    }
}

internal sealed class ProjectRunReservation
{
    private readonly ProjectSpaceRunner owner;
    private int consumed;

    internal long CancellationEpoch { get; private set; }
    internal long VoiceCancellationEpoch { get; private set; }

    internal ProjectRunReservation(ProjectSpaceRunner runner, long cancellationEpoch,
        long voiceCancellationEpoch)
    {
        owner = runner;
        CancellationEpoch = cancellationEpoch;
        VoiceCancellationEpoch = voiceCancellationEpoch;
    }

    internal bool TryConsume(ProjectSpaceRunner runner)
    {
        return object.ReferenceEquals(owner, runner) &&
            Interlocked.CompareExchange(ref consumed, 1, 0) == 0;
    }
}

internal interface IProjectSpaceExecutionBackend
{
    ActionResult ExecuteStep(ProjectSpace snapshot, ProjectPlanStep step,
        Func<bool> cancellationRequested);
}

internal sealed class DelegateProjectSpaceExecutionBackend : IProjectSpaceExecutionBackend
{
    private readonly Func<ProjectSpace, ProjectPlanStep, Func<bool>, ActionResult> handler;

    internal DelegateProjectSpaceExecutionBackend(
        Func<ProjectSpace, ProjectPlanStep, Func<bool>, ActionResult> handler)
    {
        if (handler == null) throw new ArgumentNullException("handler");
        this.handler = handler;
    }

    public ActionResult ExecuteStep(ProjectSpace snapshot, ProjectPlanStep step,
        Func<bool> cancellationRequested)
    {
        return handler(snapshot, step, cancellationRequested);
    }
}

internal sealed class ProjectSpaceExecutionBackend : IProjectSpaceExecutionBackend
{
    private readonly Func<ProcessStartInfo, ProjectStepResource, Func<bool>, ActionResult> processRequest;
    private readonly Func<ProjectSpace, string, Func<bool>, ActionResult> profileSwitch;
    private readonly Func<ProjectSpace, string, Func<bool>, ActionResult> focusTarget;
    private readonly Func<ProjectSpace, ActionResult> notification;

    internal ProjectSpaceExecutionBackend(
        Func<ProcessStartInfo, ProjectStepResource, Func<bool>, ActionResult> processRequest,
        Func<ProjectSpace, string, Func<bool>, ActionResult> profileSwitch,
        Func<ProjectSpace, string, Func<bool>, ActionResult> focusTarget,
        Func<ProjectSpace, ActionResult> notification)
    {
        if (processRequest == null) throw new ArgumentNullException("processRequest");
        if (profileSwitch == null) throw new ArgumentNullException("profileSwitch");
        if (focusTarget == null) throw new ArgumentNullException("focusTarget");
        if (notification == null) throw new ArgumentNullException("notification");
        this.processRequest = processRequest;
        this.profileSwitch = profileSwitch;
        this.focusTarget = focusTarget;
        this.notification = notification;
    }

    public ActionResult ExecuteStep(ProjectSpace snapshot, ProjectPlanStep step,
        Func<bool> cancellationRequested)
    {
        if (Canceled(cancellationRequested)) return CanceledResult();
        if (step == null)
            return Failure("入口步骤无效，后续步骤未执行", "PROJECT-STEP-UNSUPPORTED");

        if (step.Kind == ProjectStepKind.OpenWorkspaceWithVerifiedAdapter ||
            step.Kind == ProjectStepKind.OpenOrActivateApp || step.Kind == ProjectStepKind.OpenUrl)
        {
            ProcessStartInfo startInfo;
            string errorCode;
            if (!ProjectSpaceProcess.TryCreateStartInfo(snapshot, step, out startInfo, out errorCode))
                return Failure("项目资源无效，本次未执行打开请求", errorCode);
            if (Canceled(cancellationRequested)) return CanceledResult();
            return processRequest(startInfo, step.Resource, cancellationRequested);
        }
        if (step.Kind == ProjectStepKind.SwitchProfile && step.Resource == ProjectStepResource.Profile)
            return Canceled(cancellationRequested) ? CanceledResult() :
                profileSwitch(snapshot, snapshot.ProfileId, cancellationRequested);
        if (step.Kind == ProjectStepKind.FocusTarget && step.Resource == ProjectStepResource.FocusTarget)
            return Canceled(cancellationRequested) ? CanceledResult() :
                focusTarget(snapshot, snapshot.FocusTargetId, cancellationRequested);
        if (step.Kind == ProjectStepKind.ShowNotification && step.Resource == ProjectStepResource.Notification)
            return Canceled(cancellationRequested) ? CanceledResult() : notification(snapshot);
        return Failure("项目步骤不受支持，后续步骤未执行", "PROJECT-STEP-UNSUPPORTED");
    }

    private static bool Canceled(Func<bool> cancellationRequested)
    {
        try { return cancellationRequested != null && cancellationRequested(); }
        catch { return true; }
    }

    private static ActionResult CanceledResult()
    {
        return ActionResult.Create("打开项目", "项目", ActionState.Canceled,
            "项目已取消，本次操作未执行", "取消发生在外部操作之前",
            "需要时重新打开项目", "PROJECT-CANCELED");
    }

    private static ActionResult Failure(string message, string errorCode)
    {
        return ActionResult.Create("打开项目", "项目", ActionState.Error, message,
            "受约束执行器拒绝了当前资源", "编辑项目资源后重试",
            string.IsNullOrWhiteSpace(errorCode) ? "PROJECT-STEP-FAILED" : errorCode);
    }
}

internal sealed class ProjectSpaceRunner
{
    private readonly IProjectSpaceExecutionBackend backend;
    private readonly Func<bool> recordingHasPriority;
    private readonly Action<ActionResult> publish;
    private readonly Action<string> log;
    private int running;
    private long cancellationEpoch;
    private long voiceCancellationEpoch;

    internal ProjectSpaceRunner(IProjectSpaceExecutionBackend backend,
        Func<bool> recordingHasPriority, Action<ActionResult> publishResult, Action<string> safeLog)
    {
        if (backend == null) throw new ArgumentNullException("backend");
        this.backend = backend;
        this.recordingHasPriority = recordingHasPriority ?? delegate { return false; };
        publish = publishResult ?? delegate { };
        log = safeLog ?? delegate { };
    }

    internal bool IsRunning { get { return Interlocked.CompareExchange(ref running, 0, 0) != 0; } }

    internal ProjectRunReport Run(ProjectSpace source, int timeoutMs)
    {
        ProjectSpace snapshot = source == null ? null : source.Copy();
        var results = new List<ProjectStepResult>();
        string validationCode = "PROJECT-SPACE-MISSING";
        if (snapshot == null || !snapshot.TryValidateForStorage(out validationCode))
            return Finish(snapshot, results, ActionState.Error,
                "项目配置无效，未执行任何步骤", "请编辑并重新检查入口资源",
                validationCode.Length == 0 ? "PROJECT-INVALID" : validationCode);
        if (!snapshot.Enabled)
            return Finish(snapshot, results, ActionState.Error,
                "项目已停用，未执行任何步骤", "在项目设置中启用后重试", "PROJECT-DISABLED");
        ProjectRunReservation reservation;
        if (!TryReserve(out reservation))
            return Finish(snapshot, results, ActionState.Warning,
                "已有项目正在执行", "等待当前入口完成或先取消", "PROJECT-RUN-BUSY");

        return RunReserved(snapshot, timeoutMs, reservation);
    }

    internal bool TryReserve(out ProjectRunReservation reservation)
    {
        reservation = null;
        long startCancellationEpoch = Interlocked.Read(ref cancellationEpoch);
        long startVoiceEpoch = Interlocked.Read(ref voiceCancellationEpoch);
        if (Interlocked.CompareExchange(ref running, 1, 0) != 0) return false;
        reservation = new ProjectRunReservation(this, startCancellationEpoch, startVoiceEpoch);
        return true;
    }

    internal bool ReleaseReservation(ProjectRunReservation reservation)
    {
        if (reservation == null || !reservation.TryConsume(this)) return false;
        Interlocked.Exchange(ref running, 0);
        return true;
    }

    internal ProjectRunReport RunReserved(ProjectSpace source, int timeoutMs,
        ProjectRunReservation reservation)
    {
        if (reservation == null || !reservation.TryConsume(this))
            throw new InvalidOperationException("Project Space run reservation is invalid or already consumed");

        ProjectSpace snapshot = null;
        var results = new List<ProjectStepResult>();
        long startCancellationEpoch = reservation.CancellationEpoch;
        long startVoiceEpoch = reservation.VoiceCancellationEpoch;
        var elapsed = Stopwatch.StartNew();
        try
        {
            snapshot = source == null ? null : source.Copy();
            string validationCode = "PROJECT-SPACE-MISSING";
            if (snapshot == null || !snapshot.TryValidateForStorage(out validationCode))
                return Finish(snapshot, results, ActionState.Error,
                    "项目配置无效，未执行任何步骤", "请编辑并重新检查入口资源",
                    validationCode.Length == 0 ? "PROJECT-INVALID" : validationCode);
            if (!snapshot.Enabled)
                return Finish(snapshot, results, ActionState.Error,
                    "项目已停用，未执行任何步骤", "在项目设置中启用后重试", "PROJECT-DISABLED");
            if (timeoutMs < 1) timeoutMs = 1;
            ActionResult canceled = CancellationResult(snapshot, startCancellationEpoch, startVoiceEpoch);
            if (canceled != null) return PublishReport(snapshot, results, canceled);

            List<ProjectPlanStep> plan = BuildPlan(snapshot);
            for (int index = 0; index < plan.Count; index++)
            {
                canceled = CancellationResult(snapshot, startCancellationEpoch, startVoiceEpoch);
                if (canceled != null) return PublishReport(snapshot, results, canceled);
                if (elapsed.ElapsedMilliseconds >= timeoutMs)
                    return Finish(snapshot, results, ActionState.Error,
                        "项目未在限定时间内完成，后续步骤未执行",
                        "检查已完成步骤后重试", "PROJECT-TIMEOUT");

                ProjectPlanStep step = plan[index];
                SafePublish(ActionResult.Create("打开项目", StepTarget(step), ActionState.Running,
                    "正在执行第 " + (index + 1) + "/" + plan.Count + " 步：" + StepText(step),
                    "", "", ""));
                var stepTimer = Stopwatch.StartNew();
                ActionResult result;
                try
                {
                    result = backend.ExecuteStep(snapshot, step,
                        delegate { return IsCancellationRequested(startCancellationEpoch, startVoiceEpoch); });
                }
                catch
                {
                    result = ActionResult.Create("打开项目", StepTarget(step), ActionState.Error,
                        "当前步骤未完成，后续步骤未执行", "系统拒绝了受约束操作",
                        "检查项目资源后重试", "PROJECT-STEP-FAILED");
                }
                if (result == null)
                    result = ActionResult.Create("打开项目", StepTarget(step), ActionState.Error,
                        "当前步骤没有返回结果，后续步骤未执行", "执行器未提供可验证回执",
                        "检查项目设置后重试", "PROJECT-STEP-NO-RESULT");
                results.Add(new ProjectStepResult(step, result, stepTimer.ElapsedMilliseconds));
                SafeLog(snapshot.Id, step, result, stepTimer.ElapsedMilliseconds);
                SafePublish(result);

                canceled = CancellationResult(snapshot, startCancellationEpoch, startVoiceEpoch);
                if (canceled != null) return PublishReport(snapshot, results, canceled);
                if (elapsed.ElapsedMilliseconds >= timeoutMs)
                    return Finish(snapshot, results, ActionState.Error,
                        "项目未在限定时间内完成，后续步骤未执行",
                        "检查已完成步骤后重试", "PROJECT-TIMEOUT");
                if (!result.IsSuccess) return Report(snapshot, results, result);
            }
            return Finish(snapshot, results, ActionState.Success,
                "项目步骤已按顺序执行", "", "");
        }
        catch
        {
            return Finish(snapshot, results, ActionState.Error,
                "项目未开始，未执行任何外部操作", "重新检查项目配置后重试",
                "PROJECT-RUN-INITIALIZATION-FAILED");
        }
        finally
        {
            Interlocked.Exchange(ref running, 0);
        }
    }

    internal void CancelCurrent()
    {
        Interlocked.Increment(ref cancellationEpoch);
    }

    internal void CancelForRecording()
    {
        Interlocked.Increment(ref voiceCancellationEpoch);
        Interlocked.Increment(ref cancellationEpoch);
    }

    internal static List<ProjectPlanStep> BuildPlan(ProjectSpace snapshot)
    {
        var plan = new List<ProjectPlanStep>();
        bool workspaceAdapter = snapshot != null &&
            (string.Equals(snapshot.EditorKind, "cursor", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(snapshot.EditorKind, "vscode", StringComparison.OrdinalIgnoreCase));
        plan.Add(new ProjectPlanStep(workspaceAdapter
            ? ProjectStepKind.OpenWorkspaceWithVerifiedAdapter
            : ProjectStepKind.OpenOrActivateApp, ProjectStepResource.Editor));
        if (!string.IsNullOrWhiteSpace(snapshot.TerminalExecutablePath))
            plan.Add(new ProjectPlanStep(ProjectStepKind.OpenOrActivateApp, ProjectStepResource.Terminal));
        if (!string.IsNullOrWhiteSpace(snapshot.PreviewUrl))
            plan.Add(new ProjectPlanStep(ProjectStepKind.OpenUrl, ProjectStepResource.Preview));
        if (!string.IsNullOrWhiteSpace(snapshot.RepositoryUrl))
            plan.Add(new ProjectPlanStep(ProjectStepKind.OpenUrl, ProjectStepResource.Repository));
        if (!string.IsNullOrWhiteSpace(snapshot.DocumentationUrl))
            plan.Add(new ProjectPlanStep(ProjectStepKind.OpenUrl, ProjectStepResource.Documentation));
        if (!string.IsNullOrWhiteSpace(snapshot.ProfileId))
            plan.Add(new ProjectPlanStep(ProjectStepKind.SwitchProfile, ProjectStepResource.Profile));
        if (!string.IsNullOrWhiteSpace(snapshot.FocusTargetId))
            plan.Add(new ProjectPlanStep(ProjectStepKind.FocusTarget, ProjectStepResource.FocusTarget));
        plan.Add(new ProjectPlanStep(ProjectStepKind.ShowNotification, ProjectStepResource.Notification));
        return plan;
    }

    internal static List<string> BuildReceiptLines(ProjectRunReport report)
    {
        var lines = new List<string>();
        if (report == null)
        {
            lines.Add("尚无项目执行记录");
            return lines;
        }
        string projectName = report.Snapshot == null || string.IsNullOrWhiteSpace(report.Snapshot.Name)
            ? "项目" : report.Snapshot.Name;
        string projectId = report.Snapshot == null ? "unknown" : report.Snapshot.Id;
        lines.Add("最近执行：" + projectName + " · ID " + projectId);
        ActionResult final = report.FinalResult;
        string finalState = final == null ? "未知" : StateText(final.State);
        string finalMessage = final == null ? "执行器未返回最终结果" : final.Message;
        string finalCode = final == null || string.IsNullOrWhiteSpace(final.ErrorCode)
            ? "" : " · 错误码 " + final.ErrorCode;
        lines.Add("最终状态：" + finalState + " · " + finalMessage + finalCode);
        if (final != null && final.State == ActionState.Canceled)
            lines.Add("已取消后续步骤；已完成的外部操作不会回滚。");
        for (int index = 0; index < report.Steps.Count; index++)
        {
            ProjectStepResult stepResult = report.Steps[index];
            ProjectPlanStep step = stepResult == null ? null : stepResult.Step;
            ActionResult result = stepResult == null ? null : stepResult.Result;
            string state = result == null ? "未知" : StateText(result.State);
            string code = result == null || string.IsNullOrWhiteSpace(result.ErrorCode)
                ? "" : " · 错误码 " + result.ErrorCode;
            lines.Add((index + 1) + ". " + StepTarget(step) + " · " + StepText(step) + " · " + state + " · " +
                (stepResult == null ? 0 : stepResult.DurationMs) + " ms" + code);
        }
        return lines;
    }

    private static string StateText(ActionState state)
    {
        if (state == ActionState.Success) return "成功";
        if (state == ActionState.Warning) return "警告";
        if (state == ActionState.Error) return "失败";
        if (state == ActionState.Canceled) return "已取消";
        if (state == ActionState.Running) return "执行中";
        if (state == ActionState.Checking) return "检查中";
        return "等待";
    }

    private bool IsCancellationRequested(long startCancellationEpoch, long startVoiceEpoch)
    {
        return Interlocked.Read(ref cancellationEpoch) != startCancellationEpoch ||
            Interlocked.Read(ref voiceCancellationEpoch) != startVoiceEpoch || SafeRecordingPriority();
    }

    private ActionResult CancellationResult(ProjectSpace snapshot,
        long startCancellationEpoch, long startVoiceEpoch)
    {
        bool voice = Interlocked.Read(ref voiceCancellationEpoch) != startVoiceEpoch || SafeRecordingPriority();
        if (!voice && Interlocked.Read(ref cancellationEpoch) == startCancellationEpoch) return null;
        return ActionResult.Create("打开项目", snapshot == null ? "未设置" : snapshot.Name,
            ActionState.Canceled,
            voice ? "录音已开始，后续入口步骤已取消" : "项目已取消，后续步骤未执行",
            voice ? "录音操作优先；已经完成的步骤不会回滚" : "已经完成的步骤不会回滚",
            voice ? "录音结束后重新打开项目" : "需要时重新打开项目",
            voice ? "PROJECT-CANCELED-VOICE" : "PROJECT-CANCELED");
    }

    private bool SafeRecordingPriority()
    {
        try { return recordingHasPriority(); }
        catch { return true; }
    }

    private ProjectRunReport Finish(ProjectSpace snapshot, List<ProjectStepResult> results,
        ActionState state, string message, string recovery, string errorCode)
    {
        ActionResult final = ActionResult.Create("打开项目", snapshot == null ? "未设置" : snapshot.Name,
            state, message, state == ActionState.Success ? "" : "未执行剩余步骤", recovery, errorCode);
        SafePublish(final);
        return Report(snapshot, results, final);
    }

    private static ProjectRunReport Report(ProjectSpace snapshot,
        List<ProjectStepResult> results, ActionResult final)
    {
        return new ProjectRunReport(snapshot, results, final);
    }

    private ProjectRunReport PublishReport(ProjectSpace snapshot,
        List<ProjectStepResult> results, ActionResult final)
    {
        SafePublish(final);
        return Report(snapshot, results, final);
    }

    private void SafePublish(ActionResult result)
    {
        try { publish(result); } catch { }
    }

    private void SafeLog(string spaceId, ProjectPlanStep step, ActionResult result, long elapsedMs)
    {
        try
        {
            log("PROJECT SPACE space_id=" + SafeCode(spaceId, "invalid") +
                " step=" + step.Kind + " resource=" + step.Resource +
                " state=" + result.State.ToString().ToLowerInvariant() +
                " code=" + SafeCode(result.ErrorCode, result.IsSuccess ? "OK" : "PROJECT-FAILED") +
                " elapsed_ms=" + Math.Max(0, elapsedMs));
        }
        catch { }
    }

    private static string SafeCode(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        var safe = new System.Text.StringBuilder();
        foreach (char character in value.Trim())
        {
            if (safe.Length >= 80) break;
            if (char.IsLetterOrDigit(character) || character == '_' || character == '-' || character == '.')
                safe.Append(character);
        }
        return safe.Length == 0 ? fallback : safe.ToString();
    }

    private static string StepTarget(ProjectPlanStep step)
    {
        if (step == null) return "未知步骤";
        switch (step.Resource)
        {
            case ProjectStepResource.Editor: return "编辑器";
            case ProjectStepResource.Terminal: return "终端";
            case ProjectStepResource.Preview: return "本地预览";
            case ProjectStepResource.Repository: return "仓库网页";
            case ProjectStepResource.Documentation: return "需求文档";
            case ProjectStepResource.Profile: return "Profile";
            case ProjectStepResource.FocusTarget: return "输入目标";
            default: return "项目";
        }
    }

    private static string StepText(ProjectPlanStep step)
    {
        if (step == null) return "未返回步骤信息";
        switch (step.Kind)
        {
            case ProjectStepKind.OpenWorkspaceWithVerifiedAdapter: return "打开本地目录";
            case ProjectStepKind.OpenOrActivateApp: return "打开或激活应用";
            case ProjectStepKind.OpenUrl: return "打开网页";
            case ProjectStepKind.SwitchProfile: return "应用关联 Profile（智能切换开启时设为默认）";
            case ProjectStepKind.FocusTarget: return "锁定输入目标";
            default: return "显示执行结果";
        }
    }
}
