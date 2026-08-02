#pragma warning disable IDE0058 // Expression value is never used
using PokemonBattleJournal.Interfaces;
using SQLite;

namespace PokemonBattleJournal.Tests.ViewModels
{
    public class MainPageViewModelTests
    {
        private MainPageViewModel _viewModel = null!;
        private ISqliteConnectionFactory _mockConnectionFactory = null!;
        private ILogger<MainPageViewModel> _mockLogger = null!;
        //private ILogger<SqliteConnectionFactory> _mockFactoryLogger;
        private ITrainerOperations _mockTrainerOps = null!;
        private IMatchOperations _mockMatchOps = null!;
        private IMatchResultsCalculatorFactory _mockCalculatorFactory = null!;
        private ITrainerSwitchService _mockSwitchService = null!;

        [SetUp]
        public void SetUp()
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

        [Test]
        public void MainPageViewModel_WhenViewModelConstructed_ViewModelShouldNotBeNull()
        {
            // Arrange
            // Act
            // Assert
            _viewModel.ShouldNotBeNull();
        }
        [Test]
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

        [Test]
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

        [Test]
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

        [Test]
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

        [Test]
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
            _viewModel.ValidationMessage.ShouldNotBeNull();
            _viewModel.ValidationMessage.ShouldContain("Game 2 result is required");
        }

