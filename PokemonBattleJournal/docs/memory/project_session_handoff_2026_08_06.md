---
name: project_session_handoff_2026_08_06
description: "HANDOFF: feat/loading-indicator is 1 Android test from green. The remaining failure is layout shift from the INLINE indicator — switching to an overlay fixes it and is already the planned next change. Read this first."
metadata:
  type: project
---

**Written 2026-08-06 as context ran out. Start here.**

## Where things are right now

Branch **`feat/loading-indicator`**, pushed, not merged. Everything else this session is already
merged to master.

| Suite | Result |
|---|---|
| Unit | 478/478 |
| Integration | 180/180 |
| Windows UI | 77/77 |
| Android UI | **75/76** — one failure, see below |

**Both UI CI workflows run a five-job matrix, one fixture per job**
(`--filter "FullyQualifiedName~${{ matrix.fixture }}"`), so a job's pass count is one
fixture, not the suite. The failure below is 24/25 *within the OptionsPageTests job*.
Do not read a single job's numbers as a platform total.

The two platforms run nearly the same tests — 75 shared in `UITests.Shared/Views/`, plus
platform-only partials: Windows has `MainPage_BOSwitch_DisplayedAndToggled` and
`OptionsPage_TrainerNameInput_AcceptsText` (the latter reads the UIA-only
`Value.Value` attribute, which is why it has no Android twin), Android has its own
`MainPage_BOSwitch_DisplayedAndToggled`. That one test is the entire count difference.

## The one blocker, and the fix the user already identified

`OptionsPage_DeleteTag_RemovesFromList` fails on Android at
`OptionsPageTests.cs:308` — `FindUIElement($"DeleteTag_{tagName}")` cannot find the row it
just created. CI run 31065757204.

**Cause: layout shift from the INLINE loading indicator.** The indicator and its DEBUG toggle
were added near the top of OptionsPage, pushing ~150px of content down. Android's
`UiScrollable` **only scrolls down** ([[feedback_uiscrollable_direction]]), so a row that ends
up above the current scroll position is unreachable. The shift was enough to flip this test.

**The fix is the overlay change already on the roadmap**, and the user said so directly:
*"im thinking some of these problems would be fixed by a proper loading icon."* An overlay in
the same Grid cell takes **no layout space**, so nothing shifts, and this failure disappears
without touching the test. That is a better fix than adding a scroll-to-top workaround, which
would only paper over displacement the user already wants gone.

Note there is **no `AndroidScrollToTop` / `ScrollToTop` helper** in the codebase — I checked.
The memory that mentions calling one is describing a technique, not an existing method.

### Two options

1. **Do the overlay now** (recommended). It is required before release anyway, removes this
   failure structurally, and is the one thing the user has already reviewed and rejected the
   current version of.
2. Merge with the inline version and one known-failing Android test. Not recommended — it
   leaves Android red, which this project has worked hard to keep green
   ([[feedback_dont_churn_stable_ci]]).

## What the loading indicator is, as built

- `Controls/Loading/` — `LoadingIndicator` (GraphicsView), `PokeballSpinnerDrawable`,
  `SpinnerAnimation`, `MinimumVisibilityGate`. 21 unit tests.
- Wired on all four data pages. `MainPageViewModel` and `OptionsPageViewModel` gained
  `IsAnyBusy` (both have two gates); ReadJournal and TrainerPage bind their single gate.
- Tuned values and the reasoning behind each are in [[project_spinner_drawing_lessons]].
  Settled: 44 layers, 0.075 layer alpha, 310° sweep, tail at **0.52** of head width, head
  thickness = ball diameter, ball fixed 28px, 16ms frame interval.
- DEBUG-only toggle on OptionsPage (`SimulateLoadingButton`, bound to
  `OptionsPageViewModel.IsDebugBuild`) holds the gate open so the spinner can be seen and
  tested.

### Two Android traps already hit and fixed — do not undo these

1. **A stuck toggle poisons the whole fixture.** Android clicks silently miss MAUI gesture
   handlers. If the toggle-off click misses, the gate stays open, the spinner animates at
   60fps, the UI thread never idles, and every later UiAutomator call burns its full ~20s
   `waitForIdle` budget. One missed click caused eight unrelated failures. Fixed with
   click-verify-retry.
2. **The teardown must key on `Busy_Mutating`, never on `LoadingIndicatorHost`.** The indicator
   deliberately lingers `MinimumVisibleDuration` (500ms) after the gate clears, so straight
   after a mutating test it is legitimately visible. An earlier teardown read that as "stuck",
   clicked the toggle, turned the gate **on**, and broke nearly the whole fixture — causing the
   exact failure it existed to prevent.

## Next work, in the user's agreed order

1. **Overlay + region-scoped indicators.** Design is in [[project_roadmap]]. Not a blanket page
   scrim: page-level while the page loads, then scoped to whichever section is still working,
   sized to that section. `InputTransparent="True"` is non-negotiable — the 500ms linger means
   a test resumes while the overlay is still up, and clicks would land on it.
2. **#14 Backup restore.** Full design in [[project_roadmap]]. **First task is fixing the
   export**: `ExportEntry` carries a single `time` field and no `startTime`/`endTime`, so a
   restore loses `EndTime`, which silently corrupts `CalculateAverageMatchDuration` and
   `CalculateWinRateByMatchLength`. Fix before building the restore or every backup taken
   meanwhile is already lossy.
3. **#15 BO3 note picker.** `SelectedNote2`/`SelectedNote3` are computed and never bound in
   XAML, so games 2 and 3 notes have never been visible (B-08).

## PTCG Live parsing — reconnaissance done, do not redo it

Full findings in [[project_roadmap]]. Headlines:

- **Clipboard only. There is no battle log file.** Verified on this machine: the
  `Game*.log` files in `%LOCALAPPDATA%Low\pokemon\Pokemon TCG Live\` are Unity exception traces
  with zero battle content. I wrongly concluded otherwise from a third party's setup copy; the
  user was right from the start.
- **A real current log is saved at
  `PokemonBattleJournal/docs/samples/ptcgl-battle-log-2026-08-06.txt`.** The format changed
  since 2025 samples: turn headers have **no turn number**, cards are prefixed with IDs like
  `(me2-5_34_ph)`, and the end line varies by win condition.
- **The log identifies its own owner** — the owner's draws are named, the opponent's are not —
  so no Live-username setting is needed.
- No timestamps anywhere, so Live imports need their own duplicate story.

## Standing corrections from this session

- [[feedback_fact_check_the_user]] — verify confident claims, including your own inferences.
  A vendor's setup copy is not evidence about someone else's file formats.
- [[feedback_platform_specific_is_fine]] — "not cross-platform" is not an automatic no.
  Handlers and built-in native renderers are ordinary; only exotic interop like hosting DX is
  out.
- [[feedback_dont_poll_ci]] — sleep the run's expected duration, then check a few times.
