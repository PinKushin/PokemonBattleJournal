# Pokemon Battle Journal — Feature & Bug Roadmap

Tracked here so nothing gets lost between sessions. Bugs first (things broken right now), features second (things not built yet).

---

## Bugs

### MainPage

| # | Description | Location | Notes |
|---|---|---|---|
| ~~B-01~~ | ~~Archetype dropdowns are too close together~~ | — | **Fixed.** Added `Spacing="10"` to the StackLayout. |
| ~~B-02~~ | ~~Dropdown placeholder text is grammatically wrong~~ | — | **Fixed.** Changed to `"Player"` and `"Rival"`. |
| ~~B-03~~ | ~~Dropdown width is fixed at 210~~ | — | **Fixed.** `WidthRequest` reduced to 180; labels already had `TailTruncation`. |

### UI / Layout (general)

| # | Description | Location | Notes |
|---|---|---|---|
| B-04 | ~~TrainerPage hangs on navigation~~ | — | **Fixed.** Charts lazy-load correctly now. |

---

## Features

### MainPage

| # | Description | Notes |
|---|---|---|
| F-01 | BO3 result validation — require Game 3 only when ShowGame3 is true | Already partially implemented via `ShowGame3` property and `ValidateEntryAsync`. Needs end-to-end UI test coverage. |
| F-02 | Clear form after save | After `SaveFile` succeeds, reset all fields to defaults. Currently fields stay populated. |

### TrainerPage — Charts

All 8 chart data sets are wired in `TrainerPageViewModel` and ready. The XAML uses placeholder `Label`s pending safe lazy init.

| # | Chart | Notes |
|---|---|---|
| F-03 | Win rate over time | Line chart — `WinRateOverTimeSeries` / `WinRateOverTimeXAxes` / `WinRateOverTimeYAxes` |
| F-04 | Win/Loss/Tie distribution | Pie or donut chart — `WinLossTieSeries` |
| F-05 | Matchup win rates by archetype | Bar chart — `MatchupWinRateSeries` / `MatchupXAxes` / `MatchupYAxes` |
| F-06 | Games played per archetype | Bar chart — `GamesPlayedSeries` / `GamesPlayedXAxes` / `GamesPlayedYAxes` |
| F-07 | Tag frequency | Bar or column chart — `TagFrequencySeries` / `TagFrequencyXAxes` / `TagFrequencyYAxes` |
| F-08 | First player win rate | Stat card or single-value chart |
| F-09 | BO3 vs BO1 performance split | Grouped bar — `BO3WinRateSeries` / `BO3WinRateXAxes` / `BO3WinRateYAxes` |
| F-10 | Session win rate (today) | Stat card or mini line |

Safe init strategy to investigate: `CollectionChanged`-deferred load, `Loaded` event per chart, or a single `ScrollView` virtualization approach.

### ReadJournalPage — Styling

| # | Description | Notes |
|---|---|---|
| ~~F-11~~ | ~~Apply consistent page styling~~ | **Done.** PokeYellow title, PokeBlue match card borders, result badge chips, PokeYellow date. |
| ~~F-12~~ | ~~Match history card design~~ | **Done.** Cards show playing archetype icon + name, rival name, date (PokeYellow), result badge (PokeBlue chip). |
| F-13 | Filter/search by archetype or date | UI for narrowing the journal list. |

### OptionsPage — Styling

| # | Description | Notes |
|---|---|---|
| ~~F-14~~ | ~~Apply consistent page styling~~ | **Done.** Section headings (TRAINER / CUSTOM ARCHETYPE / CUSTOM TAG) in PokeBlue, PokeYellow-bordered inputs, BostonRed delete button. Icon picker replaced with `ComboBoxControl` (searchable, shows image + name). Fixed pre-existing bug: `NewDeckIcon` was never wired from old Picker. |
| F-15 | Archetype management | Add/rename/delete custom archetypes (currently only pre-seeded ones exist). `SaveAsync` in `ArchetypeOperations` exists but no UI for it on OptionsPage. |
| F-16 | Trainer name editing | Edit the trainer name in place without re-triggering first-start flow. |

### AboutPage — Styling

| # | Description | Notes |
|---|---|---|
| ~~F-17~~ | ~~Apply consistent page styling~~ | **Done.** Pokeball hero, Pokemon font title in PokeYellow, PokeBlue divider, Saira credit + tagline. `AutomationId="AboutPageTitle"` added for UI tests. |

### FirstStartPage — Styling

| # | Description | Notes |
|---|---|---|
| ~~F-18~~ | ~~Polish first-start flow~~ | **Done.** Pokeball hero, Pokemon font title, centered PokeYellow-bordered name input card. |

### Infrastructure

| # | Description | Notes |
|---|---|---|
| F-19 | Replace Windows Appium driver | Custom driver in progress to replace WinAppDriver. Better Win32 compatibility and fewer bugs. The exe path in `AppiumSetup.cs` is hardcoded — this is required by WinAppDriver (only way to target an unpackaged Windows app). The custom driver may change this. Update `AppiumSetup.cs` when the new driver is ready. |
| F-20 | Configurable Android emulator AVD | `pixel_7_-_api_35` is hardcoded in `UITests.Android/AppiumSetup.cs`. Make it configurable via env var or test config file. |
| F-21 | Multi-trainer switcher | Options page can create trainers but there's no switcher UI. Planned. |
| F-22 | Archetype periodic refresh | Currently upserts on every `GetAllAsync` call (first call per launch). Consider background refresh or a manual "Refresh Meta" button on OptionsPage so existing DB stays current without requiring a restart. |

---

## Priority order (suggested)

1. ~~**B-01, B-02, B-03**~~ — done
2. **F-03 → F-10** — TrainerPage charts (data is ready, just need safe lazy XAML init)
3. ~~**F-11 → F-18**~~ — done (styling pass complete)
4. **F-19** — Windows Appium driver replacement (in progress externally)
5. **F-13, F-15, F-16** — ReadJournalPage filter/search, archetype management UI, trainer name edit
6. **F-20, F-21, F-22** — infrastructure and multi-trainer
