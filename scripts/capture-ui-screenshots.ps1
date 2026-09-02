param(
    [string]$OutputDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) "docs\images"),
    [int]$ProcessId = 0,
    [ValidateSet("Current", "Light", "Dark", "System")]
    [string]$Theme = "Current",
    [switch]$CaptureFullOnboarding,
    [switch]$AllowUnhealthyDiagnostics
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName UIAutomationClient
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

    [StructLayout(LayoutKind.Sequential)]
    public struct COMBOBOXINFO
    {
        public int cbSize;
        public RECT rcItem;
        public RECT rcButton;
        public IntPtr hwndCombo;
        public IntPtr hwndItem;
        public IntPtr hwndList;
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
    public static extern bool GetComboBoxInfo(IntPtr combo, ref COMBOBOXINFO info);

    [DllImport("user32.dll")]
    public static extern bool PrintWindow(IntPtr handle, IntPtr deviceContext, uint flags);

    [DllImport("user32.dll")]
    public static extern bool PostMessage(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessageW")]
    public static extern IntPtr SendMessageText(IntPtr handle, uint message, IntPtr wParam, StringBuilder lParam);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr handle);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr handle, int command);
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

function Test-ChildText([IntPtr]$Parent, [string]$Text) {
    $script:matchingText = $false
    $callback = [VibeScreenshotNative+WindowCallback]{
        param([IntPtr]$handle, [IntPtr]$parameter)
        if ((Get-WindowText $handle).Contains($Text)) {
            $script:matchingText = $true
        }
        return $true
    }
    [VibeScreenshotNative]::EnumChildWindows($Parent, $callback, [IntPtr]::Zero) | Out-Null
    return $script:matchingText
}

function Wait-ForChildText([IntPtr]$Parent, [string]$Text, [int]$TimeoutMilliseconds) {
    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
    do {
        if (Test-ChildText $Parent $Text) { return $true }
        Start-Sleep -Milliseconds 250
    } while ([DateTime]::UtcNow -lt $deadline)
    return $false
}

