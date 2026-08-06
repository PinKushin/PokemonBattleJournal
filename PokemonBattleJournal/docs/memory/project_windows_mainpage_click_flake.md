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

## Next steps when picking this up

1. Find what runs between the last good click and the first bad one. In 31071811345 that
   window is `MainPage_FirstCheck_Displayed` and the start of `Game3Tab_ShowsGamePanel`
   (`EnsureBO3On`).
2. Suspect a leftover modal/popup owning input: a ComboBox popup left open swallows clicks
   while leaving the underlying tree findable, which matches the fingerprint exactly.
   `MainPage_ArchetypePicker_Search_FiltersResults` opens a popup earlier in the run.
3. Log the UIA window list (not just the element tree) on click failure — a second top-level
   window would show up immediately and would explain everything.
4. Note the local/CI difference is most likely **screen size**: CI runs a smaller desktop, and
   MainPage's `MainColumnsGrid` collapses to one column, changing what is on screen.

## Related

- [[project_windows_tab_click_ci]] — tabs had to become `Button` for UIA `InvokePattern`
- [[project_uitest_nav_cascade_fix]] — earlier Windows Game3Tab stall, fixed by splitting SendKeys
- [[feedback_dont_churn_stable_ci]] — fix at the source; do not paper over with a retry layer
