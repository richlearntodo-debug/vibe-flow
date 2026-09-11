$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$buildScript = Join-Path $root "BUILD_VIBE_MIC.cmd"
$testRoot = Join-Path ([IO.Path]::GetTempPath()) `
    ("vibe-flow-v2-tests-" + [Guid]::NewGuid().ToString("N"))

function Invoke-CSharpBuild([string]$OutputPath, [string]$MainClass, [string[]]$Sources,
    [string]$Target = "exe") {
    $arguments = @(
        "/nologo",
        # The same source encoding the product builds with, so a Unicode assertion cannot pass here
        # against text the product would decode differently.
        "/codepage:65001",
        "/target:$Target",
        "/platform:anycpu",
        "/out:$OutputPath"
    )
    if (-not [string]::IsNullOrWhiteSpace($MainClass)) {
        $arguments += "/main:$MainClass"
    }
    $arguments += @(
        "/reference:System.Windows.Forms.dll",
        "/reference:System.Drawing.dll",
        "/reference:System.Web.Extensions.dll",
        "/reference:System.Security.dll",
        "/reference:$($env:WINDIR)\Microsoft.NET\Framework64\v4.0.30319\WPF\UIAutomationClient.dll",
        "/reference:$($env:WINDIR)\Microsoft.NET\Framework64\v4.0.30319\WPF\UIAutomationTypes.dll"
    )
    $arguments += $Sources
    & $csc $arguments
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $OutputPath -PathType Leaf)) {
        throw "C# test build failed: $MainClass"
    }
}

function Invoke-TestProcess([string]$FilePath, [string[]]$Arguments = @()) {
    if ($Arguments.Count -gt 0) {
        & $FilePath $Arguments
    }
    else {
        & $FilePath
    }
    $testExitCode = $LASTEXITCODE
    if ($testExitCode -ne 0) {
        throw "V2 focused test failed: $(Split-Path -Leaf $FilePath) exit=$testExitCode"
    }
}

try {
    if (-not (Test-Path -LiteralPath $csc -PathType Leaf)) {
        throw "The .NET Framework x64 C# compiler is unavailable: $csc"
    }
    $buildText = Get-Content -Raw -LiteralPath $buildScript
    $sourceMatches = [regex]::Matches($buildText, '"%~dp0(?<path>scripts\\[^\"]+\.cs)"')
    $productSources = @($sourceMatches | ForEach-Object {
        Join-Path $root $_.Groups["path"].Value
    } | Select-Object -Unique)
    if ($productSources.Count -lt 10) {
        throw "Could not resolve the Host source set from BUILD_VIBE_MIC.cmd."
    }
    foreach ($source in $productSources) {
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
            throw "Host source is missing: $source"
        }
    }

    New-Item -ItemType Directory -Force -Path $testRoot | Out-Null
    $smokeApp = Join-Path $testRoot "FocusTargetSmokeApp.exe"
    Invoke-CSharpBuild $smokeApp "FocusTargetSmokeApp" @(
        (Join-Path $root "scripts\tests\FocusTargetSmokeApp.cs")
    ) "winexe"

    $tests = @(
        [ordered]@{ Name = "FocusTargetMultiWindowTests"; Arguments = @($smokeApp) },
        [ordered]@{ Name = "CaptureAskServiceTests"; Arguments = @() },
        [ordered]@{ Name = "CaptureAskUiTests"; Arguments = @() },
        [ordered]@{ Name = "BrowserRemoteLiteTests"; Arguments = @() },
        [ordered]@{ Name = "BrowserRemoteLiteUiTests"; Arguments = @() },
        [ordered]@{ Name = "LiveHudUiTests"; Arguments = @() },
        [ordered]@{ Name = "ShortcutActionTests"; Arguments = @() },
        [ordered]@{ Name = "FeatureSurfaceTests"; Arguments = @() }
    )
    foreach ($test in $tests) {
        $output = Join-Path $testRoot ($test.Name + ".exe")
        $sources = @($productSources) + (Join-Path $root ("scripts\tests\" + $test.Name + ".cs"))
        Invoke-CSharpBuild $output $test.Name $sources
        Invoke-TestProcess $output $test.Arguments
        Write-Host "$($test.Name) passed."
    }
    Write-Host "V2 focused feature suite passed."
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
