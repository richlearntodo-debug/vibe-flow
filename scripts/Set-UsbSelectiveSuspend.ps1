[CmdletBinding()]
param(
    [switch]$Disable,
    [switch]$Restore,
    [switch]$StatusOnly,
    [string]$StateDirectory = ""
)

$ErrorActionPreference = "Stop"
$subgroup = "2a737441-1930-4402-8d77-b2bebba308a3"
$setting = "48e6b7a6-50f5-4782-a5d4-53bb8f07e226"
$stateDirectory = if ([string]::IsNullOrWhiteSpace($StateDirectory)) {
    Join-Path $env:LOCALAPPDATA "Vibe Flow Remote\usb-suspend"
} else { $StateDirectory }
$statePath = Join-Path $stateDirectory "state.json"

function Get-SuspendValues {
    $lines = powercfg /query SCHEME_CURRENT $subgroup $setting 2>$null |
        Select-String -Pattern '0x[0-9A-Fa-f]{8}'
    $values = @()
    foreach ($line in $lines) {
        $text = ($line.Line -split ':')[-1].Trim()
        try { $values += [Convert]::ToInt32($text, 16) } catch { }
    }
    if ($values.Count -ge 2) { return @($values[0], $values[1]) }
    if ($values.Count -eq 1) { return @($values[0], $values[0]) }
    return @(-1, -1)
}

function Write-State([string]$state, [string]$detail, $before, $after) {
    New-Item -ItemType Directory -Force -Path $stateDirectory | Out-Null
    $payload = [ordered]@{
        updated_at = (Get-Date).ToUniversalTime().ToString("o")
        state = $state
        detail = $detail
        ac_before = $before[0]
        dc_before = $before[1]
        ac_after = $after[0]
        dc_after = $after[1]
    }
    $payload | ConvertTo-Json -Compress | Set-Content -LiteralPath $statePath -Encoding UTF8
}

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

$before = Get-SuspendValues
if ($StatusOnly) {
    Write-State "status" "USB selective suspend AC=$($before[0]) DC=$($before[1])" $before $before
    Get-Content -LiteralPath $statePath -Raw
    exit 0
}
if (-not $Disable -and -not $Restore) {
    Write-State "status" "Nothing to do; pass -Disable or -Restore" $before $before
    exit 0
}

$target = if ($Disable) { 0 } else { 1 }
if ($before[0] -eq $target -and $before[1] -eq $target) {
    Write-State "already_applied" "USB selective suspend is already $target" $before $before
    exit 0
}

if (-not (Test-IsAdministrator)) {
    Write-State "elevation_required" "Administrator permission is required; confirm the Windows UAC prompt" $before $before
    $scriptPath = $MyInvocation.MyCommand.Path
    $verb = if ($Disable) { "-Disable" } else { "-Restore" }
    $arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$scriptPath`" $verb -StateDirectory `"$stateDirectory`""
    Start-Process -FilePath "powershell.exe" -ArgumentList $arguments -Verb RunAs | Out-Null
    exit 740
}

powercfg /setacvalueindex SCHEME_CURRENT $subgroup $setting $target | Out-Null
powercfg /setdcvalueindex SCHEME_CURRENT $subgroup $setting $target | Out-Null
powercfg /setactive SCHEME_CURRENT | Out-Null
$after = Get-SuspendValues
if ($after[0] -ne $target -or $after[1] -ne $target) {
    Write-State "failed" "powercfg did not apply the requested value" $before $after
    exit 1
}
Write-State "applied" "USB selective suspend set to $target (AC/DC)" $before $after
exit 0
