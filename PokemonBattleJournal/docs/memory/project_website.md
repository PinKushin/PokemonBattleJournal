---
name: project-website
description: GitHub Pages landing site URL and deployment setup
metadata:
  type: project
---

Project website live at https://pinkushin.github.io/PokemonBattleJournal/

**Why:** Public-facing landing page for the open-source project.

**How to apply:** Reference this URL in docs, README, and any external links. The site is `index.html` at repo root, deployed by `.github/workflows/static.yml` on every push to master that touches `index.html`. GitHub repo homepage field also set to this URL. To update the site, edit `index.html` at repo root and push.

## Planned: self-host fonts, no Google Fonts CDN (2026-08-05)

Site currently pulls Inter/Baloo 2/JetBrains Mono from a Google Fonts `<link>`. User wants
these self-hosted instead (no CDN dependency) — download `woff2` files into a `fonts/`
directory next to `index.html`, reference via `@font-face { src: url(...) }` with relative
paths, drop the `<link rel="preconnect">`/stylesheet tags.

User also wants to bring in the Pokémon-brand font (`PokemonSolid`) for certain spots on
the site "sooner or later" — no need to source it fresh, it already ships in
`PokemonBattleJournal/Resources/Fonts/PokemonSolid.ttf` (used for app headings, see
[[project_styling_palette]]) and can just be copied into the site's `fonts/` directory.
`Saira-Regular/Bold/Black.ttf` are there too if the site wants to match the app's body font
as well. Not started — deferred until the broader site-refresh work happens (see
[[project_roadmap]]).
