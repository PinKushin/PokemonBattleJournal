# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Living context doc

**Read `PokemonBattleJournal/docs/AI-CONTEXT.md` at the start of every session.** It is the canonical context file — architecture, domain model, session log, known bugs, and user decisions. Update its Session log before starting any long multi-file task, and again when finishing significant work.

## Long-term AI memory

**Read all files in `PokemonBattleJournal/docs/memory/` at the start of every session.** These are the persistent memory files for Claude Code — user preferences, feedback on past approaches, and project decisions that must carry across conversations. Apply them throughout the session exactly as you would local memory.

- `PokemonBattleJournal/docs/memory/MEMORY.md` — index of all memory entries
- `PokemonBattleJournal/docs/memory/feedback_*.md` — how the user wants work approached
- `PokemonBattleJournal/docs/memory/project_*.md` — project decisions and context
- `PokemonBattleJournal/docs/memory/user_*.md` — user background and preferences

**After making a memory-worthy observation** (the user corrects an approach, confirms a decision, states a preference), write both to the local memory system AND copy the updated files to `PokemonBattleJournal/docs/memory/` and commit them so the repo stays in sync.

## Commands

```powershell
# Build the app for Windows. -f must target the app project, NOT the solution —
# the test and scraper projects do not target the Windows TFM, so passing -f to
# PokemonBattleJournal.slnx fails with NETSDK1005 on every one of them.
dotnet build PokemonBattleJournal/PokemonBattleJournal.csproj -f net10.0-windows10.0.19041.0

# Build everything (all projects, all their own TFMs — no -f)
dotnet build PokemonBattleJournal.slnx

# Run (Windows)
dotnet run --project PokemonBattleJournal/PokemonBattleJournal.csproj -f net10.0-windows10.0.19041.0

# Unit tests — genuinely unit tests now, and fast (~1s for ~460). The six
# real-SQLite files that used to live here moved to the IntegrationTests
# project on 2026-08-05; nothing in here touches a database or the network.
dotnet test PokemonBattleJournal.Tests/PokemonBattleJournal.Tests.csproj

# Single unit test
dotnet test PokemonBattleJournal.Tests/PokemonBattleJournal.Tests.csproj --filter "FullyQualifiedName~MethodName"

# Integration tests (real SQLite, temp DB file per test)
dotnet test PokemonBattleJournal.IntegrationTests/PokemonBattleJournal.IntegrationTests.csproj

# Windows UI tests (WinAppDriver + Appium — app auto-built and launched)
dotnet test PokemonBattleJournal.UITests/UITests.Windows/UITests.Windows.csproj

# Android UI tests (requires pixel_7_-_api_35 AVD; emulator booted automatically).
# Default assumes the app is already deployed (VS Fast Deployment) — safe path:
# force-stop + delete .db3 only. Set ANDROID_USE_INSTALLED=0 (CI does) to force
# full EmbedAssembliesIntoApk build + adb install + pm clear.
#
# IF APP CODE CHANGED, DEPLOY FIRST — otherwise you test the previous build. That
# is wrong in both directions: an existing test passes against stale code (silent,
# ships regressions), and a NEW test fails on an element the old APK lacks (looks
# like an Android binding bug). Android alone failing a test you just wrote is
# usually a stale APK, not a platform quirk. ~40s, needs a booted emulator:
#   dotnet build PokemonBattleJournal/PokemonBattleJournal.csproj -f net10.0-android -t:Install
dotnet test PokemonBattleJournal.UITests/UITests.Android/UITests.Android.csproj

# All the CI suites locally, in CI's shape — the stand-in when GitHub Actions is
# down (self-hosted runners do NOT help there; the runner polls GitHub for work).
# UI suites run one fixture per invocation to match the per-fixture matrix; the
# script is sequential on purpose and refuses to start Android while the Windows
# suite is alive. See docs/memory/project_android_session_poisoning.md.
./build/ci-local.ps1              # unit + integration, seconds
./build/ci-local.ps1 -All         # everything; leave the machine alone
./build/ci-local.ps1 -Suites WindowsUI -Combined   # one fast pass, less faithful

# Launch the app at CI's window geometry to inspect layout the way CI sees it.
# UI tests honour UITEST_WINDOW_SIZE; this is the app's own equivalent.
PBJ_WINDOW_SIZE=754x512 dotnet run --project PokemonBattleJournal/PokemonBattleJournal.csproj -f net10.0-windows10.0.19041.0

# Reproduce CI's Windows geometry in the UI tests. Set BOTH: the size alone pins the window to
# (0,0), where screen-space and window-relative coordinates coincide — which once hid a real
# coordinate-space bug until CI, whose window sits at (85,78), failed on it.
UITEST_WINDOW_SIZE=754x512 UITEST_WINDOW_POS=85,78 dotnet test PokemonBattleJournal.UITests/UITests.Windows/UITests.Windows.csproj

# A leftover PokemonBattleJournal.exe makes WinAppDriver attach to the wrong window and fails
# the whole fixture in OneTimeSetUp — it looks exactly like a regression. Kill it first.
Stop-Process -Name PokemonBattleJournal -Force -ErrorAction SilentlyContinue


# Mutation testing — grades the assertions, not the code. Stryker still cannot
# touch the MAUI head (its internal recompile does not reproduce XAML codegen or
# the MVVM source generators, and surfaces no CS error when it fails), which is
# why the logic lives in PokemonBattleJournal.Core. Two configs, two targets.
dotnet tool restore
dotnet stryker                                  # Scraper (~5 min)
dotnet stryker --config-file stryker-core.json  # Core (slower; uses both test projects)
```

