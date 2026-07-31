---
name: feedback_bo3_state_idempotent
description: Use EnsureBO3On() to set BO3 state — blind BOSwitch click toggles off if already on, breaking downstream tests
metadata:
  type: feedback
---

Never blindly click `BOSwitch` to turn BO3 on. If BO3 is already on (leaked from a previous test), blind click turns it off, hiding `Game2Tab` (`IsVisible="{Binding BO3Toggle}"`), causing all Game2/Game3 lookups to fail.

Use `EnsureBO3On()` pattern:

```csharp
private void EnsureBO3On()
{
    AppiumElement label = FindUIElement("BO3StatusLabel");
    if (label.Text != "Best of 3")
        FindUIElement("BOSwitch").Click();
}
```

**Do NOT call `AndroidScrollToTop()` inside `EnsureBO3On()`.** Calling it unconditionally breaks tests that run early in the suite (page not yet scrolled): scrollToBeginning + BOSwitch click + binding re-render causes Game2Tab to be unfindable for ~28s.

Instead, call `AndroidScrollToTop()` explicitly at the top of individual tests that need it — specifically tests that run after a test which used UiScrollable stage-3 (which leaves the page scrolled). `ShowsGamePanel` and `ShowsWhenGame1IsTie` both call `AndroidScrollToTop()` explicitly before `EnsureBO3On()` because `FirstCheck_Displayed` (alphabetically prior) uses stage-3 and leaves the page scrolled down.

**Why:** `ResetBOSwitch()` uses `ImplicitWait = TimeSpan.Zero`. On slow emulators, `FindElement(AccessibilityId("BO3StatusLabel"))` returns nothing (element briefly off-screen or not yet rendered) and the `NoSuchElementException` is caught silently — BOSwitch never clicked, BO3 stays on. Next test then calls blind `BOSwitch.Click()` which toggles BO3 off. Performance log (`%TEMP%\UITests.PerfLog.txt`) showed 38s for `ShowsGamePanel` — the signal that cleanup timing was the problem.

**How to apply:** Always use `EnsureBO3On()` at the start of any test that needs BO3 active. Never assume `ResetBOSwitch()` succeeded.
