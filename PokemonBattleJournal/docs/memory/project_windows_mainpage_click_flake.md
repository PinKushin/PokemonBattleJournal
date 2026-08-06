---
name: project_windows_mainpage_click_flake
description: "OPEN BUG: on Windows CI, MainPageTests reaches a point where every click-driven test fails while every find-only test still passes. Same six tests each time. Predates the loading indicator — do not blame the overlay."
metadata:
  type: project
---

**Open, not diagnosed.** Recurring Windows CI failure in `MainPageTests`. Recorded 2026-08-06
so the next occurrence is recognised instead of re-investigated from scratch.

## Fingerprint

Partway through the fixture, **clicks stop reaching handlers permanently**. Everything that
only *finds* an element keeps passing, so the app is alive and the automation tree is intact —
it is input that dies, not the app.

Always the same six tests:

```
MainPage_Game3Tab_ShowsGamePanel
MainPage_Game3Tab_ShowsWhenGame1IsTie
MainPage_PlayerArchetype_Cancel_DismissesPopup
MainPage_PlayerArchetype_DualIconDeck_ShowsBothIcons
MainPage_RivalArchetype_Cancel_DismissesPopup
MainPage_SaveMatch_WithArchetypes_ShowsSavedText
```

Errors are `ClickTab: 'UserNoteInput2' never appeared after 3 clicks on 'Game2Tab'` and
`Archetype popup did not open after 3 clicks`.

## Occurrences

| Run | Commit | Date | Notes |
|---|---|---|---|
| 31037214916 | `e1ec187` | 2026-08-05 18:58 | **Before the loading indicator existed** |
| 31057634743 | `88a65eb` | 2026-08-05 23:48 | Broader — 12+ failures including find-only tests, so possibly a different/worse mode |
| 31071811345 | `5df2e39` | 2026-08-06 04:38 | Overlay merge |
| 31072099391 | `f7661fe` | 2026-08-06 04:44 | Android-only branch — **cannot** have caused it |

## THE OVERLAY IS NOT THE CAUSE — do not re-blame it

The loading overlay looks guilty (it covers the page and relies on `InputTransparent`), and it
was my first suspect. It is not:

- `e1ec187` has the identical six-test signature and predates the indicator entirely.
- `f7661fe` changes only `UITests.Android/*` and the Android workflow, yet failed Windows the
  same way.

Both points have to be explained away before suspecting the overlay again.

## What is known

From the PerfLog for run 31071811345:

- Clicks worked early — `OpenArchetypePopup(PlayerArchetype): popup open on attempt 1` at
  04:46:31, and the BOSwitch tests clicked fine through 04:46:44.
- The first failure is at 04:46:52, and **nothing that clicks succeeds afterwards**.
- Each failing click still *takes* ~1000ms, so the driver believes it dispatched something.
- Interleaved find-only tests (`PageTitle_Displayed`, `Pickers_DisplayedAndEnabled`,
  `PlayerArchetype_Displayed`) all pass throughout.

**Does not reproduce locally.** Full suite 78/78, and `MainPageTests` alone — matching CI's
per-fixture matrix — is 25/25.

## Leftover-popup theory: DISPROVEN, do not re-run it

It was the leading hypothesis and it is wrong. The PerfLog for 31071811345 shows the popup
opened by `MainPage_ArchetypePicker_Search_FiltersResults` closing cleanly:

```
[04:46:32.975] DismissArchetypePopup: try TryClickIfPresent(ArchetypePopupCancel)
[04:46:33.391] DismissArchetypePopup: cancelClicked=True
[04:46:33.711] DismissArchetypePopup: OK — searchBar gone via Cancel in 313ms
```

and clicks kept working for eleven seconds afterwards (`BO3GameTabs`, `BOSwitch_DisplayedAnd
Toggled`, `BOSwitch_ShowsBO3Fields` all clicked successfully through 04:46:44).

Worth knowing anyway: `DismissArchetypePopup` **logs but does not throw** when the popup
survives, and its fallback `SendAndroidBack()` is a no-op on Windows. So on Windows a failed
dismiss is silent. That is a real latent hole even though it did not fire here.

## Falsified hypotheses — do not re-run these

Three, each killed by evidence rather than opinion. Re-testing them is wasted time.

1. **Leftover archetype popup swallowing clicks.** Disproven above — the popup closed cleanly
   and clicks kept working for eleven seconds after.
2. **Global input death after some point.** Disproven: `EnsureBO3On` logged
   `Game2Tab appeared in 450ms (attempt 1)` at 04:46:46.877, *after* the supposed cut-off. It
   is element-specific, not a dead session.
3. **Window geometry / below-the-fold clicks.** This was the strongest remaining theory: CI's
   desktop is 1024x768, `MainColumnsGrid` collapses to one column at narrow widths, and BO3
   adds a panel. Tested directly with the new `UITEST_WINDOW_SIZE` override — **MainPageTests
   is 25/25 at both 1024x768 and 800x600**, with the setup log confirming the resize applied.
   Geometry alone does not reproduce it.

What that leaves is CI-specific *timing* (the runner is far slower, and every click in the
failing run took ~1000ms versus ~200ms locally), or something not yet imagined. The point of
this branch is that the next occurrence arrives with the answer attached rather than needing
another round of theories.

## Where the failure actually starts

Last successful click: `MainPage_BOSwitch_ShowsBO3Fields`, ending 04:46:44.275 — it toggles
BO3 **on**. First failed click: 04:46:52.392. In between only `MainPage_FirstCheck_Displayed`
(a find) and the start of `Game3Tab_ShowsGamePanel`.

**CORRECTED:** I first wrote that no click happened in between. Wrong — `MainPage_BOSwitch_
ShowsBO3Fields` ends with `finally { ResetBOSwitch(); }`, and `EnsureBO3On` then logged
`Game2Tab appeared in 450ms (attempt 1)` at 04:46:46.877.

The honest summary is that **the PerfLog cannot tell you which clicks landed**, because the two
things it logs are both ambiguous:

- `TryClickIfPresent('BOSwitch'): clicked after 973ms` reports *dispatch*, not effect.
- `EnsureBO3On: Game2Tab appeared` can be reached without clicking at all, when the label
  already reads "Best of 3".

So "the last successful click" was not actually determinable from the artifacts. That gap is
now closed: `ResetBOSwitch` verifies the label flips and says so when it does not, and
`WaitUntilText` returns bool instead of void so a timeout stops looking like success.

What *is* solid: `Game2Tab` was present and clicked three times, and `UserNoteInput2` never
appeared; the same for the archetype pickers. The element exists, the click is dispatched, the
handler does not run.

## Next steps when picking this up

1. Read the `BLOCKERS [...]` lines now emitted by `LogInputBlockers` on every click-helper
   giveup (`UITests.Windows/BaseTest.cs`). They record top-level window count, the focused
   element, and whether any archetype-popup marker is present — enough to separate "second
   window", "focus stolen" and "popup open" without another guessing round.
2. Look hard at what toggling BO3 changes. It cascades `IsVisible` across the Game 2/3 panels,
   so it is the one structural layout change immediately before input dies.
3. The local/CI difference is most likely **screen size**: CI runs a smaller desktop, and
   MainPage's `MainColumnsGrid` collapses to one column, which changes what is on screen and
   what overlaps what.

## Related

- [[project_windows_tab_click_ci]] — tabs had to become `Button` for UIA `InvokePattern`
- [[project_uitest_nav_cascade_fix]] — earlier Windows Game3Tab stall, fixed by splitting SendKeys
- [[feedback_dont_churn_stable_ci]] — fix at the source; do not paper over with a retry layer
