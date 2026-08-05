---
name: project-substitute-sprite
description: "substitute.png source and usage as the \"Other\" archetype icon"
metadata: 
  node_type: memory
  type: project
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-08-03T13:21:41.863Z
---

`substitute.png` is the default icon for the "Other" archetype and all unknown/unresolved icon fallbacks.

Source: https://play.pokemonshowdown.com/sprites/substitutes/gen5/substitute.png (downloaded manually)
Stored at: `PokemonBattleJournal/Resources/Images/` (NOT inside PokemonSprites/ — that folder is replaceable when new Pokemon come out)

**Why:** ball_icon.svg was overused everywhere as a fallback; substitute doll is more meaningful as "unknown/other".

**How to apply:** Any new "unknown" or "other" icon reference should use `substitute.png`, not `ball_icon.png`.
