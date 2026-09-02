[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("Experiment", "Restore")]
    [string] $Mode,

    [Parameter(Mandatory = $true)]
    [string] $InstanceId,

    [Parameter(Mandatory = $true)]
    [string] $SessionDirectory,

    [string] $ProbeScript = "",
    [string] $ProbeReportPath = ""
)

$ErrorActionPreference = "Stop"
$expectedPrefix = "HID\{00001812-0000-1000-8000-00805F9B34FB}_DEV_VID&012717_PID&32B8"
$resultPath = Join-Path $SessionDirectory "elevated-result.json"
$pnputil = Join-Path $env:WINDIR "System32\pnputil.exe"

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-DeviceState {
    param([string] $Id)

    $device = Get-PnpDevice -InstanceId $Id -ErrorAction Stop
    $properties = Get-PnpDeviceProperty -InstanceId $Id -ErrorAction Stop
    $service = ($properties | Where-Object KeyName -eq "DEVPKEY_Device_Service" | Select-Object -First 1).Data
    $parent = ($properties | Where-Object KeyName -eq "DEVPKEY_Device_Parent" | Select-Object -First 1).Data
    $hardwareIds = @(($properties | Where-Object KeyName -eq "DEVPKEY_Device_HardwareIds" | Select-Object -First 1).Data)
    $configFlags = ($properties | Where-Object KeyName -eq "DEVPKEY_Device_ConfigFlags" | Select-Object -First 1).Data

    [pscustomobject][ordered]@{
        instance_id = $device.InstanceId
        status = $device.Status.ToString()
        class = $device.Class
        friendly_name = $device.FriendlyName
        problem = [int]$device.Problem
        config_manager_error_code = [int]$device.ConfigManagerErrorCode
        config_flags = [int]$configFlags
        service = [string]$service
        parent = [string]$parent
        hardware_ids = $hardwareIds
    }
}

