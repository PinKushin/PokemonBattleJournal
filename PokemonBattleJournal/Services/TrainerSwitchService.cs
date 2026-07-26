namespace PokemonBattleJournal.Services
{
    public class TrainerSwitchService : ITrainerSwitchService
    {
        private readonly ISqliteConnectionFactory _connection;
        private readonly ILogger<TrainerSwitchService> _logger;

        public Trainer? ActiveTrainer { get; private set; }

        public event EventHandler<Trainer>? TrainerChanged;

        public TrainerSwitchService(ISqliteConnectionFactory connection, ILogger<TrainerSwitchService> logger)
        {
            _connection = connection;
            _logger = logger;
        }

        public Task<List<Trainer>> GetAllTrainersAsync() =>
            _connection.Trainers.GetAllAsync();

        public Task SwitchToAsync(Trainer trainer)
        {
            ActiveTrainer = trainer;
            PreferencesHelper.SetSetting("TrainerName", trainer.Name ?? string.Empty);
            PreferencesHelper.SetTrainerId(trainer.Id);
            _logger.LogInformation("Switched to trainer {TrainerName} ({TrainerId})", trainer.Name, trainer.Id);
            TrainerChanged?.Invoke(this, trainer);
            return Task.CompletedTask;
        }
    }
}
