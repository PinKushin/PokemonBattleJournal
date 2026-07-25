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

## Quick start

```powershell
# Build
dotnet build PokemonBattleJournal.slnx -f net10.0-windows10.0.19041.0

# Run (Windows)
dotnet run --project PokemonBattleJournal/PokemonBattleJournal.csproj -f net10.0-windows10.0.19041.0

# Unit tests
dotnet test PokemonBattleJournal.Tests/PokemonBattleJournal.Tests.csproj
```

> **Solution file:** always use `PokemonBattleJournal.slnx` — do not recreate `.sln`.

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

## Project structure

```
PokemonBattleJournal/          # MAUI app
PokemonBattleJournal.Scraper/  # Limitless TCG meta deck fetcher
PokemonBattleJournal.Tests/    # 221 unit tests (xUnit, NSubstitute, Shouldly)
PokemonBattleJournal.UITests/  # Appium UI tests (Windows + Android)
PokemonBattleJournal.Benchmarks/
docs/                          # AI-CONTEXT.md, memory/, archived README
```

---

## Documentation

| File | Purpose |
|---|---|
| [`docs/AI-CONTEXT.md`](docs/AI-CONTEXT.md) | Architecture, domain model, session log, known issues — read this first |
| [`CLAUDE.md`](CLAUDE.md) | Claude Code guidance — commands, conventions, architecture summary |
| [`LICENSE.txt`](LICENSE.txt) | The Unlicense (public domain) |

---

## License

This is free and unencumbered software released into the public domain under [The Unlicense](LICENSE.txt).
