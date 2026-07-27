using Microsoft.Extensions.Logging;
using PokemonBattleJournal.Scraper.Interfaces;
using PokemonBattleJournal.Services;

namespace PokemonBattleJournal.IntegrationTests.Infrastructure;

/// <summary>
/// Subclass that redirects the DB to an isolated temp file per test fixture.
/// Implements IAsyncDisposable so the temp file is cleaned up after the fixture is done.
/// </summary>
public sealed class TestSqliteConnectionFactory : SqliteConnectionFactory, IAsyncDisposable
{
    private readonly string _dbPath;

    public TestSqliteConnectionFactory(ILimitlessMetaService metaService)
        : base(NullLogger<SqliteConnectionFactory>.Instance, metaService)
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"pbjtest_{Guid.NewGuid():N}.db3");
    }

    protected override string GetDbPath() => _dbPath;

    public async ValueTask DisposeAsync()
    {
        // Give SQLite a moment to flush before deleting
        await Task.Delay(100);
        try { File.Delete(_dbPath); } catch { /* best effort */ }
    }
}
