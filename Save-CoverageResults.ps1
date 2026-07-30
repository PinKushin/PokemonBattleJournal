# Finds the newest coverage.cobertura.xml in TestResults and copies it to docs/ with a timestamp.
# Run after "Run All Tests" with coverage.runsettings selected in VS, or after:
#   dotnet test PokemonBattleJournal.Tests/PokemonBattleJournal.Tests.csproj --settings coverage.runsettings

$testResultsDir = Join-Path $PSScriptRoot "PokemonBattleJournal.Tests\TestResults"
$docsDir        = Join-Path $PSScriptRoot "PokemonBattleJournal\docs"

$latest = Get-ChildItem -Path $testResultsDir -Recurse -Filter "coverage.cobertura.xml" |
          Sort-Object LastWriteTime -Descending |
          Select-Object -First 1

if (-not $latest) {
    Write-Error "No coverage.cobertura.xml found in $testResultsDir. Run tests with coverlet first."
    exit 1
}

$timestamp  = Get-Date -Format "M-d-yy-h-mm-tt"
$destName   = "TestCoverageResults$timestamp.xml"
$destPath   = Join-Path $docsDir $destName

Copy-Item -Path $latest.FullName -Destination $destPath
Write-Host "Saved: $destPath (source: $($latest.FullName))"

$relativeDest = $destPath.Replace($PSScriptRoot + "\", "").Replace("\", "/")
git -C $PSScriptRoot add $relativeDest
Write-Host "Staged: $relativeDest"
