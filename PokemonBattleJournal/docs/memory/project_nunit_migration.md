---
name: project_nunit_migration
description: NUnit migration status — complete on feature/nunit-migration, pending merge to master
metadata:
  node_type: memory
  type: project
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-07-30T20:32:00.700Z
---

Branch `feature/nunit-migration` replaces xUnit with NUnit 4 across all test projects. Status: complete, pending final UI test run verification before merge to master.

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
- Cleanup helpers (ResetBOSwitch, ResetGame1Tab, CloseWindowsPickers, ClearUserNoteInput, DeleteCreatedArchetype, DeleteCreatedTag) called in `try/finally` only by tests that mutate state
- All helpers use `ImplicitWait = TimeSpan.Zero` so missing elements fail instantly
- `BaseTest` has `[SetUp]/[TearDown]` with Stopwatch logging to `%TEMP%\UITests.PerfLog.txt`
- NavigateTo logs duration to both NavLog and PerfLog

**Test counts:** 350 unit + 22 integration — all passing. Android/Windows UI tests in progress.

**Next task:** Add timestamped logging to AppiumSetup (Android + Windows) covering: emulator/WinAppDriver launch, Appium driver init, SeedTestData start/end, individual seed steps. Write to PerfLog so full timeline from setup to first test is visible.

**Why:** Windows UI tests are now much faster after the NUnit refactor. Android deploy time is fixed overhead (~7 min EmbedAssembliesIntoApk on CI). Need AppiumSetup logging to diagnose whether remaining slowness is in setup vs tests.
