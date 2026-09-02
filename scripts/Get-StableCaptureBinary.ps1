[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Destination
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$expectedSha256 = "B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683"
$releaseUrl = "https://github.com/richlearntodo-debug/vibe-flow/releases/download/v1.2.1/Vibe-Flow-Windows-x64.zip"
$candidates = @(
    $env:VIBE_FLOW_STABLE_CAPTURE_PATH,
    (Join-Path $root "release\Vibe-Flow-Windows-x64\VibeMicAtvvCapture.exe"),
    (Join-Path $env:LOCALAPPDATA "Programs\Vibe Flow Remote\VibeMicAtvvCapture.exe"),
    (Join-Path $root "VibeMicAtvvCapture.exe")
) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

function Test-StableCapture([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $false }
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -eq $expectedSha256
}

$source = $candidates | Where-Object { Test-StableCapture $_ } | Select-Object -First 1
$temporaryRoot = ""
try {
    if (-not $source) {
        $temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) ("vibe-flow-stable-capture-" + [Guid]::NewGuid().ToString("N"))
        $archive = Join-Path $temporaryRoot "v1.2.1.zip"
        $expanded = Join-Path $temporaryRoot "expanded"
        New-Item -ItemType Directory -Force -Path $temporaryRoot | Out-Null
        Invoke-WebRequest -UseBasicParsing -Uri $releaseUrl -OutFile $archive
        Expand-Archive -LiteralPath $archive -DestinationPath $expanded -Force
        $source = Get-ChildItem -LiteralPath $expanded -Filter "VibeMicAtvvCapture.exe" -File -Recurse |
            Where-Object { Test-StableCapture $_.FullName } |
            Select-Object -First 1 -ExpandProperty FullName
        if (-not $source) {
            throw "The v1.2.1 release did not contain the pinned stable capture binary."
        }
    }

    $destinationPath = [IO.Path]::GetFullPath($Destination)
    $destinationDirectory = Split-Path -Parent $destinationPath
    New-Item -ItemType Directory -Force -Path $destinationDirectory | Out-Null
    Copy-Item -LiteralPath $source -Destination $destinationPath -Force
    if (-not (Test-StableCapture $destinationPath)) {
        throw "Stable capture verification failed after copy: $destinationPath"
    }
    Write-Output $destinationPath
}
finally {
    if ($temporaryRoot -and (Test-Path -LiteralPath $temporaryRoot)) {
        Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
    }
}
