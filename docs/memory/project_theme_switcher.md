---
name: project_theme_switcher
description: "Long-term goal: add an in-app theme switcher (light/dark). Android emulator defaults to light theme on clean install."
metadata: 
  node_type: memory
  type: project
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-07-27T21:12:01.095Z
---

In-app theme switcher is a planned long-term feature.

**Why:** Android emulator (clean install) starts in light theme, so the app currently renders with light colors on Android even though the dev's intention is dark-first. A theme switcher would let users (and test environments) control this explicitly.

**How to apply:** Don't hardcode colors anywhere — always use `DynamicResource`/`AppThemeBinding` or pass colors as parameters so a future theme switch can propagate correctly. Never bake `PokeBlue`/`PokeYellow`/`OffBlack` as literals into C# code.

[[project_roadmap]]
