[CmdletBinding()]
param(
    [switch]$Install,
    [switch]$StatusOnly,
    [string]$StateDirectory = ""
)

$ErrorActionPreference = "Stop"
$packageUrl = "https://download.vb-audio.com/Download_CABLE/VBCABLE_Driver_Pack45.zip"
$expectedSha256 = "b950e39f01af1d04ea623c8f6d8eb9b6ea5c477c637295fabf20631c85116bfb"
$stateDirectory = if ([string]::IsNullOrWhiteSpace($StateDirectory)) {
    Join-Path $env:LOCALAPPDATA "Vibe Flow Remote\vb-cable"
} else { $StateDirectory }
$zipPath = Join-Path $stateDirectory "VBCABLE_Driver_Pack45.zip"
$extractPath = Join-Path $stateDirectory "package"
$statePath = Join-Path $stateDirectory "install-state.json"

function Write-InstallState([string]$state, [string]$detail, [int]$exitCode = 0) {
    New-Item -ItemType Directory -Force -Path $stateDirectory | Out-Null
    $payload = [ordered]@{
        updated_at = (Get-Date).ToUniversalTime().ToString("o")
        state = $state
        detail = $detail
        source = "VB-Audio official download"
        package_sha256 = $expectedSha256
        exit_code = $exitCode
    }
    $payload | ConvertTo-Json -Compress | Set-Content -LiteralPath $statePath -Encoding UTF8
}

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-SetupExecutable {
    $candidate = Get-ChildItem -LiteralPath $extractPath -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -ieq "VBCABLE_Setup_x64.exe" } |
        Select-Object -First 1
    if (-not $candidate) { throw "VBCABLE_Setup_x64.exe was not found in the official package" }
    return $candidate.FullName
}

try {
    New-Item -ItemType Directory -Force -Path $stateDirectory | Out-Null
    if ($StatusOnly) {
        if (Test-Path -LiteralPath $statePath) {
            Get-Content -LiteralPath $statePath -Raw
        } else {
            Write-InstallState "not_started" "The official VB-CABLE package has not been downloaded"
            Get-Content -LiteralPath $statePath -Raw
        }
        exit 0
    }

    if (-not $Install) {
        Write-InstallState "ready" "Ready to download and verify the official VB-CABLE package"
        exit 0
    }

    if (-not (Test-IsAdministrator)) {
        Write-InstallState "elevation_required" "Administrator permission is required; confirm the Windows UAC prompt"
        $scriptPath = $MyInvocation.MyCommand.Path
        $arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$scriptPath`" -Install -StateDirectory `"$stateDirectory`""
        Start-Process -FilePath "powershell.exe" -ArgumentList $arguments -Verb RunAs | Out-Null
        exit 740
    }

    if (-not (Test-Path -LiteralPath $zipPath)) {
        Write-InstallState "downloading" "Downloading from the official VB-Audio URL"
        Invoke-WebRequest -Uri $packageUrl -OutFile $zipPath -UseBasicParsing
    }

    $actualSha256 = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualSha256 -ne $expectedSha256) {
        Write-InstallState "verification_failed" "SHA-256 verification failed; installation refused" 1
        throw "VB-CABLE package SHA-256 mismatch: $actualSha256"
    }

    if (Test-Path -LiteralPath $extractPath) { Remove-Item -LiteralPath $extractPath -Recurse -Force }
    Expand-Archive -LiteralPath $zipPath -DestinationPath $extractPath -Force
    $setupPath = Get-SetupExecutable
    $signature = Get-AuthenticodeSignature -LiteralPath $setupPath
    if ($signature.Status -ne "Valid") {
        Write-InstallState "signature_warning" "Installer signature status is $($signature.Status); the pinned SHA-256 passed" 0
    } else {
        Write-InstallState "verified" "Official installer signature is valid and pinned SHA-256 passed"
    }

    Write-InstallState "installing" "Running the official VB-Audio installer"
    $process = Start-Process -FilePath $setupPath -ArgumentList "/install" -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        Write-InstallState "installer_failed" "Official installer exit code: $($process.ExitCode)" $process.ExitCode
        throw "VB-CABLE installer failed with exit code $($process.ExitCode)"
    }
    Write-InstallState "installed" "VB-CABLE installer completed; return to Vibe Flow and recheck"
    exit 0
}
catch {
    try { Write-InstallState "error" $_.Exception.Message 1 } catch { }
    Write-Error $_.Exception.Message
    exit 1
}
