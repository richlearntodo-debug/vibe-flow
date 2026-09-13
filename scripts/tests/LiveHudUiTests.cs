using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

internal static class LiveHudUiTests
{
    [STAThread]
    private static int Main()
    {
        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (var owner = new Form())
            using (var hud = new LiveHudForm())
            using (var deck = new ContextDeckForm())
            {
                const int wsExNoActivate = 0x08000000;
                PropertyInfo deckShowWithoutActivation = typeof(ContextDeckForm).GetProperty(
                    "ShowWithoutActivation", BindingFlags.Instance | BindingFlags.NonPublic);
                PropertyInfo deckCreateParams = typeof(ContextDeckForm).GetProperty(
                    "CreateParams", BindingFlags.Instance | BindingFlags.NonPublic);
                CreateParams deckParameters = deckCreateParams == null ? null :
                    deckCreateParams.GetValue(deck, null) as CreateParams;
                Require(deckShowWithoutActivation != null &&
                    Convert.ToBoolean(deckShowWithoutActivation.GetValue(deck, null)) &&
                    deckParameters != null && (deckParameters.ExStyle & wsExNoActivate) != 0,
                    "Context Deck lacks the native non-activating window contract");
                hud.PrepareInactiveHandle();
                RequireNativeTree(hud);
                Require(DurationFor(ActionState.Checking) == 0 &&
                    DurationFor(ActionState.Running) == 0,
                    "Live HUD auto-hides an operation that is still running");
                Require(DurationFor(ActionState.Success) == 2800 &&
                    DurationFor(ActionState.Warning) == 12000,
                    "Live HUD completion durations changed unexpectedly");
                owner.Text = "Live HUD focus owner";
                owner.Show();
                owner.Activate();
                SetForegroundWindow(owner.Handle);
                Pump(80);
                IntPtr before = GetForegroundWindow();
                hud.ApplySnapshot(Snapshot());
                hud.ShowInactive(null);
                Pump(80);
                IntPtr after = GetForegroundWindow();
                Console.WriteLine("Live HUD focus probe: owner=" + owner.Handle +
                    " hud=" + hud.Handle + " before=" + before + " after=" + after);
                Require(hud.IsPresented, "Live HUD did not become natively visible");
                Require(after != hud.Handle, "Live HUD became the foreground window");
                if (before == owner.Handle)
                    Require(after == before, "Live HUD changed a controlled foreground owner");
                hud.HideInactive();

                owner.Activate();
                SetForegroundWindow(owner.Handle);
                Pump(80);
                before = GetForegroundWindow();
                deck.ShowInactive(owner);
                Pump(80);
                after = GetForegroundWindow();
                Require(after != deck.Handle, "Context Deck became the foreground window");
                if (before == owner.Handle)
                    Require(after == before, "Context Deck changed a controlled foreground owner");
                deck.Hide();
            }
            Console.WriteLine("Live HUD UI tests passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Live HUD UI tests failed: " + ex.Message);
            return 1;
        }
    }

    private static VibeUiStatusSnapshot Snapshot()
    {
        return Snapshot(ActionState.Warning);
    }

    private static VibeUiStatusSnapshot Snapshot(ActionState state)
    {
        return VibeUiStatusSnapshot.Create("设备已连接", "语音桥接就绪", "通用导航",
            "VibeMic", "Cursor Chat", "未进入项目", "", false,
            ActionResult.Create("截图提问", "Cursor Chat", state,
                "截图动作已派发，请按住录音键描述问题", "",
                "检查目标后手动确认发送", "CAPTURE-ASK-VISUAL-CONFIRMATION"),
            new Dictionary<string, string>());
    }

    private static int DurationFor(ActionState state)
    {
        MethodInfo method = typeof(VibeMicForm).GetMethod("LiveHudDurationMilliseconds",
            BindingFlags.Static | BindingFlags.NonPublic);
        Require(method != null, "Live HUD duration policy is unavailable");
        return (int)method.Invoke(null, new object[] { Snapshot(state) });
    }

    private static void Pump(int milliseconds)
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(milliseconds);
        while (DateTime.UtcNow < deadline)
        {
            Application.DoEvents();
            Thread.Sleep(5);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void RequireNativeTree(Control root)
    {
        Require(root.IsHandleCreated,
            "Live HUD did not prepare a native handle for " + root.GetType().Name);
        foreach (Control child in root.Controls) RequireNativeTree(child);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr window);
}
