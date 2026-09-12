# Geometry check for the shipped pages: opens the UI in smoke mode, walks every page and reports
# sibling controls whose rectangles intersect.
#
# Why siblings: a nested control legitimately sits inside its parent's rectangle, so a blanket
# "any two controls must not overlap" rule reports hundreds of false positives. Two text-bearing
# controls that share a parent and still intersect are the real defect class — that is what a
# user sees as cramped or colliding text, and it is how the voice page's CABLE status line was
# found overlapping the endpoint line below it by 6 px at 100% DPI.
#
# One implementation trap, worth keeping in mind when editing this: EnumChildWindows already
# enumerates every descendant, not just the direct children, so recursing into each returned
# handle counts every control once per ancestor and multiplies the reported overlaps.
#
# Run it locally; CI has no interactive desktop to show a window on:
#   powershell -File scripts\check-ui-geometry.ps1 -Exe .\VibeMic.exe -OutDir $env:TEMP\geom
param(
    # Required only when this script starts the application; measuring a running one does not need it.
    [string]$Exe = "",
    [Parameter(Mandatory = $true)][string]$OutDir,
    [int]$TimeoutMilliseconds = 25000,
    # Optional "WxH". A small screen and a high scaling factor both end up as a window that cannot be
    # as large as the layout assumes, so the geometry can be measured against a forced size instead of
    # only against whatever this machine's display happens to allow.
    [string]$ForceSize = "",
    # Measure an application that is already running instead of starting a smoke instance. Needed for
    # anything the smoke path cannot show: smoke mode runs on its own configuration, so the theme matrix
    # has to look at the installed application, which reads the user's own configuration.
    [int]$ProcessId = 0,
    # Measure one window of that process by title instead of sweeping the six pages. Used for the dialogs,
    # which are opened by the flow being driven and are not pages of the main window.
    [string]$WindowTitle = "",
    # Start the application in a specific theme. The theme matrix needs this: the theme used to be
    # reachable only by editing a configuration file, which is exactly why the dark theme shipped unable to
    # start — no automated run had ever launched it.
    [string]$Theme = "",
    # What to pass the application when this script starts it.
    [string]$ExeArguments = "--ui-smoke",
    # Measure one window by handle instead of by title. Some surfaces have no title at all — measured, the Live
    # HUD is a borderless window with empty text, so it could not be named by title and was written off as
    # needing hardware until a session was started and the window appeared.
    [long]$WindowHandle = 0
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

Add-Type -TypeDefinition @'
using System;
using System.Text;
using System.Runtime.InteropServices;

public static class DpiNative
{
    public delegate bool WindowCallback(IntPtr handle, IntPtr parameter);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr parent, WindowCallback callback, IntPtr parameter);
    [DllImport("user32.dll")] public static extern bool EnumWindows(WindowCallback callback, IntPtr parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr handle, StringBuilder text, int maximum);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr handle, StringBuilder text, int maximum);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr handle, out RECT rectangle);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr handle, out RECT rectangle);
    [DllImport("user32.dll")] public static extern IntPtr GetParent(IntPtr handle);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr handle, IntPtr deviceContext, uint flags);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr handle);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr handle);
    // The checker has to be DPI aware itself. Windows virtualizes window rectangles for an unaware
    // process, so at 150% this script read a 1920x1260 window as 1280x840 and, worse, allocated its
    // PrintWindow bitmap from the virtualized size — the 200% capture showed only the top-left quarter
    // of the window. Declaring awareness makes both the measurements and the captures physical.
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr handle, IntPtr after, int x, int y, int width, int height, uint flags);
    // Clipping detection: a label that is not auto-sized keeps whatever box it was given, and text
    // wider than that box is silently cut off. Overlap detection cannot see this, so the rendered text
    // extent is measured against the control's own client width.
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr handle);
    [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr handle, IntPtr deviceContext);
    [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr deviceContext, IntPtr handle);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] public static extern bool GetTextExtentPoint32W(IntPtr deviceContext, string text, int length, out SIZE size);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] public static extern int DrawTextW(IntPtr deviceContext, string text, int length, ref RECT rectangle, uint format);
    [DllImport("gdi32.dll")] public static extern IntPtr CreateFontIndirectW(ref LOGFONT logFont);
    [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr handle);
    [DllImport("user32.dll")] public static extern bool SystemParametersInfoW(uint action, uint param, ref NONCLIENTMETRICS metrics, uint flags);
    [StructLayout(LayoutKind.Sequential)] public struct SIZE { public int cx; public int cy; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct LOGFONT
    {
        public int lfHeight; public int lfWidth; public int lfEscapement; public int lfOrientation;
        public int lfWeight; public byte lfItalic; public byte lfUnderline; public byte lfStrikeOut;
        public byte lfCharSet; public byte lfOutPrecision; public byte lfClipPrecision;
        public byte lfQuality; public byte lfPitchAndFamily;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string lfFaceName;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct NONCLIENTMETRICS
    {
        public int cbSize; public int iBorderWidth; public int iScrollWidth; public int iScrollHeight;
        public int iCaptionWidth; public int iCaptionHeight; public LOGFONT lfCaptionFont;
        public int iSmCaptionWidth; public int iSmCaptionHeight; public LOGFONT lfSmCaptionFont;
        public int iMenuWidth; public int iMenuHeight; public LOGFONT lfMenuFont;
        public LOGFONT lfStatusFont; public LOGFONT lfMessageFont;
        public int iPaddedBorderWidth;
    }
}
'@

# Declared before any window of this process is created, so the measurements and the captures below are
# in physical pixels. Per-monitor-v2 first; system awareness is the fallback for older Windows.
try { [void][DpiNative]::SetProcessDpiAwarenessContext([IntPtr](-4)) } catch { }
try { [void][DpiNative]::SetProcessDPIAware() } catch { }

# How tall must this control be to show its text at its current width?
#
# Measuring a single line's width is not enough, and it is actively misleading: a paragraph label is
# allowed to be narrower than its text because it wraps, so a width comparison flagged every wrapped
# paragraph (measured: a 2701 px sentence in an 880 px label, which renders fine over several lines).
# What matters is whether the text fits the box it was given for the width it has, which is exactly
# DrawText with DT_CALCRECT and word breaking. WM_GETFONT is not reliable on Windows Forms controls
# (measured: it returns 0 for a Label), so the shell's message font stands in when it is unavailable,
# which keeps the height estimate close and the result an approximation — hence the tolerance.
function Get-TextHeight([IntPtr]$Handle, [string]$Text, [int]$Width) {
    if ([string]::IsNullOrWhiteSpace($Text) -or $Width -le 0) { return 0 }
    $font = [DpiNative]::SendMessage($Handle, 0x0031, [IntPtr]::Zero, [IntPtr]::Zero)   # WM_GETFONT
    $created = [IntPtr]::Zero
    if ($font -eq [IntPtr]::Zero) {
        $metrics = New-Object DpiNative+NONCLIENTMETRICS
        $metrics.cbSize = [Runtime.InteropServices.Marshal]::SizeOf([type][DpiNative+NONCLIENTMETRICS])
        if ([DpiNative]::SystemParametersInfoW(0x0029, [uint32]$metrics.cbSize, [ref]$metrics, 0)) {  # SPI_GETNONCLIENTMETRICS
            $created = [DpiNative]::CreateFontIndirectW([ref]$metrics.lfMessageFont)
            $font = $created
        }
    }
    if ($font -eq [IntPtr]::Zero) { return 0 }
    $deviceContext = [DpiNative]::GetDC($Handle)
    if ($deviceContext -eq [IntPtr]::Zero) {
        if ($created -ne [IntPtr]::Zero) { [void][DpiNative]::DeleteObject($created) }
        return 0
    }
    $previous = [DpiNative]::SelectObject($deviceContext, $font)
    try {
        $rectangle = New-Object DpiNative+RECT
        $rectangle.L = 0; $rectangle.T = 0; $rectangle.R = $Width; $rectangle.B = 0
        # DT_CALCRECT 0x0400 | DT_WORDBREAK 0x0010 | DT_NOPREFIX 0x0800
        [void][DpiNative]::DrawTextW($deviceContext, $Text, $Text.Length, [ref]$rectangle, 0x00000410 -bor 0x00000800)
        return ($rectangle.B - $rectangle.T)
    }
    finally {
        [void][DpiNative]::SelectObject($deviceContext, $previous)
        [void][DpiNative]::ReleaseDC($Handle, $deviceContext)
        if ($created -ne [IntPtr]::Zero) { [void][DpiNative]::DeleteObject($created) }
    }
}

# A control whose text does not fit the box it was given: it either needs more height at that width
# (word-wrapped text that was cut off, or a single line that the control is too narrow for) or it is
# taller than the space it was allocated.
function Get-ClippedControls([IntPtr]$Root) {
    $clipped = New-Object System.Collections.ArrayList
    foreach ($handle in (Get-AllDescendants $Root)) {
        if (-not [DpiNative]::IsWindowVisible($handle)) { continue }
        $text = Get-WindowText $handle
        if ([string]::IsNullOrWhiteSpace($text)) { continue }
        $client = New-Object DpiNative+RECT
        [DpiNative]::GetClientRect($handle, [ref]$client) | Out-Null
        $width = $client.Right - $client.Left
        $height = $client.B - $client.T
        if ($width -le 0 -or $height -le 0) { continue }
        $needed = Get-TextHeight $handle $text $width
        if ($needed -gt ($height + 3)) {
            [void]$clipped.Add(("    [{0}] needs about {1}px of height at {2}px wide, has {3}px  class={4}" -f
                $text, $needed, $width, $height, (Get-ClassName $handle)))
        }
    }
    return $clipped
}

function Get-WindowText([IntPtr]$Handle) {
    $text = New-Object System.Text.StringBuilder 512
    [DpiNative]::GetWindowText($Handle, $text, $text.Capacity) | Out-Null
    return $text.ToString()
}
function Get-ClassName([IntPtr]$Handle) {
    $text = New-Object System.Text.StringBuilder 256
    [DpiNative]::GetClassName($Handle, $text, $text.Capacity) | Out-Null
    return $text.ToString()
}
# Chinese literals come from code points so this file can stay pure ASCII: Windows PowerShell
# reads a BOM-less script as ANSI and would mangle a literal.
function ConvertFrom-CodePoints([int[]]$CodePoints) { return -join ($CodePoints | ForEach-Object { [char]$_ }) }

# EnumChildWindows already enumerates every descendant, not just the direct children, so a single
# call per root is the whole tree; recursing into each result counted every control once per ancestor.
function Get-AllDescendants([IntPtr]$Parent) {
    $accumulator = New-Object System.Collections.Generic.List[IntPtr]
    $callback = [DpiNative+WindowCallback]{
        param([IntPtr]$handle, [IntPtr]$parameter)
        [void]$accumulator.Add($handle)
        return $true
    }
    [DpiNative]::EnumChildWindows($Parent, $callback, [IntPtr]::Zero) | Out-Null
    return $accumulator
}
function Get-Rect([IntPtr]$Handle) {
    $rectangle = New-Object DpiNative+RECT
    [DpiNative]::GetWindowRect($Handle, [ref]$rectangle) | Out-Null
    return $rectangle
}
function Find-TopWindow([int]$ProcessId, [string]$Title, [bool]$Prefix) {
    # The largest matching visible window, not the last one enumerated.
    #
    # Assigning on every match meant the enumeration order decided, and the application has small hidden-ish helpers
    # whose titles also begin with 言灵: measured, this function returned a 416x217 window and the report said the
    # interface was that size, while the real main window was 1600x1050. Area is the discriminator, and the chosen
    # window is written to the verbose stream so the instrument says what it measured.
    $script:foundWindow = [IntPtr]::Zero
    $script:foundArea = 0
    $callback = [DpiNative+WindowCallback]{
        param([IntPtr]$handle, [IntPtr]$parameter)
        [uint32]$owner = 0
        [DpiNative]::GetWindowThreadProcessId($handle, [ref]$owner) | Out-Null
        if ($owner -eq $ProcessId) {
            $text = Get-WindowText $handle
            $hit = if ($Prefix) { $text.StartsWith($Title) } else { $text -eq $Title }
            if ($hit -and [DpiNative]::IsWindowVisible($handle)) {
                $rectangle = Get-Rect $handle
                $area = ($rectangle.Right - $rectangle.Left) * ($rectangle.Bottom - $rectangle.Top)
                if ($area -gt $script:foundArea) {
                    $script:foundArea = $area
                    $script:foundWindow = $handle
                }
            }
        }
        return $true
    }
    [DpiNative]::EnumWindows($callback, [IntPtr]::Zero) | Out-Null
    return $script:foundWindow
}
function Wait-ForTopWindow([int]$ProcessId, [string]$Title, [bool]$Prefix) {
    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
    do {
        $window = Find-TopWindow $ProcessId $Title $Prefix
        if ($window -ne [IntPtr]::Zero) { return $window }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Window not found: $Title"
}
function Save-Window([IntPtr]$Handle, [string]$Path) {
    $rectangle = Get-Rect $Handle
    $width = $rectangle.Right - $rectangle.Left
    $height = $rectangle.Bottom - $rectangle.Top
    if ($width -lt 100 -or $height -lt 100) { throw "Window is not visible: $width x $height" }
    $bitmap = New-Object System.Drawing.Bitmap $width, $height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $deviceContext = $graphics.GetHdc()
    try { [void][DpiNative]::PrintWindow($Handle, $deviceContext, 2) }
    finally { $graphics.ReleaseHdc($deviceContext) }
    $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
    return "$width x $height"
}

# Two text-bearing siblings that overlap are the visible defect class this checks for: nested
# parent/child overlap is normal, siblings must not collide.
function Get-SiblingOverlaps([IntPtr]$Root) {
    $byParent = @{}
    foreach ($handle in (Get-AllDescendants $Root)) {
        if (-not [DpiNative]::IsWindowVisible($handle)) { continue }
        $text = Get-WindowText $handle
        if ([string]::IsNullOrWhiteSpace($text)) { continue }
        $rectangle = Get-Rect $handle
        if (($rectangle.Right - $rectangle.Left) -le 0 -or ($rectangle.Bottom - $rectangle.Top) -le 0) { continue }
        $parent = [DpiNative]::GetParent($handle)
        $key = $parent.ToInt64()
        if (-not $byParent.ContainsKey($key)) { $byParent[$key] = New-Object System.Collections.ArrayList }
        [void]$byParent[$key].Add([pscustomobject]@{
            Handle = $handle; Text = $text; Class = (Get-ClassName $handle); Rect = $rectangle })
    }
    $overlaps = New-Object System.Collections.ArrayList
    foreach ($key in $byParent.Keys) {
        $items = $byParent[$key]
        for ($i = 0; $i -lt $items.Count; $i++) {
            for ($j = $i + 1; $j -lt $items.Count; $j++) {
                $a = $items[$i]; $b = $items[$j]
                $overlapX = [Math]::Min($a.Rect.Right, $b.Rect.Right) - [Math]::Max($a.Rect.Left, $b.Rect.Left)
                $overlapY = [Math]::Min($a.Rect.Bottom, $b.Rect.Bottom) - [Math]::Max($a.Rect.Top, $b.Rect.Top)
                if ($overlapX -gt 2 -and $overlapY -gt 2) {
                    [void]$overlaps.Add(("    [{0}] x [{1}]  {2}x{3}px" -f $a.Text, $b.Text, $overlapX, $overlapY))
                }
            }
        }
    }
    return $overlaps
}

$windowPrefix = "Vibe"   # product name: matches "Vibe Link" (and older "Vibe Flow") within this process only
$pages = @(
    @{ Name = "01-home";       Label = (ConvertFrom-CodePoints @(0x9996, 0x9875)) },
    @{ Name = "02-workflow";   Label = (ConvertFrom-CodePoints @(0x5DE5, 0x4F5C, 0x6D41)) },
    @{ Name = "03-shortcuts";  Label = (ConvertFrom-CodePoints @(0x5FEB, 0x6377, 0x952E)) },
    @{ Name = "04-voice";      Label = (ConvertFrom-CodePoints @(0x8BED, 0x97F3)) },
    @{ Name = "05-diagnostics";Label = (ConvertFrom-CodePoints @(0x81EA, 0x68C0)) },
    @{ Name = "06-settings";   Label = (ConvertFrom-CodePoints @(0x8BBE, 0x7F6E)) }
)

$process = $null
if ($ProcessId -le 0) {
    if ([string]::IsNullOrWhiteSpace($Exe)) {
        throw "Pass -Exe to start a smoke instance, or -ProcessId to measure one that is already running."
    }
    $launchArguments = $ExeArguments
    if (-not [string]::IsNullOrWhiteSpace($Theme)) { $launchArguments = ($launchArguments + " --ui-theme " + $Theme).Trim() }
    $process = Start-Process -FilePath $Exe -ArgumentList $launchArguments -PassThru
}
$report = New-Object System.Collections.ArrayList
$startedByUs = $false
try {
    if ($ProcessId -gt 0) {
        # An application that is already running: measured as it is, and deliberately not stopped at the
        # end — it belongs to the user, not to this script.
        $main = Wait-ForTopWindow $ProcessId $windowPrefix $true
        $startedByUs = $false
    }
    else {
        $main = Wait-ForTopWindow $process.Id $windowPrefix $true
        $startedByUs = $true
    }
    Start-Sleep -Seconds 3
    if (-not [string]::IsNullOrWhiteSpace($ForceSize)) {
        $parts = $ForceSize -split 'x'
        if ($parts.Count -eq 2) {
            $forcedWidth = [int]$parts[0]
            $forcedHeight = [int]$parts[1]
            [void][DpiNative]::SetWindowPos($main, [IntPtr]::Zero, 0, 0, $forcedWidth, $forcedHeight, 0x0004)
            Start-Sleep -Seconds 2
        }
    }
    $mainRect = Get-Rect $main
    $dpi = [DpiNative]::GetDpiForWindow($main)
    [void]$report.Add("window=" + ($mainRect.Right - $mainRect.Left) + "x" + ($mainRect.Bottom - $mainRect.Top) +
        "  dpi=" + $dpi + "  scale=" + [Math]::Round($dpi / 96.0, 2))
    if ($WindowHandle -gt 0) {
        # Measured by handle: the surfaces without a title cannot be named, and measuring the first window whose
        # text starts with an empty string would just measure the main window.
        $target = [IntPtr]::new($WindowHandle)
        $dialogRect = Get-Rect $target
        [void]$report.Add("dialog=handle:" + $WindowHandle + "  size=" + ($dialogRect.Right - $dialogRect.Left) + "x" +
            ($dialogRect.Bottom - $dialogRect.Top))
        $overlaps = Get-SiblingOverlaps $target
        [void]$report.Add("dialog: overlaps=" + $overlaps.Count)
        foreach ($line in $overlaps) { [void]$report.Add($line) }
        $clipped = Get-ClippedControls $target
        [void]$report.Add("dialog: clipped=" + $clipped.Count)
        foreach ($line in $clipped) { [void]$report.Add($line) }
        $size = Save-Window $target (Join-Path $OutDir "dialog.png")
        [void]$report.Add("    captured " + $size)
    }
    elseif (-not [string]::IsNullOrWhiteSpace($WindowTitle)) {
        # One window, measured and captured: the dialogs are not pages, and the risk being checked is that a
        # dialog is scaled twice (its contents existed before Windows Forms' autoscale) or not at all.
        #
        # Three things this search used to get wrong, all of which produced a wrong measurement: it did not filter by
        # process, it stopped at the first match instead of taking the largest, and it enumerated only top-level
        # windows. Measured: asked for 言灵 it returned a 416x217 window that belonged to another process while the
        # application's real window was 1600x1050. A process id and an area comparison fix all three.
        $script:foundWindow = [IntPtr]::Zero
        $script:foundArea = 0
        $callback = [DpiNative+WindowCallback]{
            param([IntPtr]$handle, [IntPtr]$parameter)
            [uint32]$owner = 0
            [DpiNative]::GetWindowThreadProcessId($handle, [ref]$owner) | Out-Null
            if ($ProcessId -gt 0 -and $owner -ne $ProcessId) { return $true }
            if ([DpiNative]::IsWindowVisible($handle) -and
                (Get-WindowText $handle).StartsWith($WindowTitle, [StringComparison]::Ordinal)) {
                $rectangle = Get-Rect $handle
                $area = ($rectangle.Right - $rectangle.Left) * ($rectangle.Bottom - $rectangle.Top)
                if ($area -gt $script:foundArea) {
                    $script:foundArea = $area
                    $script:foundWindow = $handle
                }
            }
            return $true
        }
        [void][DpiNative]::EnumWindows($callback, [IntPtr]::Zero)
        $target = $script:foundWindow
        if ($target -eq [IntPtr]::Zero) {
            [void]$report.Add("dialog '" + $WindowTitle + "': window not found")
        }
        else {
            $dialogRect = Get-Rect $target
            [void]$report.Add("dialog=" + $WindowTitle + "  size=" + ($dialogRect.Right - $dialogRect.Left) + "x" +
                ($dialogRect.Bottom - $dialogRect.Top))
            $overlaps = Get-SiblingOverlaps $target
            [void]$report.Add("dialog: overlaps=" + $overlaps.Count)
            foreach ($line in $overlaps) { [void]$report.Add($line) }
            $clipped = Get-ClippedControls $target
            [void]$report.Add("dialog: clipped=" + $clipped.Count)
            foreach ($line in $clipped) { [void]$report.Add($line) }
            $size = Save-Window $target (Join-Path $OutDir "dialog.png")
            [void]$report.Add("    captured " + $size)
        }
    }
    else {
    foreach ($page in $pages) {
        $nav = [IntPtr]::Zero
        foreach ($handle in (Get-AllDescendants $main)) {
            if ((Get-WindowText $handle) -eq $page.Label -and (Get-ClassName $handle) -like "*BUTTON*") {
                $nav = $handle; break
            }
        }
        if ($nav -eq [IntPtr]::Zero) { [void]$report.Add($page.Name + ": navigation button not found"); continue }
        [void][DpiNative]::PostMessage($nav, 0x00F5, [IntPtr]::Zero, [IntPtr]::Zero)
        Start-Sleep -Seconds 2
        $overlaps = Get-SiblingOverlaps $main
        [void]$report.Add($page.Name + ": overlaps=" + $overlaps.Count)
        foreach ($line in $overlaps) { [void]$report.Add($line) }
        $clipped = Get-ClippedControls $main
        [void]$report.Add($page.Name + ": clipped=" + $clipped.Count)
        foreach ($line in $clipped) { [void]$report.Add($line) }
        $size = Save-Window $main (Join-Path $OutDir ($page.Name + ".png"))
        [void]$report.Add("    captured " + $size)
    }
    }
}
finally {
    Start-Sleep -Milliseconds 400
    if ($startedByUs -and $process -ne $null -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    }
}
$report | Set-Content -LiteralPath (Join-Path $OutDir "geometry.txt") -Encoding UTF8
$report | ForEach-Object { Write-Host $_ }
$total = ($report | Where-Object { $_ -match ": overlaps=([1-9][0-9]*)$" } | Measure-Object).Count
$clippedTotal = ($report | Where-Object { $_ -match ": clipped=([1-9][0-9]*)$" } | Measure-Object).Count
if ($total -gt 0 -or $clippedTotal -gt 0) {
    Write-Host ("pages with overlapping controls: " + $total + ", pages with clipped text: " + $clippedTotal)
    exit 1
}
Write-Host "no overlapping sibling controls and no clipped text"
