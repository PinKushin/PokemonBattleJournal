---
name: project-sprite-naming-constraint
description: MAUI asset naming requires underscores not hyphens; pokesprite repo uses hyphens; substitute.png lives in Images/ not PokemonSprites/
metadata:
  type: project
---

**Problem:** pokesprite repo (and CDN URLs) use hyphens (e.g., `alcremie-caramel-swirl-berry.png`). MAUI asset resizetizer requires asset names to be `[a-z0-9_]` only — no hyphens.

**Solution:** Keep sprite filenames with underscores for MAUI assets. Map CDN URLs → local files by replacing hyphens with underscores in the lookup.

`TryResolveSpriteFromUrl(url)`: extract filename from URL, replace `-` with `_` before returning.
`TryResolveLocalSprite(name)`: replace spaces with `_` (not `-`) to build local filename.
`ToDisplayName(filename)`: split on `_` (not `-`) to show "Dragapult Ex" in UI. Test cases must use underscore filenames.

**Limitless CDN URL format:** `https://r2.limitlesstcg.net/pokemon/gen9/dragapult.png` — base Pokémon names, no card variant suffix. Multi-word Pokémon use hyphens (e.g., `iron-hands.png` → local `iron_hands.png`).

**substitute.png:** Lives in `Resources/Images/` (NOT `PokemonSprites/`) because PokemonSprites/ is replaced wholesale when new Pokémon come out. Source: `https://play.pokemonshowdown.com/sprites/substitutes/gen5/substitute.png`. Cropped to 36×36 with 3px transparent margin to match pokesprite sizing convention (content was 30×30 in center of 96×96 canvas).

**icon_file_names.txt:** Regenerated from actual sprite filenames via PowerShell `Get-ChildItem ... | Select-Object Name | Set-Content`. Contains 1477 entries with underscore names. Lives in `Resources/Raw/`.

**Why:** MAUI's MSBuild asset ID generation is strict; cannot include hyphens. CDN URLs continue using hyphens; local asset names must be underscore. Conversion happens at lookup time in `TryResolveSpriteFromUrl`.
