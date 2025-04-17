using System.Collections.ObjectModel;

namespace PokemonBattleJournal.ViewModels
{
    public partial class TrainerPageViewModel : ObservableObject
    {
        private readonly ISqliteConnectionFactory _connection;
        private readonly ILogger<TrainerPageViewModel> _logger;
        private readonly IMatchAnalysisService _analysisService;
        private static readonly SemaphoreSlim _semaphore = new(1, 1);

        public TrainerPageViewModel(ILogger<TrainerPageViewModel> logger, ISqliteConnectionFactory connection, IMatchAnalysisService analysisService)
        {
            WelcomeMsg = $"{TrainerName}'s Profile";
            _logger = logger;
            _connection = connection;
            _analysisService = analysisService;
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

        [ObservableProperty]
        public partial ObservableCollection<ChartDataPoint> MostPlayedArchetypes { get; set; } = [];

        [ObservableProperty]
        public partial ObservableCollection<TimeDataPoint> WinRateOverTime { get; set; } = [];

        [ObservableProperty]
        public partial ObservableCollection<ChartDataPoint> ArchetypeWinRates { get; set; } = [];

        [ObservableProperty]
        public partial ObservableCollection<ChartDataPoint> TagUsage { get; set; } = [];

        [ObservableProperty]
        public partial ObservableCollection<ChartDataPoint> OpponentPerformance { get; set; } = [];

        [ObservableProperty]
        public partial TimeSpan AverageMatchDuration { get; set; }

        [ObservableProperty]
        public partial ObservableCollection<ChartDataPoint> WinRateByMatchLength { get; set; } = [];

        [ObservableProperty]
        public partial ObservableCollection<ChartDataPoint> FirstTurnAdvantage { get; set; } = [];

        [ObservableProperty]
        public partial string StreakInfo { get; set; } = "";

        [RelayCommand]
        public async Task AppearingAsync()
        {
            _logger.LogInformation("TrainerPage appearing");
            try
            {
                await _semaphore.WaitAsync();

                // Get trainer
                Trainer? trainer = await _connection.Trainers.GetByNameAsync(TrainerName);
                if (trainer == null)
                {
                    _logger.LogWarning("Trainer not found: {TrainerName}", TrainerName);
                    // Create trainer if they don't exist
                    _ = await _connection.Trainers.SaveAsync(TrainerName);
                    trainer = await _connection.Trainers.GetByNameAsync(TrainerName);
                    if (trainer == null)
                    {
                        _logger.LogError("Failed to create trainer: {TrainerName}", TrainerName);
                        return;
                    }
                }

                // Get matches with related data
                _logger.LogInformation("Loading matches for trainer: {TrainerId} ({TrainerName})", trainer.Id, trainer.Name);
                List<MatchEntry>? matches = await _connection.Matches.GetByTrainerIdAsync(trainer.Id, true);

                if (matches == null || matches.Count < 1)
                {
                    _logger.LogInformation("No matches found for trainer: {TrainerId} ({TrainerName})", trainer.Id, trainer.Name);
                    // Reset all stats to zero/empty
                    Wins = 0;
                    Losses = 0;
                    Ties = 0;
                    WinAverage = 0;
                    MostPlayedArchetypes = [];
                    WinRateOverTime = [];
                    ArchetypeWinRates = [];
                    TagUsage = [];
                    OpponentPerformance = [];
                    AverageMatchDuration = TimeSpan.Zero;
                    WinRateByMatchLength = [];
                    FirstTurnAdvantage = [];
                    StreakInfo = "No matches played yet";
                    return;
                }

                // Calculate statistics using MatchAnalysisService
                _logger.LogInformation("Calculating statistics for {Count} matches", matches.Count);

                WinAverage = _analysisService.CalculateWinRate(matches, out uint wins, out uint losses, out uint ties);
                Wins = wins;
                Losses = losses;
                Ties = ties;
                _logger.LogDebug("Basic stats calculated: Wins={Wins}, Losses={Losses}, Ties={Ties}, WinRate={WinRate}",
                    wins, losses, ties, WinAverage);

                MostPlayedArchetypes = _analysisService.GetMostPlayedArchetypes(matches);
                _logger.LogDebug("Most played archetypes calculated: {Count} entries", MostPlayedArchetypes.Count);

                WinRateOverTime = _analysisService.CalculateWinRateOverTime(matches);
                _logger.LogDebug("Win rate over time calculated: {Count} data points", WinRateOverTime.Count);

                ArchetypeWinRates = _analysisService.CalculateArchetypeWinRate(matches);
                _logger.LogDebug("Archetype win rates calculated: {Count} archetypes", ArchetypeWinRates.Count);

                TagUsage = _analysisService.CalculateTagUsage(matches);
                _logger.LogDebug("Tag usage calculated: {Count} tags", TagUsage.Count);

                OpponentPerformance = _analysisService.CalculatePerformanceAgainstOpponents(matches);
                _logger.LogDebug("Opponent performance calculated: {Count} opponents", OpponentPerformance.Count);

                AverageMatchDuration = _analysisService.CalculateAverageMatchDuration(matches);
                _logger.LogDebug("Average match duration calculated: {Duration}", AverageMatchDuration);

                WinRateByMatchLength = _analysisService.CalculateWinRateByMatchLength(matches);
                _logger.LogDebug("Win rate by match length calculated: {Count} categories", WinRateByMatchLength.Count);

                FirstTurnAdvantage = _analysisService.CalculateFirstTurnAdvantage(matches);
                _logger.LogDebug("First turn advantage calculated: {Count} entries", FirstTurnAdvantage.Count);

                (int winStreak, int lossStreak, int tieStreak) = _analysisService.CalculateStreaks(matches);
                StreakInfo = $"Longest Streaks - Wins: {winStreak}, Losses: {lossStreak}, Ties: {tieStreak}";
                _logger.LogDebug("Streaks calculated: Win={WinStreak}, Loss={LossStreak}, Tie={TieStreak}",
                    winStreak, lossStreak, tieStreak);

                _logger.LogInformation("All statistics calculated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Trainer Page data");
                ModalErrorHandler modalErrorHandler = new();
                modalErrorHandler.HandleError(ex);
            }
            finally
            {
                _ = _semaphore.Release();
            }
        }
    }
}