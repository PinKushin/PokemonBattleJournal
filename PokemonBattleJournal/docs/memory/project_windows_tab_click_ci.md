---
name: project_windows_tab_click_ci
description: Use PointerKind.Pen for WinUI TapGestureRecognizer — Touch is no-op on CI, Mouse rejected by WinAppDriver locally
metadata:
  type: project
---

`PointerKind.Touch` silently no-ops on Windows Server CI (no touch driver). `PointerKind.Mouse` is rejected by WinAppDriver locally with `UnsupportedOperationException: Currently only pen and touch pointer input source types are supported`. Use `PointerKind.Pen` — WinAppDriver explicitly supports it, no physical pen hardware needed, works both locally and on CI.

```csharp
var pen = new OpenQA.Selenium.Appium.Interactions.PointerInputDevice(
    OpenQA.Selenium.Interactions.PointerKind.Pen, "pen");
var seq = new OpenQA.Selenium.Interactions.ActionSequence(pen, 0);
seq.AddAction(pen.CreatePointerMove(tabElement, 0, 0, TimeSpan.Zero));
seq.AddAction(pen.CreatePointerDown(OpenQA.Selenium.Interactions.MouseButton.Left));
seq.AddAction(pen.CreatePointerUp(OpenQA.Selenium.Interactions.MouseButton.Left));
App.PerformActions([seq]);
```

**Why:** Tab elements are `Border` with `TapGestureRecognizer` — not `Button`. WinAppDriver error message "only pen and touch supported" is the clue: Pen is the safe cross-environment choice. Touch = silent failure on CI. Mouse = local WinAppDriver rejection.

**How to apply:** Any MAUI `Border`/non-button tappable in Windows UI tests: use `PointerKind.Pen` in `ActionSequence`.
