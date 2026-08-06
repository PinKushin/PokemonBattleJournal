<#
.SYNOPSIS
    Runs the CI suites locally, in CI's shape, when CI is not available.

.DESCRIPTION
    Written on 2026-08-06 during a GitHub Actions incident that stopped workflow runs being
    created at all. Self-hosted runners do not help with that — the runner polls GitHub for
    work, so an outage of the control plane leaves them online and idle. The only thing that
    still works is running the suites here.

    "In CI's shape" means the UI suites run one fixture per invocation, matching the per-fixture
    matrix. That isolation is not cosmetic: a fixture that only passes because an earlier one
    left state behind passes locally in a combined run and fails on CI. Use -Combined for the
    faster, less faithful version.

    Everything runs SEQUENTIALLY, and that is a correctness requirement rather than a
    simplification. Measured 2026-08-06: an Android Appium session created while the Windows UI
    suite is running fails every one of its 79 tests, and keeps failing after the load stops,
    because the session itself is poisoned rather than the moment. See
    docs/memory/project_android_session_poisoning.md. The script also refuses to start the
    Android suite while Windows UI processes are alive.

    The Windows UI suite drives the real desktop. Do not use the machine while it runs — stray
    mouse or keyboard input diverts clicks and produces failures that look like product bugs.

.PARAMETER Suites
    Which suites to run: Unit, Integration, WindowsUI, AndroidUI. Defaults to the two fast
    ones, which is what most changes need.

.PARAMETER All
    Every suite. Equivalent to -Suites Unit,Integration,WindowsUI,AndroidUI.

.PARAMETER Combined
    Run each UI suite as a single invocation instead of once per fixture. Much faster — the
    Windows suite is ~1m45s combined against ~5min per-fixture, and Android is dramatically
    worse because AppiumSetup shuts the emulator down at teardown and the next fixture cold
    boots it again. Trades away the isolation CI actually has.

.PARAMETER AndroidUseInstalled
    Passed through as ANDROID_USE_INSTALLED. Defaults to 1: use the app already deployed by
    Visual Studio rather than triggering AppiumSetup's ~7 minute EmbedAssembliesIntoApk build.
    Set to 0 to reproduce what CI does. If app code changed, deploy first or you are testing
    the previous build and it passes:
        dotnet build PokemonBattleJournal/PokemonBattleJournal.csproj -f net10.0-android -t:Install

.EXAMPLE
    ./build/ci-local.ps1
    Unit + integration. Seconds.

.EXAMPLE
    ./build/ci-local.ps1 -All
    Everything, per fixture, the way CI runs it. Leave the machine alone.

.EXAMPLE
    ./build/ci-local.ps1 -Suites WindowsUI -Combined
    One fast pass over the Windows UI suite.
#>
[CmdletBinding()]
param(
    [ValidateSet('Unit', 'Integration', 'WindowsUI', 'AndroidUI')]
    [string[]]$Suites = @('Unit', 'Integration'),

    [switch]$All,
    [switch]$Combined,

    [ValidateSet('0', '1')]
    [string]$AndroidUseInstalled = '1'
)

$ErrorActionPreference = 'Stop'

if ($All) { $Suites = @('Unit', 'Integration', 'WindowsUI', 'AndroidUI') }

# Matches CI's matrix exactly. Adding a fixture to one and not the other is how a suite
# quietly stops being run.
$fixtures = @('AboutPageTests', 'MainPageTests', 'OptionsPageTests', 'ReadJournalPageTests', 'TrainerPageTests')

$repoRoot = Split-Path -Parent $PSScriptRoot
$results = [System.Collections.Generic.List[object]]::new()

function Invoke-Suite {
    <#
        Runs one "job" and records its outcome. Never throws on test failure: CI runs with
        fail-fast: false, and a script that stopped at the first red suite would hide the other
        three. The exit code at the end carries the verdict.
    #>
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Project,
        [string]$Filter,
        [hashtable]$EnvVars = @{}
    )

    Write-Host ""
    Write-Host "── $Name ".PadRight(78, '─') -ForegroundColor Cyan

    $arguments = @('test', $Project)
    if ($Filter) { $arguments += @('--filter', $Filter) }

    $restore = @{}
    foreach ($key in $EnvVars.Keys) {
        $restore[$key] = [Environment]::GetEnvironmentVariable($key)
        [Environment]::SetEnvironmentVariable($key, $EnvVars[$key])
    }

    $timer = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        # Tee so the developer sees progress live and the summary line can still be parsed.
        $output = & dotnet @arguments 2>&1 | Tee-Object -Variable captured
        $null = $output
        $exitCode = $LASTEXITCODE
    }
    finally {
        $timer.Stop()
        foreach ($key in $restore.Keys) {
            [Environment]::SetEnvironmentVariable($key, $restore[$key])
        }
    }

    # "Passed!  - Failed: 0, Passed: 80, Skipped: 0, Total: 80, Duration: 1 m 47 s"
    $summary = $captured | Select-String -Pattern 'Failed:\s+(\d+),\s+Passed:\s+(\d+),\s+Skipped:\s+(\d+),\s+Total:\s+(\d+)' | Select-Object -Last 1

    $failed = $null; $passed = $null; $total = $null
    if ($summary) {
        $failed = [int]$summary.Matches[0].Groups[1].Value
        $passed = [int]$summary.Matches[0].Groups[2].Value
        $total = [int]$summary.Matches[0].Groups[4].Value
    }

    # A green verdict has to clear three bars, not one.
    #
    # ExitCode alone is not enough, and this is not hypothetical: `dotnet test` with a filter
    # that matches nothing exits **0** and prints no summary. So a renamed fixture or a typo in
    # $fixtures would run zero tests and report PASS — the script would be loudest exactly when
    # it had stopped testing anything. Verified 2026-08-06 with --filter
    # "FullyQualifiedName~NoSuchTestZZZ".
    #
    # Counts alone are not enough either: a build error or a hung driver produces no summary
    # line, and "no failures parsed" would read as success.
    $ranSomething = ($null -ne $total -and $total -gt 0)
    $note = ''
    if ($exitCode -ne 0) { $note = "exit $exitCode" }
    elseif (-not $summary) { $note = 'no summary line — did it build?' }
    elseif (-not $ranSomething) { $note = 'ZERO tests matched — check the filter' }

    $results.Add([pscustomobject]@{
            Suite    = $Name
            Ok       = ($exitCode -eq 0 -and $ranSomething)
            Passed   = $passed
            Failed   = $failed
            Total    = $total
            Duration = $timer.Elapsed
            ExitCode = $exitCode
            Note     = $note
        })
}

