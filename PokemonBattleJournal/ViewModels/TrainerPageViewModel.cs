using System.Collections.ObjectModel;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using PokemonBattleJournal.Utilities;

namespace PokemonBattleJournal.ViewModels
{
    public partial class TrainerPageViewModel : ObservableObject
    {
        private readonly ISqliteConnectionFactory _connection;
        private readonly ILogger<TrainerPageViewModel> _logger;
        private readonly IMatchAnalysisService _analysisService;
        private readonly ITrainerSwitchService _switchService;

        public TrainerPageViewModel(ILogger<TrainerPageViewModel> logger, ISqliteConnectionFactory connection, IMatchAnalysisService analysisService, ITrainerSwitchService switchService)
        {
            _logger = logger;
            _connection = connection;
            _analysisService = analysisService;
            _switchService = switchService;
            _switchService.TrainerChanged += OnTrainerChanged;
            WelcomeMsg = "Trainer Profile";
        }

        private void OnTrainerChanged(object? sender, Trainer trainer)
        {
            MainThreadHelper.BeginInvokeOnMainThread(() =>
            {
                TrainerName = trainer.Name ?? string.Empty;
                WelcomeMsg = $"{TrainerName}'s Profile";
                AppearingAsync().FireAndForgetSafeAsync(logger: _logger);
            });
        }

        [ObservableProperty]
        public partial string TrainerName { get; set; } = string.Empty;

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
        public partial TimeSpan AverageMatchDuration { get; set; }

        [ObservableProperty]
        public partial string StreakInfo { get; set; } = "";

        // Matchup matrix heatmap
        [ObservableProperty]
        public partial ISeries[] MatchupHeatSeries { get; set; } = [];

        [ObservableProperty]
        public partial ICartesianAxis[] MatchupXAxes { get; set; } = [new Axis { Name = "Opponent Archetype" }];

        [ObservableProperty]
        public partial ICartesianAxis[] MatchupYAxes { get; set; } = [new Axis { Name = "Your Archetype" }];

        // Most played archetypes — horizontal bar
        [ObservableProperty]
        public partial ISeries[] MostPlayedSeries { get; set; } = [];

        [ObservableProperty]
        public partial ICartesianAxis[] MostPlayedYAxes { get; set; } = [new Axis()];

        // Archetype win rates — horizontal bar
        [ObservableProperty]
        public partial ISeries[] ArchetypeWinRateSeries { get; set; } = [];

        [ObservableProperty]
        public partial ICartesianAxis[] ArchetypeWinRateYAxes { get; set; } = [new Axis()];

        // Tag usage — horizontal bar
        [ObservableProperty]
        public partial ISeries[] TagUsageSeries { get; set; } = [];

        [ObservableProperty]
        public partial ICartesianAxis[] TagUsageYAxes { get; set; } = [new Axis()];

        // Performance vs opponents — horizontal bar
        [ObservableProperty]
        public partial ISeries[] OpponentSeries { get; set; } = [];

        [ObservableProperty]
        public partial ICartesianAxis[] OpponentYAxes { get; set; } = [new Axis()];

        // Win rate over time — line chart
        [ObservableProperty]
        public partial ISeries[] WinRateOverTimeSeries { get; set; } = [];

        [ObservableProperty]
        public partial ICartesianAxis[] WinRateTimeXAxes { get; set; } = [new Axis()];

        // Win rate by match length — horizontal bar (2 bars)
        [ObservableProperty]
        public partial ISeries[] MatchLengthSeries { get; set; } = [];

        [ObservableProperty]
        public partial ICartesianAxis[] MatchLengthYAxes { get; set; } = [new Axis()];

        // First turn advantage — horizontal bar (2 bars)
        [ObservableProperty]
        public partial ISeries[] FirstTurnSeries { get; set; } = [];

        [ObservableProperty]
        public partial ICartesianAxis[] FirstTurnYAxes { get; set; } = [new Axis()];

