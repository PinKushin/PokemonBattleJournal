---
name: project_windows_picker_ci
description: MAUI Picker on Windows CI opens as child window — SelectWindowsPickerItem helper in BaseTest handles this
metadata: 
  node_type: memory
  type: project
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-07-29T18:56:12.453Z
---

On Windows Server CI (headless), MAUI's Picker control may open its dropdown as a child popup window rather than within the main app window's UIA tree. `//ListItem[contains(@Name,'Win')]` XPath fails with `NoSuchElementException` after the full implicit wait because WinAppDriver searches only the current window context.

**Fix:** `BaseTest.SelectWindowsPickerItem(string itemName)` in `UITests.Shared/BaseTest.cs` iterates all `App.WindowHandles`, switches to each non-main handle, searches for the list item, clicks it, and restores the main window context. Falls back to the main window last. Catches only `NoSuchElementException` per the no-silent-catch rule.

Works both locally (item found in main window on first attempt) and CI (found in popup child window).

**Why:** Windows Server headless rendering may cause WinUI3 ComboBox/Flyout to detach as an owned window in the UIA tree.
**How to apply:** All future Picker/ComboBox item selections in Windows UI tests should call `SelectWindowsPickerItem("ItemName")` rather than bare XPath on `App`.
