namespace PokemonBattleJournal.Interfaces
{
    public interface ISqliteConnectionFactory
    {
        ITrainerOperations Trainers { get; }
        IMatchOperations Matches { get; }
        IArchetypeOperations Archetypes { get; }
        ITagOperations Tags { get; }
        Task<SQLiteAsyncConnection> GetDatabaseAsync();
        SemaphoreSlim GetLock();
    }
}
