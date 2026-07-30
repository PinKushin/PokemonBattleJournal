---
name: project_options_vm_bugs_fixed
description: OptionsPageViewModel bugs fixed — SaveTagAsync/SaveArchetypeAsync discarded returns; NewDeckIcon pre-initialized
metadata: 
  node_type: memory
  type: project
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-07-29T18:56:31.478Z
---

Two dead-code bugs fixed in `OptionsPageViewModel`:

**1. Discarded return values (SaveTagAsync + SaveArchetypeAsync)**
Both methods had `int affected = 0; _ = await ...SaveAsync(...)` — the return value was discarded, `affected` was always 0, the success log was unreachable. Fixed to `int affected = await ...SaveAsync(...)`.

**2. NewDeckIcon never set from UI (archetype save silently skipped)**
`SaveArchetypeAsync` null-checked `NewDeckIcon` before the `try` block — if null, returned early WITHOUT running `finally`, so `NewDeckName` was never cleared either. Root cause: `NewDeckIcon` was only set via `OnSelectedIconItemChanged`, which only fires when user explicitly picks an icon.

Fix: Initialize `NewDeckIcon = "ball_icon.png"` as the property default. After each save, `finally` resets it to `SelectedIcon` (not null) so the next save also has a valid default. Icon requirement is preserved — if caller explicitly sets `NewDeckIcon = null`, the null guard still blocks the save.

**Why:** Dead-code pattern `int affected = 0; _ = await ...` is easy to introduce when autocomplete fills in the discard. Watch for it on any `SaveAsync` call.
**How to apply:** When writing service save calls, always `int affected = await ...`, never `_ = await ...` with a pre-initialized local.
