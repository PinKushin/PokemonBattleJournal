---
name: project_nunit_migration
description: NUnit migration — all Android and Windows UI tests fixed and passing; pending merge to master
metadata:
  node_type: memory
  type: project
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-07-30T23:30:00.000Z
---

Branch `feature/nunit-migration` replaces xUnit with NUnit 4 across all test projects. All Android and Windows CI failures resolved. Ready to merge to master once final CI run confirms green.

**What was done:**
- All projects: xUnit packages removed, NUnit 4.6.1 + NUnit3TestAdapter 6.2.0 added
- `[Fact]` → `[Test]`, `[Theory]`/`[InlineData]` → `[Test]`/`[TestCase]`
- 13 unit test classes: constructors → `[SetUp]` methods, `private readonly` → `private X = null!;`
- Integration tests: `IAsyncLifetime` removed, `InitializeAsync/DisposeAsync` → `[SetUp]/[TearDown]`
- UI tests: `ICollectionFixture`/`[Collection]` → `[SetUpFixture]` (`AppiumSetup`)
- `Assert.Equal(e,a)` → `Assert.That(a, Is.EqualTo(e))`

**UI test patterns established:**
- Each page class has `[OneTimeSetUp]` calling `NavigateTo("Page")` — navigates once per fixture
- `[OneTimeTearDown]` calls `InvalidateCurrentPage()` on MainPage
- Cleanup helpers (ResetBOSwitch, ResetGame1Tab, CloseWindowsPickers, ClearUserNoteInput) called in `try/finally` only by tests that mutate state
- All helpers use `ImplicitWait = TimeSpan.Zero` so missing elements fail instantly
- `BaseTest` has `[SetUp]/[TearDown]` with Stopwatch logging to `%TEMP%\UITests.PerfLog.txt`

**Android fixes committed (all on feature/nunit-migration):**
- `AndroidScrollToTop()` helper using `scrollToBeginning(100)` in cleanup to reset scroll position after BO3 tests
- `EnsureBO3On()` helper: reads `BO3StatusLabel.Text`, only clicks BOSwitch if not already "Best of 3" — fixes blind toggle that accidentally turned BO3 off when cleanup left it on
- `FindUIElement("Game2Tab")` (resourceId) instead of `AccessibilityId("Game2Tab")` after picker selection — MAUI Android re-renders tab bar native views during ShowGame3 binding update, resetting content-desc from AutomationId to SemanticProperties.Description

**Windows CI fix:**
- `ClickTab` was using `PointerKind.Touch` — silent no-op on Windows Server CI (no touch hardware)
- Changed to `PointerKind.Mouse` — WinUI TapGestureRecognizer responds to left mouse click

**Coverage:**
- `coverage.runsettings` at repo root — select via Test > Configure Run Settings > Select Solution Wide runsettings File
- `Save-CoverageResults.ps1` copies cobertura XML to docs/ with timestamp
- Fine Code Coverage VS extension reads cobertura output for inline editor highlighting

**Test counts:** 350 unit + 22 integration — all passing. Android/Windows UI tests all passing locally and on CI (pending latest Windows run confirmation).

**Next task after merge:** Add timestamped logging to AppiumSetup (Android + Windows) covering emulator launch, Appium driver init, SeedTestData timing.

**Why:** NUnit runs tests alphabetically by default — critical for understanding test ordering and state contamination between tests.
