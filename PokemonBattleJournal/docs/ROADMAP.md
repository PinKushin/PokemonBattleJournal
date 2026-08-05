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

### ReadJournalPage / stats

| # | Description | Location | Notes |
|---|---|---|---|
| ~~B-05~~ | ~~Every match showed Game 2 and Game 3 tag sections, including best-of-one~~ | `MatchOperations.LoadRelatedDataAsync` | **Fixed 2026-08-05.** `MatchEntry` declares three `[OneToOne]` `Game` properties over three separate `[ForeignKey(typeof(Game))]` columns; SQLite-Net-Extensions cannot tell which key feeds which property and filled all three from one row. The loader only assigned slots that had an id, so the phantoms survived. Regression test in `MatchOperationsIntegrationTests`. |
| ~~B-06~~ | ~~Journal listed matches oldest-first~~ | `ReadJournalPageViewModel` | **Fixed 2026-08-05.** `GetByTrainerIdAsync` issues no `ORDER BY` and the result was passed straight through. Hidden because the seeder inserts in date order on a fresh database, so insertion order and date order agreed until a match was logged out of sequence. |
| ~~B-07~~ | ~~Win-rate line chart drew segments jumping backwards in time~~ | `MatchAnalysisService.CalculateWinRateOverTime` | **Fixed 2026-08-05.** Grouped by date but never ordered by it, and `GroupBy` preserves source order. `CalculateStreaks` and `CalculateMatchFrequency` were already correct. |
| B-08 | Game 2 and Game 3 notes are never displayed | `ReadJournalPage.xaml` | The view model computes `SelectedNote2`/`SelectedNote3` and the XAML binds only `SelectedNote`, so they are calculated and discarded. Addressed by priority item 9 (BO3 note picker). |

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
5. ~~**Game3Tab stall / Windows UI test latency**~~ — **Done 2026-08-05**: absent-element lookups inherited the 5s ambient ImplicitWait (~6.8s each vs 215ms when present). Windows suite ~5min → 1m28s. Also fixed CI cache contention across the matrix and WinAppDriver session backoff.

### Confirmed order from here (user, 2026-08-05)

6. ~~**TrainerHill export + full backup export**~~ — **Done 2026-08-05.** Two formats: TrainerHill's (archetype slugs, for interop, lossy without the Limitless meta list) and a backup envelope (names verbatim, lossless). Import hardened at the same time — size, depth, entry-count and name-length caps enforced before any DB write. Uncovered and fixed three unrelated bugs: phantom Game2/Game3 on every match, the journal listing oldest-first, and the win-rate line chart drawing backwards in time.
7. **Loading indicator** — design locked (partial arc + Pokéball on the leading edge); gates and sentinels already shipped. **Next.**
8. **Backup restore** — read the export envelope back in. Needs trainer creation from file contents and a duplicate policy, which is why it was not bundled with export. Scheduled after the loading indicator deliberately: a restore is the longest-running operation in the app and is the natural first consumer of the new spinner.
9. **BO3 note picker (ReadJournal)** — a way to choose which game's note to read. Closes a real gap: `SelectedNote2`/`SelectedNote3` are computed by the view model and never bound in XAML, so game 2 and 3 notes have never been visible.
10. **F-15 — Archetype management UI** — add/rename/delete custom archetypes on OptionsPage. `ArchetypeOperations.SaveAsync` already exists with no UI reaching it, so this is the smallest gap between existing capability and what a user can actually do.
11. **F-16 — Trainer name editing** — edit in place without re-triggering the first-start flow.
12. **F-13 — ReadJournalPage filter/search** — narrow the journal list by archetype or date. Grows in value as a user accumulates matches, so it pairs naturally with export.
13. **Theming** — in-app light/dark theme switcher. See `docs/memory/project_theme_switcher.md`. Deliberately after the feature work above (user, 2026-08-05): theming a UI that is still gaining controls means doing it twice.
14. **Site refresh** — finishes the theming work with a shared visual identity; self-hosted fonts, reconsider type choices.

Unscheduled / no strong ordering yet:

- **F-19** — Windows Appium driver replacement (in progress externally)
- **F-20, F-22** — configurable AVD, archetype periodic refresh
- **AOT compatibility + real installer** — see `docs/memory/project_roadmap.md`; no longer blocked on budget (Android signing is free, SignPath covers Windows)

Feature details for items 7-12 live in `PokemonBattleJournal/docs/memory/project_roadmap.md`.
