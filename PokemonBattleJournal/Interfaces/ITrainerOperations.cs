namespace PokemonBattleJournal.Interfaces
{
    public interface ITrainerOperations
    {
        /// <summary>
        /// Retrieves a list of all trainers from the database.
        /// </summary>
        Task<List<Trainer>> GetAllAsync();

        /// <summary>
        /// Retrieves a trainer by name from the database.
        /// </summary>
        Task<Trainer?> GetByNameAsync(string name);

        /// <summary>
        /// Retrieves a trainer by ID from the database.
        /// </summary>
        Task<Trainer?> GetByIdAsync(uint id);

        /// <summary>
        /// Saves a trainer to the database.
        /// </summary>
        Task<int> SaveAsync(string trainerName);

        /// <summary>
        /// Deletes a trainer and all related records from the database.
        /// </summary>
        Task<int> DeleteAsync(Trainer trainer);
    }
}