function Assert-NoWindowsUiRunning {
    <#
        Guards the measured failure mode: an Android session created while the Windows UI suite
        is live fails all 79 tests and stays broken after the load ends. Cheaper to refuse than
        to spend ten minutes producing red tests that say nothing about the code.
    #>
    $busy = Get-Process -Name 'PokemonBattleJournal', 'WinAppDriver' -ErrorAction SilentlyContinue
    if ($busy) {
        $names = ($busy | Select-Object -ExpandProperty Name -Unique) -join ', '
        throw "Refusing to start the Android suite: $names still running. An Android Appium " +
        "session created under that load fails every test for its whole life, including " +
        "after the load stops (see docs/memory/project_android_session_poisoning.md). " +
        "Wait for the Windows suite to finish, or: Stop-Process -Name PokemonBattleJournal -Force"
    }
}

Push-Location $repoRoot
try {
    Write-Host "Local CI — suites: $($Suites -join ', ')$(if ($Combined) { ' (combined)' } else { ' (per fixture)' })" -ForegroundColor Yellow

    if ($Suites -contains 'Unit') {
        Invoke-Suite -Name 'Unit Tests' -Project 'PokemonBattleJournal.Tests/PokemonBattleJournal.Tests.csproj'
    }

    if ($Suites -contains 'Integration') {
        # Category!=LiveWeb matches CI: the excluded tests hit the real Limitless site, so they
        # fail on a bad connection and are not a signal about this repo.
        Invoke-Suite -Name 'Integration Tests' `
            -Project 'PokemonBattleJournal.IntegrationTests/PokemonBattleJournal.IntegrationTests.csproj' `
            -Filter 'Category!=LiveWeb'
    }

    if ($Suites -contains 'WindowsUI') {
        Write-Host ""
        Write-Host "Windows UI drives the real desktop — do not touch the mouse or keyboard." -ForegroundColor Yellow

        $project = 'PokemonBattleJournal.UITests/UITests.Windows/UITests.Windows.csproj'
        if ($Combined) {
            Invoke-Suite -Name 'Windows UI Tests' -Project $project
        }
        else {
            foreach ($fixture in $fixtures) {
                Invoke-Suite -Name "Windows UI Tests ($fixture)" -Project $project -Filter "FullyQualifiedName~$fixture"
            }
        }
    }

    if ($Suites -contains 'AndroidUI') {
        Assert-NoWindowsUiRunning

        $project = 'PokemonBattleJournal.UITests/UITests.Android/UITests.Android.csproj'
        $androidEnv = @{ ANDROID_USE_INSTALLED = $AndroidUseInstalled }

        if ($Combined) {
            Invoke-Suite -Name 'Android UI Tests' -Project $project -EnvVars $androidEnv
        }
        else {
            Write-Host ""
            Write-Host "Per-fixture Android is slow: AppiumSetup shuts the emulator down at teardown, so every fixture cold boots it again (~40s each). -Combined skips that." -ForegroundColor Yellow
            foreach ($fixture in $fixtures) {
                Invoke-Suite -Name "Android UI Tests ($fixture)" -Project $project -Filter "FullyQualifiedName~$fixture" -EnvVars $androidEnv
            }
        }
    }
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "── Summary ".PadRight(78, '─') -ForegroundColor Cyan

# Out-Host, not a bare pipeline: Format-Table renders lazily, so without it the table lands
# AFTER the pass/fail verdict below and the summary reads back to front.
$results | Format-Table -AutoSize @(
    @{ Label = 'Suite'; Expression = { $_.Suite } }
    @{ Label = 'Result'; Expression = { if ($_.Ok) { 'PASS' } else { 'FAIL' } } }
    @{ Label = 'Passed'; Expression = { $_.Passed } }
    @{ Label = 'Failed'; Expression = { $_.Failed } }
    @{ Label = 'Total'; Expression = { $_.Total } }
    @{ Label = 'Duration'; Expression = { '{0:mm\:ss}' -f $_.Duration } }
    @{ Label = 'Note'; Expression = { $_.Note } }
) | Out-Host

$broken = @($results | Where-Object { -not $_.Ok })
if ($broken.Count -gt 0) {
    Write-Host "$($broken.Count) suite(s) failed: $(($broken | Select-Object -ExpandProperty Suite) -join ', ')" -ForegroundColor Red
    exit 1
}

Write-Host "All $($results.Count) suite(s) passed." -ForegroundColor Green
exit 0
