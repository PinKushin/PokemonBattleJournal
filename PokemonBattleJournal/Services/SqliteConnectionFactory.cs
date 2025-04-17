namespace PokemonBattleJournal.Services;

/// <summary>
/// Provides methods for interacting with the SQLite database.
/// </summary>
public class SqliteConnectionFactory : ISqliteConnectionFactory
{
    private static SQLiteAsyncConnection? _database;
    private static readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly ILogger _logger;

    public SqliteConnectionFactory(ILogger logger)
    {
        _logger = logger;
        Trainers = new TrainerOperations(this, logger);
        Matches = new MatchOperations(this, logger);
        Archetypes = new ArchetypeOperations(this, logger);
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

    private static async Task InitAsync()
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
                _database = new SQLiteAsyncConnection(Constants.DatabasePath, Constants.Flags);
                // Create tables in order of dependencies
                _ = await _database.CreateTableAsync<Trainer>();
                _ = await _database.CreateTableAsync<Archetype>();
                _ = await _database.CreateTableAsync<Tags>();
                _ = await _database.CreateTableAsync<Game>();
                _ = await _database.CreateTableAsync<TagGame>();
                _ = await _database.CreateTableAsync<MatchEntry>();
            }
        }
        finally
        {
            _ = _semaphore.Release();
        }
    }
}