# PokemonBattleJournal - AI Context Document

> **Last Updated:** 2026-07-24
> **Purpose:** Comprehensive reference for AI assistants working on this codebase. Contains architecture, domain model, Syncfusion removal plan, test coverage gaps, and known bugs.

---

## Project Overview

Pokemon Battle Journal is a .NET MAUI mobile/desktop app for logging and analyzing Pokemon TCG battle records. Built in C# with MVVM pattern using CommunityToolkit.Mvvm.

---

## Tech Stack

| Component | Technology |
|---|---|
| Framework | .NET 9.0 + MAUI (Android, iOS, MacCatalyst, Windows) |
| Database | SQLite via `sqlite-net-pcl` + `SQLiteNetExtensions` (async, ORM with FK relationships) |
| MVVM | CommunityToolkit.Maui + CommunityToolkit.Mvvm (ObservableProperty + RelayCommand codegen) |
| UI Controls | Syncfusion MAUI (9 packages — TO BE REMOVED) |
| Logging | Serilog (file + debug sinks) |
| Error Tracking | Sentry.Maui |
| Unit Tests | xUnit + Shouldly + NSubstitute |
| UI Tests | Appium (Windows + Android) |
| Benchmarks | BenchmarkDotNet |

---

## Solution Structure

```
PokemonBattleJournal.sln
├── PokemonBattleJournal/                    # Main MAUI app
│   ├── Models/                              # Domain entities (SQLite ORM)
│   ├── ViewModels/                          # MVVM ViewModels (ObservableObject + RelayCommand)
│   ├── Views/                               # XAML pages
│   ├── Services/                            # Business logic + DB operations
│   ├── Interfaces/                          # Service contracts
│   ├── Utilities/                           # Helpers (file, threading, preferences, calculations)
│   ├── Controls/                            # (empty — needs HintedEntry custom control)
│   ├── Platforms/                           # Android, iOS, MacCatalyst, Windows bootstraps
│   └── Resources/                           # Fonts, images, styles, colors
├── PokemonBattleJournal.Tests/              # Unit tests (xUnit + NSubstitute + Shouldly)
├── PokemonBattleJournal.Benchmarks/         # BenchmarkDotNet perf tests
└── PokemonBattleJournal.UITests/            # Appium UI tests
    ├── UITests.Shared/                      # Shared test code + base class
    ├── UITests.Windows/                     # Windows Appium runner
    └── UITests.Android/                     # Android Appium runner
```

---

## Domain Model

### Entities

| Entity | Key Fields | Relationships |
|---|---|---|
| `Trainer` | `Id` (uint PK), `Name` (string, Unique) | OneToMany → Archetypes, Tags, MatchEntries |
| `Archetype` | `Id` (uint PK), `Name` (string, Unique), `ImagePath` (string), `TrainerId` (uint FK) | ManyToOne → Trainer; OneToMany → MatchEntries (Playing/Against) |
| `Tags` | `Id` (uint PK), `Name` (string, Unique), `TrainerId` (uint FK) | ManyToOne → Trainer; ManyToMany → Game (via TagGame) |
| `Game` | `Id` (uint PK), `Result` (MatchResult?), `Turn` (uint, 1=Player/2=Opponent), `Notes` (string?) | ManyToMany → Tags (via TagGame) |
| `TagGame` | `GameId` (uint FK), `TagId` (uint FK) | Junction table for Game↔Tags many-to-many |
| `MatchEntry` | `Id` (uint PK), `TrainerId` (uint FK), `PlayingId`/`AgainstId` (uint FK), `Result` (MatchResult?), `Game1Id`/`Game2Id`/`Game3Id` (uint? FK), `StartTime`, `EndTime`, `DatePlayed` | ManyToOne → Trainer, Playing Archetype, Against Archetype; OneToOne → Game1/2/3 |

### Enums

```csharp
public enum MatchResult { Win, Loss, Tie }
```

### Data Models for Charts

```csharp
public class ChartDataPoint { string? Label; double Value; }
public class TimeDataPoint { DateTime Date; double Value; }
```

---

## Architecture

### MVVM Pattern

```
Views (XAML) --bind--> ViewModels (ObservableObject + RelayCommand)
    --call--> Services (business logic) --> Interfaces --> SqliteConnectionFactory --> SQLite DB
```

- **DI:** `MauiProgram.cs` registers all services, ViewModels, and Pages
- **Navigation:** MAUI Shell with Flyout
- **Lifecycle:** `EventToCommandBehavior` on `Appearing`/`Disappearing` events
- **Async:** `SemaphoreSlim` for thread safety on all DB operations
- **Error Handling:** All operations wrapped in try/catch with `ModalErrorHandler` display alerts

