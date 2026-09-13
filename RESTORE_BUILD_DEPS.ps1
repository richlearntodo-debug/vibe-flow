$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$tools = Join-Path $root "tools"

$packages = @(
    @{
        Name = "system.runtime.4.3.1"
        Url = "https://api.nuget.org/v3-flatcontainer/system.runtime/4.3.1/system.runtime.4.3.1.nupkg"
        Check = "ref\net462\System.Runtime.dll"
    },
    @{
        Name = "microsoft.windows.sdk.contracts"
        Url = "https://api.nuget.org/v3-flatcontainer/microsoft.windows.sdk.contracts/10.0.26100.4948/microsoft.windows.sdk.contracts.10.0.26100.4948.nupkg"
        Check = "ref\netstandard2.0\Windows.WinMD"
    },
    @{
        Name = "naudio.core.2.2.1"
        Url = "https://api.nuget.org/v3-flatcontainer/naudio.core/2.2.1/naudio.core.2.2.1.nupkg"
        Check = "lib\netstandard2.0\NAudio.Core.dll"
    },
    @{
        Name = "naudio.wasapi.2.2.1"
        Url = "https://api.nuget.org/v3-flatcontainer/naudio.wasapi/2.2.1/naudio.wasapi.2.2.1.nupkg"
        Check = "lib\netstandard2.0\NAudio.Wasapi.dll"
    }
)

New-Item -ItemType Directory -Force -Path $tools | Out-Null
foreach ($package in $packages) {
    $target = Join-Path $tools $package.Name
    if (Test-Path (Join-Path $target $package.Check)) { continue }

    $archive = Join-Path $env:TEMP ($package.Name + ".zip")
    Write-Host "Downloading $($package.Name)..."
    Invoke-WebRequest -UseBasicParsing -Uri $package.Url -OutFile $archive
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    Expand-Archive -LiteralPath $archive -DestinationPath $target -Force
    Remove-Item -LiteralPath $archive -Force
}

Write-Host "Build dependencies are ready."

# Optional: the official VB-CABLE package the installer prefers to carry, so a user does not
# have to download it. It is not tracked by git (a third-party binary), so a clean clone or a
# CI runner starts without it and this is where it comes from. The download is verified against
# the same pinned SHA-256 the release build and scripts/Install-VBCable.ps1 use, and a failure
# is reported without failing the build: without the bundle the installer downloads and verifies
# the official package on the user's machine, so a release can still be produced.
$vbCableUrl = "https://download.vb-audio.com/Download_CABLE/VBCABLE_Driver_Pack45.zip"
$vbCableSha256 = "b950e39f01af1d04ea623c8f6d8eb9b6ea5c477c637295fabf20631c85116bfb"
$vbCableTarget = Join-Path $tools "VBCABLE_Driver_Pack45.zip"
if ((Test-Path -LiteralPath $vbCableTarget) -and
    ((Get-FileHash -LiteralPath $vbCableTarget -Algorithm SHA256).Hash.ToLowerInvariant() -eq $vbCableSha256)) {
    Write-Host "VB-CABLE package already present and verified."
} else {
    $vbCableTemp = Join-Path $env:TEMP "VBCABLE_Driver_Pack45.zip"
    try {
        Write-Host "Downloading the official VB-CABLE package (optional)..."
        Invoke-WebRequest -UseBasicParsing -Uri $vbCableUrl -OutFile $vbCableTemp -TimeoutSec 300
        $vbCableActual = (Get-FileHash -LiteralPath $vbCableTemp -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($vbCableActual -ne $vbCableSha256) {
            throw "SHA-256 mismatch: $vbCableActual"
        }
        Move-Item -LiteralPath $vbCableTemp -Destination $vbCableTarget -Force
        Write-Host "VB-CABLE package downloaded and verified."
    }
    catch {
        if (Test-Path -LiteralPath $vbCableTemp) { Remove-Item -LiteralPath $vbCableTemp -Force -ErrorAction SilentlyContinue }
        Write-Warning ("VB-CABLE package not bundled: " + $_.Exception.Message)
        Write-Warning "The release build will continue without it; the installer downloads and verifies the official package on the user's machine."
    }
}
