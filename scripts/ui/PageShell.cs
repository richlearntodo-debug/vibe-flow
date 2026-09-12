using System;
using System.Windows.Forms;

internal sealed partial class VibeMicForm
{
    // Keep the stable V1.5 destinations first-class. Notes and the removed
    // Quick Entries surface remain reserved page IDs only; neither is a
    // user-facing product page anymore.
    //
    // The order follows how the app is actually used: while dictating, the voice page is what a user keeps coming back
    // to, so it sits second, and the workflow page — where the text is aimed — follows the shortcuts page. The page IDs
    // are deliberately unchanged: the buttons carry ids rather than positions, so nothing else depends on this order.
    // The three arrays must stay index-aligned, including the icons.
    private static readonly string[] NavigationText = { "首页", "语音", "快捷键", "工作流", "自检", "设置" };
    private static readonly string[] NavigationIcons = { "overview", "voice", "shortcuts", "shortcuts", "diagnostics", "settings" };
    private static readonly int[] NavigationPageIds = {
        (int)VibePageId.Home, (int)VibePageId.Voice, (int)VibePageId.Controls,
        (int)VibePageId.Workflow, (int)VibePageId.Diagnostics, (int)VibePageId.Settings
    };

    private void BuildPage(VibePageId page)
    {
        switch (page)
        {
            case VibePageId.Home:
                BuildOverview();
                break;
            case VibePageId.Workflow:
                BuildWorkflowPage();
                break;
            case VibePageId.Controls:
                BuildMappingsPage();
                break;
            case VibePageId.Voice:
                BuildVoicePage();
                break;
            case VibePageId.Diagnostics:
                BuildDevicePage();
                break;
            case VibePageId.Settings:
                BuildSettingsPage();
                break;
            default:
                throw new InvalidOperationException("Unknown page: " + page);
        }
    }

    private static string ExpectedPageTitle(int page)
    {
        switch ((VibePageId)page)
        {
            case VibePageId.Home: return "首页";
            case VibePageId.Workflow: return "工作流";
            case VibePageId.Controls: return "快捷键";
            case VibePageId.Voice: return "语音";
            case VibePageId.Diagnostics: return "自检";
            case VibePageId.Settings: return "设置";
            default: return "";
        }
    }

    private static bool ControlTreeContainsText(Control rootControl, string expected)
    {
        if (rootControl == null || string.IsNullOrEmpty(expected)) return false;
        if (string.Equals(rootControl.Text, expected, StringComparison.Ordinal)) return true;
        foreach (Control child in rootControl.Controls)
            if (ControlTreeContainsText(child, expected)) return true;
        return false;
    }
}
