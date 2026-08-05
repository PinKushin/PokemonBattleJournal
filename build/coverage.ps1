<#
.SYNOPSIS
    Generates a code coverage report using .NET's built-in collector.

.DESCRIPTION
    Runs the test suites under the built-in "Code Coverage" collector, merges the results and
    produces an HTML/Cobertura report plus a block-coverage summary.

    This deliberately uses "Code Coverage" and NOT "XPlat Code Coverage". Those are different
    collectors: the latter is coverlet, which only instruments assemblies the *test* process
    loads. The app under WinAppDriver is a separate process, so coverlet cannot see it and
    -IncludeUI would silently contribute nothing.

    For the same reason this never passes --settings build/coverage.runsettings: that file
    pins the collector to coverlet, which would defeat the whole script.

.PARAMETER IncludeUI
    Also run the Windows UI suite (adds ~1m30s). This is the only way to get coverage of the
    live app process — Views, Controls, App and MauiProgram are otherwise near 0%. Android UI
    tests can never be included; they run on the emulator, out of the collector's reach.

.PARAMETER SkipReport
    Collect and merge but do not run ReportGenerator. Useful when you only want the
    .coverage file to open in Visual Studio.

.EXAMPLE
    ./build/coverage.ps1
    Unit + integration only. Fast.

.EXAMPLE
    ./build/coverage.ps1 -IncludeUI
    The full picture, matching what VS's "Analyze Code Coverage for All Tests" used to report.
#>
[CmdletBinding()]
param(
    [switch]$IncludeUI,
    [switch]$SkipReport
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    $outDir = Join-Path $repoRoot 'TestResults/coverage'
    $reportDir = Join-Path $repoRoot 'PokemonBattleJournal/docs/coverage-report'

    # Missing tools produce a confusing empty report rather than an error, so check up front.
    foreach ($tool in @(
            @{ Name = 'dotnet-coverage'; Install = 'dotnet tool install --global dotnet-coverage' },
            @{ Name = 'reportgenerator'; Install = 'dotnet tool install --global dotnet-reportgenerator-globaltool' })) {
        if (-not (Get-Command $tool.Name -ErrorAction SilentlyContinue)) {
            throw "$($tool.Name) is not installed. Run: $($tool.Install)"
        }
    }

    # Stale .coverage files from a previous run would be picked up by the merge below and
    # silently inflate the result.
    if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null

    $suites = [System.Collections.Generic.List[hashtable]]::new()
    $suites.Add(@{ Name = 'unit'; Project = 'PokemonBattleJournal.Tests/PokemonBattleJournal.Tests.csproj'; Filter = $null })
    $suites.Add(@{ Name = 'integration'; Project = 'PokemonBattleJournal.IntegrationTests/PokemonBattleJournal.IntegrationTests.csproj'; Filter = 'Category!=LiveWeb' })
    if ($IncludeUI) {
        $suites.Add(@{ Name = 'windows-ui'; Project = 'PokemonBattleJournal.UITests/UITests.Windows/UITests.Windows.csproj'; Filter = $null })
    }

    foreach ($suite in $suites) {
        Write-Host "`n=== $($suite.Name) ===" -ForegroundColor Cyan
        $testArgs = @('test', $suite.Project, '--collect', 'Code Coverage',
            '--results-directory', $outDir, '--nologo', '-v', 'q')
        if ($suite.Filter) { $testArgs += @('--filter', $suite.Filter) }

        dotnet @testArgs
        if ($LASTEXITCODE -ne 0) { throw "$($suite.Name) tests failed (exit $LASTEXITCODE) — coverage not generated." }
    }

    # Pass each path explicitly. A '**' glob is expanded by the shell before dotnet-coverage
    # sees it, which quietly merges only the first file.
    $coverageFiles = @(Get-ChildItem -Path $outDir -Recurse -Filter '*.coverage' | ForEach-Object { $_.FullName })
    if ($coverageFiles.Count -eq 0) { throw "No .coverage files were produced under $outDir." }
    Write-Host "`nMerging $($coverageFiles.Count) coverage file(s)..." -ForegroundColor Cyan

    $mergedCoverage = Join-Path $outDir 'merged.coverage'
    $mergedXml = Join-Path $outDir 'merged.xml'
    # .coverage for Visual Studio, XML for the block numbers and ReportGenerator.
    dotnet-coverage merge -o $mergedCoverage $coverageFiles | Out-Null
    dotnet-coverage merge -o $mergedXml -f xml $coverageFiles | Out-Null

    # Block coverage exists only here — cobertura carries line and branch only, so
    # ReportGenerator's summary cannot show it.
    Write-Host "`nBlock coverage (the metric VS reports):" -ForegroundColor Green
    [xml]$results = Get-Content $mergedXml
    $results.results.modules.module |
        Where-Object { $_.name -like 'PokemonBattleJournal*' -and $_.name -notlike '*Tests*' } |
        ForEach-Object {
            [PSCustomObject]@{
                Module          = $_.name
                'Block %'       = $_.block_coverage
                'Line %'        = $_.line_coverage
                'Blocks Covered' = $_.blocks_covered
                'Blocks Missed' = $_.blocks_not_covered
            }
        } | Format-Table -AutoSize

    if (-not $SkipReport) {
        # The DynamicCodeCoverage parser matches module names verbatim, so the filters need
        # the .dll suffix. The bare name used for cobertura reports silently matches nothing
        # and yields "Assemblies: 0".
        reportgenerator "-reports:$mergedXml" "-targetdir:$reportDir" `
            '-reporttypes:Html;TextSummary;Cobertura' `
            '-assemblyfilters:+PokemonBattleJournal.dll;+PokemonBattleJournal.Scraper.dll' | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "ReportGenerator failed (exit $LASTEXITCODE)." }

        # ReportGenerator emits CRLF; .gitattributes declares eol=lf for the whole repo, and
        # Cobertura.xml/Summary.txt are the two tracked files in that directory.
        foreach ($name in @('Cobertura.xml', 'Summary.txt')) {
            $path = Join-Path $reportDir $name
            if (Test-Path $path) {
                $text = [IO.File]::ReadAllText($path) -replace "`r`n", "`n"
                [IO.File]::WriteAllText($path, $text)
            }
        }

        Write-Host "HTML report: $reportDir/index.html"
    }

    if (-not $IncludeUI) {
        Write-Host "`nNote: UI tests were not included, so Views/Controls/App/MauiProgram show near 0%." -ForegroundColor Yellow
        Write-Host "      Re-run with -IncludeUI for the full picture (~1m30s longer)."
    }
    Write-Host "Open in Visual Studio for the familiar report: $mergedCoverage"
}
finally {
    Pop-Location
}
