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

    }

}