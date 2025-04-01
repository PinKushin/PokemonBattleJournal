#pragma warning disable IDE0058 // Expression value is never used
namespace PokemonBattleJournal.Tests.ViewModels
{
    public class MainPageViewModelTests
    {
        private readonly MainPageViewModel _viewModel;
        private readonly SqliteConnectionFactory _mockConnectionFactory;
        private readonly ILogger<MainPageViewModel> _mockLogger;
        private readonly ILogger<SqliteConnectionFactory> _mockFactoryLogger;

        public MainPageViewModelTests()
        {
            // Mocks
            _mockLogger = Substitute.For<ILogger<MainPageViewModel>>();
            _mockFactoryLogger = Substitute.For<ILogger<SqliteConnectionFactory>>();
            _mockConnectionFactory = Substitute.For<SqliteConnectionFactory>(_mockFactoryLogger);
            _mockConnectionFactory.GetTrainerByNameAsync(Arg.Any<string>())
                .Returns(Task.FromResult<Trainer?>(new Trainer() { Id = 1, Name = "Test Trainer" }));
            // SUT
            _viewModel = new MainPageViewModel(_mockLogger, _mockConnectionFactory);
        }

        [Fact]
        public void MainPageViewModel_WhenViewModelConstructed_ViewModelShouldNotBeNull()
        {
            // Arrange
            // Act
            // Assert
            _viewModel.ShouldNotBeNull();
        }

    }
}