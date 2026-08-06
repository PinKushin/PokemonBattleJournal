---
name: project_backup_restore
description: "Backup export is lossless and RestoreService is built and merged; TrainerHill re-imports no longer duplicate. Remaining: UI wiring, then conflict resolution as its own branch."
metadata:
  type: project
---

**Status 2026-08-06.** Service layer done and merged to master. Nothing is user-facing yet —
`IRestoreService` is registered in DI and nothing calls it.

| Piece | State |
|---|---|
| Export carries `startTime` / `endTime` | done |
| Export `time` field carries `StartTime`, not `DatePlayed` | done |
| Export carries archetype icons + owning trainer | done |
| `RestoreService` — merge trainer by name, dedupe, report | done |
| TrainerHill import stops duplicating on re-import | done |
| **OptionsPage restore button + status** | **not started** |
| **Conflict resolution UI** | **own branch, not started** |

## The export was not lossless, three separate ways

All three found by writing a test first and watching it fail. Worth knowing because each was
invisible from the outside:

1. **`startTime` / `endTime` were absent entirely.** `MatchEntry` stores them apart from
   `DatePlayed`, and `CalculateAverageMatchDuration` / `CalculateWinRateByMatchLength` are
   computed from them. A restore would have produced zero-length matches and quietly wrong
   statistics.
2. **`time` carried `DatePlayed`** — the weak field, which a date picker leaves at midnight.
   It now carries `StartTime`. **The existing round-trip tests could not have caught this**:
   their seed set `DatePlayed` equal to `StartTime`, so the two were indistinguishable. A test
   that seeds them the same cannot prove which one is written.
3. **Archetype icons were not exported at all.** They are user data — OptionsPage lets a custom
   archetype pick one, `ImagePath2` carries a dual-icon deck's second — and no amount of
   guessing from a name recovers a deliberate choice.

## Archetypes: what is actually true about ownership

Corrected twice while building this, so take it from here rather than from intuition:

- `Archetype.Name` is `[Unique]` **across the whole table** — two trainers cannot both hold
  "Pinku's Brew". That is why the backup carries archetypes once at envelope level rather than
  per entry.
- But the table is **not** global. Each row has a `TrainerId`, and a custom archetype belongs to
  whoever created it. `ArchetypeOperations.SaveAsync` does persist it; only the seed and scrape
  paths bypass it with raw `INSERT OR IGNORE`, which is why those sit at 0.
- Consequence: **seeded and scraped rows are indistinguishable from each other.** Any plan that
  needs to tell them apart does not work without a schema change.
- Ownership exports as a trainer **name**. Row ids are renumbered when restoring into a fresh
  install.

**Every archetype is exported, scraped ones included** (user's call, and the reasoning is the
part to keep): resolving a Limitless deck to a local sprite is real work via `SpriteResolver`'s
alias table, and the meta shifts — an archetype that was top-10 when a match was played can be
absent from the next scrape. Since a restore recreates archetypes from the names its matches
reference, an omitted one would come back as `substitute.png` and stay that way.

## Duplicate detection

`MatchDuplicateKey` in `Services/`, shared by the restore and the TrainerHill import so the two
cannot drift — the roadmap's "fix it once and it fixes both".

`(StartTime, PlayingId, AgainstId, Result)` within one trainer. `StartTime`, never
`DatePlayed`, which a date picker leaves at midnight.

**It is deliberately not authoritative, and this is the load-bearing part.** `AgainstId`
identifies a *deck*, not a person — the model records no opponent identity at all — so two
different opponents on the same deck in the same minute produce an identical key, and mirror
matches make it likelier. A key hit is therefore grounds for skipping and reporting, never for
deleting or overwriting. Anything that "cleans up duplicates" automatically will eventually eat
a real match.

The index is updated as entries insert, because a single file is not guaranteed free of
duplicates either.

## Why the restore never merges today

Cases 2 (one side richer) and 3 (genuine conflict) are **structurally impossible for a backup
restore right now**, because matches are insert-only: `MatchOperations` has no update path and
no ViewModel has an edit command. A restore can therefore meet a match that is byte-identical
or one that does not exist yet, and nothing else. Divergence is a TrainerHill concern.

**This stops being true the moment match editing ships.** See the edit/delete item in
[[project_roadmap]] — whoever builds it must revisit this assumption rather than trusting the
"TrainerHill only" framing.

## Things the restore does on purpose

- **Refuses a newer envelope version outright** instead of applying what it understands. A
  half-restored backup is worse than none: the user believes their data is back.
- **Restores archetypes before matches**, so matches find real icons instead of creating
  `substitute.png` rows from bare names.
- **Leaves existing archetype rows alone** (`INSERT OR IGNORE`). A later scrape may have
  improved an icon, and an older backup should not downgrade it.
- **Re-links ownership only when that trainer exists here.** A custom archetype whose owner is
  absent keeps the row and loses the link, rather than inventing a trainer.
- **Collects per-entry problems in `Errors`** instead of throwing, so one bad entry cannot abort
  the rest.

## When picking up the UI

- Mirror the existing export buttons on OptionsPage.
- `RestoreResult` already carries inserted / skipped-identical / conflicts / errors — report
  them separately. Note the import status message previously reported the *error* count using
  the word "skipped", which reads as "already present": opposite meanings, reassuring word on
  the alarming case. That is fixed; do not reintroduce the pattern.
- No modals ([[project_error_handler_di]]).
- Integration tests before UI tests before XAML, per the user's standing order.

## Related

- [[feedback_mock_returns_null_not_empty]] — hit three times while building this
- [[project_roadmap]] — full design, conflict-resolution rules, edit/delete items
