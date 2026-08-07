---
name: feedback_repro_setup_can_hide_the_bug
description: "A local repro of an environment-specific bug can be wrong in a way that makes the fix look correct. Three times on 2026-08-06 a change passed locally under conditions that could not expose its flaw — twice because of reproducibility features added to catch that very class of bug."
metadata:
  type: feedback
---

**A negative result is only worth the fidelity of the setup that produced it — and the setup is
itself code that can be wrong.**

Hit three times in one day, in escalating irony.

## 1. Guessing the environment instead of measuring it

The Windows click flake was "falsified as geometry" by re-running at 1024x768 and 800x600, both
inferred from the runner's desktop resolution. Logging the real value showed CI's window is
**754x512** — smaller than either guess. The falsification had been run at sizes that could not
reproduce the bug by construction.

Fix: log the window rect every session. That was the highest-value diagnostic of the whole
investigation.

## 2. Falsifying a narrower claim than the one that mattered

Re-run at the correct 754x512, `MainPageTests` went 25/25, and geometry was recorded as *dead*.

It was not. That run proved elements can be **found** at CI's size. It never tested whether a
click **lands on-window** — and clicks landing outside the window turned out to be the entire
bug. Two different questions; only the first had been answered, and the conclusion was written
as though both had been.

**Before recording a hypothesis as falsified, state exactly which claim the evidence kills.**

## 3. The reproducibility feature hiding the bug it was built to find

`UITEST_WINDOW_SIZE` pins the app window to **(0,0)** for reproducible runs. At (0,0),
screen-space and window-relative coordinates are identical.

The off-window click guard compared element rects against a screen-space window origin. That is
wrong — WinAppDriver reported `UserNoteInput` at `(24,311)` inside a window at `(85,78)`, and
x=24 being left of the origin is impossible for a child, so the rect was window-relative. The
guard was verified locally many times and **could not fail**, because the pin collapsed the two
coordinate spaces into one. CI, whose window sits at (85,78), failed immediately.

Fix: `UITEST_WINDOW_POS` overrides the pin, so CI's *origin* is reproducible too, not just its
size. And the guard now requires the target to be outside under **both** interpretations.

## What to actually do

- **Measure the environment; never infer it from a plausible proxy.** Log it in the artifacts.
- **Reproduce the whole geometry** — size *and* position. A repro that starts somewhere
  different each run is testing something different each run.
- **Ask what a passing run has actually ruled out**, and write down that narrower claim rather
  than the comfortable general one.
- **Be suspicious of a check that has never failed.** If a guard cannot fail under the setup
  used to test it, the setup is part of the guard and needs its own test.

## Related

- [[project_windows_mainpage_click_flake]] — all three instances happened inside this one bug
- [[feedback_test_the_hypothesis_first]] — make the suspected cause observable before fixing it
- [[feedback_dont_churn_stable_ci]] — and why a guard that fires wrongly is worse than none
