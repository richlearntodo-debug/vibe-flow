# Probe 讯飞输入法's voice hotkey on this machine.
#
# The host has to know two things before it can drive 讯飞语音输入: which shortcut opens the voice
# surface, and whether that shortcut toggles the surface or only holds it open. The registry values
# 讯飞's own installer writes (HKCU\Software\iFly Info Tek\iFlyIME) are the starting candidates:
#   iFlyImeOpenSelfHotKey    = Ctrl + '              (activate 讯飞 for the focused window)
#   iFlyImeVoiceShiftHotKey  = Ctrl + Shift + Alt + [ (voice)
#   iFlyImeInkShiftOpenHotKey= Ctrl + Shift + Alt + ] (handwriting)
# Older 讯飞 builds document F6 for the voice bar, so it is probed as well.
#
# 讯飞 only answers its voice hotkey when it is the active input method of the focused window, so the
# probe first activates it with iFlyImeOpenSelfHotKey and verifies focus rather than assuming it.
# Everything printed is observation: the probe changes no setting on the machine.
param(
    [string[]]$Candidates = @('ctrl+shift+alt+[', 'f6'),
    [string]$ActivateHotkey = "ctrl+'",
    [int]$HoldMs = 2000
)

$ErrorActionPreference = 'Stop'

Add-Type @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public static class ProbeNative
{
    [DllImport("user32.dll")] public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
    [DllImport("user32.dll")] public static extern int GetClassName(IntPtr hWnd, StringBuilder text, int count);
    [DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(IntPtr hWnd, out int processId);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    public sealed class WindowInfo
    {
        public IntPtr Handle;
        public int ProcessId;
        public string ProcessName;
        public string Title;
        public string ClassName;
        public bool Visible;
    }

    public static List<WindowInfo> WindowsForProcessIds(int[] ids, string[] names)
    {
        var wanted = new HashSet<int>(ids);
        var found = new List<WindowInfo>();
        EnumWindows(delegate(IntPtr hWnd, IntPtr lParam)
        {
            int pid;
            GetWindowThreadProcessId(hWnd, out pid);
            if (!wanted.Contains(pid)) return true;
            int length = GetWindowTextLength(hWnd);
            var title = new StringBuilder(length + 2);
            GetWindowText(hWnd, title, title.Capacity);
            var cls = new StringBuilder(256);
            GetClassName(hWnd, cls, cls.Capacity);
            var info = new WindowInfo();
            info.Handle = hWnd;
            info.ProcessId = pid;
            info.ProcessName = names == null ? string.Empty : (Array.IndexOf(ids, pid) >= 0 && names.Length == ids.Length ? names[Array.IndexOf(ids, pid)] : string.Empty);
            info.Title = title.ToString();
            info.ClassName = cls.ToString();
            info.Visible = IsWindowVisible(hWnd);
            found.Add(info);
            return true;
        }, IntPtr.Zero);
        return found;
    }
}
'@

$virtualKeys = @{
    'ctrl' = 0x11; 'shift' = 0x10; 'alt' = 0x12; 'win' = 0x5B
    '[' = 0xDB; ']' = 0xDD; 'f6' = 0x75; "'" = 0xDE
}

function Get-ShortcutVirtualKeys([string]$shortcut) {
    $keys = @()
    foreach ($part in ($shortcut -split '\+')) {
        $token = $part.Trim().ToLowerInvariant()
        if ($virtualKeys.ContainsKey($token)) { $keys += [byte]$virtualKeys[$token] }
        else { throw "Probe does not know the key '$token' in '$shortcut'" }
    }
    return $keys
}

function Get-IFlyWindows {
    $procs = @(Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -match '^iFly' })
    if ($procs.Count -eq 0) { return @() }
    return @([ProbeNative]::WindowsForProcessIds([int[]]$procs.Id, [string[]]$procs.ProcessName))
}

# 讯飞 ships both a legacy IME and a TSF text service, and a TSF text service runs *inside the focused
# application*: its voice bar would be a window of the probe's own process, not of iFlyInput.exe. The
# probe therefore snapshots every top-level window and reports what appeared or disappeared, which
# catches the surface wherever it is hosted.
function Get-AllWindows {
    $procs = @(Get-Process -ErrorAction SilentlyContinue)
    return @([ProbeNative]::WindowsForProcessIds([int[]]$procs.Id, [string[]]$procs.ProcessName))
}

function Get-WindowKey($window) {
    # Visibility is part of the key: 讯飞 creates its surfaces up front and shows or hides them, so a
    # voice bar that appears is a visibility flip on an existing window, not a new window.
    return "$($window.ProcessName)/$($window.ClassName)/'$($window.Title)'/visible=$($window.Visible)"
}

