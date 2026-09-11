[CmdletBinding()]
param(
    [string]$Root = ""
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($Root)) {
    $Root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
}
$releaseRoot = Join-Path $Root "release"
$packageRoot = Join-Path $releaseRoot "Vibe-Flow-Windows-x64"
$zipPath = Join-Path $releaseRoot "Vibe-Flow-Windows-x64.zip"
$installerPath = Join-Path $releaseRoot "VibeFlow-Setup.exe"
$checksumPath = Join-Path $releaseRoot "SHA256SUMS.txt"
$releaseBodyPath = Join-Path $releaseRoot "RELEASE_BODY_v2.0.0.md"
$captureHash = "B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683"
$requiredDocs = @(
    "V2_0_USER_GUIDE_ZH.md",
    "V2_0_CONFIGURATION_MIGRATION_ZH.md",
    "V2_0_AUTOMATED_TEST_REPORT_ZH.md",
    "V2_0_HARDWARE_TEST_MATRIX_ZH.md",
    "V2_0_KNOWN_LIMITATIONS_ZH.md",
    "V2_0_ROLLBACK_ZH.md",
    "V2_0_RELEASE_NOTES_ZH.md",
    "V2_0_INSTALLER_GUIDE_ZH.md"
)
$requiredImages = @(
    "01-overview.png",
    "02-dictation.png",
    "03-shortcuts.png",
    "04-diagnostics.png",
    "05-settings.png"
)

foreach ($path in @($packageRoot, $zipPath, $installerPath, $checksumPath, $releaseBodyPath)) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Required release artifact is missing: $path" }
}

foreach ($document in $requiredDocs) {
    if (-not (Test-Path -LiteralPath (Join-Path $Root ("docs\" + $document)) -PathType Leaf)) {
        throw "Required V2 document is missing from the repository: $document"
    }
    if (-not (Test-Path -LiteralPath (Join-Path $packageRoot ("docs\" + $document)) -PathType Leaf)) {
        throw "Required V2 document is missing from the package: $document"
    }
}
foreach ($image in $requiredImages) {
    if (-not (Test-Path -LiteralPath (Join-Path $Root ("docs\images\" + $image)) -PathType Leaf) -or
        -not (Test-Path -LiteralPath (Join-Path $packageRoot ("docs\images\" + $image)) -PathType Leaf)) {
        throw "Required V2 screenshot is missing: $image"
    }
}

$hostVersion = (Get-Item -LiteralPath (Join-Path $packageRoot "VibeFlow.exe")).VersionInfo
$bridgeVersion = (Get-Item -LiteralPath (Join-Path $packageRoot "VoxDeckInputBridge.exe")).VersionInfo
$packagedCapture = Join-Path $packageRoot "VibeMicAtvvCapture.exe"
if (-not $hostVersion.FileVersion.StartsWith("2.0.0") -or
    -not $bridgeVersion.FileVersion.StartsWith("2.0.0")) {
    throw "Packaged Host or Bridge is not V2.0.0."
}
if ((Get-Item -LiteralPath $packagedCapture).VersionInfo.FileVersion -ne "1.2.1.0" -or
    (Get-FileHash -Algorithm SHA256 -LiteralPath $packagedCapture).Hash -ne $captureHash) {
    throw "Packaged Capture identity changed."
}

foreach ($mapping in @(
    @{ Root = (Join-Path $Root "VibeMic.exe"); Package = (Join-Path $packageRoot "VibeFlow.exe") },
    @{ Root = (Join-Path $Root "VoxDeckInputBridge.exe"); Package = (Join-Path $packageRoot "VoxDeckInputBridge.exe") }
)) {
    if ((Get-FileHash -Algorithm SHA256 -LiteralPath $mapping.Root).Hash -ne
        (Get-FileHash -Algorithm SHA256 -LiteralPath $mapping.Package).Hash) {
        throw "Root and packaged binary hashes differ: $($mapping.Package)"
    }
}

$checksumLines = @(Get-Content -LiteralPath $checksumPath | Where-Object { $_.Trim().Length -gt 0 })
if ($checksumLines.Count -ne 2) { throw "SHA256SUMS.txt must contain exactly installer and ZIP entries." }
foreach ($artifact in @($installerPath, $zipPath)) {
    $name = Split-Path -Leaf $artifact
    $expected = ($checksumLines | Where-Object { $_ -match ("\s+\*?" + [regex]::Escape($name) + "$") } |
        Select-Object -First 1)
    if ([string]::IsNullOrWhiteSpace($expected)) { throw "Checksum entry is missing: $name" }
    $expectedHash = ([regex]::Match($expected, '^[0-9A-Fa-f]{64}')).Value.ToUpperInvariant()
    if ((Get-FileHash -Algorithm SHA256 -LiteralPath $artifact).Hash -ne $expectedHash) {
        throw "Checksum mismatch: $name"
    }
}

$releaseBody = Get-Content -Raw -LiteralPath $releaseBodyPath
if ($releaseBody -notmatch '2\.0\.0' -or $releaseBody -notmatch '(?i)candidate') {
    throw "Release body does not identify the V2.0.0 candidate status."
}

$forbiddenPackageFiles = @(
    "vibe-mic-config.json",
    "VibeMicAtvvCapture.cs",
    "BUILD_VIBE_MIC_CAPTURE.cmd"
)
foreach ($name in $forbiddenPackageFiles) {
    if (Get-ChildItem -LiteralPath $packageRoot -Recurse -File -Filter $name -ErrorAction SilentlyContinue) {
        throw "Forbidden file was included in the package: $name"
    }
}

$expandRoot = Join-Path ([IO.Path]::GetTempPath()) ("vibe-flow-release-test-" + [Guid]::NewGuid().ToString("N"))
try {
    Expand-Archive -LiteralPath $zipPath -DestinationPath $expandRoot
    foreach ($relative in @("VibeFlow.exe", "VoxDeckInputBridge.exe", "VibeMicAtvvCapture.exe",
        "NAudio.Core.dll", "NAudio.Wasapi.dll")) {
        $packaged = Join-Path $packageRoot $relative
        $zipped = Join-Path $expandRoot $relative
        if (-not (Test-Path -LiteralPath $zipped -PathType Leaf) -or
            (Get-FileHash -Algorithm SHA256 -LiteralPath $packaged).Hash -ne
            (Get-FileHash -Algorithm SHA256 -LiteralPath $zipped).Hash) {
            throw "ZIP payload differs from the package directory: $relative"
        }
    }
}
finally {
    if (Test-Path -LiteralPath $expandRoot) {
        Remove-Item -LiteralPath $expandRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "V2.0.0 release artifact validation passed."
