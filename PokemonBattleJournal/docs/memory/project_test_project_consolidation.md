---
name: project_test_project_consolidation
description: "DONE 2026-08-05 — six real-SQLite files moved into the integration project. Unit suite 494 tests/17s -> 418/0.9s; integration 115 -> 180. All 11 name collisions verified semantically identical before removal."
metadata:
  type: project
---

**Status: COMPLETE 2026-08-05** (`7befb3f`, branch `refactor/consolidate-integration-tests`).

## Outcome

| | before | after |
|---|---|---|
| unit (`.Tests`) | 494 tests, **17s** | 418 tests, **0.9s** |
| integration | 115 tests, 13s | 180 tests, 26s |

The 11-test drop in the total is exactly the duplicates removed. The unit suite going
sub-second is the real win — fast feedback no longer pays for database I/O.

**The 11 collisions were all semantically identical**, verified body-to-body before anything
was deleted: same assertion reached through a different fixture (`_sut.GetByIdAsync(99999)`
vs `_factory.Tags.GetByIdAsync(99999)`, both `ShouldBeNull`). Note the trap in both
directions — a name-only comparison would have called them duplicates without proof, and a
text-only comparison would have called them all unique and left 11 tests running twice.

**Files moved wholesale, not rewritten.** Each carries its own private
`TestSqliteConnectionFactory` and the class names already differed
(`XxxIntegrationTests` vs `XxxTests`), so no type collisions and no need to touch 54 working
tests.

**The non-obvious break:** `LimitlessDeckParserIntegrationTests` loads its HTML snapshot via
`GetManifestResourceNames()`. Moving the `.cs` without the `<EmbeddedResource>` declaration
broke all 9 of its tests with `TypeInitializationException: Sequence contains no matching
element`. **If you ever move a test file again, check for embedded resources and fixture
files it depends on** — the compiler cannot warn you, and the failure names the type
initializer rather than the missing resource.

`GlobalUsings.cs` in the integration project gained `NSubstitute`, `SQLite`,
`Microsoft.Extensions.Logging(.Abstractions)` and the `PokemonBattleJournal.*` namespaces,
because the moved files had relied on the unit project's globals. Per-file usings that became
redundant were stripped from 11 files; namespaces used in only one or two files were left
local deliberately.

---

## Original analysis (kept — it is what made the merge safe)

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
