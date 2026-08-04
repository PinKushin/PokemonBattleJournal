---
name: project-collectionview-gridlayout-windows
description: GridItemsLayout.Span mutation on Windows causes native ItemsRepeater flash; CollectionView.Header always above items in Vertical orientation
metadata: 
  node_type: memory
  type: project
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-08-04T16:17:40.207Z
---

## CollectionView layout learnings (Windows/WinUI)

**CollectionView.Header position:**
`Header` with `Orientation="Vertical"` renders ABOVE the items grid, never beside it. To put a label to the LEFT of the tag grid, place the CollectionView inside a `Grid ColumnDefinitions="Auto,*"` with the label in column 0 and CollectionView in column 1.

**GridItemsLayout.Span mutation causes flash on Windows:**
`Span` is a BindableProperty, but changing it dynamically during window resize causes visible 1/3/wrong-column flashes on Windows. Root cause: WinUI's native `ItemsRepeater` runs its own layout pass on every size change, one frame before MAUI's BindableProperty change propagates. The native engine briefly shows an intermediate column count. Reassigning the entire `ItemsLayout` object is even worse — replaces the whole native layout engine, bigger flash.

**Fix:** Use a fixed `Span` value in XAML. Do not attempt to change Span dynamically during resize on Windows. Responsive column switching via `OnSizeAllocated` fights the native engine and loses.

**Orientation semantics:**
- `Orientation="Vertical"` + `Span=N` → N columns, rows expand as needed (vertical scroll). This is the correct mode for tag chips.
- `Orientation="Horizontal"` + `Span=N` → N rows, columns expand (horizontal scroll). Not suitable for tag chips.

**Why:** Discovered during tags responsive layout work (2026-08-04). Spent multiple sessions fighting flash artifacts before concluding fixed Span=4 is the right answer.

**How to apply:** For any CollectionView with GridItemsLayout on Windows: set Span in XAML and leave it. If truly needing responsive column counts, use FlexLayout + BindableLayout instead (loses built-in multi-select).
