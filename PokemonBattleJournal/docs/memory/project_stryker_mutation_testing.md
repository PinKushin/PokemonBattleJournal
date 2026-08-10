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

## Operational rules learned the hard way (2026-08-07)

- **Never run `dotnet test` on these projects while Stryker is running.** Stryker builds the
  same projects into the same `obj/` and `bin/`, and the file locks fail the build outright —
  producing NEITHER a pass line nor a fail line. That empty output looks like starvation and is
  not; CPU was at 80%, which is not starvation. The fix is sequencing builds, not waiting for
  headroom.
- **Timeouts count as KILLED, so contention inflates the score.** 6 timeouts on a quiet machine,
  9 with another agent active, 14 with a competing test run. Always report the timeout count
  next to the score, and bound the number: if every timeout were really a survivor, subtract
  them from the killed side. A run at 56.26% with 14 timeouts is 54.96%-56.26%.
- **Re-measure before targeting.** A report goes stale the moment tests are added. Targeting an
  old report means writing tests for code that is already reached — check the date before
  reading the gaps.
- **Read the mutant statuses around a line before believing "NoCoverage".** A guard whose
  CONDITION is Killed while its BODY is NoCoverage is not untested code; it is a branch only
  ever taken one way. Those need a test for the other leg, not a new test file.

## Code inside RunInTransactionAsync is invisible to coverage — this caps DB-heavy files

**The most important limit, found 2026-08-07 by measuring rather than assuming.** Adding six
integration tests that fully exercise `TrainerOperations.DeleteAsync`, and five that exercise
`MatchOperations.GetByTrainerIdAsync`, moved those files' numbers by EXACTLY ZERO — 85 and 78
NoCoverage before and after, identical scores. Meanwhile `ArchetypeOperations` (55.0 -> 60.5)
and `RestoreService` (23.8 -> 27.7) moved from the same kind of tests in the same run.

The difference is where the code executes. Both stuck files do their work inside
`await db.RunInTransactionAsync(tran => { ... })`, and SQLite-net dispatches that callback to a
pooled thread. Stryker's per-test coverage context does not follow it, so the body runs and is
never attributed. The files that moved do their work on the calling thread before any async
hop.

**The tests are good; the metric cannot see them.** Proof: deleting
`.Where(m => m.TrainerId == trainer.Id)` — so DeleteAsync wipes every trainer's data — fails
those tests. They catch a genuine data-loss bug in code Stryker calls uncovered.

**So do not chase NoCoverage in transaction-heavy services.** More tests will not move it. Judge
that code by sabotage instead, and treat its NoCoverage as an instrument limit rather than a
gap. The two artifacts together — transaction lambdas and expression trees — mean a DB-heavy
file cannot score much above the 30-50% it sits at now no matter how well tested it is.

## SQLite-net expression trees are structurally unkillable

`db.Table<T>().Where(x => x.Id == id)` takes an `Expression<Func<T,bool>>` that is translated to
SQL and never invoked, so Stryker's coverage markers inside the lambda never fire and the
mutants are reported NoCoverage and never tested.

**Measured rather than assumed: 52 of 407, about 13%.** An earlier guess that this explained
most of the NoCoverage was wrong — the other 355 are real gaps. It does put a ceiling near 96%
on any achievable score, which is worth knowing and not worth chasing.

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

## It runs on the Oracle `mutation-box` now (2026-08-10) — and ARM64 gives the same answer

```bash
ssh mutation-box
cd ~/pbj && bash build/run-measurements.sh stryker-core
```

`--solution DO-NOT-OPEN-IN-VS.LinuxMeasurementBox.slnx` is **required**, not optional. Stryker
builds the containing solution before mutating, and `PokemonBattleJournal.slnx` cannot build on
Linux at all: `UITests.Windows` needs `Microsoft.WindowsDesktop.App` (NETSDK1073) and the app
head's `net10.0-android` TFM needs the Android SDK (XA5300). The runner script passes it.

**First box run, commit `b5562ba`, 3 OCPU Ampere:**

```
1351 real mutants:  943 tested + 408 NoCoverage
Killed 784 | Survived 151 | Timeout 8
final mutation score 58.62 %      04:44:09 -> 05:22:32, 38m23s
```

**58.03%-58.62%** once the 8 timeouts are bounded the usual way (assume every one was really a
survivor: 784/1351). The local x64 figure was 57.96%, so the floor of the ARM range sits just
above it — **architecture does not move the score**, which is what should happen for plain
`net10.0` projects and is worth having confirmed rather than assumed.

**Concurrency was left at the default on purpose.** Stryker defaults to half the cores, so a
3-OCPU box runs effectively one test host — load average sat at 1.22 for the whole run. Raising
it to 3 is tempting and is the wrong move for the same reason contention is dangerous locally:
timeouts count as KILLED, so three test hosts competing for three cores would inflate the score
rather than speed up an honest one. 38 minutes for a signal that arrives after the fact is not a
problem worth trading accuracy for.

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
