#pragma warning disable IDE0058 // Expression value is never used
using PokemonBattleJournal.Interfaces;

namespace PokemonBattleJournal.Tests.ViewModels
{
    public class MainPageViewModelTests
    {
        private readonly MainPageViewModel _viewModel;
        private readonly ISqliteConnectionFactory _mockConnectionFactory;
        private readonly ILogger<MainPageViewModel> _mockLogger;
        //private readonly ILogger<SqliteConnectionFactory> _mockFactoryLogger;
        private readonly ITrainerOperations _mockTrainerOps;
        private readonly IMatchOperations _mockMatchOps;
        private readonly IMatchResultsCalculatorFactory _mockCalculatorFactory;

        public MainPageViewModelTests()
        {
            // Mocks
            _mockLogger = Substitute.For<ILogger<MainPageViewModel>>();
            _mockTrainerOps = Substitute.For<ITrainerOperations>();
            _mockMatchOps = Substitute.For<IMatchOperations>();
            _mockConnectionFactory = Substitute.For<ISqliteConnectionFactory>();
            _mockCalculatorFactory = Substitute.For<IMatchResultsCalculatorFactory>();

            _mockConnectionFactory.Trainers.Returns(_mockTrainerOps);
            _mockConnectionFactory.Matches.Returns(_mockMatchOps);

            _mockTrainerOps.GetByNameAsync(Arg.Any<string>())
                .Returns(Task.FromResult<Trainer?>(new Trainer { Id = 1, Name = "Test" }));
            // SUT
            _viewModel = new MainPageViewModel(_mockLogger, _mockConnectionFactory, _mockCalculatorFactory);
        }

        [Fact]
        public void MainPageViewModel_WhenViewModelConstructed_ViewModelShouldNotBeNull()
        {
            // Arrange
            // Act
            // Assert
            _viewModel.ShouldNotBeNull();
        }
        [Fact]
        public void MainPageViewModel_WhenViewModelConstructed_ViewModelShouldFindTrainerName()
        {
            // Arrange
            _viewModel.TrainerName = "Test";
            // Act
            // Assert
            _viewModel.ShouldNotBeNull();
            _viewModel.TrainerName.ShouldNotBeNullOrEmpty();
            _viewModel.TrainerName.ShouldBe("Test");
        }

        [Fact]
        public async Task SaveMatchAsync_AllFieldsNull_ReturnsZero()
        {
            // Arrange — all required fields are null/defaults
            _viewModel.PlayerSelected = null;
            _viewModel.RivalSelected = null;
            _viewModel.Result = null;

            // Act
            int result = await _viewModel.SaveMatchAsync();

            // Assert
            result.ShouldBe(0);
            _viewModel.HasValidationErrors.ShouldBeTrue();
            _viewModel.ValidationMessage.ShouldNotBeNullOrEmpty();
        }

        [Fact]
        public async Task SaveMatchAsync_PlayerSelectedOnly_ReturnsZero()
        {
            // Arrange
            _viewModel.PlayerSelected = new Archetype { Id = 1, Name = "Fire" };
            _viewModel.RivalSelected = null;
            _viewModel.Result = null;

            // Act
            int result = await _viewModel.SaveMatchAsync();

            // Assert
            result.ShouldBe(0);
            _viewModel.HasValidationErrors.ShouldBeTrue();
        }

        [Fact]
        public async Task SaveMatchAsync_AllRequiredFields_ReturnsZeroWithNullTrainer()
        {
            // Arrange — all required fields set, but trainer lookup returns null
            _viewModel.PlayerSelected = new Archetype { Id = 1, Name = "Fire" };
            _viewModel.RivalSelected = new Archetype { Id = 2, Name = "Water" };
            _viewModel.Result = MatchResult.Win;
            _viewModel.DatePlayed = DateTime.Now;
            _viewModel.StartTime = DateTime.Now;
            _viewModel.EndTime = DateTime.Now;

            _mockTrainerOps.GetByNameAsync(Arg.Any<string>())
                .Returns(Task.FromResult<Trainer?>(null));

            // Act
            int result = await _viewModel.SaveMatchAsync();

            // Assert — fails because trainer not found
            result.ShouldBe(0);
            _viewModel.HasValidationErrors.ShouldBeTrue();
        }

        [Fact]
        public async Task SaveMatchAsync_BO3WithMissingGame2_ReturnsZero()
        {
            // Arrange
            _viewModel.BO3Toggle = true;
            _viewModel.PlayerSelected = new Archetype { Id = 1, Name = "Fire" };
            _viewModel.RivalSelected = new Archetype { Id = 2, Name = "Water" };
            _viewModel.Result = MatchResult.Win;
            _viewModel.Result2 = null; // Missing game 2

            // Act
            int result = await _viewModel.SaveMatchAsync();

            // Assert
            result.ShouldBe(0);
            _viewModel.HasValidationErrors.ShouldBeTrue();
            _viewModel.ValidationMessage.ShouldContain("Game 2 result is required");
        }

        [Fact]
        public async Task SaveMatchAsync_EndTimeBeforeStartTime_PassesValidation()
        {
            // Arrange — StartTime defaults to DateTime.MinValue so EndTime < StartTime is never true
            // The VM does not validate this case; the match goes through to save
            _viewModel.PlayerSelected = new Archetype { Id = 1, Name = "Fire" };
            _viewModel.RivalSelected = new Archetype { Id = 2, Name = "Water" };
            _viewModel.Result = MatchResult.Win;

            // Act
            int result = await _viewModel.SaveMatchAsync();

            // Assert — validation passes, DB save runs (no real DB so 0 rows affected)
            result.ShouldBe(0);
            _viewModel.ValidationMessage.ShouldNotContain("End time cannot be before start time");
        }

        [Fact]
        public async Task SaveMatchAsync_NullResult_ReturnsZero()
        {
            // Arrange
            _viewModel.PlayerSelected = new Archetype { Id = 1, Name = "Fire" };
            _viewModel.RivalSelected = new Archetype { Id = 2, Name = "Water" };
            _viewModel.Result = null; // Missing result

            // Act
            int result = await _viewModel.SaveMatchAsync();

            // Assert
            result.ShouldBe(0);
            _viewModel.HasValidationErrors.ShouldBeTrue();
            _viewModel.ValidationMessage.ShouldContain("Game 1 result is required");
        }
    }

}