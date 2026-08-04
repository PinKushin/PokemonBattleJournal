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

**Physics note:** Pokeball hinges at the back — only the top half rotates away from the viewer. Bottom stays still.

1. **Asset:** Split `ball_icon.png` into two separate images: `ball_icon_top.png` (top red half) and `ball_icon_bottom.png` (bottom white half). Stack them in a Grid.

2. **Trigger point:** `ComboBoxControl.OnTapped` / `TapGestureRecognizer` command before `PopupNavigation.Instance.PushAsync(popup)`.

3. **Animation (MAUI `Animation` API):**
   ```csharp
   // Top half rotates backward around its bottom edge (the hinge line).
   // AnchorY = 1.0 pins the pivot at the bottom of the top image.
   _ballTop.AnchorY = 1.0;
   var open = new Animation();
   open.Add(0, 0.6, new Animation(v => _ballTop.RotationX = v, 0, -110,
       easing: Easing.CubicIn));   // rotate lid back ~110° (past vertical so it's clearly open)
   open.Add(0.5, 1.0, new Animation(v => _ballContainer.Opacity = v, 1, 0,
       easing: Easing.Linear));    // fade out as it opens
   open.Commit(this, "BallOpen", length: 280,
       finished: (_, _) => { /* show popup; reset RotationX = 0, Opacity = 1 */ });
   ```

4. **Close animation:** Reverse — `RotationX` from -110 back to 0, opacity 0 → 1, triggered on popup dismiss callback.

5. **Platform notes:** `RotationX` is 3D perspective rotation; verify it doesn't render flat on Android API < 28. MAUI animations run on UI thread — keep length ≤ 300ms so it doesn't feel laggy before the picker appears.

6. **Accessibility:** Check `AccessibilitySettings.IsReduceMotionEnabled` — skip animation and open immediately if true.

## Known Bugs (fix before next release)

### ComboBox Cancel Button Hangs App (MainPage)
Tapping Cancel on either archetype picker popup on MainPage (Journal Entry) freezes the app — requires force-close. Root cause unknown. Likely async deadlock in popup dismiss path. TDD approach: write failing UI test that taps Cancel and asserts popup dismisses within 3 seconds, then fix.

See [[project_combobox_cancel_hang]] for investigation notes.

---

## Deck Comparer

Compare two deck lists side-by-side:
- Show cards in common, cards unique to each
- Highlight counts that differ
- Useful for tracking meta evolution between tournament seasons

Likely a sub-view of DeckPage rather than its own Shell page.
