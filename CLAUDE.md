# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Living context doc

**Read `docs/AI-CONTEXT.md` at the start of every session.** It is the canonical context file — architecture, domain model, session log, known bugs, and user decisions. Update its Session log before starting any long multi-file task, and again when finishing significant work.

## Long-term AI memory

**Read all files in `docs/memory/` at the start of every session.** These are the persistent memory files for Claude Code — user preferences, feedback on past approaches, and project decisions that must carry across conversations. Apply them throughout the session exactly as you would local memory.

- `docs/memory/MEMORY.md` — index of all memory entries
- `docs/memory/feedback_*.md` — how the user wants work approached
- `docs/memory/project_*.md` — project decisions and context
- `docs/memory/user_*.md` — user background and preferences

**After making a memory-worthy observation** (the user corrects an approach, confirms a decision, states a preference), write both to the local memory system AND copy the updated files to `docs/memory/` and commit them so the repo stays in sync.

## Commands

```powershell
# Build (Windows)
dotnet build PokemonBattleJournal.slnx -f net10.0-windows10.0.19041.0

# Run (Windows)
dotnet run --project PokemonBattleJournal/PokemonBattleJournal.csproj -f net10.0-windows10.0.19041.0

# Unit tests only
dotnet test PokemonBattleJournal.Tests/PokemonBattleJournal.Tests.csproj

# Single test
dotnet test PokemonBattleJournal.Tests/PokemonBattleJournal.Tests.csproj --filter "FullyQualifiedName~MethodName"

# Benchmarks (Release only)
.\PokemonBattleJournal.Benchmarks\Run.ps1

# Kill orphaned app after failed Appium run
Stop-Process -Name PokemonBattleJournal -Force -ErrorAction SilentlyContinue
```

**Solution file:** always use `PokemonBattleJournal.slnx`. Do not recreate `PokemonBattleJournal.sln`.

## Architecture

MVVM app: `Views (XAML) → ViewModels → Services → ISqliteConnectionFactory → SQLite`

- **MVVM:** CommunityToolkit.Mvvm source generators (`[ObservableProperty]`, `[RelayCommand]`)
- **DI:** `MauiProgram.cs` — `MainPage`/`MainPageViewModel` are singletons; all other pages/VMs are transient
- **DB concurrency:** static `SemaphoreSlim` in `SqliteConnectionFactory`; every DB operation must acquire it
- **Error handling:** `try/catch` + `ModalErrorHandler.HandleError` in services and VMs
- **Test detection:** `DeviceInfo.Platform == DevicePlatform.Unknown` signals unit test environment (no MAUI runtime)

### Services

`SqliteConnectionFactory` owns table init and exposes typed operation services (`MatchOperations`, `TrainerOperations`, `ArchetypeOperations`, `TagOperations`). `MatchAnalysisService` computes all stats for `TrainerPageViewModel`. `MatchResultCalculatorFactory` selects `BO1ResultCalculator` or `BO3ResultCalculator` based on match format.

**Win rate formula (canonical):** `(wins + 0.5 * ties) / total * 100` — defined in `Utilities/Calculations.cs`. All stats code must align with this.

### Custom controls

`Controls/ComboBoxControl/` — archetype picker used on MainPage (icon + name popup). `Controls/ImagePicker.cs` — icon selector on OptionsPage. Both use a popup with a `CollectionView`; the `ItemsSource` must be passed via constructor (not set after init), and item templates must return `Grid` directly (not `ViewCell`).

## Test conventions

- Naming: `{Class}Tests`, methods `{Method}_{Scenario}_{Expected}`
- Mocks via NSubstitute, assertions via Shouldly
- 221 unit tests currently passing; UI tests (Appium) require a running emulator/device

## Platform notes

- Windows: unpackaged (`WindowsPackageType=None`); debug exe at `bin\Debug\net10.0-windows10.0.19041.0\win10-x64\PokemonBattleJournal.exe`
- Android UI tests: hardcoded to AVD `pixel_7_-_api_35`
- Android Release: `RunAOTCompilation=False`, `PublishTrimmed=False`
- Benchmarks fail under Debug; always use Release + `Run.ps1`
