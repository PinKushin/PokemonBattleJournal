using PokemonBattleJournal.Scraper.Interfaces;

namespace PokemonBattleJournal.Services;

/// <summary>
/// Provides methods for interacting with the SQLite database.
/// </summary>
public class SqliteConnectionFactory : ISqliteConnectionFactory
{
    private SQLiteAsyncConnection? _database;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public SqliteConnectionFactory(ILogger logger, ILimitlessMetaService metaService)
    {
        Trainers = new TrainerOperations(this, logger);
        Matches = new MatchOperations(this, logger);
        Archetypes = new ArchetypeOperations(this, logger, metaService);
        Tags = new TagOperations(this, logger);
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