using System;
using System.Collections.Generic;

// A workflow card composes capabilities the product already has for one
// application: the shortcut Profile bound to it, the learned input target for its
// process, the configured voice tool, and the last real evidence that the binding
// fired. Nothing new is persisted — the card is a read-only, truthful composition,
// so a user can see why an application did not behave as expected instead of
// guessing which of the three parts was missing.
internal sealed class WorkflowAppBinding
{
    internal string ProcessName = "";
    internal string ProfileId = "";
    internal string ProfileName = "";
    internal bool IsActiveProfile;
}

internal sealed class WorkflowTargetBinding
{
    internal string ProcessName = "";
    internal string Name = "";
    internal bool Verified;
    internal bool IsDefault;
}

internal sealed class WorkflowCard
{
    internal string ProcessName = "";
    internal string ProfileId = "";
    internal string ProfileName = "";
    internal bool ProfileBound;
    internal bool ProfileActive;
    internal string TargetName = "";
    internal bool TargetBound;
    internal bool TargetVerified;
    internal bool TargetIsDefault;
    internal bool ProviderRunning;
    internal bool ObservedRecently;
    internal string Evidence = "";
    internal List<string> Gaps = new List<string>();

    internal bool IsReady { get { return Gaps.Count == 0; } }

    internal string State
    {
        get
        {
            if (!ProfileBound || !TargetBound) return "warning";
            if (!TargetVerified || !ProviderRunning) return "warning";
            return Gaps.Count == 0 ? "pass" : "checking";
        }
    }

    internal string Title
    {
        get { return string.IsNullOrWhiteSpace(ProcessName) ? "未命名应用" : ProcessName; }
    }

    internal string Expected
    {
        get
        {
            return "该应用使用绑定到它的键位 Profile，并把它自己的工作流当作落字对象；三部分都齐了才算配置完成";
        }
    }

    internal string Actual
    {
        get
        {
            return "键位 " + (ProfileBound ? ProfileName : "未绑定") +
                " · 工作流 " + (TargetBound ? TargetName + (TargetVerified ? "（已验证）" : "（未验证）") : "未设置") +
                " · 语音工具 " + (ProviderRunning ? "已运行" : "未运行");
        }
    }

    internal string Cause
    {
        get
        {
            if (Gaps.Count == 0) return string.IsNullOrWhiteSpace(Evidence) ? "未发现异常" : Evidence;
            return WorkflowCards.DescribeGap(Gaps[0]);
        }
    }

    internal string NextStep
    {
        get
        {
            if (Gaps.Count == 0) return "无需操作";
            return WorkflowCards.AdviseGap(Gaps[0]);
        }
    }

    internal string ActionText
    {
        get
        {
            if (Gaps.Count == 0) return "";
            return WorkflowCards.ActionTextForGap(Gaps[0]);
        }
    }

    internal string Action
    {
        get
        {
            if (Gaps.Count == 0) return "";
            string action = WorkflowCards.ActionForGap(Gaps[0]);
            // Learning must know which application to bring forward.
            if (Gaps[0] == WorkflowCards.GapNoTarget) return action + ":" + ProcessName;
            return action;
        }
    }
}

internal static class WorkflowCards
{
    internal const string GapNoProfile = "NO_PROFILE";
    internal const string GapNoTarget = "NO_TARGET";
    internal const string GapTargetUnverified = "TARGET_UNVERIFIED";
    internal const string GapProviderNotRunning = "PROVIDER_NOT_RUNNING";
    internal const string GapNeverObserved = "NEVER_OBSERVED";

    internal static string DescribeGap(string gap)
    {
        switch (gap)
        {
            case GapNoProfile: return "这个应用还没有绑定键位 Profile，切换过去时按键仍是通用配置";
            case GapNoTarget: return "这个应用还没有学习并验证工作流，录音结束后无法确认文字落点";
            case GapTargetUnverified: return "已保存的工作流在这台机器上尚未验证通过";
            case GapProviderNotRunning: return "默认语音工具当前没有运行，唤起可能失败";
            case GapNeverObserved: return "配置已完整，但还没有看到它在真实按键下生效";
            default: return "未发现异常";
        }
    }

    internal static string AdviseGap(string gap)
    {
        switch (gap)
        {
            case GapNoProfile: return "为它选择一个键位 Profile，或把应用加入现有 Profile 的应用列表";
            case GapNoTarget: return "打开它并聚焦输入框，用“设置工作流”学习一次";
            case GapTargetUnverified: return "重新学习一次工作流；界面更新后旧目标可能失效";
            case GapProviderNotRunning: return "先启动默认语音工具，或改用网易八哥说";
            case GapNeverObserved: return "切换到该应用后按一次遥控器录音键完成验证";
            default: return "无需操作";
        }
    }

    internal static string ActionTextForGap(string gap)
    {
        switch (gap)
        {
            case GapNoProfile: return "选择快捷键 Profile";
            case GapNoTarget: return "打开应用并学习";
            case GapTargetUnverified: return "设置工作流";
            case GapProviderNotRunning: return "检查语音工具";
            case GapNeverObserved: return "验证一次";
            default: return "";
        }
    }

