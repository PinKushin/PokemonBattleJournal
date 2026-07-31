---
name: feedback_uiscrollable_direction
description: UiScrollable.scrollIntoView only scrolls DOWN — elements above current scroll position are unreachable; call AndroidScrollToTop first
metadata:
  type: feedback
---

`UiScrollable.scrollIntoView` (stage-3 in `FindUIElement`) scrolls in the configured direction only — default is vertical forward (downward). If a test leaves the page scrolled partway down and the target element is ABOVE the current viewport, stage-3 scrollable will scroll further DOWN, never find the element, and time out at 10s.

Stage 1 (3s) and stage 2 (10s) also fail if the element is off-screen above. Result: 23s total timeout (3+10+10) per element, multiplied by every lookup in the test.

**Observed:** `MainPage_Game3Tab_ShowsGamePanel` took 43.9s because `MainPage_FirstCheck_Displayed` (ran just before, alphabetically) used stage-3 UiScrollable which scrolled down to find `FirstCheck`. `ShowsGamePanel` then called `EnsureBO3On()` → `FindUIElement("BO3StatusLabel")` — BO3 controls are near the PAGE TOP, above where the scrollable left off. Multiple 23s timeouts stacked up.

**Fix:** Call `AndroidScrollToTop()` before any multi-step test that needs elements near the top of a long-scrolling page. Specifically, `EnsureBO3On()` does this now.

**How to apply:** Before any `FindUIElement` call where the target might be above current scroll position, call `AndroidScrollToTop()` first. The UiScrollable scrollToBeginning in `AndroidScrollToTop` always resets to top regardless of current position.
