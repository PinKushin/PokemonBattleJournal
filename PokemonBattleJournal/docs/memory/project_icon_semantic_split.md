---
name: project_icon_semantic_split
description: ball_icon.png = unselected/placeholder UI state; substitute.png = actual Other/unknown archetype
metadata: 
  node_type: memory
  type: project
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-08-03T20:52:44.840Z
---

Two icons with distinct semantic roles — never swap them:

- **`ball_icon.png`** — unselected/placeholder state: ComboBox before any archetype is picked, BO3 toggle button, Went-First toggle button, title bar decoration, AppShell flyout icon, OptionsPage new-deck-icon default. Means "tap me / open me."
- **`substitute.png`** — actual data: the "Other" archetype in DB, ReadJournal icon fallback when archetype missing, TrainerHill import archetype fallback when slug unresolved, SqliteConnectionFactory http-URL migration target. Means "unknown/other deck."

**Why:** The user's vision: pokeball = waiting to be opened (pick a pokemon); substitute doll = the deck is "other" or unknown. Mixing them makes the UI confusing.

**How to apply:** Any new unknown/fallback archetype icon reference → `substitute.png`. Any new "not yet selected" UI element → `ball_icon.png`. See [[project_substitute_sprite]] for substitute.png source and sizing.