### DI Registration (MauiProgram.cs)

```
ISqliteConnectionFactory → SqliteConnectionFactory (singleton)
IMatchResultsCalculatorFactory → MatchResultCalculatorFactory (singleton)
IMatchAnalysisService → MatchAnalysisService (singleton)
FirstStartPage/VM → Transient
MainPage/VM → Singleton
ReadJournalPage/VM → Transient
TrainerPage/VM → Transient
OptionsPage/VM → Transient
AboutPage/VM → Transient
```

---

## Pages and ViewModels

### 1. FirstStartPage / FirstStartPageViewModel
- **Purpose:** Initial onboarding — enter trainer name
- **Controls:** `SfTextInputLayout` + `Entry`, `SfButton`
- **Key Behavior:** Sets `PreferencesHelper.SetSetting("FirstStart", "false")` and `PreferencesHelper.SetSetting("TrainerName", name)`, then navigates to `AppShell`

### 2. MainPage / MainPageViewModel (Singleton)
- **Purpose:** Create match entries with BO1/BO3 toggle, archetypes, time, tags
- **Controls:** 2× `SfTextInputLayout`+`SfComboBox` (archetype pickers), `Switch` (BO3), 2× `SfTimePicker`, `SfDatePicker`, 3× `SfComboBox` (results), `Editor` (notes), `CollectionView` (tags), 3× `SfButton`
- **Key Properties:** `PlayerSelected`, `RivalSelected`, `Result`/`Result2`/`Result3`, `StartTime`/`EndTime`/`DatePlayed`, `BO3Toggle`, `FirstCheck`/`FirstCheck2`/`FirstCheck3`, `TagsSelected`/`Match2TagsSelected`/`Match3TagsSelected`, `Archetypes`, `TagCollection`
- **Key Methods:** `AppearingAsync()`, `Disappearing()`, `SaveMatchAsync()`, `ValidateMatchData()`
- **Code-Behind:** `OpenTimePickers()` and `OpenDatePlayedPicker()` call `.IsOpen = true` on Syncfusion pickers

### 3. ReadJournalPage / ReadJournalPageViewModel
- **Purpose:** Browse past matches with full game details
- **Controls:** No Syncfusion (pure MAUI)
- **Key Properties:** `MatchHistory`, `SelectedMatch`, `TagsSelectedGame1/2/3`, `PlayingName`/`AgainstName`, `PlayingIconSource`/`AgainstIconSource`
- **Key Methods:** `AppearingAsync()`, `LoadMatch()`, `ResetDisplay()`

### 4. TrainerPage / TrainerPageViewModel
- **Purpose:** Stats dashboard with 7 Syncfusion charts
- **Controls:** 4× `SfCartesianChart`, 2× `SfCircularChart`, text labels, `CollectionView`
- **Key Properties:** `Wins`, `Losses`, `Ties`, `WinAverage`, `MostPlayedArchetypes`, `WinRateOverTime`, `ArchetypeWinRates`, `TagUsage`, `OpponentPerformance`, `WinRateByMatchLength`, `FirstTurnAdvantage`, `AverageMatchDuration`, `StreakInfo`
- **Key Methods:** `AppearingAsync()` — calls `MatchAnalysisService` for all stats

### 5. OptionsPage / OptionsPageViewModel
- **Purpose:** Manage trainer name, custom archetypes, tags
- **Controls:** 4× `SfTextInputLayout`, 1× `SfComboBox` (icon picker), 5× `SfButton`
- **Key Properties:** `NameInput`, `TagInput`, `NewDeckName`, `SelectedIcon`, `IconCollection`
- **Key Methods:** `SaveTrainerAsync()`, `SaveTagAsync()`, `SaveArchetypeAsync()`, `SaveAllAsync()`, `DeleteTrainerFileAsync()`

### 6. AboutPage / AboutPageViewModel
- **Purpose:** Credits page
- **Controls:** No Syncfusion
- **ViewModel:** Only has logger injection

---

## Services Layer

### SqliteConnectionFactory (Singleton)
- Creates `SQLiteAsyncConnection` with double-checked locking
- Exposes `Trainers`, `Matches`, `Archetypes`, `Tags` operation interfaces
- Creates tables in dependency order: Trainer, Archetype, Tags, Game, TagGame, MatchEntry

