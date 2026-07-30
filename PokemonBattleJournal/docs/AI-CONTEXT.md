# PokemonBattleJournal — AI Context

> **Last updated:** 2026-07-30 (NUnit migration + UI test perf refactor — see session log)
> **Solution file:** `PokemonBattleJournal.slnx` (not `.sln`)  
> **Read this first** when working in this repo. Update the [Session log](#session-log) whenever scope, decisions, or blockers change — especially before long multi-step work.

---

## Session log

Chronological notes for the current / recent work. **Append or edit this section** as conversations progress.

| Date | Topic | Status / notes |
|---|---|---|
| 2026-07-25 | **Limitless TCG scraper shipped** | `PokemonBattleJournal.Scraper` class library with SOLID/factory architecture (`IMetaDeckFetcher`, `IMetaDeckParser`, `ILimitlessMetaService`, `IMetaServiceFactory`). Upserts top-10 meta decks from limitlesstcg.com on every launch (INSERT OR IGNORE — new decks added, existing preserved). Falls back to 8 hardcoded archetypes only when offline AND table empty. Images are CDN URLs from Limitless; load natively via MAUI `Image`. `LimitlessDeckParser` fixed: guard against empty `annotationText` before `string.Replace` (threw `ArgumentException`). 11 scraper tests. |
| 2026-07-25 | **Archetype picker search** | `ComboBoxPopup` now has a `SearchBar` above the list filtering by display name in real-time (case-insensitive contains). Filter logic extracted to `internal static FilterItems(items, query, displayMemberPath)` for testability. 8 new tests in `ComboBoxPopupTests`. |
| 2026-07-25 | **BO3 tab switcher shipped** | Replaced flat BO3 VerticalStackLayout with progressive tab UI. Game 1 always visible; Game 2 tab appears when `BO3Toggle=true`; Game 3 tab appears when `ShowGame3=true` (results differ OR both Tie — per official Pokemon TCG tournament rules). No tab auto-switch on toggle. Data preserved when switching tabs (only `IsVisible`, no unloading). |
| 2026-07-25 | **Pokeball BO3 toggle shipped** | Replaced native `Switch` with tappable `ball_icon.png` `Image`. Full opacity (1.0) when BO3 on; greyed (0.3) when off via `BoolToObjectConverter`. Label shows "Best of 3" / "Best of 1" via `BoolToObjectConverter`. `ToggleBO3Command` relay command added. `AutomationId="BOSwitch"` preserved on the Image. |
| 2026-07-25 | **StartTime/EndTime fixed to TimeSpan** | `TimePicker.Time` requires `TimeSpan`; binding `DateTime` silently showed midnight. Changed both VM properties to `TimeSpan`. Defaults refreshed in `AppearingAsync` (singleton VM). Guard logic: `OnStartTimeChanged` clamps `EndTime` ≥ `StartTime`; `OnEndTimeChanged` clamps value ≥ `StartTime`. |
| 2026-07-25 | **Unit tests: 221 passing** | Up from 78. ViewModel contract + behavioral tests, scraper tests (11), ComboBoxPopup filter tests (8). |
| 2026-07-25 | **B-01/B-02/B-03 fixed** | B-01: Added `Spacing="10"` to StackLayout wrapping both archetype `ComboBoxControl`s on `MainPage.xaml`. B-02: Placeholder text changed from "Played Archetype"→"Player" and "Rival's Archetype"→"Rival". B-03: `ArchetypePicker` style `WidthRequest` reduced 210→180; `ComboBoxControl` already had `LineBreakMode.TailTruncation` on both labels so long names truncate gracefully. |
| 2026-07-25 | **docs/ reorganization + ROADMAP.md** | Moved AI files to `docs/`; README moved to `docs/README.md` (root deleted). Created `docs/ROADMAP.md` with all features (F-01→F-22) and bugs (B-01→B-05). |
| 2026-07-26 | **Page styling pass** | AboutPage, FirstStartPage, OptionsPage, ReadJournalPage all restyled: PokeYellow/PokeBlue palette, PokemonSolid/SairaRegular fonts, PokeYellow-bordered input sections. Match list cards in ReadJournalPage use PokeBlue border + result badge chips. Delete button on OptionsPage uses BostonRed. |
| 2026-07-26 | **OptionsPage icon picker → ComboBoxControl** | Replaced native `Picker` with `ComboBoxControl` (same searchable dropdown as MainPage). Added `IconItem` record (`Name`, `ImagePath`), `IconItems`/`SelectedIconItem` VM properties. `OnSelectedIconItemChanged` syncs `SelectedIcon` (image preview) and `NewDeckIcon` (save path) — also fixed pre-existing bug where `NewDeckIcon` was never set from UI. `ToDisplayName` helper strips `.png` and title-cases filename for display. 223 unit tests (2 new contract tests). |
| 2026-07-26 | **Trainer switching — shipped** | Full multi-trainer switching via `ITrainerSwitchService` (singleton event bus). `TrainerSwitchService.SwitchToAsync` sets Preferences (name + Id), fires `TrainerChanged` event. `AppShellViewModel` subscribes and syncs the flyout. `MainPageViewModel` and `TrainerPageViewModel` subscribe and reload on switch. `OptionsPageViewModel.SwitchTrainerAsync` calls the service directly. New singleton registrations: `ITrainerSwitchService`, `AppShellViewModel`, `AppShell`. `PreferencesHelper` now stores `TrainerId` (uint) as well as `TrainerName` for stable Id-based lookup. All VMs resolve trainer by Id first, fall back to name. Unsaved-data warning in `AppShellViewModel.SwitchTrainerAsync` (checks `MainPageViewModel.HasUnsavedData`). |
| 2026-07-26 | **Shell flyout — accordion trainer submenu** | Replaced broken `Shell.TitleView` Picker with `Shell.FlyoutContent` accordion. Single-column list: nav items, separator, "Switch Trainer ▶/▼" row (`ToggleTrainerMenuCommand`), indented CollectionView of trainers (`SelectTrainerCommand`). FlyoutHeader (logo) and FlyoutFooter (copyright) unchanged. |
| 2026-07-26 | **TrainerPage DateTime crash — fixed** | `BuildWinRateOverTimeChart` labeler `new DateTime((long)value)` threw `ArgumentOutOfRangeException` when LiveCharts probed with out-of-range tick values. Fixed with ticks range guard: return `string.Empty` when outside `DateTime.MinValue.Ticks..MaxValue.Ticks`. |
| 2026-07-26 | **Pokeball "Went first" toggle — shipped** | Replaced native WinUI3 `CheckBox` (shifted horizontally ~6–8px on tab switch). Replaced with tappable `ball_icon.png` `Image` + `BoolToObjectConverter` for opacity. Three relay commands: `ToggleFirstCheckCommand`, `ToggleFirstCheck2Command`, `ToggleFirstCheck3Command`. |
| 2026-07-26 | **ComboBox layout — left-aligned icon+text, right-pinned arrow** | Inner layout changed from `HorizontalStackLayout` to `Grid(*, Auto)`. `MinimumWidthRequest=130`, `MaximumWidthRequest=260`. |
| 2026-07-26 | **Checkbox shift bug — UNRESOLVED** | In BO3 mode, the CheckBox in the "Went first" row shifts ~6–8 px when switching Game tabs. Confirmed above-panel cause. Fixes tried: removed named style, removed `HorizontalOptions="Center"` from RightColumn, Tab Bar Border, game panel Grid. Leading hypothesis: FlexLayout (`JustifyContent="Center"`) re-centers `RightColumn` when natural width changes between tabs. **Next debug step:** VS Live Visual Tree to compare `ActualOffset.X` of CheckBox in Game 1 vs Game 2. |
| 2026-07-26 | **UI test coverage: all Shell pages** | Every Shell page has a navigation + element-visible Appium test. `AboutPageTests.cs` added; `AutomationId="AboutPageTitle"` added to title label. |
| 2026-07-28 | **In-app DEBUG seeding** | `App.xaml.cs` `SeedDebugDataAsync()` (compiled `#if DEBUG`) runs in App constructor via `Task.Run(...).GetAwaiter().GetResult()` — completes before MAUI visual tree starts. Seeds UITestTrainer + 3 Win matches (idempotent: if UITestTrainer exists and inactive, activates it and returns; if exists and active, returns; otherwise creates). Replaces deleted `TestSeedService`. Android AppiumSetup simplified to `adb install -r` only — no more `pm clear` or `SeedAndPushDb`. |
| 2026-07-28 | **WinUI XamlRoot crash fixed** | `MainPageViewModel.AppearingAsync()` calls `DisplayPromptAsync` (first-boot trainer-name prompt) when `_trainer == null`. On WinUI 3, `ContentDialog.ShowAsync()` requires `XamlRoot` to be set — crashes before window is composed. Root cause: `TrainerOperations.SaveAsync` inserts with `IsActive=0`; seed was not calling `SetActiveAsync`; `GetActiveAsync()` returned null; prompt fired. **Fix:** (1) Seed always calls `SetActiveAsync` after creating or finding UITestTrainer (handles crash-leftover inactive trainer). (2) `MainPageViewModel.AppearingAsync` skips prompt when `%TEMP%\PokemonBattleJournal.uitest` sentinel file present. VS `App.g.cs:71` `Debugger.Break()` is just the debug hook — not the error itself. |
| 2026-07-28 | **Sentinel file pattern** | `UITests.Windows/AppiumSetup.RunBeforeAnyTests()` writes `%TEMP%\PokemonBattleJournal.uitest` before launching; `Dispose()` deletes it. App reads `File.Exists(...)` to skip first-boot prompt under test without blocking manual debug testing. Android doesn't need it — sentinel path doesn't cross emulator boundary; in-app seed activates UITestTrainer so prompt never fires. |
| 2026-07-28 | **Serilog logs path** | Moved from `{AppDataDirectory}/log.txt` to `{AppDataDirectory}/Logs/log.txt` (rolling daily). Directory created in `MauiProgram.cs` before Serilog init. |
| 2026-07-28 | **All UI tests passing** | Windows + Android Appium tests all green in VS test runner. |
| 2026-07-29 | **Android CI build fixed** | `<MauiIcon>` path had `Resources\Appicon\` (lowercase 'i') — Linux CI case-sensitive, failed. Fixed to `Resources\AppIcon\appicon.svg`. CI now builds Android successfully. |
| 2026-07-29 | **Windows CI picker tests fixed** | MAUI Picker on Windows Server opens as child window. Added `SelectWindowsPickerItem(string)` helper to `BaseTest` — iterates all `App.WindowHandles`, switches contexts, catches only `NoSuchElementException`. `MainPageTests` updated to use it. |
| 2026-07-29 | **OptionsPageViewModel bugs fixed** | `SaveTagAsync` + `SaveArchetypeAsync` discarded return values (`_ = await SaveAsync()`). Fixed to assign. `NewDeckIcon` now pre-initialized to `"ball_icon.png"` so icon null-guard never fires silently; `finally` resets to `SelectedIcon`. UI test `OptionsPage_SaveArchetype_WithName_ClearsInput` now passes. |
| 2026-07-29 | **Integration tests added** | `TagOperationsIntegrationTests` (5 tests), `ArchetypeOperationsIntegrationTests` (6 tests), `MatchOperationsIntegrationTests` (6 tests). Pattern: `TestSqliteConnectionFactory` overrides `GetDbPath()` with unique GUID temp file; `IAsyncLifetime` for setup/teardown. `ArchetypeOperations.GetAllAsync` needs `metaService.GetTopDecksAsync` configured to return empty list — substitute returns null by default causing silent empty-list return. Tags model property is `Name` not `TagTxt`. |
| 2026-07-29 | **OptionsPageViewModel + MainPageViewModel unit tests expanded** | 8 new tests for OptionsPageVM (SwitchTrainerAsync, SaveTrainerAsync, DeleteTrainerFileAsync, AppearingAsync, SaveTagAsync/SaveArchetypeAsync zero returns). 2 new tests for MainPageVM (SaveMatchAsync success paths — BO1 and BO3). `SaveMatchAsync` uses `GetActiveAsync()` not `GetByNameAsync()`; `SetupSuccessfulSave()` helper configures both calculator and trainer mocks. Unit test count: 329 passing. |
| 2026-07-30 | **NUnit migration — all test projects** | Branch `feature/nunit-migration`. Replaced xUnit with NUnit 4.6.1 + NUnit3TestAdapter 6.2.0 across `PokemonBattleJournal.Tests`, `PokemonBattleJournal.IntegrationTests`, and all `UITests.*` projects. `[Fact]`→`[Test]`, `[Theory]`/`[InlineData]`→`[Test]`/`[TestCase]`. 13 unit test classes: constructors→`[SetUp]`, `private readonly`→`private X = null!;`, `[FixtureLifeCycle]` removed. Integration tests: `IAsyncLifetime` removed, `InitializeAsync/DisposeAsync`→`[SetUp]/[TearDown]`. UI tests: `ICollectionFixture`/`[Collection]`→NUnit `[SetUpFixture]`. `Assert.Equal(e,a)`→`Assert.That(a, Is.EqualTo(e))`. 350 unit + 22 integration passing. |
| 2026-07-30 | **UI test NUnit patterns — [OneTimeSetUp] + targeted cleanup** | Each shared page test class has `[OneTimeSetUp]` calling `NavigateTo("Page")` — navigates once per fixture not per test. `MainPageTests` has `[OneTimeTearDown]` calling `InvalidateCurrentPage()` (singleton VM). Per-test cleanup is targeted helpers (`ResetBOSwitch`, `ResetGame1Tab`, `CloseWindowsPickers`, `ClearUserNoteInput`, `DeleteCreatedArchetype`, `DeleteCreatedTag`) called in `try/finally` only by mutating tests. Display-only tests have zero cleanup overhead. All helpers: `ImplicitWait = TimeSpan.Zero` + raw `App.FindElement` (not `FindUIElement`) so 0ms is respected. Removed all `Task.Delay` waits — replaced with implicit-wait polling. Windows UI tests confirmed much faster. |
| 2026-07-30 | **BaseTest perf logging** | `%TEMP%\UITests.PerfLog.txt` — `[SetUp]` starts Stopwatch and logs `START {TestName}`, `[TearDown]` logs `END {TestName} [Status] {ms}ms`. `NavigateTo` logs nav duration to both NavLog and PerfLog. Enables per-test and per-navigation timing diagnostics without instrumentation in each test. |
| 2026-07-30 | **docs/ moved to PokemonBattleJournal/docs/** | VS Solution Explorer includes `PokemonBattleJournal/docs/` (project item). All CLAUDE.md path references updated. `docs/memory/` (repo-local memory) lives at `PokemonBattleJournal/docs/memory/`. `AI-CONTEXT.md` at `PokemonBattleJournal/docs/AI-CONTEXT.md`. |

### User decisions

| Topic | Decision |
|---|---|
| **Release platforms** | Windows and Android (user tests both; no Mac hardware). |
| **Multi-trainer** | Full switching shipped. |
| **Android UI tests** | Tied to `pixel_7_-_api_35` — OK for now. |
| **AI onboarding** | `docs/AI-CONTEXT.md` is the canonical context doc. |
| **TrainerPage stats UI** | Not current priority. |
| **Test environment isolation** | Sentinel file pattern — not `#if DEBUG`. Manual debug sessions must still see first-boot prompt. |
| **Seeding** | In-app `#if DEBUG` in `App.xaml.cs` — no external DB manipulation, no TestSeedService. |
| **Test framework** | NUnit 4 across all test projects (unit + integration + UI). Single framework, no xUnit. |
| **UI test cleanup** | Targeted helpers in `try/finally` only for mutating tests — no blanket `[TearDown]` driver calls. |
| **UI test navigation** | `[OneTimeSetUp]` per page class — single `NavigateTo` per fixture, not per test. |

### Active work

- [x] Fix `ComboBoxPopup` empty dropdown
- [x] Fix Windows Appium path
- [x] .NET 10 migration
- [x] LiveCharts2 installed + ViewModel chart data wired
- [x] TrainerPage hang root cause diagnosed (CartesianChart WinUI3 deadlock)
- [x] BO3 tab switcher (Game 1/2/3 tabs, ShowGame3, progressive reveal)
- [x] Pokeball BO3 toggle (replace native Switch with tappable Image)
- [x] StartTime/EndTime TimeSpan fix; AppearingAsync refresh
- [x] `PokemonBattleJournal.Scraper` project — shipped; upserts top-10 on every launch; CDN images; offline fallback
- [x] Test coverage for BO3 tab features — 221+ tests passing
- [x] Archetype picker search — `ComboBoxPopup.FilterItems` extracted + tested
- [x] B-01/B-02/B-03 — dropdown spacing, placeholder text, width reduced to 180
- [x] Page styling pass — AboutPage, FirstStartPage, OptionsPage, ReadJournalPage
- [x] OptionsPage icon picker — replaced native Picker with ComboBoxControl; fixed NewDeckIcon wiring bug
- [x] UI test coverage — all 5 Shell pages have navigation + element-visible Appium tests
- [x] Trainer switching — ITrainerSwitchService, AppShellViewModel accordion flyout, Options page list
- [x] TrainerPage DateTime crash — labeler ticks range guard
- [x] Pokeball "Went first" toggle — replaces native CheckBox
- [x] ComboBox layout — left-align icon+text, right-pin arrow, auto-size 130–260px
- [x] WinUI XamlRoot crash — sentinel file + SetActiveAsync in seed
- [x] In-app DEBUG seeding — replaces TestSeedService; idempotent UITestTrainer creation
- [x] All UI tests passing (2026-07-28)
- [x] NUnit migration — all test projects (2026-07-30, branch feature/nunit-migration)
- [x] UI test [OneTimeSetUp] navigation + targeted cleanup helpers (2026-07-30)
- [x] BaseTest perf logging to UITests.PerfLog.txt (2026-07-30)
- [ ] **Add AppiumSetup timestamped logging** — cover emulator/WinAppDriver launch, Appium init, SeedTestData start/end, individual seed steps; write to PerfLog for full timeline
- [ ] **Merge feature/nunit-migration → master** once Android run confirmed passing
- [ ] **Fix TrainerPage charts** — lazy/virtualized `CartesianChart` loading to avoid WinUI3 deadlock
- [ ] **Harden concurrency** — fix static semaphore on transient `TrainerPageViewModel`
- [ ] Configurable Android Appium emulator (future)

---

## Project overview

**Pokemon Battle Journal** is a .NET MAUI app for logging and analyzing **Pokemon TCG (PTCG)** battle records. Users record BO1/BO3 matches with archetypes, tags, times, and notes; browse history; and view trainer stats.

- **Author / package id:** `com.PinKushin.PokemonBattleJournal`
- **License:** The Unlicense (`LICENSE.txt`)
- **Pattern:** MVVM with CommunityToolkit.Mvvm source generators
- **Data:** Local SQLite (`PokemonBattleJournal.db3` in app data — GUID-based path on Windows unpackaged)

---

## Tech stack

| Area | Technology |
|---|---|
| Runtime | .NET 10.0 + MAUI |
| Platforms | Android 21+, iOS 15+, MacCatalyst 15+, Windows 10 19041+ |
| Database | `sqlite-net-pcl`, `SQLite.Net.Extensions.Async`, `SQLitePCLRaw.bundle_green` |
| MVVM | CommunityToolkit.Maui 15.x, CommunityToolkit.Mvvm 8.x |
| UI | Native MAUI controls + custom `ComboBoxControl`, `ImagePicker` |
| Charts | `LiveChartsCore.SkiaSharpView.Maui` 2.0.5 — 8 `CartesianChart` on TrainerPage; currently Label placeholders due to WinUI3 init deadlock |
| Logging | Serilog → debug + rolling file (`{AppDataDirectory}/Logs/log.txt`) |
| Errors | Sentry.Maui (DSN in `MauiProgram.cs`) |
| Unit tests | NUnit 4.6.1, NUnit3TestAdapter 6.2.0, Shouldly, NSubstitute |
| UI tests | Appium (Windows + Android runners, shared tests) |
| Benchmarks | BenchmarkDotNet |

**Syncfusion:** fully removed.

---

## Solution structure (`PokemonBattleJournal.slnx`)

```
PokemonBattleJournal.slnx
├── PokemonBattleJournal/                 # Main MAUI app (Deploy)
│   ├── Models/                           # SQLite ORM entities
│   ├── ViewModels/                       # ObservableObject + RelayCommand
│   ├── Views/                            # XAML Shell pages
│   ├── Services/                         # DB + business logic
│   ├── Interfaces/                       # Service contracts
│   ├── Utilities/                        # File, prefs, threading, calculations
│   ├── Controls/                         # ComboBoxControl, ImagePicker
│   ├── Platforms/                        # Android, iOS, MacCatalyst, Windows, Tizen
│   └── Resources/                        # Fonts, sprites, styles, images
├── PokemonBattleJournal.Tests/           # Unit tests (excluded from Release solution build)
├── PokemonBattleJournal.Scraper/         # SOLID scraper library — Limitless TCG meta service
├── PokemonBattleJournal.Benchmarks/      # BenchmarkDotNet (PokemonBattleJournal.Benchmarking.csproj)
└── PokemonBattleJournal.UITests/
    ├── UITests.Shared/                   # Shared Appium tests + server helper
    ├── UITests.Windows/                  # Windows Appium runner (port 4724)
    └── UITests.Android/                  # Android Appium runner
```

**Build notes**

- Open/build with **`PokemonBattleJournal.slnx` only**. Do **not** recreate `PokemonBattleJournal.sln`.
- Debug profile launches the Windows **`.exe`** at `bin\Debug\net10.0-windows10.0.19041.0\win10-x64\PokemonBattleJournal.exe`.
- `PokemonBattleJournal.Tests` has `<Build Solution="Release|*" Project="false" />` — Release solution builds skip unit tests.
- Main app: `WindowsPackageType=None` (unpackaged Windows).
- Windows DB path is GUID-based (`%LOCALAPPDATA%\User Name\{GUID}\Data\PokemonBattleJournal.db3`) — external processes can't compute it; use in-app seeding.
- After failed Appium runs: `Stop-Process -Name PokemonBattleJournal -Force -ErrorAction SilentlyContinue`

---

## App navigation & lifecycle

```
App constructor
  └─ SeedDebugDataAsync()  (#if DEBUG — blocks until done)
       └─ UITestTrainer created/activated + 3 Win matches inserted
App.CreateWindow()
  └─ AppShell (flyout)
       ├─ MainPage          — create match entries
       ├─ ReadJournalPage   — browse past matches
       ├─ TrainerPage       — stats dashboard
       ├─ OptionsPage       — trainer name, archetypes, tags
       └─ AboutPage         — credits
```

- **Shell:** flyout navigation (`AppShell.xaml`).
- **DI:** `MauiProgram.cs` registers singletons for DB factory, analysis, calculators, trainer switch service, AppShellViewModel, AppShell, MainPage+VM; other pages transient.
- **First-boot prompt:** `MainPageViewModel.AppearingAsync()` shows `DisplayPromptAsync` when `_trainer == null` AND sentinel file `%TEMP%\PokemonBattleJournal.uitest` is absent. In DEBUG + test runs, UITestTrainer is active so `_trainer != null` and sentinel also suppresses it as a second guard.
- **Windows-only:** `CollectionViewHandler` mapping disables multi-select checkbox.

---

## Domain model

### Entities

| Entity | Key fields | Relationships |
|---|---|---|
| `Trainer` | `Id`, `Name` (unique), `IsActive` | → Archetypes, Tags, MatchEntries |
| `Archetype` | `Id`, `Name`, `ImagePath`, `TrainerId` | → Trainer; used in matches (Playing/Against) |
| `Tags` | `Id`, `Name`, `TrainerId` | → Trainer; M2M → `Game` via `TagGame` |
| `Game` | `Id`, `Result?`, `Turn`, `Notes?` | M2M → Tags |
| `TagGame` | `GameId`, `TagId` | Junction |
| `MatchEntry` | `Id`, trainer/archetype FKs, `Result?`, `Game1/2/3Id`, times, `DatePlayed` | → Trainer, archetypes, games |

**Important:** `TrainerOperations.SaveAsync` inserts with `IsActive=false` (default). Must always call `SetActiveAsync` immediately after to make the trainer visible to `GetActiveAsync()`.

### Enums & chart DTOs

```csharp
public enum MatchResult { Win, Loss, Tie }
public class ChartDataPoint { string? Label; double Value; }
public class TimeDataPoint { DateTime Date; double Value; }
```

---

## Architecture

```
Views (XAML) ──bind──► ViewModels ──call──► Services ──► ISqliteConnectionFactory ──► SQLite
                              │
                              └── ModalErrorHandler (alerts on errors)
```

- **Concurrency:** static `SemaphoreSlim` on `SqliteConnectionFactory` (correct — singleton); **WARNING:** `TrainerPageViewModel` also has `static SemaphoreSlim _semaphore` but is registered Transient — shared across instances, can deadlock if counter hits 0 at GC. Hardening planned.
- **Transactions:** `RunInTransactionAsync` for multi-step saves/deletes.
- **Match results:** `MatchResultCalculatorFactory` → `BO1ResultCalculator` or `BO3ResultCalculator`.
- **Stats:** `MatchAnalysisService` (11 calculation methods) feeds `TrainerPageViewModel`.
- **Test detection:** `DeviceInfo.Platform == DevicePlatform.Unknown` ⇒ unit test environment.

### DI registration (`MauiProgram.cs`)

| Lifetime | Types |
|---|---|
| Singleton | `ISqliteConnectionFactory`, `IMatchResultsCalculatorFactory`, `IMatchAnalysisService`, `ITrainerSwitchService`, `AppShellViewModel`, `AppShell`, `MainPage`, `MainPageViewModel` |
| Transient | All other pages + ViewModels |

---

## Pages & ViewModels

| Page | VM | Purpose | Notable UI |
|---|---|---|---|
| `MainPage` | `MainPageViewModel` | Log BO1/BO3 matches | 2× `ComboBoxControl` (archetypes), native `TimePicker`/`DatePicker`/`Picker`, tag `CollectionView`, save/validate |
| `ReadJournalPage` | `ReadJournalPageViewModel` | Match history browser | `CollectionView`, game/tag detail panels |
| `TrainerPage` | `TrainerPageViewModel` | Stats dashboard | Stat labels + 8 `lvc:CartesianChart` sections (**currently Label placeholders** — charts deadlock WinUI3 on init; lazy loading needed) |
| `OptionsPage` | `OptionsPageViewModel` | Trainer, archetype, tag CRUD | `Border`+`Entry`, `ComboBoxControl` icon picker, buttons |
| `AboutPage` | `AboutPageViewModel` | Credits | Static content |

---

## Services layer

| Service | Role |
|---|---|
| `SqliteConnectionFactory` | Connection init, table creation, exposes `Trainers`/`Matches`/`Archetypes`/`Tags` ops |
| `MatchOperations` | Save/get/delete matches + games + tag links (transactional) |
| `TrainerOperations` | Trainer CRUD; `SaveAsync` inserts `IsActive=0`; must call `SetActiveAsync` separately |
| `ArchetypeOperations` | CRUD; blocks delete if used; seeds defaults |
| `TagOperations` | CRUD; cascades `TagGame`; seeds defaults |
| `MatchAnalysisService` | Win rate, archetypes, tags, opponents, streaks, duration, etc. |
| `TrainerSwitchService` | Singleton event bus. `SwitchToAsync` sets Preferences (name + Id), fires `TrainerChanged(Trainer)`. VMs subscribe in constructor, reload data on event. |
| `BO1ResultCalculator` / `BO3ResultCalculator` | Aggregate game results into match result |
| `ModalErrorHandler` | Shows error alerts (`IErrorHandler`) |

**Win rate formula (canonical):** `(wins + 0.5 * ties) / total * 100` in `Calculations.CalculateWinRate`.

---

## Custom controls

| Control | Location | Purpose |
|---|---|---|
| `ComboBoxControl` | `Controls/ComboBoxControl/` | MainPage + OptionsPage archetype/icon picker (icon + name popup, searchable) |
| `ImagePicker` | `Controls/ImagePicker.cs` | Options page icon selection |

Text inputs use **Border + Label + Entry** (not a separate `HintedEntry` control).

---

## Test coverage

### ViewModel binding contracts

Each page ViewModel has a `{VM}ContractTests.cs` in `PokemonBattleJournal.Tests/ViewModels/` that uses reflection to assert every XAML-bound property and command still exists. **Do not rename or remove any of these members without updating the contract tests.**

XAML bindings by page:

| Page | ViewModel | Bound properties | Bound commands |
|---|---|---|---|
| MainPage | `MainPageViewModel` | WelcomeMsg, Archetypes, PlayerSelected, RivalSelected, BO3Toggle, StartTime, EndTime, DatePlayed, CurrentDateTimeDisplay, TagCollection, TagsSelected, UserNoteInput, FirstCheck, PossibleResults, Result, SavedFileDisplay, Match2TagsSelected, UserNoteInput2, FirstCheck2, Result2, Match3TagsSelected, UserNoteInput3, FirstCheck3, Result3, ShowGame3, IsGame1Selected, IsGame2Selected, IsGame3Selected, HasValidationErrors, ValidationMessage | AppearingCommand, DisappearingCommand, SaveMatchCommand, SelectGame1Command, SelectGame2Command, SelectGame3Command, ToggleBO3Command, ToggleFirstCheckCommand, ToggleFirstCheck2Command, ToggleFirstCheck3Command |
| OptionsPage | `OptionsPageViewModel` | Title, NameInput, NewDeckName, SelectedIcon, IconCollection, TagInput, AllTrainers | AppearingCommand, SaveTrainerCommand, SaveArchetypeCommand, SaveTagCommand, SaveAllCommand, DeleteTrainerFileCommand, SwitchTrainerCommand, DeleteTrainerFromListCommand |
| ReadJournalPage | `ReadJournalPageViewModel` | WelcomeMsg, MatchHistory, SelectedMatch, SelectedNote, PlayingName, PlayingIconSource, AgainstName, AgainstIconSource, DatePlayed, Game1TagsInfo, Game2TagsInfo, Game3TagsInfo, HasGame1Tags, HasGame2Tags, HasGame3Tags, TagsSelectedGame1, TagsSelectedGame2, TagsSelectedGame3, Result | AppearingCommand, LoadMatchCommand |
| TrainerPage | `TrainerPageViewModel` | WelcomeMsg, WinAverage, Wins, Losses, Ties, AverageMatchDuration, FirstTurnAdvantage, StreakInfo, MostPlayedArchetypes, ArchetypeWinRates, OpponentPerformance, TagUsage, WinRateOverTime, WinRateByMatchLength | AppearingCommand |
| FirstStartPage | `FirstStartPageViewModel` | TrainerNameInput | SaveTrainerNameCommand |

### Unit tests

350 passing + 22 integration tests (NUnit 4.6.1, NSubstitute, Shouldly).

**Still lightly covered:**
- `SqliteConnectionFactory` init (integration-style)
- `ModalErrorHandler`, `FileHelper`, `PreferencesHelper`, `MainThreadHelper`
- End-to-end UI flows beyond basic Appium smoke tests

### UI tests (Appium)

| Runner | Status |
|---|---|
| `UITests.Windows` | Passing. Port 4724. Sentinel file written before launch. `CleanupTestTrainer()` in Dispose deletes UITestTrainer + cascade. SQLite packages in csproj for teardown. |
| `UITests.Android` | Passing. `adb install -r` only; in-app seed handles data idempotently. AVD `pixel_7_-_api_35`. |
| `UITests.Shared` | `AppWindowTests`, `MainPageTests`, `AboutPageTests`, `OptionsPageTests`, `ReadJournalPageTests`, `TrainerPageTests` |

**UI test NUnit patterns (established 2026-07-30):**
- `[OneTimeSetUp]` calls `NavigateTo("Page")` — once per fixture class, not per test
- `[OneTimeTearDown]` calls `InvalidateCurrentPage()` on MainPage (singleton VM — state doesn't reset on navigate-away)
- Cleanup helpers use `ImplicitWait = TimeSpan.Zero` + raw `App.FindElement` (not `FindUIElement` which ignores ImplicitWait) — called in `try/finally` only by tests that mutate state
- `BaseTest.[SetUp]` starts per-test Stopwatch; `[TearDown]` writes `END {test} [status] {ms}ms` to `%TEMP%\UITests.PerfLog.txt`
- No `Task.Delay` anywhere — all waits via implicit-wait polling

**Seeding flow:**
1. Windows `AppiumSetup.RunBeforeAnyTests()` writes sentinel file, starts Appium (port 4724), launches exe.
2. App constructor runs `SeedDebugDataAsync()` — creates/activates UITestTrainer + 3 Win matches.
3. `MainPageViewModel.AppearingAsync()` finds active UITestTrainer, skips first-boot prompt (sentinel also guards).
4. Tests run against seeded state.
5. `Dispose()`: kills app, calls `CleanupTestTrainer()` (deletes only UITestTrainer data), deletes sentinel.

**Android seeding:** same in-app seed runs on install. No sentinel needed — `DisplayPromptAsync` uses native Android dialogs (no XamlRoot requirement); UITestTrainer active means prompt doesn't fire anyway.

### Benchmarks

- Project: `PokemonBattleJournal.Benchmarks` / `ViewModels/MainPageViewModelBenchmarks`
- Requires **Release** build; use `Run.ps1`.

---

## Platform notes

| Platform | Notes |
|---|---|
| Windows | Unpackaged; exe at `bin\Debug\net10.0-windows10.0.19041.0\win10-x64\PokemonBattleJournal.exe`; DB at `%LOCALAPPDATA%\User Name\{GUID}\Data\PokemonBattleJournal.db3` |
| Android | `RunAOTCompilation=False`, `PublishTrimmed=False` in Release; AVD `pixel_7_-_api_35` |
| iOS / MacCatalyst | Min OS 15.0 |

---

## Code conventions

- `[ObservableProperty]` / `[RelayCommand]` — CommunityToolkit source generators
- Async DB access always under `SemaphoreSlim`
- Errors: `try/catch` + `ModalErrorHandler.HandleError`
- Logging: `_logger.LogInformation/Debug/Warning/Error` throughout services/VMs; logs at `{AppDataDirectory}/Logs/log.txt`
- Tests: `{Class}Tests`, methods `{Method}_{Scenario}_{Expected}`

---

## Roadmap

| Item | Status |
|---|---|
| Remove Syncfusion | ✅ Done |
| Expand unit tests | ✅ 221+ tests passing |
| Fix MainPage archetype ComboBoxControl | ✅ Done |
| Fix Windows Appium path | ✅ Done |
| Multi-trainer switcher UI | ✅ Shipped |
| In-app DEBUG seeding | ✅ Shipped (2026-07-28) |
| WinUI XamlRoot crash fix | ✅ Shipped (2026-07-28) |
| TrainerPage charts (LiveCharts2) | 🔲 In progress — VM ready, XAML has placeholders; lazy loading needed |
| Configurable Android Appium AVD | 🔲 Deferred |
| .NET 10 upgrade | ✅ Done |
| JSON import/export (TrainerHill format) | 🔲 Planned |
| Deck maker (deck lists tied to archetypes) | 🔲 Planned |
| Deck comparer (side-by-side diff) | 🔲 Planned |

---

## Commands cheat sheet

```powershell
# Build main app (Windows)
dotnet build PokemonBattleJournal.slnx -f net10.0-windows10.0.19041.0

# Unit tests only
dotnet test PokemonBattleJournal.Tests/PokemonBattleJournal.Tests.csproj

# Windows UI tests
dotnet test PokemonBattleJournal.UITests/UITests.Windows/UITests.Windows.csproj

# Android UI tests (needs pixel_7_-_api_35 AVD)
dotnet test PokemonBattleJournal.UITests/UITests.Android/UITests.Android.csproj

# Kill orphaned app after failed Appium run
Stop-Process -Name PokemonBattleJournal -Force -ErrorAction SilentlyContinue

# Benchmarks (Release)
.\PokemonBattleJournal.Benchmarks\Run.ps1
```

---

## For AI assistants — maintenance rules

1. **Read `AI-CONTEXT.md`** (this file) at the start of a session.
2. **Read `docs/memory/`** — persistent memory files for user preferences, feedback, and project decisions.
3. **Update [Session log](#session-log)** when: user states a new goal, you discover a bug/blocker, you finish significant work, before a long multi-file refactor.
4. **Keep facts accurate:** prefer reading code over trusting stale sections.
5. **Do not commit** unless the user asks.
6. **Minimize scope** — match existing patterns; don't reintroduce Syncfusion or heavy dependencies without explicit approval.
7. **TrainerOperations.SaveAsync** always creates `IsActive=false` — always call `SetActiveAsync` after programmatic trainer creation.
8. **Sentinel file** (`%TEMP%\PokemonBattleJournal.uitest`) suppresses first-boot prompt under test — never use `#if DEBUG` for this guard.
