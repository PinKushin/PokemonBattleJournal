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

**Scheduling (user, 2026-08-05):** theming comes *after* the remaining feature work —
export, loading indicator, archetype management UI (F-15), trainer name editing (F-16),
ReadJournal filter/search (F-13) — and is followed by the site refresh, which shares its
visual identity. Reason: theming a UI that is still gaining controls means doing it twice.
See [[project_roadmap]] and `docs/ROADMAP.md` for the full order.

**Why:** Android emulator (clean install) starts in light theme, so the app currently renders with light colors on Android even though the dev's intention is dark-first. A theme switcher would let users (and test environments) control this explicitly.

**How to apply:** Don't hardcode colors anywhere — always use `DynamicResource`/`AppThemeBinding` or pass colors as parameters so a future theme switch can propagate correctly. Never bake `PokeBlue`/`PokeYellow`/`OffBlack` as literals into C# code.

## Flyout (hamburger) icon — invisible on Android light mode, found 2026-08-05

**Observed facts only (mechanism NOT yet verified):**
- On the Android emulator (light theme by default), the hamburger/flyout button is
  invisible but fully functional — clickable at its expected position, present in the UIA
  tree with content-desc "Open navigation drawer" (Appium and screen readers unaffected).
- On Windows, the flyout button shows as the true-color Pokéball sitting in the title-bar
  area — the user initially read this as "Windows uses the window/title-bar icon as the
  hamburger," but it's the app's own `Shell.FlyoutIcon = ball_icon.png` setter
  (Styles.xaml ~:513) doing it; MAUI Shell on Windows just renders the flyout button in
  the title-bar region, so it looks like a window icon. Same setter drives all platforms.
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
