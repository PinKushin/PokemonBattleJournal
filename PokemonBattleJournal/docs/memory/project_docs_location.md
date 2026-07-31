---
name: project_docs_location
description: docs/ folder lives inside PokemonBattleJournal/ subfolder so VS Solution Explorer includes it
metadata:
  node_type: memory
  type: project
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-07-30T19:59:50.449Z
---

docs/ was moved from the repo root into `PokemonBattleJournal/docs/` so Visual Studio Solution Explorer picks it up as part of the main app project.

**Why:** VS only shows files under a project folder in Solution Explorer. Root-level docs were invisible in the IDE.

**How to apply:** All doc and memory file references use `PokemonBattleJournal/docs/` prefix:
- Context doc: `PokemonBattleJournal/docs/AI-CONTEXT.md`
- Memory index: `PokemonBattleJournal/docs/memory/MEMORY.md`
- Memory files: `PokemonBattleJournal/docs/memory/*.md`

CLAUDE.md at repo root already updated to reflect new paths.
