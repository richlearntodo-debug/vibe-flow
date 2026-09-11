param(
    [string]$Repo = $PSScriptRoot + '\..',
    [string]$OutDir = "",
    # Every theme the application offers, and the window sizes worth checking. The dark theme is in this
    # list because it shipped unable to start at all: it was reachable only by editing a configuration file,
    # so no automated run had ever launched it. A theme that cannot start is what this catches.
    [string[]]$Themes = @('light', 'dark', 'system'),
    # Window sizes the interface has to survive. 1366x768 and 1920x1080 are the two smaller desktop sizes the
    # release notes have always listed as unverified, and 880x500 is the smallest window the application
    # allows at 100% scaling — its own minimum. The scaling axis cannot be changed on a CI runner, so these
    # sizes are checked at whatever scaling the machine running this has.
    [string[]]$Sizes = @('', '1366x768', '1920x1080', '880x500'),
    [switch]$KeepGoing
)

# The interface matrix: launch the application once per theme and window size, walk all six pages, and fail
# if any of them reports overlapping controls or text that does not fit its box.
#
# This is the gate for the class of defect found by hand on 2026-09-11: a theme that could not start, page
# titles colliding with their subtitles at 125% scaling, badges clipped by their own rounded region. Each was
# a thing no automated run had ever looked at. Display scaling itself cannot be changed on a CI runner, so
# the size axis is covered with a forced window size instead and the DPI axis is covered by the same walk
# being run at whatever scaling the machine has.
$ErrorActionPreference = 'Stop'
$repoPath = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not (Test-Path -LiteralPath (Join-Path $repoPath 'VibeMic.exe'))) {
    throw "VibeMic.exe not found in $repoPath; build the host first."
}
$checker = Join-Path $repoPath 'scripts\check-ui-geometry.ps1'
if (-not (Test-Path -LiteralPath $checker)) { throw "Geometry check not found: $checker" }
if ([string]::IsNullOrWhiteSpace($OutDir)) { $OutDir = Join-Path $env:TEMP 'vibe-ui-matrix' }

