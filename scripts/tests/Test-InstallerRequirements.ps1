$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$installerPath = Join-Path $root "installer\VibeFlow.iss"
$installer = Get-Content -Raw -LiteralPath $installerPath

$minimumMatch = [regex]::Match($installer, '(?im)^\s*MinVersion\s*=\s*(?<version>[0-9]+(?:\.[0-9]+){1,3})\s*$')
if (-not $minimumMatch.Success) {
    throw "Installer does not define a parseable MinVersion."
}
$minimumVersion = [version]$minimumMatch.Groups["version"].Value
$cases = @(
    [ordered]@{ Name = "Windows 7"; Version = [version]"6.1"; Supported = $false },
    [ordered]@{ Name = "Windows 8.1"; Version = [version]"6.3"; Supported = $false },
    [ordered]@{ Name = "Windows 10"; Version = [version]"10.0.10240"; Supported = $true },
    [ordered]@{ Name = "Windows 11"; Version = [version]"10.0.22000"; Supported = $true }
)
foreach ($case in $cases) {
    $actual = $case.Version -ge $minimumVersion
    if ($actual -ne $case.Supported) {
        throw "$($case.Name) boundary mismatch: version=$($case.Version) minimum=$minimumVersion"
    }
}
if ($installer -notmatch '(?im)^\s*ArchitecturesAllowed\s*=\s*x64compatible\s*$' -or
    $installer -notmatch '(?im)^\s*ArchitecturesInstallIn64BitMode\s*=\s*x64compatible\s*$') {
    throw "Installer is not constrained to the x64-compatible installation mode."
}

Write-Host "Installer Windows 10/11 x64compatible requirement test passed."
