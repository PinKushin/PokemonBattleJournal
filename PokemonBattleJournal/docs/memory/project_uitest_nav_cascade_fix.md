---
name: uitest-nav-cascade-fix
description: BaseTest.NavigateTo now resets _currentPage=null on exception to stop Android cascade failures; Windows picker SendKeys split fix
metadata: 
  node_type: memory
  type: project
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-07-30T16:49:16.732Z
---

## Android _currentPage cascade bug
`BaseTest` has `static string? _currentPage` tracking current Shell page to skip redundant navigation. Bug: if `NavigateTo` clicked the nav item but the page failed to load, `_currentPage` was still set to the target page. All subsequent tests in that class skipped navigation (thought they were already there) and all failed with `NoSuchElementException`.

**Fix** (`BaseTest.cs`): wrap navigation in try/catch; reset `_currentPage = null` in the catch so the next call re-attempts:

```csharp
try
{
    // ... click menu, click item ...
    _currentPage = pageTitle;
}
catch
{
    _currentPage = null; // force re-attempt next call
    throw;
}
```

## Windows picker keyboard nav (current implementation)

**SUPERSEDED by [[windows-picker-keyboard-nav]]** — the Enter approach was dropped entirely.

`SelectWindowsPickerItem(AppiumElement picker, string itemName)` in `BaseTest.cs` uses click + first letter + Tab. Enter is NOT used because it propagates to `SaveMatchButton` and clears the BO3 form. See [[windows-picker-keyboard-nav]] for full details.

## ClickTab helper for Border+TapGestureRecognizer (added 2026-07-30)

Plain `.Click()` on a MAUI `Border` with `TapGestureRecognizer` is unreliable on slow CI runners. Use `ClickTab()` from BaseTest instead:

```csharp
protected void ClickTab(AppiumElement tabElement)
{
    new OpenQA.Selenium.Interactions.Actions(App)
        .MoveToElement(tabElement)
        .Click()
        .Perform();
}
```

`Actions.MoveToElement().Click()` positions the mouse before clicking — matches real user behavior and works on slow CI. Any test clicking a Border tab must use `ClickTab()`.

## Editor/Entry focus before SendKeys

On CI, `SendKeys` on an Editor/Entry without prior focus discards keystrokes. Always `.Click()` the element first, then `SendKeys`. Affects `UserNoteInput` and any other text field tests.

## Windows UI tests passing state
All 40 Windows UI tests expected to pass after commit `1c1524f`. Duration ~1m30s on CI.
