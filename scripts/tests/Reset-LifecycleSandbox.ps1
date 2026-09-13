# Puts the machine back into the clean-account state scripts/Test-ReleaseLifecycle.ps1 demands.
#
# The lifecycle tests refuse to run when they find a per-user uninstall record, the per-user startup
# entry, the default install directory or the central user-data directory, because deleting a real
# user's state is not something a test may do. On CI those artefacts are created by earlier steps of
# the same job — the release build and the self-tests run the Host, which creates
# %LOCALAPPDATA%\Vibe Flow Remote\UserData — so the tests failed instantly on a runner that has no
# user data at all. This script clears exactly those four things and reports what it removed.
#
# Every path is a parameter so the script can be exercised against a scratch root and scratch
# registry keys instead of the real profile.
param(
    [string]$LocalAppData = $env:LOCALAPPDATA,
    [string]$UninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{99C65880-071A-4F75-9238-FA4E92A2E76D}_is1",
    [string]$RunKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run",
    [string]$RunValueName = "Vibe Flow",
    [string[]]$ProcessNames = @("VibeFlow", "VoxDeckInputBridge", "VibeMicAtvvCapture")
)

$ErrorActionPreference = "Stop"
$removed = [System.Collections.Generic.List[string]]::new()

foreach ($name in $ProcessNames) {
    foreach ($process in @(Get-Process -Name $name -ErrorAction SilentlyContinue)) {
        try {
            Stop-Process -Id $process.Id -Force -ErrorAction Stop
            $removed.Add("stopped process $name ($($process.Id))")
        }
        catch {
            # A process that refuses to stop is reported by the lifecycle test's own guard instead.
        }
    }
}

foreach ($directory in @(
    (Join-Path $LocalAppData "Vibe Flow Remote"),
    (Join-Path $LocalAppData "Programs\Vibe Flow Remote")
)) {
    if (Test-Path -LiteralPath $directory) {
        Remove-Item -LiteralPath $directory -Recurse -Force -ErrorAction SilentlyContinue
        if (Test-Path -LiteralPath $directory) {
            throw "Could not remove $directory, so the lifecycle tests would still see a dirty account."
        }
        $removed.Add("removed $directory")
    }
}

if (Test-Path -LiteralPath $UninstallKey) {
    Remove-Item -LiteralPath $UninstallKey -Recurse -Force -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $UninstallKey) {
        throw "Could not remove the per-user uninstall record at $UninstallKey."
    }
    $removed.Add("removed the per-user uninstall record")
}

if (Test-Path -LiteralPath $RunKey) {
    $value = Get-ItemProperty -LiteralPath $RunKey -Name $RunValueName -ErrorAction SilentlyContinue
    if ($null -ne $value) {
        Remove-ItemProperty -LiteralPath $RunKey -Name $RunValueName -ErrorAction SilentlyContinue
        $removed.Add("removed the startup entry $RunValueName")
    }
}

if ($removed.Count -eq 0) {
    Write-Host "Lifecycle sandbox already clean."
} else {
    foreach ($item in $removed) { Write-Host ("Lifecycle sandbox: " + $item) }
}
