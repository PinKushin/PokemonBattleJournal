namespace PokemonBattleJournal.Services;

/// <summary>
/// An open database connection with the write lock held. Disposing releases the lock.
/// </summary>
/// <remarks>
/// Ties the release to the acquisition so the two cannot drift apart. Opening the connection
/// can itself fail, and a bare <c>finally { GetLock().Release(); }</c> then releases a lock
/// that was never taken — <see cref="SemaphoreSlim"/> is capped at one permit, so that throws
/// <see cref="SemaphoreFullException"/> and masks the original failure.
/// </remarks>
public sealed class DbSession : IDisposable
{
    private readonly SemaphoreSlim _gate;

    internal DbSession(SQLiteAsyncConnection connection, SemaphoreSlim gate)
    {
        Connection = connection;
        _gate = gate;
    }

    /// <summary>The open connection. Valid for the lifetime of this session.</summary>
    public SQLiteAsyncConnection Connection { get; }

    public void Dispose()
    {
        _ = _gate.Release();
    }
}

public static class SqliteConnectionFactoryExtensions
{
    /// <summary>
    /// Opens the database and takes the write lock, in that order.
    /// </summary>
    /// <remarks>
    /// The order matters: the factory acquires the same semaphore while initialising the
    /// connection, so taking the lock first would deadlock. Written as an extension rather than
    /// an interface member so tests keep a single seam — making
    /// <see cref="ISqliteConnectionFactory.GetDatabaseAsync"/> fail is enough to exercise every
    /// connection-failure path.
    /// </remarks>
    public static async Task<DbSession> BeginAsync(this ISqliteConnectionFactory factory)
    {
        SQLiteAsyncConnection connection = await factory.GetDatabaseAsync();
        SemaphoreSlim gate = factory.GetLock();
        await gate.WaitAsync();
        return new DbSession(connection, gate);
    }
}
