using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Forms;

internal static class CaptureAskGeometry
{
    internal static bool TryToRelativeSelection(Rectangle sourceBounds, Rectangle absoluteSelection,
        out Rectangle relativeSelection)
    {
        relativeSelection = Rectangle.Empty;
        if (sourceBounds.Width <= 0 || sourceBounds.Height <= 0 || absoluteSelection.Width <= 0 ||
            absoluteSelection.Height <= 0 || !sourceBounds.Contains(absoluteSelection)) return false;
        relativeSelection = new Rectangle(absoluteSelection.X - sourceBounds.X,
            absoluteSelection.Y - sourceBounds.Y, absoluteSelection.Width, absoluteSelection.Height);
        return true;
    }
}

internal sealed class CaptureAskForegroundStability
{
    private readonly int requiredSamples;
    private readonly long minimumStableMs;
    private IntPtr currentWindow;
    private int samples;
    private long firstSeenMs;

    internal CaptureAskForegroundStability(int requiredSamples, long minimumStableMs)
    {
        this.requiredSamples = Math.Max(1, requiredSamples);
        this.minimumStableMs = Math.Max(0, minimumStableMs);
    }

    internal bool Observe(IntPtr window, long elapsedMs)
    {
        if (window == IntPtr.Zero)
        {
            Reset();
            return false;
        }
        if (window != currentWindow)
        {
            currentWindow = window;
            samples = 1;
            firstSeenMs = elapsedMs;
            return requiredSamples <= 1 && minimumStableMs == 0;
        }
        samples++;
        return samples >= requiredSamples && elapsedMs - firstSeenMs >= minimumStableMs;
    }

    internal void Reset()
    {
        currentWindow = IntPtr.Zero;
        samples = 0;
        firstSeenMs = 0;
    }
}

internal sealed class CaptureAskFocusedElementEvidence
{
    public string ProcessName { get; set; }
    public string AutomationId { get; set; }
    public string ControlType { get; set; }
    public string ClassName { get; set; }
    public string ParentFingerprint { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsKeyboardFocusable { get; set; }
    public bool HasKeyboardFocus { get; set; }
    public bool IsOffscreen { get; set; }
    public bool IsPassword { get; set; }
    public bool IsWritable { get; set; }
}

internal static class CaptureAskFocusEvidence
{
    internal static bool Matches(FocusTargetDescriptor target, CaptureAskFocusedElementEvidence evidence)
    {
        if (target == null || evidence == null || !evidence.IsEnabled || !evidence.IsKeyboardFocusable ||
            !evidence.HasKeyboardFocus || evidence.IsOffscreen || evidence.IsPassword || !evidence.IsWritable)
            return false;
        if (!string.Equals(FocusTargetDescriptor.NormalizeProcessName(evidence.ProcessName),
            FocusTargetDescriptor.NormalizeProcessName(target.ProcessName), StringComparison.OrdinalIgnoreCase))
            return false;
        if (!string.Equals(evidence.ControlType, "Edit", StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.IsNullOrWhiteSpace(target.AutomationId) &&
            !string.Equals(evidence.AutomationId, target.AutomationId, StringComparison.Ordinal)) return false;
        if (!string.IsNullOrWhiteSpace(target.ClassName) &&
            !string.Equals(evidence.ClassName, target.ClassName, StringComparison.Ordinal)) return false;
        if (!FocusTargetService.MatchesStoredParentFingerprint(target,
                evidence.ParentFingerprint, evidence.ClassName)) return false;
        return true;
    }
}

internal sealed class CaptureAskScreenResult : IDisposable
{
    public bool IsSuccess { get; private set; }
    public string ErrorCode { get; private set; }
    public Bitmap Source { get; private set; }
    public Rectangle SourceBounds { get; private set; }
    public Rectangle SelectedBounds { get; private set; }

    private CaptureAskScreenResult() { }

    internal static CaptureAskScreenResult Success(Bitmap source, Rectangle sourceBounds,
        Rectangle selectedBounds)
    {
        return new CaptureAskScreenResult
        {
            IsSuccess = source != null,
            ErrorCode = source == null ? "CAPTURE-ASK-SCREEN-FAILED" : "",
            Source = source,
            SourceBounds = sourceBounds,
            SelectedBounds = selectedBounds
        };
    }

    internal static CaptureAskScreenResult Failure(string errorCode)
    {
        return new CaptureAskScreenResult
        {
            IsSuccess = false,
            ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? "CAPTURE-ASK-SCREEN-FAILED" : errorCode,
            Source = null,
            SourceBounds = Rectangle.Empty,
            SelectedBounds = Rectangle.Empty
        };
    }

