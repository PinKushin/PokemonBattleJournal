using PokemonBattleJournal.Interfaces;

namespace PokemonBattleJournal.Tests.ViewModels
{
    public class TrainerPageViewModelTests
    {
        private readonly TrainerPageViewModel _viewModel;
        private readonly ISqliteConnectionFactory _mockConnectionFactory;
        private readonly ILogger<TrainerPageViewModel> _mockLogger;
        private readonly IMatchAnalysisService _mockAnalysisService;

        public TrainerPageViewModelTests()
        {
            // Mocks
            _mockLogger = Substitute.For<ILogger<TrainerPageViewModel>>();
            _mockAnalysisService = Substitute.For<IMatchAnalysisService>();
            _mockConnectionFactory = Substitute.For<ISqliteConnectionFactory>();

            _mockConnectionFactory.Trainers.Returns(Substitute.For<ITrainerOperations>());
            _mockConnectionFactory.Matches.Returns(Substitute.For<IMatchOperations>());

            // SUT
            _viewModel = new TrainerPageViewModel(_mockLogger, _mockConnectionFactory, _mockAnalysisService);
        }

        [Fact]
        public void TrainerPageViewModel_Constructor_SetsWelcomeMsg()
        {
            // Assert
            _viewModel.ShouldNotBeNull();
            _viewModel.WelcomeMsg.ShouldNotBeNullOrEmpty();
        }

        [Fact]
        public async Task AppearingAsync_NoMatches_ResetsStatsToZero()
        {
            // Arrange
            _mockConnectionFactory.Trainers.GetByNameAsync(Arg.Any<string>())
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

        [Fact]
        public async Task AppearingAsync_WithMatches_CalculatesStats()
        {
            // Arrange
            List<MatchEntry> matches =
            [
                new() { Result = MatchResult.Win },
                new() { Result = MatchResult.Loss },
                new() { Result = MatchResult.Win }
            ];

            _mockConnectionFactory.Trainers.GetByNameAsync(Arg.Any<string>())
                .Returns(Task.FromResult<Trainer?>(new Trainer { Id = 1, Name = "Test" }));
            _mockConnectionFactory.Matches.GetByTrainerIdAsync(1, true)
                .Returns(Task.FromResult(matches));

            _mockAnalysisService.CalculateWinRate(matches, out _, out _, out _)
                .Returns(66.66666666666667);
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
    }
}
