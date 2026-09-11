using System.Drawing;

internal enum VibePageId
{
    Home = 0,
    Workflow = 1,
    Controls = 2,
    Voice = 3,
    Diagnostics = 4,
    Settings = 5,
    Notes = 6
}

internal static class UiDesignTokens
{
    internal const int PageCount = 6;
    internal const int SpacingUnit = 8;
    internal const int SidebarWidth = 232;
    internal const int SidebarHeaderHeight = 104;
    internal const int SidebarFooterHeight = 76;
    internal const int NavigationButtonHeight = 48;
    internal const int NavigationGap = 8;
    internal const int ContentPaddingHorizontal = 34;
    internal const int ContentPaddingVertical = 26;
    internal const int ContentMinimumWidth = 1000;
    internal const int ContentMinimumHeight = 744;

    // --- Feedback surfaces -------------------------------------------------------------
    // The in-window toast and the floating Live HUD report the same states, so their
    // accent colours, glyphs, type steps and lifetimes live here once. Both surfaces used
    // to carry their own copy of the same values: the colours had not drifted (they were
    // byte-for-byte equal), but the lifetimes had, and nothing stopped a future edit from
    // changing one surface and not the other.
    internal const string FeedbackFontFamily = "Microsoft YaHei UI";
    // The compact in-window card and the larger floating panel use different type steps and
    // radii on purpose — those are named here so the two cannot drift, not so they match.
    internal const float FeedbackInlineTitleSize = 9.3f;
    internal const float FeedbackPanelTitleSize = 10.5f;
    internal const float FeedbackDetailSize = 9f;
    internal const float FeedbackCaptionSize = 8.2f;
    internal const int FeedbackRadiusCompact = 10;
    internal const int FeedbackRadiusPanel = 18;
    // One lifetime rule for both surfaces: a result that needs the user's attention stays
    // long enough to read, everything else is a glance.
    internal const int FeedbackInfoDurationMs = 2800;
    internal const int FeedbackProblemDurationMs = 12000;

    internal static Color StatusAccent(ActionState state)
    {
        switch (state)
        {
            case ActionState.Success: return Color.FromArgb(10, 164, 104);
            case ActionState.Warning:
            case ActionState.Canceled: return Color.FromArgb(229, 151, 39);
            case ActionState.Error: return Color.FromArgb(204, 70, 82);
            case ActionState.Checking: return Color.FromArgb(0, 153, 190);
            default: return Color.FromArgb(104, 82, 244);
        }
    }

    // Empty means "draw the vector microphone" on the floating panel, which is how the
    // running/checking/idle states stay crisp on any installed font.
    internal static string StatusGlyph(ActionState state)
    {
        switch (state)
        {
            case ActionState.Success: return "\uE73E";
            case ActionState.Warning: return "\uE7BA";
            case ActionState.Error: return "\uEA39";
            case ActionState.Canceled: return "\uE711";
            default: return "";
        }
    }

    // The in-window card carries the informative glyph for the states whose glyph is empty
    // on the panel, so a compact card is never blank.
    internal static string InlineStatusGlyph(ActionState state)
    {
        string glyph = StatusGlyph(state);
        return glyph.Length > 0 ? glyph : "\uE946";
    }

    internal static ActionState StateForKind(string kind)
    {
        string normalized = (kind ?? "").Trim().ToLowerInvariant();
        if (normalized == "error") return ActionState.Error;
        if (normalized == "success") return ActionState.Success;
        if (normalized == "warning" || normalized == "canceled") return ActionState.Warning;
        if (normalized == "running") return ActionState.Running;
        if (normalized == "checking") return ActionState.Checking;
        return ActionState.Idle;
    }

    internal static int DurationForState(ActionState state)
    {
        return state == ActionState.Error || state == ActionState.Warning ||
            state == ActionState.Canceled ? FeedbackProblemDurationMs : FeedbackInfoDurationMs;
    }
}