**Solution file:** always use `PokemonBattleJournal.slnx`. Do not recreate `PokemonBattleJournal.sln`.

## Architecture

MVVM app: `Views (XAML) → ViewModels → Services → ISqliteConnectionFactory → SQLite`

**Two projects hold that.** `PokemonBattleJournal` is the MAUI head — Views, ViewModels,
Controls, and exactly three files that need a platform: `ModalErrorHandler` (Shell),
`FileHelper` (FileSystem/DeviceInfo) and `MainThreadHelper` (MainThread), plus
`MauiSqliteConnectionFactory`, which exists only to answer where the database file lives.
`PokemonBattleJournal.Core` is a plain `net10.0` library holding Models, Services, Utilities,
Interfaces and Logging. Namespaces are identical across both, so a `using` never tells you
which assembly a type is in.

**Put new logic in Core unless it needs MAUI.** That is not a style preference: Stryker cannot
mutation-test the app head at all, so anything added there is unmeasured by definition. If a
new service needs a platform capability, take it as a constructor dependency or an abstract
method and let the head answer it — `SqliteConnectionFactory.GetDbPath()` is the worked example.

- **MVVM:** CommunityToolkit.Mvvm source generators (`[ObservableProperty]`, `[RelayCommand]`)
- **DI:** `MauiProgram.cs` — the three data pages are **singletons** (`MainPage`, `ReadJournalPage`, `TrainerPage` and their VMs, plus `AppShellViewModel`); only `OptionsPage` and `AboutPage` are transient. Singleton VMs hold state across navigations, which is why MainPage UI tests need a `[TearDown]` to reset it
- **DB access:** `using DbSession session = await _factory.BeginAsync();` **inside** the `try`. This opens the connection and takes the write lock together, and disposing releases it. Never write a bare `finally { GetLock().Release(); }` — if opening the connection failed, that releases a permit nothing took and throws `SemaphoreFullException` over the real error. See [[project_db_session_lock_pairing]]
- **Error handling:** `try/catch` + injected `IErrorHandler` (registered in `MauiProgram`, `ModalErrorHandler` in production) — no silent `catch {}`. Never `new ModalErrorHandler()`
- **Tracing:** `IPerformanceMonitor`/`ITimedSpan` (Core) wrap Sentry spans; `SentryPerformanceMonitor` is the adapter. Instrumented: restore and import. **Span names and descriptions are NOT covered by `SentryRedactingSink`** — that governs Serilog property values only — so the interface takes CONSTANTS and `ITimedSpan` exposes no way to attach a string. Varying detail must be numeric (`SetMeasurement`). A reflection contract test pins that shape; do not add a string setter. Note `TracesSampleRate` does not CREATE transactions, it samples ones that exist — MAUI makes none automatically, which is why tracing appeared configured for months while the dashboard stayed empty. Debug-only `SentryDiagnosticsButton` on OptionsPage sends one trace and one error to confirm delivery. See [[project_sentry_three_channels]]
- **Logging and Sentry:** local sinks keep everything; the Sentry sink is wrapped by `Logging/SentryRedactingSink` and forwards property values by **type** — numbers, bools, enums, `DateTime`, `Guid`. Strings and `{@destructured}` objects are withheld. So **log ids, counts and lengths, not names or paths**: a name in a template still reaches the local log but arrives at Sentry as `[redacted]`, which is a worse crash report than the id would have been. Do not widen the allowlist in that file to get a string through; use an enum. See [[project_sentry_privacy_audit]]
- **Test detection:** `DeviceInfo.Platform == DevicePlatform.Unknown` signals unit test environment (no MAUI runtime)

