using PokemonBattleJournal.Interfaces;

namespace PokemonBattleJournal.Tests.ViewModels
{
    public class TrainerPageViewModelTests
    {
        private TrainerPageViewModel _viewModel = null!;
        private ISqliteConnectionFactory _mockConnectionFactory = null!;
        private ILogger<TrainerPageViewModel> _mockLogger = null!;
        private IMatchAnalysisService _mockAnalysisService = null!;
        private ITrainerSwitchService _mockSwitchService = null!;

        [SetUp]
        public void SetUp()
        {
            // Mocks
            _mockLogger = Substitute.For<ILogger<TrainerPageViewModel>>();
            _mockAnalysisService = Substitute.For<IMatchAnalysisService>();
            _mockConnectionFactory = Substitute.For<ISqliteConnectionFactory>();
            _mockSwitchService = Substitute.For<ITrainerSwitchService>();

            _mockConnectionFactory.Trainers.Returns(Substitute.For<ITrainerOperations>());
            _mockConnectionFactory.Matches.Returns(Substitute.For<IMatchOperations>());

            // SUT
            _viewModel = new TrainerPageViewModel(_mockLogger, _mockConnectionFactory, _mockAnalysisService, _mockSwitchService);
        }

        [Test]
        public void TrainerPageViewModel_Constructor_SetsWelcomeMsg()
        {
            // Assert
            _viewModel.ShouldNotBeNull();
            _viewModel.WelcomeMsg.ShouldNotBeNullOrEmpty();
        }

        [Test]
        public async Task AppearingAsync_NoMatches_ResetsStatsToZero()
        {
            // Arrange
            _mockConnectionFactory.Trainers.GetActiveAsync()
                .Returns(Task.FromResult<Trainer?>(new Trainer { Id = 1, Name = "Test" }));
            _mockConnectionFactory.Matches.GetByTrainerIdAsync(1, true)
                .Returns(Task.FromResult(new List<MatchEntry>()));

            // Act
            await _viewModel.AppearingAsync();

            // Assert
            _viewModel.Wins.ShouldBe(0u);
            _viewModel.Losses.ShouldBe(0u);
            _viewModel.Ties.ShouldBe(0u);
            _viewModel.WinAverage.ShouldBe(0);
            _viewModel.StreakInfo.ShouldBe("No matches played yet");
        }

        [Test]
        public async Task AppearingAsync_NoTrainer_AfterCreateFails_DoesNotCallAnalysis()
        {
            _mockConnectionFactory.Trainers.GetActiveAsync()
                .Returns(Task.FromResult<Trainer?>(null));
            _mockConnectionFactory.Trainers.SaveAsync(Arg.Any<string>())
                .Returns(Task.FromResult(0));

            await _viewModel.AppearingAsync();

            _mockAnalysisService.DidNotReceive().CalculateWinRate(
                Arg.Any<List<MatchEntry>>(), out Arg.Any<uint>(), out Arg.Any<uint>(), out Arg.Any<uint>());
        }

        [Test]
        public async Task AppearingAsync_WithMatches_SetsStreakInfoString()
        {
            List<MatchEntry> matches = [new() { Result = MatchResult.Win }];

            _mockConnectionFactory.Trainers.GetActiveAsync()
                .Returns(Task.FromResult<Trainer?>(new Trainer { Id = 1, Name = "Test" }));
            _mockConnectionFactory.Matches.GetByTrainerIdAsync(1, true)
                .Returns(Task.FromResult(matches));

            _mockAnalysisService.CalculateWinRate(matches, out _, out _, out _).Returns(100);
            _mockAnalysisService.CalculateMatchupMatrix(matches).Returns((Array.Empty<string>(), Array.Empty<string>(), Array.Empty<(int, int, double)>()));
            _mockAnalysisService.GetMostPlayedArchetypes(matches).Returns([]);
            _mockAnalysisService.CalculateWinRateOverTime(matches).Returns([]);
            _mockAnalysisService.CalculateArchetypeWinRate(matches).Returns([]);
            _mockAnalysisService.CalculateTagUsage(matches).Returns([]);
            _mockAnalysisService.CalculatePerformanceAgainstOpponents(matches).Returns([]);
            _mockAnalysisService.CalculateAverageMatchDuration(matches).Returns(TimeSpan.Zero);
            _mockAnalysisService.CalculateWinRateByMatchLength(matches).Returns([]);
            _mockAnalysisService.CalculateFirstTurnAdvantage(matches).Returns([]);
            _mockAnalysisService.CalculateStreaks(matches).Returns((5, 2, 1));

            await _viewModel.AppearingAsync();

            _viewModel.StreakInfo.ShouldBe("Longest Streaks - Wins: 5, Losses: 2, Ties: 1");
        }

