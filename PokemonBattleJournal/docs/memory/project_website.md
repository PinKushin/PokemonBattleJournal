---
name: project-website
description: GitHub Pages landing site URL and deployment setup
metadata:
  type: project
---

Project website live at https://pinkushin.github.io/PokemonBattleJournal/

**Why:** Public-facing landing page for the open-source project.

**How to apply:** Reference this URL in docs, README, and any external links. The site is `index.html` at repo root, deployed by `.github/workflows/static.yml` on every push to master that touches `index.html`. GitHub repo homepage field also set to this URL. To update the site, edit `index.html` at repo root and push.