    internal static string ActionForGap(string gap)
    {
        switch (gap)
        {
            case GapNoProfile: return "workflow-profile";
            case GapNoTarget: return "workflow-learn";
            case GapTargetUnverified: return "workflow-target";
            case GapProviderNotRunning: return "provider";
            case GapNeverObserved: return "test-dictation";
            default: return "";
        }
    }

    internal static List<WorkflowCard> Build(IList<WorkflowAppBinding> bindings,
        IList<WorkflowTargetBinding> targets, bool providerRunning,
        string observedProcessName, string observedProfileName, bool observedRecently)
    {
        var cards = new List<WorkflowCard>();
        var order = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (bindings != null)
        {
            foreach (WorkflowAppBinding binding in bindings)
            {
                if (binding == null || string.IsNullOrWhiteSpace(binding.ProcessName)) continue;
                if (seen.Add(binding.ProcessName.Trim())) order.Add(binding.ProcessName.Trim());
            }
        }
        if (targets != null)
        {
            foreach (WorkflowTargetBinding target in targets)
            {
                if (target == null || string.IsNullOrWhiteSpace(target.ProcessName)) continue;
                if (seen.Add(target.ProcessName.Trim())) order.Add(target.ProcessName.Trim());
            }
        }
        foreach (string processName in order)
        {
            var card = new WorkflowCard { ProcessName = processName, ProviderRunning = providerRunning };
            if (bindings != null)
            {
                foreach (WorkflowAppBinding binding in bindings)
                {
                    if (binding == null || !Matches(binding.ProcessName, processName)) continue;
                    card.ProfileBound = true;
                    card.ProfileId = binding.ProfileId ?? "";
                    card.ProfileName = string.IsNullOrWhiteSpace(binding.ProfileName) ? binding.ProfileId ?? "" : binding.ProfileName;
                    card.ProfileActive = binding.IsActiveProfile;
                    break;
                }
            }
            if (targets != null)
            {
                bool found = false;
                foreach (WorkflowTargetBinding target in targets)
                {
                    if (target == null || !Matches(target.ProcessName, processName)) continue;
                    if (!found || target.IsDefault || (target.Verified && !card.TargetVerified))
                    {
                        card.TargetBound = true;
                        card.TargetName = string.IsNullOrWhiteSpace(target.Name) ? target.ProcessName : target.Name;
                        card.TargetVerified = target.Verified;
                        card.TargetIsDefault = target.IsDefault;
                        found = true;
                    }
                }
            }
            bool observed = observedRecently && Matches(observedProcessName, processName);
            card.ObservedRecently = observed;
            if (observed)
                card.Evidence = "最近一次按键动作由 " +
                    (string.IsNullOrWhiteSpace(observedProfileName) ? processName : observedProfileName) + " 生效";

            if (!card.ProfileBound) card.Gaps.Add(GapNoProfile);
            if (!card.TargetBound) card.Gaps.Add(GapNoTarget);
            else if (!card.TargetVerified) card.Gaps.Add(GapTargetUnverified);
            if (!providerRunning) card.Gaps.Add(GapProviderNotRunning);
            if (card.Gaps.Count == 0 && !card.ObservedRecently) card.Gaps.Add(GapNeverObserved);
            cards.Add(card);
        }
        return cards;
    }

    // A short label for the summary line: the long description belongs to the card. Phrased as progress rather
    // than as faults — an application whose workflow has not been learned yet is the normal starting point, not an
    // error, and this label is now what a user reads on the 工作流 page for each application that still needs one.
    internal static string ShortGapLabel(string gap)
    {
        switch (gap)
        {
            case GapNoProfile: return "还没选择键位";
            case GapNoTarget: return "还没学习";
            case GapTargetUnverified: return "已学习未验证";
            case GapProviderNotRunning: return "语音工具未运行";
            case GapNeverObserved: return "还没真实验证";
            default: return "需要确认";
        }
    }

    // The summary is what a user reads first, so it reports counts that match the
    // cards exactly and names the most common missing part instead of a vague score.
    internal static string Summarize(IList<WorkflowCard> cards)
    {
        if (cards == null || cards.Count == 0)
            return "还没有应用工作流：在「快捷键 → Smart Profiles」里选一个 Profile，再回来学习。";
        int ready = 0;
        var gapCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (WorkflowCard card in cards)
        {
            if (card.IsReady) { ready++; continue; }
            string first = card.Gaps[0];
            int count;
            gapCounts.TryGetValue(first, out count);
            gapCounts[first] = count + 1;
        }
        if (ready == cards.Count) return cards.Count + " 个应用的键位与工作流都已就绪";
        string dominant = "";
        int dominantCount = 0;
        foreach (KeyValuePair<string, int> entry in gapCounts)
        {
            if (entry.Value > dominantCount) { dominant = entry.Key; dominantCount = entry.Value; }
        }
        return cards.Count + " 个应用：" + ready + " 个已就绪 · " + dominantCount + " 个" +
            ShortGapLabel(dominant);
    }

    private static bool Matches(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;
        return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
