---
name: reference_open_source_controls
description: Open source MAUI control libraries used as implementation references for ComboBox/picker controls.
metadata: 
  node_type: memory
  type: reference
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-07-27T21:23:29.898Z
---

## UraniumUI
- Repo: https://github.com/enisn/UraniumUI
- Key file: `src/UraniumUI.Material/Controls/PickerField.cs` — wraps native `PickerView`, uses `ClearFocus()` on Android to dismiss
- Key file: `src/UraniumUI/Controls/Dropdown.cs` — **inherits from `Button`** so it is natively `clickable=true` on Android; no TapGestureRecognizer hack needed
- Key file: `src/UraniumUI/Controls/Select.cs` — uses `PopupOverlay` + `SelectItemFromPointer`; keyboard navigation via `KeyDown` events
- Key insight: **make the trigger control a `Button` (or inherit from it) to get native Android clickability automatically**

## Controls.UserDialogs.Maui
- Repo: https://github.com/Alex-Dobrynin/Controls.UserDialogs.Maui
- Updated version of Acr.UserDialogs for .NET MAUI
- Useful for native-style dialogs and action sheets

## UraniumUI adoption decision

**UraniumUI is NOT adopted as a dependency.** Native MAUI controls are styled directly (custom colors, fonts, dark theme). UraniumUI is kept as a **read-only reference** — study its source to understand how to implement things like keyboard nav or Android clickability, then replicate the pattern natively. Never add UraniumUI as a package dependency.

**Why:** Windows UI and dark theme are styled and working with native MAUI controls. Switching to UraniumUI was explored and reverted — the native approach gives full control with no library coupling.

## How to apply

When building any MAUI control that needs to be tappable on Android:
1. **Trigger element**: use `Button` (or a subclass) as the tap target, NOT `ContentView` + `TapGestureRecognizer` — `Button` is natively `clickable=true` on Android
2. **List items in popup**: use `CollectionView SelectionMode="Single"` + `SelectionChanged` — MAUI's native handler marks each item `clickable=true`; never put `TapGestureRecognizer` on a `Grid` inside CollectionView
3. **Popup overlay**: `CommunityToolkit.Maui.Views.Popup` (non-generic) + `TaskCompletionSource<T>` for result; handle `Closed` event for tap-outside dismiss

[[project_theme_switcher]]
