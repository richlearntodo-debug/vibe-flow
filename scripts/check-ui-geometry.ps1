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
    [Parameter(Mandatory = $true)][string]$Exe,
    [Parameter(Mandatory = $true)][string]$OutDir,
    [int]$TimeoutMilliseconds = 25000
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
}
'@

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
    $script:foundWindow = [IntPtr]::Zero
    $callback = [DpiNative+WindowCallback]{
        param([IntPtr]$handle, [IntPtr]$parameter)
        [uint32]$owner = 0
        [DpiNative]::GetWindowThreadProcessId($handle, [ref]$owner) | Out-Null
        if ($owner -eq $ProcessId) {
            $text = Get-WindowText $handle
            $hit = if ($Prefix) { $text.StartsWith($Title) } else { $text -eq $Title }
            if ($hit -and [DpiNative]::IsWindowVisible($handle)) { $script:foundWindow = $handle }
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

$windowPrefix = ConvertFrom-CodePoints @(0x8A00, 0x7075)   # product name
$pages = @(
    @{ Name = "01-home";       Label = (ConvertFrom-CodePoints @(0x9996, 0x9875)) },
    @{ Name = "02-workflow";   Label = (ConvertFrom-CodePoints @(0x5DE5, 0x4F5C, 0x6D41)) },
    @{ Name = "03-shortcuts";  Label = (ConvertFrom-CodePoints @(0x5FEB, 0x6377, 0x952E)) },
    @{ Name = "04-voice";      Label = (ConvertFrom-CodePoints @(0x8BED, 0x97F3)) },
    @{ Name = "05-diagnostics";Label = (ConvertFrom-CodePoints @(0x81EA, 0x68C0)) },
    @{ Name = "06-settings";   Label = (ConvertFrom-CodePoints @(0x8BBE, 0x7F6E)) }
)

$process = Start-Process -FilePath $Exe -ArgumentList "--ui-smoke" -PassThru
$report = New-Object System.Collections.ArrayList
try {
    $main = Wait-ForTopWindow $process.Id $windowPrefix $true
    Start-Sleep -Seconds 3
    $mainRect = Get-Rect $main
    $dpi = [DpiNative]::GetDpiForWindow($main)
    [void]$report.Add("window=" + ($mainRect.Right - $mainRect.Left) + "x" + ($mainRect.Bottom - $mainRect.Top) +
        "  dpi=" + $dpi + "  scale=" + [Math]::Round($dpi / 96.0, 2))
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
        $size = Save-Window $main (Join-Path $OutDir ($page.Name + ".png"))
        [void]$report.Add("    captured " + $size)
    }
}
finally {
    Start-Sleep -Milliseconds 400
    if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
}
$report | Set-Content -LiteralPath (Join-Path $OutDir "geometry.txt") -Encoding UTF8
$report | ForEach-Object { Write-Host $_ }
$total = ($report | Where-Object { $_ -match ": overlaps=([1-9][0-9]*)$" } | Measure-Object).Count
if ($total -gt 0) { Write-Host ("pages with overlapping controls: " + $total); exit 1 }
Write-Host "no overlapping sibling controls"
