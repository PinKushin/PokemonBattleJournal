namespace PokemonBattleJournal.Services
{
    public interface ITrainerSwitchService
    {
        Trainer? ActiveTrainer { get; }
        event EventHandler<Trainer> TrainerChanged;
        Task<List<Trainer>> GetAllTrainersAsync();
        /// <summary>
        /// Loads the active trainer from the database. Call once at app startup.
        /// </summary>
        Task InitializeAsync();
        Task SwitchToAsync(Trainer trainer);
    }
}
