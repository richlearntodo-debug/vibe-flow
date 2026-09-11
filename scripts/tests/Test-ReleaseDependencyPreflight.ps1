$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$sandbox = Join-Path ([IO.Path]::GetTempPath()) `
    ("vibe-flow-release-preflight-" + [Guid]::NewGuid().ToString("N"))

try {
    New-Item -ItemType Directory -Path $sandbox | Out-Null
    Copy-Item -LiteralPath (Join-Path $root "BUILD_RELEASE.ps1") `
        -Destination (Join-Path $sandbox "BUILD_RELEASE.ps1")
    [IO.File]::WriteAllText((Join-Path $sandbox "package.json"),
        '{"version":"2.0.0"}', [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $sandbox "RESTORE_BUILD_DEPS.ps1"),
        '[IO.File]::WriteAllText((Join-Path $PSScriptRoot "restore-called.txt"), "called")',
        [Text.UTF8Encoding]::new($false))

    $previousErrorPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    $output = & powershell -NoProfile -ExecutionPolicy Bypass `
        -File (Join-Path $sandbox "BUILD_RELEASE.ps1") 2>&1 | Out-String
    $releaseExitCode = $LASTEXITCODE
    $ErrorActionPreference = $previousErrorPreference
    if ($releaseExitCode -eq 0) {
        throw "Release build unexpectedly continued without pinned NAudio dependencies."
    }
    if (Test-Path -LiteralPath (Join-Path $sandbox "restore-called.txt")) {
        throw "Release build implicitly invoked dependency restoration."
    }
    if ($output -notmatch "Required build dependency is missing") {
        throw "Release dependency failure did not explain the explicit restore prerequisite."
    }
    Write-Host "Release dependency preflight test passed."
}
finally {
    if (Test-Path -LiteralPath $sandbox) {
        Remove-Item -LiteralPath $sandbox -Recurse -Force -ErrorAction SilentlyContinue
    }
}
