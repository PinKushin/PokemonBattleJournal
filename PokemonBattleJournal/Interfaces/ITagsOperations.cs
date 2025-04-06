namespace PokemonBattleJournal.Interfaces
{
    public interface ITagOperations
    {
        /// <summary>
        /// Saves a tag to the database.
        /// </summary>
        Task<int> SaveAsync(string tagTxt, uint trainerId);

        /// <summary>
        /// Gets all tags.
        /// </summary>
        Task<List<Tags>> GetAllAsync();

        /// <summary>
        /// Gets a tag by ID.
        /// </summary>
        Task<Tags?> GetByIdAsync(uint id);

        /// <summary>
        /// Deletes a tag.
        /// </summary>
        Task<int> DeleteAsync(Tags tag);
    }
}
