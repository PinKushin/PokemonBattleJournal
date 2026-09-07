<#
.SYNOPSIS
    Proves every discovered test actually ran. Fails loudly when a lane silently tests nothing.

.DESCRIPTION
    "Passed!" is not the result — the COUNT is. Two failures in this repo's history produce a
    green tick while measuring nothing, and neither shows up in a status check:

      * `dotnet test --filter` matching NOTHING exits 0 and writes a trx reporting total="0"
        (measured). CI selects UI fixtures by name in a matrix, so renaming a fixture without
        editing the matrix leaves a job that runs zero tests and reports success.
      * A crashed test host prints `Passed!` with a TRUNCATED total. The run is short by however
        many tests died with it, and the summary line says nothing about that.

    Both are invisible to a status tick because the exit code is 0 in each case. The only thing
    that catches them is comparing what was DISCOVERED against what was EXECUTED, so this script
    derives both numbers rather than trusting either alone.

    Deliberately no hardcoded expected count anywhere. A pinned number is a change-detector: it
    fails on every legitimately added test, gets bumped without thought, and stops meaning
    anything. Both modes derive their expectation — from `--list-tests`, or from the fixture
    classes present in source — so the assertion maintains itself.

    TWO MODES, usable together:

      -Project            Count mode. Discovery (`dotnet test --list-tests`, honouring -Filter)
                          must equal the executed total summed across -TrxPath. Use on a lane
                          that runs one project in one job.

      -RequireFixturesFrom  Coverage mode. Every `*Tests` fixture class found under that directory
                          must have a .trx of its own reporting at least one executed test. Use on
                          a name-selected matrix, where the hazard runs BOTH ways: a fixture the
                          matrix forgot produces no trx, and a stale matrix entry produces a trx
                          with total 0. Needs no build, so it stays cheap on the UI lanes where
                          `--list-tests` would mean a full MAUI build plus workload install.

.PARAMETER MinimumPerTrx
    Every individual .trx must report at least this many executed tests. Catches the shard that
    matched nothing: a union can look healthy while one shard contributed zero. Implied (1) by
    -RequireFixturesFrom.

.EXAMPLE
    ./build/assert-test-inventory.ps1 -Project PokemonBattleJournal.Tests/PokemonBattleJournal.Tests.csproj -TrxPath TestResults/unit/unit.trx

.EXAMPLE
    ./build/assert-test-inventory.ps1 -RequireFixturesFrom PokemonBattleJournal.UITests/UITests.Shared -TrxPath 'artifacts/**/*.trx'
#>
[CmdletBinding()]
param(
    [string]$Project = '',
    [string]$Filter = '',
    [string]$RequireFixturesFrom = '',
    [Parameter(Mandatory = $true)][string[]]$TrxPath,
    [int]$MinimumPerTrx = 0
)

$ErrorActionPreference = 'Stop'

if (-not $Project -and -not $RequireFixturesFrom) {
    throw "Nothing to prove: pass -Project (count mode), -RequireFixturesFrom (coverage mode), or both. Collecting trx files without an expectation to check them against asserts nothing."
}

# ── Executed ──────────────────────────────────────────────────────────────────
[System.IO.FileInfo[]]$trxFiles = @()
foreach ($pattern in $TrxPath) {
    $trxFiles += Get-ChildItem -Path $pattern -Recurse -ErrorAction SilentlyContinue
}
$trxFiles = $trxFiles | Sort-Object -Property FullName -Unique

if ($trxFiles.Count -eq 0) {
    throw "No .trx found under: $($TrxPath -join ', '). The lane produced no result file, so nothing proves it executed at all."
}

[int]$executed = 0
$totalsByFile = [ordered]@{}

foreach ($trx in $trxFiles) {
    [xml]$doc = Get-Content -LiteralPath $trx.FullName -Raw
    $counters = $doc.TestRun.ResultSummary.Counters
    if ($null -eq $counters) {
        throw "'$($trx.Name)' has no ResultSummary/Counters element — it is not a well-formed trx, so its count cannot be trusted."
    }
    [int]$total = [int]$counters.total
    $executed += $total
    $totalsByFile[$trx.Name] = $total
}

