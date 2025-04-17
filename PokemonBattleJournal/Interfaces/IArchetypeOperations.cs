namespace PokemonBattleJournal.Interfaces
{
    public interface IArchetypeOperations
    {
        /// <summary>
        /// Saves an archetype to the database.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when name, image path, or trainer ID is invalid.</exception>
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
        /// <exception cref="ArgumentNullException">Thrown when archetype is null.</exception>
        /// <exception cref="ArgumentException">Thrown when archetype ID is not provided.</exception>
        /// <exception cref="InvalidOperationException">Thrown when archetype is used in existing matches.</exception>
        Task<int> DeleteAsync(Archetype archetype);
    }
}
