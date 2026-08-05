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

## Why 500+ tests can still feel like weak coverage (measured 2026-08-05)

User's instinct: *"i feel like my actual code coverage is actually shit even with 500+ tests."*
Investigated rather than assumed. The suite is healthier than it feels, but three real things
explain the gap:

**1. Almost nothing is a fake test.** Scanned all 617 `[Test]` methods across both test
projects for ones containing no `Should`/`Received`/`Assert`/`Throw` at all. Result: **4**.

```
OptionsPageViewModelTests: SaveTagAsync_NullTrainer_DoesNotSave   (comment only, no assert)
TrainerPageViewModelTests: AppearingAsync_WithMatches_CalculatesStats
MatchOperationsTests:      SaveAsync_BO3Match_SavesAllThreeGames
MatchOperationsTests:      SaveAsync_TwoGameBO3_SetsGame1AndGame2ButNotGame3
```

Those four should get real assertions — they currently cannot fail.

**2. ~21% of unit tests assert existence, not behaviour.** 106 of 494 are reflection-based
contract tests (`MainPageViewModelContractTests` 40, `TrainerPageViewModelContractTests` 27,
`ReadJournalPageViewModelContractTests` 21, `OptionsPageViewModelContractTests` 18). These are
the user's deliberate AI-guardrail strategy ([[feedback_contract_tests]]) and worth keeping —
but they pin XAML binding names to VM members, so they inflate the test count without moving
behavioural coverage. A high test count that includes them will always feel better than the
coverage number looks.

**3. 54 catch paths are structurally untestable.** Every `ModalErrorHandler error = new();`
cannot be substituted, so no test can verify error handling anywhere in the app. This is the
single biggest genuine hole and it is being fixed first — see [[project_error_handler_di]].

**Also: the 57.7% coverlet figure understates reality.** It counts MAUI startup, XAML
codebehind and platform code that unit tests can never reach, and it does not credit anything
the 145 UI tests exercise. VS's tool reports ~80% on the same code. Re-measure only AFTER the
error-handler injection lands, since unlocking 54 paths changes the picture.
