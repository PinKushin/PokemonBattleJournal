---
name: feedback_explicit_types_no_var
description: "Write the type at the START of a declaration, never var. The user's own convention from before any AI touched this repo — types should be apparent as early as possible when reading a line."
metadata:
  type: feedback
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

## Status

**Not clean yet.** ~330 `var` occurrences across the repo as of 2026-08-07, nearly all in test
projects and predating this instruction. New and edited code follows the rule immediately; the
sweep is its own task rather than something to bury inside an unrelated change.

## Why it is worth honouring rather than arguing

The reason given is readability, and it is the reader's call. It also happens to pair with two
standards already in force here: `Nullable` is `enable`, so a declaration's nullability is part
of the type and worth seeing; and `ImplicitUsings` is disabled precisely so that what a file
depends on is greppable rather than inferred. `var` is the same class of invisibility one level
down — see [[feedback_engineering_principles]].
