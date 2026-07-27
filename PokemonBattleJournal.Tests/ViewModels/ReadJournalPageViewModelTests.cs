namespace PokemonBattleJournal.Tests.ViewModels
{
    public class ReadJournalPageViewModelTests
    {
        private readonly ReadJournalPageViewModel _viewModel;
        private readonly ISqliteConnectionFactory _mockConnectionFactory;
        private readonly ILogger<ReadJournalPageViewModel> _mockLogger;

        public ReadJournalPageViewModelTests()
        {
            // Mocks
            _mockLogger = Substitute.For<ILogger<ReadJournalPageViewModel>>();
            _mockConnectionFactory = Substitute.For<ISqliteConnectionFactory>();

            _mockConnectionFactory.Trainers.Returns(Substitute.For<ITrainerOperations>());
            _mockConnectionFactory.Matches.Returns(Substitute.For<IMatchOperations>());

            // SUT
            _viewModel = new ReadJournalPageViewModel(_mockLogger, _mockConnectionFactory, Substitute.For<ITrainerSwitchService>());
        }

        [Fact]
        public void ReadJournalPageViewModel_Constructor_SetsWelcomeMsg()
        {
            // Assert
            _viewModel.ShouldNotBeNull();
            _viewModel.WelcomeMsg.ShouldNotBeNullOrEmpty();
        }

        [Fact]
        public void LoadMatch_NullSelectedMatch_ResetsDisplay()
        {
            // Arrange
            _viewModel.SelectedMatch = null;

            // Act
            _viewModel.LoadMatch();

            // Assert
            _viewModel.SelectedNote.ShouldBe("Select Match");
            _viewModel.PlayingName.ShouldBe("other");
            _viewModel.AgainstName.ShouldBe("other");
        }

        [Fact]
        public void LoadMatch_ValidMatchWithGame1_PopulatesPlayingAndResult()
        {
            _viewModel.SelectedMatch = new MatchEntry
            {
                Result = MatchResult.Win,
                Playing = new Archetype { Name = "Charizard", ImagePath = "charizard.png" },
                Against = new Archetype { Name = "Gardevoir", ImagePath = "gardevoir.png" },
                Game1 = new Game
                {
                    Result = MatchResult.Win,
                    Notes = "Good start",
                    Tags = [new Tags { Name = "Lucky" }]
                }
            };

            _viewModel.LoadMatch();

            _viewModel.PlayingName.ShouldBe("Charizard");
            _viewModel.AgainstName.ShouldBe("Gardevoir");
            _viewModel.PlayingIconSource.ShouldBe("charizard.png");
            _viewModel.AgainstIconSource.ShouldBe("gardevoir.png");
            _viewModel.OverallResult.ShouldBe(MatchResult.Win);
            _viewModel.ResultGame1.ShouldBe(MatchResult.Win);
            _viewModel.SelectedNote.ShouldBe("Good start");
            _viewModel.HasGame1Tags.ShouldBeTrue();
            _viewModel.Game1TagsInfo.ShouldBe("Game 1: 1 tags");
        }

        [Fact]
        public void LoadMatch_MatchWithNoGames_SetsGameResultsToNull()
        {
            _viewModel.SelectedMatch = new MatchEntry
            {
                Result = MatchResult.Loss,
                Playing = new Archetype { Name = "Fire" },
                Against = new Archetype { Name = "Water" }
            };

            _viewModel.LoadMatch();

            _viewModel.ResultGame1.ShouldBeNull();
            _viewModel.ResultGame2.ShouldBeNull();
            _viewModel.ResultGame3.ShouldBeNull();
            _viewModel.HasGame1Tags.ShouldBeFalse();
        }

        [Fact]
        public async Task AppearingAsync_WithTrainerAndMatches_PopulatesMatchHistory()
        {
            List<MatchEntry> matches =
            [
                new() { Result = MatchResult.Win },
                new() { Result = MatchResult.Loss }
            ];

            _mockConnectionFactory.Trainers.GetByNameAsync(Arg.Any<string>())
                .Returns(Task.FromResult<Trainer?>(new Trainer { Id = 1, Name = "Test" }));
            _mockConnectionFactory.Matches.GetByTrainerIdAsync(1, Arg.Any<bool>())
                .Returns(Task.FromResult(matches));

            await _viewModel.AppearingAsync();

            _viewModel.MatchHistory.ShouldNotBeNull();
            _viewModel.MatchHistory!.Count.ShouldBe(2);
        }

        [Fact]
        public async Task AppearingAsync_NoTrainer_SetsEmptyMatchHistory()
        {
            // Arrange
            _mockConnectionFactory.Trainers.GetByNameAsync(Arg.Any<string>())
                .Returns(Task.FromResult<Trainer?>(null));

            // Act
            await _viewModel.AppearingAsync();

            // Assert
            _viewModel.MatchHistory.ShouldBeNull();
        }
    }
}
