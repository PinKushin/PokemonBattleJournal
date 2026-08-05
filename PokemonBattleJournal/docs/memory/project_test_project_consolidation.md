---
name: project_test_project_consolidation
description: "PLANNED — six *IntegrationTests.cs files live in the unit test project and hit real SQLite. Consolidating is a MERGE job, not a delete job: ~11 name collisions across 65 tests, the rest is unique coverage."
metadata:
  type: project
---

**Status: planned, not started.** Second of three agreed follow-ups (order confirmed by user):
(1) [[project_error_handler_di]] → **(2) this** → (3) fresh coverage report.

## The problem

`PokemonBattleJournal.Tests` — documented and run as the fast unit-test project — contains
six files that are genuine integration tests hitting real SQLite with a temp `.db3` per test:

```
PokemonBattleJournal.Tests/Services/
  ArchetypeOperationsIntegrationTests.cs        (20 test methods)
  MatchOperationsIntegrationTests.cs            (17)
  TrainerOperationsIntegrationTests.cs          (15)
  TagOperationsIntegrationTests.cs              (13)
  TrainerHillImportServiceIntegrationTests.cs   (14)
  LimitlessDeckParserIntegrationTests.cs        (9)
```

Meanwhile a separate `PokemonBattleJournal.IntegrationTests` project exists with its own
`Services/` copies. Integration coverage is therefore split across two projects, and the
"Unit tests only" comment in the docs was wrong (fixed 2026-08-05).

## It is a MERGE, not a delete — measured 2026-08-05

The user's guess was that these are duplicates from a coverage push. **Partly true, but most
are unique.** Comparing method names between the overlapping pairs:

| Subject | in `.Tests` | in `.IntegrationTests` | identical names |
|---|---|---|---|
| Tag | 13 | 11 | 3 |
| Trainer | 15 | 19 | 4 |
| Archetype | 20 | 18 | 5 (incl. `SetUp`/`TearDown`) |
| Match | 17 | 23 | 1 |

**~11 genuinely duplicated test methods out of 65.** The overlap is concentrated in the cheap
argument-validation cases — `DeleteAsync_ZeroId_ThrowsArgumentException`,
`GetByIdAsync_NonExistentId_ReturnsNull`, `DeleteAsync_NullTrainer_ThrowsArgumentNullException`,
`SaveAsync_ZeroTrainerId_ThrowsArgumentException`, `SaveAsync_DuplicateName_ReturnsZero`,
`GetAllAsync_AfterSave_ReturnsTrainer` — exactly the kind that get written twice without anyone
noticing.

`LimitlessDeckParserIntegrationTests` and `TrainerHillImportServiceIntegrationTests` have **no
counterpart** and move cleanly. `MatchAnalysisTests`, `TrainerSwitchServiceTests` and
`LimitlessLiveWebTests` exist only in `.IntegrationTests`.

**Moving the six files wholesale will collide on ~11 names; deleting the "duplicates" blindly
will lose real tests.** Compare bodies, not just names, before removing anything — same name
does not guarantee same assertions.

## Why it matters beyond tidiness

- `dotnet test PokemonBattleJournal.Tests` is the fast feedback loop; real-SQLite tests make
  it slower than it needs to be and blur what a failure means.
- CI runs both projects, so the duplicated ~11 run twice for no benefit.
- The two projects use different fixtures (`TestSqliteConnectionFactory` +
  `NullMetaService` live only in `.IntegrationTests`), so moved tests will need their setup
  reconciled rather than copied.

## Related

- [[feedback_integration_tests_project]] — IntegrationTests must be updated alongside .Tests on any API change
- [[project_integration_test_isolation]] — the GUID-temp-file isolation pattern both sets use
- [[project_coverage_tooling]] — the coverage question that follows this work
