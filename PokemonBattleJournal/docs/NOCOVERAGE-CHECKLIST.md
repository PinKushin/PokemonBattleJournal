# NoCoverage checklist — PokemonBattleJournal.Core

Generated from the Stryker run of **2026-08-10 19:00** on `mutation-box`, commit `f21b64c`.
Score 61.58 %: Killed 823, Survived 113, Timeout 9, **NoCoverage 406**, CompileError 160,
Ignored 298.

`NoCoverage` means no test executed the line at all, so the mutant was never even run. It is
**in the score denominator** — `(823 + 9) / (823 + 9 + 113 + 406) = 61.58 %` — so every entry
here costs score directly, and clearing them is worth more than chasing the 113 survivors.

## Read this first: 406 is an upper bound, and the instrument is suspect

Coverage attribution is dropping the service a test actually acts on, in at least two independent
places, reproducibly. **Do not work this list until that is settled.**

**Instance 1 — `TagOperations`.** Three integration tests record no coverage of `TagOperations` at
all despite calling `_factory.Tags.DeleteAsync`: `DeleteAsync_ATagUsedByGames_RemovesEveryLinkToIt`,
`DeleteAsync_CountsTheLinksItRemovedAsWellAsTheTag` and `DeleteAsync_LeavesTheGamesAndOtherTagsAlone`.
They are exactly the three that first seed a match through `MatchOperations` via
`SaveMatchWithAsync`; they are credited with 64 mutants in `MatchOperations.cs` and **zero** in
`TagOperations.cs`. The fourth test in the fixture seeds nothing and covers `TagOperations`
lines 122-176 normally.

**Instance 2 — `RestoreService`.** `ApplyResolutionAsync_Keep_WritesNothing` is credited with
**one** line of `RestoreService.cs` (L32) — the service it exists to test. Its setup runs through
`ExportService`, `MatchOperations`, `TrainerOperations` and `ArchetypeOperations`, all of which it
is credited with. `if (resolution == ConflictResolution.Keep)` at L202 is marked NoCoverage while
a test is named after that exact branch.

The shared shape: **a test sets up through service A and acts on service B; A is credited, B is
not.** That inflates NoCoverage and, because NoCoverage sits in the score denominator, depresses
the score.

What is established:

- It reproduces across **both** box runs (07:00 on `1169b51`, 19:00 on `f21b64c`).
- The affected tests **pass on Windows** — `TagDeletionTests` is 7/7 locally.
- The files are byte-identical at the measured commit, so it is not a stale-checkout artefact.

What is **not** established: whether those tests fail on the box (Linux/ARM) and so never reach the
act, or whether the coverage is recorded and lost. The two differ enormously — the first means the
integration suite is quietly red on `mutation-box` and neither the Stryker summary nor
`~/cron-measure.log` says so.

**Resolve it by running those two fixtures on the box**, which the tf2 corpus run is currently
blocking. A count discrepancy is the thing to look for, per the house rule: a green summary line
with a smaller total than expected is the failure mode this project has already been bitten by.

Note the earlier reading that "attribution survives hand-offs, because tests are credited with up
to 7 service files" was wrong — it counted *distinct files*, which scores one incidental line the
same as full coverage of a method. Instance 2 was found by looking at line counts instead.

## Where the 406 are

| Cause | Count | What clears it |
|---|---:|---|
| Unreached `catch` block | 58 | Inject a throw of that exception type |
| Untaken `if` branch | 123 | Seed data that makes the condition true |
| Other unreached statements | 203 | Mostly bodies of the above, one level deeper |
| SQLite-net expression lambda | 22 | **Nothing — do not chase.** Unkillable by construction |
| **Total** | **406** | |

## 1. Unreached catch blocks — 58 mutants

The highest-value group, and the cheapest: `DatabaseConnectionFailureTests` already has the
pattern. Its cases make `BeginAsync` fail, which lands in the **generic** `catch (Exception)` —
that is why those are largely covered while the typed catches above them are not. A test needs
to throw the specific type from inside the `try`.

| Exception type | Mutants | Files |
|---|---:|---|
| `catch (ArgumentException)` | 24 | TrainerOperations.cs (9), ArchetypeOperations.cs (6), TagOperations.cs (6), MatchOperations.cs (3) |
| `catch (SQLiteException)` | 21 | TrainerOperations.cs (9), MatchOperations.cs (6), ArchetypeOperations.cs (3), TagOperations.cs (3) |
| `catch (Exception)` | 13 | RestoreService.cs (8), ArchetypeOperations.cs (3), TrainerHillImportService.cs (2) |

## 2. Untaken branches — 123 mutants

Every one is a condition whose *false* side a test takes and whose *true* side it never does.
Two distinct shapes, and only one is worth work:

- **Post-write verification** (`remainingCount > 0`, `remainingTags > 0`, `remainingTagGames != 0`)
  — these fire only if a `DELETE` inside a transaction silently did nothing. Reaching them needs
  fault injection at the SQLite layer, not a different fixture. Low value, high cost.
