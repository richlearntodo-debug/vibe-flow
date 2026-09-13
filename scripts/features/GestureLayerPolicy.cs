using System;
using System.Runtime.InteropServices;

// Remote gesture layering: one physical key can carry three layers — short press, long
// press and double tap. This is pure decision logic so it can be verified without a
// remote: a caller feeds the measured hold time and whether the previous tap happened
// inside the double-tap window, and gets back which layer fired. Binding lookup falls
// back from double to long to short, so a partially configured key still works.
internal enum GestureKind
{
    Short,
    Long,
    Double
}

internal sealed class GestureBinding
{
    public string Key { get; set; }
    public string ShortAction { get; set; }
    public string LongAction { get; set; }
    public string DoubleAction { get; set; }
}

internal static class GestureLayerPolicy
{
    // A press at or above this duration is a long press.
    internal const int LongPressMs = 650;
    // The window is measured release-to-release, and the platform default it is compared
    // against is the user's own double-click speed rather than a number invented here.
    //
    // Measured on a real RC003 over BLE, a hard-coded window failed in BOTH directions: at
    // 320 ms a natural double tap measured 378 ms apart and was rejected as two separate
    // short presses, while asking for faster taps produced taps SO short that the remote
    // never reported the press at all (the log held two key-up edges and no key-down, so no
    // gesture could be formed). Tying the window to GetDoubleClickTime() means the remote
    // follows the double-click speed the user already tuned in Windows; the floor keeps it
    // from ever being tighter than the 500 ms platform default.
    internal const int DoubleTapWindowFloorMs = 500;
    private const int DoubleTapWindowCeilingMs = 900;
    private static readonly int doubleTapWindowMs = ReadDoubleTapWindow();

    internal static int DoubleTapWindowMs { get { return doubleTapWindowMs; } }

    [DllImport("user32.dll")]
    private static extern int GetDoubleClickTime();

    private static int ReadDoubleTapWindow()
    {
        int system;
        try { system = GetDoubleClickTime(); }
        catch { system = 0; }
        if (system < DoubleTapWindowFloorMs) return DoubleTapWindowFloorMs;
        return system > DoubleTapWindowCeilingMs ? DoubleTapWindowCeilingMs : system;
    }

    internal static GestureKind Classify(int holdMs, bool previousTapWithinWindow)
    {
        if (holdMs >= LongPressMs) return GestureKind.Long;
        if (previousTapWithinWindow) return GestureKind.Double;
        return GestureKind.Short;
    }

    internal static string NormalizeAction(string action)
    {
        return string.IsNullOrWhiteSpace(action) ? "" : action.Trim();
    }

    // Returns the action for the fired layer, or the nearest configured layer. A key with
    // no binding at all returns an empty action so the caller can report it honestly.
    internal static string ActionFor(GestureBinding binding, GestureKind kind)
    {
        if (binding == null) return "";
        string doubleAction = NormalizeAction(binding.DoubleAction);
        string longAction = NormalizeAction(binding.LongAction);
        string shortAction = NormalizeAction(binding.ShortAction);
        switch (kind)
        {
            case GestureKind.Double:
                if (doubleAction.Length > 0) return doubleAction;
                if (longAction.Length > 0) return longAction;
                return shortAction;
            case GestureKind.Long:
                if (longAction.Length > 0) return longAction;
                return shortAction;
            default:
                return shortAction;
        }
    }

    // Human readable layer name for toasts and cards ("短按" / "长按" / "双击").
    internal static string Describe(GestureKind kind)
    {
        switch (kind)
        {
            case GestureKind.Long: return "长按";
            case GestureKind.Double: return "双击";
            default: return "短按";
        }
    }

    // True when the layer itself has its own binding (used to tell the user that a double
    // tap is falling back to another layer instead of silently doing nothing different).
    internal static bool HasOwnBinding(GestureBinding binding, GestureKind kind)
    {
        if (binding == null) return false;
        switch (kind)
        {
            case GestureKind.Double: return NormalizeAction(binding.DoubleAction).Length > 0;
            case GestureKind.Long: return NormalizeAction(binding.LongAction).Length > 0;
            default: return NormalizeAction(binding.ShortAction).Length > 0;
        }
    }
}
