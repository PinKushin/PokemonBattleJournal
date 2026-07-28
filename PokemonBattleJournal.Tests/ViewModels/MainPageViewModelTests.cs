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
        private readonly ITrainerSwitchService _mockSwitchService;

        public MainPageViewModelTests()
        {
            // Mocks
            _mockLogger = Substitute.For<ILogger<MainPageViewModel>>();
            _mockTrainerOps = Substitute.For<ITrainerOperations>();
            _mockMatchOps = Substitute.For<IMatchOperations>();
            _mockConnectionFactory = Substitute.For<ISqliteConnectionFactory>();
            _mockCalculatorFactory = Substitute.For<IMatchResultsCalculatorFactory>();
            _mockSwitchService = Substitute.For<ITrainerSwitchService>();

            _mockConnectionFactory.Trainers.Returns(_mockTrainerOps);
            _mockConnectionFactory.Matches.Returns(_mockMatchOps);

            _mockTrainerOps.GetByNameAsync(Arg.Any<string>())
                .Returns(Task.FromResult<Trainer?>(new Trainer { Id = 1, Name = "Test" }));
            // SUT
            _viewModel = new MainPageViewModel(_mockLogger, _mockConnectionFactory, _mockCalculatorFactory, _mockSwitchService);
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
            _viewModel.StartTime = DateTime.Now.TimeOfDay;
            _viewModel.EndTime = DateTime.Now.AddMinutes(5).TimeOfDay;

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

        // --- ShowGame3 ---

        [Fact]
        public void ShowGame3_BO3Off_ReturnsFalse()
        {
            _viewModel.BO3Toggle = false;
            _viewModel.Result = MatchResult.Win;
            _viewModel.Result2 = MatchResult.Loss;
            _viewModel.ShowGame3.ShouldBeFalse();
        }

        [Fact]
        public void ShowGame3_ResultsNull_ReturnsFalse()
        {
            _viewModel.BO3Toggle = true;
            _viewModel.Result = null;
            _viewModel.Result2 = null;
            _viewModel.ShowGame3.ShouldBeFalse();
        }

        [Fact]
        public void ShowGame3_BothWin_ReturnsFalse()
        {
            _viewModel.BO3Toggle = true;
            _viewModel.Result = MatchResult.Win;
            _viewModel.Result2 = MatchResult.Win;
            _viewModel.ShowGame3.ShouldBeFalse();
        }

        [Fact]
        public void ShowGame3_SplitResult_ReturnsTrue()
        {
            _viewModel.BO3Toggle = true;
            _viewModel.Result = MatchResult.Win;
            _viewModel.Result2 = MatchResult.Loss;
            _viewModel.ShowGame3.ShouldBeTrue();
        }

        [Fact]
        public void ShowGame3_BothTie_ReturnsTrue()
        {
            // Official Pokemon TCG tournament rule: Tie+Tie means neither player has won 2 games, Game 3 required
            _viewModel.BO3Toggle = true;
            _viewModel.Result = MatchResult.Tie;
            _viewModel.Result2 = MatchResult.Tie;
            _viewModel.ShowGame3.ShouldBeTrue();
        }

        [Fact]
        public void ShowGame3_Game1Tie_ReturnsTrue()
        {
            // Tie in Game 1 means winner undecided regardless of Game 2 — Game 3 required
            _viewModel.BO3Toggle = true;
            _viewModel.Result = MatchResult.Tie;
            _viewModel.Result2 = MatchResult.Win;
            _viewModel.ShowGame3.ShouldBeTrue();
        }

        [Fact]
        public void ShowGame3_Game2Tie_ReturnsTrue()
        {
            // Tie in Game 2 means winner undecided regardless of Game 1 — Game 3 required
            _viewModel.BO3Toggle = true;
            _viewModel.Result = MatchResult.Win;
            _viewModel.Result2 = MatchResult.Tie;
            _viewModel.ShowGame3.ShouldBeTrue();
        }

        [Fact]
        public void ShowGame3_Game1TieGame2Loss_ReturnsTrue()
        {
            _viewModel.BO3Toggle = true;
            _viewModel.Result = MatchResult.Tie;
            _viewModel.Result2 = MatchResult.Loss;
            _viewModel.ShowGame3.ShouldBeTrue();
        }

        [Fact]
        public void ShowGame3_Game1LossGame2Tie_ReturnsTrue()
        {
            _viewModel.BO3Toggle = true;
            _viewModel.Result = MatchResult.Loss;
            _viewModel.Result2 = MatchResult.Tie;
            _viewModel.ShowGame3.ShouldBeTrue();
        }

        // --- SelectGame commands ---

        [Fact]
        public void SelectGame1Command_ResetsToGame1()
        {
            _viewModel.IsGame2Selected = true;
            _viewModel.IsGame1Selected = false;
            _viewModel.SelectGame1Command.Execute(null);
            _viewModel.IsGame1Selected.ShouldBeTrue();
            _viewModel.IsGame2Selected.ShouldBeFalse();
            _viewModel.IsGame3Selected.ShouldBeFalse();
        }

        [Fact]
        public void SelectGame2Command_SetsGame2()
        {
            _viewModel.SelectGame2Command.Execute(null);
            _viewModel.IsGame1Selected.ShouldBeFalse();
            _viewModel.IsGame2Selected.ShouldBeTrue();
            _viewModel.IsGame3Selected.ShouldBeFalse();
        }

        [Fact]
        public void SelectGame3Command_SetsGame3()
        {
            _viewModel.SelectGame3Command.Execute(null);
            _viewModel.IsGame1Selected.ShouldBeFalse();
            _viewModel.IsGame2Selected.ShouldBeFalse();
            _viewModel.IsGame3Selected.ShouldBeTrue();
        }

        // --- ToggleBO3Command ---

        [Fact]
        public void ToggleBO3Command_TogglesBO3Toggle()
        {
            _viewModel.BO3Toggle = false;
            _viewModel.ToggleBO3Command.Execute(null);
            _viewModel.BO3Toggle.ShouldBeTrue();
            _viewModel.ToggleBO3Command.Execute(null);
            _viewModel.BO3Toggle.ShouldBeFalse();
        }

        // --- OnBO3ToggleChanged side effects ---

        [Fact]
        public void OnBO3ToggleChanged_WhenDisabling_ClearsGame2And3Fields()
        {
            _viewModel.BO3Toggle = true;
            _viewModel.Result2 = MatchResult.Win;
            _viewModel.Result3 = MatchResult.Loss;
            _viewModel.UserNoteInput2 = "game 2 note";
            _viewModel.UserNoteInput3 = "game 3 note";

            _viewModel.BO3Toggle = false;

            _viewModel.Result2.ShouldBeNull();
            _viewModel.Result3.ShouldBeNull();
            _viewModel.UserNoteInput2.ShouldBeNull();
            _viewModel.UserNoteInput3.ShouldBeNull();
        }

        [Fact]
        public void OnBO3ToggleChanged_WhenDisabling_ResetsToGame1Selected()
        {
            _viewModel.BO3Toggle = true;
            _viewModel.SelectGame2Command.Execute(null);
            _viewModel.IsGame2Selected.ShouldBeTrue();

            _viewModel.BO3Toggle = false;

            _viewModel.IsGame1Selected.ShouldBeTrue();
            _viewModel.IsGame2Selected.ShouldBeFalse();
            _viewModel.IsGame3Selected.ShouldBeFalse();
        }

        // --- Time guard logic ---

        [Fact]
        public void OnStartTimeChanged_WhenEndTimeBeforeNewStart_ClampsEndTime()
        {
            // Establish a known baseline to avoid current-time defaults interfering
            _viewModel.StartTime = new TimeSpan(9, 0, 0);
            _viewModel.EndTime = new TimeSpan(10, 0, 0); // valid: end > start
            _viewModel.StartTime = new TimeSpan(11, 0, 0); // now start > end → end must clamp
            _viewModel.EndTime.ShouldBe(new TimeSpan(11, 0, 0));
        }

        [Fact]
        public void OnEndTimeChanged_WhenValueBeforeStart_ClampsToStartTime()
        {
            _viewModel.StartTime = new TimeSpan(10, 0, 0);
            _viewModel.EndTime = new TimeSpan(9, 0, 0); // end < start → clamps to start
            _viewModel.EndTime.ShouldBe(new TimeSpan(10, 0, 0));
        }

        // --- HasUnsavedData ---

        [Fact]
        public void HasUnsavedData_DefaultState_ReturnsFalse()
        {
            _viewModel.HasUnsavedData.ShouldBeFalse();
        }

        [Fact]
        public void HasUnsavedData_WhenPlayerSelected_ReturnsTrue()
        {
            _viewModel.PlayerSelected = new Archetype { Id = 1, Name = "Fire" };
            _viewModel.HasUnsavedData.ShouldBeTrue();
        }

        [Fact]
        public void HasUnsavedData_WhenRivalSelected_ReturnsTrue()
        {
            _viewModel.RivalSelected = new Archetype { Id = 2, Name = "Water" };
            _viewModel.HasUnsavedData.ShouldBeTrue();
        }

        [Fact]
        public void HasUnsavedData_WhenNoteEntered_ReturnsTrue()
        {
            _viewModel.UserNoteInput = "some note";
            _viewModel.HasUnsavedData.ShouldBeTrue();
        }

        // --- ToggleFirstCheck commands ---

        [Fact]
        public void ToggleFirstCheckCommand_TogglesFirstCheck()
        {
            _viewModel.FirstCheck = false;
            _viewModel.ToggleFirstCheckCommand.Execute(null);
            _viewModel.FirstCheck.ShouldBeTrue();
            _viewModel.ToggleFirstCheckCommand.Execute(null);
            _viewModel.FirstCheck.ShouldBeFalse();
        }

        [Fact]
        public void ToggleFirstCheck2Command_TogglesFirstCheck2()
        {
            _viewModel.FirstCheck2 = false;
            _viewModel.ToggleFirstCheck2Command.Execute(null);
            _viewModel.FirstCheck2.ShouldBeTrue();
            _viewModel.ToggleFirstCheck2Command.Execute(null);
            _viewModel.FirstCheck2.ShouldBeFalse();
        }

        [Fact]
        public void ToggleFirstCheck3Command_TogglesFirstCheck3()
        {
            _viewModel.FirstCheck3 = false;
            _viewModel.ToggleFirstCheck3Command.Execute(null);
            _viewModel.FirstCheck3.ShouldBeTrue();
            _viewModel.ToggleFirstCheck3Command.Execute(null);
            _viewModel.FirstCheck3.ShouldBeFalse();
        }

        // --- AppearingAsync ---

        [Fact]
        public async Task AppearingAsync_LoadsArchetypes()
        {
            var archetypes = new List<Archetype>
            {
                new() { Id = 1, Name = "Fire" },
                new() { Id = 2, Name = "Water" }
            };
            _mockConnectionFactory.Archetypes.Returns(Substitute.For<IArchetypeOperations>());
            _mockConnectionFactory.Tags.Returns(Substitute.For<ITagOperations>());
            _mockConnectionFactory.Archetypes.GetAllAsync().Returns(Task.FromResult(archetypes));
            _mockConnectionFactory.Tags.GetAllAsync().Returns(Task.FromResult(new List<Tags>()));

            await _viewModel.AppearingAsync();

            _viewModel.Archetypes.ShouldNotBeNull();
            _viewModel.Archetypes!.Count.ShouldBe(2);
        }

        [Fact]
        public async Task AppearingAsync_SetsWelcomeMsg()
        {
            _mockConnectionFactory.Archetypes.Returns(Substitute.For<IArchetypeOperations>());
            _mockConnectionFactory.Tags.Returns(Substitute.For<ITagOperations>());
            _mockConnectionFactory.Archetypes.GetAllAsync().Returns(Task.FromResult(new List<Archetype>()));
            _mockConnectionFactory.Tags.GetAllAsync().Returns(Task.FromResult(new List<Tags>()));

            await _viewModel.AppearingAsync();

            _viewModel.WelcomeMsg.ShouldNotBeNullOrEmpty();
        }
    }

}