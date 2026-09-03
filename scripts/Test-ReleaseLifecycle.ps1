param(
    [Parameter(Mandatory = $true)]
    [string]$InstallerPath,
    [string]$PreviousInstallerPath = ""
)

$ErrorActionPreference = "Stop"
$installer = (Resolve-Path -LiteralPath $InstallerPath).Path
$temporaryRoot = if (-not [string]::IsNullOrWhiteSpace($env:RUNNER_TEMP)) {
    $env:RUNNER_TEMP
} else {
    [IO.Path]::GetTempPath()
}
$sandbox = Join-Path $temporaryRoot ("vibe-flow-lifecycle-" + [Guid]::NewGuid().ToString("N"))
$installDir = Join-Path $sandbox "app"
$upgradeMarker = Join-Path $installDir "upgrade-preservation.marker"

function Invoke-CheckedProcess([string]$FilePath, [string[]]$Arguments, [int]$TimeoutSeconds = 120) {
    $process = Start-Process -FilePath $FilePath -ArgumentList $Arguments -PassThru -WindowStyle Hidden
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        $process.Kill()
        throw "$FilePath did not exit within $TimeoutSeconds seconds."
    }
    if ($process.ExitCode -ne 0) {
        throw "$FilePath exited with code $($process.ExitCode)."
    }
}

function Stop-InstalledProcesses([string]$Directory) {
    $normalizedDirectory = [IO.Path]::GetFullPath($Directory).TrimEnd('\') + '\'
    Get-Process -ErrorAction SilentlyContinue | ForEach-Object {
        try {
            $processPath = $_.Path
            if ($processPath -and $processPath.StartsWith($normalizedDirectory, [StringComparison]::OrdinalIgnoreCase)) {
                Stop-Process -Id $_.Id -Force -ErrorAction Stop
            }
        }
        catch {
            if ($_.Exception.Message -notlike "*exited*") { throw }
        }
    }
}

try {
    New-Item -ItemType Directory -Force -Path $sandbox | Out-Null
    if (-not [string]::IsNullOrWhiteSpace($PreviousInstallerPath)) {
        $previousInstaller = (Resolve-Path -LiteralPath $PreviousInstallerPath).Path
        Invoke-CheckedProcess $previousInstaller @(
            "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/CLOSEAPPLICATIONS",
            "/DIR=$installDir"
        )
        Stop-InstalledProcesses $installDir
        if (-not (Test-Path -LiteralPath (Join-Path $installDir "VibeFlow.exe"))) {
            throw "Previous release did not install correctly."
        }
        [IO.File]::WriteAllText($upgradeMarker, "preserve", [Text.UTF8Encoding]::new($false))
    }

    Invoke-CheckedProcess $installer @(
        "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/CLOSEAPPLICATIONS",
        "/DIR=$installDir"
    )

    foreach ($file in @("VibeFlow.exe", "VoxDeckInputBridge.exe", "VibeMicAtvvCapture.exe", "unins000.exe")) {
        if (-not (Test-Path -LiteralPath (Join-Path $installDir $file))) {
            throw "Installed file is missing: $file"
        }
    }
    if (-not [string]::IsNullOrWhiteSpace($PreviousInstallerPath) -and -not (Test-Path -LiteralPath $upgradeMarker)) {
        throw "The upgrade removed an existing user-owned file from the installation directory."
    }

    $hostVersion = (Get-Item -LiteralPath (Join-Path $installDir "VibeFlow.exe")).VersionInfo.ProductVersion
    $bridgeVersion = (Get-Item -LiteralPath (Join-Path $installDir "VoxDeckInputBridge.exe")).VersionInfo.ProductVersion
    $captureHash = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $installDir "VibeMicAtvvCapture.exe")).Hash
    if (-not $hostVersion.StartsWith("1.5.0") -or -not $bridgeVersion.StartsWith("1.5.0")) {
        throw "Installed component version mismatch: host=$hostVersion bridge=$bridgeVersion"
    }
    if ($captureHash -ne "B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683") {
        throw "Installed Capture hash does not match the frozen voice baseline."
    }

    Stop-InstalledProcesses $installDir
    Invoke-CheckedProcess (Join-Path $installDir "unins000.exe") @("/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART")
    if (Test-Path -LiteralPath (Join-Path $installDir "VibeFlow.exe")) {
        throw "Uninstall left the application executable behind."
    }
    $scope = if ([string]::IsNullOrWhiteSpace($PreviousInstallerPath)) {
        "Clean install, component verification, and uninstall"
    } else {
        "Clean install, upgrade, component verification, and uninstall"
    }
    Write-Host "$scope lifecycle test passed."
}
finally {
    if (Test-Path -LiteralPath $sandbox) {
        Remove-Item -LiteralPath $sandbox -Recurse -Force -ErrorAction SilentlyContinue
    }
}
