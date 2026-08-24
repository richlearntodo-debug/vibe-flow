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
        Check = "package\lib\netstandard2.0\NAudio.Core.dll"
    },
    @{
        Name = "naudio.wasapi.2.2.1"
        Url = "https://api.nuget.org/v3-flatcontainer/naudio.wasapi/2.2.1/naudio.wasapi.2.2.1.nupkg"
        Check = "package\lib\netstandard2.0\NAudio.Wasapi.dll"
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