function Assert-Rc003KeyboardChild {
    param($State)

    if (-not $State.instance_id.StartsWith($expectedPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing unexpected device instance: $($State.instance_id)"
    }
    if ($State.class -ne "Keyboard" -or $State.service -ne "kbdhid") {
        throw "Refusing non-keyboard RC003 device: class=$($State.class) service=$($State.service)"
    }
    if (-not $State.parent.StartsWith("BTHLEDevice\{00001812-0000-1000-8000-00805F9B34FB}", [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing RC003 device with unexpected parent: $($State.parent)"
    }
}

function Invoke-PnpUtil {
    param([string] $Operation, [string] $Id)

    $arguments = @($Operation, $Id)
    $output = @(& $pnputil @arguments 2>&1 | ForEach-Object { $_.ToString() })
    $exitCode = $LASTEXITCODE
    [pscustomobject][ordered]@{
        operation = $Operation
        exit_code = $exitCode
        output = $output
    }
}

function Clear-PendingDisableFlag {
    param([string] $Id)

    if (-not $Id.StartsWith($expectedPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing registry repair for unexpected device: $Id"
    }
    $registryPath = "Registry::HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Enum\" + $Id
    $properties = Get-ItemProperty -LiteralPath $registryPath -ErrorAction Stop
    $beforeFlags = [uint32]$properties.ConfigFlags
    $afterFlags = [uint32]($beforeFlags -band 0xFFFFFFFE)
    if ($afterFlags -ne $beforeFlags) {
        Set-ItemProperty -LiteralPath $registryPath -Name "ConfigFlags" -Type DWord -Value $afterFlags -ErrorAction Stop
    }
    $verifiedFlags = [uint32](Get-ItemProperty -LiteralPath $registryPath -Name "ConfigFlags" -ErrorAction Stop).ConfigFlags
    if (($verifiedFlags -band 1) -ne 0) {
        throw "RC003 pending disable flag remained set after exact registry repair"
    }
    [pscustomobject][ordered]@{
        registry_path = $registryPath
        flags_before = $beforeFlags
        flags_after = $verifiedFlags
    }
}

function Wait-DeviceCondition {
    param(
        [string] $Id,
        [bool] $Enabled,
        [int] $TimeoutMs = 12000
    )

    $started = [Environment]::TickCount
    do {
        Start-Sleep -Milliseconds 250
        try {
            $state = Get-DeviceState $Id
            $isEnabled = $state.status -eq "OK" -and
                $state.config_manager_error_code -eq 0 -and
                ($state.config_flags -band 1) -eq 0
            if ($isEnabled -eq $Enabled) { return $state }
        }
        catch {
            if (-not $Enabled) { return $null }
        }
    } while ([Environment]::TickCount - $started -lt $TimeoutMs)

    throw "Timed out waiting for RC003 keyboard enabled=$Enabled"
}

if ($Mode -eq "Experiment") {
    throw "The exclusive-GATT experiment is retired: Windows treats this keyboard child as critical and requires a reboot to force-disable it. Use the signed RC003 device filter on a separate driver-test computer instead."
}
if (-not (Test-IsAdministrator)) {
    throw "RC003 keyboard isolation helper must run elevated"
}

New-Item -ItemType Directory -Path $SessionDirectory -Force | Out-Null
$before = $null
$afterDisable = $null
$afterRestore = $null
$disableResult = $null
$enableResult = $null
$probeExitCode = $null
$probeOutputPath = Join-Path $SessionDirectory "characteristic-probe-console.txt"
$probeEvidencePath = Join-Path $SessionDirectory "characteristic-probe-report.json"
$otherKeyboardCount = 0
$configFlagRepair = $null
$errorText = ""

try {
    $before = Get-DeviceState $InstanceId
    Assert-Rc003KeyboardChild $before
}
catch {
    $errorText = $_.Exception.GetType().Name + ": " + $_.Exception.Message
}
finally {
    try {
        $current = Get-DeviceState $InstanceId
        Assert-Rc003KeyboardChild $current
        $isEnabled = $current.status -eq "OK" -and
            $current.config_manager_error_code -eq 0 -and
            ($current.config_flags -band 1) -eq 0
        $mustEnable = $Mode -eq "Restore" -or $null -ne $disableResult -or -not $isEnabled
        if ($mustEnable) {
            $enableResult = Invoke-PnpUtil "/enable-device" $InstanceId
            $afterEnableAttempt = Get-DeviceState $InstanceId
            if (($afterEnableAttempt.config_flags -band 1) -ne 0) {
                $configFlagRepair = Clear-PendingDisableFlag $InstanceId
            }
            elseif ($enableResult.exit_code -ne 0 -and $enableResult.exit_code -ne 3010) {
                throw "pnputil failed to restore the RC003 keyboard child and no pending flag was available to repair"
            }
        }
        $afterRestore = Wait-DeviceCondition $InstanceId $true
    }
    catch {
        $restoreError = $_.Exception.GetType().Name + ": " + $_.Exception.Message
        if ([string]::IsNullOrWhiteSpace($errorText)) { $errorText = $restoreError }
        else { $errorText += " | restore: " + $restoreError }
    }

    $restored = $null -ne $afterRestore -and
        $afterRestore.status -eq "OK" -and
        $afterRestore.config_manager_error_code -eq 0 -and
        ($afterRestore.config_flags -band 1) -eq 0
    $result = [ordered]@{
        generated_at = (Get-Date).ToString("o")
        mode = $Mode
        success = [string]::IsNullOrWhiteSpace($errorText) -and $restored -and ($Mode -eq "Restore" -or $probeExitCode -eq 0)
        restored = $restored
        error = $errorText
        device_before = $before
        device_after_disable = $afterDisable
        device_after_restore = $afterRestore
        disable = $disableResult
        enable = $enableResult
        probe_exit_code = $probeExitCode
        probe_output = $probeOutputPath
        probe_report = $(if (Test-Path -LiteralPath $probeEvidencePath) { $probeEvidencePath } else { "" })
        other_working_keyboards = $otherKeyboardCount
        config_flag_repair = $configFlagRepair
    }
    Set-Content -LiteralPath $resultPath -Value ($result | ConvertTo-Json -Depth 10) -Encoding UTF8
}

if (-not [string]::IsNullOrWhiteSpace($errorText)) {
    Write-Error $errorText
    exit 1
}

exit 0