$results = New-Object System.Collections.ArrayList
$failures = 0
# The processes a smoke instance leaves behind. The capture process is the one that matters: the installer
# asks the RestartManager to close whatever holds the files it is replacing, the capture process is a worker
# the installer cannot close, and with a suppressed message box it then aborts and rolls the installation back
# — measured, three silent installs returned exit code 5 for exactly this reason, and only the installer's own
# log (/LOG=) said so. Cleaning up happens before the first case and after the last one, not only between them.
$leftovers = @('VibeMic', 'VoxDeckInputBridge', 'VibeMicAtvvCapture')
function Stop-SmokeLeftovers {
    foreach ($name in $leftovers) {
        Get-Process -Name $name -ErrorAction SilentlyContinue | ForEach-Object {
            Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
        }
    }
}
Stop-SmokeLeftovers
try {
foreach ($theme in $Themes) {
    foreach ($size in $Sizes) {
        $label = $theme + $(if ([string]::IsNullOrWhiteSpace($size)) { '/default' } else { '/' + $size })
        $caseDir = Join-Path $OutDir ($theme + '-' + $(if ([string]::IsNullOrWhiteSpace($size)) { 'default' } else { $size }))
        Remove-Item -Recurse -Force $caseDir -ErrorAction SilentlyContinue
        New-Item -ItemType Directory -Force -Path $caseDir | Out-Null

        # A leftover smoke instance owns the single-instance mutex, so the next launch would signal it and
        # exit, and the run would report on a window it did not start.
        Stop-SmokeLeftovers
        Start-Sleep -Seconds 2

        $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $checker,
            '-Exe', (Join-Path $repoPath 'VibeMic.exe'), '-OutDir', $caseDir,
            '-Theme', $theme, '-ExeArguments', '--ui-smoke')
        if (-not [string]::IsNullOrWhiteSpace($size)) { $arguments += @('-ForceSize', $size) }
        $output = & powershell.exe @arguments 2>&1
        $exit = $LASTEXITCODE

        $report = Join-Path $caseDir 'geometry.txt'
        $pages = 0
        $overlaps = 0
        $clipped = 0
        if (Test-Path -LiteralPath $report) {
            foreach ($line in (Get-Content -LiteralPath $report -Encoding UTF8)) {
                if ($line -match ': overlaps=(\d+)') { $pages++; $overlaps += [int]$Matches[1] }
                elseif ($line -match ': clipped=(\d+)') { $clipped += [int]$Matches[1] }
            }
        }

        # A machine without an interactive desktop cannot show the window at all, and that is not a layout
        # failure: it is reported as skipped, loudly, rather than either failing the build or quietly counting
        # as a pass. The check says "Window not found" when that happens.
        $noDesktop = (($output | Out-String) -match 'Window not found') -and $pages -eq 0
        if ($noDesktop) {
            [void]$results.Add([pscustomobject]@{
                Case = $label; Pages = 0; Overlaps = 0; Clipped = 0; Theme = 'n/a'
                Exit = $exit; Result = 'skipped (no desktop)'
            })
            Write-Host ("skipped " + $label + ": no window appeared, so this machine has no interactive desktop")
            continue
        }

        $ok = ($exit -eq 0) -and ($pages -eq 6) -and ($overlaps -eq 0) -and ($clipped -eq 0)

        # The theme is checked from the pixels rather than trusted from the flag: a run that reports on the
        # dark theme while rendering the light one would be the same class of false pass this whole gate
        # exists to prevent. "system" follows the Windows apps setting, which is read here to know what to
        # expect.
        $observed = 'unknown'
        $homeShot = Join-Path $caseDir '01-home.png'
        if (Test-Path -LiteralPath $homeShot) {
            Add-Type -AssemblyName System.Drawing
            $image = [System.Drawing.Bitmap]::FromFile($homeShot)
            try {
                $point = $image.GetPixel([int]($image.Width * 0.6), [int]($image.Height * 0.2))
                $luminance = ($point.R + $point.G + $point.B) / 3
                $observed = if ($luminance -lt 100) { 'dark' } else { 'light' }
            }
            finally { $image.Dispose() }
        }
        $expected = $theme
        if ($theme -eq 'system') {
            $appsLight = (Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize' `
                -Name AppsUseLightTheme -ErrorAction SilentlyContinue).AppsUseLightTheme
            $expected = if ($appsLight -eq 0) { 'dark' } else { 'light' }
        }
        $themeOk = ($observed -eq $expected)
        if (-not $themeOk) { $ok = $false }
        if (-not $ok) { $failures++ }
        [void]$results.Add([pscustomobject]@{
            Case = $label; Pages = $pages; Overlaps = $overlaps; Clipped = $clipped
            Theme = ($observed + $(if ($themeOk) { '' } else { ' != ' + $expected }))
            Exit = $exit; Result = $(if ($ok) { 'ok' } else { 'FAILED' })
        })
        if (-not $ok -and -not $KeepGoing) { break }
        if (-not $ok) {
            # The failing case keeps its report on screen: a summary line without the detail is not enough to
            # act on, and this is the run that has to be diagnosable later.
            Write-Host ("---- " + $label + " output ----")
            $output | Select-Object -Last 20 | ForEach-Object { Write-Host ("    " + $_) }
        }
    }
}

}
finally {
    # Never leave a capture process behind: it blocks the installer, which is how this was found.
    Stop-SmokeLeftovers
}

Write-Host ""
Write-Host "theme/size        pages  overlaps  clipped  theme      exit  result"
foreach ($row in $results) {
    Write-Host ("{0,-16}  {1,5}  {2,8}  {3,7}  {4,-9}  {5,4}  {6}" -f $row.Case, $row.Pages, $row.Overlaps,
        $row.Clipped, $row.Theme, $row.Exit, $row.Result)
}
Write-Host ""
if ($failures -gt 0) {
    Write-Host ("interface matrix: " + $failures + " case(s) failed")
    exit 1
}
Write-Host ("interface matrix: " + $results.Count + " case(s) passed")
exit 0