### MatchOperations
- `SaveAsync(MatchEntry, List<Game>)` — Transaction: insert/update match, save games with tags, verify integrity
- `GetAllAsync()`, `GetByIdAsync()`, `GetByTrainerIdAsync()` — Load with related data
- `DeleteAsync(MatchEntry)` — Cascade delete games and tag relationships
- **Internal methods:** `SaveGame()`, `DeleteGame()` for transaction helpers

### TrainerOperations
- `GetByNameAsync()`, `GetAllAsync()`, `SaveAsync()`, `DeleteAsync()` — Full CRUD with cascade
- `DeleteAsync()` loads related matches, archetypes, tags and deletes in transaction

### ArchetypeOperations
- `GetAllAsync()` — Seeds 8 default archetypes if table empty
- `SaveAsync()`, `DeleteAsync()` — Blocks deletion if archetype is used in matches

### TagOperations
- `GetAllAsync()` — Seeds 8 default tags if table empty
- `SaveAsync()`, `DeleteAsync()` — Cascades TagGame relationships

### MatchAnalysisService
- 11 methods for computing statistics:
  - `CalculateWinRate()`, `GetMostPlayedArchetypes()`, `CalculateWinRateOverTime()`
  - `CalculateArchetypeWinRate()`, `CalculateTagUsage()`, `CalculatePerformanceAgainstOpponents()`
  - `CalculateAverageMatchDuration()`, `CalculateWinRateByMatchLength()`, `CalculateFirstTurnAdvantage()`
  - `CalculateStreaks()`, `CalculateMatchFrequency()`

### MatchResultCalculatorFactory / BO1ResultCalculator / BO3ResultCalculator
- Factory pattern: `GetCalculator(isBestOf3)` returns appropriate calculator
- BO1: Returns the single game result
- BO3: Counts wins/losses across 2-3 games; ties are neither win nor loss

---

## Syncfusion Removal Plan

### Packages to Remove (9)

| Package | Version | Used Controls |
|---|---|---|
| `Syncfusion.Maui.AIAssistView` | 29.1.38 | **UNUSED** — no references in code |
| `Syncfusion.Maui.Buttons` | 29.1.38 | **UNUSED** — SfButton comes from Toolkit |
| `Syncfusion.Maui.Cards` | 29.1.38 | **UNUSED** — no references in code |
| `Syncfusion.Maui.Charts` | 29.1.38 | SfCartesianChart, SfCircularChart, LineSeries, ColumnSeries, PieSeries |
| `Syncfusion.Maui.Core` | 29.1.38 | SfTextInputLayout, LabelStyle, SyncfusionLicenseProvider |
| `Syncfusion.Maui.DataForm` | 29.1.38 | **UNUSED** — no references in code |
| `Syncfusion.Maui.Picker` | 29.1.38 | SfTimePicker, SfDatePicker, PickerHeaderView |
| `Syncfusion.Maui.Sliders` | 29.1.38 | **UNUSED** — no references in code |
| `Syncfusion.Maui.Toolkit` | 1.0.4 | SfButton, ConfigureSyncfusionToolkit() |

### Files to Modify

| File | Changes |
|---|---|
| `PokemonBattleJournal.csproj` | Remove 9 PackageReference lines |
| `MauiProgram.cs` | Remove 2 using directives + 2 `.ConfigureSyncfusion*()` calls |
| `App.xaml.cs` | Remove `SyncfusionLicenseProvider.RegisterLicense()` (4 lines) |
| `MainPage.xaml` | Replace SfButton→Button, SfTextInputLayout→HintedEntry, SfComboBox→Picker, SfTimePicker→TimePicker, SfDatePicker→DatePicker |
| `MainPage.xaml.cs` | Remove `OpenTimePickers()`/`OpenDatePlayedPicker()` and their `.IsOpen` calls |
| `OptionsPage.xaml` | Replace SfButton→Button, SfTextInputLayout→HintedEntry, SfComboBox→Picker |
| `FirstStartPage.xaml` | Replace SfButton→Button, SfTextInputLayout→HintedEntry |
| `TrainerPage.xaml` | Replace 7 Syncfusion charts with native MAUI controls |

### Replacement Components Needed

1. **HintedEntry** (custom control) — Floating hint Label + Entry + helper text Label. Supports `Hint`, `HelperText`, `ContainerType` (None/Outlined), `Stroke`, `HintLabelStyle`, `HelperLabelStyle`
2. **Picker with DataTemplate** — Replaces SfComboBox. Must support icon+name display in ItemTemplate
3. **Native MAUI TimePicker/DatePicker** — Inline, no dialog mode. `MinimumTime` constraint must move to ViewModel
4. **Heatmap Grid** — New component for Archetype Matchup Matrix (replaces charts)
5. **ProgressBar segments** — Replace LineSeries, ColumnSeries charts

