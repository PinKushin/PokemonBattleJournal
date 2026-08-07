---
name: project_windows_mainpage_click_flake
description: "CAUSE FOUND 2026-08-06: WinAppDriver clicks are mouse input at SCREEN coordinates, so targets below the window are clicked outside it — on CI that hits empty desktop and silently does nothing. Fixed by activating via UIA patterns; suite now 83/83 at CI geometry."
metadata:
  type: project
---

**CAUSE FOUND AND FIXED 2026-08-06.** Kept in full because the investigation is more
instructive than the fix, and because several confident conclusions along the way were wrong.

## The answer

`WinAppDriver`'s `Click()` is **synthesized mouse input at the element's centre, in SCREEN
coordinates**. An element MAUI has laid out below the window bottom is therefore "clicked" at
whatever screen position that resolves to.

- **Locally** that launched Visual Studio and the Epic Games store from the taskbar.
- **On CI** nothing sits behind the app, so the identical click lands on empty desktop and
  returns silently: dispatched, ~1000ms, no handler runs. Find-only tests keep passing because
  *finding* an off-viewport element works fine.

**Fix:** `TestBase.ClickElement` seam, with a Windows override that walks a UIA pattern ladder —
`ScrollIntoView`, then `Invoke`, then `Toggle`, then `SelectionItem`. None of those carry
coordinates, so window bounds cannot make them miss. The mouse path survives only for controls
with no pattern, and **refuses** to click a target measured outside the window.

**Result: the full Windows suite is 83/83 at CI's exact 754x512 geometry**, where it previously
failed and fired clicks into other applications. Both `Game3Tab` tests — two of the six in the
signature below — pass there now, having failed at that size before the change.

`BOSwitch` has since been converted — a transparent `Button` overlays the Border and owns the
AutomationId and command, so every BOSwitch click now logs `UIA Invoke`. **The archetype popup
items are the last controls reachable only by mouse** (`ComboBoxPopup.cs`, a `Grid` with a
`TapGestureRecognizer` per deck). See [[feedback_invokable_controls]] for the rule and the
overlay recipe.

**Final state:** Windows 83/83 at CI's 754x512 *and* at the normal window size, Android 82/82,
unit 506, integration 197. Merged at `6a49611`.

Two side-fixes worth knowing, both found while doing this:
- The implicit `Button` style's `MinimumHeightRequest=44` beats an explicit `HeightRequest`, so
  the first overlay rendered 44px tall over a 32px switch.
- `BorderWidth="0"` does not remove a WinUI Button's border without `BorderColor` — that was the
  outline on every button in the app, tabs included.
- The white outline remaining on focus is the WinUI **focus visual**, not a border. Leave it
  (WCAG 2.4.7); recolour it to the palette instead.



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
3. **Window geometry / below-the-fold clicks.** This was the strongest remaining theory:
   `MainColumnsGrid` collapses to one column at narrow widths, and BO3 adds a panel.

   Falsified — but note **how nearly it was falsified wrongly.** The first attempt guessed
   CI's window from the runner's desktop resolution and tested 1024x768 and 800x600. The new
   `App window:` log line then showed the real value: **754x512 at (59,52)** — smaller than
   either guess, so the test had been run at sizes that could not reproduce it by
   construction. Re-run at exactly 754x512: **MainPageTests 25/25**, setup log confirming the
   resize applied. Geometry is genuinely dead now.

   **SUPERSEDED 2026-08-06 evening — read the CONFIRMED MECHANISM section above.** That
   25/25 result proved elements can be *found* at CI's size. It never tested whether a click
   *lands on-window*, and they are different questions. Off-window clicks at 754x512 are now
   directly observed. Geometry is NOT dead; only "elements cannot be found" is.

   The lesson is the general one, and it applies to this very entry: a negative result is only
   worth as much as the fidelity of the setup that produced it — and as much as the precision
   of the claim it is taken to refute.

