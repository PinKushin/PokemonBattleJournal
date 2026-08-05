---
name: project_db_session_lock_pairing
description: All 22 DB operations open the connection via `using DbSession session = await _factory.BeginAsync()` inside the try. Never write a bare `finally { GetLock().Release(); }` — it throws SemaphoreFullException when the connection failed.
metadata:
  type: project
---

**Landed 2026-08-05 on `fix/db-connection-error-handling`.** Read this before editing
`TagOperations`, `TrainerOperations`, `MatchOperations`, `ArchetypeOperations` or
`TrainerHillImportService`.

## The shape every DB operation now uses

```csharp
try
{
    using DbSession session = await _factory.BeginAsync();
    SQLiteAsyncConnection db = session.Connection;
    …
}
catch (Exception ex)
{
    _logger.LogError(ex, "…");
    _errorHandler.HandleError(ex);
    return fallback;
}
// NO finally — the using releases the lock
```

`DbSession` and the `BeginAsync` extension live in `Services/DbSession.cs`.

## Three defects this fixed at once

**1. Connection failures were completely unhandled (20 of 22 sites).**
`await _factory.GetDatabaseAsync()` sat *above* the `try`, so a failure to open the database
escaped every catch: no log, no `IErrorHandler`, straight to a crash. The catch only ever
covered query failures. Opening the connection is the likeliest thing to fail on a real
device — corrupt or locked `.db3`, revoked storage permission, full disk — and it was the one
step with no handling at all.

**2. Moving it inside the try is NOT sufficient — this is the important part.**
`MatchOperations.SaveAsync` already had the call inside its try, and that site failed
*differently*:

```
System.Threading.SemaphoreFullException : Adding the specified count to the semaphore
would cause it to exceed its maximum count.
    at MatchOperations.SaveAsync(…) MatchOperations.cs:line 143
```

`finally { _ = _factory.GetLock().Release(); }` ran even though `WaitAsync` was never
reached. `SemaphoreSlim(1, 1)` is capped at one permit, so the release threw — masking the
real exception and replacing the method's return value. The naive fix would have reproduced
this at all 20 remaining sites. **Tying release to acquisition is the whole point of
`DbSession`; do not reintroduce a bare `finally`-release.**

**3. Services depended on the concrete `SqliteConnectionFactory`.** They take
`ISqliteConnectionFactory` now. `GetDatabaseAsync` is not virtual, so with the concrete type
the failure could not be substituted and none of this was testable.

## Ordering constraint — do not "simplify" BeginAsync

```csharp
SQLiteAsyncConnection connection = await factory.GetDatabaseAsync();  // opens (takes+releases the gate internally)
SemaphoreSlim gate = factory.GetLock();
await gate.WaitAsync();                                               // then locks
```

Open first, then lock. `SqliteConnectionFactory.InitAsync` acquires **the same semaphore**
while creating tables, so taking the lock first deadlocks on the first call of the process.

`BeginAsync` is an extension method, not an interface member, deliberately: it keeps
`GetDatabaseAsync` as the single seam, so making that one method fail exercises every
connection-failure path in the app.

## Deliberate behaviour changes

- **The lock is released before the catch body runs**, where it used to be released after.
  `IErrorHandler` is `ModalErrorHandler` in production and really does show a dialog
  ([[project_error_handler_di]]); holding the DB semaphore across a modal was a deadlock
  waiting to happen.
- **`TrainerHillImportService`'s two sites still propagate.** They have no catch-all on
  purpose — the importer collects per-entry errors and needs the exception to reach it. They
  gained only the corrected release, and `BeginAsync` sits outside the try there because
  `ResolveTagAsync`'s UNIQUE-race catch needs the connection in scope.

## Tests

`PokemonBattleJournal.Tests/Services/DatabaseConnectionFailureTests.cs` — 20 cases, one per
operation. **Unit** tests, not integration: the failure is injected at the
`ISqliteConnectionFactory` seam via `Task.FromException`, so no SQLite file is touched (this
resolves the placement question the user raised, see [[project_test_project_consolidation]]).

Each case asserts four things: the call does not throw, it logs at Error with the exception,
it reports through `IErrorHandler` — **or, for the four AppearingAsync paths, that it does
NOT** (`TagOperations.GetAllAsync`, `ArchetypeOperations.GetAllAsync`,
`TrainerOperations.GetActiveAsync`, `TrainerOperations.SetActiveAsync` are log-only because a
ContentDialog raised before the page's XamlRoot is composed crashes WinUI,
[[project_optionspage_crash_fresh_db]]) — and that the semaphore is left at `CurrentCount ==
1`. That last assertion is what pins defect 2; keep it on any new case.

All 20 were confirmed red before the fix, and the two failure modes were distinguishable in
the output, which is what proved the diagnosis rather than just the symptom
([[feedback_test_the_hypothesis_first]]).

## Related

- [[project_error_handler_di]] — the refactor that exposed defects 1 and 2
- [[project_integration_test_isolation]] — real-SQLite factory subclassing, still valid for integration tests
- [[feedback_engineering_principles]] — the Dependency Inversion rule defect 3 violated
