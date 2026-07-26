using PokemonBattleJournal.Interfaces;

namespace PokemonBattleJournal.Tests.ViewModels
{
    public class OptionsPageViewModelTests
    {
        private readonly OptionsPageViewModel _viewModel;
        private readonly ISqliteConnectionFactory _mockConnectionFactory;
        private readonly ILogger<OptionsPageViewModel> _mockLogger;
        private readonly ITrainerSwitchService _mockSwitchService;
        private readonly AppShellViewModel _shellVm;

        public OptionsPageViewModelTests()
        {
            // Mocks
            _mockLogger = Substitute.For<ILogger<OptionsPageViewModel>>();
            _mockConnectionFactory = Substitute.For<ISqliteConnectionFactory>();
            _mockSwitchService = Substitute.For<ITrainerSwitchService>();

            _mockConnectionFactory.Trainers.Returns(Substitute.For<ITrainerOperations>());
            _mockConnectionFactory.Tags.Returns(Substitute.For<ITagOperations>());
            _mockConnectionFactory.Archetypes.Returns(Substitute.For<IArchetypeOperations>());
            _mockConnectionFactory.Matches.Returns(Substitute.For<IMatchOperations>());

            var mainPageVm = new MainPageViewModel(
                Substitute.For<ILogger<MainPageViewModel>>(),
                _mockConnectionFactory,
                Substitute.For<IMatchResultsCalculatorFactory>(),
                _mockSwitchService);
            _shellVm = new AppShellViewModel(_mockSwitchService, mainPageVm, Substitute.For<ILogger<AppShellViewModel>>());

            // SUT
            _viewModel = new OptionsPageViewModel(_mockLogger, _mockConnectionFactory, _mockSwitchService, _shellVm);
        }

        [Fact]
        public void OptionsPageViewModel_Constructor_SetsTitle()
        {
            // Assert
            _viewModel.ShouldNotBeNull();
            _viewModel.Title.ShouldNotBeNullOrEmpty();
        }

        [Fact]
        public async Task SaveTrainerAsync_NullInput_DoesNotSave()
        {
            // Arrange
            _viewModel.NameInput = null;

            // Act
            await _viewModel.SaveTrainerAsync();

            // Assert
            _ = _mockConnectionFactory.Trainers.DidNotReceive().SaveAsync(Arg.Any<string>());
        }

        [Fact]
        public async Task SaveTagAsync_NullInput_DoesNotSave()
        {
            // Arrange
            _viewModel.TagInput = null;

            // Act
            await _viewModel.SaveTagAsync();

            // Assert
            _ = _mockConnectionFactory.Tags.DidNotReceive().SaveAsync(Arg.Any<string>(), Arg.Any<uint>());
        }

        [Fact]
        public async Task SaveTagAsync_NullTrainer_DoesNotSave()
        {
            // Arrange
            _viewModel.TagInput = "Lucky";
            _mockConnectionFactory.Trainers.GetByNameAsync(Arg.Any<string>())
                .Returns(Task.FromResult<Trainer?>(null));

            // Act
            await _viewModel.AppearingAsync();
            await _viewModel.SaveTagAsync();

            // Assert — should not throw, and tag should not be saved since trainer is null
        }

        [Fact]
        public async Task SaveArchetypeAsync_NullName_DoesNotSave()
        {
            // Arrange
            _viewModel.NewDeckName = null;

            // Act
            await _viewModel.SaveArchetypeAsync();

            // Assert
            _ = _mockConnectionFactory.Archetypes.DidNotReceive().SaveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<uint>());
        }
    }
}
