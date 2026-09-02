[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Release",

    [ValidateSet("x64")]
    [string] $Platform = "x64",

    [string] $OutputRoot = ""
)

$ErrorActionPreference = "Stop"
$driverRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $driverRoot "..\.."))
$buildScript = Join-Path $driverRoot "Build-Driver.ps1"
$packageRoot = Join-Path $driverRoot ("src\" + $Platform + "\" + $Configuration)
$exactHardwareId = "HID\{00001812-0000-1000-8000-00805F9B34FB}_Dev_VID&012717_PID&32B8_REV&00A4"

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repositoryRoot "artifacts\driver-candidate"
}
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)

& $buildScript -Configuration $Configuration -Platform $Platform | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw "RC003 filter build failed"
}

$inf = Get-ChildItem -LiteralPath $packageRoot -Filter "VibeFlowRc003Filter.inf" -File -Recurse |
    Select-Object -First 1
$sys = Get-ChildItem -LiteralPath $packageRoot -Filter "VibeFlowRc003Filter.sys" -File -Recurse |
    Select-Object -First 1
$cat = Get-ChildItem -LiteralPath $packageRoot -Filter "VibeFlowRc003Filter.cat" -File -Recurse |
    Select-Object -First 1
if ($null -eq $inf -or $null -eq $sys -or $null -eq $cat) {
    throw "The driver build did not produce the expected INF, SYS, and CAT files"
}

$infText = Get-Content -Raw -LiteralPath $inf.FullName
if (-not $infText.Contains($exactHardwareId)) {
    throw "Driver INF is not pinned to the exact RC003 hardware ID"
}
if (-not $infText.Contains('HKR,,UpperFilters,0x00010008,"VibeFlowRc003Filter"')) {
    throw "Driver INF does not append its per-device UpperFilters entry"
}
if ($infText.Contains('Class\{4D36E96B-E325-11CE-BFC1-08002BE10318}\UpperFilters')) {
    throw "Driver INF attempts to modify the keyboard class filter"
}

$package = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot "package.json") | ConvertFrom-Json
$stamp = (Get-Date).ToUniversalTime().ToString("yyyyMMdd-HHmmss")
$candidateName = "VibeFlow-RC003-DriverLab-v$($package.version)-$stamp"
$candidateDirectory = Join-Path $OutputRoot $candidateName
New-Item -ItemType Directory -Path $candidateDirectory -ErrorAction Stop | Out-Null

foreach ($file in @($inf, $sys, $cat)) {
    Copy-Item -LiteralPath $file.FullName -Destination $candidateDirectory
}

$warning = @"
VIBE FLOW RC003 DRIVER LAB CANDIDATE

This package is not approved for production installation or public release.
It must be used only on a separate Windows driver-test computer.

Required before release:
- Microsoft-signed catalog
- Secure Boot and Memory Integrity validation
- sleep, wake, Bluetooth reconnect, and uninstall recovery tests
- 10,000-event stress test
- physical keyboard regression test

The normal Vibe Flow release and installer intentionally exclude this driver.
"@
[IO.File]::WriteAllText(
    (Join-Path $candidateDirectory "TEST_ONLY.txt"),
    $warning,
    [Text.UTF8Encoding]::new($false))

$sourceCommit = ""
$sourceDirty = $true
if (Get-Command git.exe -ErrorAction SilentlyContinue) {
    $sourceCommit = [string](& git.exe -C $repositoryRoot rev-parse HEAD 2>$null)
    $sourceCommit = $sourceCommit.Trim()
    $sourceDirty = @(& git.exe -C $repositoryRoot status --porcelain).Count -gt 0
}

$catalogSignature = Get-AuthenticodeSignature -LiteralPath (Join-Path $candidateDirectory $cat.Name)
$payloadHashes = [ordered]@{}
Get-ChildItem -LiteralPath $candidateDirectory -File |
    Sort-Object Name |
    ForEach-Object {
        $payloadHashes[$_.Name] = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
    }

$manifest = [ordered]@{
    product = "Vibe Flow RC003 input filter"
    appVersion = [string]$package.version
    channel = "driver-lab-candidate"
    builtAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    sourceCommit = $sourceCommit
    sourceDirty = $sourceDirty
    configuration = $Configuration
    platform = $Platform
    exactHardwareId = $exactHardwareId
    filterScope = "exact-device-upper-filter-append"
    heartbeatFailOpenMs = 2000
    catalogSignatureStatus = [string]$catalogSignature.Status
    microsoftSigned = $false
    productionInstallApproved = $false
    releaseApproved = $false
    payloadSha256 = $payloadHashes
}
[IO.File]::WriteAllText(
    (Join-Path $candidateDirectory "DRIVER_CANDIDATE_MANIFEST.json"),
    ($manifest | ConvertTo-Json -Depth 5),
    [Text.UTF8Encoding]::new($false))

$hashLines = Get-ChildItem -LiteralPath $candidateDirectory -File |
    Where-Object { $_.Name -ne "SHA256SUMS.txt" } |
    Sort-Object Name |
    ForEach-Object {
        $hash = Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256
        $hash.Hash + "  " + $_.Name
    }
[IO.File]::WriteAllLines(
    (Join-Path $candidateDirectory "SHA256SUMS.txt"),
    $hashLines,
    [Text.UTF8Encoding]::new($false))

if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_OUTPUT)) {
    "candidate_directory=$candidateDirectory" |
        Out-File -LiteralPath $env:GITHUB_OUTPUT -Encoding utf8 -Append
}

Write-Host "Driver Lab candidate: $candidateDirectory"
Write-Host "Production install approved: false"
