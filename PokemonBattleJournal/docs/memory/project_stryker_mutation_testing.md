---
name: project_stryker_mutation_testing
description: "Stryker.NET pinned as a local tool. Scores the Scraper at 78.46% (51 killed / 14 survived). It CANNOT mutate the MAUI app project — Stryker's internal recompile fails on XAML codegen + MVVM source generators, with no CS error surfaced."
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

## First result: the Scraper scores 78.46%

51 killed, 14 survived, 4 compile-errored, 9 ignored. **The 14 survivors are the output** — each
is a change to `PokemonBattleJournal.Scraper` that no test detects. Read them in the HTML report
under `StrykerOutput/` (gitignored) before adding tests; some will be equivalent mutants that
cannot be killed, and those are not defects.

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
