---
name: project_windows_scrollview_reset
description: Windows BO3 UI tests should reset the MainPage ScrollView itself, not a nearby visible control, because clipped controls can disappear before the reset runs.
metadata:
  type: project
---

`MainPageTests.ScrollPageToTop()` now targets `MainPageScrollView` directly on Windows.

**Why:** The BO3 tests were still flaky on GitHub Actions even after adding viewport resets around Game 2/Game 3 transitions. The failing run showed `UserNoteInput2` and `Game3Tab` becoming unfound on Windows CI, which means the viewport reset needed to hit the actual scroll container instead of relying on `SaveMatchButton` or another nearby control staying visible enough to receive `Keys.Home`.

**How to apply:** When a Windows MAUI page can clip its lower section inside a `ScrollView`, give the `ScrollView` an `AutomationId` and use that directly for viewport normalization in UI tests. Do not assume a visible control is a stable stand-in for the scroll container.
