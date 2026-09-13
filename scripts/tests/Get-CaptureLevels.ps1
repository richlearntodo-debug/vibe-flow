# Reports the captured audio levels of recent Vibe Flow dictation sessions, so a change to the microphone or to
# Windows' input level can be judged from the numbers instead of from how the transcription felt.
#
# Read-only: it parses the capture component's own runtime log and prints nothing but measurements.
#
# Why this exists: on the reference machine (2026-09-12) 92% of 918 sessions arrived below 10% output RMS, and 34% of
# them peaked at 100% while averaging below 5% — the signature of an acoustic source that is too quiet and, in a third
# of cases, of handling noise rather than voice. The application's automatic gain was already applying about 4x, so the
# software side has nothing left to give; what was missing was a way to *see* whether a fix on the microphone side
# helped.

param(
    [int]$Last = 20,
    [string]$Root = (Join-Path $env:LOCALAPPDATA 'Vibe Flow Remote\UserData\remote-voice-session')
)

$ErrorActionPreference = 'Stop'
$log = Join-Path $Root 'vibe-mic-runtime.log'
if (-not (Test-Path -LiteralPath $log)) {
    Write-Host "No capture log at $log"
    exit 2
}

$sessions = Get-Content -LiteralPath $log -Encoding UTF8 | Select-String -Pattern 'REMOTE STREAM STOP session='
if (-not $sessions) {
    Write-Host "No completed sessions recorded yet."
    exit 0
}

Write-Host ("Sessions recorded: {0}; showing the last {1}" -f $sessions.Count, [Math]::Min($Last, $sessions.Count))
Write-Host ""
Write-Host ("{0,-12} {1,7} {2,8} {3,8} {4,8} {5,8} {6,7} {7,9}" -f 'time', 'audio_ms', 'raw_rms', 'out_rms', 'out_peak', 'gain', 'gap_ms', 'verdict')

$rows = @()
foreach ($line in ($sessions | Select-Object -Last $Last)) {
    $text = $line.Line
    $time = [regex]::Match($text, '^(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})').Groups[1].Value
    $audio = [int][regex]::Match($text, 'audio_ms=(\d+)').Groups[1].Value
    $rawRms = [double][regex]::Match($text, 'raw_rms_pct=([0-9\.]+)').Groups[1].Value
    $outRms = [double][regex]::Match($text, 'output_rms_pct=([0-9\.]+)').Groups[1].Value
    $outPeak = [double][regex]::Match($text, 'output_peak_pct=([0-9\.]+)').Groups[1].Value
    $rawPeak = [double][regex]::Match($text, 'raw_peak_pct=([0-9\.]+)').Groups[1].Value
    $gain = [double][regex]::Match($text, 'avg_gain=([0-9\.]+)').Groups[1].Value
    $gap = [int][regex]::Match($text, 'max_gap_ms=(\d+)').Groups[1].Value

    $verdict = if ($rawRms -eq 0 -and $outRms -eq 0) { 'no audio' }
        elseif ($rawPeak -ge 95 -and $rawRms -lt 5) { 'noise?' }
        elseif ($outRms -lt 10) { 'too quiet' }
        else { 'usable' }

    Write-Host ("{0,-12} {1,7} {2,7}% {3,7}% {4,7}% {5,8} {6,7} {7,9}" -f
        ($time -replace '^\d{4}-\d{2}-\d{2} ', ''), $audio, $rawRms, $outRms, $outPeak, $gain, $gap, $verdict)
    $rows += [pscustomobject]@{ OutRms = $outRms; RawRms = $rawRms; RawPeak = $rawPeak; Audio = $audio }
}

$usable = @($rows | Where-Object { $_.OutRms -ge 10 }).Count
$quiet = @($rows | Where-Object { $_.OutRms -gt 0 -and $_.OutRms -lt 10 }).Count
$empty = @($rows | Where-Object { $_.Audio -eq 0 }).Count
$noisy = @($rows | Where-Object { $_.RawPeak -ge 95 -and $_.RawRms -lt 5 }).Count
$median = if ($rows.Count -gt 0) { ($rows | Sort-Object OutRms)[[int]($rows.Count / 2)].OutRms } else { 0 }

Write-Host ""
Write-Host ("median output RMS: {0}%   usable(>=10%): {1}   too quiet: {2}   noise-like: {3}   no audio: {4}" -f
    $median, $usable, $quiet, $noisy, $empty)
Write-Host ""
if ($median -ge 10 -and $usable -ge [Math]::Max(2, [int]($rows.Count / 2))) {
    Write-Host "Verdict: the level is in the range speech recognition works well with."
} else {
    Write-Host "Verdict: the captured level is low. Check, in this order:"
    Write-Host "  1. the remote is 10-20 cm from the mouth and aimed at its microphone hole"
    Write-Host "  2. Windows > Settings > System > Sound > Input > CABLE Output > Properties > Levels: 100%, and enable"
    Write-Host "     Microphone Boost if the device offers it"
    Write-Host "  3. a fresh battery in the remote; a low battery lowers the microphone gain"
    Write-Host "  4. hold the remote still: a third of the low sessions peaked at 100% while averaging almost nothing,"
    Write-Host "     which is handling noise rather than speech"
}
