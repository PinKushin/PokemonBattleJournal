---
name: project_error_handler_di
description: "IN PROGRESS (branch refactor/inject-error-handler) — ModalErrorHandler is new()'d at 54 sites and IErrorHandler is never DI-registered. Injecting it fixes a DI violation, 54 untestable catch paths, and modals firing during CI automation."
metadata:
  type: project
---

**Status: in progress, branch `refactor/inject-error-handler`, started 2026-08-05.**
First of three agreed follow-ups (order confirmed by user): **(1) inject IErrorHandler →
(2) consolidate test projects ([[project_test_project_consolidation]]) → (3) fresh coverage
report**.

## The finding

`ModalErrorHandler` is constructed inline at **54 call sites**:

```csharp
catch (Exception ex)
{
    ModalErrorHandler error = new();   // concrete type, constructed in a catch block
    _logger.LogError(ex, "…");
    error.HandleError(ex);             // → shell.DisplayAlertAsync("Error", …)
}
```

`IErrorHandler` exists (`Services/IErrorHandler.cs`) but is **never registered in DI** and
never injected anywhere. Three problems in one:

1. **Violates Dependency Inversion** — the project's own rule is "new dependencies go through
   DI, not `new ConcreteService()` inside classes" ([[feedback_engineering_principles]]).
2. **54 catch paths are untestable.** Nothing can substitute the handler, so no test can
   verify that errors are surfaced. A real, invisible hole behind the "coverage feels bad
   despite 500+ tests" instinct — see [[project_coverage_tooling]].
3. **Modals fire during automation.** `HandleError` shows a `DisplayAlertAsync`; any error
   during a UI test pops a dialog that steals the accessibility tree. The user has an
   explicit standing objection to modals for exactly this reason
   ([[feedback_no_silent_guards]], and the modal reasoning in [[project_roadmap]]).

**Correction to an earlier claim in this session:** `ModalErrorHandler` was NOT "neutered to
log-only". It still shows modals, so its name is accurate and no rename is needed. What
changed for the fresh-DB crash was one *call site* — `ArchetypeOperations.GetAllAsync` logs
instead of calling the handler ([[project_optionspage_crash_fresh_db]]).

## Where the call sites are

`ArchetypeOperations` (8), `MatchOperations` (8), `TagOperations` (7), `TrainerOperations`
(11), `MainPageViewModel` (4), `OptionsPageViewModel` (9), `ReadJournalPageViewModel` (2),
`TrainerPageViewModel` (1). Plus `TaskUtilities.FireAndForgetSafeAsync` already accepts an
optional `IErrorHandler` — the seam partly exists already.

## Construction path (this is what makes the refactor feasible)

```
MauiProgram  →  AddSingleton<ISqliteConnectionFactory>(sp => new SqliteConnectionFactory(logger, metaService))
SqliteConnectionFactory ctor  →  new TrainerOperations(this, logger)
                                 new MatchOperations(this, logger)
                                 new ArchetypeOperations(this, logger, metaService)
                                 new TagOperations(this, logger)
```

So the four operations services are **not** DI-constructed — the factory builds them. Add
`IErrorHandler` to the factory's constructor and pass it down; register
`AddSingleton<IErrorHandler, ModalErrorHandler>()` in `MauiProgram`. ViewModels take it as a
normal constructor parameter.

## Plan

1. Register `IErrorHandler` → `ModalErrorHandler` in `MauiProgram`.
2. Thread it through `SqliteConnectionFactory` into the four operations services.
3. Replace all 54 `new ModalErrorHandler()` with the injected instance.
4. Update test `SetUp` in both test projects (they construct VMs and a
   `TestSqliteConnectionFactory` directly) — this is the bulk of the mechanical work.
5. **Then** add tests asserting `HandleError` is invoked on failure paths, which is only
   possible once the seam exists.

**On TDD ordering:** steps 1-4 are a pure refactor with 494 existing tests as the safety net;
behaviour must not change. The new error-path tests in step 5 genuinely cannot be written
first, because the seam they need does not exist until the refactor lands. Do not skip step 5
— it is the entire point of the refactor.

## Deeper issue, deliberately NOT fixed here

A *data* service showing a UI modal is a layering violation: `TagOperations` should not know
the UI exists. The clean end state is services logging and returning/throwing, with
ViewModels deciding whether to surface anything. That is a redesign; injecting the interface
preserves current behaviour exactly while fixing DI, testability and the CI hazard. Revisit
once the inline validation label lands ([[project_roadmap]]), since that is the mechanism
that would replace most of these modals.

---

## What the refactor exposed — two follow-ups (found 2026-08-05)

Injecting the seam immediately revealed that the seam **cannot currently be tested**, for two
independent reasons. Both were invisible while the handler was `new`'d inline.

### 1. `GetDatabaseAsync()` sits OUTSIDE the try block — 20 call sites

Every operation follows this shape:

```csharp
public async Task<List<Tags>> GetAllAsync()
{
    SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();   // <-- OUTSIDE
    try { … }
    catch (Exception ex) { _logger.LogError(…); _errorHandler.HandleError(ex); }
}
```

**A database connection failure is therefore unhandled.** It escapes the method, the error
handler never fires, nothing is logged, and the app crashes rather than surfacing anything.
The `catch` only ever covers query failures. Counts: `TrainerOperations` 7, `MatchOperations`
5, `TagOperations` 4, `ArchetypeOperations` 4 — **20 total**.

Proven accidentally: a test pointing the factory at an invalid path threw
`SQLite.SQLiteException : Could not open database file … (CannotOpen)` straight out of the
method, past the catch.

Fixing this is a genuine behaviour change (connection failures would start being handled
rather than crashing) across 20 sites, so it wants its own branch and its own tests. It is
also the *prerequisite* for testing the error-handler seam on read paths.

### 2. Services depend on the concrete `SqliteConnectionFactory`, and `GetDatabaseAsync` is not virtual

Only `GetDbPath()` is `protected virtual` — which is why `TestSqliteConnectionFactory`
overrides that and nothing else. So a failure cannot be induced by substitution; NSubstitute
cannot stub `GetDatabaseAsync` on the concrete type. Any error-path test today must cause a
**real** SQLite failure, which makes it an integration test by definition.

The clean fix is for the operations services to depend on `ISqliteConnectionFactory` rather
than the concrete class — the same Dependency Inversion issue this branch is fixing for
`IErrorHandler`, one layer down.

### Consequence for testing (user caught this)

A first attempt at `ErrorHandlerInvocationTests` was written into
`PokemonBattleJournal.Tests` and **removed** — it induced a real SQLite failure, making it an
integration test in the unit project, i.e. exactly the mistake
[[project_test_project_consolidation]] exists to clean up. User flagged it: *"should that be
in unit tests or integration since its calling the real path."*

**Do not re-add error-path tests until #1 is fixed.** Until `GetDatabaseAsync` is inside the
try, there is no reachable failure mode on the read paths to assert against.
