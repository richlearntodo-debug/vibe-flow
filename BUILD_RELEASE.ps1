$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$releaseRoot = Join-Path $root "release"
$packageName = "Vibe-Flow-Windows-x64"
$packageDir = Join-Path $releaseRoot $packageName
$zipPath = Join-Path $releaseRoot ($packageName + ".zip")

& cmd.exe /c (Join-Path $root "BUILD_INPUT_BRIDGE.cmd")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& cmd.exe /c (Join-Path $root "BUILD_VIBE_MIC_CAPTURE.cmd")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& cmd.exe /c (Join-Path $root "BUILD_VIBE_MIC.cmd")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

New-Item -ItemType Directory -Force -Path $releaseRoot | Out-Null
if (Test-Path $packageDir) { Remove-Item -LiteralPath $packageDir -Recurse -Force }
if (Test-Path $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
New-Item -ItemType Directory -Path $packageDir | Out-Null

Copy-Item (Join-Path $root "VibeMic.exe") (Join-Path $packageDir "VibeFlow.exe")
Copy-Item (Join-Path $root "VibeMicAtvvCapture.exe") $packageDir
Copy-Item (Join-Path $root "VoxDeckInputBridge.exe") $packageDir
Copy-Item (Join-Path $root "START_VIBE_FLOW.cmd") $packageDir
Copy-Item (Join-Path $root "vibe-flow-logo.png") $packageDir
Copy-Item (Join-Path $root "QUICK_START_ZH.md") $packageDir
Copy-Item (Join-Path $root "README_VIBE_MIC.md") $packageDir
Copy-Item (Join-Path $root "LICENSE") $packageDir
Copy-Item (Join-Path $root "THIRD_PARTY_NOTICES.md") $packageDir
Copy-Item (Join-Path $root "SECURITY.md") $packageDir
Copy-Item (Join-Path $root "vibe-mic-config.default.json") (Join-Path $packageDir "vibe-mic-config.json")

Compress-Archive -Path (Join-Path $packageDir "*") -DestinationPath $zipPath -CompressionLevel Optimal
Write-Host "Built $zipPath"
