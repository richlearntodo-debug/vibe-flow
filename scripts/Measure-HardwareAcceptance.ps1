[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("Begin", "Complete", "Status")]
    [string]$Mode,

    [string]$InstallRoot = "",
    [string]$EvidenceRoot = "",
    [string]$SessionId = "",
    [ValidateRange(1, 1000)]
    [int]$ExpectedVoiceCycles = 100
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$pinnedCaptureSha256 = "B62DE035A9CAD0A16B97F6935C6E4DE0BF2B73C61B180595482D852C0582E683"
$expectedProcessNames = @("VibeFlow", "VoxDeckInputBridge", "VibeMicAtvvCapture")
$requiredButtons = [ordered]@{
    "Up" = "48"
    "Down" = "50"
    "Left" = "4B"
    "Right" = "4D"
    "Confirm" = "1C"
    "Home" = "47"
    "TV" = "29"
    "Function" = "5D"
}

if ([string]::IsNullOrWhiteSpace($InstallRoot)) {
    $portableExecutable = Join-Path $repoRoot "VibeFlow.exe"
    if (Test-Path -LiteralPath $portableExecutable) {
        $InstallRoot = $repoRoot
    }
    else {
        $InstallRoot = Join-Path $env:LOCALAPPDATA "Programs\Vibe Flow Remote"
    }
}
if ([string]::IsNullOrWhiteSpace($EvidenceRoot)) {
    $EvidenceRoot = Join-Path $repoRoot "artifacts\hardware-acceptance"
}

$InstallRoot = [IO.Path]::GetFullPath($InstallRoot)
$EvidenceRoot = [IO.Path]::GetFullPath($EvidenceRoot)
$activeStatePath = Join-Path $EvidenceRoot "active-session.json"

function Write-Utf8File([string]$Path, [string]$Value) {
    $parent = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $parent)) {
        New-Item -ItemType Directory -Force -Path $parent | Out-Null
    }
    [IO.File]::WriteAllText($Path, $Value, [Text.UTF8Encoding]::new($false))
}

function Get-FileLength([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return [long]-1 }
    return [long](Get-Item -LiteralPath $Path).Length
}

