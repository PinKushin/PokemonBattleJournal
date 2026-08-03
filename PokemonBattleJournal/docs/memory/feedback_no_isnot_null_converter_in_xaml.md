---
name: feedback_no_isnot_null_converter_in_xaml
description: Never use IsNotNullConverter in XAML for icon visibility — it crashed the OptionsPage; use bool VM properties instead
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-08-03T21:15:09.513Z
---

`IsNotNullConverter` (CommunityToolkit.Maui) used as an `IsVisible` binding converter for image icons caused a crash on the OptionsPage in a prior session.

**Why:** Exact root cause unknown, but the crash is reproducible when the converter is wired to image source visibility during page startup.

**How to apply:** For any `IsVisible` binding that depends on whether a string/object is null, expose an explicit `bool` property on the ViewModel (`HasPlayingIcon2`, `HasAgainstIcon2`, etc.) and bind `IsVisible` directly to that. Do NOT use `IsNotNullConverter` or `StringNotEmptyConverter` in XAML bindings. The converter class (`StringNotEmptyConverter`) can still be used in code-behind DataTemplates (e.g., `ComboBoxPopup`) where the crash does not occur.