### Syncfusion Control Replacement Map

| Syncfusion Control | Location | Replacement |
|---|---|---|
| `SfButton` | MainPage (2), OptionsPage (5), FirstStartPage (1) | MAUI `Button` |
| `SfTextInputLayout` | MainPage (2), OptionsPage (4), FirstStartPage (1) | Custom `HintedEntry` control |
| `SfComboBox` | MainPage (5), OptionsPage (1) | MAUI `Picker` + `DataTemplate` |
| `SfTimePicker` | MainPage (2) | MAUI `TimePicker` (inline) |
| `SfDatePicker` | MainPage (1) | MAUI `DatePicker` (inline) |
| `SfCartesianChart` | TrainerPage (4) | Native MAUI Grid + ProgressBar |
| `SfCircularChart` | TrainerPage (2) | Native MAUI ProgressBar + labels |

---

## Known Bugs

> All bugs listed below have been fixed in recent commits.

### 1. Win Rate Formula Inconsistency — RESOLVED
- ~~`Calculations.CalculateWinRate` uses `(wins + 0.5 * ties) / total * 100` (ties = half win)~~
- ~~`MatchAnalysisService.CalculateWinRate` uses `wins / total * 100` (ties = zero)~~
- **Resolution**: The standard statistical formula `(wins + 0.5 * ties) / total * 100` is the correct one for heatmaps and PTCG analysis. Both methods should eventually be consistent; `Calculations` version is unused in production.

### 2. TrainerOperations.DeleteAsync Orphaned Records — RESOLVED
- ~~`DeleteGameAndTags()` references `match.Game1/2/3` but matches are loaded via `db.Table<MatchEntry>()` **without children**~~
- ~~Game1/2/3 are always null — game/tag deletion branches never execute~~
- **Resolution**: Replaced `DeleteGameAndTags()` calls with direct SQL `DELETE` statements using `Game1Id`/`Game2Id`/`Game3Id` FKs from `MatchEntry`.

### 3. MatchOperations.DeleteAsync Potential Deadlock — RESOLVED
- ~~Lines 278-293 call `db.FindAsync` and `db.Table<TagGame>().ToListAsync()` inside `RunInTransactionAsync`~~
- **Resolution**: Replaced async calls inside transaction with synchronous `tran.ExecuteScalar<int>(...)` SQL queries.

### 4. OptionsPageViewModel.SaveAllAsync Deadlock — RESOLVED
- ~~Acquires `SemaphoreSlim`, then calls `SaveTrainerAsync()`, `SaveTagAsync()`, `SaveArchetypeAsync()`~~
- **Resolution**: Removed outer semaphore lock from `SaveAllAsync()` — each sub-method manages its own lock.

### 5. FirstStartPageViewModel Missing Logging — RESOLVED (accepted)
- ~~`SaveTrainerName()` creates `new Logger<FirstStartPageViewModel>(new LoggerFactory())` inline instead of using DI~~
- **Resolution**: Reverted to parameterless constructor; logging in first-start flow is non-critical.

### 6. Dead Code in Save Methods — RESOLVED
- ~~`TrainerOperations.SaveAsync()` and `ArchetypeOperations.SaveAsync()` always create new entities with `Id == 0`~~
- **Resolution**: Removed unreachable `if (entity.Id != 0)` update branches from `TrainerOperations`, `ArchetypeOperations`, and `TagOperations`.

### 7. Race Conditions in Save Methods — RESOLVED
- ~~`TrainerOperations.SaveAsync()` and `ArchetypeOperations.SaveAsync()` perform duplicate-name checks before acquiring the semaphore lock~~
- **Resolution**: Moved duplicate-name check inside the semaphore lock within `RunInTransactionAsync` in `TrainerOperations.SaveAsync`.

---

## Test Coverage Summary

### Current Tests (78 total)