        [Test]
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
            (_viewModel.ValidationMessage ?? string.Empty).ShouldNotContain("End time cannot be before start time");
        }

        [Test]
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
            _viewModel.ValidationMessage.ShouldNotBeNull();
            _viewModel.ValidationMessage.ShouldContain("Game 1 result is required");
        }

        // --- ShowGame3 ---

        [Test]
        public void ShowGame3_BO3Off_ReturnsFalse()
        {
            _viewModel.BO3Toggle = false;
            _viewModel.Result = MatchResult.Win;
            _viewModel.Result2 = MatchResult.Loss;
            _viewModel.ShowGame3.ShouldBeFalse();
        }

        [Test]
        public void ShowGame3_ResultsNull_ReturnsFalse()
        {
            _viewModel.BO3Toggle = true;
            _viewModel.Result = null;
            _viewModel.Result2 = null;
            _viewModel.ShowGame3.ShouldBeFalse();
        }

        [Test]
        public void ShowGame3_BothWin_ReturnsFalse()
        {
            _viewModel.BO3Toggle = true;
            _viewModel.Result = MatchResult.Win;
            _viewModel.Result2 = MatchResult.Win;
            _viewModel.ShowGame3.ShouldBeFalse();
        }

        [Test]
        public void ShowGame3_SplitResult_ReturnsTrue()
        {
            _viewModel.BO3Toggle = true;
            _viewModel.Result = MatchResult.Win;
            _viewModel.Result2 = MatchResult.Loss;
            _viewModel.ShowGame3.ShouldBeTrue();
        }

        [Test]
        public void ShowGame3_BothTie_ReturnsTrue()
        {
            // Official Pokemon TCG tournament rule: Tie+Tie means neither player has won 2 games, Game 3 required
            _viewModel.BO3Toggle = true;
            _viewModel.Result = MatchResult.Tie;
            _viewModel.Result2 = MatchResult.Tie;
            _viewModel.ShowGame3.ShouldBeTrue();
        }

        [Test]
        public void ShowGame3_Game1Tie_ReturnsTrue()
        {
            // Tie in Game 1 means winner undecided regardless of Game 2 — Game 3 required
            _viewModel.BO3Toggle = true;
            _viewModel.Result = MatchResult.Tie;
            _viewModel.Result2 = MatchResult.Win;
            _viewModel.ShowGame3.ShouldBeTrue();
        }

        [Test]
        public void ShowGame3_Game2Tie_ReturnsTrue()
        {
            // Tie in Game 2 means winner undecided regardless of Game 1 — Game 3 required
            _viewModel.BO3Toggle = true;
            _viewModel.Result = MatchResult.Win;
            _viewModel.Result2 = MatchResult.Tie;
            _viewModel.ShowGame3.ShouldBeTrue();
        }

        [Test]
        public void ShowGame3_Game1TieGame2Loss_ReturnsTrue()
        {
            _viewModel.BO3Toggle = true;
            _viewModel.Result = MatchResult.Tie;
            _viewModel.Result2 = MatchResult.Loss;
            _viewModel.ShowGame3.ShouldBeTrue();
        }

        [Test]
        public void ShowGame3_Game1LossGame2Tie_ReturnsTrue()
        {
            _viewModel.BO3Toggle = true;
            _viewModel.Result = MatchResult.Loss;
            _viewModel.Result2 = MatchResult.Tie;
            _viewModel.ShowGame3.ShouldBeTrue();
        }

        // --- SelectGame commands ---

        [Test]
        public void SelectGame1Command_ResetsToGame1()
        {
            _viewModel.IsGame2Selected = true;
            _viewModel.IsGame1Selected = false;
            _viewModel.SelectGame1Command.Execute(null);
            _viewModel.IsGame1Selected.ShouldBeTrue();
            _viewModel.IsGame2Selected.ShouldBeFalse();
            _viewModel.IsGame3Selected.ShouldBeFalse();
        }

        [Test]
        public void SelectGame2Command_SetsGame2()
        {
            _viewModel.SelectGame2Command.Execute(null);
            _viewModel.IsGame1Selected.ShouldBeFalse();
            _viewModel.IsGame2Selected.ShouldBeTrue();
            _viewModel.IsGame3Selected.ShouldBeFalse();
        }

        [Test]
        public void SelectGame3Command_SetsGame3()
        {
            _viewModel.SelectGame3Command.Execute(null);
            _viewModel.IsGame1Selected.ShouldBeFalse();
            _viewModel.IsGame2Selected.ShouldBeFalse();
            _viewModel.IsGame3Selected.ShouldBeTrue();
        }

        // --- ToggleBO3Command ---

        [Test]
        public void ToggleBO3Command_TogglesBO3Toggle()
        {
            _viewModel.BO3Toggle = false;
            _viewModel.ToggleBO3Command.Execute(null);
            _viewModel.BO3Toggle.ShouldBeTrue();
            _viewModel.ToggleBO3Command.Execute(null);
            _viewModel.BO3Toggle.ShouldBeFalse();
        }

        // --- OnBO3ToggleChanged side effects ---

        [Test]
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

        [Test]
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

        [Test]
        public void OnStartTimeChanged_WhenEndTimeBeforeNewStart_ClampsEndTime()
        {
            // Establish a known baseline to avoid current-time defaults interfering
            _viewModel.StartTime = new TimeSpan(9, 0, 0);
            _viewModel.EndTime = new TimeSpan(10, 0, 0); // valid: end > start
            _viewModel.StartTime = new TimeSpan(11, 0, 0); // now start > end → end must clamp
            _viewModel.EndTime.ShouldBe(new TimeSpan(11, 0, 0));
        }

        [Test]
        public void OnEndTimeChanged_WhenValueBeforeStart_ClampsToStartTime()
        {
            _viewModel.StartTime = new TimeSpan(10, 0, 0);
            _viewModel.EndTime = new TimeSpan(9, 0, 0); // end < start → clamps to start
            _viewModel.EndTime.ShouldBe(new TimeSpan(10, 0, 0));
        }

        // --- HasUnsavedData ---

        [Test]
        public void HasUnsavedData_DefaultState_ReturnsFalse()
        {
            _viewModel.HasUnsavedData.ShouldBeFalse();
        }

        [Test]
        public void HasUnsavedData_WhenPlayerSelected_ReturnsTrue()
        {
            _viewModel.PlayerSelected = new Archetype { Id = 1, Name = "Fire" };
            _viewModel.HasUnsavedData.ShouldBeTrue();
        }

        [Test]
        public void HasUnsavedData_WhenRivalSelected_ReturnsTrue()
        {
            _viewModel.RivalSelected = new Archetype { Id = 2, Name = "Water" };
            _viewModel.HasUnsavedData.ShouldBeTrue();
        }

        [Test]
        public void HasUnsavedData_WhenNoteEntered_ReturnsTrue()
        {
            _viewModel.UserNoteInput = "some note";
            _viewModel.HasUnsavedData.ShouldBeTrue();
        }

        // --- ToggleFirstCheck commands ---

        [Test]
        public void ToggleFirstCheckCommand_TogglesFirstCheck()
        {
            _viewModel.FirstCheck = false;
            _viewModel.ToggleFirstCheckCommand.Execute(null);
            _viewModel.FirstCheck.ShouldBeTrue();
            _viewModel.ToggleFirstCheckCommand.Execute(null);
            _viewModel.FirstCheck.ShouldBeFalse();
        }

        [Test]
        public void ToggleFirstCheck2Command_TogglesFirstCheck2()
        {
            _viewModel.FirstCheck2 = false;
            _viewModel.ToggleFirstCheck2Command.Execute(null);
            _viewModel.FirstCheck2.ShouldBeTrue();
            _viewModel.ToggleFirstCheck2Command.Execute(null);
            _viewModel.FirstCheck2.ShouldBeFalse();
        }

        [Test]
        public void ToggleFirstCheck3Command_TogglesFirstCheck3()
        {
            _viewModel.FirstCheck3 = false;
            _viewModel.ToggleFirstCheck3Command.Execute(null);
            _viewModel.FirstCheck3.ShouldBeTrue();
            _viewModel.ToggleFirstCheck3Command.Execute(null);
            _viewModel.FirstCheck3.ShouldBeFalse();
        }

        // --- SaveMatchAsync success path ---

        private void SetupSuccessfulSave()
        {
            var mockCalculator = Substitute.For<IMatchResultCalculator>();
            mockCalculator.CalculateResult(Arg.Any<MatchResult?>(), Arg.Any<MatchResult?>(), Arg.Any<MatchResult?>())
                .Returns(MatchResult.Win);
            _mockCalculatorFactory.GetCalculator(Arg.Any<bool>()).Returns(mockCalculator);
            _mockTrainerOps.GetActiveAsync()
                .Returns(Task.FromResult<Trainer?>(new Trainer { Id = 1, Name = "Test" }));
            _viewModel.TrainerName = "Test";
            _mockMatchOps.SaveAsync(Arg.Any<MatchEntry>(), Arg.Any<List<Game>>())
                .Returns(Task.FromResult(1));
        }

        [Test]
        public async Task SaveMatchAsync_SuccessfulSave_ClearsFormFields()
        {
            SetupSuccessfulSave();
            _viewModel.PlayerSelected = new Archetype { Id = 1, Name = "Fire" };
            _viewModel.RivalSelected = new Archetype { Id = 2, Name = "Water" };
            _viewModel.Result = MatchResult.Win;
            _viewModel.UserNoteInput = "good game";
            _viewModel.FirstCheck = true;

            await _viewModel.SaveMatchAsync();

            _viewModel.PlayerSelected.ShouldBeNull();
            _viewModel.RivalSelected.ShouldBeNull();
            _viewModel.Result.ShouldBeNull();
            _viewModel.UserNoteInput.ShouldBeEmpty();
            _viewModel.FirstCheck.ShouldBeFalse();
        }

        [Test]
        public async Task SaveMatchAsync_BO3SuccessfulSave_ResetsBO3State()
        {
            SetupSuccessfulSave();
            _viewModel.BO3Toggle = true;
            _viewModel.PlayerSelected = new Archetype { Id = 1, Name = "Fire" };
            _viewModel.RivalSelected = new Archetype { Id = 2, Name = "Water" };
            _viewModel.Result = MatchResult.Win;
            _viewModel.Result2 = MatchResult.Loss;
            _viewModel.Result3 = MatchResult.Win;
            _viewModel.UserNoteInput2 = "game 2 note";
            _viewModel.FirstCheck2 = true;

            await _viewModel.SaveMatchAsync();

            _viewModel.BO3Toggle.ShouldBeFalse();
            _viewModel.Result2.ShouldBeNull();
            _viewModel.Result3.ShouldBeNull();
            _viewModel.UserNoteInput2.ShouldBeNull();
            _viewModel.FirstCheck2.ShouldBeFalse();
        }

        // --- AppearingAsync ---

        [Test]
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

        [Test]
        public async Task AppearingAsync_SetsWelcomeMsg()
        {
            _mockConnectionFactory.Archetypes.Returns(Substitute.For<IArchetypeOperations>());
            _mockConnectionFactory.Tags.Returns(Substitute.For<ITagOperations>());
            _mockConnectionFactory.Archetypes.GetAllAsync().Returns(Task.FromResult(new List<Archetype>()));
            _mockConnectionFactory.Tags.GetAllAsync().Returns(Task.FromResult(new List<Tags>()));

            await _viewModel.AppearingAsync();

            _viewModel.WelcomeMsg.ShouldNotBeNullOrEmpty();
        }

        [Test]
        public async Task AppearingAsync_LoadsTags()
        {
            var tags = new List<Tags>
            {
                new() { Id = 1, Name = "Aggro" },
                new() { Id = 2, Name = "Lucky" }
            };
            _mockConnectionFactory.Archetypes.Returns(Substitute.For<IArchetypeOperations>());
            _mockConnectionFactory.Tags.Returns(Substitute.For<ITagOperations>());
            _mockConnectionFactory.Archetypes.GetAllAsync().Returns(Task.FromResult(new List<Archetype>()));
            _mockConnectionFactory.Tags.GetAllAsync().Returns(Task.FromResult(tags));

            await _viewModel.AppearingAsync();

            _viewModel.TagCollection.ShouldNotBeNull();
            _viewModel.TagCollection!.Count.ShouldBe(2);
        }

        // ---------------------------------------------------------------------------
        // HasUnsavedData
        // ---------------------------------------------------------------------------

        [Test]
        public void HasUnsavedData_NothingSet_ReturnsFalse()
        {
            _viewModel.HasUnsavedData.ShouldBeFalse();
        }

        [Test]
        public void HasUnsavedData_PlayerSelected_ReturnsTrue()
        {
            _viewModel.PlayerSelected = new Archetype { Id = 1, Name = "Fire" };

            _viewModel.HasUnsavedData.ShouldBeTrue();
        }

        [Test]
        public void HasUnsavedData_RivalSelected_ReturnsTrue()
        {
            _viewModel.RivalSelected = new Archetype { Id = 2, Name = "Water" };

            _viewModel.HasUnsavedData.ShouldBeTrue();
        }

        [Test]
        public void HasUnsavedData_NonEmptyUserNote_ReturnsTrue()
        {
            _viewModel.UserNoteInput = "Good game";

            _viewModel.HasUnsavedData.ShouldBeTrue();
        }

        [Test]
        public void HasUnsavedData_TagsSelected_ReturnsTrue()
        {
            _viewModel.TagsSelected = [new Tags { Name = "Lucky" }];

            _viewModel.HasUnsavedData.ShouldBeTrue();
        }

        // ---------------------------------------------------------------------------
        // OnTrainerChanged
        // ---------------------------------------------------------------------------

        [Test]
        public void OnTrainerChanged_EventRaised_UpdatesTrainerName()
        {
            var trainer = new Trainer { Id = 5, Name = "Gary" };

            _mockSwitchService.TrainerChanged += Raise.Event<EventHandler<Trainer>>(this, trainer);

            _viewModel.TrainerName.ShouldBe("Gary");
        }

        [Test]
        public void OnTrainerChanged_EventRaised_UpdatesWelcomeMsg()
        {
            var trainer = new Trainer { Id = 5, Name = "Gary" };

            _mockSwitchService.TrainerChanged += Raise.Event<EventHandler<Trainer>>(this, trainer);

            _viewModel.WelcomeMsg.ShouldBe("Welcome Gary");
        }

        [Test]
        public void OnTrainerChanged_EventRaised_ResetsPlayerSelected()
        {
            _viewModel.PlayerSelected = new Archetype { Id = 1, Name = "Fire" };
            var trainer = new Trainer { Id = 5, Name = "Gary" };

            _mockSwitchService.TrainerChanged += Raise.Event<EventHandler<Trainer>>(this, trainer);

            _viewModel.PlayerSelected.ShouldBeNull();
        }

        [Test]
        public void OnTrainerChanged_EventRaised_ResetsBO3Toggle()
        {
            _viewModel.BO3Toggle = true;
            var trainer = new Trainer { Id = 5, Name = "Gary" };

            _mockSwitchService.TrainerChanged += Raise.Event<EventHandler<Trainer>>(this, trainer);

            _viewModel.BO3Toggle.ShouldBeFalse();
        }

        // --- SaveMatchAsync additional paths ---

        [Test]
        public async Task SaveMatchAsync_BO3SplitWithNoGame3_ReturnsZero()
        {
            // BO3, Win+Loss split, ShowGame3=true, but Result3=null → validation fails
            _viewModel.BO3Toggle = true;
            _viewModel.PlayerSelected = new Archetype { Id = 1, Name = "Fire" };
            _viewModel.RivalSelected = new Archetype { Id = 2, Name = "Water" };
            _viewModel.Result = MatchResult.Win;
            _viewModel.Result2 = MatchResult.Loss;
            _viewModel.Result3 = null;

            int result = await _viewModel.SaveMatchAsync();

            result.ShouldBe(0);
            _viewModel.HasValidationErrors.ShouldBeTrue();
            _viewModel.ValidationMessage!.ShouldContain("Game 3 result is required");
        }

        [Test]
        public async Task SaveMatchAsync_SaveReturnsZero_SetsFailureMessage()
        {
            // All validation passes, DB reports 0 rows affected → failure path
            var mockCalculator = Substitute.For<IMatchResultCalculator>();
            mockCalculator.CalculateResult(Arg.Any<MatchResult?>(), Arg.Any<MatchResult?>(), Arg.Any<MatchResult?>())
                .Returns(MatchResult.Win);
            _mockCalculatorFactory.GetCalculator(Arg.Any<bool>()).Returns(mockCalculator);
            _mockTrainerOps.GetActiveAsync().Returns(Task.FromResult<Trainer?>(new Trainer { Id = 1, Name = "Test" }));
            _viewModel.TrainerName = "Test";
            _mockMatchOps.SaveAsync(Arg.Any<MatchEntry>(), Arg.Any<List<Game>>()).Returns(Task.FromResult(0));

            _viewModel.PlayerSelected = new Archetype { Id = 1, Name = "Fire" };
            _viewModel.RivalSelected = new Archetype { Id = 2, Name = "Water" };
            _viewModel.Result = MatchResult.Win;

            int result = await _viewModel.SaveMatchAsync();

            result.ShouldBe(0);
            _viewModel.HasValidationErrors.ShouldBeTrue();
            _viewModel.SavedFileDisplay.ShouldBe("Failed to save match");
        }

        [Test]
        public async Task SaveMatchAsync_ArgumentExceptionFromSave_SetsValidationMessage()
        {
            var mockCalculator = Substitute.For<IMatchResultCalculator>();
            mockCalculator.CalculateResult(Arg.Any<MatchResult?>(), Arg.Any<MatchResult?>(), Arg.Any<MatchResult?>())
                .Returns(MatchResult.Win);
            _mockCalculatorFactory.GetCalculator(Arg.Any<bool>()).Returns(mockCalculator);
            _mockTrainerOps.GetActiveAsync().Returns(Task.FromResult<Trainer?>(new Trainer { Id = 1, Name = "Test" }));
            _viewModel.TrainerName = "Test";
            _mockMatchOps.SaveAsync(Arg.Any<MatchEntry>(), Arg.Any<List<Game>>())
                .Returns(Task.FromException<int>(new ArgumentException("invalid field")));

            _viewModel.PlayerSelected = new Archetype { Id = 1, Name = "Fire" };
            _viewModel.RivalSelected = new Archetype { Id = 2, Name = "Water" };
            _viewModel.Result = MatchResult.Win;

            int result = await _viewModel.SaveMatchAsync();

            result.ShouldBe(0);
            _viewModel.HasValidationErrors.ShouldBeTrue();
            _viewModel.SavedFileDisplay.ShouldBe("Save Failed: Invalid Data");
        }

        [Test]
        public async Task SaveMatchAsync_SQLiteExceptionFromSave_SetsValidationMessage()
        {
            var mockCalculator = Substitute.For<IMatchResultCalculator>();
            mockCalculator.CalculateResult(Arg.Any<MatchResult?>(), Arg.Any<MatchResult?>(), Arg.Any<MatchResult?>())
                .Returns(MatchResult.Win);
            _mockCalculatorFactory.GetCalculator(Arg.Any<bool>()).Returns(mockCalculator);
            _mockTrainerOps.GetActiveAsync().Returns(Task.FromResult<Trainer?>(new Trainer { Id = 1, Name = "Test" }));
            _viewModel.TrainerName = "Test";
            _mockMatchOps.SaveAsync(Arg.Any<MatchEntry>(), Arg.Any<List<Game>>())
                .Returns(Task.FromException<int>(SQLiteException.New(SQLite3.Result.Error, "disk full")));

            _viewModel.PlayerSelected = new Archetype { Id = 1, Name = "Fire" };
            _viewModel.RivalSelected = new Archetype { Id = 2, Name = "Water" };
            _viewModel.Result = MatchResult.Win;

            int result = await _viewModel.SaveMatchAsync();

            result.ShouldBe(0);
            _viewModel.HasValidationErrors.ShouldBeTrue();
            _viewModel.SavedFileDisplay.ShouldBe("Save Failed: Database Error");
        }

        [Test]
        public async Task SaveMatchAsync_UnexpectedExceptionFromSave_SetsValidationMessage()
        {
            var mockCalculator = Substitute.For<IMatchResultCalculator>();
            mockCalculator.CalculateResult(Arg.Any<MatchResult?>(), Arg.Any<MatchResult?>(), Arg.Any<MatchResult?>())
                .Returns(MatchResult.Win);
            _mockCalculatorFactory.GetCalculator(Arg.Any<bool>()).Returns(mockCalculator);
            _mockTrainerOps.GetActiveAsync().Returns(Task.FromResult<Trainer?>(new Trainer { Id = 1, Name = "Test" }));
            _viewModel.TrainerName = "Test";
            _mockMatchOps.SaveAsync(Arg.Any<MatchEntry>(), Arg.Any<List<Game>>())
                .Returns(Task.FromException<int>(new InvalidOperationException("unexpected")));

            _viewModel.PlayerSelected = new Archetype { Id = 1, Name = "Fire" };
            _viewModel.RivalSelected = new Archetype { Id = 2, Name = "Water" };
            _viewModel.Result = MatchResult.Win;

            int result = await _viewModel.SaveMatchAsync();

            result.ShouldBe(0);
            _viewModel.HasValidationErrors.ShouldBeTrue();
            _viewModel.SavedFileDisplay.ShouldBe("Save Failed: Unexpected Error");
        }

        [Test]
        public async Task AppearingAsync_ActiveTrainerFromSwitchService_DoesNotCallGetActiveAsync()
        {
            var trainer = new Trainer { Id = 5, Name = "Misty" };
            _mockSwitchService.ActiveTrainer.Returns(trainer);
            _mockConnectionFactory.Archetypes.Returns(Substitute.For<IArchetypeOperations>());
            _mockConnectionFactory.Tags.Returns(Substitute.For<ITagOperations>());
            _mockConnectionFactory.Archetypes.GetAllAsync().Returns(Task.FromResult(new List<Archetype>()));
            _mockConnectionFactory.Tags.GetAllAsync().Returns(Task.FromResult(new List<Tags>()));

            await _viewModel.AppearingAsync();

            await _mockConnectionFactory.Trainers.DidNotReceive().GetActiveAsync();
            _viewModel.TrainerName.ShouldBe("Misty");
        }

        [Test]
        public void DisappearingCommand_DoesNotThrow()
        {
            Should.NotThrow(() => _viewModel.DisappearingCommand.Execute(null));
        }

        [Test]
        public void OnBO3ToggleChanged_WhenDisabling_ClearsMatch2And3TagsSelected()
        {
            _viewModel.BO3Toggle = true;
            _viewModel.Match2TagsSelected = [new Tags { Name = "Lucky" }];
            _viewModel.Match3TagsSelected = [new Tags { Name = "Aggro" }];

            _viewModel.BO3Toggle = false;

            _viewModel.Match2TagsSelected.ShouldBeNull();
            _viewModel.Match3TagsSelected.ShouldBeNull();
        }
    }

}