function Compare-Snapshot($before, $after) {
    $beforeKeys = @($before | ForEach-Object { Get-WindowKey $_ })
    $afterKeys = @($after | ForEach-Object { Get-WindowKey $_ })
    $appeared = @($afterKeys | Where-Object { $beforeKeys -notcontains $_ } | Sort-Object -Unique)
    $vanished = @($beforeKeys | Where-Object { $afterKeys -notcontains $_ } | Sort-Object -Unique)
    $parts = @()
    if ($appeared.Count -gt 0) { $parts += 'appeared: ' + ($appeared -join ' ; ') }
    if ($vanished.Count -gt 0) { $parts += 'vanished: ' + ($vanished -join ' ; ') }
    if ($parts.Count -eq 0) { return '(no window change)' }
    return ($parts -join ' || ')
}

# Only the windows that are actually on screen matter, and a voice bar that appears is a difference.
function Format-Snapshot($windows) {
    $visible = @($windows | Where-Object { $_.Visible })
    if ($visible.Count -eq 0) { return "(nothing visible; hidden windows=$($windows.Count))" }
    return (($visible | ForEach-Object { "$($_.ProcessName)/$($_.ClassName) '$($_.Title)'" }) | Sort-Object) -join ' | '
}

function Invoke-KeyDown([byte[]]$keys) {
    foreach ($key in $keys) { [ProbeNative]::keybd_event($key, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 30 }
}
function Invoke-KeyUp([byte[]]$keys) {
    for ($i = $keys.Count - 1; $i -ge 0; $i--) { [ProbeNative]::keybd_event($keys[$i], 0, 2, [UIntPtr]::Zero); Start-Sleep -Milliseconds 30 }
}
function Invoke-Shortcut([string]$shortcut, [int]$holdMs = 0) {
    $keys = Get-ShortcutVirtualKeys $shortcut
    Invoke-KeyDown $keys
    if ($holdMs -gt 0) { Start-Sleep -Milliseconds $holdMs }
    Invoke-KeyUp $keys
}

# Windows refuses SetForegroundWindow from a process that is not already foreground. Tapping ALT is
# the documented way to release that lock, and the probe verifies the result instead of assuming it:
# a hotkey delivered to the wrong window would look exactly like a hotkey 讯飞 ignores.
function Focus-Window([IntPtr]$handle) {
    [ProbeNative]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero)
    [ProbeNative]::keybd_event(0x12, 0, 2, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 120
    [void][ProbeNative]::SetForegroundWindow($handle)
    Start-Sleep -Milliseconds 350
    return ([ProbeNative]::GetForegroundWindow() -eq $handle)
}

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$scratch = New-Object System.Windows.Forms.Form
$scratch.Text = '讯飞探针'
$scratch.Width = 520
$scratch.Height = 180
$scratch.StartPosition = 'CenterScreen'
$box = New-Object System.Windows.Forms.TextBox
$box.Multiline = $true
$box.Dock = 'Fill'
$scratch.Controls.Add($box)
$scratch.Show()
$scratch.Activate()
$box.Focus()
$focused = Focus-Window $scratch.Handle
[System.Windows.Forms.Application]::DoEvents()
Start-Sleep -Milliseconds 600

try {
    Write-Output "scratch handle=$($scratch.Handle) foreground_ok=$focused foreground=$([ProbeNative]::GetForegroundWindow())"
    $baseline = Get-AllWindows
    Write-Output "baseline  : $(Format-Snapshot (Get-IFlyWindows))"
    Write-Output "baseline windows (visible, all processes): $(@($baseline | Where-Object { $_.Visible }).Count)"

    Write-Output ''
    Write-Output "=== activate 讯飞 with $ActivateHotkey ==="
    $mark = Get-AllWindows
    Invoke-Shortcut $ActivateHotkey
    Start-Sleep -Milliseconds 1200
    Write-Output "  $((Compare-Snapshot $mark (Get-AllWindows)))"

    foreach ($candidate in $Candidates) {
        Write-Output ''
        Write-Output "=== $candidate ==="

        # If the shortcut toggles the voice surface, one tap opens it and it stays open.
        $mark = Get-AllWindows
        Invoke-Shortcut $candidate
        Start-Sleep -Milliseconds 1500
        Write-Output "  after one tap  : $((Compare-Snapshot $mark (Get-AllWindows)))"

        # A second tap closes a toggle again.
        $mark = Get-AllWindows
        Invoke-Shortcut $candidate
        Start-Sleep -Milliseconds 1500
        Write-Output "  after two taps : $((Compare-Snapshot $mark (Get-AllWindows)))"

        # If the shortcut is hold-to-talk, the surface is only there while the keys are down.
        $keys = Get-ShortcutVirtualKeys $candidate
        $mark = Get-AllWindows
        Invoke-KeyDown $keys
        Start-Sleep -Milliseconds $HoldMs
        Write-Output "  while held     : $((Compare-Snapshot $mark (Get-AllWindows)))"
        $mark = Get-AllWindows
        Invoke-KeyUp $keys
        Start-Sleep -Milliseconds 1500
        Write-Output "  after release  : $((Compare-Snapshot $mark (Get-AllWindows)))"
        Write-Output "  scratch text   : '$($box.Text)'"
    }
}
finally {
    $scratch.Close()
    $scratch.Dispose()
}
