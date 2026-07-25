# Pokemon Battle Journal

A .NET MAUI app for logging and analyzing **Pokemon TCG** battle records across Windows and Android.

Record matches, track win rates, and review your performance against specific archetypes — all stored locally with no account required.

---

## Features

- **Match logging** — BO1 and BO3 formats, with per-game results, tags, notes, start/end times, and coin flip
- **Archetype picker** — live meta deck list fetched from [limitlesstcg.com](https://limitlesstcg.com/decks) on launch, with searchable dropdown and deck images; falls back to local defaults when offline
- **Trainer stats** — win rate, matchup matrix, and performance breakdowns per archetype
- **BO3 tab switcher** — progressive Game 1 / 2 / 3 tabs; Game 3 shown only when the match result is split or both games tied (official tournament rules)
- **Journal view** — filterable history of all recorded matches
- **Tags** — Early Start, Donked Rival, Lucky, Behind Early, Got Donked, Unlucky, Punished, Never Punished

---

## Prerequisites

| Tool | Minimum version | Notes |
|---|---|---|
| .NET SDK | 10.0 | [Download](https://dotnet.microsoft.com/download) |
| .NET MAUI workload | 10.0 | `dotnet workload install maui` |
| Windows App SDK | bundled with MAUI workload | Required for Windows target |
| Node.js | 18+ | Required only for Appium UI tests |
| Appium | 2.x | `npm install -g appium` |
| Appium Windows driver | latest | `appium driver install windows` |
| Appium UIAutomator2 driver | latest | `appium driver install uiautomator2` (Android UI tests only) |
| Android SDK + emulator | API 35 | Android Studio or `sdkmanager`; AVD must be named `pixel_7_-_api_35` |

---

## Fresh install setup

### 1 — Clone and restore

```powershell
git clone https://github.com/PinKushin/PokemonBattleJournal.git
cd PokemonBattleJournal
dotnet restore PokemonBattleJournal.slnx
```

If restore fails with `NETSDK1045` (SDK version not found), verify your SDK:

```powershell
dotnet --version   # must be 10.x
dotnet workload list
```

If the MAUI workload is missing:

```powershell
dotnet workload install maui
```

### 2 — Build the app (Windows)

```powershell
dotnet build PokemonBattleJournal/PokemonBattleJournal.csproj -f net10.0-windows10.0.19041.0
```

**Common build errors:**

| Error | Fix |
|---|---|
| `MSB3027` — file locked by another process | Kill any running instance: `Stop-Process -Name PokemonBattleJournal -Force -ErrorAction SilentlyContinue` |
| `MSB3492` — cannot read `.cache` file | Delete the stale cache: `Remove-Item PokemonBattleJournal.Scraper\obj -Recurse -Force`, then rebuild |
| `XamlPreCompile` fails on first run after clean | Run the build command a second time — a transient XAML cache issue that resolves itself |
| NuGet restore error on specific package | Clear the cache: `dotnet nuget locals all --clear`, then `dotnet restore` |

### 3 — Run the app

```powershell
dotnet run --project PokemonBattleJournal/PokemonBattleJournal.csproj -f net10.0-windows10.0.19041.0
```

Or launch the built exe directly:

```powershell
.\PokemonBattleJournal\bin\Debug\net10.0-windows10.0.19041.0\win-x64\PokemonBattleJournal.exe
```

---

## Running tests

### Unit tests (no device required)

```powershell
dotnet test PokemonBattleJournal.Tests/PokemonBattleJournal.Tests.csproj
```

Run a single test by name:

```powershell
dotnet test PokemonBattleJournal.Tests/PokemonBattleJournal.Tests.csproj --filter "FullyQualifiedName~MethodName"
```

Expected: **221 tests passing**.

If tests fail to build, restore the scraper project first — the test project references it:

```powershell
dotnet restore PokemonBattleJournal.Scraper/PokemonBattleJournal.Scraper.csproj
dotnet test PokemonBattleJournal.Tests/PokemonBattleJournal.Tests.csproj
```

### Windows UI tests (Appium)

**Before running:**

1. Build the app in Debug for Windows (step 2 above).
2. Update the exe path in `PokemonBattleJournal.UITests/UITests.Windows/AppiumSetup.cs` if your clone is not at `C:\Users\pinku\source\repos\...`:
   ```csharp
   App = @"C:\your\path\PokemonBattleJournal\bin\Debug\net10.0-windows10.0.19041.0\win-x64\PokemonBattleJournal.exe"
   ```
3. Enable Developer Mode on Windows: **Settings → System → For developers → Developer Mode → On**.
4. Verify Appium and the Windows driver are installed:
   ```powershell
   appium driver list --installed
   # should show: windows
   ```

**Run:**

```powershell
dotnet test PokemonBattleJournal.UITests/UITests.Windows/UITests.Windows.csproj
```

The test runner starts and stops the Appium server automatically. If a previous run crashed and left the app open:

```powershell
Stop-Process -Name PokemonBattleJournal -Force -ErrorAction SilentlyContinue
```

### Android UI tests (Appium)

**Before running:**

1. Create an AVD named exactly `pixel_7_-_api_35` (API 35, Pixel 7 profile) in Android Studio or via:
   ```powershell
   avdmanager create avd -n "pixel_7_-_api_35" -k "system-images;android-35;google_apis;x86_64" -d "pixel_7"
   ```
2. Deploy a Debug build to the emulator. Start the emulator first, then:
   ```powershell
   dotnet build PokemonBattleJournal/PokemonBattleJournal.csproj -f net10.0-android
   # Deploy via Android Studio or adb
   ```
3. Verify the app is installed on the emulator:
   ```powershell
   adb shell pm list packages | Select-String "PinKushin"
   # should show: package:com.PinKushin.PokemonBattleJournal
   ```
4. Verify Appium and the UIAutomator2 driver are installed:
   ```powershell
   appium driver list --installed
   # should show: uiautomator2
   ```

**Run (emulator must be booted and app deployed before running):**

```powershell
dotnet test PokemonBattleJournal.UITests/UITests.Android/UITests.Android.csproj
```

The `avd` option in `AppiumSetup.cs` will boot the emulator automatically if it is not already running.

---

## Benchmarks

Benchmarks only work in Release configuration:

```powershell
.\PokemonBattleJournal.Benchmarks\Run.ps1
```

Do not run with `dotnet run` or in Debug — results will be invalid and the run may fail.

---

## Project structure

```
PokemonBattleJournal/          # MAUI app
PokemonBattleJournal.Scraper/  # Limitless TCG meta deck fetcher
PokemonBattleJournal.Tests/    # 221 unit tests (xUnit, NSubstitute, Shouldly)
PokemonBattleJournal.UITests/
  UITests.Windows/             # Appium Windows UI tests
  UITests.Android/             # Appium Android UI tests
  UITests.Shared/              # Shared test logic
PokemonBattleJournal.Benchmarks/
docs/                          # AI-CONTEXT.md, memory/
```

---

## Architecture

```
Views (XAML) → ViewModels → Services → ISqliteConnectionFactory → SQLite
                                  ↘ PokemonBattleJournal.Scraper → limitlesstcg.com
```

- **MVVM** via CommunityToolkit.Mvvm (`[ObservableProperty]`, `[RelayCommand]`)
- **DI** in `MauiProgram.cs` — MainPage/VM are singletons; all other pages/VMs are transient
- **Scraper** — separate `PokemonBattleJournal.Scraper` class library; SOLID factory pattern; no MAUI dependency so it runs in unit tests
- **Win rate formula:** `(wins + 0.5 × ties) / total × 100`

---

## Documentation

| File | Purpose |
|---|---|
| [`AI-CONTEXT.md`](AI-CONTEXT.md) | Architecture, domain model, session log, known issues — read this first |
| [`ROADMAP.md`](ROADMAP.md) | Bug and feature backlog with priority order |
| [`../CLAUDE.md`](../CLAUDE.md) | Claude Code guidance — commands, conventions, architecture summary |
| [`../LICENSE.txt`](../LICENSE.txt) | The Unlicense (public domain) |

---

## License

This is free and unencumbered software released into the public domain under [The Unlicense](../LICENSE.txt).
