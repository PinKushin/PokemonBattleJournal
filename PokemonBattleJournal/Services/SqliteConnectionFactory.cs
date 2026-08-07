using PokemonBattleJournal.Scraper.Interfaces;

namespace PokemonBattleJournal.Services;

/// <summary>
/// Provides methods for interacting with the SQLite database.
/// </summary>
public class SqliteConnectionFactory : ISqliteConnectionFactory
{
    private SQLiteAsyncConnection? _database;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public SqliteConnectionFactory(ILogger logger, ILimitlessMetaService metaService, IErrorHandler errorHandler)
    {
        Trainers = new TrainerOperations(this, logger, errorHandler);
        Matches = new MatchOperations(this, logger, errorHandler);
        Archetypes = new ArchetypeOperations(this, logger, metaService, errorHandler);
        Tags = new TagOperations(this, logger, errorHandler);
    }

    public ITrainerOperations Trainers { get; }
    public IMatchOperations Matches { get; }

    public IArchetypeOperations Archetypes { get; }

    public ITagOperations Tags { get; }

    public async Task<SQLiteAsyncConnection> GetDatabaseAsync()
    {
        await InitAsync();
        return _database!;
    }

    public SemaphoreSlim GetLock()
    {
        return _semaphore;
    }

    protected virtual string GetDbPath() => Constants.DatabasePath;

    private async Task InitAsync()
    {
        if (_database is not null)
        {
            return;
        }

        try
        {
            await _semaphore.WaitAsync();

            // Double-checked locking, and the second check is load-bearing. This type is a DI
            // singleton, so several pages can be inside InitAsync at once: another caller can
            // pass the check above, win the semaphore, and finish table creation while this one
            // is still suspended on WaitAsync. Without this it would build a second connection
            // and re-run every CreateTableAsync.
            //
            // CodeQL flags it as cs/constant-condition ("always true"), because its flow analysis
            // reasons from the early return above and does not model another thread mutating
            // _database across the await. Dismissed as a false positive on 2026-08-07 — do not
            // "simplify" this away.
            if (_database is null)
            {
                _database = new SQLiteAsyncConnection(GetDbPath(), Constants.Flags);
                // Create tables in order of dependencies
                _ = await _database.CreateTableAsync<Trainer>();
                _ = await _database.CreateTableAsync<Archetype>();
                _ = await _database.CreateTableAsync<Tags>();
                _ = await _database.CreateTableAsync<Game>();
                _ = await _database.CreateTableAsync<TagGame>();
                _ = await _database.CreateTableAsync<MatchEntry>();
                // Migrate any CDN URLs left over from an old import to the default ball icon.
                // ArchetypeOperations.GetAllAsync resolves proper local sprites on next load.
                await _database.ExecuteAsync(
                    "UPDATE Archetype SET ImagePath = 'substitute.png' WHERE ImagePath LIKE 'http%'");
            }
        }
        finally
        {
            _ = _semaphore.Release();
        }
    }
}