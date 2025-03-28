namespace PokemonBattleJournal.ViewModels
{
    public partial class TrainerPageViewModel : ObservableObject
    {
        private readonly SqliteConnectionFactory _connection;
        private readonly ILogger<TrainerPageViewModel> _logger;
        private static readonly SemaphoreSlim _semaphore = new(1, 1);

        public TrainerPageViewModel(ILogger<TrainerPageViewModel> logger, SqliteConnectionFactory connection)
        {
            WelcomeMsg = $"{TrainerName}'s Profile";
            _logger = logger;
            _connection = connection;
        }

        [ObservableProperty]
        public partial string TrainerName { get; set; } = PreferencesHelper.GetSetting("TrainerName");

        [ObservableProperty]
        public partial string WelcomeMsg { get; set; }

        [ObservableProperty]
        public partial uint Wins { get; set; } = 0;

        [ObservableProperty]
        public partial uint Losses { get; set; } = 0;

        [ObservableProperty]
        public partial uint Ties { get; set; } = 0;

        [ObservableProperty]
        public partial double WinAverage { get; set; } = 0;

        [RelayCommand]
        public async Task AppearingAsync()
        {
            try
            {
                await _semaphore.WaitAsync();
                var trainer = await _connection.GetTrainerByNameAsync(TrainerName);
                if (trainer == null)
                {
                    _logger.LogWarning("Trainer not found");
                    return;
                }
                var matchList = await _connection.GetMatchEntriesByTrainerIdAsync(trainer.Id);
                if (matchList.Count < 1 || matchList is null)
                {
                    _logger.LogWarning("No matches found for trainer");
                    return;
                }
                uint wins, losses, ties;
                WinAverage = Calculations.CalculateWinRate(matchList, out wins, out losses, out ties);
                Wins = wins;
                Losses = losses;
                Ties = ties;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Trainer Page");
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}