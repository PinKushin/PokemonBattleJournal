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
            _viewModel = new ReadJournalPageViewModel(_mockLogger, _mockConnectionFactory);
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
