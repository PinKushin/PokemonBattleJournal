---
name: project_combobox_cancel_hang
description: "Cancel button on MainPage archetype ComboBox pickers freezes/hangs the app, requiring force-close"
metadata:
  node_type: memory
  type: project
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-08-04T00:00:00.000Z
---

## Bug: ComboBox Cancel Button Hangs App on MainPage

**Symptom:** Tapping the Cancel button on either archetype picker popup (PlayerArchetype or RivalArchetype) on the Journal Entry / MainPage freezes the entire application. Must force-close. Reported 2026-08-04.

**Affected:** MainPage ComboBox pickers only (OptionsPage pickers not yet confirmed affected).

**Root cause:** Unknown — not yet investigated. Likely candidates:
- Popup dismiss path blocking the UI thread (async deadlock)
- `PopupNavigation.Instance.PopAsync()` waiting on something that never resolves
- Cancel command in ComboBoxControl triggering a re-bind or reload that deadlocks

**Why:** User discovered during regular use 2026-08-04.

**How to apply:** Before touching ComboBoxControl dismiss/cancel logic, reproduce the hang first. Write a regression UI test that taps Cancel and asserts the popup is dismissed within 3 seconds (currently this would time out). Fix must make the test green. Do not ship any ComboBox change without verifying Cancel still works.
