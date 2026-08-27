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

if ($version -ne "1.2.1") {
    throw "Hardware candidate builder is pinned to V1.2.1; package.json reports $version."
}
if ($resolvedOutputRoot.StartsWith($installedRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Candidate output must not be inside the installed V1.0 directory: $installedRoot"
}

$candidateName = "Vibe-Flow-v$version-Hardware-Candidate-$stamp"
$candidateDir = Join-Path $resolvedOutputRoot $candidateName
$zipPath = Join-Path $resolvedOutputRoot ($candidateName + ".zip")

& node (Join-Path $root "scripts\validate.js")
if ($LASTEXITCODE -ne 0) { throw "Source validation failed." }

foreach ($build in @("BUILD_INPUT_BRIDGE.cmd", "BUILD_VIBE_MIC_CAPTURE.cmd", "BUILD_VIBE_MIC.cmd")) {
    & cmd.exe /d /c (Join-Path $root $build)
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $build" }
}

foreach ($test in @(
    @("VibeMic.exe", "--self-test"),
    @("VibeMicAtvvCapture.exe", "--self-test"),
    @("VoxDeckInputBridge.exe", "--self-test")
)) {
    & (Join-Path $root $test[0]) $test[1]
    if ($LASTEXITCODE -ne 0) { throw "Self-test failed: $($test[0])" }
}

New-Item -ItemType Directory -Force -Path $candidateDir | Out-Null
$candidateDocs = Join-Path $candidateDir "docs"
$candidateImages = Join-Path $candidateDocs "images"
$candidateScripts = Join-Path $candidateDir "scripts"
New-Item -ItemType Directory -Force -Path $candidateImages | Out-Null
New-Item -ItemType Directory -Force -Path $candidateScripts | Out-Null

Copy-Item (Join-Path $root "VibeMic.exe") (Join-Path $candidateDir "VibeFlow.exe")
Copy-Item (Join-Path $root "VibeMicAtvvCapture.exe") $candidateDir
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
Copy-Item (Join-Path $root "docs\USER_GUIDE_ZH.md") $candidateDocs
Copy-Item (Join-Path $root "docs\V1_2_1_TUTORIAL_ZH.md") $candidateDocs
Copy-Item (Join-Path $root "docs\VERSION_ARCHIVE_ZH.md") $candidateDocs
Copy-Item (Join-Path $root "docs\CONTINUOUS_DICTATION_ZH.md") $candidateDocs
Copy-Item (Join-Path $root "docs\V1_2_HARDWARE_ACCEPTANCE_ZH.md") $candidateDocs
Copy-Item (Join-Path $root "docs\images\*.png") $candidateImages

$manifest = [ordered]@{
    product = "Vibe Flow Remote"
    version = $version
    channel = "hardware-candidate"
    builtAt = (Get-Date).ToString("o")
    installable = $false
    hardwareAcceptancePassed = $false
    recordingKernel = "v1.0.3"
    recordingInteraction = "hold-to-talk; natural RC003 stop; approximately 60 seconds"
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
Write-Host "Installed V1.0 directory was not modified: $installedRoot"
