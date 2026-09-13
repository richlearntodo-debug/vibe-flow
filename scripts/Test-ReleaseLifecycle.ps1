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
# The fixture this test wrote and the exact text it must still contain right before the installer runs;
# both stay empty when -NoConfigFixture is used.
$fixturePath = ""
$fixtureContent = ""

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
    # Read as UTF-8 explicitly: the fixture is written without a byte-order mark and this projection
    # compares its Chinese keys against a file the application rewrote as UTF-8. Reading it with the
    # machine's ANSI code page turned "上键" into mojibake and made every Chinese field look changed,
    # so a preserved configuration was reported as an upgrade failure.
    $value = Get-Content -Raw -Encoding UTF8 -LiteralPath $Path | ConvertFrom-Json
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
        # The installer preserves the user's configuration verbatim, including a provider this
        # candidate retires: the migration to the stable WeChat baseline happens the first time the
        # application loads that configuration, and the upgrade path asserts it after the launch
        # further down.
        $expectedConfigProjection = Get-ConfigContractProjection $legacyConfigPath
        $fixturePath = $legacyConfigPath
        $fixtureContent = $legacyFixture
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
        $fixturePath = $userConfigPath
        $fixtureContent = $cleanFixture
        $createdUserConfigFixture = $true
        }
    }

    # The fixture has to be the file the installer preserves. The previous release's own installer can
    # start that release — V1.5's [Run] entry has no skipifsilent — and a running instance writes its own
    # configuration back, so the fixture can be replaced between the write above and the install below.
    # The installer only checks that the file exists, which is why this surfaced as "the upgrade did not
    # preserve the configuration" while the fixture was still present by name. Everything is stopped,
    # the fixture is written again, and it is proven to be ours before the installer is handed it.
    # The account is disposable by contract (Assert-DisposableAccount refuses otherwise), so stopping
    # these processes is safe, and each one is named with the directory it runs from.
    function Get-VibeLinkProcesses {
        return @(Get-Process -ErrorAction SilentlyContinue |
            Where-Object { $_.ProcessName -in @("VibeFlow", "VibeMic", "VibeMicAtvvCapture", "VoxDeckInputBridge") })
    }
    function Format-VibeLinkProcesses($Processes) {
        return (($Processes | ForEach-Object {
            $path = ""
            try { $path = $_.Path } catch { $path = "(path unavailable)" }
            $_.ProcessName + "#" + $_.Id + "[" + $path + "]"
        }) -join ", ")
    }
    function Get-TextSha256([string]$Text) {
        $sha = [Security.Cryptography.SHA256]::Create()
        try {
            return (($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($Text)) |
                ForEach-Object { $_.ToString("X2") }) -join "")
        }
        finally { $sha.Dispose() }
    }
    function Stop-VibeLinkProcesses([string]$Phase) {
        $running = Get-VibeLinkProcesses
        if ($running.Count -gt 0) {
            Write-Host ("stopping " + $running.Count + " running Vibe Link process(es) " + $Phase + ": " +
                (Format-VibeLinkProcesses $running))
            $running | Stop-Process -Force -ErrorAction SilentlyContinue
            Start-Sleep -Milliseconds 800
        }
        return $running.Count
    }
    Stop-VibeLinkProcesses "before the install" | Out-Null
    if (-not [string]::IsNullOrWhiteSpace($fixturePath)) {
        # The published V1.5 installer's [Run] entry has no skipifsilent, so a silent install of the
        # previous release still starts that release; it registers the configuration in its own directory
        # and writes it back, which replaced this fixture with V1.5's defaults. The installer only checks
        # that the file exists, so the copy succeeded and the projection compared the wrong content. This
        # loop stops whatever the previous release keeps starting and re-proves the fixture each time.
        $wantedHash = Get-TextSha256 $fixtureContent
        $stable = $false
        for ($attempt = 1; $attempt -le 6 -and -not $stable; $attempt++) {
            $stopped = Stop-VibeLinkProcesses ("while proving the fixture (attempt " + $attempt + ")")
            $presentHash = if (Test-Path -LiteralPath $fixturePath) {
                (Get-FileHash -Algorithm SHA256 -LiteralPath $fixturePath).Hash
            } else { "" }
            if ($presentHash -eq $wantedHash -and $stopped -eq 0) {
                $stable = $true
                break
            }
            Write-Host ("configuration fixture was replaced (attempt " + $attempt + "): wanted " +
                $wantedHash.Substring(0, 12) + ", found " +
                $(if ($presentHash) { $presentHash.Substring(0, 12) } else { "absent" }) + " at " + $fixturePath)
            [IO.File]::WriteAllText($fixturePath, $fixtureContent, [Text.UTF8Encoding]::new($false))
            Start-Sleep -Milliseconds 700
        }
        $finalHash = if (Test-Path -LiteralPath $fixturePath) {
            (Get-FileHash -Algorithm SHA256 -LiteralPath $fixturePath).Hash
        } else { "" }
        if (-not $stable -or $finalHash -ne $wantedHash) {
            throw ("The configuration fixture at " + $fixturePath + " keeps being rewritten by another process (" +
                $finalHash + " instead of " + $wantedHash + "), so the upgrade assertion would measure the wrong file.")
        }
        Write-Host ("configuration fixture verified before the install: " + $fixturePath + " sha256 " + $wantedHash.Substring(0, 12))
    }

    $installerLogPath = Join-Path $sandbox "v2-install.log"
    Remove-Item -LiteralPath $installerLogPath -Force -ErrorAction SilentlyContinue
    Invoke-CheckedProcess $installer @(
        "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/CLOSEAPPLICATIONS",
        "/DIR=$installDir", "/LOG=$installerLogPath"
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
    if (Test-Path -LiteralPath $userConfigPath) {
        $configFile = Get-Item -LiteralPath $userConfigPath
        Write-Host ("configuration right after the install: " + $configFile.Length + " bytes, last written " +
            $configFile.LastWriteTime.ToString("HH:mm:ss.fff"))
    }
    else {
        Write-Host "configuration right after the install: absent"
    }
    $runningAfterInstall = Get-VibeLinkProcesses
    Write-Host ("running Vibe Link processes after the install: " +
        $(if ($runningAfterInstall.Count -gt 0) { Format-VibeLinkProcesses $runningAfterInstall } else { "none" }))
    $actualConfigProjection = Get-ConfigContractProjection $userConfigPath
    if ($actualConfigProjection -ne $expectedConfigProjection) {
        # Print both projections: a bare "did not preserve" tells a reader nothing about which field
        # moved, and this check is the whole point of the upgrade lifecycle test. The installer's own
        # log says which legacy root it was handed and what the migration helper answered, because a
        # mismatch has two causes that look identical from here: no previous installation was named,
        # or the helper refused the configuration it was given.
        Write-Host ("expected projection: " + $expectedConfigProjection)
        Write-Host ("actual projection  : " + $actualConfigProjection)
        if (-not [string]::IsNullOrWhiteSpace($fixturePath)) {
            # The installer copies whatever the fixture contains, so when this differs the first thing to
            # check is whether the fixture itself is still the configuration this test wrote.
            $nowHash = if (Test-Path -LiteralPath $fixturePath) {
                (Get-FileHash -Algorithm SHA256 -LiteralPath $fixturePath).Hash
            } else { "" }
            Write-Host ("fixture intact     : " + ($nowHash -eq (Get-TextSha256 $fixtureContent)) +
                "  (" + $fixturePath + ", sha256 " + $(if ($nowHash) { $nowHash.Substring(0, 12) } else { "absent" }) + ")")
        }
        Write-Host ("installer log      : " + $installerLogPath + "  exists=" + (Test-Path -LiteralPath $installerLogPath))
        if (Test-Path -LiteralPath $installerLogPath) {
            Write-Host "--- installer log lines about the previous installation and the migration ---"
            Get-Content -LiteralPath $installerLogPath -Encoding UTF8 -ErrorAction SilentlyContinue |
                Where-Object { $_ -match 'previous install|migrating the legacy|migration helper|InstallLocation' } |
                Select-Object -Last 20 | ForEach-Object { Write-Host ("  " + $_) }
            Write-Host "--- end of installer log ---"
        }
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
        # The preserved provider is one this candidate retires. Loading it is what migrates it, so the
        # upgrade path starts the installed application, waits for the rewrite, and checks the three
        # fields the migration owns — the rest of the configuration must stay as the user left it.
        $applicationProcess = Start-Process -FilePath (Join-Path $installDir "VibeFlow.exe") `
            -ArgumentList "--background" -PassThru
        try {
            $migrated = $false
            for ($attempt = 0; $attempt -lt 40; $attempt++) {
                Start-Sleep -Seconds 1
                $loadedProjection = Get-ConfigContractProjection $userConfigPath
                if ($loadedProjection -match '"inputMethod":"wechat"') { $migrated = $true; break }
            }
            if (-not $migrated) {
                throw "The upgraded configuration kept a retired provider after the application loaded it."
            }
            $migratedState = $loadedProjection | ConvertFrom-Json
            if ($migratedState.inputMethodHotkey -ne "ctrl+win" -or
                $migratedState.inputMethodTrigger -ne "toggle" -or
                $migratedState.providerStartupDelayMs -ne 80) {
                throw ("The retired provider migrated without the stable WeChat baseline " +
                    "(hotkey=$($migratedState.inputMethodHotkey) trigger=$($migratedState.inputMethodTrigger) " +
                    "delay=$($migratedState.providerStartupDelayMs)).")
            }
        }
        finally {
            Stop-InstalledProcesses $installDir
        }
        # Everything after this point compares against the migrated configuration, because that is now
        # what the user has on disk and what an uninstall has to retain.
        $expectedConfigProjection = $loadedProjection
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
        # The state root still holds the configuration this test deliberately retains after an
        # uninstall, plus whatever logs the application wrote while it ran, so it is never empty:
        # removing a directory without -Recurse makes Windows PowerShell 5.1's file-system provider
        # throw a NullReferenceException, and cleanup must never turn a passed test into a failure.
        try { Remove-Item -LiteralPath $userStateRoot -Recurse -Force -ErrorAction Stop }
        catch { Write-Warning ("Could not remove the lifecycle user state at " + $userStateRoot + ": " + $_.Exception.Message) }
    }
    if (Test-Path -LiteralPath $sandbox) {
        try { Remove-Item -LiteralPath $sandbox -Recurse -Force -ErrorAction Stop }
        catch { Write-Warning ("Could not remove the lifecycle sandbox at " + $sandbox + ": " + $_.Exception.Message) }
    }
}
