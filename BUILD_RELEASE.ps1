$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$releaseRoot = Join-Path $root "release"
$releaseVersion = (Get-Content -LiteralPath (Join-Path $root "package.json") -Raw | ConvertFrom-Json).version
$packageName = "Vibe-Flow-Windows-x64"
$packageDir = Join-Path $releaseRoot $packageName
$zipPath = Join-Path $releaseRoot ($packageName + ".zip")
$installerPath = Join-Path $releaseRoot "VibeFlow-Setup.exe"
$checksumPath = Join-Path $releaseRoot "SHA256SUMS.txt"
$releaseBodySource = Join-Path $root "docs\GITHUB_RELEASE_BODY_ZH.md"
$releaseBodyPath = Join-Path $releaseRoot ("RELEASE_BODY_v" + $releaseVersion + ".md")
$stableCapturePath = Join-Path ([IO.Path]::GetTempPath()) "VibeFlow-StableCapture-v1.2.1.exe"
$signingThumbprint = if ($env:VIBE_FLOW_SIGN_THUMBPRINT) { $env:VIBE_FLOW_SIGN_THUMBPRINT.Trim() } else { "" }
$signingPfx = if ($env:VIBE_FLOW_SIGN_PFX) { $env:VIBE_FLOW_SIGN_PFX.Trim() } else { "" }
$timestampUrl = if ($env:VIBE_FLOW_TIMESTAMP_URL) { $env:VIBE_FLOW_TIMESTAMP_URL.Trim() } else { "http://timestamp.digicert.com" }
$signingRequested = -not [string]::IsNullOrWhiteSpace($signingThumbprint) -or -not [string]::IsNullOrWhiteSpace($signingPfx)

function Resolve-SignTool {
    $command = Get-Command signtool.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -ErrorAction SilentlyContinue
    if ($command -and (Test-Path -LiteralPath $command)) { return $command }
    $kitsRoot = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin"
    if (Test-Path -LiteralPath $kitsRoot) {
        $candidate = Get-ChildItem -LiteralPath $kitsRoot -Directory -ErrorAction SilentlyContinue |
            Sort-Object Name -Descending |
            ForEach-Object { Join-Path $_.FullName "x64\signtool.exe" } |
            Where-Object { Test-Path -LiteralPath $_ } |
            Select-Object -First 1
        if ($candidate) { return $candidate }
    }
    throw "Windows SDK signtool.exe was not found. Install the Windows 10/11 SDK before signing."
}

function Invoke-VibeFlowCodeSign([string]$Path) {
    if (-not $script:signingRequested) { return }
    if (-not (Test-Path -LiteralPath $Path)) { throw "Signing target not found: $Path" }
    $arguments = @("sign", "/fd", "SHA256", "/tr", $script:timestampUrl, "/td", "SHA256")
    if (-not [string]::IsNullOrWhiteSpace($script:signingThumbprint)) {
        $arguments += @("/s", "My", "/sha1", $script:signingThumbprint.Replace(" ", ""))
        if ($env:VIBE_FLOW_SIGN_STORE -and $env:VIBE_FLOW_SIGN_STORE.Equals("machine", [StringComparison]::OrdinalIgnoreCase)) {
            $arguments += "/sm"
        }
    }
    else {
        if (-not (Test-Path -LiteralPath $script:signingPfx)) { throw "Signing PFX not found: $script:signingPfx" }
        $arguments += @("/f", (Resolve-Path -LiteralPath $script:signingPfx).Path)
        if ($null -ne $env:VIBE_FLOW_SIGN_PFX_PASSWORD) { $arguments += @("/p", $env:VIBE_FLOW_SIGN_PFX_PASSWORD) }
    }
    $arguments += (Resolve-Path -LiteralPath $Path).Path
    & $script:signTool @arguments
    if ($LASTEXITCODE -ne 0) { throw "Code signing failed: $Path" }
    & $script:signTool verify /pa /all /v (Resolve-Path -LiteralPath $Path).Path
    if ($LASTEXITCODE -ne 0) { throw "Authenticode verification failed: $Path" }
    Write-Host "Signed and verified $Path"
}

if ($signingRequested) {
    if (-not [string]::IsNullOrWhiteSpace($signingThumbprint) -and -not [string]::IsNullOrWhiteSpace($signingPfx)) {
        throw "Choose either VIBE_FLOW_SIGN_THUMBPRINT or VIBE_FLOW_SIGN_PFX, not both."
    }
    $signTool = Resolve-SignTool
    Write-Host "Authenticode signing enabled. Timestamp server: $timestampUrl"
}
else {
    Write-Warning "Authenticode signing is not configured. Building unsigned local artifacts. See docs/CODE_SIGNING_ZH.md."
}

& cmd.exe /c (Join-Path $root "BUILD_INPUT_BRIDGE.cmd")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& cmd.exe /c (Join-Path $root "BUILD_VIBE_MIC.cmd")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$stableCapturePath = & (Join-Path $root "scripts\Get-StableCaptureBinary.ps1") -Destination $stableCapturePath
if (-not (Test-Path -LiteralPath $stableCapturePath)) { throw "Pinned stable capture resolution failed." }

