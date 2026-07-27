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

        public async Task InitializeAsync()
        {
            ActiveTrainer = await _connection.Trainers.GetActiveAsync();
            _logger.LogInformation("Active trainer loaded: {Name} ({Id})", ActiveTrainer?.Name, ActiveTrainer?.Id);
        }

        public async Task SwitchToAsync(Trainer trainer)
        {
            await _connection.Trainers.SetActiveAsync(trainer);
            ActiveTrainer = trainer;
            _logger.LogInformation("Switched to trainer {TrainerName} ({TrainerId})", trainer.Name, trainer.Id);
            TrainerChanged?.Invoke(this, trainer);
        }
    }
}
