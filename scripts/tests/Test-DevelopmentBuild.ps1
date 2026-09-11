$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$build = Join-Path $root "BUILD_DEVELOPMENT.ps1"

& $build

foreach ($name in @(
    "VibeMic.exe",
    "VoxDeckInputBridge.exe",
    "VibeMicAtvvCapture.exe",
    "NAudio.Core.dll",
    "NAudio.Wasapi.dll"
)) {
    if (-not (Test-Path -LiteralPath (Join-Path $root $name) -PathType Leaf)) {
        throw "Development build did not produce a runnable root layout: missing $name."
    }
}

$captureHash = (Get-FileHash -Algorithm SHA256 -LiteralPath `
    (Join-Path $root "VibeMicAtvvCapture.exe")).Hash
if ($captureHash -ne "B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683") {
    throw "Development build did not preserve the frozen Capture binary."
}

Write-Host "Development build integration test passed."
