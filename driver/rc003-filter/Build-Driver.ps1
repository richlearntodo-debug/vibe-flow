[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Release",

    [ValidateSet("x64")]
    [string] $Platform = "x64"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root "src\VibeFlowRc003Filter.vcxproj"

function Resolve-Tool {
    param([string] $Name)

    $command = Get-Command $Name -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -ne $command) { return $command.Source }

    if ($Name -eq "msbuild.exe") {
        $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
        if (Test-Path -LiteralPath $vswhere -PathType Leaf) {
            $candidate = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild `
                -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
            if (-not [string]::IsNullOrWhiteSpace($candidate)) {
                return [IO.Path]::GetFullPath($candidate)
            }
        }
    }

    if ($Name -eq "infverif.exe" -or $Name -eq "inf2cat.exe") {
        $nugetRoot = if ([string]::IsNullOrWhiteSpace($env:NUGET_PACKAGES)) {
            Join-Path $env:USERPROFILE ".nuget\packages"
        }
        else {
            $env:NUGET_PACKAGES
        }
        $wdkPackageRoot = Join-Path $nugetRoot "microsoft.windows.wdk.x64"
        if (Test-Path -LiteralPath $wdkPackageRoot -PathType Container) {
            $wdkPackages = Get-ChildItem -LiteralPath $wdkPackageRoot -Directory `
                -ErrorAction SilentlyContinue | Sort-Object Name -Descending
            foreach ($package in $wdkPackages) {
                $pattern = if ($Name -eq "infverif.exe") {
                    Join-Path $package.FullName "c\tools\*\x64\infverif.exe"
                }
                else {
                    Join-Path $package.FullName "c\bin\*\x86\Inf2Cat.exe"
                }
                $candidate = Get-ChildItem -Path $pattern -File -ErrorAction SilentlyContinue |
                    Select-Object -First 1
                if ($null -ne $candidate) {
                    return $candidate.FullName
                }
            }
        }

        $kitsRoot = ""
        try {
            $kitsRoot = [string](Get-ItemProperty `
                -LiteralPath "HKLM:\SOFTWARE\Microsoft\Windows Kits\Installed Roots" `
                -Name KitsRoot10 `
                -ErrorAction Stop).KitsRoot10
        }
        catch {}

        if (-not [string]::IsNullOrWhiteSpace($kitsRoot)) {
            $versionDirectories = Get-ChildItem -LiteralPath (Join-Path $kitsRoot "bin") `
                -Directory -ErrorAction SilentlyContinue | Sort-Object Name -Descending
            foreach ($directory in $versionDirectories) {
                $candidate = Join-Path $directory.FullName ("x64\" + $Name)
                if (Test-Path -LiteralPath $candidate -PathType Leaf) {
                    return $candidate
                }
            }
        }
    }

    return ""
}

$msbuild = Resolve-Tool "msbuild.exe"

if ([string]::IsNullOrWhiteSpace($msbuild)) {
    throw "msbuild.exe was not found. Install Visual Studio 2022 C++ Build Tools with MSBuild."
}
if (-not (Test-Path -LiteralPath $project -PathType Leaf)) {
    throw "Driver project was not found: $project"
}

& $msbuild $project /restore /m /t:Rebuild "/p:Configuration=$Configuration" "/p:Platform=$Platform" /warnaserror
if ($LASTEXITCODE -ne 0) { throw "RC003 filter build failed" }

$infverif = Resolve-Tool "infverif.exe"
$inf2cat = Resolve-Tool "inf2cat.exe"
if ([string]::IsNullOrWhiteSpace($infverif) -or [string]::IsNullOrWhiteSpace($inf2cat)) {
    throw "WDK validation tools were not found after NuGet restore."
}

$packageRoot = Join-Path $root ("src\" + $Platform + "\" + $Configuration)
$inf = Get-ChildItem -LiteralPath $packageRoot -Filter "VibeFlowRc003Filter.inf" -File -Recurse -ErrorAction SilentlyContinue |
    Select-Object -First 1
$sys = Get-ChildItem -LiteralPath $packageRoot -Filter "VibeFlowRc003Filter.sys" -File -Recurse -ErrorAction SilentlyContinue |
    Select-Object -First 1
if ($null -eq $inf -or $null -eq $sys) {
    throw "Build completed without the expected INF/SYS package"
}

& $infverif /w $inf.FullName
if ($LASTEXITCODE -ne 0) { throw "InfVerif failed" }

& $inf2cat "/driver:$($inf.Directory.FullName)" /os:10_X64,Server10_X64
if ($LASTEXITCODE -ne 0) { throw "Inf2Cat failed" }

$cat = Get-ChildItem -LiteralPath $packageRoot -Filter "VibeFlowRc003Filter.cat" -File -Recurse -ErrorAction SilentlyContinue |
    Select-Object -First 1
if ($null -eq $cat) {
    throw "Inf2Cat completed without the expected catalog"
}

Get-FileHash -LiteralPath $inf.FullName,$sys.FullName,$cat.FullName -Algorithm SHA256 |
    Select-Object Path,Hash |
    Format-Table -AutoSize

Write-Host "Unsigned driver candidate built. Do not install it on a production computer."