    public void Dispose()
    {
        if (Source != null) Source.Dispose();
        Source = null;
    }
}

internal sealed class WindowsCaptureAskBackend
{
    private readonly object sync = new object();
    private readonly int processId;
    private IntPtr recentExternalWindow;

    internal WindowsCaptureAskBackend()
    {
        processId = Process.GetCurrentProcess().Id;
    }

    internal void ObserveForegroundWindow()
    {
        IntPtr candidate = GetForegroundWindow();
        if (!IsExternalCaptureWindow(candidate)) return;
        lock (sync) recentExternalWindow = candidate;
    }

    internal CaptureAskScreenResult CaptureRecentWindow(IntPtr excludedWindow, int timeoutMs,
        Func<bool> cancellationRequested)
    {
        IntPtr preferred;
        lock (sync) preferred = recentExternalWindow;
        if (IsExternalCaptureWindow(preferred) && preferred != excludedWindow)
        {
            try { SetForegroundWindow(preferred); } catch { }
        }

        IntPtr captureWindow;
        string waitError;
        if (!TryWaitForStableExternalForeground(excludedWindow, timeoutMs,
            cancellationRequested, out captureWindow, out waitError))
            return CaptureAskScreenResult.Failure(waitError);
        if (captureWindow == IntPtr.Zero)
            return CaptureAskScreenResult.Failure("CAPTURE-ASK-WINDOW-NOT-FOUND");

        Rectangle bounds;
        if (!TryGetCaptureBounds(captureWindow, out bounds))
            return CaptureAskScreenResult.Failure("CAPTURE-ASK-WINDOW-BOUNDS-FAILED");
        Bitmap image;
        if (!TryCaptureRectangle(bounds, out image))
            return CaptureAskScreenResult.Failure("CAPTURE-ASK-SCREEN-FAILED");
        lock (sync) recentExternalWindow = captureWindow;
        return CaptureAskScreenResult.Success(image, bounds,
            new Rectangle(0, 0, image.Width, image.Height));
    }

    internal CaptureAskScreenResult CaptureVirtualScreen(Func<bool> cancellationRequested)
    {
        IntPtr foreground;
        string waitError;
        if (!TryWaitForStableExternalForeground(IntPtr.Zero, 1800, cancellationRequested,
            out foreground, out waitError)) return CaptureAskScreenResult.Failure(waitError);
        Rectangle bounds = SystemInformation.VirtualScreen;
        Bitmap image;
        if (!TryCaptureRectangle(bounds, out image))
            return CaptureAskScreenResult.Failure("CAPTURE-ASK-SCREEN-FAILED");
        return CaptureAskScreenResult.Success(image, bounds,
            new Rectangle(0, 0, image.Width, image.Height));
    }