- **Ordinary data cases** (`tagGameCount > 0`, `game.Tags is null`, `entry.Game3 is not null`,
  `existingByKey.ContainsKey(key)`) — reachable by seeding different data. These are real gaps.

| Condition | Mutants | File | Method |
|---|---:|---|---|
| `if (remainingCount > 0)` | 4 | ArchetypeOperations.cs | `DeleteAsync` |
| `if (tag.Id == 0)` | 4 | MatchOperations.cs | `SaveGame` |
| `if (matchExists is null)` | 4 | MatchOperations.cs | `VerifyDataIntegrityAsync` |
| `if (gameExists is null)` | 4 | MatchOperations.cs | `VerifyDataIntegrityAsync` |
| `if (remainingCount > 0)` | 4 | TagOperations.cs | `DeleteAsync` |
| `if (trainerExists > 0)` | 4 | TrainerOperations.cs | `VerifyDeletionAsync` |
| `if (existingByKey.ContainsKey(key)` | 3 | TrainerHillImportService.cs | `ImportCoreAsync` |
| `if (gameResult is null)` | 3 | TrainerHillImportService.cs | `AddGameAsync` |
| `if (matchEntry.Id != 0)` | 3 | MatchOperations.cs | `SaveAsync` |
| `if (remainingTagGames != 0)` | 3 | MatchOperations.cs | `DeleteAsync` |
| `if (game != null)` | 3 | MatchOperations.cs | `LoadGameWithTagsAsync` |
| `if (missingTagIds.Count > 0)` | 3 | MatchOperations.cs | `PreValidateTagsAsync` |
| `if (backup is null)` | 3 | RestoreService.cs | `RestoreBackupCoreAsync` |
| `if (string.IsNullOrWhiteSpace(exported.Name)` | 3 | RestoreService.cs | `RestoreBackupCoreAsync` |
| `if (!Enum.TryParse(exportGame.Result, ignoreCase: true, out MatchResult ga)` | 3 | RestoreService.cs | `RestoreEntryAsync` |
| `if (incoming is null || existing is null)` | 3 | RestoreService.cs | `CompareGame` |
| `if (tagGameCount > 0)` | 3 | TagOperations.cs | `DeleteAsync` |
| `if (remainingRelationships > 0)` | 3 | TagOperations.cs | `DeleteAsync` |
| `if (remainingMatches > 0)` | 3 | TrainerOperations.cs | `VerifyDeletionAsync` |
| `if (remainingArchetypes > 0)` | 3 | TrainerOperations.cs | `VerifyDeletionAsync` |
| `if (remainingTags > 0)` | 3 | TrainerOperations.cs | `VerifyDeletionAsync` |
| `if (affected > 0)` | 2 | TrainerHillImportService.cs | `ImportCoreAsync` |
| `if (game.Tags.Count > MaxTagsPerGame)` | 2 | TrainerHillImportService.cs | `TryValidateEntry` |
| `if (game.Tags.Exists(t => t is not null && t.Length > MaxTagNameLength)` | 2 | TrainerHillImportService.cs | `TryValidateEntry` |
| `if (!exists)` | 2 | MatchOperations.cs | `GetByIdAsync` |
| `if (entries == null || entries.Count == 0)` | 2 | MatchOperations.cs | `GetByTrainerIdAsync` |
| `if (remainingGames != 0)` | 2 | MatchOperations.cs | `DeleteAsync` |
| `if (relationExists is null)` | 2 | MatchOperations.cs | `VerifyDataIntegrityAsync` |
| `if (resolution == ConflictResolution.Keep)` | 2 | RestoreService.cs | `ApplyResolutionAsync` |
| `if (stored is null)` | 2 | RestoreService.cs | `ApplyResolutionAsync` |
| `if (games.Count == 0)` | 2 | RestoreService.cs | `ApplyResolutionAsync` |
| `if (!Enum.TryParse(entry.Result, ignoreCase: true, out MatchResult result)` | 2 | RestoreService.cs | `RestoreEntryAsync` |
| `if (playingId == 0 || againstId == 0)` | 2 | RestoreService.cs | `RestoreEntryAsync` |
| `if (games.Count == 0)` | 2 | RestoreService.cs | `RestoreEntryAsync` |
| `if (affected <= 0)` | 2 | RestoreService.cs | `RestoreEntryAsync` |
| `if (!string.Equals(incomingNotes, existingNotes, StringComparison.Ordinal)` | 2 | RestoreService.cs | `CompareGame` |
| `if (!incomingTags.SetEquals(existingTags)` | 2 | RestoreService.cs | `CompareGame` |
| `if (match.Game1Id.HasValue)` | 2 | TrainerOperations.cs | `DeleteAsync` |
| `if (match.Game2Id.HasValue)` | 2 | TrainerOperations.cs | `DeleteAsync` |
| `if (match.Game3Id.HasValue)` | 2 | TrainerOperations.cs | `DeleteAsync` |
| `if (existingTrainer != null)` | 2 | TrainerOperations.cs | `SaveAsync` |
| `if (matchEntry != null && includeRelated)` | 1 | MatchOperations.cs | `GetByIdAsync` |
| `if (includeRelated)` | 1 | MatchOperations.cs | `GetByTrainerIdAsync` |
| `if (matchEntry.Game2Id.HasValue)` | 1 | MatchOperations.cs | `DeleteAsync` |
| `if (matchEntry.Game3Id.HasValue)` | 1 | MatchOperations.cs | `DeleteAsync` |
| `if (game.Id != 0)` | 1 | MatchOperations.cs | `SaveGame` |
| `if (game.Tags is not null && game.Tags.Count > 0)` | 1 | MatchOperations.cs | `VerifyDataIntegrityAsync` |
| `if (game is null)` | 1 | RestoreService.cs | `ApplyToSlotAsync` |
| `if (diff is null)` | 1 | RestoreService.cs | `ApplyToSlotAsync` |
| `if (tag is not null)` | 1 | RestoreService.cs | `ApplyToSlotAsync` |
| `if (backup.Archetypes.Count == 0)` | 1 | RestoreService.cs | `RestoreOutcome` |
| `if (existing is not null)` | 1 | RestoreService.cs | `ResolveTrainerAsync` |
| `if (exportGame is null)` | 1 | RestoreService.cs | `RestoreEntryAsync` |
| `if (tag is not null)` | 1 | RestoreService.cs | `RestoreEntryAsync` |
| `if (incoming is null && existing is null)` | 1 | RestoreService.cs | `CompareGame` |

