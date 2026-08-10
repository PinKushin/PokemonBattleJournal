---
name: feedback_flaui_scroll_into_view
description: "Use FlaUI ScrollItemPattern to bring an off-screen item into view, then click through ClickElement — never raw .Click(). CORRECTED 2026-08-09: the original claim that WinAppDriver .Click() uses InvokePattern was wrong; it is synthesized mouse input at SCREEN coordinates."
metadata:
  type: feedback
---

## Corrected 2026-08-09 — the original mechanism claim was wrong

This entry used to open with:

> *"WinAppDriver `.Click()` on CollectionView items uses UIA InvokePattern — works regardless of
> scroll position for Button-type elements."*

**That is false, and it directly contradicted [[project_windows_mainpage_click_flake]]**, which
root-caused a six-test CI failure to the opposite fact. Both entries were live at once, saying
opposite things about the same API.

**The truth:** `WinAppDriver.Click()` is **synthesized mouse input at the element's centre in
SCREEN coordinates**. It carries no UIA pattern. An element laid out below the window is clicked
at whatever screen position that resolves to — locally that launched Visual Studio and the Epic
Games store off the taskbar; on CI it lands on empty desktop, returns in ~1000ms having done
nothing, and find-only tests keep passing.

The `.Click()`-uses-InvokePattern belief is why the click flake stayed open from 2026-08-05: it
made "the element was found, so the click worked" look like sound reasoning.

## What the code does now

`ScrollIntoViewAndClick` scrolls with FlaUI, then hands off to the **guarded** `ClickElement`:

```csharp
AutomationElement? el = window.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
if (el is not null && el.Patterns.ScrollItem.IsSupported)
    el.Patterns.ScrollItem.Pattern.ScrollIntoView();

// NOT App.FindElement(...).Click(). ScrollIntoView may still leave the target off-window in a
// container UIA cannot scroll, and a raw click there lands in another application.
ClickElement(automationId);
```

`ClickElement` walks a UIA pattern ladder — ScrollItem, Invoke, Toggle, SelectionItem,
ExpandCollapse, Focus for text inputs, then up to three ancestors — none of which carry
coordinates. Only if no pattern is available does it fall back to the mouse, and that path
**refuses** to click a target measured outside the window rather than firing into another app.

## The part that was right

FlaUI's `ScrollItemPattern.ScrollIntoView()` is still the correct way to bring an off-screen
CollectionView item into view, and `Actions.MoveToElement().Click()` is still wrong — it is
coordinate-based and breaks for anything outside the viewport. That half of the original entry
stands.

## How to apply

Clicking an item that may be off-screen in a list — delete buttons on OptionsPage, rows in
ReadJournal — use `ScrollIntoViewAndClick`. Never `FindUIElement(...).Click()`, on any element:
that is the raw mouse path with no guard.

**Android is a separate problem with a separate fix.** There, `.Click()` reaches the driver but
sometimes never reaches the MAUI handler at all — see [[feedback_android_flaky_tap_retry]] for
the click-verify-retry pattern. Do not conflate the two: Windows clicks land in the wrong
*place*, Android clicks land nowhere.

## Related

- [[project_windows_mainpage_click_flake]] — the investigation that established the real mechanism
- [[feedback_invokable_controls]] — why a tappable element must be a real control with a pattern
- [[project_uitest_presence_is_not_one_question]] — the other place FlaUI and WinAppDriver disagree