Timing remains a contributing factor and the numbers are stark, though it is no longer the
only surviving theory. On CI `Shell ready`
took 8798ms against ~485ms locally — roughly 18x — and every click in the failing run took
~1000ms against ~200ms here. Nothing about the app's layout differs; the runner is simply
far slower, and the tests' polling deadlines (2500-3000ms) were tuned on fast hardware.

The point of this branch is that the next occurrence arrives with the answer attached rather
than needing another round of theories.

**The instrumentation is confirmed working on CI**, not just locally: run 31074492325 emitted
`7. App window: 754x512 at (59,52)` in the setup log and PerfLog. Diagnostics that silently
no-op in the environment that matters would be worse than none, so check this line still
appears if the logging is ever refactored.

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
3. Chase **timing**, not geometry. An earlier version of this list said the local/CI difference
   was "most likely screen size" — that contradicted the falsified-hypotheses section directly
   above it, which had already re-run the fixture at CI's real 754x512 for 25/25. Screen size
   is dead. What survives is the 18x `Shell ready` gap and ~1000ms clicks against deadlines
   tuned at ~200ms.

## CONFIRMED MECHANISM (2026-08-06 evening): clicks landing OFF the app window

**Running the Windows UI suite at `UITEST_WINDOW_SIZE=754x512` on a normal desktop has real
side effects.** The off-window clicks hit whatever occupies that screen coordinate — observed
launching Visual Studio from the taskbar and pulling a browser to the front. Expect it; it is
not a red test misbehaving, it is genuine input going to other applications.

**Why it reaches the taskbar.** Not window placement — the window is nowhere near it
(screenshot: 754x512 at (0,0) on a 1920x1080 desktop, taskbar at y~1040). WinAppDriver reports
element coordinates in **screen space**, so an element MAUI has laid out ~545px below the
window bottom is clicked at y~1057, which is the taskbar. Any element below the fold can be
"clicked" anywhere on the desktop.

### The evidence

Two independent observations at CI's window size, both watched live:

1. A click landed on the **browser window behind** the app, bringing it to the front. The app
   returned, Save then fired on an empty form, and every following test desynced or stalled.
2. Several MainPage tests clicked the **taskbar**, trying to open Visual Studio.

So the driver is issuing clicks at coordinates outside the app window.

### Why CI shows a different symptom

On CI the app window fills the desktop and there is no taskbar or other window beneath it, so
the identical off-window click lands on nothing and returns silently. That is precisely the
recorded CI signature:

- the click is dispatched and takes its usual ~1000ms
- no handler runs
- find-only tests keep passing, because *finding* an off-viewport element still works
- onset is partway through a fixture, after BO3 toggling grows the page and pushes targets down

### The stacking breakpoint: necessary, not sufficient

`MainPage.OnSizeAllocated` stacked its columns only below **560**px while CI runs at **754**px,
so CI rendered two columns at ~377px each — narrow enough that the note editor stops shrinking
gracefully and the result and rival pickers clip at the right edge. Raised to **800**.

**But stacking alone did not stop the off-window clicks, and may make them likelier.** Verified
after the change: clicks still reached the taskbar. Stacking trades horizontal clipping for a
taller page, and at 754x512 **height** is the binding constraint — the page ends at "Start
Time / End Time" and everything below is off-window. Fixing the clipping moved more targets
below the fold.

So the breakpoint is worth keeping for layout quality, but the actual defect is in the click
path, not the geometry.

### Root cause of the geometry

`MainPage.OnSizeAllocated` stacked its two columns only below **560**px. CI runs at **754**px,
which cleared that threshold and so rendered two columns at ~377px each. At that width the note
editor stops shrinking gracefully and the result picker and rival archetype picker clip at the
right edge — and a control clipped at the window edge can have its centre, and therefore its
click point, outside the window.

Raised to **800** so CI's width stacks. Nothing then needs to be clipped or scrolled to be
clicked, which removes the failure mode rather than compensating for it.

### Still open after the breakpoint change

