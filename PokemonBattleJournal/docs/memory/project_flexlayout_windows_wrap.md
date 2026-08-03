---
name: project-flexlayout-windows-wrap
description: FlexLayout Wrap never fires inside VerticalStackLayout on Windows — root cause and fix
metadata:
  type: project
---

`FlexLayout Wrap="Wrap"` does not work when the FlexLayout is a child of `VerticalStackLayout` on Windows (WinUI3/MAUI).

**Root cause:** `VerticalStackLayout` measures its children with infinite width. FlexLayout uses the measure-phase width to decide whether to wrap — infinite width means items always appear to fit, so wrapping never triggers regardless of `Basis`, `Grow`, or `Shrink` values.

**Fix used:** Replace the outer `FlexLayout` with a `Grid` (2 columns, 2 rows). Use `OnSizeAllocated` in code-behind to dynamically move the right column between row 0 col 1 (wide) and row 1 col 0 (narrow) based on actual page width. Set `SecondColDef.Width = new GridLength(0)` in narrow mode.

**Breakpoint:** 560px (two 280px minimum-width columns).

**How to apply:** Avoid `FlexLayout Wrap` inside `VerticalStackLayout` for responsive two-column layouts on Windows. Use Grid + `OnSizeAllocated` or a custom `Behavior<Grid>` instead.
