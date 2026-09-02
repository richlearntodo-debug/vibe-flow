[CmdletBinding()]
param(
    [string] $InstallRoot = "",
    [switch] $SkipAppRestart
)

$ErrorActionPreference = "Stop"
$null = $InstallRoot
$null = $SkipAppRestart

throw "This experiment is retired because Windows requires a reboot to force-disable the RC003 keyboard child. No process or device state was changed. Continue with the signed device-specific filter on a separate driver-test computer."
