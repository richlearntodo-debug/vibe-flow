# Ask the running host to inject 讯飞's voice hotkey, and watch for the voice surface.
#
# The static probe (Probe-IFlyVoice.ps1) could not make 讯飞 react to keys injected from PowerShell,
# so this probe uses the production path instead: the host itself taps the shortcut when
# "Local\VibeMicProviderHotkeyTapRequested" is signalled, and the host's own log records what it
# injected. The probe borrows 微信输入法's provider slot for one tap: it edits only inputMethodHotkey,
# keeps a backup, and restores the file in a finally block. The 讯飞 hotkey it taps is 讯飞's own
# installer default, HKCU\Software\iFly Info Tek\iFlyIME -> iFlyImeVoiceShiftHotKey.
param(
    [string]$Shortcut = 'ctrl+shift+alt+[',
    [int]$TimeoutMs = 8000
)

$ErrorActionPreference = 'Stop'

Add-Type @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public static class InjectProbeNative
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

    public static List<WindowInfo> AllWindows(int[] ids, string[] names)
    {
        var map = new Dictionary<int, string>();
        for (int i = 0; i < ids.Length; i++) map[ids[i]] = names[i];
        var found = new List<WindowInfo>();
        EnumWindows(delegate(IntPtr hWnd, IntPtr lParam)
        {
            int pid;
            GetWindowThreadProcessId(hWnd, out pid);
            if (!map.ContainsKey(pid)) return true;
            int length = GetWindowTextLength(hWnd);
            var title = new StringBuilder(length + 2);
            GetWindowText(hWnd, title, title.Capacity);
            var cls = new StringBuilder(256);
            GetClassName(hWnd, cls, cls.Capacity);
            var info = new WindowInfo();
            info.Handle = hWnd;
            info.ProcessId = pid;
            info.ProcessName = map[pid];
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

function Get-Snapshot {
    $procs = @(Get-Process -ErrorAction SilentlyContinue)
    return @([InjectProbeNative]::AllWindows([int[]]$procs.Id, [string[]]$procs.ProcessName))
}

function Get-Keys($snapshot) {
    return @($snapshot | ForEach-Object { "$($_.ProcessName)/$($_.ClassName)/'$($_.Title)'/visible=$($_.Visible)" })
}

function Compare-Keys($before, $after) {
    $appeared = @($after | Where-Object { $before -notcontains $_ } | Sort-Object -Unique)
    $vanished = @($before | Where-Object { $after -notcontains $_ } | Sort-Object -Unique)
    $parts = @()
    if ($appeared.Count -gt 0) { $parts += 'appeared: ' + ($appeared -join ' ; ') }
    if ($vanished.Count -gt 0) { $parts += 'vanished: ' + ($vanished -join ' ; ') }
    if ($parts.Count -eq 0) { return '(no window change)' }
    return ($parts -join ' || ')
}

$sessionDir = Join-Path $env:LOCALAPPDATA 'Vibe Flow Remote\UserData\remote-voice-session'
$hostLog = Join-Path $sessionDir 'vibe-flow-host.log'
$configPath = Join-Path $env:LOCALAPPDATA 'Vibe Flow Remote\UserData\vibe-mic-config.json'

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$scratch = New-Object System.Windows.Forms.Form
$scratch.Text = '讯飞注入探针'
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
[InjectProbeNative]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero)
[InjectProbeNative]::keybd_event(0x12, 0, 2, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 120
[void][InjectProbeNative]::SetForegroundWindow($scratch.Handle)
Start-Sleep -Milliseconds 400

$backup = "$configPath.before-ifly-probe-$(Get-Date -Format yyyyMMdd-HHmmss).bak"
Copy-Item $configPath $backup -Force
$logLengthBefore = if (Test-Path $hostLog) { (Get-Item $hostLog).Length } else { 0 }

try {
    $document = Get-Content $configPath -Raw -Encoding UTF8 | ConvertFrom-Json
    Write-Output "config: provider=$($document.inputMethod) hotkey=$($document.inputMethodHotkey) trigger=$($document.inputMethodTrigger)"
    $document.inputMethodHotkey = $Shortcut
    $document | ConvertTo-Json -Depth 12 | Set-Content $configPath -Encoding UTF8
    Write-Output "borrowed shortcut=$Shortcut (backup: $(Split-Path $backup -Leaf))"
    Write-Output "foreground_ok=$([InjectProbeNative]::GetForegroundWindow() -eq $scratch.Handle)"

    $before = Get-Keys (Get-Snapshot)
    Write-Output "訊飛 windows before: $((@((Get-Snapshot) | Where-Object { $_.ProcessName -match '^iFly' -and $_.Visible }) | ForEach-Object { "$($_.ProcessName)/$($_.ClassName)/'$($_.Title)'" }) -join ' | ')"

    $handle = [System.Threading.EventWaitHandle]::OpenExisting('Local\VibeMicProviderHotkeyTapRequested')
    [void]$handle.Set()
    Write-Output 'signalled Local\VibeMicProviderHotkeyTapRequested'

    $deadline = (Get-Date).AddMilliseconds($TimeoutMs)
    $seen = $false
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 400
        $now = Get-Keys (Get-Snapshot)
        $diff = Compare-Keys $before $now
        if ($diff -ne '(no window change)' -and $diff -notmatch 'VibeFlow/|讯飞注入探针|WindowsForms10') {
            Write-Output "  $diff"
            $seen = $true
        }
    }
    if (-not $seen) { Write-Output '  (no 讯飞 or IME window change during the whole window)' }
    Write-Output "訊飛 windows after : $((@((Get-Snapshot) | Where-Object { $_.ProcessName -match '^iFly' -and $_.Visible }) | ForEach-Object { "$($_.ProcessName)/$($_.ClassName)/'$($_.Title)'" }) -join ' | ')"
    Write-Output "scratch text       : '$($box.Text)'"
}
finally {
    Copy-Item $backup $configPath -Force
    $document = Get-Content $configPath -Raw -Encoding UTF8 | ConvertFrom-Json
    Write-Output "restored config: hotkey=$($document.inputMethodHotkey)"
    if (Test-Path $hostLog) {
        Write-Output '--- host log since the tap ---'
        $stream = [System.IO.File]::Open($hostLog, 'Open', 'Read', 'ReadWrite')
        try {
            $stream.Seek($logLengthBefore, 'Begin') | Out-Null
            $reader = New-Object System.IO.StreamReader($stream)
            $text = $reader.ReadToEnd()
            ($text -split "`r?`n" | Where-Object { $_ -match 'PROVIDER HOTKEY|PROVIDER TAP|VOICE KEY|AUDIO DUCK' } | Select-Object -Last 12) | ForEach-Object { "  $_" }
        }
        finally { $stream.Dispose() }
    }
    $scratch.Close()
    $scratch.Dispose()
}
