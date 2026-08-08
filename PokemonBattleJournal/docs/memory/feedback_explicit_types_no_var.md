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

## Status: enforced as a warning, cleaned opportunistically

`.editorconfig` sets all three `csharp_style_var_*` rules to `false:warning`, and
`Directory.Build.props` sets `EnforceCodeStyleInBuild` — without that, IDE rules are
editor-only and an `.editorconfig` severity does nothing to the build.

**758 warnings as of 2026-08-07** (Tests 390, IntegrationTests 216, app 128, Core 24,
Scraper 0). The user accepted this explicitly as a temporary exception to Zero Warnings.

**The rule is: if you touch a file, you clean that file.** Not the project, not the solution.
Do not suppress it — the warning is the worklist, and the count only goes down. Test projects
are not exempt: *"in test projects its more forgivable, but still we should keep style
consistent across the project period."*

A new warning of any OTHER rule is still a defect.

## Why it is worth honouring rather than arguing

The reason given is readability, and it is the reader's call. It also happens to pair with two
standards already in force here: `Nullable` is `enable`, so a declaration's nullability is part
of the type and worth seeing; and `ImplicitUsings` is disabled precisely so that what a file
depends on is greppable rather than inferred. `var` is the same class of invisibility one level
down — see [[feedback_engineering_principles]].
