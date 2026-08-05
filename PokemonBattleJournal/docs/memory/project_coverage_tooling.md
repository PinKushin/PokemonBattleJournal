---
name: project_coverage_tooling
description: How to generate and interpret coverage reports — coverlet vs VS built-in, two-number gap explained
metadata:
  type: project
---

Two separate coverage tools give different numbers — both are correct for different scopes:

**VS built-in coverage:** instruments the running process; captures code exercised by ALL test types including Appium UI tests driving the live app. Run via Test Explorer "Run All Tests with Code Coverage", **or entirely from the CLI with `--collect "Code Coverage"`** — no VS needed, see the section below. Exports as `.coverage` binary (VS format) or XML.

**Coverlet / XPlat (57.7% line):** only instruments assemblies the test process loads directly. UI tests run the app as a separate process — coverlet cannot see inside it. Only unit + integration tests contribute to app coverage via coverlet.

**To generate coverlet report:**
```powershell
dotnet test PokemonBattleJournal.Tests/PokemonBattleJournal.Tests.csproj --settings build/coverage.runsettings
dotnet test PokemonBattleJournal.IntegrationTests/PokemonBattleJournal.IntegrationTests.csproj --settings build/coverage.runsettings
$unit = Get-ChildItem -Recurse "PokemonBattleJournal.Tests/TestResults" -Filter "coverage.cobertura.xml" | Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName
$integ = Get-ChildItem -Recurse "PokemonBattleJournal.IntegrationTests/TestResults" -Filter "coverage.cobertura.xml" | Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName
reportgenerator "-reports:$unit;$integ" "-targetdir:PokemonBattleJournal/docs/coverage-report" "-reporttypes:Cobertura;TextSummary" "-assemblyfilters:+PokemonBattleJournal;-*.Tests;-*.IntegrationTests;-*.UITests"
```

**ReportGenerator:** installed globally as `dotnet-reportgenerator-globaltool`. Merged report lives in `PokemonBattleJournal/docs/coverage-report/`.

## Getting the VS-style report (block coverage) — verified 2026-08-05

The user prefers VS's report because it reports **block coverage**, which cobertura cannot
carry: cobertura has line and branch only. Block % lives in the binary `.coverage` file and
survives conversion to VS's own XML, nowhere else.

