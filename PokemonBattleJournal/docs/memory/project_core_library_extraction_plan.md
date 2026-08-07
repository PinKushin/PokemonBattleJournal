---
name: project_core_library_extraction_plan
description: "PLAN, not started. Move 33 of 36 Models/Services/Utilities files into a plain class library so Stryker can mutation-test them — today it cannot touch the MAUI project at all. Measured 2026-08-07: only 3 files are genuinely MAUI-coupled."
metadata:
  type: project
---

**Status: planned, not started.** Written 2026-08-07 while the measurements were fresh.

## Why

Stryker cannot mutation-test the MAUI app project at all — its internal recompile chokes on
XAML codegen and the MVVM source generators ([[project_stryker_mutation_testing]]). So the
"tests that cannot fail" problem ([[feedback_tests_that_cannot_fail]]) is **invisible in
ViewModels and Services**, which hold nearly all the branching logic here. The one place it
could be measured — the Scraper — turned up 14 such tests immediately.

A plain class library is an ordinary project. Stryker handles it fine, as the Scraper proves.

Secondary benefit: the library physically cannot reference `FileSystem`, `Shell.Current` or a
`Page`, so the separation stops depending on discipline.

**This is not for reuse.** Nobody will consume it as a package, and that is fine — it is not
the reason.

## What was measured (2026-08-07)

36 files across `Models/`, `Services/`, `Utilities/`. **Only 3 genuinely depend on MAUI:**

| File | What blocks it |
|---|---|
| `Services/ModalErrorHandler.cs` | `Shell.Current`, `Application.Current` |
| `Utilities/FileHelper.cs` | `FileSystem` |
| `Utilities/MainThreadHelper.cs` | `MainThread` |

Everything else — all Models, every `*Operations` class, `SqliteConnectionFactory`,
`MatchAnalysisService`, `TrainerHillImportService`, `ExportService`, `RestoreService`,
`Calculations`, `MatchDuplicateKey`, the result calculators — depends on **SQLite-net**, which
is a normal NuGet package that works in a class library. An earlier grep lumped SQLite in with
MAUI and made this look far worse than it is.

## Plan

**One library, not several.** `PokemonBattleJournal.Core`, `net10.0`. Splitting into Core/Data
invents a boundary nothing has needed yet.

1. Create `PokemonBattleJournal.Core` (net10.0). Add the SQLite-net and
   `Microsoft.Extensions.Logging.Abstractions` package references it needs. Add to the `.slnx`.
2. Move the 33 files. **Keep namespaces identical** (`PokemonBattleJournal.Models`, `.Services`,
   `.Utilities`) so no `using` anywhere has to change — this is what keeps it a pure move.
3. The 3 blocked files stay in the MAUI project. `IErrorHandler` is already an interface in
   `Services/` — the interface moves, `ModalErrorHandler` stays. That boundary already works
   because of the DI refactor ([[project_error_handler_di]]).
4. App project references Core. Move the matching `global using` lines into Core's own
   `GlobalUsings.cs`; the app keeps its own.
5. Test projects reference Core (they reference the app project today; several may end up
   needing both, since ViewModel tests stay pointed at the app).
6. `MauiProgram` registrations are unchanged in content — only the assembly the types come from
   changes.

## Verify

In this order, and **one commit for the whole move with no behaviour change**:

- `dotnet build PokemonBattleJournal.slnx` — zero warnings
- unit + integration (514 / 197 at time of writing)
- Windows UI 101 at CI geometry, Android UI 82
- **then point Stryker at Core and record the first score** — that is the whole objective

## Risks

- **Touches everything at once.** Highest-risk shape of change. Checkpoint-commit; do not
  interleave it with a feature.
- **`InternalsVisibleTo`** is set on the app project for both test projects. Anything `internal`
  that moves — `MatchDuplicateKey`, `DescribeRestore`, `ApplyRestoreAsync` — needs the same
  attribute on Core or those tests stop compiling.
- **Circular reference trap.** If anything in the moved set reaches back into the app, it will
  surface here. The 3-file list says nothing does, but the compiler is the real check.
- **Not a rewrite.** No renaming, no reshaping, no splitting types while moving them. Anything
  that is not a file move belongs in a separate commit.

## Explicitly NOT doing

Separate domain entities plus mapping. The models carry SQLite attributes, so they are
persistence models rather than pure domain objects — a purist would split them. That solves
swapping the database, a schema that disagrees with the object shape, or several consumers with
different storage. **None of those exist here**, and it would double the type count and add
mapping code to buy nothing. Agreed with the user 2026-08-07.
