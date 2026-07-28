---
name: project_sqlite_only
description: All app state is in SQLite — no MAUI Preferences API used. CLAUDE.md reference to preferences.dat is stale.
metadata: 
  node_type: memory
  type: project
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-07-27T21:57:34.798Z
---

All persistent state lives in `PokemonBattleJournal.db3` (SQLite). The MAUI `Preferences` API is **not used anywhere** in the app — confirmed by full codebase search. The old `CLAUDE.md` mention of `preferences.dat` in `WipeAppData()` is stale; that file is searched for but never actually created by the app.

**Why:** User asked whether Preferences could be removed and data moved to SQL. Answer: already done — nothing to migrate.

**How to apply:** UI test seed injection only needs to place a pre-built `.db3` file. No preferences file needs to be written. `WipeAppData()` can simplify to DB-only deletion.
