[CmdletBinding()]
param(
    [string]$Root = ""
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($Root)) {
    $Root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
}
$expectedVersion = "2.0.0"
$expectedFileVersion = "2.0.0.0"
$captureSourceHash = "736017A0C7099F72F8A81755DA67E81FA7FE8BAC3C400C129CE6E30AB74137E2"
$captureBinaryHash = "B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683"
$failures = New-Object 'System.Collections.Generic.List[string]'

function Require-Equal([string]$Name, [string]$Actual, [string]$Expected) {
    if ($Actual -ne $Expected) {
        $script:failures.Add("$Name expected '$Expected', got '$Actual'.")
    }
}

$package = Get-Content -Raw -LiteralPath (Join-Path $Root "package.json") | ConvertFrom-Json
Require-Equal "package version" ([string]$package.version) $expectedVersion

foreach ($binary in @(
    @{ Name = "Host"; Path = (Join-Path $Root "VibeMic.exe") },
    @{ Name = "Bridge"; Path = (Join-Path $Root "VoxDeckInputBridge.exe") }
)) {
    if (-not (Test-Path -LiteralPath $binary.Path -PathType Leaf)) {
        $failures.Add("$($binary.Name) binary is missing: $($binary.Path)")
        continue
    }
    $version = (Get-Item -LiteralPath $binary.Path).VersionInfo
    Require-Equal "$($binary.Name) file version" ([string]$version.FileVersion) $expectedFileVersion
    if (-not ([string]$version.ProductVersion).StartsWith($expectedVersion,
            [StringComparison]::OrdinalIgnoreCase)) {
        $failures.Add("$($binary.Name) product version is not ${expectedVersion}: $($version.ProductVersion)")
    }
}

$installerSource = Get-Content -Raw -LiteralPath (Join-Path $Root "installer\VibeFlow.iss")
if ($installerSource -notmatch '(?m)^#define MyAppVersion "2\.0\.0"\s*$' -or
    $installerSource -notmatch '(?m)^VersionInfoVersion=2\.0\.0\.0\s*$') {
    $failures.Add("Installer source identity is not 2.0.0 / 2.0.0.0.")
}

$captureSourcePath = Join-Path $Root "scripts\VibeMicAtvvCapture.cs"
$captureBinaryPath = Join-Path $Root "VibeMicAtvvCapture.exe"
Require-Equal "Capture source SHA-256" `
    ((Get-FileHash -Algorithm SHA256 -LiteralPath $captureSourcePath).Hash) $captureSourceHash
Require-Equal "Capture binary SHA-256" `
    ((Get-FileHash -Algorithm SHA256 -LiteralPath $captureBinaryPath).Hash) $captureBinaryHash
Require-Equal "Capture file version" `
    ([string](Get-Item -LiteralPath $captureBinaryPath).VersionInfo.FileVersion) "1.2.1.0"

if ($failures.Count -gt 0) {
    throw "Release identity validation failed:`n - " + ($failures -join "`n - ")
}

Write-Host "Release identity validation passed for V$expectedVersion; Capture remains frozen at 1.2.1.0."
