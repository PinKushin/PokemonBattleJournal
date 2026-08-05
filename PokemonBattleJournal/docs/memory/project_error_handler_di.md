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