    internal bool IsFocusedTarget(FocusTargetDescriptor target)
    {
        try
        {
            AutomationElement element = AutomationElement.FocusedElement;
            if (element == null) return false;
            AutomationElement.AutomationElementInformation current = element.Current;
            string currentProcess = "";
            try
            {
                using (Process process = Process.GetProcessById(current.ProcessId))
                    currentProcess = process.ProcessName;
            }
            catch { return false; }
            var evidence = new CaptureAskFocusedElementEvidence
            {
                ProcessName = currentProcess,
                AutomationId = current.AutomationId ?? "",
                ControlType = current.ControlType == null ? "" :
                    current.ControlType.ProgrammaticName.Replace("ControlType.", ""),
                ClassName = current.ClassName ?? "",
                ParentFingerprint = WindowsUiaFocusAutomationBackend.BuildParentFingerprint(element),
                IsEnabled = current.IsEnabled,
                IsKeyboardFocusable = current.IsKeyboardFocusable,
                HasKeyboardFocus = current.HasKeyboardFocus,
                IsOffscreen = current.IsOffscreen,
                IsPassword = current.IsPassword,
                IsWritable = WindowsUiaFocusAutomationBackend.HasWritableEditablePattern(element)
            };
            return CaptureAskFocusEvidence.Matches(target, evidence);
        }
        catch (ElementNotAvailableException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
        catch (InvalidOperationException) { return false; }
        catch (COMException) { return false; }
    }

    private bool IsExternalCaptureWindow(IntPtr window)
    {
        if (window == IntPtr.Zero || !IsWindowVisible(window) || IsIconic(window)) return false;
        uint ownerProcessId;
        GetWindowThreadProcessId(window, out ownerProcessId);
        return ownerProcessId != 0 && ownerProcessId != (uint)processId;
    }

    private bool TryWaitForStableExternalForeground(IntPtr excludedWindow, int timeoutMs,
        Func<bool> cancellationRequested, out IntPtr stableWindow, out string errorCode)
    {
        stableWindow = IntPtr.Zero;
        errorCode = "CAPTURE-ASK-WINDOW-NOT-FOUND";
        var timer = Stopwatch.StartNew();
        var stability = new CaptureAskForegroundStability(3, 160);
        while (timer.ElapsedMilliseconds < Math.Max(1, timeoutMs))
        {
            if (Canceled(cancellationRequested))
            {
                errorCode = "CAPTURE-ASK-CANCELED-VOICE";
                return false;
            }
            IntPtr foreground = GetForegroundWindow();
            if (foreground == excludedWindow || !IsExternalCaptureWindow(foreground))
            {
                stability.Reset();
                Thread.Sleep(20);
                continue;
            }
            if (!stability.Observe(foreground, timer.ElapsedMilliseconds))
            {
                Thread.Sleep(20);
                continue;
            }
            if (!TryFlushDesktop() || GetForegroundWindow() != foreground)
            {
                stability.Reset();
                Thread.Sleep(20);
                continue;
            }
            stableWindow = foreground;
            errorCode = "";
            return true;
        }
        return false;
    }

    private static bool TryFlushDesktop()
    {
        try { return DwmFlush() == 0 && DwmFlush() == 0; }
        catch (DllNotFoundException) { return false; }
        catch (EntryPointNotFoundException) { return false; }
    }

    private static bool TryGetCaptureBounds(IntPtr window, out Rectangle bounds)
    {
        bounds = Rectangle.Empty;
        NativeRectangle native;
        int result = -1;
        try { result = DwmGetWindowAttribute(window, 9, out native, Marshal.SizeOf(typeof(NativeRectangle))); }
        catch (DllNotFoundException) { native = new NativeRectangle(); }
        catch (EntryPointNotFoundException) { native = new NativeRectangle(); }
        if (result != 0 && !GetWindowRect(window, out native)) return false;
        bounds = Rectangle.FromLTRB(native.Left, native.Top, native.Right, native.Bottom);
        Rectangle visible = Rectangle.Intersect(bounds, SystemInformation.VirtualScreen);
        if (visible.Width < 2 || visible.Height < 2 || visible.Width > 32768 || visible.Height > 32768)
            return false;
        bounds = visible;
        return true;
    }

    private static bool TryCaptureRectangle(Rectangle bounds, out Bitmap image)
    {
        image = null;
        if (bounds.Width < 1 || bounds.Height < 1) return false;
        try
        {
            image = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(image))
                graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);
            return true;
        }
        catch
        {
            if (image != null) image.Dispose();
            image = null;
            return false;
        }
    }

    private static bool Canceled(Func<bool> cancellationRequested)
    {
        try { return cancellationRequested != null && cancellationRequested(); }
        catch { return true; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRectangle
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr window);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr window, out NativeRectangle rectangle);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr window, int attribute,
        out NativeRectangle value, int valueSize);

    [DllImport("dwmapi.dll")]
    private static extern int DwmFlush();
}

internal sealed class WindowsCaptureAskImageClipboard : ICaptureAskImageClipboard
{
    public bool TrySetImage(Bitmap image, out string errorCode)
    {
        if (image == null)
        {
            errorCode = "CAPTURE-ASK-IMAGE-MISSING";
            return false;
        }
        try
        {
            Clipboard.SetImage(image);
            errorCode = "";
            return true;
        }
        catch
        {
            errorCode = "CAPTURE-ASK-CLIPBOARD-FAILED";
            return false;
        }
    }
}

internal sealed class WindowsCaptureAskPasteDispatcher : ICaptureAskPasteDispatcher
{
    public bool TryPasteImage(out string errorCode)
    {
        bool controlDown = false;
        bool vDown = false;
        try
        {
            keybd_event(0x11, 0x1D, 0, UIntPtr.Zero);
            controlDown = true;
            keybd_event(0x56, 0x2F, 0, UIntPtr.Zero);
            vDown = true;
            keybd_event(0x56, 0x2F, 0x0002, UIntPtr.Zero);
            vDown = false;
            keybd_event(0x11, 0x1D, 0x0002, UIntPtr.Zero);
            controlDown = false;
            errorCode = "";
            return true;
        }
        catch
        {
            errorCode = "CAPTURE-ASK-PASTE-FAILED";
            return false;
        }
        finally
        {
            if (vDown) try { keybd_event(0x56, 0x2F, 0x0002, UIntPtr.Zero); } catch { }
            if (controlDown) try { keybd_event(0x11, 0x1D, 0x0002, UIntPtr.Zero); } catch { }
        }
    }

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);
}