## 3. Other unreached statements — 203 mutants

Bodies nested inside the constructs above, grouped by the method that owns them.
`RestoreService` dominates at 103 because it is the newest code and its conflict-resolution
paths are reached only by the resolution tests, not by the plain restore ones.

| File | Method | Mutants |
|---|---|---:|
| RestoreService.cs | `RestoreEntryAsync` | 20 |
| RestoreService.cs | `CompareGame` | 18 |
| MatchOperations.cs | `LoadRelatedDataAsync` | 14 |
| TrainerOperations.cs | `DeleteAsync` | 14 |
| RestoreService.cs | `RestoreBackupCoreAsync` | 12 |
| TrainerOperations.cs | `VerifyDeletionAsync` | 12 |
| TrainerHillImportService.cs | `ImportCoreAsync` | 11 |
| RestoreService.cs | `ClassifyAgainstExisting` | 11 |
| RestoreService.cs | `ApplyResolutionAsync` | 10 |
| RestoreService.cs | `ResolveArchetypeAsync` | 9 |
| RestoreService.cs | `BuildDiff` | 8 |
| RestoreService.cs | `ResolveTagAsync` | 7 |
| TrainerHillImportService.cs | `AddGameAsync` | 6 |
| MatchOperations.cs | `GetByTrainerIdAsync` | 6 |
| RestoreService.cs | `ApplyToSlotAsync` | 6 |
| TrainerOperations.cs | `SetActiveAsync` | 6 |
| MatchOperations.cs | `GetByIdAsync` | 5 |
| TrainerHillImportService.cs | `ResolveTagAsync` | 4 |
| ExportService.cs | `ExportBackupAsync` | 3 |
| ExportService.cs | `ToEntry` | 3 |
| TrainerHillImportService.cs | `ResolveArchetypeAsync` | 3 |
| ArchetypeOperations.cs | `GetAllAsync` | 2 |
| ExportService.cs | `ExportTrainerHillAsync` | 2 |
| ExportService.cs | `ToGame` | 2 |
| MatchAnalysisService.cs | `CalculateMatchupMatrix` | 2 |
| MatchOperations.cs | `LoadGameWithTagsAsync` | 2 |
| TrainerHillImportService.cs | `TryValidateEntry` | 1 |
| MatchAnalysisService.cs | `CalculateTagUsage` | 1 |
| RestoreService.cs | `RestoreBackupAsync` | 1 |
| RestoreService.cs | `RestoreOutcome` | 1 |
| TrainerOperations.cs | `SaveAsync` | 1 |

## 4. SQLite-net expression lambdas — 22 mutants. Do not chase.

`Table<T>().Where(i => i.Id == id)` compiles to an expression tree that SQLite-net translates to
SQL. Mutating the predicate produces a different tree that still translates, so no assertion can
distinguish them without asserting on generated SQL. Already known: 52 of 407 in the earlier
triage were this same class.

## Suggested order

1. **Settle the TagOperations anomaly on the box.** Everything else is measured with an
   instrument of unknown accuracy until this is answered.
2. **Typed catch blocks (45 of the 58).** One fixture in the shape of `DatabaseConnectionFailureTests`,
   throwing `ArgumentException` and `SQLiteException` from inside the `try`.
3. **Ordinary-data branches.** Cheapest real coverage per test written.
4. Leave post-write verification branches and the LINQ lambdas alone.

