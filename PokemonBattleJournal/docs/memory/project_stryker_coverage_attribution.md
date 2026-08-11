---
name: project-stryker-coverage-attribution
description: Stryker credits the setup service but not the service a test acts on — NoCoverage is inflated and the mutation score understated; unresolved as of 2026-08-11
metadata: 
  node_type: memory
  type: project
  originSessionId: 4938edd8-4dd8-4200-98f4-755f11ee9fd8
  modified: 2026-08-11T21:48:49.850Z
---

Stryker's `CoverageBasedTest` attribution is dropping the service a test **acts on** while
crediting the services it **sets up through**. Found 2026-08-11 while extracting the NoCoverage
checklist from the 2026-08-10 19:00 `mutation-box` run (`f21b64c`, 61.58 %).

Two independent instances, both reproducing across **both** box runs (`1169b51` at 07:00 and
`f21b64c` at 19:00):

- **`TagOperations`** — `DeleteAsync_ATagUsedByGames_RemovesEveryLinkToIt`,
  `DeleteAsync_CountsTheLinksItRemovedAsWellAsTheTag` and
  `DeleteAsync_LeavesTheGamesAndOtherTagsAlone` each call `_factory.Tags.DeleteAsync`. All three
  are credited with 64 mutants in `MatchOperations.cs` and **zero** in `TagOperations.cs`. They
  are exactly the three that seed a match first via `SaveMatchWithAsync`. The fourth test in the
  fixture seeds nothing and covers `TagOperations` 122-176 normally.
- **`RestoreService`** — `ApplyResolutionAsync_Keep_WritesNothing` is credited with **one** line
  of `RestoreService.cs` (L32), the service it exists to test, while
  `if (resolution == ConflictResolution.Keep)` at L202 sits in the NoCoverage pile.

**Why it matters beyond a wrong label:** NoCoverage is in the score denominator —
`(823 + 9) / (823 + 9 + 113 + 406) = 61.58 %` — so every mis-attributed mutant depresses the
score directly. The 406 is an upper bound on real gaps, not a to-do list. See
`PokemonBattleJournal/docs/NOCOVERAGE-CHECKLIST.md` for the triaged list and the categories.

**Still unresolved, and it is the fork that matters:** either those tests **fail on the box**
(Linux/ARM) so the act never runs, or the coverage is recorded and lost. The first would mean the
integration suite is quietly red on `mutation-box` with neither the Stryker summary nor
`~/cron-measure.log` saying so — the same family as [[project_dotnet_test_filter_exits_zero]].
Both fixtures pass on Windows (`TagDeletionTests` 7/7) and are byte-identical at the measured
commit, so it is neither a stale checkout nor an edited test.

**How to settle it:** run `TagDeletionTests` and the `ApplyResolutionAsync` fixture *on the box*
and compare the totals against the local run. Blocked while tf2's corpus run holds
`/tmp/measurement-box.lock` — see [[project_measurement_boxes_reminder]].

**Method note worth keeping.** The first pass concluded attribution was fine because tests were
credited with up to 7 distinct service files. That counted *files*, which scores one incidental
line the same as full coverage of a method — and it hid instance 2 completely. Counting **lines
per file** found it. Relates to [[feedback_tests_that_cannot_fail]]: a measurement can be
insensitive to the thing it is supposed to detect, and the summary number looks fine either way.