function Get-HashOrEmpty([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return "" }
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Get-ProcessSnapshot([string]$ExpectedRoot) {
    $items = @()
    foreach ($name in $expectedProcessNames) {
        $processes = @(Get-Process -Name $name -ErrorAction SilentlyContinue)
        foreach ($process in $processes) {
            $path = ""
            try { $path = [string]$process.Path } catch { }
            $insideRoot = -not [string]::IsNullOrWhiteSpace($path) -and
                $path.StartsWith($ExpectedRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)
            $items += [ordered]@{
                name = $name
                pid = $process.Id
                path = $path
                insideInstallRoot = $insideRoot
            }
        }
    }
    return @($items)
}

function Read-JsonOrNull([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    for ($attempt = 1; $attempt -le 5; $attempt++) {
        try { return Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json }
        catch {
            if ($attempt -lt 5) { Start-Sleep -Milliseconds 50 }
        }
    }
    return $null
}

function Read-TextFromOffset([string]$Path, [long]$Offset) {
    $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read,
        [IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete)
    try {
        [void]$stream.Seek($Offset, [IO.SeekOrigin]::Begin)
        $reader = New-Object IO.StreamReader($stream, [Text.Encoding]::UTF8, $true, 4096, $true)
        try { return $reader.ReadToEnd() } finally { $reader.Dispose() }
    }
    finally { $stream.Dispose() }
}

function Read-AppendedText([string]$Path, [long]$Offset) {
    if (-not (Test-Path -LiteralPath $Path)) {
        return [ordered]@{ exists = $false; rotated = $false; recovered = $false; length = -1; text = "" }
    }
    $length = [long](Get-Item -LiteralPath $Path).Length
    if ($Offset -lt 0) { $Offset = 0 }
    if ($length -lt $Offset) {
        $rotatedPath = $Path + ".1"
        if (Test-Path -LiteralPath $rotatedPath) {
            $rotatedLength = [long](Get-Item -LiteralPath $rotatedPath).Length
            if ($rotatedLength -ge $Offset) {
                $text = (Read-TextFromOffset $rotatedPath $Offset) + (Read-TextFromOffset $Path 0)
                return [ordered]@{
                    exists = $true
                    rotated = $true
                    recovered = $true
                    length = $length
                    text = $text
                }
            }
        }
        return [ordered]@{ exists = $true; rotated = $true; recovered = $false; length = $length; text = "" }
    }
    $text = Read-TextFromOffset $Path $Offset
    return [ordered]@{ exists = $true; rotated = $false; recovered = $false; length = $length; text = $text }
}

function Count-Matches([string]$Text, [string]$Pattern) {
    if ([string]::IsNullOrEmpty($Text)) { return 0 }
    return [regex]::Matches($Text, $Pattern, [Text.RegularExpressions.RegexOptions]::Multiline).Count
}

function Get-RawKeyLifecycle([string]$Text, [string]$ScanCode) {
    $started = 0
    $released = 0
    $repeatedDown = 0
    $strayUp = 0
    $held = $false
    $pattern = "RC003 RAW KEY (?<state>DOWN|UP) .*scan=0x(?<scan>[0-9A-F]+)(?:\s|$)"
    foreach ($match in [regex]::Matches($Text, $pattern,
        [Text.RegularExpressions.RegexOptions]::Multiline -bor [Text.RegularExpressions.RegexOptions]::IgnoreCase)) {
        if (-not [string]::Equals($match.Groups["scan"].Value, $ScanCode,
            [StringComparison]::OrdinalIgnoreCase)) { continue }
        if ($match.Groups["state"].Value -eq "DOWN") {
            if ($held) { $repeatedDown++ }
            else {
                $started++
                $held = $true
            }
        }
        elseif ($held) {
            $released++
            $held = $false
        }
        else { $strayUp++ }
    }
    return [ordered]@{
        down = $started
        up = $released
        repeatedDown = $repeatedDown
        strayUp = $strayUp
        leftHeld = $held
    }
}

function Get-CurrentEvidenceSnapshot {
    $healthPath = Join-Path $InstallRoot "input-bridge-health.json"
    $bridgeConfigPath = Join-Path $InstallRoot "voxdeck-shortcuts.json"
    $userStateRoot = Join-Path $env:LOCALAPPDATA "Vibe Flow Remote\UserData"
    $userConfigPath = Join-Path $userStateRoot "vibe-mic-config.json"
    if (-not (Test-Path -LiteralPath $userConfigPath)) {
        $userConfigPath = Join-Path $InstallRoot "vibe-mic-config.json"
    }
    $health = Read-JsonOrNull $healthPath
    $bridgeConfig = Read-JsonOrNull $bridgeConfigPath
    $userConfig = Read-JsonOrNull $userConfigPath
    $homeLongAction = ""
    $configuredAppActions = @()
    if ($null -ne $bridgeConfig -and $null -ne $bridgeConfig.mappings) {
        $homeMapping = @($bridgeConfig.mappings | Where-Object { $_.name -eq "home" } | Select-Object -First 1)
        if ($homeMapping.Count -gt 0) { $homeLongAction = [string]$homeMapping[0].longShortcut }
        foreach ($mapping in @($bridgeConfig.mappings)) {
            foreach ($propertyName in @("shortcut", "shortShortcut", "longShortcut")) {
                $action = [string]$mapping.$propertyName
                if ($action -match '^(?:open-app|open-exe|launch-app|start-app):') {
                    $configuredAppActions += [ordered]@{
                        button = [string]$mapping.name
                        trigger = $propertyName
                        action = $action
                    }
                }
            }
        }
    }
    $hashes = [ordered]@{}
    foreach ($fileName in @("VibeFlow.exe", "VoxDeckInputBridge.exe", "VibeMicAtvvCapture.exe")) {
        $hashes[$fileName] = Get-HashOrEmpty (Join-Path $InstallRoot $fileName)
    }
    return [ordered]@{
        capturedAt = (Get-Date).ToString("o")
        hashes = $hashes
        processes = @(Get-ProcessSnapshot $InstallRoot)
        health = if ($null -eq $health) { $null } else { [ordered]@{
            state = [string]$health.state
            hookInstalled = [bool]$health.hook_installed
            rawInputRegistered = [bool]$health.raw_input_registered
            rawInputDevicePresent = [bool]$health.raw_input_device_present
            configVersion = [int]$health.config_version
            configRevision = [string]$health.config_revision
            configError = [string]$health.config_error
        }}
        bridgeConfig = if ($null -eq $bridgeConfig) { $null } else { [ordered]@{
            version = [int]$bridgeConfig.version
            revision = [string]$bridgeConfig.revision
            mappingCount = @($bridgeConfig.mappings).Count
            homeLongAction = $homeLongAction
            appActions = @($configuredAppActions)
        }}
        userConfig = if ($null -eq $userConfig) { $null } else { [ordered]@{
            schemaVersion = [int]$userConfig.schemaVersion
            stableVoiceProfileVersion = [int]$userConfig.stableVoiceProfileVersion
            voiceMode = [string]$userConfig.voiceMode
            gain = [double]$userConfig.gain
            audioProcessingMode = [string]$userConfig.audioProcessingMode
            drainMs = [int]$userConfig.drainMs
            audioEndpointName = [string]$userConfig.audioEndpointName
        }}
    }
}

function Get-LogPaths {
    $userStateRoot = Join-Path $env:LOCALAPPDATA "Vibe Flow Remote\UserData"
    $sessionRoot = Join-Path $userStateRoot "remote-voice-session"
    if (-not (Test-Path -LiteralPath $sessionRoot)) {
        $sessionRoot = Join-Path $InstallRoot "remote-voice-session"
    }
    return [ordered]@{
        bridge = Join-Path $InstallRoot "input-bridge-log.txt"
        voiceEvents = Join-Path $sessionRoot "remote-voice-events.jsonl"
        captureRuntime = Join-Path $sessionRoot "vibe-mic-runtime.log"
        host = Join-Path $sessionRoot "vibe-flow-host.log"
    }
}

function Resolve-SessionState {
    if (-not [string]::IsNullOrWhiteSpace($SessionId)) {
        $path = Join-Path (Join-Path $EvidenceRoot $SessionId) "begin-state.json"
    }
    else {
        if (-not (Test-Path -LiteralPath $activeStatePath)) {
            throw "No active hardware acceptance session. Run -Mode Begin first."
        }
        $active = Get-Content -LiteralPath $activeStatePath -Raw -Encoding UTF8 | ConvertFrom-Json
        $path = [string]$active.statePath
    }
    if (-not (Test-Path -LiteralPath $path)) { throw "Acceptance state not found: $path" }
    return Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json
}

if ($Mode -eq "Begin") {
    if (-not (Test-Path -LiteralPath (Join-Path $InstallRoot "VibeFlow.exe"))) {
        throw "VibeFlow.exe was not found in the requested install root: $InstallRoot"
    }
    if ([string]::IsNullOrWhiteSpace($SessionId)) {
        $SessionId = "rc003-" + (Get-Date -Format "yyyyMMdd-HHmmss")
    }
    $sessionDirectory = Join-Path $EvidenceRoot $SessionId
    if (Test-Path -LiteralPath $sessionDirectory) {
        throw "Evidence session already exists: $sessionDirectory"
    }
    New-Item -ItemType Directory -Force -Path $sessionDirectory | Out-Null
    $logPaths = Get-LogPaths
    $offsets = [ordered]@{}
    foreach ($entry in $logPaths.GetEnumerator()) {
        $offsets[$entry.Key] = Get-FileLength $entry.Value
    }
    $state = [ordered]@{
        schemaVersion = 1
        sessionId = $SessionId
        startedAt = (Get-Date).ToString("o")
        installRoot = $InstallRoot
        evidenceRoot = $EvidenceRoot
        expectedVoiceCycles = $ExpectedVoiceCycles
        requiredButtons = $requiredButtons
        logPaths = $logPaths
        logOffsets = $offsets
        begin = Get-CurrentEvidenceSnapshot
    }
    $statePath = Join-Path $sessionDirectory "begin-state.json"
    Write-Utf8File $statePath ($state | ConvertTo-Json -Depth 10)
    Write-Utf8File $activeStatePath (([ordered]@{
        sessionId = $SessionId
        statePath = $statePath
        startedAt = $state.startedAt
    }) | ConvertTo-Json -Depth 4)

    Write-Host "Hardware acceptance session started: $SessionId"
    Write-Host "Install root: $InstallRoot"
    Write-Host "Expected voice cycles: $ExpectedVoiceCycles"
    Write-Host "Now test Voice, Up, Down, Left, Right, Confirm, Home short/long, TV, and Function short/long."
    Write-Host "Then run: powershell -ExecutionPolicy Bypass -File scripts\Measure-HardwareAcceptance.ps1 -Mode Complete"
    exit 0
}

if ($Mode -eq "Status") {
    if (-not (Test-Path -LiteralPath $activeStatePath)) {
        Write-Host "No active hardware acceptance session."
        exit 0
    }
    $active = Get-Content -LiteralPath $activeStatePath -Raw -Encoding UTF8 | ConvertFrom-Json
    Write-Host "Active session: $($active.sessionId)"
    Write-Host "Started: $($active.startedAt)"
    Write-Host "State: $($active.statePath)"
    exit 0
}

$state = Resolve-SessionState
if (-not [string]::Equals([IO.Path]::GetFullPath([string]$state.installRoot), $InstallRoot,
    [StringComparison]::OrdinalIgnoreCase)) {
    $InstallRoot = [IO.Path]::GetFullPath([string]$state.installRoot)
}
$sessionDirectory = Join-Path $EvidenceRoot ([string]$state.sessionId)
$logDeltas = [ordered]@{}
$logsAvailable = $true
$logsRotated = $false
foreach ($property in $state.logPaths.PSObject.Properties) {
    $offset = [long]$state.logOffsets.($property.Name)
    $delta = Read-AppendedText ([string]$property.Value) $offset
    $logDeltas[$property.Name] = $delta
    if (-not $delta.exists) { $logsAvailable = $false }
    if ($delta.rotated) { $logsRotated = $true }
}
$requiredLogNames = @("bridge", "captureRuntime", "host")
$requiredLogsAvailable = (@($requiredLogNames | Where-Object { -not $logDeltas[$_].exists }).Count -eq 0)
$requiredLogsRotated = (@($requiredLogNames | Where-Object {
    $logDeltas[$_].rotated -and -not $logDeltas[$_].recovered
}).Count -gt 0)
$eventStreamEvidenceAvailable = $logDeltas.voiceEvents.exists -and
    (-not $logDeltas.voiceEvents.rotated -or $logDeltas.voiceEvents.recovered)
$logStatus = [ordered]@{}
foreach ($name in $logDeltas.Keys) {
    $delta = $logDeltas[$name]
    $logStatus[$name] = [ordered]@{
        exists = $delta.exists
        rotated = $delta.rotated
        recovered = $delta.recovered
        currentBytes = $delta.length
        appendedBytes = [Text.Encoding]::UTF8.GetByteCount([string]$delta.text)
    }
}

$bridgeText = [string]$logDeltas.bridge.text
$eventText = [string]$logDeltas.voiceEvents.text
$captureText = [string]$logDeltas.captureRuntime.text
$hostText = [string]$logDeltas.host.text
$current = Get-CurrentEvidenceSnapshot

$rawCounts = [ordered]@{}
foreach ($button in $requiredButtons.GetEnumerator()) {
    $rawCounts[$button.Key] = Get-RawKeyLifecycle $bridgeText ([string]$button.Value)
}

$voice = [ordered]@{
    expectedCycles = [int]$state.expectedVoiceCycles
    physicalDown = Count-Matches $bridgeText "Key .* DOWN vk=0x(?:74|F5|FF) scan=0x(?:3F|5E) source="
    physicalUp = Count-Matches $bridgeText "Key .* UP vk=0x(?:74|F5|FF) scan=0x(?:3F|5E) source="
    duplicateDownIgnored = Count-Matches $bridgeText "Voice key duplicate DOWN ignored"
    duplicateUpIgnored = Count-Matches $bridgeText "Voice key duplicate UP ignored"
    pressSignalDelivered = Count-Matches $bridgeText "Voice key signal delivered=True"
    releaseSignalDelivered = Count-Matches $bridgeText "Voice key release signal delivered=True"
    remoteStreamStart = Count-Matches $captureText "^.*REMOTE STREAM START session="
    remoteStreamStop = Count-Matches $captureText "^.*REMOTE STREAM STOP session=.* frames="
    audioLiveStart = Count-Matches $captureText "^.*AUDIO LIVE START session="
    audioLiveStop = Count-Matches $captureText "^.*AUDIO LIVE STOP session="
    transcriptionSubmitted = Count-Matches $captureText "TRANSCRIPTION SUBMIT .*sent=True audio_delivered=True"
    eventStreamStart = Count-Matches $eventText '"name":"stream_start"'
    eventStreamStop = Count-Matches $eventText '"name":"stream_stop"'
}

$audioDurations = @()
foreach ($match in [regex]::Matches($captureText, "REMOTE STREAM STOP session=.*? audio_ms=(?<value>\d+)",
    [Text.RegularExpressions.RegexOptions]::Multiline)) {
    $audioDurations += [int]$match.Groups["value"].Value
}
$audioStats = [ordered]@{
    sessionsWithAudio = $audioDurations.Count
    minimumMs = if ($audioDurations.Count -gt 0) { ($audioDurations | Measure-Object -Minimum).Minimum } else { 0 }
    maximumMs = if ($audioDurations.Count -gt 0) { ($audioDurations | Measure-Object -Maximum).Maximum } else { 0 }
    totalMs = if ($audioDurations.Count -gt 0) { ($audioDurations | Measure-Object -Sum).Sum } else { 0 }
}

$homeLongAction = if ($null -eq $current.bridgeConfig) { "" } else { [string]$current.bridgeConfig.homeLongAction }
$homeLongAppConfigured = $homeLongAction.StartsWith("open-app:", [StringComparison]::OrdinalIgnoreCase) -or
    $homeLongAction.StartsWith("open-exe:", [StringComparison]::OrdinalIgnoreCase) -or
    $homeLongAction.StartsWith("launch-app:", [StringComparison]::OrdinalIgnoreCase)
$appActivation = [ordered]@{
    homeLongAppConfigured = $homeLongAppConfigured
    homeLongGestureSuccess = Count-Matches $bridgeText "Gesture action executed label=Home.* action=(?:open-app|open-exe|launch-app):.* success=True"
    configuredAppActionCount = if ($null -eq $current.bridgeConfig) { 0 } else { @($current.bridgeConfig.appActions).Count }
    physicalGestureSuccess = Count-Matches $bridgeText "Gesture action executed label=.* phase=(?!测试).* action=(?:open-app|open-exe|launch-app|start-app):.* success=True"
    testGestureSuccess = Count-Matches $bridgeText "Gesture action executed label=.* phase=测试 action=(?:open-app|open-exe|launch-app|start-app):.* success=True"
    foregroundConfirmed = Count-Matches $bridgeText "Client window activation .* foreground=true"
    processStarted = Count-Matches $bridgeText "Configured app action (?:started|fallback_started) "
    appOpened = Count-Matches $bridgeText "Start app action opened "
}
$appActivationPassed = $appActivation.configuredAppActionCount -eq 0 -or
    ($appActivation.physicalGestureSuccess -ge 1 -and
        ($appActivation.foregroundConfirmed -ge 1 -or $appActivation.processStarted -ge 1 -or
            $appActivation.appOpened -ge 1))

$allText = $bridgeText + "`n" + $captureText + "`n" + $hostText
$errorPatterns = [ordered]@{
    deliveredFalse = "delivered=False"
    foregroundFalse = "foreground=False"
    configError = 'config_error=(?!"")\S+'
    noAudio = "delivered no audio|audio_delivered=False|session_without_audio"
    queueDrops = "(?:queue_drops|sink_queue_drops)=[1-9]\d*"
    captureFailure = "VOICE KEY failure|TRANSCRIPTION .*sent=False|Raw Input handling failed|Gesture action .*success=False"
}
$anomalies = [ordered]@{}
foreach ($pattern in $errorPatterns.GetEnumerator()) {
    $anomalies[$pattern.Key] = Count-Matches $allText $pattern.Value
}

$processChecks = [ordered]@{}
$foreignProcesses = @()
foreach ($name in $expectedProcessNames) {
    $matches = @($current.processes | Where-Object { $_.name -eq $name })
    $inside = @($matches | Where-Object { $_.insideInstallRoot })
    $outside = @($matches | Where-Object { -not $_.insideInstallRoot })
    $processChecks[$name] = [ordered]@{
        total = $matches.Count
        insideInstallRoot = $inside.Count
        expectedSingleInstance = ($matches.Count -eq 1 -and $inside.Count -eq 1)
    }
    $foreignProcesses += $outside
}

$hashDrift = @()
foreach ($name in @("VibeFlow.exe", "VoxDeckInputBridge.exe", "VibeMicAtvvCapture.exe")) {
    $before = [string]$state.begin.hashes.$name
    $after = [string]$current.hashes.$name
    if (-not [string]::Equals($before, $after, [StringComparison]::OrdinalIgnoreCase)) {
        $hashDrift += $name
    }
}
$captureHashPinned = [string]::Equals([string]$current.hashes."VibeMicAtvvCapture.exe",
    $pinnedCaptureSha256, [StringComparison]::OrdinalIgnoreCase)
$beginRevision = if ($null -eq $state.begin.health) { "" } else { [string]$state.begin.health.configRevision }
$currentRevision = if ($null -eq $current.health) { "" } else { [string]$current.health.configRevision }
$bridgeRevision = if ($null -eq $current.bridgeConfig) { "" } else { [string]$current.bridgeConfig.revision }
$revisionStable = -not [string]::IsNullOrWhiteSpace($beginRevision) -and
    [string]::Equals($beginRevision, $currentRevision, [StringComparison]::OrdinalIgnoreCase)
$revisionAcknowledged = -not [string]::IsNullOrWhiteSpace($currentRevision) -and
    [string]::Equals($currentRevision, $bridgeRevision, [StringComparison]::OrdinalIgnoreCase)

$missingButtons = @()
foreach ($button in $rawCounts.GetEnumerator()) {
    if ($button.Value.down -lt 1 -or $button.Value.up -lt 1) { $missingButtons += $button.Key }
}
$voiceCountsMatch = $voice.physicalDown -eq $voice.physicalUp -and
    $voice.physicalDown -eq $voice.remoteStreamStart -and
    $voice.remoteStreamStart -eq $voice.remoteStreamStop -and
    $voice.remoteStreamStop -eq $voice.audioLiveStart -and
    $voice.audioLiveStart -eq $voice.audioLiveStop
if ($voiceCountsMatch -and $eventStreamEvidenceAvailable) {
    $voiceCountsMatch = $voice.audioLiveStop -eq $voice.eventStreamStart -and
        $voice.eventStreamStart -eq $voice.eventStreamStop
}
$voiceTargetMet = $voice.physicalDown -ge $voice.expectedCycles
$noAnomalies = (@($anomalies.Values | Where-Object { $_ -gt 0 }).Count -eq 0)
$processesValid = (@($processChecks.Values | Where-Object { -not $_.expectedSingleInstance }).Count -eq 0)
$healthValid = $null -ne $current.health -and $current.health.state -eq "running" -and
    $current.health.hookInstalled -and $current.health.rawInputRegistered -and
    $current.health.rawInputDevicePresent -and [string]::IsNullOrWhiteSpace($current.health.configError)
$voiceEvidenceComplete = $voiceTargetMet -and $voiceCountsMatch -and
    $voice.pressSignalDelivered -ge $voice.expectedCycles -and
    $voice.releaseSignalDelivered -ge $voice.expectedCycles -and
    $audioStats.sessionsWithAudio -ge $voice.expectedCycles -and
    $voice.transcriptionSubmitted -ge $voice.expectedCycles

$automaticEvidencePassed = $requiredLogsAvailable -and -not $requiredLogsRotated -and $healthValid -and
    $processesValid -and $foreignProcesses.Count -eq 0 -and $hashDrift.Count -eq 0 -and
    $captureHashPinned -and $revisionStable -and $revisionAcknowledged -and
    $missingButtons.Count -eq 0 -and $voiceEvidenceComplete -and $appActivationPassed -and $noAnomalies

$verdict = if ($automaticEvidencePassed) { "automatic-evidence-pass" }
    elseif (-not $requiredLogsAvailable -or $requiredLogsRotated -or $missingButtons.Count -gt 0 -or -not $voiceTargetMet) { "incomplete" }
    else { "automatic-evidence-fail" }

$report = [ordered]@{
    schemaVersion = 2
    sessionId = [string]$state.sessionId
    startedAt = [string]$state.startedAt
    completedAt = (Get-Date).ToString("o")
    installRoot = $InstallRoot
    verdict = $verdict
    automaticEvidencePassed = $automaticEvidencePassed
    releaseApproved = $false
    note = "This report never approves release automatically. Manual restart, reconnect, upgrade, DPI, transcription-target, and signing gates remain."
    logs = [ordered]@{
        available = $logsAvailable
        rotated = $logsRotated
        requiredAvailable = $requiredLogsAvailable
        requiredRotated = $requiredLogsRotated
        eventStreamEvidenceAvailable = $eventStreamEvidenceAvailable
        details = $logStatus
    }
    health = $current.health
    processChecks = $processChecks
    foreignProcesses = @($foreignProcesses)
    hashDrift = @($hashDrift)
    captureHashPinned = $captureHashPinned
    configuration = [ordered]@{
        beginRevision = $beginRevision
        currentRevision = $currentRevision
        bridgeRevision = $bridgeRevision
        revisionStable = $revisionStable
        revisionAcknowledged = $revisionAcknowledged
    }
    physicalButtons = $rawCounts
    missingButtons = @($missingButtons)
    voice = $voice
    audio = $audioStats
    appActivation = $appActivation
    appActivationPassed = $appActivationPassed
    anomalies = $anomalies
    remainingManualGates = @(
        "Confirm Home short shows the desktop and Home long opens/focuses the configured APP",
        "Confirm translated text remains in the original input field and Enter sends it",
        "Confirm APP mapping survives app restart and Bluetooth disconnect/reconnect",
        "Confirm RC003 sleep/wake, Windows sleep/wake, and Windows restart recovery",
        "Confirm upgrade preserves configuration",
        "Check UI at Windows 125%, 150%, and 200% scaling",
        "Sign the installer and binaries with an Authenticode certificate"
    )
}

$jsonPath = Join-Path $sessionDirectory "hardware-acceptance-report.json"
Write-Utf8File $jsonPath ($report | ConvertTo-Json -Depth 12)

$markdown = @(
    "# Vibe Flow RC003 Hardware Acceptance Evidence",
    "",
    "- Session: ``$($report.sessionId)``",
    "- Started: ``$($report.startedAt)``",
    "- Completed: ``$($report.completedAt)``",
    "- Verdict: **$($report.verdict)**",
    "- Release approved: **No** (manual gates remain)",
    "",
    "## Automatic Evidence",
    "",
    "| Check | Result |",
    "| --- | --- |",
    "| Required Bridge/Capture/Host logs available and not rotated | $($requiredLogsAvailable -and -not $requiredLogsRotated) |",
    "| Optional event-stream log available for this session | $eventStreamEvidenceAvailable |",
    "| Bridge health ready | $healthValid |",
    "| One expected process per component | $processesValid |",
    "| No foreign process roots | $($foreignProcesses.Count -eq 0) |",
    "| Runtime hashes unchanged | $($hashDrift.Count -eq 0) |",
    "| Frozen Capture hash | $captureHashPinned |",
    "| Configuration revision stable and acknowledged | $($revisionStable -and $revisionAcknowledged) |",
    "| All supported physical buttons observed | $($missingButtons.Count -eq 0) |",
    "| Voice target and one-to-one lifecycle | $voiceEvidenceComplete |",
    "| Configured physical APP action reached its target | $appActivationPassed |",
    "| No detected runtime anomalies | $noAnomalies |",
    "",
    "## Voice Lifecycle",
    "",
    "| Metric | Count |",
    "| --- | ---: |",
    "| Expected cycles | $($voice.expectedCycles) |",
    "| Physical DOWN | $($voice.physicalDown) |",
    "| Physical UP | $($voice.physicalUp) |",
    "| Stream start | $($voice.remoteStreamStart) |",
    "| Stream stop | $($voice.remoteStreamStop) |",
    "| Audio live start | $($voice.audioLiveStart) |",
    "| Audio live stop | $($voice.audioLiveStop) |",
    "| Transcription submitted with audio | $($voice.transcriptionSubmitted) |",
    "| Duplicate DOWN ignored | $($voice.duplicateDownIgnored) |",
    "| Duplicate UP ignored | $($voice.duplicateUpIgnored) |",
    "| Sessions with real audio | $($audioStats.sessionsWithAudio) |",
    "",
    "## Physical Buttons",
    "",
    "| Button | DOWN | UP | Repeated DOWN ignored |",
    "| --- | ---: | ---: | ---: |"
)
foreach ($button in $rawCounts.GetEnumerator()) {
    $markdown += "| $($button.Key) | $($button.Value.down) | $($button.Value.up) | $($button.Value.repeatedDown) |"
}
$markdown += @(
    "",
    "## Remaining Manual Gates",
    ""
)
foreach ($gate in $report.remainingManualGates) { $markdown += "- [ ] $gate" }
$markdown += @(
    "",
    "> This evidence report intentionally cannot set ``hardwareAcceptancePassed=true`` or approve a release."
)
$markdownPath = Join-Path $sessionDirectory "hardware-acceptance-report.md"
Write-Utf8File $markdownPath ($markdown -join [Environment]::NewLine)

if ((Test-Path -LiteralPath $activeStatePath) -and [string]::IsNullOrWhiteSpace($SessionId)) {
    Remove-Item -LiteralPath $activeStatePath -Force
}

Write-Host "Hardware evidence verdict: $verdict"
Write-Host "JSON report: $jsonPath"
Write-Host "Markdown report: $markdownPath"
if (-not $automaticEvidencePassed) { exit 2 }