| File | Tests | What's Covered |
|---|---|---|
| `BO1ResultCalculatorTests.cs` | 3 | Null throws, Win→Win, Loss→Loss. **Missing: Tie** |
| `BO3ResultCalculatorTests.cs` | 10 | Null combos, 2-wins, 2-losses, 1-1-tie, ties+win, ties+loss. **Missing: 3-of-a-kind** |
| `MatchResultCalculatorFactoryTests.cs` | 2 | Both branches |
| `MatchAnalysisServiceTests.cs` | 2 | `CalculateWinRate` and `GetMostPlayedArchetypes` only. **9 of 11 methods untested** |
| `MainPageViewModelTests.cs` | 2 | Constructor non-null, TrainerName property. **Mock wired but never called** |
| UI Tests (Shared) | 2 | Note input accepts text, ball icon displays |
| UI Tests (AppWindow) | 1 | App launches (no assertions) |
| UI Tests (Windows) | 1 | BO3 switch toggles |
| UI Tests (Android) | 1 | BO3 switch toggles |

### Untested Services (0 unit tests)

- `MatchOperations` — Save, GetAll, GetById, GetByTrainerId, Delete
- `TrainerOperations` — GetAll, GetByName, Save, Delete
- `ArchetypeOperations` — GetAll, GetById, Save, Delete
- `TagOperations` — GetAll, GetById, Save, Delete
- `SqliteConnectionFactory` — Init, GetDatabase, GetLock
- `ModalErrorHandler` — HandleError

### Untested ViewModels

- `MainPageViewModel` — AppearingAsync, Disappearing, SaveMatchAsync, ValidateMatchData
- `TrainerPageViewModel` — AppearingAsync (all stat calculations)
- `OptionsPageViewModel` — SaveTrainerAsync, SaveTagAsync, SaveArchetypeAsync, SaveAllAsync, DeleteTrainerFileAsync
- `ReadJournalPageViewModel` — AppearingAsync, LoadMatch, ResetDisplay
- `FirstStartPageViewModel` — SaveTrainerName

### Untested Utilities

- `Calculations` — CalculateWinRate (uses standard formula, consistent with MatchAnalysisService after bug #1 fix)
- `FileHelper` — All 6 methods (GetAppDataPath, Exists, CreateFile, DeleteFile, ReadFileAsync, WriteFileAsync)
- `MainThreadHelper` — BeginInvokeOnMainThread, IsMainThread
- `TaskUtilities` — FireAndForgetSafeAsync
- `PreferencesHelper` — GetSetting, SetSetting

### Tests Needed Before Syncfusion Replacement (Priority Order)

1. `MainPageViewModel.ValidateMatchData()` — all validation paths
2. `MainPageViewModel.SaveMatchAsync()` — end-to-end save with mocked DB
3. `TrainerPageViewModel.AppearingAsync()` — all stat calculations via mocked `IMatchAnalysisService`
4. `MatchAnalysisService` — all 11 methods with empty, single, mixed, and edge-case inputs
5. `OptionsPageViewModel` — all save/delete commands
6. `ReadJournalPageViewModel.LoadMatch()` — match loading with tags
7. `MatchOperations.SaveAsync` / `DeleteAsync` — validation and data integrity
8. `TrainerOperations.DeleteAsync` — verify the Game1/2/3 null bug
9. UI tests for archetype picker, result picker, time/date picker, save button flow

---

## Platform-Specific Notes

### Windows
- `CollectionViewHandler` mapping disables multi-select checkbox (`MauiProgram.cs` lines 97-101)
- `WindowsPackageType=None` (unpackaged app)

### Android
- `SupportedOSPlatformVersion`: 21.0
- `RunAOTCompilation=False`, `PublishTrimmed=False`

### iOS / MacCatalyst
- `SupportedOSPlatformVersion`: 15.0

---

## Code Conventions

- **ObservableProperty/RelayCommand:** Source-generated via CommunityToolkit.Mvvm (no manual INPC)
- **Async patterns:** `SemaphoreSlim` for all DB access, `await` everywhere
- **Error handling:** `try/catch` with `ModalErrorHandler.HandleError(ex)` — swallows exceptions, returns default values
- **Logging:** Heavy use of `_logger.LogInformation/LogDebug/LogWarning/LogError` throughout
- **Test helpers:** `DeviceInfo.Platform == DevicePlatform.Unknown` detects unit test environment (no MAUI runtime)
- **Naming:** `{ClassName}Tests` for test classes, `{MethodName}_{Scenario}_{Expected}` for test methods

---

## Branch Strategy

- `master` — production-ready code
- `refactor/syncfusion-exit-and-net10` — planned branch for Syncfusion removal + .NET 10 upgrade

## .NET 10 Upgrade (Post-Syncfusion)

1. Update all `TargetFramework` values to `net10.0-*` in all 6 csproj files
2. Update remaining NuGet packages to latest versions
3. Build and fix deprecated API calls
4. Run tests
5. Test on each platform