The breakpoint fixes the *geometry*. It does not fix the *test helpers*, which will still
dispatch a click at an off-window coordinate whenever a target does end up outside the viewport.
`LogInputBlockers` already computes `insideViewport` — the open question is why that never fired
on the failing CI runs. A click helper that refuses to click an off-window element, and says so,
is the durable guard.

## Earlier framing of the same lead

**Observed directly.** Running a cross-page test at CI's window size
(`UITEST_WINDOW_SIZE=754x512`) locally, the user watched a click land on **the browser window
behind the app**, pulling it to the front. The app then returned, Save was clicked on an empty
form, and every following test desynced or stalled.

So at 754x512 the driver clicked at coordinates **outside the app window** and hit whatever was
underneath.

**Why this is the best remaining explanation for the CI failures.** CI's window is exactly
754x512 (measured, logged as `7. App window: 754x512 at (59,52)`). There is no browser behind
it there — an off-window click lands on the empty desktop and silently does nothing. That is
the recorded signature precisely:

- the click is dispatched and takes its usual ~1000ms
- no handler runs
- find-only tests keep passing, because *finding* an off-viewport element still works
- it starts partway through a fixture — after BO3 toggling grows the page and pushes targets
  further down

**This does not contradict the geometry falsification above; it refines it.** That re-run at
754x512 went 25/25 and killed "elements cannot be FOUND at CI's size". It never tested whether
a click **lands on-window**. Finding and clicking are separate questions and only the first was
answered.

### What to do with it

1. Before clicking, assert the target's rect is inside the window rect, and log both when it is
   not. `LogInputBlockers` already reports `insideViewport` — check whether the failing clicks
   were flagged and, if not, why that check did not fire.
2. Scroll the target into view before clicking rather than trusting WinAppDriver's implicit
   scroll-on-click.
3. **Design the page to fit 754x512** (user's suggestion, and the structural fix). If nothing
   needs scrolling to be clicked, off-window clicks stop being possible. That is worth more
   than any amount of retry logic — see [[feedback_dont_churn_stable_ci]] on fixing at source.

## Foreground and pointer state: a real mechanism, but not this bug

Windows Appium clicks depend on the app window being frontmost, and this is **confirmed by
direct observation**, not theory: on 2026-08-06 a local full-suite run failed an About-page
click because the user moved the mouse as the driver clicked, and clicking the page by hand
let the run continue.

Two qualifiers, both of which matter:

- The user notes stray input "wasn't always the case" — the suite appears to have become more
  sensitive to it at some point. Nobody has investigated when or why. **Open thread**, and
  possibly the more interesting one, since a suite that grew input-sensitive may have grown
  other timing sensitivities.
- The related idea — that parallel Windows/Android runs stole focus from the Windows suite —
  is now **measured and false in that direction**. Running both concurrently on 2026-08-06,
  Windows passed 80/80 while Android failed 79/79. Windows is the aggressor, not the victim.
  See [[project_android_session_poisoning]].

**Load is not a way to reproduce this bug.** The concurrent run was an attempt to slow this
machine to CI speed. It failed completely: under full contention Windows still posted
`Shell ready` at **244ms** against CI's **8,798ms** — about 36x faster while loaded. The
timing theory may still be right, but testing it needs real throttling (CPU affinity, a
constrained VM), not a busy machine.

So "focus stolen" is a demonstrated way for a dispatched Windows click to do nothing, which is
exactly this bug's symptom — but it **does not explain the CI failures**. Each hosted matrix
job is its own VM with no human at the mouse and no emulator sharing the desktop. Keep it as a
mechanism to recognise in local runs, and as the reason a local run that anyone touched is not
evidence either way.

## Related

- [[project_windows_tab_click_ci]] — tabs had to become `Button` for UIA `InvokePattern`
- [[project_uitest_nav_cascade_fix]] — earlier Windows Game3Tab stall, fixed by splitting SendKeys
- [[feedback_dont_churn_stable_ci]] — fix at the source; do not paper over with a retry layer
