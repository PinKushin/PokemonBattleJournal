namespace PokemonBattleJournal.Services;

/// <summary>
/// Provides methods for interacting with the SQLite database.
/// </summary>
public class SqliteConnectionFactory : ISqliteConnectionFactory
{
    private static SQLiteAsyncConnection? _database;
    private static readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly ILogger _logger;
    private readonly ITrainerOperations _trainerOps;
    private readonly IMatchOperations _matchOps;
    private readonly IArchetypeOperations _archetypeOperations;
    private readonly ITagOperations _tagOperations;

    public SqliteConnectionFactory(ILogger logger)
    {
        _logger = logger;
        _trainerOps = new TrainerOperations(this, logger);
        _matchOps = new MatchOperations(this, logger);
        _archetypeOperations = new ArchetypeOperations(this, logger);
        _tagOperations = new TagOperations(this, logger);
    }

    public ITrainerOperations Trainers => _trainerOps;
    public IMatchOperations Matches => _matchOps;

    public IArchetypeOperations Archetypes => _archetypeOperations;

    public ITagOperations Tags => _tagOperations;

    public async Task<SQLiteAsyncConnection> GetDatabaseAsync()
    {
        await InitAsync();
        return _database!;
    }

    public SemaphoreSlim GetLock() => _semaphore;

    private static async Task InitAsync()
    {
        if (_database is not null)
            return;

        try
        {
            await _semaphore.WaitAsync();
            if (_database is null)
            {
                _database = new SQLiteAsyncConnection(Constants.DatabasePath, Constants.Flags);
                await _database.CreateTableAsync<Trainer>();
                await _database.CreateTableAsync<Archetype>();
                await _database.CreateTableAsync<MatchEntry>();
                await _database.CreateTableAsync<Tags>();
                await _database.CreateTableAsync<Game>();
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }
}