[CmdletBinding()]
param(
    [string]$DestinationRoot = "",
    [string]$StableCapturePath = "",
    [string]$NAudioCorePath = "",
    [string]$NAudioWasapiPath = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$expectedCaptureHash = "B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683"

if ([string]::IsNullOrWhiteSpace($DestinationRoot)) { $DestinationRoot = $root }
if ([string]::IsNullOrWhiteSpace($NAudioCorePath)) {
    $NAudioCorePath = Join-Path $root "tools\naudio.core.2.2.1\lib\netstandard2.0\NAudio.Core.dll"
}
if ([string]::IsNullOrWhiteSpace($NAudioWasapiPath)) {
    $NAudioWasapiPath = Join-Path $root "tools\naudio.wasapi.2.2.1\lib\netstandard2.0\NAudio.Wasapi.dll"
}

function Test-FrozenCapture([string]$Path) {
    return (Test-Path -LiteralPath $Path -PathType Leaf) -and
        (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash -eq $expectedCaptureHash
}

if ([string]::IsNullOrWhiteSpace($StableCapturePath)) {
    $StableCapturePath = @(
        $env:VIBE_FLOW_STABLE_CAPTURE_PATH,
        (Join-Path $root "release\Vibe-Flow-Windows-x64\VibeMicAtvvCapture.exe"),
        (Join-Path $env:LOCALAPPDATA "Programs\Vibe Flow Remote\VibeMicAtvvCapture.exe"),
        (Join-Path $root "VibeMicAtvvCapture.exe")
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and (Test-FrozenCapture $_) } |
        Select-Object -First 1
}
if (-not (Test-FrozenCapture $StableCapturePath)) {
    throw "Development runtime requires the frozen Capture SHA-256 $expectedCaptureHash. Run BUILD_RELEASE.ps1 first or provide -StableCapturePath."
}

$naudioDependencies = @(
    @{
        Path = $NAudioCorePath
        Name = "NAudio.Core"
        Hash = "FCF493FC47A2F478A65303886B975FBDBF714CBB1F2D79F7FCE97E4BB16B01A8"
    },
    @{
        Path = $NAudioWasapiPath
        Name = "NAudio.Wasapi"
        Hash = "618EF0E49D64E7A66DFE64BBF6AE81705B9D9683D8A9F321E5C3024D666BDF82"
    }
)
foreach ($dependency in $naudioDependencies) {
    if (-not (Test-Path -LiteralPath $dependency.Path -PathType Leaf)) {
        throw "Pinned NAudio 2.2.1 dependency is missing: $($dependency.Path). Run RESTORE_BUILD_DEPS.ps1 with user approval."
    }
    try {
        $assembly = [Reflection.AssemblyName]::GetAssemblyName(
            [IO.Path]::GetFullPath($dependency.Path))
        $token = -join ($assembly.GetPublicKeyToken() | ForEach-Object { $_.ToString("X2") })
        $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $dependency.Path).Hash
        if ($assembly.Name -ne $dependency.Name -or $assembly.Version.ToString() -ne "2.2.1.0" -or
            $token -ne "E279AA5131008A41" -or $hash -ne $dependency.Hash) {
            throw "mismatch"
        }
    }
    catch {
        throw "Development runtime rejected an unexpected NAudio identity at $($dependency.Path). Restore the pinned dependency after approval."
    }
}

function Copy-VerifiedRuntimeFile([string]$Source, [string]$Destination, [string]$ExpectedHash) {
    $sourceFullPath = [IO.Path]::GetFullPath($Source)
    $destinationFullPath = [IO.Path]::GetFullPath($Destination)
    if ($sourceFullPath.Equals($destinationFullPath, [StringComparison]::OrdinalIgnoreCase)) {
        if ((Get-FileHash -Algorithm SHA256 -LiteralPath $destinationFullPath).Hash -ne $ExpectedHash) {
            throw "Runtime verification failed: $destinationFullPath"
        }
        return
    }

    $temporaryPath = $destinationFullPath + ".tmp-" + [Guid]::NewGuid().ToString("N")
    try {
        Copy-Item -LiteralPath $sourceFullPath -Destination $temporaryPath
        if ((Get-FileHash -Algorithm SHA256 -LiteralPath $temporaryPath).Hash -ne $ExpectedHash) {
            throw "Runtime copy verification failed before activation: $destinationFullPath"
        }
        Move-Item -LiteralPath $temporaryPath -Destination $destinationFullPath -Force
        if ((Get-FileHash -Algorithm SHA256 -LiteralPath $destinationFullPath).Hash -ne $ExpectedHash) {
            throw "Runtime verification failed after activation: $destinationFullPath"
        }
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath) { Remove-Item -LiteralPath $temporaryPath -Force }
    }
}

$destinationFullRoot = [IO.Path]::GetFullPath($DestinationRoot)
New-Item -ItemType Directory -Force -Path $destinationFullRoot | Out-Null
$runtimeFiles = @(
    @{ Source = $StableCapturePath; Name = "VibeMicAtvvCapture.exe" },
    @{ Source = $NAudioCorePath; Name = "NAudio.Core.dll" },
    @{ Source = $NAudioWasapiPath; Name = "NAudio.Wasapi.dll" }
)
foreach ($runtimeFile in $runtimeFiles) {
    $sourceHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $runtimeFile.Source).Hash
    Copy-VerifiedRuntimeFile $runtimeFile.Source (Join-Path $destinationFullRoot $runtimeFile.Name) $sourceHash
}

Write-Host "Development runtime is complete at $destinationFullRoot."
Write-Host "Frozen Capture SHA-256: $expectedCaptureHash"
