namespace PokemonBattleJournal.Interfaces
{
    public interface IMatchOperations
    {
        /// <summary>
        /// Saves a match entry and its associated games to the database.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when required fields are missing or validation fails.</exception>
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
        /// <exception cref="ArgumentNullException">Thrown when match entry is null.</exception>
        /// <exception cref="ArgumentException">Thrown when match entry ID is not provided.</exception>
        Task<int> DeleteAsync(MatchEntry matchEntry);

    }
}
