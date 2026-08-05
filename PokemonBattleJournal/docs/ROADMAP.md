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
| ~~F-02~~ | ~~Clear form after save~~ | **Done.** `SaveMatchAsync` clears the form on success. |

### TrainerPage — Charts

| # | Chart | Notes |
|---|---|---|
| ~~F-03 → F-10~~ | ~~All 8 charts~~ | **Done.** Implemented with LiveCharts2: matchup matrix, win rate over time, most played, archetype win rates, opponent performance, tag usage, match length, first-turn split. Covered by `TrainerPage_AllEightCharts_Rendered` UI test on both platforms. |

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
| ~~F-21~~ | ~~Multi-trainer switcher~~ | **Done.** `TrainerSwitchPicker` on OptionsPage; `ITrainerSwitchService` broadcasts `TrainerChanged` to all VMs. |
| F-22 | Archetype periodic refresh | Currently upserts on every `GetAllAsync` call (first call per launch). Consider background refresh or a manual "Refresh Meta" button on OptionsPage so existing DB stays current without requiring a restart. |

---

## Priority order (suggested)

1. ~~**B-01, B-02, B-03**~~ — done
2. ~~**F-03 → F-10**~~ — done (LiveCharts2 charts live)
3. ~~**F-11 → F-18**~~ — done (styling pass complete)
4. ~~**Loading gates + ReadJournal Android test slowdown**~~ — **Done 2026-08-04** (feat/loading-gates): IsBusy* gates on all 4 data pages + Busy_* sentinels + WaitUntilBusyGone; ReadJournal tag CollectionViews → FlexLayout. SelectMatch tests 50-111s → sub-second; Android suite 18m → 8m44s, 72/72.
5. **TrainerHill export + full backup export** — see `docs/memory/project_roadmap.md`
6. **F-19** — Windows Appium driver replacement (in progress externally)
7. **F-13, F-15, F-16** — ReadJournalPage filter/search, archetype management UI, trainer name edit
8. **F-20, F-22** — configurable AVD, archetype periodic refresh
9. **AOT compatibility + real installer** — long-term, see `docs/memory/project_roadmap.md`

Feature details for items 4, 5, and 9 live in `PokemonBattleJournal/docs/memory/project_roadmap.md`.
