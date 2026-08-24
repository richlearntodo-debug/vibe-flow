$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$releaseRoot = Join-Path $root "release"
$packageName = "Vibe-Flow-Windows-x64"
$packageDir = Join-Path $releaseRoot $packageName
$zipPath = Join-Path $releaseRoot ($packageName + ".zip")
$installerPath = Join-Path $releaseRoot "VibeFlow-Setup.exe"
$checksumPath = Join-Path $releaseRoot "SHA256SUMS.txt"

& cmd.exe /c (Join-Path $root "BUILD_INPUT_BRIDGE.cmd")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& cmd.exe /c (Join-Path $root "BUILD_VIBE_MIC_CAPTURE.cmd")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& cmd.exe /c (Join-Path $root "BUILD_VIBE_MIC.cmd")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

New-Item -ItemType Directory -Force -Path $releaseRoot | Out-Null
if (Test-Path $packageDir) { Remove-Item -LiteralPath $packageDir -Recurse -Force }
if (Test-Path $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
if (Test-Path $installerPath) { Remove-Item -LiteralPath $installerPath -Force }
if (Test-Path $checksumPath) { Remove-Item -LiteralPath $checksumPath -Force }
New-Item -ItemType Directory -Path $packageDir | Out-Null

Copy-Item (Join-Path $root "VibeMic.exe") (Join-Path $packageDir "VibeFlow.exe")
Copy-Item (Join-Path $root "VibeMicAtvvCapture.exe") $packageDir
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

$packageDocs = Join-Path $packageDir "docs"
$packageImages = Join-Path $packageDocs "images"
New-Item -ItemType Directory -Force -Path $packageImages | Out-Null
Copy-Item (Join-Path $root "docs\USER_GUIDE_ZH.md") $packageDocs
Copy-Item (Join-Path $root "docs\RELEASE_NOTES_V1_ZH.md") $packageDocs
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

$hashLines = @($installerPath, $zipPath) | ForEach-Object {
    $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $_
    $hash.Hash + "  " + (Split-Path -Leaf $_)
}
[System.IO.File]::WriteAllLines($checksumPath, $hashLines, [System.Text.UTF8Encoding]::new($false))

Write-Host "Built $installerPath"
Write-Host "Built $zipPath"
Write-Host "Built $checksumPath"