function Set-ChildCheckboxUnchecked([IntPtr]$Parent, [string]$Text) {
    $window = [Windows.Automation.AutomationElement]::RootElement
    $nameCondition = New-Object Windows.Automation.PropertyCondition(
        [Windows.Automation.AutomationElement]::NameProperty, $Text)
    $typeCondition = New-Object Windows.Automation.PropertyCondition(
        [Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::CheckBox)
    $element = $window.FindFirst([Windows.Automation.TreeScope]::Descendants,
        (New-Object Windows.Automation.AndCondition($nameCondition, $typeCondition)))
    if ($null -eq $element) { throw "Checkbox not found: $Text" }
    $pattern = $element.GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern)
    if ($pattern.Current.ToggleState -eq [Windows.Automation.ToggleState]::On) {
        $pattern.Toggle()
        Start-Sleep -Milliseconds 250
    }
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

function Find-TopmostComboBox([IntPtr]$Parent) {
    $script:matchingCombo = [IntPtr]::Zero
    $script:matchingComboTop = [int]::MaxValue
    $callback = [VibeScreenshotNative+WindowCallback]{
        param([IntPtr]$handle, [IntPtr]$parameter)
        $className = New-Object System.Text.StringBuilder 128
        [VibeScreenshotNative]::GetClassName($handle, $className, $className.Capacity) | Out-Null
        if ($className.ToString().Contains("COMBOBOX")) {
            $rectangle = New-Object VibeScreenshotNative+RECT
            [VibeScreenshotNative]::GetWindowRect($handle, [ref]$rectangle) | Out-Null
            if ($rectangle.Top -lt $script:matchingComboTop) {
                $script:matchingCombo = $handle
                $script:matchingComboTop = $rectangle.Top
            }
        }
        return $true
    }
    [VibeScreenshotNative]::EnumChildWindows($Parent, $callback, [IntPtr]::Zero) | Out-Null
    if ($script:matchingCombo -eq [IntPtr]::Zero) { throw "No combo box found in the active page." }
    return $script:matchingCombo
}

function Set-ComboSelectionByText([IntPtr]$Combo, [string]$Text) {
    $count = [VibeScreenshotNative]::SendMessage($Combo, 0x0146, [IntPtr]::Zero, [IntPtr]::Zero).ToInt32()
    for ($index = 0; $index -lt $count; $index++) {
        $item = New-Object System.Text.StringBuilder 256
        [VibeScreenshotNative]::SendMessageText($Combo, 0x0148, [IntPtr]$index, $item) | Out-Null
        if ($item.ToString() -eq $Text) {
            [VibeScreenshotNative]::SendMessage($Combo, 0x014E, [IntPtr]$index, [IntPtr]::Zero) | Out-Null
            return
        }
    }
    throw "Combo-box item not found: $Text"
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

function Save-ScreenRegion([IntPtr]$Handle, [string]$Path) {
    $rectangle = New-Object VibeScreenshotNative+RECT
    [VibeScreenshotNative]::GetWindowRect($Handle, [ref]$rectangle) | Out-Null
    $width = $rectangle.Right - $rectangle.Left
    $height = $rectangle.Bottom - $rectangle.Top
    if ($width -lt 100 -or $height -lt 100) { throw "Window is not visible: $Path" }
    $bitmap = New-Object System.Drawing.Bitmap $width, $height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen($rectangle.Left, $rectangle.Top, 0, 0, $bitmap.Size,
            [System.Drawing.CopyPixelOperation]::SourceCopy)
        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

function Save-WindowWithComboDropdown([IntPtr]$Handle, [IntPtr]$Combo, [string]$Path) {
    $mainRectangle = New-Object VibeScreenshotNative+RECT
    [VibeScreenshotNative]::GetWindowRect($Handle, [ref]$mainRectangle) | Out-Null
    $width = $mainRectangle.Right - $mainRectangle.Left
    $height = $mainRectangle.Bottom - $mainRectangle.Top
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

    $comboInfo = New-Object VibeScreenshotNative+COMBOBOXINFO
    $comboInfo.cbSize = [Runtime.InteropServices.Marshal]::SizeOf([type][VibeScreenshotNative+COMBOBOXINFO])
    if (-not [VibeScreenshotNative]::GetComboBoxInfo($Combo, [ref]$comboInfo) -or
        $comboInfo.hwndList -eq [IntPtr]::Zero) {
        $graphics.Dispose()
        $bitmap.Dispose()
        throw "Unable to locate the expanded transcription-tool list."
    }

    $comboRectangle = New-Object VibeScreenshotNative+RECT
    $listRectangle = New-Object VibeScreenshotNative+RECT
    [VibeScreenshotNative]::GetWindowRect($Combo, [ref]$comboRectangle) | Out-Null
    [VibeScreenshotNative]::GetWindowRect($comboInfo.hwndList, [ref]$listRectangle) | Out-Null
    $overlayLeft = [Math]::Min($comboRectangle.Left, $listRectangle.Left)
    $overlayTop = [Math]::Min($comboRectangle.Top, $listRectangle.Top)
    $overlayRight = [Math]::Max($comboRectangle.Right, $listRectangle.Right)
    $overlayBottom = [Math]::Max($comboRectangle.Bottom, $listRectangle.Bottom)
    $overlayWidth = $overlayRight - $overlayLeft
    $overlayHeight = $overlayBottom - $overlayTop
    try {
        $graphics.CopyFromScreen($overlayLeft, $overlayTop,
            $overlayLeft - $mainRectangle.Left, $overlayTop - $mainRectangle.Top,
            (New-Object System.Drawing.Size $overlayWidth, $overlayHeight),
            [System.Drawing.CopyPixelOperation]::SourceCopy)
        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
if ($ProcessId -le 0) {
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
}
$process = if ($ProcessId -gt 0) {
    Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
} else {
    Get-Process VibeMic, VibeFlow -ErrorAction SilentlyContinue |
        Where-Object { $_.MainWindowHandle -ne 0 } |
        Select-Object -First 1
}
if ($null -eq $process) { throw "Start Vibe Flow before capturing screenshots." }

$main = $process.MainWindowHandle
[VibeScreenshotNative]::ShowWindow($main, 9) | Out-Null
[VibeScreenshotNative]::SetForegroundWindow($main) | Out-Null
Start-Sleep -Milliseconds 300
$overviewLabel = ConvertFrom-CodePoints @(0x9996, 0x9875)
$dictationLabel = ConvertFrom-CodePoints @(0x8BED, 0x97F3)
$shortcutsLabel = ConvertFrom-CodePoints @(0x5FEB, 0x6377, 0x952E)
$selfCheckLabel = ConvertFrom-CodePoints @(0x81EA, 0x68C0)
$settingsLabel = ConvertFrom-CodePoints @(0x8BBE, 0x7F6E)
$lightThemeLabel = ConvertFrom-CodePoints @(0x767D, 0x5929, 0x6A21, 0x5F0F)
$darkThemeLabel = ConvertFrom-CodePoints @(0x591C, 0x95F4, 0x6A21, 0x5F0F)
$systemThemeLabel = ConvertFrom-CodePoints @(0x8DDF, 0x968F, 0x20, 0x57, 0x69, 0x6E, 0x64, 0x6F, 0x77, 0x73)
$screenshotActionLabel = ConvertFrom-CodePoints @(0x7CFB, 0x7EDF, 0x20, 0xB7, 0x20, 0x533A, 0x57DF, 0x622A, 0x56FE)
$setupLabel = ConvertFrom-CodePoints @(0x6253, 0x5F00, 0x5165, 0x95E8, 0x6307, 0x5357)
$welcomePrefix = ConvertFrom-CodePoints @(0x9996, 0x6B21, 0x8BBE, 0x7F6E)
$healthySelfCheckText = ConvertFrom-CodePoints @(0x5168, 0x90E8, 0x901A, 0x8FC7, 0xFF0C, 0x53EF, 0x4EE5, 0x7A33, 0x5B9A, 0x4F7F, 0x7528)
$pages = @(
    @{ Button = $overviewLabel; File = "01-overview.png" },
    @{ Button = $dictationLabel; File = "02-dictation.png" },
    @{ Button = $shortcutsLabel; File = "03-shortcuts.png" },
    @{ Button = $selfCheckLabel; File = "04-diagnostics.png" },
    @{ Button = $settingsLabel; File = "05-settings.png" }
)

if ($Theme -ne "Current") {
    Invoke-Button $main $settingsLabel
    Invoke-Button $main $(if ($Theme -eq "Dark") { $darkThemeLabel } elseif ($Theme -eq "System") { $systemThemeLabel } else { $lightThemeLabel })
    Start-Sleep -Milliseconds 450
}

foreach ($page in $pages) {
    Invoke-Button $main $page.Button
    if ($page.File -eq "03-shortcuts.png") {
        Start-Sleep -Milliseconds 250
    }
    if ($page.File -eq "04-diagnostics.png" -and -not $AllowUnhealthyDiagnostics -and
        -not (Test-ChildText $main $healthySelfCheckText)) {
        throw "Release diagnostics screenshot requires a healthy 10/10 self-check. Use -AllowUnhealthyDiagnostics only for troubleshooting captures."
    }
    Save-Window $main (Join-Path $OutputDirectory $page.File)
    if ($page.File -eq "03-shortcuts.png") {
        Save-Window $main (Join-Path $OutputDirectory "03-shortcuts-screenshot.png")
    }
    if ($page.File -eq "02-dictation.png") {
        [VibeScreenshotNative]::SetForegroundWindow($main) | Out-Null
        $providerCombo = Find-TopmostComboBox $main
        [VibeScreenshotNative]::SendMessage($providerCombo, 0x014F, [IntPtr]1, [IntPtr]::Zero) | Out-Null
        Start-Sleep -Milliseconds 350
        Save-WindowWithComboDropdown $main $providerCombo (Join-Path $OutputDirectory "06-transcription-tools.png")
        [VibeScreenshotNative]::SendMessage($providerCombo, 0x014F, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
    }
}

Invoke-Button $main $setupLabel
$wizard = [IntPtr]::Zero
for ($attempt = 0; $attempt -lt 20 -and $wizard -eq [IntPtr]::Zero; $attempt++) {
    Start-Sleep -Milliseconds 150
    $wizard = Find-ProcessWindow $process.Id $welcomePrefix
}
if ($wizard -eq [IntPtr]::Zero) { throw "First-run wizard did not open." }
if ($CaptureFullOnboarding) {
    $nextStep = ConvertFrom-CodePoints @(0x5B8C, 0x6210, 0x672C, 0x6B65, 0xFF0C, 0x7EE7, 0x7EED)
}
Save-Window $wizard (Join-Path $OutputDirectory "00-first-run.png")
if ($CaptureFullOnboarding) {
    $stepFiles = @(
        "00-setup-01-device.png", "00-setup-02-remote.png", "00-setup-03-audio.png",
        "00-setup-04-dictation.png", "00-setup-05-ready.png"
    )
    Save-Window $wizard (Join-Path $OutputDirectory $stepFiles[0])
    for ($step = 1; $step -lt $stepFiles.Count; $step++) {
        Invoke-Button $wizard $nextStep
        Start-Sleep -Milliseconds 180
        Save-Window $wizard (Join-Path $OutputDirectory $stepFiles[$step])
    }
}
[VibeScreenshotNative]::PostMessage($wizard, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null

Write-Host "Captured Vibe Flow screenshots in $OutputDirectory"
