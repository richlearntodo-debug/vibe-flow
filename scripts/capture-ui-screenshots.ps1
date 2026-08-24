param(
    [string]$OutputDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) "docs\images")
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System;
using System.Text;
using System.Runtime.InteropServices;

public static class VibeScreenshotNative
{
    public delegate bool WindowCallback(IntPtr handle, IntPtr parameter);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    public static extern bool EnumChildWindows(IntPtr parent, WindowCallback callback, IntPtr parameter);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(WindowCallback callback, IntPtr parameter);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr handle, StringBuilder text, int maximum);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetClassName(IntPtr handle, StringBuilder text, int maximum);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr handle, out RECT rectangle);

    [DllImport("user32.dll")]
    public static extern bool PrintWindow(IntPtr handle, IntPtr deviceContext, uint flags);

    [DllImport("user32.dll")]
    public static extern bool PostMessage(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam);
}
'@

function Get-WindowText([IntPtr]$Handle) {
    $text = New-Object System.Text.StringBuilder 512
    [VibeScreenshotNative]::GetWindowText($Handle, $text, $text.Capacity) | Out-Null
    return $text.ToString()
}

function ConvertFrom-CodePoints([int[]]$CodePoints) {
    return -join ($CodePoints | ForEach-Object { [char]$_ })
}

function Find-ChildButton([IntPtr]$Parent, [string]$Text) {
    $script:matchingChild = [IntPtr]::Zero
    $callback = [VibeScreenshotNative+WindowCallback]{
        param([IntPtr]$handle, [IntPtr]$parameter)
        $className = New-Object System.Text.StringBuilder 128
        [VibeScreenshotNative]::GetClassName($handle, $className, $className.Capacity) | Out-Null
        if ($className.ToString().Contains("BUTTON") -and (Get-WindowText $handle).EndsWith($Text)) {
            $script:matchingChild = $handle
        }
        return $true
    }
    [VibeScreenshotNative]::EnumChildWindows($Parent, $callback, [IntPtr]::Zero) | Out-Null
    if ($script:matchingChild -eq [IntPtr]::Zero) { throw "Button not found: $Text" }
    return $script:matchingChild
}

function Find-ProcessWindow([int]$ProcessId, [string]$TitlePrefix) {
    $script:matchingWindow = [IntPtr]::Zero
    $callback = [VibeScreenshotNative+WindowCallback]{
        param([IntPtr]$handle, [IntPtr]$parameter)
        [uint32]$owner = 0
        [VibeScreenshotNative]::GetWindowThreadProcessId($handle, [ref]$owner) | Out-Null
        if ($owner -eq $ProcessId -and (Get-WindowText $handle).StartsWith($TitlePrefix)) {
            $script:matchingWindow = $handle
        }
        return $true
    }
    [VibeScreenshotNative]::EnumWindows($callback, [IntPtr]::Zero) | Out-Null
    return $script:matchingWindow
}

function Invoke-Button([IntPtr]$Parent, [string]$Text) {
    $button = Find-ChildButton $Parent $Text
    [VibeScreenshotNative]::PostMessage($button, 0x00F5, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
    Start-Sleep -Milliseconds 650
}

function Save-Window([IntPtr]$Handle, [string]$Path) {
    $rectangle = New-Object VibeScreenshotNative+RECT
    [VibeScreenshotNative]::GetWindowRect($Handle, [ref]$rectangle) | Out-Null
    $width = $rectangle.Right - $rectangle.Left
    $height = $rectangle.Bottom - $rectangle.Top
    if ($width -lt 100 -or $height -lt 100) { throw "Window is not visible: $Path" }
    $bitmap = New-Object System.Drawing.Bitmap $width, $height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $deviceContext = $graphics.GetHdc()
    try {
        if (-not [VibeScreenshotNative]::PrintWindow($Handle, $deviceContext, 2)) {
            throw "PrintWindow failed: $Path"
        }
    }
    finally {
        $graphics.ReleaseHdc($deviceContext)
    }
    $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$showWindow = $null
try {
    $showWindow = [Threading.EventWaitHandle]::OpenExisting("Local\VibeMicShowWindow")
    $showWindow.Set() | Out-Null
    Start-Sleep -Milliseconds 650
}
catch { }
finally {
    if ($null -ne $showWindow) { $showWindow.Dispose() }
}
$process = Get-Process VibeMic, VibeFlow -ErrorAction SilentlyContinue |
    Where-Object { $_.MainWindowHandle -ne 0 } |
    Select-Object -First 1
if ($null -eq $process) { throw "Start Vibe Flow before capturing screenshots." }

$main = $process.MainWindowHandle
$overviewLabel = ConvertFrom-CodePoints @(0x603B, 0x89C8)
$dictationLabel = ConvertFrom-CodePoints @(0x8BED, 0x97F3, 0x542C, 0x5199)
$shortcutsLabel = ConvertFrom-CodePoints @(0x6309, 0x952E, 0x5FEB, 0x6377, 0x65B9, 0x5F0F)
$selfCheckLabel = ConvertFrom-CodePoints @(0x8FDE, 0x63A5, 0x4E0E, 0x81EA, 0x68C0)
$settingsLabel = ConvertFrom-CodePoints @(0x504F, 0x597D, 0x8BBE, 0x7F6E)
$setupLabel = ConvertFrom-CodePoints @(0x6253, 0x5F00, 0x5165, 0x95E8, 0x6307, 0x5357)
$welcomePrefix = ConvertFrom-CodePoints @(0x6B22, 0x8FCE, 0x4F7F, 0x7528)
$pages = @(
    @{ Button = $overviewLabel; File = "01-overview.png" },
    @{ Button = $dictationLabel; File = "02-dictation.png" },
    @{ Button = $shortcutsLabel; File = "03-shortcuts.png" },
    @{ Button = $selfCheckLabel; File = "04-diagnostics.png" },
    @{ Button = $settingsLabel; File = "05-settings.png" }
)

foreach ($page in $pages) {
    Invoke-Button $main $page.Button
    Save-Window $main (Join-Path $OutputDirectory $page.File)
}

Invoke-Button $main $setupLabel
$wizard = [IntPtr]::Zero
for ($attempt = 0; $attempt -lt 20 -and $wizard -eq [IntPtr]::Zero; $attempt++) {
    Start-Sleep -Milliseconds 150
    $wizard = Find-ProcessWindow $process.Id $welcomePrefix
}
if ($wizard -eq [IntPtr]::Zero) { throw "First-run wizard did not open." }
Save-Window $wizard (Join-Path $OutputDirectory "00-first-run.png")
[VibeScreenshotNative]::PostMessage($wizard, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null

Write-Host "Captured Vibe Flow screenshots in $OutputDirectory"
