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

# Kill orphaned app after failed Appium run
Stop-Process -Name PokemonBattleJournal -Force -ErrorAction SilentlyContinue
```

**Solution file:** always use `PokemonBattleJournal.slnx`. Do not recreate `PokemonBattleJournal.sln`.

## Architecture

MVVM app: `Views (XAML) → ViewModels → Services → ISqliteConnectionFactory → SQLite`

- **MVVM:** CommunityToolkit.Mvvm source generators (`[ObservableProperty]`, `[RelayCommand]`)
- **DI:** `MauiProgram.cs` — the three data pages are **singletons** (`MainPage`, `ReadJournalPage`, `TrainerPage` and their VMs, plus `AppShellViewModel`); only `OptionsPage` and `AboutPage` are transient. Singleton VMs hold state across navigations, which is why MainPage UI tests need a `[TearDown]` to reset it
- **DB access:** `using DbSession session = await _factory.BeginAsync();` **inside** the `try`. This opens the connection and takes the write lock together, and disposing releases it. Never write a bare `finally { GetLock().Release(); }` — if opening the connection failed, that releases a permit nothing took and throws `SemaphoreFullException` over the real error. See [[project_db_session_lock_pairing]]
- **Error handling:** `try/catch` + injected `IErrorHandler` (registered in `MauiProgram`, `ModalErrorHandler` in production) — no silent `catch {}`. Never `new ModalErrorHandler()`
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
