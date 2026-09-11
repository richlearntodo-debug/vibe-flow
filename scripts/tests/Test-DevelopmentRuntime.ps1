$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$prepare = Join-Path $root "scripts\Prepare-DevelopmentRuntime.ps1"
$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ("vibe-flow-dev-runtime-test-" + [Guid]::NewGuid().ToString("N"))
$destination = Join-Path $fixtureRoot "runtime"
$invalidCapture = Join-Path $fixtureRoot "invalid-capture.exe"
$tamperedNAudioCore = Join-Path $fixtureRoot "NAudio.Core.dll"
$stableCapture = Join-Path $root "release\Vibe-Flow-Windows-x64\VibeMicAtvvCapture.exe"
$naudioCore = Join-Path $root "tools\naudio.core.2.2.1\lib\netstandard2.0\NAudio.Core.dll"
$naudioWasapi = Join-Path $root "tools\naudio.wasapi.2.2.1\lib\netstandard2.0\NAudio.Wasapi.dll"
$expectedCaptureHash = "B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683"

try {
    New-Item -ItemType Directory -Force -Path $fixtureRoot | Out-Null
    & $prepare -DestinationRoot $destination -StableCapturePath $stableCapture `
        -NAudioCorePath $naudioCore -NAudioWasapiPath $naudioWasapi

    foreach ($name in @("VibeMicAtvvCapture.exe", "NAudio.Core.dll", "NAudio.Wasapi.dll")) {
        if (-not (Test-Path -LiteralPath (Join-Path $destination $name) -PathType Leaf)) {
            throw "Development runtime preparation omitted $name."
        }
    }
    $actualCaptureHash = (Get-FileHash -Algorithm SHA256 -LiteralPath `
        (Join-Path $destination "VibeMicAtvvCapture.exe")).Hash
    if ($actualCaptureHash -ne $expectedCaptureHash) {
        throw "Development runtime preparation changed the frozen Capture binary."
    }
    if ((Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $destination "NAudio.Core.dll")).Hash -ne
        (Get-FileHash -Algorithm SHA256 -LiteralPath $naudioCore).Hash -or
        (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $destination "NAudio.Wasapi.dll")).Hash -ne
        (Get-FileHash -Algorithm SHA256 -LiteralPath $naudioWasapi).Hash) {
        throw "Development runtime preparation did not copy the pinned NAudio files byte-for-byte."
    }

    [IO.File]::WriteAllBytes($invalidCapture, [byte[]](1, 2, 3, 4))
    $invalidRejected = $false
    try {
        & $prepare -DestinationRoot (Join-Path $fixtureRoot "invalid-runtime") `
            -StableCapturePath $invalidCapture -NAudioCorePath $naudioCore -NAudioWasapiPath $naudioWasapi
    }
    catch {
        $invalidRejected = $_.Exception.Message -like "*frozen Capture SHA-256*"
    }
    if (-not $invalidRejected) {
        throw "Development runtime preparation accepted an unverified Capture binary."
    }

    Copy-Item -LiteralPath $naudioCore -Destination $tamperedNAudioCore
    $tamperedStream = [IO.File]::Open($tamperedNAudioCore, [IO.FileMode]::Append,
        [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $tamperedStream.WriteByte(0) } finally { $tamperedStream.Dispose() }
    $tamperedDependencyRejected = $false
    try {
        & $prepare -DestinationRoot (Join-Path $fixtureRoot "tampered-runtime") `
            -StableCapturePath $stableCapture -NAudioCorePath $tamperedNAudioCore `
            -NAudioWasapiPath $naudioWasapi
    }
    catch {
        $tamperedDependencyRejected = $_.Exception.Message -like "*NAudio identity*"
    }
    if (-not $tamperedDependencyRejected) {
        throw "Development runtime preparation accepted a modified NAudio binary with the expected file version."
    }

    Write-Host "Development runtime layout test passed."
}
finally {
    if (Test-Path -LiteralPath $fixtureRoot) {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
    }
}
