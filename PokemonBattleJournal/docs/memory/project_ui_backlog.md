---
name: project_ui_backlog
description: "UI improvement backlog items noted post-NUnit migration, not blocking merge"
metadata: 
  node_type: memory
  type: project
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-07-30T21:36:22.792Z
---

Post-NUnit-migration UI backlog (noted 2026-07-30, none block feature/nunit-migration merge):

- **OptionsPage collections → searchable dropdowns** — archetype list and tag list should use ComboBoxControl-style dropdown for faster keyboard lookup (same pattern as archetype picker on MainPage).
- **ReadJournal match list → searchable** — match history CollectionView could get a SearchBar like the archetype picker for quick filtering.
- **Android styling + light theme pass** — full Android style pass still needed; never hardcode colors; emulator defaults to light theme.
- **Inline StartTime/EndTime** — currently two rows, takes too much space. User wants horizontal inline layout on all platforms. Was using chained popups in Syncfusion era; need to re-implement that pattern (popup chaining: select start → auto-open end, or side-by-side pickers).
- **ReadJournal + TrainerPage navigation slow on Android** — neither page should stall on navigate-to/from; investigate lazy load.
- **TrainerPage scroll stalls in UI tests** — page has many chart elements; UiScrollable stalls on each scroll step. Likely needs lazy/virtualized chart loading (existing TODO in active work).

**Why:** Syncfusion had built-in chained popup support that was removed. Time picker UX regressed when Syncfusion was dropped.
**How to apply:** When touching MainPage time fields or OptionsPage lists, check this backlog first to avoid duplicating work.