        [RelayCommand]
        public async Task AppearingAsync()
        {
            _logger.LogInformation("TrainerPage appearing");
            List<MatchEntry>? matches = null;
            try
            {
                Trainer? trainer = _switchService.ActiveTrainer ?? await _connection.Trainers.GetActiveAsync();
                if (trainer == null)
                {
                    _logger.LogWarning("No active trainer set");
                    return;
                }
                TrainerName = trainer.Name ?? TrainerName;
                WelcomeMsg = $"{TrainerName}'s Profile";

                _logger.LogInformation("Loading matches for trainer: {TrainerId} ({TrainerName})", trainer.Id, trainer.Name);
                matches = await _connection.Matches.GetByTrainerIdAsync(trainer.Id, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Trainer Page data");
                ModalErrorHandler modalErrorHandler = new();
                modalErrorHandler.HandleError(ex);
                return;
            }

            if (matches == null || matches.Count < 1)
            {
                MainThreadHelper.BeginInvokeOnMainThread(ResetStats);
                return;
            }

            _logger.LogInformation("Calculating statistics for {Count} matches", matches.Count);

            // Compute stats on current thread before touching UI
            double winAverage = _analysisService.CalculateWinRate(matches, out uint wins, out uint losses, out uint ties);
            TimeSpan avgDuration = _analysisService.CalculateAverageMatchDuration(matches);
            (int winStreak, int lossStreak, int tieStreak) = _analysisService.CalculateStreaks(matches);

            // Post scalar stats immediately
            MainThreadHelper.BeginInvokeOnMainThread(() =>
            {
                WinAverage = winAverage;
                Wins = wins;
                Losses = losses;
                Ties = ties;
                AverageMatchDuration = avgDuration;
                StreakInfo = $"Longest Streaks - Wins: {winStreak}, Losses: {lossStreak}, Ties: {tieStreak}";
            });

            MainThreadHelper.BeginInvokeOnMainThread(() => BuildMatchupHeatmap(matches));
            MainThreadHelper.BeginInvokeOnMainThread(() => BuildMostPlayedChart(matches));
            MainThreadHelper.BeginInvokeOnMainThread(() => BuildArchetypeWinRateChart(matches));
            MainThreadHelper.BeginInvokeOnMainThread(() => BuildTagUsageChart(matches));
            MainThreadHelper.BeginInvokeOnMainThread(() => BuildOpponentChart(matches));
            MainThreadHelper.BeginInvokeOnMainThread(() => BuildWinRateOverTimeChart(matches));
            MainThreadHelper.BeginInvokeOnMainThread(() => BuildMatchLengthChart(matches));
            MainThreadHelper.BeginInvokeOnMainThread(() => BuildFirstTurnChart(matches));

            _logger.LogInformation("All statistics queued for rendering");
        }

        private void ResetStats()
        {
            Wins = 0; Losses = 0; Ties = 0; WinAverage = 0;
            AverageMatchDuration = TimeSpan.Zero;
            StreakInfo = "No matches played yet";
            MatchupHeatSeries = []; MostPlayedSeries = []; ArchetypeWinRateSeries = [];
            TagUsageSeries = []; OpponentSeries = []; WinRateOverTimeSeries = [];
            MatchLengthSeries = []; FirstTurnSeries = [];
        }

        private static SKColor PokeYellow => new(255, 203, 5);
        private static SKColor PokeBlue => new(59, 130, 246);

        private void BuildMatchupHeatmap(List<MatchEntry> matches)
        {
            var (played, opponents, cells) = _analysisService.CalculateMatchupMatrix(matches);
            if (cells.Length == 0) { MatchupHeatSeries = []; return; }

            MatchupHeatSeries =
            [
                new HeatSeries<WeightedPoint>
                {
                    Values = cells.Select(c => new WeightedPoint(c.OpponentIdx, c.PlayedIdx, c.WinRate)).ToArray(),
                    HeatMap =
                    [
                        new(239, 68, 68, 200),   // red — 0%
                        new(250, 204, 21, 200),  // yellow — 50%
                        new(34, 197, 94, 200),   // green — 100%
                    ],
                    MinValue = 0,
                    MaxValue = 100,
                    DataLabelsPaint = new SolidColorPaint(SKColors.White) { SKTypeface = SKTypeface.Default },
                    DataLabelsFormatter = p => $"{p.Model?.Weight:F0}%",
                }
            ];
            MatchupXAxes = [new Axis { Labels = opponents, Name = "Opponent Archetype", TextSize = 11 }];
            MatchupYAxes = [new Axis { Labels = played, Name = "Your Archetype", TextSize = 11 }];
        }

        private void BuildMostPlayedChart(List<MatchEntry> matches)
        {
            var data = _analysisService.GetMostPlayedArchetypes(matches);
            MostPlayedSeries =
            [
                new RowSeries<ObservableValue>
                {
                    Values = data.Select(x => new ObservableValue(x.Value)).ToArray(),
                    Fill = new SolidColorPaint(PokeBlue),
                    Name = "Games Played",
                    MaxBarWidth = 20,
                }
            ];
            MostPlayedYAxes = [new Axis { Labels = data.Select(x => x.Label ?? "").ToArray(), TextSize = 11 }];
        }

        private void BuildArchetypeWinRateChart(List<MatchEntry> matches)
        {
            var data = _analysisService.CalculateArchetypeWinRate(matches);
            ArchetypeWinRateSeries =
            [
                new RowSeries<ObservableValue>
                {
                    Values = data.Select(x => new ObservableValue(x.Value)).ToArray(),
                    Fill = new SolidColorPaint(PokeYellow),
                    Name = "Win Rate %",
                    MaxBarWidth = 20,
                }
            ];
            ArchetypeWinRateYAxes = [new Axis { Labels = data.Select(x => x.Label ?? "").ToArray(), TextSize = 11 }];
        }

        private void BuildTagUsageChart(List<MatchEntry> matches)
        {
            var data = _analysisService.CalculateTagUsage(matches);
            TagUsageSeries =
            [
                new RowSeries<ObservableValue>
                {
                    Values = data.Select(x => new ObservableValue(x.Value)).ToArray(),
                    Fill = new SolidColorPaint(PokeBlue),
                    Name = "Uses",
                    MaxBarWidth = 20,
                }
            ];
            TagUsageYAxes = [new Axis { Labels = data.Select(x => x.Label ?? "").ToArray(), TextSize = 11 }];
        }

        private void BuildOpponentChart(List<MatchEntry> matches)
        {
            var data = _analysisService.CalculatePerformanceAgainstOpponents(matches);
            OpponentSeries =
            [
                new RowSeries<ObservableValue>
                {
                    Values = data.Select(x => new ObservableValue(x.Value)).ToArray(),
                    Fill = new SolidColorPaint(PokeYellow),
                    Name = "Win Rate %",
                    MaxBarWidth = 20,
                }
            ];
            OpponentYAxes = [new Axis { Labels = data.Select(x => x.Label ?? "").ToArray(), TextSize = 11 }];
        }

        private void BuildWinRateOverTimeChart(List<MatchEntry> matches)
        {
            var data = _analysisService.CalculateWinRateOverTime(matches);
            WinRateOverTimeSeries =
            [
                new LineSeries<DateTimePoint>
                {
                    Values = data.Select(x => new DateTimePoint(x.Date, x.Value)).ToArray(),
                    Fill = null,
                    Stroke = new SolidColorPaint(PokeYellow, 2),
                    GeometryFill = new SolidColorPaint(PokeYellow),
                    GeometryStroke = new SolidColorPaint(PokeBlue, 1),
                    GeometrySize = 6,
                    Name = "Win Rate %",
                }
            ];
            WinRateTimeXAxes =
            [
                new Axis
                {
                    Labeler = value =>
                    {
                        long ticks = (long)value;
                        if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
                            return string.Empty;
                        return new DateTime(ticks, DateTimeKind.Utc).ToString("MM/dd");
                    },
                    UnitWidth = TimeSpan.FromDays(1).Ticks,
                    MinStep = TimeSpan.FromDays(1).Ticks,
                    TextSize = 11,
                }
            ];
        }

        private void BuildMatchLengthChart(List<MatchEntry> matches)
        {
            var data = _analysisService.CalculateWinRateByMatchLength(matches);
            MatchLengthSeries =
            [
                new RowSeries<ObservableValue>
                {
                    Values = data.Select(x => new ObservableValue(x.Value)).ToArray(),
                    Fill = new SolidColorPaint(PokeBlue),
                    Name = "Win Rate %",
                    MaxBarWidth = 24,
                }
            ];
            MatchLengthYAxes = [new Axis { Labels = data.Select(x => x.Label ?? "").ToArray(), TextSize = 12 }];
        }

        private void BuildFirstTurnChart(List<MatchEntry> matches)
        {
            var data = _analysisService.CalculateFirstTurnAdvantage(matches);
            FirstTurnSeries =
            [
                new RowSeries<ObservableValue>
                {
                    Values = data.Select(x => new ObservableValue(x.Value)).ToArray(),
                    Fill = new SolidColorPaint(PokeYellow),
                    Name = "Win Rate %",
                    MaxBarWidth = 24,
                }
            ];
            FirstTurnYAxes = [new Axis { Labels = data.Select(x => x.Label ?? "").ToArray(), TextSize = 12 }];
        }
    }
}