**Why VS had been showing coverlet numbers — FIXED 2026-08-05.** The runsettings file used to
sit in the repo root, and it pins the collector to `XPlat Code Coverage`, which *is* coverlet
— the friendly name for the built-in collector is plain `"Code Coverage"`. VS auto-detects any
`*.runsettings` **in the solution root** (Tools ▸ Options ▸ Test ▸ "Auto detect runsettings
files"), so "Analyze Code Coverage for All Tests" silently routed through coverlet. Nothing
about the VS install was broken.

The fix was to move it to `build/coverage.runsettings`. Auto-detect only scans the solution
root, so VS now uses its own collector with no settings to toggle. **Do not move it back**,
and do not add any other `*.runsettings` to the repo root. The CI workflows never referenced
it — they pass `--collect "XPlat Code Coverage"` directly — so nothing else had to change;
only explicit local `--settings build/coverage.runsettings` runs are affected.

**Just run the script — `build/coverage.ps1` (added 2026-08-05).** It does the whole sequence
and encodes every gotcha listed below, so there is no reason to run the steps by hand:

```powershell
./build/coverage.ps1 -IncludeUI
```

Without `-IncludeUI` it is unit + integration only (fast, but Views/Controls/App/MauiProgram
sit near 0%). `-SkipReport` collects and merges without running ReportGenerator, for when you
only want the `.coverage` to open in VS. It fails fast if a suite fails, wipes stale
`.coverage` files first (they would otherwise be swept into the merge and inflate the
result), prints the block-coverage table, and converts ReportGenerator's CRLF output to LF so
the tracked report files do not fight `.gitattributes`.

**The underlying commands, if you ever need them directly.** Do NOT pass `--settings
build/coverage.runsettings` here or it routes to coverlet again:

```bash
dotnet test PokemonBattleJournal.Tests/PokemonBattleJournal.Tests.csproj --collect "Code Coverage" --results-directory TestResults/vscov
```

```bash
dotnet-coverage merge -o TestResults/vscov/merged.xml -f xml <each .coverage path>
```

Gotchas, all hit and confirmed:

- `dotnet-coverage` is a separate global tool (`dotnet tool install --global dotnet-coverage`).
  A community Q&A claims it cannot convert `.coverage`; that is wrong — `merge -f xml` is the
  documented conversion path and works.
- Pass the `.coverage` paths explicitly. A `**` glob gets eaten by bash before the tool sees
  it and you silently merge only one file.
- ReportGenerator **cannot read binary `.coverage`** — it says so and emits an empty report.
  Convert first; it then parses the XML as `DynamicCodeCoverage`.
- In that parser the assembly filters need the **`.dll` suffix**
  (`-assemblyfilters:+PokemonBattleJournal.dll`). The bare name used for cobertura matches
  nothing and yields "Assemblies: 0" with no error.
- **Double-clicking the `.coverage` file opens it straight in VS's Code Coverage Results
  window** — the familiar report, without needing Test Explorer to run the coverage itself.

**CI can run it, but is deliberately NOT wired up (decided 2026-08-05).** Both `ci.yml` jobs
are `runs-on: windows-latest`, and the built-in collector needs only
`Microsoft.NET.Test.Sdk` ≥ 15.8 (the repo is on 18.8.1) plus `Microsoft.CodeCoverage`
(18.8.1, already referenced). `--collect "Code Coverage;Format=Cobertura"` emits cobertura
directly for the existing ReportGenerator step, but that throws block coverage away — to keep
it you would publish the `.coverage` or converted XML as an artifact.

Why it was left alone, after the user asked to change the non-UI jobs *only if they needed
it*: they don't. `ci.yml` runs unit + integration only, so switching those to the built-in
collector cannot recover the UI coverage — that is structural, not a config problem. It would
buy a slightly different number and nothing else. The UI coverage would have to come from
`ui-tests-windows.yml`, which is a **5-way per-fixture matrix**: five partial `.coverage`
files, needing artifact upload plus a merge job, on a suite whose timing stability was hard
won and which instrumentation would slow down. See [[feedback_dont_churn_stable_ci]]. The
script gives the same number locally on demand.

The runsettings move affects nothing in CI either — no workflow references the file; they
pass `--collect` directly.

## The UI tests ARE captured from the CLI — verified 2026-08-05

This is the capability worth protecting, and it is the whole reason to prefer the built-in
collector: **it instruments the app process the Appium tests drive.** Coverlet structurally
cannot, because it only sees assemblies the *test* process loads, and the app runs under
WinAppDriver as a separate process.

Proven by running the Windows UI suite alone under the collector:

```
PokemonBattleJournal.dll   block 49.77%   line 40.88%   3598 blocks covered
```

3598 blocks from UI tests alone, with unit and integration tests excluded entirely — only the
live app process can produce that. `skipped_module` was empty, so nothing was left
uninstrumented. No Visual Studio involved; plain `dotnet test --collect "Code Coverage"` on
`UITests.Windows.csproj`.

## Measured 2026-08-05 — unit + integration + Windows UI merged

| module | block % | line % | blocks covered | blocks uncovered |
|---|---|---|---|---|
| `PokemonBattleJournal.dll` | **72.89** | 60.73 | 5269 | 1960 |
| `PokemonBattleJournal.Scraper.dll` | **98.32** | 97.50 | 117 | 2 |

Unit + integration alone were 65.63% block / 61.72% line, so the Windows UI suite is worth
roughly **+7 points of block coverage**.

**Line % goes slightly DOWN when the UI tests are merged in (61.72 → 60.73) — this is not a
regression.** The UI run loads Views, Controls, `App` and `MauiProgram`, which unit tests
never touch, so the *coverable* denominator grows faster than the covered numerator. Block
coverage rises (65.63 → 72.89) because it is measured against reachable blocks. Read block %
for the trend; that is the number VS reports and the one the user tracks.

Android UI tests are **not** included — they execute on the emulator, out of the collector's
reach. Any comparison against the historical ~80% should account for that plus the fact that
the figure was recorded before this session's changes.

ReportGenerator over the merged file: 62.4% line, 55.3% method (231 of 417). Note RG reports
line/branch only — **block coverage exists solely in the `.coverage` binary and the converted
VS XML**, so read it from those, or open `.coverage` in VS.

**coverage.runsettings:** lives at `build/coverage.runsettings` (deliberately NOT the repo root — see above); no ResultsDirectory set (VS manages its own output; custom path creates GUID subfolders per run).

**0% classes (expected, not worth chasing):** Views (XAML code-behind), Controls (ComboBox/ImagePicker), App, AppShell, MauiProgram — all MAUI DI/startup/UI code with no unit-testable surface.

**Why:** VS coverage shows a much higher number because it instruments the running app during UI test execution.

## Why 500+ tests can still feel like weak coverage (measured 2026-08-05)

User's instinct: *"i feel like my actual code coverage is actually shit even with 500+ tests."*
Investigated rather than assumed. The suite is healthier than it feels, but three real things
explain the gap:

**1. Almost nothing is a fake test.** Scanned every `[Test]` method across both test projects
for ones containing no `Should`/`Received`/`Assert`/`Throw` at all. Result: **4** — and
**all four were fixed 2026-08-05**. A re-scan of all 499 test methods now reports zero.

The original list of four was *wrong in both directions*, which is worth knowing before
trusting a scan like this again:

- The two `MatchOperationsTests` BO3 entries already asserted by the time they were revisited
  — the test-project consolidation had replaced them with the better of two duplicate copies
  ([[project_test_project_consolidation]]).
- Two others that genuinely asserted nothing were never on the list, because the first scan's
  regex (`\bShould\w*\(`) missed generic assertions like `ShouldBeOfType<T>()` — the `<`
  breaks the match. Use `\bShould\w*\s*[<(]` instead.

The two real ones, and why each was fixed rather than deleted:

- `TrainerPageViewModelTests.AppearingAsync_WithMatches_CalculatesStats` ended in a bare
  `_mockAnalysisService.CalculateWinRate(matches, out …);` — no `Received()`, so it just
  called the substitute again. Its gap was real: `Wins`/`Losses`/`Ties`/`WinAverage` were
  asserted only for the *empty*-match case. Now sets the out params through a `Returns`
  callback and asserts all four.
- `OptionsPageViewModelTests.SaveTagAsync_NullTrainer_DoesNotSave` had a comment where the
  assertion belonged. The guard-logging suite pins that the path *warns*
  ([[feedback_no_silent_guards]]); this now pins that it also declines the write.

Two `TaskUtilitiesTests` "DoesNotThrow" tests were also converted to explicit
`Should.NotThrowAsync`. They were never broken — NUnit fails a test whose exception escapes —
but the bodies read as if the assertion had been forgotten.

**2. ~21% of unit tests assert existence, not behaviour.** 106 of 494 are reflection-based
contract tests (`MainPageViewModelContractTests` 40, `TrainerPageViewModelContractTests` 27,
`ReadJournalPageViewModelContractTests` 21, `OptionsPageViewModelContractTests` 18). These are
the user's deliberate AI-guardrail strategy ([[feedback_contract_tests]]) and worth keeping —
but they pin XAML binding names to VM members, so they inflate the test count without moving
behavioural coverage. A high test count that includes them will always feel better than the
coverage number looks.

**3. 54 catch paths were structurally untestable — FIXED 2026-08-05.** Every
`ModalErrorHandler error = new();` could not be substituted, so no test could verify error
handling anywhere in the app. This was the single biggest genuine hole. `IErrorHandler` is
now injected ([[project_error_handler_di]]), and fixing it exposed a second hole underneath:
connection failures were unhandled at 20 of 22 database operations, now covered by 20 new
tests ([[project_db_session_lock_pairing]]).

**Also: the 57.7% coverlet figure understates reality.** It counts MAUI startup, XAML
codebehind and platform code that unit tests can never reach, and it does not credit anything
the 145 UI tests exercise. VS's tool reports ~80% on the same code.

**The re-measure is now unblocked and still outstanding.** The precondition was "only after
the error-handler injection lands, since unlocking 54 paths changes the picture" — that has
landed, along with the 20 connection-failure tests and the four assertion fixes above. Both
numbers in this file (57.7% coverlet, ~80% VS) and the counts in the checked-in report under
`PokemonBattleJournal/docs/coverage-report/` therefore predate all of it and should be
treated as stale until regenerated with the commands above.
