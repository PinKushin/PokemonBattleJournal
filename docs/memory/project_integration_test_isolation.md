---
name: project_integration_test_isolation
description: "SQLite integration test isolation — unique temp file per test, not :memory:, close before delete"
metadata: 
  node_type: memory
  type: project
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-07-29T18:56:22.429Z
---

SQLite integration tests must use a **unique temp file path per test instance**, not `:memory:`. Reason: `Constants.Flags` includes `SQLiteOpenFlags.SharedCache`, which makes `:memory:` a shared in-process database — UNIQUE constraints (Trainer.Name) cause cross-test interference.

Pattern for test factory:
```csharp
private sealed class TestSqliteConnectionFactory : SqliteConnectionFactory
{
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(), $"pbj_test_{Guid.NewGuid():N}.db3");

    public TestSqliteConnectionFactory()
        : base(Substitute.For<ILogger<SqliteConnectionFactory>>(),
               Substitute.For<ILimitlessMetaService>()) { }

    protected override string GetDbPath() => _dbPath;

    public async Task CloseAndDeleteAsync()
    {
        SQLiteAsyncConnection db = await GetDatabaseAsync();
        await db.CloseAsync();           // must close BEFORE File.Delete
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }
}
```

Use `IAsyncLifetime` for async setup/teardown:
- `InitializeAsync`: create factory, create SUT, call `GetDatabaseAsync()` to trigger table creation
- `DisposeAsync`: call `CloseAndDeleteAsync()`

**ArchetypeOperations extra:** `GetAllAsync` calls `_metaService.GetTopDecksAsync()` first. The NSubstitute default returns `null` → NullReferenceException caught → returns empty list. Must configure:
```csharp
metaService.GetTopDecksAsync(Arg.Any<int>())
    .Returns(Task.FromResult(new List<MetaDeck>()));
```

**Tags model:** text property is `Name`, not `TagTxt`.

**Why:** SharedCache makes `:memory:` shared; connection must be closed to release file lock before deletion.
**How to apply:** Every new integration test file must use this pattern. Never use `:memory:` with this codebase.
