---
name: project_coverage_tooling
description: How to generate and interpret coverage reports — coverlet vs VS built-in, two-number gap explained
metadata:
  type: project
---

Two separate coverage tools give different numbers — both are correct for different scopes:

**VS built-in coverage (~80%):** instruments the running process; captures code exercised by ALL test types including Appium UI tests driving the live app. Run via Test Explorer "Run All Tests with Code Coverage". Exports as `.coverage` binary (VS format) or XML via "Export Results".

**Coverlet / XPlat (57.7% line):** only instruments assemblies the test process loads directly. UI tests run the app as a separate process — coverlet cannot see inside it. Only unit + integration tests contribute to app coverage via coverlet.

**To generate coverlet report:**
```powershell
dotnet test PokemonBattleJournal.Tests/PokemonBattleJournal.Tests.csproj --settings coverage.runsettings
dotnet test PokemonBattleJournal.IntegrationTests/PokemonBattleJournal.IntegrationTests.csproj --settings coverage.runsettings
$unit = Get-ChildItem -Recurse "PokemonBattleJournal.Tests/TestResults" -Filter "coverage.cobertura.xml" | Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName
$integ = Get-ChildItem -Recurse "PokemonBattleJournal.IntegrationTests/TestResults" -Filter "coverage.cobertura.xml" | Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName
reportgenerator "-reports:$unit;$integ" "-targetdir:PokemonBattleJournal/docs/coverage-report" "-reporttypes:Cobertura;TextSummary" "-assemblyfilters:+PokemonBattleJournal;-*.Tests;-*.IntegrationTests;-*.UITests"
```

**ReportGenerator:** installed globally as `dotnet-reportgenerator-globaltool`. Merged report lives in `PokemonBattleJournal/docs/coverage-report/`.

**coverage.runsettings:** at repo root; no ResultsDirectory set (VS manages its own output; custom path creates GUID subfolders per run).

**0% classes (expected, not worth chasing):** Views (XAML code-behind), Controls (ComboBox/ImagePicker), App, AppShell, MauiProgram — all MAUI DI/startup/UI code with no unit-testable surface.

**Why:** VS coverage shows a much higher number because it instruments the running app during UI test execution.
