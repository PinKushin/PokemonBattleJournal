namespace PokemonBattleJournal.Interfaces
{
    public interface IArchetypeOperations
    {
        /// <summary>
        /// Saves an archetype to the database.
        /// </summary>
        Task<int> SaveAsync(string name, string imgPath, uint trainerId);

        /// <summary>
        /// Gets all archetypes.
        /// </summary>
        Task<List<Archetype>> GetAllAsync();

        /// <summary>
        /// Gets an archetype by ID.
        /// </summary>
        Task<Archetype?> GetByIdAsync(uint id);

        /// <summary>
        /// Deletes an archetype and related records.
        /// </summary>
        Task<int> DeleteAsync(Archetype archetype);
    }
}
