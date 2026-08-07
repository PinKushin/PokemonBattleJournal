---
name: project_stryker_mutation_testing
description: "Stryker.NET pinned as a local tool. Scraper 100%. Core baseline 53.09% on 2026-08-07, the first time app logic was ever measured — 352 mutants sit in code NO test touches. Still cannot mutate the MAUI head."
metadata:
  type: project
---

**Added 2026-08-07.** `dotnet-stryker` 4.16.0, pinned in `.config/dotnet-tools.json`.

```bash
dotnet tool restore
dotnet stryker
```

## What it is for

Mutation testing changes production code and fails when the tests do **not** notice. It grades
the assertions, not the code. "506 unit tests pass" says they run; this says whether they would
catch a regression. It is the natural companion to the sabotage checks used by hand all through
2026-08-06 — break the thing on purpose, confirm a test goes red — done exhaustively.

## Result: 78.46% → 100% after triaging the survivors

First run: 51 killed, 14 survived. All 14 killed by 8 new tests; now 65 killed, 0 survived
(4 compile-errored and 9 ignored are Stryker's own, not gaps). `break` is now **90** — there is
a baseline to defend.

**What the 14 actually were**, because the shape is more useful than the number:

- **5 in `LimitlessDeckParser`** — every existing test row had exactly ONE image, so
  `imgs.Length > 1 ? imgs[1]... : null` was never true and the **dual-icon path was entirely
  untested**. That is a real shipped feature. The annotation branch was also unkillable for a
  different reason: with tidy markup it is a no-op, since rebuilding "base + annotation" returns
  the anchor's own text. It only earns its keep on irregular whitespace, which is exactly what a
  scraped page produces — so the test that kills it uses `"Dragapult    <span>ex</span>"`.
- **8 logging mutants** in `HttpMetaDeckFetcher` and `LimitlessMetaService` — every test used
  `NullLogger`, so deleting a `LogWarning` went unnoticed. These are not cosmetic: all four sites
  swallow a failure and return empty, so **the log is the only evidence the caller gets**, which
  is the standard in [[feedback_no_silent_guards]]. Tests now use `RecordingLogger`.
- **1 in `MetaServiceFactory`** — deleting the whole constructor body survived, because the only
  test asserted `Create()` returns the right TYPE. That passes with every field null, since
  construction never dereferences them; the failure would surface later as an NRE from inside a
  service the factory appeared to build correctly. Killed by exercising the built service.

**The pattern worth remembering:** none of these were "untested code". Every one had tests that
passed. They were tests that could not fail — asserting a type instead of behaviour, using a
null logger, or seeding data that never reaches the branch.

## Core baseline: 53.09% (2026-08-07, first measurement ever)

`dotnet stryker --config-file stryker-core.json` — both test projects, 727 tests, ~14 min on a
quiet machine.

```
1523 mutants created
 133 CompileError   (Stryker's own)
 258 Ignored        (block already covered)
 352 NoCoverage     <-- no test reaches these at all
 780 tested -> Killed 595 | Survived 179 | Timeout 6
```

**53.09% = 601 detected / 1132 real.** NoCoverage counts as undetected, which is correct.

**Read the two failure kinds separately — they need different fixes.** 352 NoCoverage is
*untested code*; 179 Survived is *covered code with tests that cannot fail*
([[feedback_tests_that_cannot_fail]]). Only the second kind is the classic assertion problem.

| Worst | score | surv | nocov |
|---|---|---|---|
| `Services/MatchDuplicateKey.cs` | 0.0% | 0 | 2 |
| `Restore/RestoreService.cs` | 29.2% | 2 | 117 |
| `Services/TrainerOperations.cs` | 29.5% | 13 | 85 |
| `Services/MatchOperations.cs` | 48.8% | 35 | 52 |
| `Services/ArchetypeOperations.cs` | 55.0% | 39 | 19 |

Already at 100%: `SqliteConnectionFactory`, `SpriteResolver`, `DbSession`,
`MatchResultsCalculatorFactory`, `TrainerHillModels`, `Archetype`. `Calculations` 93.3%,
`MatchAnalysisService` 90.1%.

**Two things checked rather than assumed.** `RestoreService` is NOT mocked in the integration
suite — `RestoreServiceIntegrationTests` constructs the real thing, so its 117 uncovered
mutants are genuinely unreached branches (error, conflict and version-refusal paths), not a
mocking artifact. And `MatchDuplicateKey` has **no test that names it at all**, which matters
because it is the dedupe key shared by import and restore.

**Timeouts inflate a score**, because Stryker counts a Timeout as killed. This run had 6, so the
number is trustworthy — but a run competing with another Stryker process is not. Measure on a
quiet machine before treating any score as a baseline.

`break` stays 0 for Core until the survivors are triaged; raising it before anyone has seen the
number teaches people to pass `--break-at` rather than fix tests.

## It CANNOT mutate the MAUI app project — do not retry blindly

`dotnet stryker --mutate "Utilities/**/*.cs"` against `PokemonBattleJournal.csproj` fails with:

```
Stryker.Abstractions.Exceptions.CompilationException: Internal error due to compile error.
```

**No CS error is surfaced, even at `--verbosity debug`.** The failure is inside Stryker's own
Roslyn recompilation, which rebuilds the project from source with mutations applied — and that
compilation does not reproduce MAUI's XAML codegen or the CommunityToolkit.Mvvm source
generators the app depends on for every `[ObservableProperty]` and `[RelayCommand]`.

Attempts that will not help: narrowing `--mutate`, setting `target-framework`, excluding
`*.g.cs`. The problem is the recompile, not the mutation scope.

**If app-code mutation is ever wanted**, the route is extracting the pure logic — `Utilities/`,
`Services/`, `MatchDuplicateKey`, the result calculators — into a plain `net10.0` class library
the MAUI head references. That is worth doing for its own sake and would make the most
test-worthy code mutable as a side effect. It is a real refactor, not a config change.

## Config choices

- **Scraper as the target**, because it is a plain library that compiles under Stryker and
  contains genuine parsing logic over untrusted HTML.
- **`break: 0`** deliberately. A gate that fails before anyone has seen a baseline teaches
  people to pass `--break-at`, not to fix tests. Raise it once the survivors are triaged.
- **Not in CI.** ~5 minutes for 78 mutants on one small library, and the value is in reading
  survivors rather than in a pass/fail. See [[feedback_dont_churn_stable_ci]].
- **Comments cannot live in the config.** Stryker validates keys strictly and rejects `"//"`
  entries outright, which is why the reasoning is here instead.

## Related

- [[feedback_test_the_hypothesis_first]] — the manual version of the same idea
- [[project_coverage_tooling]] — coverage says what ran; this says what was checked
