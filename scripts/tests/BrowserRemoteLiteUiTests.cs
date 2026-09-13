using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

internal static class BrowserRemoteLiteUiTests
{
    [STAThread]
    private static int Main()
    {
        try
        {
            int applies = 0;
            int undoes = 0;
            int tests = 0;
            Dictionary<string, string> mappings = BrowserFixture();
            using (var form = new BrowserRemoteLiteForm(delegate { return Clone(mappings); },
                delegate(BrowserRemotePlan plan)
                {
                    applies++;
                    mappings = plan.ApplyTo(mappings);
                    return ActionResult.Create("应用浏览器遥控", "浏览器 AI", ActionState.Success,
                        "按键服务已确认 Browser Remote Lite 配置", "", "", "");
                }, delegate
                {
                    undoes++;
                    return ActionResult.Create("撤销浏览器遥控", "浏览器 AI", ActionState.Success,
                        "按键服务已确认恢复应用前配置", "", "", "");
                }, delegate { return false; },
                delegate(string browser, string label, string action, Action<ActionResult> completion)
                {
                    tests++;
                    return ActionResult.Create("测试浏览器动作", browser, ActionState.Running,
                        label + "已准备，等待按键服务回执", "", "", "");
                }, delegate { }, false))
            {
                Require(form.AutoScaleMode == AutoScaleMode.Dpi,
                    "Browser Remote Lite form is not DPI-scaled");
                Panel scrollHost = Find(form, "browserRemoteScrollHost") as Panel;
                ListView diff = Find(form, "browserRemoteDiff") as ListView;
                ComboBox browser = Find(form, "browserRemoteBrowserChoice") as ComboBox;
                Button apply = Find(form, "browserRemoteApplyButton") as Button;
                Button undo = Find(form, "browserRemoteUndoButton") as Button;
                Button test = Find(form, "browserRemoteTestButton") as Button;
                Button close = Find(form, "browserRemoteCloseButton") as Button;
                Require(scrollHost != null && scrollHost.AutoScroll,
                    "Browser Remote Lite cannot scroll in a high-DPI small work area");
                Require(diff != null && diff.Columns.Count == 3 && diff.Items.Count == 7,
                    "Browser Remote Lite does not show the seven per-key differences");
                Require(browser != null && browser.Items.Count == 2 &&
                    browser.Items[0].ToString() == "Chrome" && browser.Items[1].ToString() == "Edge",
                    "Browser Remote Lite does not constrain tests to Chrome and Edge");
                Require(browser.AccessibleName == "逐项测试目标" &&
                    ((ComboBox)Find(form, "browserRemoteRightChoice")).AccessibleName ==
                        "右键推荐动作" &&
                    ((ComboBox)Find(form, "browserRemoteFunctionLongChoice")).AccessibleName ==
                        "功能键长按推荐动作",
                    "Browser Remote Lite choices do not expose distinct accessible names");
                Require(apply != null && apply.Enabled && undo != null && !undo.Enabled &&
                    test != null && close != null,
                    "Browser Remote Lite apply, undo, test, or close state is incorrect");
                Require(Find(form, "browserRemoteRightChoice") is ComboBox &&
                    Find(form, "browserRemoteFunctionLongChoice") is ComboBox,
                    "Browser Remote Lite configurable recommendations are missing");

                form.Show();
                diff.Items[0].Selected = true;
                diff.Select();
                Application.DoEvents();
                Require(test.Enabled, "Selecting a recommended action did not enable its test");
                test.PerformClick();
                Application.DoEvents();
                Require(tests == 1 && applies == 0 && undoes == 0,
                    "Individual testing modified the Profile or invoked undo");

                form.ApplyTheme(true);
                Require(form.BackColor == Color.FromArgb(25, 26, 31) &&
                    browser.BackColor == Color.FromArgb(31, 33, 39) &&
                    browser.ForeColor == Color.FromArgb(229, 232, 239),
                    "Browser Remote Lite did not apply the dark palette");

                form.Scale(new SizeF(2f, 2f));
                MethodInfo applyWorkingArea = typeof(BrowserRemoteLiteForm).GetMethod(
                    "ApplyWorkingAreaLayout", BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo fitWindow = typeof(BrowserRemoteLiteForm).GetMethod(
                    "FitWindowToWorkingArea", BindingFlags.Static | BindingFlags.NonPublic);
                Require(applyWorkingArea != null && fitWindow != null,
                    "Browser Remote Lite working-area layout hooks are missing");
                Size fitted = (Size)fitWindow.Invoke(null,
                    new object[] { new Size(1680, 1440), new Rectangle(0, 0, 1366, 728) });
                Require(fitted.Width <= 1342 && fitted.Height <= 704,
                    "Browser Remote Lite can exceed a 1366x768 work area");
                applyWorkingArea.Invoke(form, new object[] { new Rectangle(0, 0, 1366, 728) });
                Application.DoEvents();
                Require(form.Width <= 1342 && form.Height <= 704 &&
                    scrollHost.DisplayRectangle.Height > scrollHost.ClientSize.Height,
                    "Browser Remote Lite does not expose scrolling after 200% scaling");
                scrollHost.ScrollControlIntoView(close);
                Application.DoEvents();
                Point closeTopLeft = scrollHost.PointToClient(close.PointToScreen(Point.Empty));
                Require(closeTopLeft.Y >= 0 && closeTopLeft.Y + close.Height <= scrollHost.ClientSize.Height,
                    "Browser Remote Lite footer cannot be reached at 200% scaling");
                form.Close();
                Application.DoEvents();
            }

            Dictionary<string, string> recommended = BrowserProfileTemplate.CreatePlan(
                BrowserProfileTemplate.ProfileId, BrowserFixture(), "tab", "address-bar")
                .ApplyTo(BrowserFixture());
            using (var noOp = new BrowserRemoteLiteForm(delegate { return Clone(recommended); },
                null, null, delegate { return false; }, null, null, false))
            {
                Button apply = Find(noOp, "browserRemoteApplyButton") as Button;
                Button close = Find(noOp, "browserRemoteCloseButton") as Button;
                Require(apply != null && !apply.Enabled,
                    "No-op Browser Remote Lite plan left apply enabled");
                close.PerformClick();
                Require(applies == 0 && undoes == 0,
                    "Closing Browser Remote Lite changed Profile mappings");
            }

            Console.WriteLine("Browser Remote Lite UI tests passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Browser Remote Lite UI tests failed: " + ex.Message);
            return 1;
        }
    }

    private static Dictionary<string, string> BrowserFixture()
    {
        return new Dictionary<string, string>
        {
            { "确认键", "ctrl+s" },
            { "Home", "win+d" },
            { "Home:short", "win+d" },
            { "Home:long", "none" },
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

    private static Dictionary<string, string> Clone(IDictionary<string, string> source)
    {
        var clone = new Dictionary<string, string>();
        foreach (KeyValuePair<string, string> pair in source) clone[pair.Key] = pair.Value;
        return clone;
    }

    private static Control Find(Control root, string name)
    {
        if (root == null) return null;
        if (string.Equals(root.Name, name, StringComparison.Ordinal)) return root;
        foreach (Control child in root.Controls)
        {
            Control found = Find(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
