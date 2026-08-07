---
name: feedback_invokable_controls
description: "Anything tappable must be a real control exposing a UIA pattern, not a Border/Grid with a TapGestureRecognizer. SemanticProperties on a pattern-less element is fake accessibility — a screen reader cannot activate it, and automation can only reach it by coordinate click."
metadata:
  type: feedback
---

**Rule: if a user can tap it, it must be a control that exposes a UIA pattern.**

| Control | Pattern | Coordinate-free |
|---|---|---|
| `Button`, `ImageButton` | Invoke | yes |
| `CheckBox`, `Switch` | Toggle | yes |
| `RadioButton`, list item with `SelectionMode` | SelectionItem | yes |
| `Border` / `Grid` / `Image` + `TapGestureRecognizer` | **none** | no — mouse only |

## Why this is an accessibility rule, not a testing one

Assistive technology activates controls **through UIA patterns**, not by clicking pixels. A
`Border` with a `TapGestureRecognizer` is a hit-test target and nothing else, so a screen-reader
user cannot operate it at all.

**The trap: `SemanticProperties` makes it look handled.** `BOSwitch` carried
`SemanticProperties.Description`, `SemanticProperties.Hint` and
`AutomationProperties.IsInAccessibleTree="True"` — it read as fully accessible in review and in
the accessibility checklist. It was announced correctly and could not be activated. A
description without a pattern is a label on a control nobody can press.

Same shape still applies to every item in the archetype picker
(`Controls/ComboBoxControl/ComboBoxPopup.cs`): a `Grid` with a `TapGestureRecognizer`, one per
deck.

## Why it also broke the test suite

`WinAppDriver.Click()` is synthesized mouse input at the element's centre in **screen**
coordinates. With no pattern to invoke, that is the only way in — and when the element sits
below the window it clicks whatever occupies that screen position instead. See
[[project_windows_mainpage_click_flake]], where this was the root cause of a flake open for two
days.

## How to fix a control that must keep custom visuals

MAUI's `Button` only accepts Text + ImageSource, so it cannot *contain* an icon+label layout.
**Overlay it instead**: keep the existing visual element, mark it `InputTransparent="True"`, and
put a transparent borderless `Button` in the same `Grid` cell as the last sibling. Move the
`AutomationId`, the semantics and the command onto the Button — it is what UIA, Appium and
screen readers now see. Appearance is unchanged.

Two gotchas when doing this, both hit on 2026-08-06:

- The implicit `Button` style sets `MinimumHeightRequest`/`MinimumWidthRequest` to 44, and a
  minimum **beats** an explicit `HeightRequest`. Zero both on the overlay or it renders larger
  than the thing it covers.
- `BorderWidth="0"` alone does not remove a WinUI Button's border — it keeps its default
  `BorderBrush` unless `BorderColor` is set too. Now set to Transparent on the implicit style.

The white outline that remains on focus is WinUI's **focus visual**, a different mechanism.
Do not delete it: a visible focus indicator is WCAG 2.4.7. Recolour it to the palette instead.

## Related

- [[project_windows_mainpage_click_flake]] — the flake this caused and how clicks work now
- [[project_windows_tab_click_ci]] — the earlier instance: tabs had to become `Button`s
- [[feedback_combobox_popup_platforms]] — why `SelectionMode.Single` is not the fix here