### Services

`SqliteConnectionFactory` owns table init and exposes typed operation services (`MatchOperations`, `TrainerOperations`, `ArchetypeOperations`, `TagOperations`). Those four depend on `ISqliteConnectionFactory`, not the concrete class — that abstraction is what makes connection failures injectable in tests. `MatchAnalysisService` computes all stats for `TrainerPageViewModel`. `MatchResultCalculatorFactory` selects `BO1ResultCalculator` or `BO3ResultCalculator` based on match format.

`Services/Import/TrainerHillImportService` reads TrainerHill JSON, with limits on size, depth, entry count and name lengths enforced **before** any DB write. `Services/Export/ExportService` writes two formats: TrainerHill's (archetype slugs, for interop, lossy) and a backup envelope (names verbatim, plus match timings and archetype icons — lossless). `Services/Restore/RestoreService` reads that envelope back: it merges into an existing trainer of the same name, restores archetypes before matches so icons survive, and refuses a newer envelope version rather than half-applying it. **It is not wired to any UI yet** — registered in DI, nothing calls it.

Both the import and the restore de-duplicate through `Services/MatchDuplicateKey`: `(StartTime, PlayingId, AgainstId, Result)` within one trainer. `StartTime`, never `DatePlayed` — a date picker leaves that at midnight. The key is **deliberately not authoritative**: `AgainstId` identifies a *deck*, not a person, and the model stores no opponent identity, so two opponents on the same deck in the same minute collide. A hit skips and reports; it must never delete or overwrite. See [[project_backup_restore]].

**Win rate formula (canonical):** `(wins + 0.5 * ties) / total * 100` — defined in `Utilities/Calculations.cs`. All stats code must align with this.

### Custom controls

`Controls/ComboBoxControl/` — archetype picker used on MainPage and OptionsPage (icon + name popup). `Controls/ImagePicker.cs` — icon selector on OptionsPage. Both use a popup with a `CollectionView`; the `ItemsSource` must be passed via constructor (not set after init), and item templates must return `Grid` directly (not `ViewCell`).

Popup item grids have `AutomationId` bound to `"ArchetypeItem_{Name}"` — used by Appium seed and screen readers.

## Accessibility standards

Every UI element must have:
- `AutomationId` — stable, unique identifier (used by Appium and screen readers)
- `SemanticProperties.Description` — plain-English label for screen readers
- `SemanticProperties.Hint` on tappable non-button elements — "Double tap to …"
- `SemanticProperties.HeadingLevel="Level2"` on section headers
- Images: `SemanticProperties.Description` with meaningful text (e.g., `"{Name} deck icon"`); purely decorative images get `SemanticProperties.IsInAccessibleTree="False"`
- **Tappable elements must be real controls.** A `Border`/`Grid`/`Image` with a
  `TapGestureRecognizer` exposes no UIA pattern, so a screen reader cannot activate it —
  `SemanticProperties` on such an element is announced correctly and still unusable. Use
  `Button`/`ImageButton` (Invoke), `CheckBox`/`Switch` (Toggle), or overlay a transparent
  `Button` in the same Grid cell when custom visuals are required, moving the `AutomationId`
  and command onto it. Also what makes UI tests click reliably — see
  `docs/memory/feedback_invokable_controls.md`.

- **The contract is enforced on Windows.** `MainPage_InteractiveElement_IsAnnouncedAndOperable`
  and its OptionsPage twin read the live UIA tree and require both a Name and a control pattern.
  Add new interactive elements to those `[TestCase]` lists. They do not cover Android.


## TDD workflow

For anything new: **write the failing test first, then write the code.**

1. Write the test. Run it. Confirm it fails for the right reason.
2. Write the minimum code to pass it.
3. Refactor. Tests stay green.

In this project:
- New service method → unit test in `PokemonBattleJournal.Tests` first.
- New ViewModel command → unit test asserting the expected state change first.
- New Shell page → Appium navigation + element-visible test written before the page exists.
- New seed assertion → data-presence test written before the seed logic is added.
- Bug fix → regression test that reproduces the bug, confirmed failing, before the fix.

