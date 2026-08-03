---
name: project_roadmap
description: "Planned features and product goals for PokemonBattleJournal — import/export, deck tools, and other roadmap items."
metadata: 
  node_type: memory
  type: project
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-07-27T20:15:18.066Z
---

Planned features confirmed by the user. Implement via TDD — write failing tests first.

**Why:** User stated these goals explicitly during sessions. They should drive future feature work and architecture decisions.

**How to apply:** When starting any new feature work, check this list. Prefer designs that leave room for these features even if not implementing them yet.

---

## Import / Export (JSON)

Format reverse-engineered from `trainerhill-battle-log-2026-07-27.json`:

```json
[
  {
    "playing": "archetype-slug",
    "against": "archetype-slug",
    "time": "2026-07-27 19:45:24.403684",
    "result": "Win|Loss|Tie",
    "game1": { "result": "Win|Loss|Tie", "turn": 1, "tags": ["..."], "notes": "..." },
    "game2": { ... },  // BO3 only
    "game3": { ... }   // BO3 only, split result
  }
]
```

Key mapping notes:
- `playing` / `against` are archetype name slugs — resolve to `Archetype` DB rows by name (case-insensitive slug match), create on import if absent
- `turn` is int OR string in the wild — coerce to `uint` (1 = went first, 2 = went second)
- `result` at match level is the overall result; game-level results drive BO3 calculation
- `tags` are tag names — resolve to `Tags` DB rows, create on import if absent
- `time` maps to `DatePlayed` + `StartTime`

Implementation plan (TDD):
1. `ImportService` — parses JSON array, resolves archetypes/tags, calls `MatchOperations.SaveAsync`
2. `ExportService` — queries `MatchOperations`, serializes to same JSON shape
3. Unit tests for both services with mock DB operations
4. OptionsPage: "Import" button (`FilePicker.PickAsync` → JSON file) + "Export" button (`FileSaver.SaveAsync`)
5. Both services injected via DI; no SQL in the services directly

## Deck Maker

Build and store deck lists tied to archetypes. Goals:
- Associate a deck list (card name + count) with an `Archetype`
- View/edit deck list from OptionsPage or a dedicated DeckPage
- Export deck list to a standard format (e.g., PTCG Live import format)

Architecture notes: new `DeckEntry` model + `DeckOperations` service; new Shell page if complex enough.

## Pokeball Archetype Picker Animation

When the archetype ComboBox is tapped, animate the pokeball icon as if it's opening to "release" the archetype list. Goal: reinforce the "tap to pick a Pokémon (deck)" metaphor.

**Trigger:** User idea from 2026-08-03 session — ball_icon.png is the unselected placeholder; opening the picker should feel like throwing a ball.

### Rough implementation plan

1. **Asset:** Create two frames — `ball_icon_open_top.png` (top half tilted up) and `ball_icon_open_bottom.png` (bottom half), or use a GIF/Lottie animation. Alternative: CSS-style rotation + translate of the existing ball via a custom `DrawingView` or SkiaSharp canvas.

2. **Trigger point:** `ComboBoxControl.OnTapped` or the `TapGestureRecognizer` command that opens the popup. Play the animation before `PopupNavigation.Instance.PushAsync(popup)`.

3. **Animation (MAUI `Animation` API):**
   ```csharp
   // Rotate top half up, bottom half down, then snap open
   var open = new Animation();
   open.Add(0, 0.4, new Animation(v => _ballTop.TranslationY = v, 0, -8));
   open.Add(0, 0.4, new Animation(v => _ballBottom.TranslationY = v, 0, 8));
   open.Add(0.4, 1.0, new Animation(v => _ballContainer.Opacity = v, 1, 0));
   open.Commit(this, "BallOpen", length: 300, easing: Easing.CubicOut,
       finished: (_, _) => { /* show popup */ });
   ```

4. **Close animation:** Reverse on popup dismiss — ball snaps shut, restoring opacity.

5. **Platform notes:** Test on Android (ensure animation doesn't block touch input) and Windows (MAUI animations run on UI thread; no WinAppDriver conflicts if AutomationId stays on the container).

6. **Accessibility:** Respect `AccessibilitySettings.IsReduceMotionEnabled` — skip animation if true, open immediately.

## Deck Comparer

Compare two deck lists side-by-side:
- Show cards in common, cards unique to each
- Highlight counts that differ
- Useful for tracking meta evolution between tournament seasons

Likely a sub-view of DeckPage rather than its own Shell page.
