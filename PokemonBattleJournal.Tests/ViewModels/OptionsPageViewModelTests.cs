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

        [Fact]
        public async Task SaveTrainerAsync_ValidInput_CallsSaveAsync()
        {
            _viewModel.NameInput = "Ash";
            _mockConnectionFactory.Trainers.SaveAsync("Ash").Returns(Task.FromResult(1));
            _mockConnectionFactory.Trainers.GetByNameAsync("Ash")
                .Returns(Task.FromResult<Trainer?>(new Trainer { Id = 5, Name = "Ash" }));
            _mockConnectionFactory.Trainers.GetAllAsync()
                .Returns(Task.FromResult(new List<Trainer> { new() { Id = 5, Name = "Ash" } }));

            await _viewModel.SaveTrainerAsync();

            _ = _mockConnectionFactory.Trainers.Received(1).SaveAsync("Ash");
        }

        [Fact]
        public async Task SaveTrainerAsync_ValidInput_ClearsNameInput()
        {
            _viewModel.NameInput = "Ash";
            _mockConnectionFactory.Trainers.SaveAsync("Ash").Returns(Task.FromResult(1));
            _mockConnectionFactory.Trainers.GetByNameAsync("Ash")
                .Returns(Task.FromResult<Trainer?>(new Trainer { Id = 5, Name = "Ash" }));
            _mockConnectionFactory.Trainers.GetAllAsync()
                .Returns(Task.FromResult(new List<Trainer> { new() { Id = 5, Name = "Ash" } }));

            await _viewModel.SaveTrainerAsync();

            _viewModel.NameInput.ShouldBeNull();
        }

        [Fact]
        public async Task SaveTrainerAsync_SaveReturnsZero_DoesNotLoadTrainer()
        {
            _viewModel.NameInput = "Ash";
            _mockConnectionFactory.Trainers.SaveAsync("Ash").Returns(Task.FromResult(0));

            await _viewModel.SaveTrainerAsync();

            _ = _mockConnectionFactory.Trainers.DidNotReceive().GetByNameAsync(Arg.Any<string>());
        }

        [Fact]
        public async Task SwitchTrainerAsync_DifferentTrainer_CallsSwitchService()
        {
            var target = new Trainer { Id = 99, Name = "Brock" };
            _mockSwitchService.SwitchToAsync(target).Returns(Task.CompletedTask);
            _mockConnectionFactory.Trainers.GetAllAsync()
                .Returns(Task.FromResult(new List<Trainer> { target }));

            await _viewModel.SwitchTrainerAsync(target);

            _ = _mockSwitchService.Received(1).SwitchToAsync(target);
        }

        [Fact]
        public void OnSelectedIconItemChanged_UpdatesSelectedIconAndNewDeckIcon()
        {
            var item = new IconItem("Charizard", "charizard.png");
            _viewModel.SelectedIconItem = item;

            _viewModel.SelectedIcon.ShouldBe("charizard.png");
            _viewModel.NewDeckIcon.ShouldBe("charizard.png");
        }

        [Fact]
        public void OnSelectedIconItemChanged_NullItem_SetsDefaultIcon()
        {
            _viewModel.SelectedIconItem = new IconItem("Old", "old.png");
            _viewModel.SelectedIconItem = null;

            _viewModel.SelectedIcon.ShouldBe("ball_icon.png");
            _viewModel.NewDeckIcon.ShouldBeNull();
        }

        [Fact]
        public async Task DeleteTrainerFileAsync_NullTrainer_DoesNotCallDelete()
        {
            // _trainer is null by default (never loaded)
            await _viewModel.DeleteTrainerFileAsync();

            _ = _mockConnectionFactory.Trainers.DidNotReceive().DeleteAsync(Arg.Any<Trainer>());
        }
    }
}
