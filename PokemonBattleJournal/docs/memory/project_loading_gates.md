---
name: project_loading_gates
description: Named IsBusy* loading gates implemented on all 4 data pages — VM props, XAML sentinels, WaitUntilBusyGone test sync; branch feat/loading-gates
metadata:
  type: project
---

**Status (2026-08-04): VERIFIED and merged** (branch `feat/loading-gates`). Windows UI 73/73, Android **72/72 in 8m44s** (down from 18m19s), unit 478/478 (8 new gate tests, TDD red→green). The FlexLayout swap fixed the ReadJournal stall outright: SelectMatch tests 50-111 s → 92-368 ms. Root cause was the three tag CollectionViews; MatchHistoryList did not need replacing.

## Phase 2 (2026-08-05): mutation gates — branch `feat/mutation-busy-gates`, not yet merged

Extended the pattern from page-load reads to mutating commands (Save/Delete), which have
their own race: the CollectionView rebind after a delete can return a WinAppDriver phantom
element (null/empty ID → InvalidOperationException) if a test checks presence before the
rebind settles.

- **MainPageViewModel.IsBusyMutating** — wraps `SaveMatchAsync`. Set AFTER validation +
  trainer lookup succeed (never flips for a save that fails validation and never reaches
  the DB) — see `Main_SaveMatchAsync_ValidationFails_NeverSetsBusyMutating` test.
- **OptionsPageViewModel.IsBusyMutating** — expanded from Save/Delete Archetype+Tag (Phase 1
  scope) to ALL 9 mutating commands: `SaveTrainerAsync`, `SwitchTrainerAsync`,
  `DeleteTrainerFromListAsync`, `DeleteTrainerFileAsync`, `ImportFromTrainerHillAsync`,
  `SaveTagAsync`, `DeleteTagAsync`, `SaveArchetypeAsync`, `DeleteArchetypeAsync`.
  `ImportFromTrainerHillAsync` is the one exception in placement: the gate starts AFTER
  `FilePicker.Default.PickAsync` returns, not before — gating through the picker would leave
  `Busy_Mutating` visible for however long the user spends browsing files, which is user
  interaction time, not app processing time.
- New `Busy_Mutating` sentinel on both MainPage.xaml and OptionsPage.xaml (OptionsPage now
  has two sentinels: `Busy_ArchetypeList` for the page-load gate, `Busy_Mutating` for command
  gates — they're independent and can both be true simultaneously in theory, though not in
  practice since AppearingAsync and mutating commands don't usually overlap).
- `TestBase.WaitUntilRemoved` (added same session, superseded by the gate for the two cases
  it targeted) is KEPT as a second line of defense in the two Delete tests — WinAppDriver can
  still return a stale node for one frame after the gate clears, so belt-and-suspenders.
- MainPage SaveMatch UI tests: `WaitUntilBusyGone("Busy_Mutating")` added right after the
  Save click, ahead of the pre-existing `WebDriverWait` poll for the "Saved" text — targets
  save-then-assert races seen on slower local Windows runs (user's own observation, not just
  CI).
- 10 new unit tests (TDD, written failing first against not-yet-existing properties/behavior).

**Status:** local unit 488/488, integration 115/115, Windows UI 73/73 (1m49s). Android CI
blocked by an unrelated infra flake — see [[project_android_ci_gpu_flake]]. NOT merged to
master as of this writing; holding for explicit user go-ahead per their "tiny tiny bit less
[proactive about merging]" instruction this session.

## What was built

Every page VM with an async data load exposes a named loading gate:

| VM | Property | Covers | Sentinel AutomationId |
|----|----------|--------|----------------------|
| ReadJournalPageViewModel | `IsBusyMatchHistory` | match history load | `Busy_MatchHistory` |
| TrainerPageViewModel | `IsBusyChartData` | match load + 8 chart builds (AppearingAsync wraps `LoadTrainerDataAsync`) | `Busy_ChartData` |
| MainPageViewModel | `IsBusyArchetypeList` | archetypes + tags (+ Limitless) | `Busy_ArchetypeList` |
| OptionsPageViewModel | `IsBusyArchetypeList` | trainers + archetypes + tags | `Busy_ArchetypeList` |

AboutPage: no gate — zero async, static content. Quick DB writes (save tag, delete row) deliberately not gated — sub-100 ms, gate adds churn for nothing. TrainerHill import worth a gate when built.

Pattern per VM: set true first line of AppearingAsync, clear in `finally` — every early return and exception path clears the flag. TrainerPage needed its body extracted to `LoadTrainerDataAsync()` because the original had returns outside the try.

XAML sentinel per page: hidden 1×1 Label, `IsVisible` bound to the gate, stable AutomationId. Placed as direct child of the page's root layout so it's always in the UIA tree region tests can reach.

## Test-side sync

`TestBase.WaitUntilBusyGone(sentinelId, timeoutMs=10000)` — polls `IsElementPresent`
(platform-correct: resource-id Android, AccessibilityId Windows; the pre-existing
`WaitUntilGone` uses AccessibilityId and can NOT see the sentinels on Android).
Returns bool; a gate that never appears returns true immediately (load finished
before we looked — fine). Wired into all four page-test `[OneTimeSetUp]`s right
after `NavigateTo`.

## Unit-test technique (LoadingGateTests.cs)

TaskCompletionSource-gated mock: mock the DB call to return `tcs.Task`, start
`vm.AppearingAsync()` without awaiting, assert gate is TRUE mid-flight, complete
the TCS, await, assert FALSE. Plus an error-path test per VM (mock throws → gate
still clears). 8 tests total.

## Also on this branch

- ReadJournal 3 tag CollectionViews → FlexLayout + BindableLayout (badge moved to a Grid `Auto` column since BindableLayout has no Header; `BindableLayout.EmptyView` preserved the "No Tags for Game N" labels). Targets the ~20 s-per-UIA-call Android stall — see [[project_readjournal_android_slow]]. MatchHistoryList CollectionView left in place pending measurement; swap it too if still slow.
- `MainPage_UserNoteInput_ShowTextEntry` → type-verify-retry (SendKeys dropped chars on Windows CI, StaleElementReferenceException on Android CI — same day, both platforms).
- ComboBoxControl `LogPopupEvent` `catch {}` → typed catches (IOException, UnauthorizedAccessException) with reasons.

## CI learnings (2026-08-04 evening)

- Workflows only fired on master/main/`feature/**` — `fix/*` branches got ZERO CI. What looked like branch CI was merge commits on master. Now `branches: ['**']` on push in all three workflows.
- Android CI (ubuntu, software GPU emulator) is far flakier than local: StaleElementReference on SendKeys, then a nav cascade took out all OptionsPage tests at uniform 10 s. Local suite was 72/72 the same evening. The gates + FlexLayout target exactly this busy-thread flakiness; re-evaluate CI after this branch merges.
- Windows CI flake: SendKeys delivered "Hello " (dropped "World") on the slow runner.

## Related

- [[feedback_android_flaky_tap_retry]] — click-verify-retry pattern (MainPage fix)
- [[project_readjournal_android_slow]] — the 20 s idle-wait analysis this branch attacks
- [[project_android_mainpage_failures]] — prior session's failure catalog
