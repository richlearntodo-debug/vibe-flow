using System;

// What one favourite application's row has to be able to say before it can offer the right action.
//
// A favourite is "learned" once it points at a stored focus target, "verified" once that target carries a
// verification timestamp, and "missing" when the stored id no longer resolves — which is what happens after
// a target is replaced or the store is rebuilt. The mode names the runtime contract: only a workflow entry
// takes part in automatic text delivery; a shortcut entry is summoned on demand and never claims the text.
// That split is the whole meaning of the two modes — before this they differed only by which one could be
// marked as the current app, while the toast text claimed the opposite.
internal enum FavoriteAppState
{
    NotLearned,
    LearnedUnverified,
    Verified,
    TargetMissing
}

internal static class FavoriteAppStatus
{
    internal const string WorkflowMode = "workflow";
    internal const string ShortcutMode = "shortcut";
    internal const string WorkflowModeLabel = "工作流";
    internal const string ShortcutModeLabel = "快捷键";

    internal static bool IsWorkflowMode(string mode)
    {
        return string.Equals((mode ?? "").Trim(), WorkflowMode, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsShortcutMode(string mode)
    {
        return string.Equals((mode ?? "").Trim(), ShortcutMode, StringComparison.OrdinalIgnoreCase);
    }

    internal static string ModeValue(bool workflow)
    {
        return workflow ? WorkflowMode : ShortcutMode;
    }

    internal static FavoriteAppState Classify(string targetId, DateTime? lastVerifiedUtc, bool targetExists)
    {
        if (string.IsNullOrWhiteSpace(targetId)) return FavoriteAppState.NotLearned;
        if (!targetExists) return FavoriteAppState.TargetMissing;
        return lastVerifiedUtc.HasValue ? FavoriteAppState.Verified : FavoriteAppState.LearnedUnverified;
    }

    // Only a learned entry can receive the text at all, so "设为当前" is meaningless before learning.
    internal static bool CanBecomeCurrent(FavoriteAppState state)
    {
        return state == FavoriteAppState.Verified || state == FavoriteAppState.LearnedUnverified;
    }

    // The row's one line of state. A shortcut entry says plainly that it does not take the text, instead of
    // promising delivery that only a current workflow entry performs.
    internal static string DescribeForRow(FavoriteAppState state, bool isCurrent, bool isWorkflow)
    {
        if (state == FavoriteAppState.NotLearned) return "还没学习过 · 点「学习」开始";
        if (state == FavoriteAppState.TargetMissing) return "目标已失效 · 点「重新学习」修复";
        if (isCurrent) return "文字会进入它的输入框";
        if (!isWorkflow) return "快捷键模式 · 只按需召唤，不自动接走文字";
        return state == FavoriteAppState.LearnedUnverified
            ? "已学习但未验证 · 建议先点「测试」"
            : "点「设为当前」，文字就会进入它";
    }

    internal static string ModeTooltip(bool workflow)
    {
        return workflow
            ? "工作流模式：可以被设为「当前」；说话前把焦点恢复到它学到的输入框，文字固定送进它"
            : "快捷键模式：只在点「打开」时被召唤，不会自动接走文字";
    }
}