## One temporarily accepted warning: IDE0008

The build is otherwise at **zero warnings** and that still holds. The single exception is
IDE0008, "use explicit type instead of `var`" — **758 occurrences as of 2026-08-07**, and that
is deliberate rather than the baseline slipping.

Types go at the START of a declaration, never `var`. This is the user's own convention,
predating any AI work here: *"types should always be apparent as early as possible."*
Target-typed `new()` on the right is fine and preferred — the objection is to `var`, not to
brevity. Anonymous types and tuple deconstructions have no nameable type and stay.

- **Do not suppress it.** No `#pragma`, no severity downgrade, no `.editorconfig` edit to
  quieten it. The warning IS the worklist.
- **If you touch a file, you clean that file.** Not the project, not the solution — the file you
  were editing anyway. The count only goes down.
- **Test projects are not exempt.** Style is consistent across the repo or it is not a style.
- **The exception ends at zero**, and this section goes with it. Tracked as task #21.

Distribution, so a jump is visible: Tests 390, IntegrationTests 216, app 128, Core 24,
Scraper 0.

A NEW warning of any OTHER rule is a defect. This exception does not cover it.

**This changes how CI annotations are read.** The standing rule is to hold annotations at zero,
and that is what caught a CS8602 in a new test file on 2026-08-07 that the local build had not
surfaced. With ~1000 IDE0008 warnings the raw count is permanently non-zero and a real new
warning would hide in it, so check the annotations EXCLUDING this one rule and expect zero of
everything else:

```bash
gh api repos/{owner}/{repo}/check-runs/{job-id}/annotations   --jq '[.[] | select(.message | ascii_downcase | test("ide0008") | not)] | length'
```

**Match on the message, lowercased.** A code-scanning annotation has no rule-id field — `title`
and `raw_details` come back empty — and the human text reads "Use explicit type instead of
'var'". The only place the id appears is the docs URL inside the message, in lower case. A
filter keyed on `title`, or on an upper-case `IDE0008`, silently matches nothing and reports
every annotation as a real one. Verified 2026-08-07: 140 annotations across four workflows,
0 remaining after this filter.

That number is the one that must stay at zero. When the sweep finishes, drop the filter.


## Test conventions

- **Unit tests:** `{Class}Tests`, methods `{Method}_{Scenario}_{Expected}`, NSubstitute mocks, Shouldly assertions
- **UI tests (Appium):** every Shell page needs navigation + element-visible test; every data page needs a data-presence assertion test (not just "element exists")
- `SeedTestData()` runs in `AppiumSetup` constructor: handles first-boot trainer prompt, selects "Other" for both PlayerArchetype and RivalArchetype via `ArchetypeItem_Other` AutomationId, then saves 3 Win matches. `SaveMatchAsync` clears the form on success so no navigation needed between seed iterations.
- Seed failures throw `InvalidOperationException` — never swallowed silently
- Windows UI tests: `WipeAppData()` deletes the SQLite DB before each run so first-boot prompt always fires on a clean slate (all state lives in the `.db3` — the Preferences API is not used)
- Android taps can silently miss MAUI gesture handlers — any tap-driven interaction needs the click-verify-retry pattern (see `docs/memory/feedback_android_flaky_tap_retry.md`)
- Diagnostic logs after UI test runs: `%TEMP%\UITests.PerfLog.txt` (per-element FIND stage timing, rotates `.1`/`.2`), `%TEMP%\UITests.NavLog.txt` (navigation), `%TEMP%\UITests.Android.setup.log` (AppiumSetup steps), `%TEMP%\UITests.PopupLog.txt` (in-app ComboBox popup lifecycle, pulled via adb at teardown)

## Platform notes

- Windows: unpackaged (`WindowsPackageType=None`); debug exe at `bin\Debug\net10.0-windows10.0.19041.0\win-x64\PokemonBattleJournal.exe`
- Android UI tests: AVD `pixel_7_-_api_35`; `EnsureEmulatorRunning()` verifies correct AVD by name via `adb emu avd name` and boots it if absent
- Android local workflow: deploy once from VS (Fast Deployment), then rerun tests freely — never `pm clear` a VS-deployed app (wipes `.__override__/`, crashes on launch)
- Android Release: `RunAOTCompilation=False`, `PublishTrimmed=False`
