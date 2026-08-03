---
name: project_trainerhill_import
description: TrainerHill battle log import feature — JSON format quirks and design decisions
metadata:
  type: project
---

TrainerHill JSON export (`trainerhill.com`) can be imported via Options page → "Import from TrainerHill" button.

**Why:** User copies battle logs from TrainerHill and wants them in the local SQLite journal.

**Key format quirks:**
- `playing`/`against` are kebab-case slugs → `SlugToName()` converts to title-case ("dragapult-dusknoir" → "Dragapult Dusknoir")
- `turn` field is mixed-type: integer `1` or string `"2"` → stored as `JsonElement`, parsed via `ValueKind` switch
- `game2`/`game3` are absent for BO1 entries; presence signals BO3
- `result` values are "Win"/"Loss"/"Tie" (case-insensitive)
- `time` is a datetime string (no timezone) used for `DatePlayed`/`StartTime`/`EndTime`

**Architecture:**
- `Services/Import/TrainerHillModels.cs` — internal record DTOs
- `Interfaces/ITrainerHillImportService.cs` — public interface
- `Services/Import/TrainerHillImportService.cs` — implementation
- Registered as singleton in `MauiProgram.cs`
- `OptionsPageViewModel.ImportFromTrainerHillCommand` — calls `FilePicker.Default.PickAsync`, then `ImportAsync`
- `OptionsPage.xaml` — "Import from TrainerHill" button + `ImportStatusLabel` (shows "Imported N matches" or error count)

**Slug-to-name resolution:** `TrainerHillImportService` injects `ILimitlessMetaService`. At `ImportAsync` start, fetches all Limitless decks (`GetTopDecksAsync(int.MaxValue)`) and builds a `Dictionary<string, string>` lookup via `BuildSlugLookup`. Each Limitless name generates up to 4 normalized keys (2 possessive variants × 2 version-strip variants) to cover TrainerHill's inconsistent slug conventions. For unknown slugs, falls back to `SlugToName` (title-case). `LookupSlug` normalizes the slug (lowercase, hyphens→spaces) and looks up the key. `GetTopDecksAsync` returns empty list when offline or in UI test mode — import falls back to title-case gracefully.

**Archetype get-or-create:** `INSERT OR IGNORE INTO Archetype (Name, ImagePath)` with `ball_icon.png` as default. Resolved name (from lookup or title-case) used. Lock held during insert+select.

**Tag dedup:** Tags.Name is globally unique across trainers. `ResolveTagAsync` checks by name first; if missing, inserts with the importing trainer's ID. UNIQUE constraint race caught in catch block.

**Error handling:** Per-entry errors collected in `List<string>`, returned alongside import count. One bad entry does not abort the rest.

**How to apply:** When touching the import feature, know the mixed-turn type and slug conversion are the two non-obvious parts; all else is standard get-or-create DB logic.
