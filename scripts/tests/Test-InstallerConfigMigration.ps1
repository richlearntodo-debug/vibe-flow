$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$hostPath = Join-Path $root "VibeMic.exe"
$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) `
    ("vibe-flow-installer-config-test-" + [Guid]::NewGuid().ToString("N"))

function Invoke-InstallerMigration([string]$LegacyRoot, [string]$UserRoot) {
    $process = Start-Process -FilePath $hostPath -ArgumentList @(
        "--installer-config-migrate",
        $LegacyRoot,
        $UserRoot
    ) -Wait -PassThru -WindowStyle Hidden
    return $process.ExitCode
}

try {
    if (-not (Test-Path -LiteralPath $hostPath -PathType Leaf)) {
        throw "Host build is missing: $hostPath"
    }

    $legacyRoot = Join-Path $fixtureRoot "legacy"
    $userRoot = Join-Path $fixtureRoot "user"
    New-Item -ItemType Directory -Force -Path $legacyRoot, $userRoot | Out-Null
    $centralPath = Join-Path $userRoot "vibe-mic-config.json"
    $centralBackupPath = $centralPath + ".bak"
    [IO.File]::WriteAllText($centralPath, '{"launchAtStartup":false}',
        [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText($centralBackupPath, '{broken-backup',
        [Text.UTF8Encoding]::new($false))

    $legacy = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $root "vibe-mic-config.default.json") |
        ConvertFrom-Json
    $legacy.launchAtStartup = $false
    $legacy.mappings.'Home:long' = "open-url:https://example.com/installer-cli"
    $legacy.schemaVersion = 26
    $legacy | Add-Member -NotePropertyName customButtons -NotePropertyValue @(
        [ordered]@{
            slot = "custom1"; label = "Legacy Home"; sourceType = "keyboard"
            vk = "0x24"; scan = "0x47"; usagePage = 0; usage = 0
            action = "open-url:https://example.com/legacy-home"; enabled = $true
        }
    ) -Force
    $legacy | Add-Member -NotePropertyName futureInstallerField -NotePropertyValue "preserved"
    $legacyJson = $legacy | ConvertTo-Json -Depth 30
    [IO.File]::WriteAllText((Join-Path $legacyRoot "vibe-mic-config.json.bak"), $legacyJson,
        [Text.UTF8Encoding]::new($false))

    if ((Invoke-InstallerMigration $legacyRoot $userRoot) -ne 0) {
        throw "Installer migration command did not recover a valid legacy backup."
    }
    $recovered = Get-Content -Raw -Encoding UTF8 -LiteralPath $centralPath | ConvertFrom-Json
    $recoveredBackup = Get-Content -Raw -Encoding UTF8 -LiteralPath $centralBackupPath | ConvertFrom-Json
    if ($recovered.mappings.'Home:long' -ne "open-url:https://example.com/installer-cli" -or
        $recovered.customButtons[0].action -ne "open-url:https://example.com/legacy-home" -or
        $recovered.futureInstallerField -ne "preserved" -or
        $recoveredBackup.mappings.'Home:long' -ne "open-url:https://example.com/installer-cli" -or
        $recoveredBackup.customButtons[0].action -ne "open-url:https://example.com/legacy-home") {
        throw "Installer migration did not preserve mappings, a legacy custom-button action, unknown fields, and a valid backup."
    }

    $legacy.mappings.'Home:long' = "none"
    [IO.File]::WriteAllText((Join-Path $legacyRoot "vibe-mic-config.json.bak"),
        ($legacy | ConvertTo-Json -Depth 30), [Text.UTF8Encoding]::new($false))
    if ((Invoke-InstallerMigration $legacyRoot $userRoot) -ne 0 -or
        (Get-Content -Raw -Encoding UTF8 -LiteralPath $centralPath | ConvertFrom-Json).mappings.'Home:long' -ne
            "open-url:https://example.com/installer-cli") {
        throw "Installer migration is not idempotent."
    }

    $emptyLegacy = Join-Path $fixtureRoot "empty-legacy"
    $emptyUser = Join-Path $fixtureRoot "empty-user"
    if ((Invoke-InstallerMigration $emptyLegacy $emptyUser) -ne 0) {
        throw "Installer migration rejected a clean install without configuration."
    }
    New-Item -ItemType Directory -Force -Path $emptyUser | Out-Null
    [IO.File]::WriteAllText((Join-Path $emptyUser "vibe-mic-config.json"), '{broken',
        [Text.UTF8Encoding]::new($false))
    if ((Invoke-InstallerMigration $emptyLegacy $emptyUser) -ne 12) {
        throw "Installer migration accepted corrupted configuration without a recovery source."
    }

    Write-Host "Installer configuration migration integration test passed."
}
finally {
    if (Test-Path -LiteralPath $fixtureRoot) {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
    }
}
