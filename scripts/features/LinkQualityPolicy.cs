using System;

// Link quality is derived only from the metrics the frozen recording kernel already
// publishes (ATVV packet gaps, queue drops, output level, trigger latency). The
// thresholds mirror the published session self-check gate so the status strip, the
// self-check and the log can never disagree, and every verdict stays advisory: the
// app cannot read the target text box, so a good link is never reported as proof
// that text arrived.
internal sealed class LinkQualityVerdict
{
    internal string State = "unknown";
    internal string Summary = "等待首次听写";
    internal string Detail = "";
    internal string Advice = "";
    internal string ReasonCode = "NO_SESSION";

    internal bool IsGood { get { return State == "good"; } }

    internal bool IsDegraded { get { return State == "fair" || State == "poor"; } }

    internal bool IsKnown { get { return State != "unknown"; } }
}

internal static class LinkQualityPolicy
{
    // Mirrors the published session gate in the self-check: max ATVV gap 250 ms,
    // zero drops, 0.8 % output level, 1500 ms trigger-to-ready, 700 ms of audio.
    internal const int HealthyMaxGapMs = 250;
    internal const int DegradedMaxGapMs = 600;
    internal const int HealthyTriggerToReadyMs = 1500;
    internal const int DegradedTriggerToReadyMs = 3000;
    internal const double HealthyOutputRmsPercent = 0.8;
    internal const int MinimumAssessableAudioMs = 700;

    internal static LinkQualityVerdict Classify(bool started, bool success, bool failed, bool transportFailed,
        int audioMs, int maxGapMs, int queueDrops, int sinkQueueDrops, double outputRmsPercent, int triggerToReadyMs)
    {
        var verdict = new LinkQualityVerdict();
        if (!started)
        {
            verdict.ReasonCode = "NO_SESSION";
            return verdict;
        }
        int drops = Math.Max(0, queueDrops) + Math.Max(0, sinkQueueDrops);
        verdict.Detail = "间隔 " + Math.Max(0, maxGapMs) + " ms · 丢包 " + drops +
            " · 响应 " + Math.Max(0, triggerToReadyMs) + " ms";
        if (failed || transportFailed)
        {
            verdict.State = "poor";
            verdict.Summary = "本次失败";
            verdict.ReasonCode = "SESSION_FAILED";
            verdict.Advice = "本次会话未完成：打开“自检”按第一项错误修复后重试";
            return verdict;
        }
        if (drops > 0)
        {
            verdict.State = "poor";
            verdict.Summary = "出现丢包";
            verdict.ReasonCode = "AUDIO_DROPS";
            verdict.Advice = "出现音频丢包：靠近电脑、减少遮挡，必要时更换遥控器电池后重试";
            return verdict;
        }
        if (maxGapMs > DegradedMaxGapMs)
        {
            verdict.State = "poor";
            verdict.Summary = "蓝牙间隔异常";
            verdict.ReasonCode = "GAP_HIGH";
            verdict.Advice = "蓝牙音频间隔过大：把遥控器与电脑保持在 5 米内，并让接收端远离 USB 3.0 与金属遮挡";
            return verdict;
        }
        if (triggerToReadyMs > DegradedTriggerToReadyMs)
        {
            verdict.State = "poor";
            verdict.Summary = "工具响应过慢";
            verdict.ReasonCode = "LATENCY_HIGH";
            verdict.Advice = "语音工具响应超过 3 秒：确认工具已启动；首次唤起通常较慢，可先手动唤起一次预热";
            return verdict;
        }
        if (audioMs < MinimumAssessableAudioMs)
        {
            verdict.State = "fair";
            verdict.Summary = "样本过短";
            verdict.ReasonCode = "AUDIO_TOO_SHORT";
            verdict.Advice = "有效音频不足 0.7 秒：按住录音键持续说 1 秒以上，链路评估才有意义";
            return verdict;
        }
        if (maxGapMs > HealthyMaxGapMs || triggerToReadyMs > HealthyTriggerToReadyMs ||
            outputRmsPercent < HealthyOutputRmsPercent)
        {
            verdict.State = "fair";
            verdict.Summary = maxGapMs > HealthyMaxGapMs ? "轻微抖动" :
                triggerToReadyMs > HealthyTriggerToReadyMs ? "响应偏慢" : "声音偏小";
            verdict.ReasonCode = maxGapMs > HealthyMaxGapMs ? "GAP_ELEVATED" :
                triggerToReadyMs > HealthyTriggerToReadyMs ? "LATENCY_ELEVATED" : "LEVEL_LOW";
            verdict.Advice = maxGapMs > HealthyMaxGapMs
                ? "蓝牙间隔略高：保持遥控器与电脑之间无遮挡，可明显降低抖动"
                : triggerToReadyMs > HealthyTriggerToReadyMs
                    ? "语音工具响应偏慢：确认工具已启动并完成过一次预热"
                    : "声音偏小：靠近遥控器麦克风、自然说话即可";
            return verdict;
        }
        if (!success)
        {
            verdict.State = "fair";
            verdict.Summary = "等待完整回执";
            verdict.ReasonCode = "NO_RECEIPT";
            verdict.Advice = "还没有拿到工具回执：完成一次完整听写后再看链路评估";
            return verdict;
        }
        verdict.State = "good";
        verdict.Summary = "质量良好";
        verdict.ReasonCode = "HEALTHY";
        verdict.Advice = "链路指标正常；最终文字仍请目视确认";
        return verdict;
    }
}
