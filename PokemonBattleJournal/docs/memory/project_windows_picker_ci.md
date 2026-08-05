---
name: project_windows_picker_ci
description: "CORRECTED 2026-08-05 — the WindowHandles-iterating SelectWindowsPickerItem was deleted in df081b9. Windows picker selection is keyboard nav: click, first letter, Tab. Do not reinstate the window search."
metadata:
  type: project
---

## Corrected 2026-08-05 — the approach described here no longer exists

**Do not reinstate the `App.WindowHandles` iteration.** It was deliberately removed in
`df081b9` ("use Tab to confirm picker selection instead of Enter on Windows"), which found
keyboard navigation faster and working on both Windows 11 and Windows Server CI.

**What the code actually does now** (`UITests.Windows/BaseTest.cs`):

```csharp
protected override void SelectWindowsPickerItem(AppiumElement pickerElement, string itemName)
{
    pickerElement.Click();                            // open the dropdown
    pickerElement.SendKeys(itemName[0].ToString());   // first letter jumps to the item
    pickerElement.SendKeys(OpenQA.Selenium.Keys.Tab); // Tab confirms and closes
    // then re-anchor, so IsVisible cascades that fired while the dropdown was open are seen
}
```

Measured 2026-08-05 and not a bottleneck: `click 225ms, letter 33ms, tab 58ms,
re-anchor 19ms` — ~330ms total. It was ruled out as the cause of the Game3Tab stall
(see [[project_game3tab_ci_flake_recurring]]).

**Two details worth keeping, both learned the hard way:**

1. **Tab, not Enter.** `Keys.Enter` inside a MAUI Picker/ComboBox on Windows propagated to
   `SaveMatchButton`, clearing the BO3 layout and failing the Game3Tab tests (`df081b9`).
2. **Two separate `SendKeys` calls.** The letter and the Tab must not be combined into one
   string — a combined string stalled on Windows (`f2768f5`).

## The original observation (kept for context)

On Windows Server CI, MAUI's Picker *may* open its dropdown as a child popup window rather
than inside the main window's UIA tree, so `//ListItem[contains(@Name,'Win')]` XPath could
fail with `NoSuchElementException`. That observation was real. The fix simply removed the
need to find the popup at all — keyboard navigation never queries the dropdown's UIA tree,
so where the popup lives stopped mattering.

## Related

- [[feedback_combobox_popup_platforms]] — confirmed ComboBox platform constraints
- [[project_uitest_nav_cascade_fix]] — the SendKeys split
