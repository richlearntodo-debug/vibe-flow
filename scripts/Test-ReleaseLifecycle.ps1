param(
    [Parameter(Mandatory = $true)]
    [string]$InstallerPath,
    [string]$PreviousInstallerPath = "",
    [switch]$NoConfigFixture
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
$previousInstallDir = Join-Path $sandbox "previous-app"
$upgradeMarker = Join-Path $previousInstallDir "upgrade-preservation.marker"
$userStateRoot = Join-Path $env:LOCALAPPDATA "Vibe Flow Remote\UserData"
$userConfigPath = Join-Path $userStateRoot "vibe-mic-config.json"
$defaultInstallDir = Join-Path $env:LOCALAPPDATA "Programs\Vibe Flow Remote"
$uninstallRegistryPath = "Registry::HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Uninstall\{99C65880-071A-4F75-9238-FA4E92A2E76D}_is1"
$runRegistryPath = "Registry::HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run"
$createdUserConfigFixture = $false
$createdUserStateRoot = $false

function Assert-DisposableAccount {
    $conflicts = [System.Collections.Generic.List[string]]::new()
    if (Test-Path -LiteralPath $uninstallRegistryPath) {
        $conflicts.Add("the Vibe Flow per-user uninstall record")
    }
    try {
        $startupValue = Get-ItemPropertyValue -Name "Vibe Flow" -LiteralPath $runRegistryPath -ErrorAction Stop
        if (-not [string]::IsNullOrWhiteSpace([string]$startupValue)) {
            $conflicts.Add("the Vibe Flow per-user startup entry")
        }
    }
    catch [System.Management.Automation.ItemNotFoundException] { }
    catch [System.Management.Automation.PSArgumentException] { }
    if (Test-Path -LiteralPath $defaultInstallDir) {
        $conflicts.Add("the default Vibe Flow installation directory")
    }
    if (Test-Path -LiteralPath $userStateRoot) {
        $conflicts.Add("the Vibe Flow central user-data directory")
    }
    if ($conflicts.Count -gt 0) {
        throw "Release lifecycle testing requires a disposable Windows account; found $($conflicts -join ', ')."
    }
}

function Get-ConfigContractProjection([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    $value = Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
    return [ordered]@{
        stableVoiceProfileVersion = $value.stableVoiceProfileVersion
        inputMethod = $value.inputMethod
        inputMethodHotkey = $value.inputMethodHotkey
        inputMethodTrigger = $value.inputMethodTrigger
        providerStartupDelayMs = $value.providerStartupDelayMs
        voiceMode = $value.voiceMode
        captureSeconds = $value.captureSeconds
        gain = $value.gain
        autoLevel = $value.autoLevel
        audioProcessingMode = $value.audioProcessingMode
        audioEndpointName = $value.audioEndpointName
        autoRouteVirtualMicrophone = $value.autoRouteVirtualMicrophone
        soundFeedbackEnabled = $value.soundFeedbackEnabled
        autoCheckUpdates = $value.autoCheckUpdates
        drainMs = $value.drainMs
        inputRoutingMode = $value.inputRoutingMode
        mappingPreset = $value.mappingPreset
        mappings = $value.mappings
        customButtons = $value.customButtons
        shortcutProfiles = $value.shortcutProfiles
        activeShortcutProfileId = $value.activeShortcutProfileId
        smartProfilesEnabled = $value.smartProfilesEnabled
        smartProfileLocked = $value.smartProfileLocked
        smartProfileFallbackId = $value.smartProfileFallbackId
        theme = $value.theme
        launchAtStartup = $value.launchAtStartup
        startBridgeOnLaunch = $value.startBridgeOnLaunch
        minimizeToTray = $value.minimizeToTray
        setupCompleted = $value.setupCompleted
        onboardingVersion = $value.onboardingVersion
        onboardingStep = $value.onboardingStep
        resumeSetupAfterRestart = $value.resumeSetupAfterRestart
    } | ConvertTo-Json -Depth 20 -Compress
}

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

function Assert-RegisteredInstallLocation([string]$ExpectedDirectory, [string]$Phase) {
    $registered = [string](Get-ItemPropertyValue -Name "InstallLocation" `
        -LiteralPath $uninstallRegistryPath -ErrorAction Stop)
    $expected = [IO.Path]::GetFullPath($ExpectedDirectory).TrimEnd('\')
    $actual = [IO.Path]::GetFullPath($registered).TrimEnd('\')
    if (-not $actual.Equals($expected, [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Phase registered InstallLocation '$actual' instead of '$expected'."
    }
}

try {
    Assert-DisposableAccount
    if ($NoConfigFixture -and -not [string]::IsNullOrWhiteSpace($PreviousInstallerPath)) {
        throw "NoConfigFixture cannot be combined with PreviousInstallerPath."
    }
    $createdUserStateRoot = $true
    New-Item -ItemType Directory -Force -Path $sandbox | Out-Null
    if (-not [string]::IsNullOrWhiteSpace($PreviousInstallerPath)) {
        $previousInstaller = (Resolve-Path -LiteralPath $PreviousInstallerPath).Path
        Invoke-CheckedProcess $previousInstaller @(
            "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/CLOSEAPPLICATIONS",
            "/DIR=$previousInstallDir"
        )
        Stop-InstalledProcesses $previousInstallDir
        if (-not (Test-Path -LiteralPath (Join-Path $previousInstallDir "VibeFlow.exe"))) {
            throw "Previous release did not install correctly."
        }
        Assert-RegisteredInstallLocation $previousInstallDir "Previous release"
        [IO.File]::WriteAllText($upgradeMarker, "preserve", [Text.UTF8Encoding]::new($false))
        $legacyFixture = [ordered]@{
            schemaVersion = 32
            stableVoiceProfileVersion = 11
            inputMethod = "typeless"
            inputMethodHotkey = "ctrl+alt+t"
            inputMethodTrigger = "hold"
            providerStartupDelayMs = 137
            voiceMode = "hold"
            captureSeconds = 0
            gain = 1.0
            autoLevel = $true
            audioProcessingMode = "speech"
            audioEndpointName = "CABLE Input"
            autoRouteVirtualMicrophone = $false
            soundFeedbackEnabled = $false
            autoCheckUpdates = $false
            drainMs = 180
            inputRoutingMode = "strict"
            mappingPreset = "custom"
            mappings = [ordered]@{ "上键" = "pageup"; "Home:long" = "none" }
            customButtons = $null
            shortcutProfiles = @(
                [ordered]@{
                    id = "general"; name = "保留配置"; preset = "custom"
                    mappings = [ordered]@{ "上键" = "pageup" }; processNames = @("cursor")
                },
                [ordered]@{
                    id = "fallback"; name = "保留回退"; preset = "general"
                    mappings = [ordered]@{ "上键" = "up" }; processNames = @()
                }
            )
            activeShortcutProfileId = "general"
            smartProfilesEnabled = $true
            smartProfileLocked = $true
            smartProfileFallbackId = "fallback"
            theme = "dark"
            launchAtStartup = $false
            startBridgeOnLaunch = $true
            minimizeToTray = $true
            setupCompleted = $true
            onboardingVersion = 9
            onboardingStep = 4
            resumeSetupAfterRestart = $false
        } | ConvertTo-Json -Depth 20
        $legacyConfigPath = Join-Path $previousInstallDir "vibe-mic-config.json"
        [IO.File]::WriteAllText($legacyConfigPath, $legacyFixture, [Text.UTF8Encoding]::new($false))
        $expectedConfigProjection = Get-ConfigContractProjection $legacyConfigPath
        $createdUserConfigFixture = $true
    }
    else {
        if ($NoConfigFixture) {
            $expectedConfigProjection = $null
        }
        else {
        $cleanFixture = [ordered]@{
            schemaVersion = 32
            stableVoiceProfileVersion = 11
            inputMethod = "wechat"
            inputMethodHotkey = "ctrl+win"
            inputMethodTrigger = "toggle"
            providerStartupDelayMs = 211
            voiceMode = "hold"
            captureSeconds = 0
            gain = 1.0
            autoLevel = $true
            audioProcessingMode = "speech"
            audioEndpointName = "CABLE Input"
            autoRouteVirtualMicrophone = $false
            soundFeedbackEnabled = $false
            autoCheckUpdates = $false
            drainMs = 180
            inputRoutingMode = "strict"
            mappingPreset = "custom"
            mappings = [ordered]@{ "上键" = "up"; "Home:long" = "none" }
            customButtons = $null
            shortcutProfiles = @(
                [ordered]@{
                    id = "general"; name = "通用导航"; preset = "custom"
                    mappings = [ordered]@{ "上键" = "up" }; processNames = @("notepad")
                },
                [ordered]@{
                    id = "fallback"; name = "回退方案"; preset = "general"
                    mappings = [ordered]@{ "上键" = "pageup" }; processNames = @()
                }
            )
            activeShortcutProfileId = "general"
            smartProfilesEnabled = $true
            smartProfileLocked = $true
            smartProfileFallbackId = "fallback"
            theme = "light"
            launchAtStartup = $true
            startBridgeOnLaunch = $true
            minimizeToTray = $false
            setupCompleted = $true
            onboardingVersion = 9
            onboardingStep = 4
            resumeSetupAfterRestart = $false
        } | ConvertTo-Json -Depth 20
        New-Item -ItemType Directory -Force -Path $userStateRoot | Out-Null
        [IO.File]::WriteAllText($userConfigPath, $cleanFixture, [Text.UTF8Encoding]::new($false))
        $expectedConfigProjection = Get-ConfigContractProjection $userConfigPath
        $createdUserConfigFixture = $true
        }
    }

    Invoke-CheckedProcess $installer @(
        "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/CLOSEAPPLICATIONS",
        "/DIR=$installDir"
    )
    Assert-RegisteredInstallLocation $installDir "V2 candidate"

    foreach ($file in @("VibeFlow.exe", "VoxDeckInputBridge.exe", "VibeMicAtvvCapture.exe",
        "NAudio.Core.dll", "NAudio.Wasapi.dll", "unins000.exe")) {
        if (-not (Test-Path -LiteralPath (Join-Path $installDir $file))) {
            throw "Installed file is missing: $file"
        }
    }
    if (-not [string]::IsNullOrWhiteSpace($PreviousInstallerPath) -and -not (Test-Path -LiteralPath $upgradeMarker)) {
        throw "The directory-changing upgrade removed an existing file from the previous installation directory."
    }
    $actualConfigProjection = Get-ConfigContractProjection $userConfigPath
    if ($actualConfigProjection -ne $expectedConfigProjection) {
        throw "Install or upgrade did not preserve provider, mappings, Profiles, Smart Profile state, theme, or startup preferences."
    }
    $expectedStartup = -not $NoConfigFixture -and
        [string]::IsNullOrWhiteSpace($PreviousInstallerPath)
    $startupAfterInstall = ""
    try {
        $startupAfterInstall = [string](Get-ItemPropertyValue -Name "Vibe Flow" `
            -LiteralPath $runRegistryPath -ErrorAction Stop)
    }
    catch [System.Management.Automation.ItemNotFoundException] { }
    catch [System.Management.Automation.PSArgumentException] { }
    if ($expectedStartup -and ($startupAfterInstall -notlike ('*' + (Join-Path $installDir "VibeFlow.exe") + '*') -or
        $startupAfterInstall -notlike '*--background*')) {
        throw "Install did not create the configured Vibe Flow startup entry."
    }
    if (-not $expectedStartup -and -not [string]::IsNullOrWhiteSpace($startupAfterInstall)) {
        throw "Install created a Vibe Flow startup entry without an enabled configuration."
    }
    if (-not [string]::IsNullOrWhiteSpace($PreviousInstallerPath)) {
        Invoke-CheckedProcess $installer @(
            "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/CLOSEAPPLICATIONS",
            "/DIR=$installDir"
        )
        Assert-RegisteredInstallLocation $installDir "Second V2 upgrade"
        if ((Get-ConfigContractProjection $userConfigPath) -ne $expectedConfigProjection) {
            throw "A second upgrade changed the preserved configuration; migration is not idempotent."
        }
    }

    $hostVersion = (Get-Item -LiteralPath (Join-Path $installDir "VibeFlow.exe")).VersionInfo.ProductVersion
    $bridgeVersion = (Get-Item -LiteralPath (Join-Path $installDir "VoxDeckInputBridge.exe")).VersionInfo.ProductVersion
    $captureHash = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $installDir "VibeMicAtvvCapture.exe")).Hash
    if (-not $hostVersion.StartsWith("2.0.0") -or -not $bridgeVersion.StartsWith("2.0.0")) {
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
    if ($NoConfigFixture) {
        if (Test-Path -LiteralPath $userConfigPath) {
            throw "An unconfigured install created user configuration during uninstall lifecycle testing."
        }
    }
    elseif (-not (Test-Path -LiteralPath $userConfigPath) -or
        (Get-ConfigContractProjection $userConfigPath) -ne $expectedConfigProjection) {
        throw "Uninstall removed or changed the central user configuration; user data must be retained."
    }
    if (Test-Path -LiteralPath $uninstallRegistryPath) {
        throw "Uninstall left the per-user uninstall record behind."
    }
    try {
        $remainingStartupValue = Get-ItemPropertyValue -Name "Vibe Flow" -LiteralPath $runRegistryPath -ErrorAction Stop
        if (-not [string]::IsNullOrWhiteSpace([string]$remainingStartupValue)) {
            throw "Uninstall left the Vibe Flow startup entry behind."
        }
    }
    catch [System.Management.Automation.ItemNotFoundException] { }
    catch [System.Management.Automation.PSArgumentException] { }
    $scope = if ($NoConfigFixture) {
        "Unconfigured clean install, component verification, and uninstall"
    } elseif ([string]::IsNullOrWhiteSpace($PreviousInstallerPath)) {
        "Clean install, component verification, and uninstall"
    } else {
        "Clean install, upgrade, component verification, and uninstall"
    }
    Write-Host "$scope lifecycle test passed."
}
finally {
    if ($createdUserConfigFixture) {
        Remove-Item -LiteralPath $userConfigPath -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath ($userConfigPath + ".bak") -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath ($userConfigPath + ".tmp") -Force -ErrorAction SilentlyContinue
    }
    if ($createdUserStateRoot -and (Test-Path -LiteralPath $userStateRoot)) {
        Remove-Item -LiteralPath $userStateRoot -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path -LiteralPath $sandbox) {
        Remove-Item -LiteralPath $sandbox -Recurse -Force -ErrorAction SilentlyContinue
    }
}
