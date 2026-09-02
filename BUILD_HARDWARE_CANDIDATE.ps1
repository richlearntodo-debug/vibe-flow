[CmdletBinding()]
param(
    [string]$OutputRoot = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $root "artifacts"
}
$package = Get-Content -LiteralPath (Join-Path $root "package.json") -Raw | ConvertFrom-Json
$version = [string]$package.version
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$installedRoot = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA "Programs\Vibe Flow Remote"))
$resolvedOutputRoot = [IO.Path]::GetFullPath($OutputRoot)
$stableCapturePath = Join-Path ([IO.Path]::GetTempPath()) "VibeFlow-StableCapture-v1.2.1.exe"
$stableCaptureSha256 = "B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683"

if ($version -ne "1.4.0") {
    throw "Hardware candidate builder is pinned to V1.4.0; package.json reports $version."
}
if ($resolvedOutputRoot.StartsWith($installedRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Candidate output must not be inside the installed V1.0 directory: $installedRoot"
}
$stableCapturePath = & (Join-Path $root "scripts\Get-StableCaptureBinary.ps1") -Destination $stableCapturePath
if (-not (Test-Path -LiteralPath $stableCapturePath)) { throw "Pinned stable capture resolution failed." }
$actualCaptureSha256 = (Get-FileHash -LiteralPath $stableCapturePath -Algorithm SHA256).Hash
if ($actualCaptureSha256 -ne $stableCaptureSha256) {
    throw "The verified capture binary hash changed: $actualCaptureSha256"
}

$candidateName = "Vibe-Flow-v$version-Hardware-Candidate-$stamp"
$candidateDir = Join-Path $resolvedOutputRoot $candidateName
$zipPath = Join-Path $resolvedOutputRoot ($candidateName + ".zip")

& node (Join-Path $root "scripts\validate.js")
if ($LASTEXITCODE -ne 0) { throw "Source validation failed." }

foreach ($build in @("BUILD_INPUT_BRIDGE.cmd", "BUILD_VIBE_MIC.cmd")) {
    & cmd.exe /d /c (Join-Path $root $build)
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $build" }
}

foreach ($test in @(
    @("VibeMic.exe", "--self-test"),
    @("VoxDeckInputBridge.exe", "--self-test")
)) {
    & (Join-Path $root $test[0]) $test[1]
    if ($LASTEXITCODE -ne 0) { throw "Self-test failed: $($test[0])" }
}
& $stableCapturePath --self-test
if ($LASTEXITCODE -ne 0) { throw "Self-test failed: verified VibeMicAtvvCapture.exe" }

New-Item -ItemType Directory -Force -Path $candidateDir | Out-Null
$candidateDocs = Join-Path $candidateDir "docs"
$candidateImages = Join-Path $candidateDocs "images"
$candidateScripts = Join-Path $candidateDir "scripts"
New-Item -ItemType Directory -Force -Path $candidateImages | Out-Null
New-Item -ItemType Directory -Force -Path $candidateScripts | Out-Null

Copy-Item (Join-Path $root "VibeMic.exe") (Join-Path $candidateDir "VibeFlow.exe")
Copy-Item -LiteralPath $stableCapturePath -Destination (Join-Path $candidateDir "VibeMicAtvvCapture.exe")
Copy-Item (Join-Path $root "VoxDeckInputBridge.exe") $candidateDir
Copy-Item (Join-Path $root "NAudio.Core.dll") $candidateDir
Copy-Item (Join-Path $root "NAudio.Wasapi.dll") $candidateDir
Copy-Item (Join-Path $root "START_VIBE_FLOW.cmd") $candidateDir
Copy-Item (Join-Path $root "vibe-flow-logo.png") $candidateDir
Copy-Item (Join-Path $root "vibe-mic-config.default.json") $candidateDir
Copy-Item (Join-Path $root "QUICK_START_ZH.md") $candidateDir
Copy-Item (Join-Path $root "README_VIBE_MIC.md") $candidateDir
Copy-Item (Join-Path $root "VIBE_MIC_VERSION.md") $candidateDir
Copy-Item (Join-Path $root "CHANGELOG.md") $candidateDir
Copy-Item (Join-Path $root "LICENSE") $candidateDir
Copy-Item (Join-Path $root "THIRD_PARTY_NOTICES.md") $candidateDir
Copy-Item (Join-Path $root "SECURITY.md") $candidateDir
Copy-Item (Join-Path $root "scripts\Install-VBCable.ps1") $candidateScripts
Copy-Item (Join-Path $root "scripts\Measure-HardwareAcceptance.ps1") $candidateScripts
Copy-Item (Join-Path $root "docs\USER_GUIDE_ZH.md") $candidateDocs
Copy-Item (Join-Path $root "docs\V1_2_1_TUTORIAL_ZH.md") $candidateDocs
Copy-Item (Join-Path $root "docs\V1_3_USER_GUIDE_ZH.md") $candidateDocs
Copy-Item (Join-Path $root "docs\VERSION_ARCHIVE_ZH.md") $candidateDocs
Copy-Item (Join-Path $root "docs\CONTINUOUS_DICTATION_ZH.md") $candidateDocs
Copy-Item (Join-Path $root "docs\V1_2_HARDWARE_ACCEPTANCE_ZH.md") $candidateDocs
Copy-Item (Join-Path $root "docs\V1_3_HARDWARE_ACCEPTANCE_ZH.md") $candidateDocs
Copy-Item (Join-Path $root "docs\V1_3_PREVIEW_ZH.md") $candidateDocs
Copy-Item (Join-Path $root "docs\V1_3_INPUT_ROUTING_ROOT_CAUSE_ZH.md") $candidateDocs
Copy-Item (Join-Path $root "docs\V1_4_PREVIEW_ZH.md") $candidateDocs
Copy-Item (Join-Path $root "docs\images\*.png") $candidateImages

$manifest = [ordered]@{
    product = "Vibe Flow Remote"
    version = $version
    channel = "hardware-candidate"
    builtAt = (Get-Date).ToString("o")
    installable = $false
    hardwareAcceptancePassed = $false
    configurationSchema = 31
    bridgeConfigurationSchema = 6
    recordingKernel = "v1.0.3"
    stableCaptureSha256 = $stableCaptureSha256
    recordingInteraction = "hold-to-talk; natural RC003 stop; approximately 60 seconds"
    nonVoiceRouting = "device-scoped Raw Input with native passthrough fallback"
    exactDeviceIsolation = "optional Microsoft-signed filter; not required for Raw Input action execution"
    powerKeySupport = "unsupported-no-stable-windows-event"
    stableVoiceProfile = [ordered]@{
        gain = 1.0
        processing = "speech"
        drainMs = 180
        endpoint = "CABLE Input"
        automaticRouting = $true
    }
}
$manifestJson = $manifest | ConvertTo-Json -Depth 4
[IO.File]::WriteAllText(
    (Join-Path $candidateDir "CANDIDATE_MANIFEST.json"),
    $manifestJson,
    [Text.UTF8Encoding]::new($false))

$hashLines = Get-ChildItem -LiteralPath $candidateDir -Recurse -File |
    Where-Object { $_.Name -ne "SHA256SUMS.txt" } |
    Sort-Object FullName |
    ForEach-Object {
        $relative = $_.FullName.Substring($candidateDir.Length).TrimStart([char[]]@("\", "/")).Replace("\", "/")
        $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName
        $hash.Hash + "  " + $relative
    }
[IO.File]::WriteAllLines(
    (Join-Path $candidateDir "SHA256SUMS.txt"),
    $hashLines,
    [Text.UTF8Encoding]::new($false))

Compress-Archive -Path (Join-Path $candidateDir "*") -DestinationPath $zipPath -CompressionLevel Optimal

Write-Host "Hardware candidate directory: $candidateDir"
Write-Host "Hardware candidate archive:   $zipPath"
Write-Host "Installed stable-release directory was not modified: $installedRoot"
