$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

function Invoke-BuildCommand([string]$ScriptName) {
    & cmd.exe /d /c (Join-Path $root $ScriptName)
    if ($LASTEXITCODE -ne 0) { throw "$ScriptName failed with exit code $LASTEXITCODE." }
}

function Invoke-SelfTest([string]$ExecutableName) {
    $path = Join-Path $root $ExecutableName
    $process = Start-Process -FilePath $path -ArgumentList "--self-test" -Wait -PassThru -WindowStyle Hidden
    if ($process.ExitCode -ne 0) { throw "$ExecutableName --self-test failed with exit code $($process.ExitCode)." }
}

Invoke-BuildCommand "BUILD_INPUT_BRIDGE.cmd"
Invoke-BuildCommand "BUILD_VIBE_MIC.cmd"
& (Join-Path $root "scripts\Prepare-DevelopmentRuntime.ps1") -DestinationRoot $root

Invoke-SelfTest "VibeMic.exe"
Invoke-SelfTest "VoxDeckInputBridge.exe"
Invoke-SelfTest "VibeMicAtvvCapture.exe"
& (Join-Path $root "scripts\tests\Test-InstallerConfigMigration.ps1")

Write-Host "Development build is ready at $root."
Write-Host "Start with: .\VibeMic.exe --background"
