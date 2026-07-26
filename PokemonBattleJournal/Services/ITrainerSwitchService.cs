namespace PokemonBattleJournal.Services
{
    public interface ITrainerSwitchService
    {
        Trainer? ActiveTrainer { get; }
        event EventHandler<Trainer> TrainerChanged;
        Task<List<Trainer>> GetAllTrainersAsync();
        Task SwitchToAsync(Trainer trainer);
    }
}
