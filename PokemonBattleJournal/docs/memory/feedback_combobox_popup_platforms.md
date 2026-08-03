---
name: feedback_combobox_popup_platforms
description: ComboBox popup items must use platform-specific implementations for clickability
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-07-28T15:58:50.173Z
---

## Finding
**SelectionMode.Single breaks Windows tests** (window closes during seed).

Attempted to fix Android ComboBox popup clickability (UIAutomator2 `clickable=false`) by changing:
- `SelectionMode.None` + `TapGestureRecognizer` → `SelectionMode.Single` + `SelectionChanged` handler

Result: Windows app window crashes mid-seed (CloseAsync failure or popup lifecycle issue).

## Why
- TapGestureRecognizer: works on Windows, ignored on Android (UIAutomator doesn't mark as clickable)
- SelectionMode.Single: crashes Windows, doesn't solve Android's UIAutomator issue

## Solution path (not implemented yet)
1. **Platform detection in ComboBoxPopup**: use `DeviceInfo.Platform` to choose implementation
   - Windows: TapGestureRecognizer
   - Android: Button wrapper or alternative approach
2. **OR Button wrapper**: wrap items in native Button (clickable on both)
3. **Pending**: proper Android fix still needed

Reverted SelectionMode.Single change. Original TapGestureRecognizer restored.

## Accessibility IDs required on all interactive popup elements

Every element in `ComboBoxPopup` that Appium or screen readers need must have an `AutomationId`. Added in commit 4092182/167d915:

- `SearchBar` → `AutomationId = "ArchetypeSearchBar"`
- Cancel `Button` → `AutomationId = "ArchetypePopupCancel"`

**Why:** On Windows, `MobileBy.AccessibilityId` matches `AutomationId` — it does NOT fall back to `Text`. A button with `Text = "Cancel"` and no `AutomationId` is invisible to WinAppDriver. Tests that relied on `AccessibilityId("Cancel")` failed on CI even though the button was visible. Use `TryClickIfPresent("ArchetypePopupCancel")` in test cleanup, not text-based lookup.
