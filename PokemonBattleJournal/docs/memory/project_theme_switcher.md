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

## Flyout (hamburger) icon — invisible on Android light mode, found 2026-08-05

**Observed facts only (mechanism NOT yet verified):**
- On the Android emulator (light theme by default), the hamburger/flyout button is
  invisible but fully functional — clickable at its expected position, present in the UIA
  tree with content-desc "Open navigation drawer" (Appium and screen readers unaffected).
- On Windows and/or dark mode, the flyout button shows as the Pokéball (`ball_icon.png`,
  set as `Shell.FlyoutIcon` in Styles.xaml:510).
- `Shell.ForegroundColor` in light mode was `PokeBlue` — the same StaticResource as the
  Shell nav bar background. Changed to `PokeYellow` on `feat/ci-matrix-per-fixture` as the
  probable fix; **not yet visually confirmed on the emulator**.

**Unverified:** whether Android tints the custom FlyoutIcon with ForegroundColor, or
whether `Shell.FlyoutIcon` simply doesn't apply on Android (known MAUI inconsistency) and
the default hamburger glyph was being drawn in ForegroundColor blue-on-blue. Verify by
looking at the emulator after the PokeYellow change: a yellow Pokéball silhouette means
tinted custom icon; a yellow standard hamburger glyph means FlyoutIcon isn't applying on
Android at all (different bug). Update this note once seen.

[[project_roadmap]]
