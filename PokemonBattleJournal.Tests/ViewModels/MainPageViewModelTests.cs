#pragma warning disable IDE0058 // Expression value is never used
using NSubstitute;
using PokemonBattleJournal.Services;
namespace PokemonBattleJournal.Tests.ViewModels
{
    public class MainPageViewModelTests
    {
        private readonly MainPageViewModel _viewModel;
        private readonly SqliteConnectionFactory _mockConnectionFactory;
        private readonly ILogger<MainPageViewModel> _mockLogger;

        public MainPageViewModelTests()
        {
            // Mocks
            _mockLogger = Substitute.For<ILogger<MainPageViewModel>>();
            _mockConnectionFactory = Substitute.For<SqliteConnectionFactory>();
            _mockConnectionFactory.GetTrainerByNameAsync(Arg.Any<string>())
                .Returns(Task.FromResult(new Trainer() { Id = 1, Name = "Test Trainer" }));
            // SUT
            _viewModel = new MainPageViewModel(_mockLogger, _mockConnectionFactory);
        }

        [Fact]
        public void MainPageViewModel_WhenViewModelConstructed_ViewModelShouldNotBeNull()
        {
            // Assert

            _viewModel.ShouldNotBeNull();
        }

        [Fact]
        public async Task MainPageViewModel_CreateMatchEntryAsync_ShouldCreateMatchEntry()
        {
            // Arrange
            _viewModel.PlayerSelected = new() { Id = 1, Name = "Regidrago", ImagePath = "regidrago.png" };
            _viewModel.RivalSelected = new() { Id = 2, Name = "Charizard", ImagePath = "charizard.png" };
            _viewModel.DatePlayed = DateTime.Now;
            _viewModel.TagsSelected = new List<object>() { new Tags() { Name = "Donked Rival" }, new Tags() { Name = "Early Start" } };
            //_viewModel.Match2TagsSelected = new IList<object>() { "Behind Early" };
            //_viewModel.Match3TagsSelected = new IList<object>() { "Donked Rival", "Early Start" };
            _viewModel.Result = MatchResult.Win;
            _viewModel.Result2 = MatchResult.Loss;
            _viewModel.Result3 = MatchResult.Tie;
            _viewModel.UserNoteInput = "Test Note";
            _viewModel.UserNoteInput2 = "Test Note 2";
            _viewModel.UserNoteInput3 = "Test Note 3";
            _viewModel.BO3Toggle = true;
            _viewModel.FirstCheck = true;
            _viewModel.FirstCheck2 = false;
            _viewModel.FirstCheck3 = true;

            // Act
            MatchEntry? matchentry = await _viewModel.CreateMatchEntryAsync();

            // Assert
            matchentry.ShouldNotBeNull();
            //matchentry.Playing.ShouldBeAssignableTo<Archetype>();
            //matchentry.Against.ShouldBeAssignableTo<Archetype>();
            matchentry.Game1.Result.ShouldBe(MatchResult.Win);
            //matchentry.Game2!.Result.ShouldBe(MatchResult.Loss);
            //matchentry.Game3!.Result.ShouldBe(MatchResult.Tie);
            //matchentry.Game1.Turn.ShouldBe(1);
            //matchentry.Game2.Turn.ShouldBe(2);
            //matchentry.Game3.Turn.ShouldBe(1);
            //matchentry.Game1.Tags?.ShouldContain("Early Start");
            //matchentry.Game2.Tags?.ShouldContain("Behind Early");
            //matchentry.Game3.Tags?.ShouldContain("Donked Rival");
            //matchentry.Game3.Tags?.ShouldContain("Early Start");
            //matchentry.Result.ShouldBe(MatchResult.Tie);
            _viewModel.BO3Toggle.ShouldBe(false);
            //_viewModel.Result.ShouldBeNull();
            //_viewModel.Result2.ShouldBeNull();
            //_viewModel.Result3.ShouldBeNull();
            _viewModel.UserNoteInput.ShouldBeNull();
            _viewModel.UserNoteInput2.ShouldBeNull();
            _viewModel.UserNoteInput3.ShouldBeNull();
            _viewModel.TagsSelected.ShouldBeNull();
            _viewModel.Match2TagsSelected.ShouldBeNull();
            _viewModel.Match3TagsSelected.ShouldBeNull();
            _viewModel.FirstCheck.ShouldBe(false);
            _viewModel.FirstCheck2.ShouldBe(false);
            _viewModel.FirstCheck3.ShouldBe(false);
            _viewModel.PlayerSelected.ShouldBeNull();
            _viewModel.RivalSelected.ShouldBeNull();
        }
    }
}