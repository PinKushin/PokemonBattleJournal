---
name: feedback_explicit_types_no_var
description: "Write the type at the START of a declaration, never var. The user's own convention from before any AI touched this repo. SWEEP COMPLETE 2026-08-09 — 331 to 0, the accepted-exception period is over, a new IDE0008 is now an ordinary defect."
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 4938edd8-4dd8-4200-98f4-755f11ee9fd8
  modified: 2026-08-10T00:16:05.585Z
---

**The user, 2026-08-07:** *"i prefer types to be explicit at the begining of a line, not after a var,
i hate var, types before a variable are easier to read imo and easier to parse, types should
alway be apparent as early as possible basically."*

Stated as a long-standing personal convention that predates any AI work in this repo — not a new
rule, a restored one.

## What to write

```csharp
// Yes — the type is the first thing on the line.
MainPageViewModel viewModel = new(logger, factory, ...);
List<MatchEntry> matches = await _factory.Matches.GetByTrainerIdAsync(id);
Dictionary<MatchDuplicateKey, MatchEntry> index = MatchDuplicateKey.Index(existing);

// No.
var viewModel = new MainPageViewModel(logger, factory, ...);
var matches = await _factory.Matches.GetByTrainerIdAsync(id);
```

Target-typed `new()` on the right is fine and preferred — it keeps the type leading without
repeating it. The objection is to `var`, not to brevity.

## Where it genuinely cannot be written

Anonymous types and some tuple deconstructions have no nameable type:

```csharp
var (played, opponents, cells) = _service.CalculateMatchupMatrix(matches);
```

Those stay. Everything else gets a type.

## Status: DONE. Swept 2026-08-09, 331 to 0

`.editorconfig` sets all three `csharp_style_var_*` rules to `false:warning`, and
`Directory.Build.props` sets `EnforceCodeStyleInBuild` — without that, IDE rules are editor-only
and an `.editorconfig` severity does nothing to the build.

**The accepted-exception period is over.** The build is back at zero warnings, so there is no
IDE0008 baseline to hide in and no annotation filter to apply when reading CI. A new IDE0008 is
now an ordinary defect, fixed before the change lands.

## How the sweep was actually done, if one is ever needed again

Not by hand and not by regex — the replacement needs Roslyn's inferred type.

```bash
dotnet format style <project.csproj> --diagnostics IDE0008 --severity warn
```

Four things that cost time and would cost it again:

1. **`style`, not `analyzers`.** `dotnet format analyzers` handles third-party analyzers and
   silently does nothing for IDE-prefixed rules — it reports success having changed no files.
2. **It reorders using directives** even when scoped to a single diagnostic. Several files here
   have comments explaining the grouping, and sorting moves usings across the comment they belong
   to. Check `git diff` for using-line changes and restore those blocks.
3. **But keep the usings it ADDS.** Replacing `var` with `Process` or `ObservableCollection<T>`
   genuinely needs a new using. Blindly restoring the original block breaks the build — it did,
   in `MatchAnalysisTests.cs`.
4. **Run it more than once.** One pass took 331 to 106; a second took it to 10. The last 10 were
   ordinary cases the fixer simply skipped and were done by hand.

## Two defects the sweep surfaced, both pre-existing on master

A full rebuild is what found them, and that is the transferable lesson:
**incremental builds do not re-emit warnings for unchanged projects**, so a warning introduced in
an earlier session stays invisible until something forces a rebuild. Use `--no-incremental` when
checking Zero Warnings.

- **CS8604** in `RestoreService.RestoreBackupAsync` — `json?.Length` put flow analysis into
  "may be null" for the rest of the method, then the value was passed to a non-nullable
  parameter. Fixed with `json ?? string.Empty` at the call site.
- **CA5394** in `TagDeletionTests` — `Random.Shared` seeding match StartTimes. Replaced with a
  counter, and the analyser warning was the *lesser* reason: random input makes a failure
  irreproducible from the test alone, and it can collide, which matters because
  `MatchDuplicateKey` keys on StartTime. Two seeded matches on the same second would be treated
  as duplicates and quietly change what the test measures.

## Why it is worth honouring rather than arguing

The reason given is readability, and it is the reader's call. It also pairs with two standards
already in force here: `Nullable` is `enable`, so a declaration's nullability is part of the type
and worth seeing; and `ImplicitUsings` is disabled precisely so that what a file depends on is
greppable rather than inferred. `var` is the same class of invisibility one level down — see
[[feedback_engineering_principles]].