Write-Host 'Per-result-file totals:'
foreach ($entry in $totalsByFile.GetEnumerator()) {
    Write-Host ("  {0,-52} {1,5}" -f $entry.Key, $entry.Value)
}
Write-Host ''

if ($MinimumPerTrx -gt 0) {
    foreach ($entry in $totalsByFile.GetEnumerator()) {
        if ($entry.Value -lt $MinimumPerTrx) {
            throw "'$($entry.Key)' executed $($entry.Value) test(s), below the required minimum of $MinimumPerTrx. A name-selected shard that matches nothing exits 0 and writes exactly this — total 0, no failures, green."
        }
    }
}

# ── Coverage mode ─────────────────────────────────────────────────────────────
if ($RequireFixturesFrom) {
    if (-not (Test-Path -LiteralPath $RequireFixturesFrom)) {
        throw "-RequireFixturesFrom path '$RequireFixturesFrom' does not exist; cannot determine which fixtures were expected."
    }

    [string[]]$fixtures = Get-ChildItem -Path $RequireFixturesFrom -Recurse -Filter *.cs |
        Select-String -Pattern '(?m)^\s*(?:public\s+|internal\s+|sealed\s+|abstract\s+|partial\s+)*class\s+(\w+Tests)\b' |
        ForEach-Object { $_.Matches[0].Groups[1].Value } |
        Sort-Object -Unique

    if ($fixtures.Count -eq 0) {
        throw "No '*Tests' fixture classes found under '$RequireFixturesFrom'. Either the path is wrong or the naming convention changed — either way this check is no longer measuring anything."
    }

    Write-Host "Fixtures found in source ($($fixtures.Count)): $($fixtures -join ', ')"

    [string[]]$missing = @()
    foreach ($fixture in $fixtures) {
        $match = $totalsByFile.Keys | Where-Object { $_ -like "*$fixture*" }
        if (-not $match) { $missing += $fixture }
    }

    if ($missing.Count -gt 0) {
        throw "No result file for fixture(s): $($missing -join ', '). The fixture exists in source but no matrix shard produced a trx for it, so it was never run and nothing said so. Add it to the workflow's matrix list."
    }

    Write-Host "OK: every fixture in source has a result file."
    Write-Host ''
}

# ── Count mode ────────────────────────────────────────────────────────────────
if ($Project) {
    [string[]]$listArgs = @('test', $Project, '--list-tests')
    if ($Filter) { $listArgs += @('--filter', $Filter) }

    Write-Host "Discovering: dotnet $($listArgs -join ' ')"
    [string[]]$listOutput = & dotnet @listArgs 2>&1
    if ($LASTEXITCODE -ne 0) {
        $listOutput | ForEach-Object { Write-Host $_ }
        throw "Test discovery FAILED (exit $LASTEXITCODE). Without an inventory there is nothing to compare the run against."
    }

    # --list-tests indents each test name by four spaces under a header line.
    [int]$discovered = ([string[]]($listOutput | Where-Object { $_ -match '^\s{4}\S' })).Count

    if ($discovered -eq 0) {
        throw "Discovery found ZERO tests in '$Project'$(if ($Filter) { " under filter '$Filter'" }). Either the filter matches nothing or the project has no tests — both make this lane's green tick meaningless."
    }

    Write-Host ("  discovered : {0}" -f $discovered)
    Write-Host ("  executed   : {0}" -f $executed)

    if ($executed -ne $discovered) {
        throw "INVENTORY MISMATCH: discovered $discovered test(s) but executed $executed. A shortfall usually means a crashed test host truncated the run (the summary still says 'Passed!'); an excess means the inventory and the lane disagree about the filter."
    }

    Write-Host ''
    Write-Host "OK: all $discovered discovered test(s) executed."
}
