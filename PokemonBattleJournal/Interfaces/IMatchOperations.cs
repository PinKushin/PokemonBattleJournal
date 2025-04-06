namespace PokemonBattleJournal.Interfaces
{
    public interface IMatchOperations
    {
        /// <summary>
        /// Saves a match entry and its associated games to the database.
        /// </summary>
        Task<int> SaveAsync(MatchEntry matchEntry, List<Game> games);

        /// <summary>
        /// Gets a match entry with all related data by ID.
        /// </summary>
        Task<MatchEntry?> GetByIdAsync(uint id, bool includeRelated = true);

        /// <summary>
        /// Gets all match entries for a trainer with related data.
        /// </summary>
        Task<List<MatchEntry>> GetByTrainerIdAsync(uint trainerId, bool includeRelated = true);

        /// <summary>
        /// Gets all match entries.
        /// </summary>
        Task<List<MatchEntry>> GetAllAsync();

        /// <summary>
        /// Deletes a match entry and all related records.
        /// </summary>
        Task<int> DeleteAsync(MatchEntry matchEntry);

    }
}
