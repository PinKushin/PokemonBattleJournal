---
name: project_combobox_cancel_hang
description: "NOT A BUG — the MainPage ComboBox Cancel 'hang' was a transient Windows OS hiccup, not app behavior. Closed 2026-08-05. Regression tests kept."
metadata:
  type: project
---

## Closed 2026-08-05 — not an application bug

The reported symptom (tapping Cancel on either MainPage archetype picker froze the app and
required a force-close) was **a one-off Windows OS hiccup**, not a defect in the popup
dismiss path. User confirmed 2026-08-05: the kind of transient "stopped responding" state
Windows produces for no application-level reason. It has not recurred.

**Do not go looking for an async deadlock in `ComboBoxControl` / `ComboBoxPopup`.** The
earlier speculation in this file — blocked UI thread, `PopupNavigation.Instance.PopAsync()`
never resolving, a cancel-triggered re-bind — was guesswork written before anyone had
reproduced it, and nobody ever did reproduce it.

## What was actually built in response

Regression UI tests, branch `fix/combobox-cancel-hang`, merged at `5c9b7da`:

- `MainPage_PlayerArchetype_Cancel_DismissesPopup`
- `MainPage_RivalArchetype_Cancel_DismissesPopup`

Both open the picker, dismiss it via the platform's natural gesture (`DismissPopupPlatform`
— Cancel button on Windows, BACK/outside-tap on Android, since the Cancel button often is
not in Android's UIA tree), then assert `ArchetypeSearchBar` leaves the tree within 3s.

**Keep these tests.** They passed immediately, which is part of why the report resolved as
environmental — but they are cheap, they cover a real user path, and they would catch a
genuine dismiss-path regression if one is ever introduced.

## Lesson worth carrying

A single unreproduced report on a desktop OS is not yet a bug. This one sat in the roadmap
as a "fix before next release" blocker with an unknown root cause, while the investigation
notes accumulated plausible-sounding causes for something that was never happening.
Reproduce first, then theorize.

[[feedback_combobox_popup_platforms]] — the real, confirmed ComboBox platform constraints