foreach ($test in @(
    @("VibeMic.exe", "--self-test"),
    @("VoxDeckInputBridge.exe", "--self-test")
)) {
    & (Join-Path $root $test[0]) $test[1]
    if ($LASTEXITCODE -ne 0) { throw "Self-test failed: $($test[0])" }
}
& $stableCapturePath --self-test
if ($LASTEXITCODE -ne 0) { throw "Self-test failed: verified VibeMicAtvvCapture.exe" }

@("VibeMic.exe", "VoxDeckInputBridge.exe") |
    ForEach-Object { Invoke-VibeFlowCodeSign (Join-Path $root $_) }

New-Item -ItemType Directory -Force -Path $releaseRoot | Out-Null
Get-ChildItem -LiteralPath $releaseRoot -Filter "RELEASE_BODY_v*.md" -File -ErrorAction SilentlyContinue |
    Remove-Item -Force
if (Test-Path $packageDir) { Remove-Item -LiteralPath $packageDir -Recurse -Force }
if (Test-Path $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
if (Test-Path $installerPath) { Remove-Item -LiteralPath $installerPath -Force }
if (Test-Path $checksumPath) { Remove-Item -LiteralPath $checksumPath -Force }
New-Item -ItemType Directory -Path $packageDir | Out-Null

Copy-Item (Join-Path $root "VibeMic.exe") (Join-Path $packageDir "VibeFlow.exe")
Copy-Item -LiteralPath $stableCapturePath -Destination (Join-Path $packageDir "VibeMicAtvvCapture.exe")
Copy-Item (Join-Path $root "NAudio.Core.dll") $packageDir
Copy-Item (Join-Path $root "NAudio.Wasapi.dll") $packageDir
Copy-Item (Join-Path $root "VoxDeckInputBridge.exe") $packageDir
Copy-Item (Join-Path $root "START_VIBE_FLOW.cmd") $packageDir
Copy-Item (Join-Path $root "vibe-flow-logo.png") $packageDir
Copy-Item (Join-Path $root "QUICK_START_ZH.md") $packageDir
Copy-Item (Join-Path $root "README_VIBE_MIC.md") $packageDir
Copy-Item (Join-Path $root "VIBE_MIC_VERSION.md") $packageDir
Copy-Item (Join-Path $root "CHANGELOG.md") $packageDir
Copy-Item (Join-Path $root "LICENSE") $packageDir
Copy-Item (Join-Path $root "THIRD_PARTY_NOTICES.md") $packageDir
Copy-Item (Join-Path $root "SECURITY.md") $packageDir
Copy-Item (Join-Path $root "vibe-mic-config.default.json") $packageDir
New-Item -ItemType Directory -Force -Path (Join-Path $packageDir "scripts") | Out-Null
Copy-Item (Join-Path $root "scripts\Install-VBCable.ps1") (Join-Path $packageDir "scripts")

$packageDocs = Join-Path $packageDir "docs"
$packageImages = Join-Path $packageDocs "images"
New-Item -ItemType Directory -Force -Path $packageImages | Out-Null
Copy-Item (Join-Path $root "docs\USER_GUIDE_ZH.md") $packageDocs
Copy-Item (Join-Path $root "docs\V1_2_1_TUTORIAL_ZH.md") $packageDocs
Copy-Item (Join-Path $root "docs\V1_3_USER_GUIDE_ZH.md") $packageDocs
Copy-Item (Join-Path $root "docs\VERSION_ARCHIVE_ZH.md") $packageDocs
Copy-Item (Join-Path $root "docs\RELEASE_NOTES_ZH.md") $packageDocs
Copy-Item (Join-Path $root "docs\CONTINUOUS_DICTATION_ZH.md") $packageDocs
Copy-Item (Join-Path $root "docs\CODE_SIGNING_ZH.md") $packageDocs
Copy-Item (Join-Path $root "docs\images\*.png") $packageImages

Compress-Archive -Path (Join-Path $packageDir "*") -DestinationPath $zipPath -CompressionLevel Optimal

$isccCandidates = @(
    (Get-Command ISCC.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -ErrorAction SilentlyContinue),
    (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"),
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -Unique

$iscc = $isccCandidates | Select-Object -First 1
if (-not $iscc) {
    throw "Inno Setup 6 was not found. Install JRSoftware.InnoSetup before building the formal release."
}

& $iscc (Join-Path $root "installer\VibeFlow.iss")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Invoke-VibeFlowCodeSign $installerPath

$hashLines = @($installerPath, $zipPath) | ForEach-Object {
    $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $_
    $hash.Hash + "  " + (Split-Path -Leaf $_)
}
[System.IO.File]::WriteAllLines($checksumPath, $hashLines, [System.Text.UTF8Encoding]::new($false))
Copy-Item -LiteralPath $releaseBodySource -Destination $releaseBodyPath

Write-Host "Built $installerPath"
Write-Host "Built $zipPath"
Write-Host "Built $checksumPath"
Write-Host "Built $releaseBodyPath"
