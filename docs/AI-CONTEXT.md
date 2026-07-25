# PokemonBattleJournal — AI Context

> **Last updated:** 2026-07-25 (TrainerPage hang diagnosed; concurrency hardening in progress)
> **Solution file:** `PokemonBattleJournal.slnx` (not `.sln`)  
> **Read this first** when working in this repo. Update the [Session log](#session-log) whenever scope, decisions, or blockers change — especially before long multi-step work.

---

## Session log

Chronological notes for the current / recent work. **Append or edit this section** as conversations progress.

| Date | Topic | Status / notes |
|---|---|---|
| 2026-07-25 | **TrainerPage hang — root cause found** | `lvc:CartesianChart` (LiveCharts2 2.0.5) deadlocks the WinUI3 message pump during initialization, even with `AnimationsSpeed="0" EasingFunction="{x:Null}"`. Confirmed by replacing all 8 charts with Label placeholders — page loads instantly. Fix: chart controls must be lazy-loaded or virtualized so they don't all initialize at once on navigation. **TrainerPage.xaml currently uses placeholder Labels; charts not restored yet.** |
| 2026-07-25 | **UraniumUI experiment — tried and reverted** | Installed `UraniumUI.Material` to get styled `TextField`/`PickerField`/`TimePickerField`/`DatePickerField`. Blockers: `PickerField` has no image support (no item templates), `material:CheckBox` doesn't exist, MD3 color system didn't pick up app colors. Reverted cleanly via `git revert 68adcb9 --no-edit`. All pages, controls, MauiProgram restored. |
| 2026-07-25 | **LiveCharts2 installed** | Added `LiveChartsCore.SkiaSharpView.Maui 2.0.5`. `UseLiveCharts()` in MauiProgram. `TrainerPageViewModel` has 8 chart property sets (ISeries[], ICartesianAxis[]) and all 8 `Build*Chart` private methods — they are correct and ready. Only the XAML is using placeholders pending the safe lazy-load implementation. |
| 2026-07-25 | **Concurrency architecture concerns** | `TrainerPageViewModel._semaphore` is `static` on a `Transient` VM — shared across all instances, counter can get stuck at 0 if an instance is GC'd while holding the lock. `AsyncRelayCommand` already prevents concurrent invocations, so the VM-level semaphore may be redundant. DB semaphore in `SqliteConnectionFactory` is correct (static on a singleton). **Next: audit and harden concurrency throughout ViewModels.** |
| 2026-07-25 | **ViewModel contract tests** | Adding reflection-based contract tests to `PokemonBattleJournal.Tests/ViewModels/` — one file per page VM pinning all XAML-bound property/command names. Strategy: AI guardrail so renames break tests. Binding lists captured in AI-CONTEXT.md. In progress. |
| 2026-07-25 | **Package updates + SQLite vuln fix** | **Done.** All packages updated to latest. SQLite vulnerability (GHSA-2m69-gcr7-jv3q) fixed by pinning `SQLitePCLRaw.lib.e_sqlite3` → 3.53.3 and `SQLitePCLRaw.lib.e_sqlite3.android` → 2.1.12 as direct refs. `sqlite-net-pcl` → 1.11.285. `Microsoft.NET.Test.Sdk` → 18.8.1. `Appium.WebDriver` → 8.3.2. Serilog family updated. 78 tests pass. |
| 2026-07-25 | **.NET 10 migration** | **Done.** All projects updated to `net10.0` TFMs. CommunityToolkit.Maui → 15.0.0 (Popup API: `Popup<T>` for typed results, `CloseAsync(null/result)`, `ShowPopupAsync<T>(page, popup, new PopupOptions())` from `CommunityToolkit.Maui.Extensions`). CommunityToolkit.Mvvm → 8.4.2. Sentry → 6.7.0. Microsoft.Maui.Controls → 10.0.90. 78 unit tests pass on net10.0. |
| 2026-07-25 | Solution contextualization + living AI docs | User requested full solution map and `docs/AI-CONTEXT.md` kept current for future AI sessions (Claude, Cursor, etc.). |
| 2026-07-25 | **Top priority: MainPage archetype picker** | `ComboBoxControl` dropdown should show icon + name. **Fixed:** (1) `ComboBoxPopup` used object initializer for `ItemsSource` *after* ctor — `CollectionView` always got `null`; now passed via ctor. (2) `ViewCell` in `CollectionView.ItemTemplate` — invalid in MAUI; return `Grid` directly. Same fix applied to `ImagePickerPopup`. |
| 2026-07-25 | IDE run/debug broken | **Cause:** stale `PokemonBattleJournal.sln` referenced deleted `PokemonBattleJournal.UI.Tests` project; Cursor/VS Code tasks pointed at `.sln`. **Fix:** removed `.sln`; use `PokemonBattleJournal.slnx` only. Updated `.vscode/settings.json` (`dotnet.defaultSolution`), `tasks.json`, `launch.json` (Windows MAUI exe). |
| 2026-07-25 | Orphan processes after UI tests | Appium Windows tests can leave `PokemonBattleJournal.exe` running and lock rebuilds. Kill manually: `Stop-Process -Name PokemonBattleJournal -Force -ErrorAction SilentlyContinue` |
| 2026-07-25 | Unit tests | **78 passing** (`PokemonBattleJournal.Tests`). |
| 2026-07-25 | Android Appium UI tests | **4 passing** with local emulator `pixel_7_-_api_35`. Hardcoded to user's setup — note for later: make AVD/path configurable (VS 2026 + MAUI). |
| 2026-07-25 | Benchmarks | Fail under Debug; use Release + `Run.ps1`. |
| — | Syncfusion removal | **Done.** Native MAUI + custom controls only. |
| — | .NET 10 upgrade | **Planned next phase** after picker/stabilization. User open to adding a shared picker dependency post-.NET 10 if custom controls remain painful. |
| — | TrainerPage visualizations | Lists/labels only; richer visuals deferred. |
| — | Multi-trainer | **Planned.** Options page already has **Create New Trainer** (`SaveTrainerAsync` + `PreferencesHelper`). Not a trainer switcher yet — only creates/renames via preferences. |
| — | `origin/sqlite` branch | **Ignore** — SQLite work merged to master. |
| — | Cursor rules | `.cursor/` added to `.gitignore`; local rule points here. Shared doc is this file. |

### User decisions (2026-07-25)

| Topic | Decision |
|---|---|
| **Top priority** | Fix MainPage archetype `ComboBoxControl` (image + name dropdown). Avoid new dependency unless migrating everything to a library post-.NET 10. |
| **Release platforms** | All 4 MAUI targets matter. User tests on **Windows** and **Android** only (no Mac hardware). Support as far back as MAUI allows on those two. |
| **Multi-trainer** | Planned. Options page has create-trainer flow; full multi-trainer UX (switch/list) not built yet. |
| **Android UI tests** | Tied to `pixel_7_-_api_35` — OK for now; refactor later if needed. |
| **AI onboarding** | `docs/AI-CONTEXT.md` is the canonical context doc (not Cursor-specific). |
| **TrainerPage stats UI** | Not current priority. |
| **Windows Appium** | Fix path quoting — done. |

### Active work

- [x] Fix `ComboBoxPopup` empty dropdown
- [x] Fix Windows Appium path
- [x] .NET 10 migration
- [x] LiveCharts2 installed + ViewModel chart data wired
- [x] TrainerPage hang root cause diagnosed (CartesianChart WinUI3 deadlock)
- [ ] **Fix TrainerPage charts** — implement lazy/virtualized chart loading so `CartesianChart` controls don't all initialize on navigation
- [ ] **Harden concurrency** — audit all VM semaphores; fix static semaphore on transient `TrainerPageViewModel`
- [ ] Multi-trainer switcher UI (future)
- [ ] Configurable Android Appium emulator (future)


---

## Project overview

**Pokemon Battle Journal** is a .NET MAUI app for logging and analyzing **Pokemon TCG (PTCG)** battle records. Users record BO1/BO3 matches with archetypes, tags, times, and notes; browse history; and view trainer stats.

- **Author / package id:** `com.PinKushin.PokemonBattleJournal`
- **License:** The Unlicense (`LICENSE.txt`)
- **Pattern:** MVVM with CommunityToolkit.Mvvm source generators
- **Data:** Local SQLite (`PokemonBattleJournal.db3` in app data)

---

## Tech stack

| Area | Technology |
|---|---|
| Runtime | .NET 10.0 + MAUI |
| Platforms | Android 21+, iOS 15+, MacCatalyst 15+, Windows 10 19041+ (Tizen scaffold present, not in active TFM list) |
| Database | `sqlite-net-pcl`, `SQLite.Net.Extensions.Async`, `SQLitePCLRaw.bundle_green` |
| MVVM | CommunityToolkit.Maui 15.x, CommunityToolkit.Mvvm 8.x |
| UI | Native MAUI controls + custom `ComboBoxControl`, `ImagePicker` |
| Charts | `LiveChartsCore.SkiaSharpView.Maui` 2.0.5 — `CartesianChart` (8 on TrainerPage); currently using Label placeholders due to WinUI3 init deadlock |
| Logging | Serilog → debug + rolling file (`log.txt` in app data) |
| Errors | Sentry.Maui (DSN in `MauiProgram.cs`) |
| Unit tests | xUnit, Shouldly, NSubstitute |
| UI tests | Appium (Windows + Android runners, shared tests) |
| Benchmarks | BenchmarkDotNet |

**Syncfusion:** fully removed. No Syncfusion packages in `PokemonBattleJournal.csproj`.

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
├── PokemonBattleJournal.Benchmarks/      # BenchmarkDotNet (PokemonBattleJournal.Benchmarking.csproj)
└── PokemonBattleJournal.UITests/
    ├── UITests.Shared/                   # Shared Appium tests + server helper
    ├── UITests.Windows/                  # Windows Appium runner
    └── UITests.Android/                  # Android Appium runner
```

**Build notes**

- Open/build with **`PokemonBattleJournal.slnx` only**. Do **not** recreate `PokemonBattleJournal.sln` — the old file referenced removed projects (`PokemonBattleJournal.UI.Tests`) and broke IDE build/debug.
- Cursor/VS Code: `.vscode/settings.json` sets `dotnet.defaultSolution` → `PokemonBattleJournal.slnx`.
- Debug profile launches the Windows **`.exe`**, not the `.dll` (`launch.json` → "Windows (MAUI)").
- `PokemonBattleJournal.Tests` has `<Build Solution="Release|*" Project="false" />` — Release solution builds skip unit tests; run tests explicitly.
- Main app: `WindowsPackageType=None` (unpackaged Windows).
- After failed Appium runs, kill orphaned app: `Stop-Process -Name PokemonBattleJournal -Force -ErrorAction SilentlyContinue`

---

## App navigation & lifecycle

```
App.CreateWindow()
  ├─ FirstStartPage (if Preferences "FirstStart" != "false")
  └─ AppShell (flyout) otherwise
       ├─ MainPage          — create match entries
       ├─ ReadJournalPage   — browse past matches
       ├─ TrainerPage       — stats dashboard
       ├─ OptionsPage       — trainer name, archetypes, tags
       └─ AboutPage         — credits
```

- **Shell:** flyout navigation (`AppShell.xaml`).
- **DI:** `MauiProgram.cs` registers singletons for DB factory, analysis, calculators; `MainPage`+VM singleton; other pages transient.
- **First start:** `FirstStartPageViewModel` saves trainer name to `PreferencesHelper` and opens `AppShell`.
- **Windows-only:** `CollectionViewHandler` mapping disables multi-select checkbox.

---

## Domain model

### Entities

| Entity | Key fields | Relationships |
|---|---|---|
| `Trainer` | `Id`, `Name` (unique) | → Archetypes, Tags, MatchEntries |
| `Archetype` | `Id`, `Name`, `ImagePath`, `TrainerId` | → Trainer; used in matches (Playing/Against) |
| `Tags` | `Id`, `Name`, `TrainerId` | → Trainer; M2M → `Game` via `TagGame` |
| `Game` | `Id`, `Result?`, `Turn`, `Notes?` | M2M → Tags |
| `TagGame` | `GameId`, `TagId` | Junction |
| `MatchEntry` | `Id`, trainer/archetype FKs, `Result?`, `Game1/2/3Id`, times, `DatePlayed` | → Trainer, archetypes, games |

### Enums & chart DTOs

```csharp
public enum MatchResult { Win, Loss, Tie }
public class ChartDataPoint { string? Label; double Value; }
public class TimeDataPoint { DateTime Date; double Value; }
```

Default archetypes/tags are seeded when tables are empty (`ArchetypeOperations`, `TagOperations`).

---

## Architecture

```
Views (XAML) ──bind──► ViewModels ──call──► Services ──► ISqliteConnectionFactory ──► SQLite
                              │
                              └── ModalErrorHandler (alerts on errors)
```

- **Concurrency:** static `SemaphoreSlim` on `SqliteConnectionFactory` (correct — singleton); **WARNING:** `TrainerPageViewModel` also has `static SemaphoreSlim _semaphore` but is registered Transient — shared across instances, can deadlock if counter hits 0 at GC. `AsyncRelayCommand` already prevents concurrent calls. Hardening planned.
- **Transactions:** `RunInTransactionAsync` for multi-step saves/deletes.
- **Match results:** `MatchResultCalculatorFactory` → `BO1ResultCalculator` or `BO3ResultCalculator`.
- **Stats:** `MatchAnalysisService` (11 calculation methods) feeds `TrainerPageViewModel`.
- **Test detection:** `DeviceInfo.Platform == DevicePlatform.Unknown` ⇒ unit test environment (no MAUI runtime).

### DI registration (`MauiProgram.cs`)

| Lifetime | Types |
|---|---|
| Singleton | `ISqliteConnectionFactory`, `IMatchResultsCalculatorFactory`, `IMatchAnalysisService`, `MainPage`, `MainPageViewModel` |
| Transient | All other pages + ViewModels |

---

## Pages & ViewModels

| Page | VM | Purpose | Notable UI |
|---|---|---|---|
| `FirstStartPage` | `FirstStartPageViewModel` | Onboarding — trainer name | `Border`+`Entry`, `Button` |
| `MainPage` | `MainPageViewModel` | Log BO1/BO3 matches | 2× `ComboBoxControl` (archetypes), native `TimePicker`/`DatePicker`/`Picker`, tag `CollectionView`, save/validate |
| `ReadJournalPage` | `ReadJournalPageViewModel` | Match history browser | `CollectionView`, game/tag detail panels |
| `TrainerPage` | `TrainerPageViewModel` | Stats dashboard | Stat labels + 8 `lvc:CartesianChart` sections (**currently Label placeholders** — charts deadlock WinUI3 on init; lazy loading needed) |
| `OptionsPage` | `OptionsPageViewModel` | Trainer, archetype, tag CRUD | `Border`+`Entry`, `Picker`, `ImagePicker`, buttons |
| `AboutPage` | `AboutPageViewModel` | Credits | Static content |

**MainPageViewModel highlights:** `AppearingAsync`, `Disappearing`, `SaveMatchAsync`, `ValidateMatchData`, BO3 toggle, per-game tags/results/first-turn flags.

---

## Services layer

| Service | Role |
|---|---|
| `SqliteConnectionFactory` | Connection init, table creation, exposes `Trainers`/`Matches`/`Archetypes`/`Tags` ops |
| `MatchOperations` | Save/get/delete matches + games + tag links (transactional) |
| `TrainerOperations` | Trainer CRUD; delete cascades via SQL on FK ids |
| `ArchetypeOperations` | CRUD; blocks delete if used; seeds defaults |
| `TagOperations` | CRUD; cascades `TagGame`; seeds defaults |
| `MatchAnalysisService` | Win rate, archetypes, tags, opponents, streaks, duration, etc. |
| `BO1ResultCalculator` / `BO3ResultCalculator` | Aggregate game results into match result |
| `ModalErrorHandler` | Shows error alerts (`IErrorHandler`) |

**Win rate formula (canonical):** `(wins + 0.5 * ties) / total * 100` in `Calculations.CalculateWinRate`. Align any new stats code with this.

---

## Custom controls

| Control | Location | Purpose | Known issues |
|---|---|---|---|
| `ComboBoxControl` | `Controls/ComboBoxControl/` | MainPage archetype picker (icon + name popup) | Fixed 2026-07-25: popup ItemsSource + ViewCell template bugs |
| `ImagePicker` | `Controls/ImagePicker.cs` | Options page icon selection | Same ViewCell fix applied to popup |

Text inputs use **Border + Label + Entry** (not a separate `HintedEntry` control).

---

## Resolved bugs (historical)

All fixed in recent commits on `master`:

1. Win rate formula inconsistency — aligned to standard formula with half-weight ties.
2. `TrainerOperations.DeleteAsync` orphaned games — SQL delete via `Game1/2/3Id` FKs.
3. `MatchOperations.DeleteAsync` deadlock — sync SQL inside transactions.
4. `OptionsPageViewModel.SaveAllAsync` deadlock — removed outer semaphore.
5. `FirstStartPageViewModel` logging — accepted minimal logging in onboarding.
6. Dead update branches in Save methods — removed unreachable `Id != 0` paths.
7. Race in `TrainerOperations.SaveAsync` — duplicate check moved inside lock.

---

## Test coverage

### ViewModel binding contracts

Each page ViewModel has a `{VM}ContractTests.cs` in `PokemonBattleJournal.Tests/ViewModels/` that uses reflection to assert every XAML-bound property and command still exists. **Do not rename or remove any of these members without updating the contract tests.** This is the primary AI guardrail for XAML/ViewModel consistency.

XAML bindings by page (source of truth for contract tests):

| Page | ViewModel | Bound properties | Bound commands |
|---|---|---|---|
| MainPage | `MainPageViewModel` | WelcomeMsg, Archetypes, PlayerSelected, RivalSelected, IsBO3 (via BO3Toggle), StartTime, EndTime, DatePlayed, CurrentDateTimeDisplay, TagCollection, TagsSelected, UserNoteInput, FirstCheck, PossibleResults, Result, SavedFileDisplay, Match2TagsSelected, UserNoteInput2, FirstCheck2, Result2, Match3TagsSelected, UserNoteInput3, FirstCheck3, Result3 | AppearingCommand, DisappearingCommand, SaveMatchCommand, BO3Toggle |
| OptionsPage | `OptionsPageViewModel` | Title, NameInput, NewDeckName, SelectedIcon, IconCollection, TagInput | AppearingCommand, SaveTrainerCommand, SaveArchetypeCommand, SaveTagCommand, SaveAllCommand, DeleteTrainerFileCommand |
| ReadJournalPage | `ReadJournalPageViewModel` | WelcomeMsg, MatchHistory, SelectedMatch, SelectedNote, PlayingName, PlayingIconSource, AgainstName, AgainstIconSource, DatePlayed, Game1TagsInfo, Game2TagsInfo, Game3TagsInfo, HasGame1Tags, HasGame2Tags, HasGame3Tags, TagsSelectedGame1, TagsSelectedGame2, TagsSelectedGame3, Result | AppearingCommand, LoadMatchCommand |
| TrainerPage | `TrainerPageViewModel` | WelcomeMsg, WinAverage, Wins, Losses, Ties, AverageMatchDuration, FirstTurnAdvantage, StreakInfo, MostPlayedArchetypes, ArchetypeWinRates, OpponentPerformance, TagUsage, WinRateOverTime, WinRateByMatchLength | AppearingCommand |
| FirstStartPage | `FirstStartPageViewModel` | TrainerNameInput | SaveTrainerNameCommand |

### Unit tests — 78 total (all passing)

| Area | File(s) | Count |
|---|---|---|
| BO1/BO3 calculators | `BO1ResultCalculatorTests`, `BO3ResultCalculatorTests`, `MatchResultCalculatorFactoryTests` | 20 |
| Match analysis | `MatchAnalysisServiceTests` | 11 |
| DB services | `MatchOperationsTests`, `TrainerOperationsTests`, `ArchetypeOperationsTests`, `TagOperationsTests` | 21 |
| ViewModels | `MainPage`, `TrainerPage`, `OptionsPage`, `ReadJournalPage` ViewModel tests | 19 |
| Utilities | `CalculationsTests`, `TaskUtilitiesTests` | 7 |

**Still lightly covered or untested**

- `SqliteConnectionFactory` init (integration-style)
- `ModalErrorHandler`, `FileHelper`, `PreferencesHelper`, `MainThreadHelper`
- End-to-end UI flows (save match, pick archetype) beyond basic Appium smoke tests

### UI tests (Appium)

| Runner | Tests | Status (2026-07-25) |
|---|---|---|
| `UITests.Android` | 4 | Pass with configured emulator + debug app |
| `UITests.Windows` | 4 | Path quoting fixed; requires built exe at hardcoded path + Appium |
| `UITests.Shared` | Shared by both | `AppWindowTests`, `MainPageTests` |

**Windows Appium setup:** hardcoded exe path in `UITests.Windows/AppiumSetup.cs` — update when machine/output path changes. Do **not** wrap path in extra quote characters.

**Android Appium setup:** hardcoded AVD `pixel_7_-_api_35` — matches author's machine; make configurable later.

### Benchmarks

- Project: `PokemonBattleJournal.Benchmarks` / `ViewModels/MainPageViewModelBenchmarks`
- Requires **Release** build of main app; use `Run.ps1`.

---

## Platform notes

| Platform | Notes |
|---|---|
| Windows | Unpackaged; Appium path points to `bin\Debug\net9.0-windows10.0.19041.0\win10-x64\PokemonBattleJournal.exe` |
| Android | `RunAOTCompilation=False`, `PublishTrimmed=False` in Release |
| iOS / MacCatalyst | Min OS 15.0 |

---

## Code conventions

- `[ObservableProperty]` / `[RelayCommand]` — CommunityToolkit source generators
- Async DB access always under `SemaphoreSlim`
- Errors: `try/catch` + `ModalErrorHandler.HandleError` (often returns defaults)
- Logging: `_logger.LogInformation/Debug/Warning/Error` throughout services/VMs
- Tests: `{Class}Tests`, methods `{Method}_{Scenario}_{Expected}`

---

## Roadmap

| Item | Status |
|---|---|
| Remove Syncfusion | ✅ Done |
| Expand unit tests | ✅ Largely done (78 tests) |
| Fix MainPage archetype ComboBoxControl | ✅ Done (2026-07-25) |
| Fix Windows Appium path | ✅ Done (2026-07-25) |
| Multi-trainer switcher UI | 🔲 Partial — create trainer on Options page only |
| TrainerPage charts (LiveCharts2) | 🔲 In progress — VM ready, XAML has placeholders; lazy loading needed to avoid WinUI3 deadlock |
| Configurable Android Appium AVD | 🔲 Deferred |
| .NET 10 upgrade | ✅ Done (2026-07-25) |
| Branch `origin/sqlite` | Ignore — merged |

---

## Commands cheat sheet

```powershell
# Build main app (Windows)
dotnet build PokemonBattleJournal.slnx -f net10.0-windows10.0.19041.0

# Unit tests only
dotnet test PokemonBattleJournal.Tests/PokemonBattleJournal.Tests.csproj

# Full test run (includes UI tests — needs Appium + emulator/device)
dotnet test PokemonBattleJournal.slnx

# Benchmarks (Release)
.\PokemonBattleJournal.Benchmarks\Run.ps1
```

---

## For AI assistants — maintenance rules

1. **Read this file** at the start of a session.
2. **Update [Session log](#session-log)** when:
   - User states a new goal or priority
   - You discover a bug, blocker, or environment constraint
   - You finish a significant chunk of work
   - Before starting a long multi-file refactor
3. **Keep facts accurate:** prefer reading code over trusting stale sections.
4. **Do not commit** unless the user asks.
5. **Minimize scope** — match existing patterns; don't reintroduce Syncfusion or heavy dependencies without explicit approval.
