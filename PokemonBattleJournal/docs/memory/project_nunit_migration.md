---
name: project_nunit_migration
description: NUnit migration — merged to master, all CI passing, coverage work done
metadata:
  node_type: memory
  type: project
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-07-30T23:59:00.000Z
---

Branch `feature/nunit-migration` merged to master. All Android and Windows CI tests passing.

**What was done:**
- All projects: xUnit packages removed, NUnit 4.6.1 + NUnit3TestAdapter 6.2.0 added
- `[Fact]` → `[Test]`, `[Theory]`/`[InlineData]` → `[Test]`/`[TestCase]`
- 13 unit test classes: constructors → `[SetUp]` methods, `private readonly` → `private X = null!;`
- Integration tests: `IAsyncLifetime` removed, `InitializeAsync/DisposeAsync` → `[SetUp]/[TearDown]`
- UI tests: `ICollectionFixture`/`[Collection]` → `[SetUpFixture]` (`AppiumSetup`)

**Windows CI final fix (post-merge on master):**
- Root cause: game tab `Border+TapGestureRecognizer` has no UIA InvokePattern; Touch/Pen/Mouse pointer simulation all fail on CI
- Fix: converted Game1Tab/Game2Tab/Game3Tab from `Border` to `Button` (BorderWidth=0, CornerRadius=6, MinimumHeightRequest=0)
- `ClickTab` simplified to `tabElement.Click()` on all platforms — Button exposes InvokePattern, WinAppDriver invokes it directly

**Coverage (unit + integration via coverlet, master branch):**
- `coverage.runsettings` at repo root; ResultsDirectory removed (VS manages its own output path)
- ReportGenerator installed globally (`dotnet-reportgenerator-globaltool`)
- Merged report in `PokemonBattleJournal/docs/coverage-report/`
- 57.7% line / 67.8% branch / 64.5% method on PokemonBattleJournal.dll (unit+integration only)
- VS built-in coverage tool shows ~80% (includes UI test exercise of the running app)

**Coverage session additions (359 unit tests total, up from 350):**
- `MatchAnalysisService`: CalculateMatchFrequency + CalculateAverageMatchDuration with actual matches → 100%
- `ReadJournalPageViewModel`: empty-matches path, DB exception catch, Game2WithTags, Game3WithoutTags → 91.6%

**Test counts:** 359 unit + 22 integration — all passing.

**Next tasks:**
- Add timestamped logging to AppiumSetup (Android + Windows)
- Improve OptionsPageViewModel coverage (57.9%)
- Improve ArchetypeOperations / TrainerOperations coverage

**Why:** NUnit runs tests alphabetically — critical for understanding ordering and state contamination.
