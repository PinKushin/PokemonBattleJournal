---
name: feedback_cleanup_helper_timeout
description: Cleanup helpers that click non-optional UI elements must use FindUIElement (3s), not 0ms ImplicitWait — silent misses corrupt subsequent tests
metadata:
  type: feedback
---

`0ms ImplicitWait` in cleanup helpers is appropriate for OPTIONAL elements (e.g. "close a picker only if one is open"). It is NOT appropriate for elements that MUST be clicked to restore state.

`ResetGame1Tab()` originally used `0ms + AccessibilityId("Game1Tab")`. On slow emulators, the element isn't returned at 0ms → `NoSuchElementException` caught silently → Game1Tab never clicked → `IsGame2Selected = true` remains → MAUI hides the Game1 content panel (`View.GONE`) → `PossibleResultsPicker`, `TagsView`, `UserNoteInput` disappear from the Android view hierarchy → all subsequent tests fail at 26s (full FindUIElement timeout).

Fix: use `FindUIElement("Game1Tab")` (3s minimum wait, resourceId 3-stage) so the click reliably lands on slow emulators.

```csharp
private void ResetGame1Tab()
{
    try { FindUIElement("Game1Tab").Click(); }
    catch (OpenQA.Selenium.NoSuchElementException) { }
    finally
    {
        AndroidScrollToTop();
        RestoreImplicitWait();
    }
}
```

**Why:** `0ms` is only safe when the element's absence is expected and harmless. For state-restoring clicks, the element must be found reliably.

**How to apply:** Review every cleanup helper. If missing the click corrupts test state → use `FindUIElement`. If element is genuinely optional (may or may not exist) → keep `0ms + catch NoSuchElementException`.