        [Test]
        public async Task AppearingAsync_WithMatches_CalculatesStats()
        {
            // Arrange
            List<MatchEntry> matches =
            [
                new() { Result = MatchResult.Win },
                new() { Result = MatchResult.Loss },
                new() { Result = MatchResult.Win }
            ];

            _mockConnectionFactory.Trainers.GetActiveAsync()
                .Returns(Task.FromResult<Trainer?>(new Trainer { Id = 1, Name = "Test" }));
            _mockConnectionFactory.Matches.GetByTrainerIdAsync(1, true)
                .Returns(Task.FromResult(matches));

            _mockAnalysisService.CalculateWinRate(matches, out _, out _, out _)
                .Returns(66.66666666666667);
            _mockAnalysisService.CalculateMatchupMatrix(matches)
                .Returns((Array.Empty<string>(), Array.Empty<string>(), Array.Empty<(int, int, double)>()));
            _mockAnalysisService.GetMostPlayedArchetypes(matches)
                .Returns([]);
            _mockAnalysisService.CalculateWinRateOverTime(matches)
                .Returns([]);
            _mockAnalysisService.CalculateArchetypeWinRate(matches)
                .Returns([]);
            _mockAnalysisService.CalculateTagUsage(matches)
                .Returns([]);
            _mockAnalysisService.CalculatePerformanceAgainstOpponents(matches)
                .Returns([]);
            _mockAnalysisService.CalculateAverageMatchDuration(matches)
                .Returns(TimeSpan.Zero);
            _mockAnalysisService.CalculateWinRateByMatchLength(matches)
                .Returns([]);
            _mockAnalysisService.CalculateFirstTurnAdvantage(matches)
                .Returns([]);
            _mockAnalysisService.CalculateStreaks(matches)
                .Returns((2, 1, 0));

            // Act
            await _viewModel.AppearingAsync();

            // Assert
            _mockAnalysisService.CalculateWinRate(matches, out Arg.Any<uint>(), out Arg.Any<uint>(), out Arg.Any<uint>());
        }

        // ---------------------------------------------------------------------------
        // OnTrainerChanged
        // ---------------------------------------------------------------------------

        [Test]
        public void OnTrainerChanged_EventRaised_UpdatesTrainerName()
        {
            var trainer = new Trainer { Id = 3, Name = "Misty" };

            _mockSwitchService.TrainerChanged += Raise.Event<EventHandler<Trainer>>(this, trainer);

            _viewModel.TrainerName.ShouldBe("Misty");
        }

        [Test]
        public void OnTrainerChanged_EventRaised_UpdatesWelcomeMsg()
        {
            var trainer = new Trainer { Id = 3, Name = "Misty" };

            _mockSwitchService.TrainerChanged += Raise.Event<EventHandler<Trainer>>(this, trainer);

            _viewModel.WelcomeMsg.ShouldBe("Misty's Profile");
        }

        [Test]
        public void OnTrainerChanged_EventRaised_NullName_SetsEmptyTrainerName()
        {
            var trainer = new Trainer { Id = 3, Name = null };

            _mockSwitchService.TrainerChanged += Raise.Event<EventHandler<Trainer>>(this, trainer);

            _viewModel.TrainerName.ShouldBe(string.Empty);
        }

        // ---------------------------------------------------------------------------
        // BuildMatchupHeatmap — non-empty data path
        // ---------------------------------------------------------------------------

        [Test]
        public async Task AppearingAsync_WithMatchupData_SetsMatchupHeatSeries()
        {
            var matches = new List<MatchEntry> { new() { Result = MatchResult.Win } };
            _mockConnectionFactory.Trainers.GetActiveAsync()
                .Returns(Task.FromResult<Trainer?>(new Trainer { Id = 1, Name = "Ash" }));
            _mockConnectionFactory.Matches.GetByTrainerIdAsync(1, true)
                .Returns(Task.FromResult(matches));

            _mockAnalysisService.CalculateWinRate(matches, out _, out _, out _).Returns(100d);
            _mockAnalysisService.CalculateMatchupMatrix(matches)
                .Returns((
                    new[] { "Charizard" },
                    new[] { "Pikachu" },
                    new (int, int, double)[] { (0, 0, 75.0) }));
            _mockAnalysisService.GetMostPlayedArchetypes(matches).Returns([]);
            _mockAnalysisService.CalculateWinRateOverTime(matches).Returns([]);
            _mockAnalysisService.CalculateArchetypeWinRate(matches).Returns([]);
            _mockAnalysisService.CalculateTagUsage(matches).Returns([]);
            _mockAnalysisService.CalculatePerformanceAgainstOpponents(matches).Returns([]);
            _mockAnalysisService.CalculateAverageMatchDuration(matches).Returns(TimeSpan.Zero);
            _mockAnalysisService.CalculateWinRateByMatchLength(matches).Returns([]);
            _mockAnalysisService.CalculateFirstTurnAdvantage(matches).Returns([]);
            _mockAnalysisService.CalculateStreaks(matches).Returns((1, 0, 0));

            await _viewModel.AppearingAsync();

            _viewModel.MatchupHeatSeries.ShouldNotBeNull();
            _viewModel.MatchupHeatSeries.ShouldNotBeEmpty();
            _viewModel.MatchupXAxes.ShouldNotBeNull();
            _viewModel.MatchupYAxes.ShouldNotBeNull();
        }

        // ---------------------------------------------------------------------------
        // FormatChartDateLabel
        // ---------------------------------------------------------------------------

        [Test]
        public void FormatChartDateLabel_ValidTicks_ReturnsFormattedDate()
        {
            double ticks = new DateTime(2026, 7, 25, 0, 0, 0, DateTimeKind.Utc).Ticks;

            string label = TrainerPageViewModel.FormatChartDateLabel(ticks);

            label.ShouldBe("07/25");
        }

        [Test]
        public void FormatChartDateLabel_NegativeTicks_ReturnsEmpty()
        {
            string label = TrainerPageViewModel.FormatChartDateLabel(-1d);

            label.ShouldBe(string.Empty);
        }

        [Test]
        public void FormatChartDateLabel_OverflowTicks_ReturnsEmpty()
        {
            string label = TrainerPageViewModel.FormatChartDateLabel((double)DateTime.MaxValue.Ticks + 1e10);

            label.ShouldBe(string.Empty);
        }
    }
}
