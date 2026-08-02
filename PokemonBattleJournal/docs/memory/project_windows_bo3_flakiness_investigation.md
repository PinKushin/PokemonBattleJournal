---
name: project_windows_bo3_flakiness_investigation
description: Investigation into flaky Windows BO3 Game3Tab tests — timing, scroll, and WinAppDriver UIA tree visibility issues; not fully resolved
metadata:
  type: project
---

## Status: PARTIALLY FIXED — needs more CI runs to confirm

Two Windows UI tests have been persistently flaky:
- `MainPage_Game3Tab_ShowsWhenGame1IsTie`
- `MainPage_Game3Tab_ShowsGamePanel`

Both follow the same pattern: set Game1 result via picker → click Game2Tab → set Game2 result → verify Game3Tab appears. The failure is always `NoSuchElementException` on `Game2Tab` or `Game3Tab` after a picker interaction.

## What we tried (chronological)

1. **`ScrollPageToTop()` after picker selections** (commit 6ff35db) — Added `Keys.Home` sent to `MainPageScrollView`. Doesn't work: WinUI3 ScrollView doesn't accept keyboard focus, so `SendKeys(Keys.Home)` is a no-op. The ScrollView never actually scrolls.

2. **Shift+Tab after picker Tab** (commit a25e748) — Moves logical focus back but doesn't scroll the viewport. Confirmed locally: no visual scroll happens.

3. **PageUp on SaveMatchButton** (commit 400c893) — `SendKeys(Keys.PageUp)` on the SaveButton (which has focus after Tab). Works locally but unclear if it helps on CI. Removed in favor of approach 4.

4. **Poll loop in FindUIElement** (commit 0298ba3, current) — Replaced the 3-stage retry (3s+10s+10s) with a 500ms poll loop up to 30s. Catches the element the instant it appears in the UIA tree. **This is the current approach.**

## Root cause theories (not confirmed)

### Theory A: WinAppDriver visibility filtering
WinAppDriver `FindElement(AutomationId(...))` may only return elements that are on-screen. After picker Tab moves focus to SaveMatchButton, WinUI3 ScrollView auto-scrolls the tab bar off-screen. WinAppDriver can't see it. But `FindUIElement` works fine on OptionsPage with off-screen elements, so this theory is weak.

### Theory B: ComboBox dropdown close animation
The MAUI Picker on Windows opens as a ComboBox dropdown. After Tab commits the selection, the dropdown close is async (WinUI3 visual transition). During the animation, WinAppDriver's UIA tree snapshot may be inconsistent — sibling elements (like Game2Tab) temporarily disappear from the tree. The earlier test `MainPage_BO3GameTabs_DisplayedWhenBO3Active` finds Game2Tab fine because it doesn't touch any picker, so no dropdown animation.

### Theory C: ShowGame3 binding cascade
Selecting a picker item fires `NotifyPropertyChangedFor(nameof(ShowGame3))` on `Result`/`Result2`. This triggers `Game3Tab.IsVisible` re-evaluation, which causes a native layout re-render of the tab bar. During the re-render, elements may briefly detach from the UIA tree. Combined with the dropdown close animation (Theory B), the instability window could be longer than expected.

### Theory D: CI timing / machine slowness
CI runners are slower than local machines. The binding cascade + dropdown close + layout re-render take longer. The old 3-stage retry did 3 long waits (3s, 10s, 10s) — if the element wasn't at those exact 3 moments, it failed. The poll loop (500ms intervals) is more likely to catch the element.

## What's deployed now

- `FindUIElement` on Windows: 500ms poll loop, 30s deadline, finally resets ImplicitWait to 5s
- `ScrollPageToTop()` calls removed from mid-test (only kept at test start and in cleanup helpers)
- CI timing logs uploaded as artifacts (`UITests.PerfLog.txt`, `UITests.NavLog.txt`, `UITests.Windows.setup.log`)

## What to check on next flaky CI run

1. Download `windows-ui-timing-logs` artifact
2. Check `UITests.PerfLog.txt` for how long the flaky test took — if it's near 30s, the poll loop is barely catching it
3. Check `UITests.Windows.setup.log` for driver/build timing
4. If the test still fails after 30s poll, the element is genuinely not in the UIA tree for 30+ seconds — need to investigate WinAppDriver or MAUI further

## Key files
- `UITests.Shared/BaseTest.cs:44-70` — `FindUIElement` Windows poll loop
- `UITests.Shared/Views/MainPageTests.cs:226-271` — `Game3Tab_ShowsWhenGame1IsTie`
- `UITests.Shared/Views/MainPageTests.cs:273-323` — `Game3Tab_ShowsGamePanel`
- `.github/workflows/ui-tests-windows.yml` — timing log artifact upload

## Lessons learned

- `Keys.Home` / `Ctrl+Home` / `Shift+Tab` sent via WinAppDriver `SendKeys` do NOT scroll a WinUI3 ScrollView. The ScrollView doesn't hold keyboard focus.
- WinAppDriver `FindElement` behavior with off-screen elements is inconsistent — sometimes works, sometimes doesn't. Don't assume it searches the full UIA tree.
- The old 3-stage retry pattern (3 attempts with long waits) is fragile for timing-sensitive UI. A poll loop with short intervals is strictly better.
- `SelectWindowsPickerItem` Tab approach is correct and reliable for picker selection — the issue is always what happens AFTER the Tab.
- CI timing logs were missing from all workflows. Now uploaded.
