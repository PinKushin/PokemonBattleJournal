---
name: feedback_flaui_scroll_into_view
description: Use FlaUI ScrollItemPattern to scroll off-screen CollectionView items into view before WinAppDriver click; Actions.MoveToElement breaks
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-08-03T20:53:01.917Z
---

WinAppDriver `.Click()` on CollectionView items uses UIA InvokePattern — works regardless of scroll position for Button-type elements. But for items that are off-screen and not Button-typed, `Actions.MoveToElement().Click()` uses mouse coordinate simulation and fails because coordinates are invalid off-screen.

**Fix:** FlaUI `ScrollItemPattern.ScrollIntoView()` + WinAppDriver `.Click()`:
```csharp
AutomationElement? el = window.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
if (el is not null && el.Patterns.ScrollItem.IsSupported)
    el.Patterns.ScrollItem.Pattern.ScrollIntoView();
App.FindElement(MobileBy.AccessibilityId(automationId)).Click();
```
Implemented as `ScrollIntoViewAndClick(string automationId)` in `UITests.Windows/BaseTest.cs`. Android override just calls `FindUIElement(id).Click()` since UiScrollable already handles scroll at lookup.

**Why:** `Actions.MoveToElement` approach was tried and caused more failures than it fixed — it's coordinate-based and breaks for items outside the visible viewport.

**How to apply:** Any time a Windows UI test needs to click an item that may be off-screen in a list (delete buttons in OptionsPage, rows in ReadJournal), use `ScrollIntoViewAndClick` not `FindUIElement(...).Click()`.
