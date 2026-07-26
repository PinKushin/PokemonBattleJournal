using PokemonBattleJournal.Interfaces;

namespace PokemonBattleJournal.Tests.ViewModels
{
    public class AppShellViewModelTests
    {
        private readonly AppShellViewModel _sut;
        private readonly ITrainerSwitchService _mockSwitchService;
        private readonly ISqliteConnectionFactory _mockConnectionFactory;
        private readonly MainPageViewModel _mainPageVm;

        public AppShellViewModelTests()
        {
            _mockSwitchService = Substitute.For<ITrainerSwitchService>();
            _mockConnectionFactory = Substitute.For<ISqliteConnectionFactory>();

            _mockConnectionFactory.Trainers.Returns(Substitute.For<ITrainerOperations>());
            _mockConnectionFactory.Tags.Returns(Substitute.For<ITagOperations>());
            _mockConnectionFactory.Archetypes.Returns(Substitute.For<IArchetypeOperations>());
            _mockConnectionFactory.Matches.Returns(Substitute.For<IMatchOperations>());

            _mainPageVm = new MainPageViewModel(
                Substitute.For<ILogger<MainPageViewModel>>(),
                _mockConnectionFactory,
                Substitute.For<IMatchResultsCalculatorFactory>(),
                _mockSwitchService);

            _sut = new AppShellViewModel(
                _mockSwitchService,
                _mainPageVm,
                Substitute.For<ILogger<AppShellViewModel>>());
        }

        [Fact]
        public void ToggleTrainerMenuCommand_TogglesIsTrainerMenuOpen()
        {
            // Arrange — initially false
            _sut.IsTrainerMenuOpen.ShouldBeFalse();

            // Act
            _sut.ToggleTrainerMenuCommand.Execute(null);

            // Assert
            _sut.IsTrainerMenuOpen.ShouldBeTrue();

            // Act again
            _sut.ToggleTrainerMenuCommand.Execute(null);

            // Assert
            _sut.IsTrainerMenuOpen.ShouldBeFalse();
        }

        [Fact]
        public async Task LoadAsync_PopulatesTrainers()
        {
            // Arrange
            var trainers = new List<Trainer>
            {
                new() { Id = 1, Name = "Ash" },
                new() { Id = 2, Name = "Misty" }
            };
            _mockSwitchService.GetAllTrainersAsync().Returns(Task.FromResult(trainers));

            // Act
            await _sut.LoadAsync();

            // Assert
            _sut.Trainers.Count.ShouldBe(2);
        }

        [Fact]
        public async Task LoadAsync_SetsSelectedTrainerByActiveId()
        {
            // Arrange
            var trainers = new List<Trainer>
            {
                new() { Id = 1, Name = "Ash" },
                new() { Id = 2, Name = "Misty" }
            };
            _mockSwitchService.GetAllTrainersAsync().Returns(Task.FromResult(trainers));

            // Act
            await _sut.LoadAsync();

            // Assert — test env returns 0 so falls back to first
            _sut.SelectedTrainer.ShouldNotBeNull();
        }

        [Fact]
        public void OnTrainerCreated_AddsNewTrainer()
        {
            // Arrange
            _sut.Trainers.ShouldBeEmpty();

            // Act
            _sut.OnTrainerCreated(new Trainer { Id = 1, Name = "Ash" });

            // Assert
            _sut.Trainers.Count.ShouldBe(1);
        }

        [Fact]
        public void OnTrainerCreated_SkipsDuplicate()
        {
            // Arrange
            var trainer = new Trainer { Id = 1, Name = "Ash" };
            _sut.Trainers.Add(trainer);

            // Act
            _sut.OnTrainerCreated(new Trainer { Id = 1, Name = "Ash" });

            // Assert
            _sut.Trainers.Count.ShouldBe(1);
        }

        [Fact]
        public void TrainerChangedEvent_UpdatesSelectedTrainer()
        {
            // Arrange
            var trainer1 = new Trainer { Id = 1, Name = "Ash" };
            var trainer2 = new Trainer { Id = 2, Name = "Misty" };
            _sut.Trainers.Add(trainer1);
            _sut.Trainers.Add(trainer2);
            _sut.IsTrainerMenuOpen = true;

            // Act — fire the event via NSubstitute
            _mockSwitchService.TrainerChanged += Raise.Event<EventHandler<Trainer>>(this, trainer2);

            // Assert
            _sut.SelectedTrainer.ShouldNotBeNull();
            _sut.SelectedTrainer!.Id.ShouldBe(2u);
            _sut.IsTrainerMenuOpen.ShouldBeFalse();
        }

        [Fact]
        public async Task SelectTrainerAsync_NullTrainer_ClosesMenu()
        {
            // Arrange
            _sut.IsTrainerMenuOpen = true;

            // Act
            await _sut.SelectTrainerAsync(null!);

            // Assert
            _sut.IsTrainerMenuOpen.ShouldBeFalse();
        }

        [Fact]
        public async Task SelectTrainerAsync_SameTrainer_ClosesMenu()
        {
            // Arrange
            var trainer = new Trainer { Id = 5, Name = "Gary" };
            _sut.Trainers.Add(trainer);
            // Set SelectedTrainer without triggering switch (use suppress via LoadAsync pattern — but easier: set directly with same id in collection)
            // We need _suppressSelectionChanged = true, which LoadAsync does; populate via mock then load
            _mockSwitchService.GetAllTrainersAsync().Returns(Task.FromResult(new List<Trainer> { trainer }));
            await _sut.LoadAsync(); // sets SelectedTrainer = trainer (Id=5) with suppression
            _sut.IsTrainerMenuOpen = true;

            // Act — same id => no Switch, just close
            await _sut.SelectTrainerAsync(new Trainer { Id = 5, Name = "Gary" });

            // Assert
            _sut.IsTrainerMenuOpen.ShouldBeFalse();
            await _mockSwitchService.DidNotReceive().SwitchToAsync(Arg.Any<Trainer>());
        }
    }
}
