using PokemonBattleJournal.Interfaces;

namespace PokemonBattleJournal.Tests.ViewModels
{
    public class OptionsPageViewModelTests
    {
        private OptionsPageViewModel _viewModel = null!;
        private ISqliteConnectionFactory _mockConnectionFactory = null!;
        private ILogger<OptionsPageViewModel> _mockLogger = null!;
        private ITrainerSwitchService _mockSwitchService = null!;
        private AppShellViewModel _shellVm = null!;

        [SetUp]
        public void SetUp()
        {
            // Mocks
            _mockLogger = Substitute.For<ILogger<OptionsPageViewModel>>();
            _mockConnectionFactory = Substitute.For<ISqliteConnectionFactory>();
            _mockSwitchService = Substitute.For<ITrainerSwitchService>();

            _mockConnectionFactory.Trainers.Returns(Substitute.For<ITrainerOperations>());
            _mockConnectionFactory.Tags.Returns(Substitute.For<ITagOperations>());
            _mockConnectionFactory.Archetypes.Returns(Substitute.For<IArchetypeOperations>());
            _mockConnectionFactory.Matches.Returns(Substitute.For<IMatchOperations>());

            MainPageViewModel mainPageVm = new MainPageViewModel(
                Substitute.For<ILogger<MainPageViewModel>>(),
                _mockConnectionFactory,
                Substitute.For<IMatchResultsCalculatorFactory>(),
                _mockSwitchService, Substitute.For<IErrorHandler>());
            _shellVm = new AppShellViewModel(
                _mockSwitchService,
                mainPageVm,
                Substitute.For<ILogger<AppShellViewModel>>());

            // SUT
            _viewModel = new OptionsPageViewModel(_mockLogger, _mockConnectionFactory, _mockSwitchService, _shellVm, Substitute.For<ITrainerHillImportService>(), Substitute.For<IExportService>(), Substitute.For<IRestoreService>(), Substitute.For<IErrorHandler>(),
                new PokemonBattleJournal.Logging.SentryPerformanceMonitor());
        }

        [Test]
        public void OptionsPageViewModel_Constructor_SetsTitle()
        {
            // Assert
            _viewModel.ShouldNotBeNull();
            _viewModel.Title.ShouldNotBeNullOrEmpty();
        }

        [Test]
        public async Task SaveTrainerAsync_NullInput_DoesNotSave()
        {
            // Arrange
            _viewModel.NameInput = null;

            // Act
            await _viewModel.SaveTrainerAsync();

            // Assert
            _ = _mockConnectionFactory.Trainers.DidNotReceive().SaveAsync(Arg.Any<string>());
        }

        [Test]
        public async Task SaveTagAsync_NullInput_DoesNotSave()
        {
            // Arrange
            _viewModel.TagInput = null;

            // Act
            await _viewModel.SaveTagAsync();

            // Assert
            _ = _mockConnectionFactory.Tags.DidNotReceive().SaveAsync(Arg.Any<string>(), Arg.Any<uint>());
        }

        [Test]
        public async Task SaveTagAsync_NullTrainer_DoesNotSave()
        {
            // Arrange
            _viewModel.TagInput = "Lucky";
            _mockConnectionFactory.Trainers.GetByNameAsync(Arg.Any<string>())
                .Returns(Task.FromResult<Trainer?>(null));

            // Act
            await _viewModel.AppearingAsync();
            await _viewModel.SaveTagAsync();

            // Assert — the sibling guard-logging test pins that this path WARNS; this one pins
            // that it also declines the write, which is the behaviour the name claims.
            _ = _mockConnectionFactory.Tags.DidNotReceive().SaveAsync(Arg.Any<string>(), Arg.Any<uint>());
        }

        [Test]
        public async Task SaveArchetypeAsync_NullName_DoesNotSave()
        {
            // Arrange
            _viewModel.NewDeckName = null;

            // Act
            await _viewModel.SaveArchetypeAsync();

            // Assert
            _ = _mockConnectionFactory.Archetypes.DidNotReceive().SaveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<uint>());
        }

        [Test]
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

        [Test]
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

        [Test]
        public async Task SaveTrainerAsync_SaveReturnsZero_DoesNotLoadTrainer()
        {
            _viewModel.NameInput = "Ash";
            _mockConnectionFactory.Trainers.SaveAsync("Ash").Returns(Task.FromResult(0));

            await _viewModel.SaveTrainerAsync();

            _ = _mockConnectionFactory.Trainers.DidNotReceive().GetByNameAsync(Arg.Any<string>());
        }

        [Test]
        public async Task SwitchTrainerAsync_DifferentTrainer_CallsSwitchService()
        {
            Trainer target = new Trainer { Id = 99, Name = "Brock" };
            _mockSwitchService.SwitchToAsync(target).Returns(Task.CompletedTask);
            _mockConnectionFactory.Trainers.GetAllAsync()
                .Returns(Task.FromResult(new List<Trainer> { target }));

            await _viewModel.SwitchTrainerAsync(target);

            _ = _mockSwitchService.Received(1).SwitchToAsync(target);
        }

        [Test]
        public void OnSelectedIconItemChanged_UpdatesSelectedIconAndNewDeckIcon()
        {
            IconItem item = new IconItem("Charizard", "charizard.png");
            _viewModel.SelectedIconItem = item;

            _viewModel.SelectedIcon.ShouldBe("charizard.png");
            _viewModel.NewDeckIcon.ShouldBe("charizard.png");
        }

        [Test]
        public void OnSelectedIconItemChanged_NullItem_SetsDefaultIcon()
        {
            _viewModel.SelectedIconItem = new IconItem("Old", "old.png");
            _viewModel.SelectedIconItem = null;

            _viewModel.SelectedIcon.ShouldBe("ball_icon.png");
            _viewModel.NewDeckIcon.ShouldBeNull();
        }

        [Test]
        public async Task DeleteTrainerFileAsync_NullTrainer_DoesNotCallDelete()
        {
            // _trainer is null by default (never loaded)
            await _viewModel.DeleteTrainerFileAsync();

            _ = _mockConnectionFactory.Trainers.DidNotReceive().DeleteAsync(Arg.Any<Trainer>());
        }

        [Test]
        public async Task SaveAllAsync_AllInputsNull_DoesNotCallAnyService()
        {
            _viewModel.NameInput = null;
            _viewModel.TagInput = null;
            _viewModel.NewDeckName = null;

            await _viewModel.SaveAllAsync();

            _ = _mockConnectionFactory.Trainers.DidNotReceive().SaveAsync(Arg.Any<string>());
            _ = _mockConnectionFactory.Tags.DidNotReceive().SaveAsync(Arg.Any<string>(), Arg.Any<uint>());
            _ = _mockConnectionFactory.Archetypes.DidNotReceive().SaveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<uint>());
        }

        [Test]
        public async Task SaveAllAsync_ValidTrainerInput_CallsTrainerSave()
        {
            _viewModel.NameInput = "Ash";
            _mockConnectionFactory.Trainers.SaveAsync("Ash").Returns(Task.FromResult(1));
            _mockConnectionFactory.Trainers.GetByNameAsync("Ash")
                .Returns(Task.FromResult<Trainer?>(new Trainer { Id = 5, Name = "Ash" }));
            _mockConnectionFactory.Trainers.GetAllAsync()
                .Returns(Task.FromResult(new List<Trainer> { new() { Id = 5, Name = "Ash" } }));

            await _viewModel.SaveAllAsync();

            _ = _mockConnectionFactory.Trainers.Received(1).SaveAsync("Ash");
        }

        [Test]
        public async Task SaveTagAsync_WithTrainerSet_CallsTagSave()
        {
            Trainer trainer = new Trainer { Id = 3, Name = "Misty" };
            _mockConnectionFactory.Trainers.GetActiveAsync()
                .Returns(Task.FromResult<Trainer?>(trainer));
            _mockConnectionFactory.Trainers.GetAllAsync()
                .Returns(Task.FromResult(new List<Trainer> { trainer }));
            _mockConnectionFactory.Tags.SaveAsync(Arg.Any<string>(), Arg.Any<uint>())
                .Returns(Task.FromResult(1));

            await _viewModel.AppearingAsync();   // sets _trainer
            _viewModel.TagInput = "Aggro";

            await _viewModel.SaveTagAsync();

            _ = _mockConnectionFactory.Tags.Received(1).SaveAsync("Aggro", trainer.Id);
        }

        [Test]
        public async Task SaveTagAsync_WithTrainer_ClearsTagInput()
        {
            Trainer trainer = new Trainer { Id = 3, Name = "Misty" };
            _mockConnectionFactory.Trainers.GetActiveAsync()
                .Returns(Task.FromResult<Trainer?>(trainer));
            _mockConnectionFactory.Trainers.GetAllAsync()
                .Returns(Task.FromResult(new List<Trainer> { trainer }));
            _mockConnectionFactory.Tags.SaveAsync(Arg.Any<string>(), Arg.Any<uint>())
                .Returns(Task.FromResult(1));

            await _viewModel.AppearingAsync();
            _viewModel.TagInput = "Aggro";

            await _viewModel.SaveTagAsync();

            _viewModel.TagInput.ShouldBeNull();
        }

        [Test]
        public async Task SaveArchetypeAsync_NullIconExplicitlySet_DoesNotSave()
        {
            // Icon guard still fires if caller explicitly clears NewDeckIcon to null
            _viewModel.NewDeckName = "Charizard";
            _viewModel.NewDeckIcon = null; // explicit null overrides the default

            await _viewModel.SaveArchetypeAsync();

            _ = _mockConnectionFactory.Archetypes.DidNotReceive().SaveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<uint>());
        }

        [Test]
        public async Task SaveArchetypeAsync_WithTrainer_CallsArchetypeSave()
        {
            Trainer trainer = new Trainer { Id = 7, Name = "Gary" };
            _mockConnectionFactory.Trainers.GetActiveAsync()
                .Returns(Task.FromResult<Trainer?>(trainer));
            _mockConnectionFactory.Trainers.GetAllAsync()
                .Returns(Task.FromResult(new List<Trainer> { trainer }));
            _mockConnectionFactory.Archetypes.SaveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<uint>())
                .Returns(Task.FromResult(1));

            await _viewModel.AppearingAsync();
            _viewModel.NewDeckName = "Charizard";
            _viewModel.NewDeckIcon = "charizard.png";

            await _viewModel.SaveArchetypeAsync();

            _ = _mockConnectionFactory.Archetypes.Received(1).SaveAsync("Charizard", "charizard.png", trainer.Id);
        }

        [Test]
        public async Task SaveArchetypeAsync_WithTrainer_ClearsInputs()
        {
            Trainer trainer = new Trainer { Id = 7, Name = "Gary" };
            _mockConnectionFactory.Trainers.GetActiveAsync()
                .Returns(Task.FromResult<Trainer?>(trainer));
            _mockConnectionFactory.Trainers.GetAllAsync()
                .Returns(Task.FromResult(new List<Trainer> { trainer }));
            _mockConnectionFactory.Archetypes.SaveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<uint>())
                .Returns(Task.FromResult(1));

            await _viewModel.AppearingAsync();
            _viewModel.NewDeckName = "Charizard";
            _viewModel.NewDeckIcon = "charizard.png";

            await _viewModel.SaveArchetypeAsync();

            _viewModel.NewDeckName.ShouldBeNull();
            _viewModel.NewDeckIcon.ShouldBe(_viewModel.SelectedIcon); // resets to default, not null
        }

        [Test]
        public async Task DeleteTrainerFileAsync_WithTrainer_CallsDeleteAsync()
        {
            Trainer trainer = new Trainer { Id = 9, Name = "Giovanni" };
            _mockConnectionFactory.Trainers.GetActiveAsync()
                .Returns(Task.FromResult<Trainer?>(trainer));
            _mockConnectionFactory.Trainers.GetAllAsync()
                .Returns(Task.FromResult(new List<Trainer> { trainer }));
            _mockConnectionFactory.Trainers.DeleteAsync(trainer)
                .Returns(Task.FromResult(1));

            await _viewModel.AppearingAsync();   // sets _trainer
            await _viewModel.DeleteTrainerFileAsync();

            _ = _mockConnectionFactory.Trainers.Received(1).DeleteAsync(trainer);
        }

        [Test]
        public async Task SwitchTrainerAsync_SameTrainer_DoesNotCallSwitchService()
        {
            Trainer trainer = new Trainer { Id = 3, Name = "Misty" };
            _mockConnectionFactory.Trainers.GetActiveAsync()
                .Returns(Task.FromResult<Trainer?>(trainer));
            _mockConnectionFactory.Trainers.GetAllAsync()
                .Returns(Task.FromResult(new List<Trainer> { trainer }));

            await _viewModel.AppearingAsync();

            // Switch to the same trainer — should be a no-op
            await _viewModel.SwitchTrainerAsync(trainer);

            await _mockSwitchService.DidNotReceive().SwitchToAsync(Arg.Any<Trainer>());
        }

        [Test]
        public async Task SwitchTrainerAsync_DifferentTrainer_UpdatesTrainerName()
        {
            Trainer original = new Trainer { Id = 1, Name = "Ash" };
            Trainer newTrainer = new Trainer { Id = 2, Name = "Brock" };
            _mockConnectionFactory.Trainers.GetByNameAsync(Arg.Any<string>())
                .Returns(Task.FromResult<Trainer?>(original));
            _mockConnectionFactory.Trainers.GetAllAsync()
                .Returns(Task.FromResult(new List<Trainer> { original, newTrainer }));
            _mockSwitchService.SwitchToAsync(newTrainer).Returns(Task.CompletedTask);
            _mockSwitchService.GetAllTrainersAsync()
                .Returns(Task.FromResult(new List<Trainer> { original, newTrainer }));

            await _viewModel.AppearingAsync();
            await _viewModel.SwitchTrainerAsync(newTrainer);

            _viewModel.TrainerName.ShouldBe("Brock");
        }

        [Test]
        public async Task SwitchTrainerAsync_DifferentTrainer_UpdatesTitle()
        {
            Trainer newTrainer = new Trainer { Id = 2, Name = "Brock" };
            _mockConnectionFactory.Trainers.GetAllAsync()
                .Returns(Task.FromResult(new List<Trainer> { newTrainer }));
            _mockSwitchService.SwitchToAsync(newTrainer).Returns(Task.CompletedTask);

            await _viewModel.SwitchTrainerAsync(newTrainer);

            _viewModel.Title.ShouldBe("Brock's Options");
        }

        [Test]
        public async Task SwitchTrainerAsync_DifferentTrainer_UpdatesFileConfirmMessage()
        {
            Trainer newTrainer = new Trainer { Id = 2, Name = "Brock" };
            _mockConnectionFactory.Trainers.GetAllAsync()
                .Returns(Task.FromResult(new List<Trainer> { newTrainer }));
            _mockSwitchService.SwitchToAsync(newTrainer).Returns(Task.CompletedTask);

            await _viewModel.SwitchTrainerAsync(newTrainer);

            _viewModel.FileConfirmMessage.ShouldBe("Delete Brock's Trainer File?");
        }

        [Test]
        public async Task SaveTrainerAsync_SaveReturnsZero_StillClearsNameInput()
        {
            // NameInput always cleared in finally regardless of save outcome
            _viewModel.NameInput = "Ash";
            _mockConnectionFactory.Trainers.SaveAsync("Ash").Returns(Task.FromResult(0));

            await _viewModel.SaveTrainerAsync();

            _viewModel.NameInput.ShouldBeNull();
        }

        [Test]
        public async Task SaveTrainerAsync_ValidInput_UpdatesTitle()
        {
            _viewModel.NameInput = "Ash";
            _mockConnectionFactory.Trainers.SaveAsync("Ash").Returns(Task.FromResult(1));
            _mockConnectionFactory.Trainers.GetByNameAsync("Ash")
                .Returns(Task.FromResult<Trainer?>(new Trainer { Id = 5, Name = "Ash" }));
            _mockConnectionFactory.Trainers.GetAllAsync()
                .Returns(Task.FromResult(new List<Trainer> { new() { Id = 5, Name = "Ash" } }));

            await _viewModel.SaveTrainerAsync();

            _viewModel.Title.ShouldBe("Ash's Options");
        }

        [Test]
        public async Task DeleteTrainerFileAsync_WithTrainer_ClearsTrainerName()
        {
            Trainer trainer = new Trainer { Id = 9, Name = "Giovanni" };
            _mockConnectionFactory.Trainers.GetActiveAsync()
                .Returns(Task.FromResult<Trainer?>(trainer));
            _mockConnectionFactory.Trainers.GetAllAsync()
                .Returns(Task.FromResult(new List<Trainer> { trainer }));
            _mockConnectionFactory.Trainers.DeleteAsync(trainer)
                .Returns(Task.FromResult(1));

            await _viewModel.AppearingAsync();
            await _viewModel.DeleteTrainerFileAsync();

            _viewModel.TrainerName.ShouldBe(string.Empty);
        }

        [Test]
        public async Task AppearingAsync_SetsSelectedSwitchTrainerToActiveTrainer()
        {
            Trainer trainer = new Trainer { Id = 3, Name = "Misty" };
            _mockConnectionFactory.Trainers.GetActiveAsync()
                .Returns(Task.FromResult<Trainer?>(trainer));
            _mockConnectionFactory.Trainers.GetAllAsync()
                .Returns(Task.FromResult(new List<Trainer> { trainer }));

            await _viewModel.AppearingAsync();

            _viewModel.SelectedSwitchTrainer.ShouldNotBeNull();
            _viewModel.SelectedSwitchTrainer!.Id.ShouldBe(trainer.Id);
        }

        [Test]
        public async Task SaveTagAsync_SaveReturnsZero_StillClearsTagInput()
        {
            // TagInput always cleared in finally — even when DB save reports 0 affected
            Trainer trainer = new Trainer { Id = 3, Name = "Misty" };
            _mockConnectionFactory.Trainers.GetActiveAsync()
                .Returns(Task.FromResult<Trainer?>(trainer));
            _mockConnectionFactory.Trainers.GetAllAsync()
                .Returns(Task.FromResult(new List<Trainer> { trainer }));
            _mockConnectionFactory.Tags.SaveAsync(Arg.Any<string>(), Arg.Any<uint>())
                .Returns(Task.FromResult(0));

            await _viewModel.AppearingAsync();
            _viewModel.TagInput = "Aggro";

            await _viewModel.SaveTagAsync();

            _viewModel.TagInput.ShouldBeNull();
        }

        [Test]
        public async Task SaveArchetypeAsync_SaveReturnsZero_StillClearsInputs()
        {
            // NewDeckName/Icon always cleared in finally — even when DB save reports 0 affected
            Trainer trainer = new Trainer { Id = 7, Name = "Gary" };
            _mockConnectionFactory.Trainers.GetActiveAsync()
                .Returns(Task.FromResult<Trainer?>(trainer));
            _mockConnectionFactory.Trainers.GetAllAsync()
                .Returns(Task.FromResult(new List<Trainer> { trainer }));
            _mockConnectionFactory.Archetypes.SaveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<uint>())
                .Returns(Task.FromResult(0));

            await _viewModel.AppearingAsync();
            _viewModel.NewDeckName = "Charizard";
            _viewModel.NewDeckIcon = "charizard.png";

            await _viewModel.SaveArchetypeAsync();

            _viewModel.NewDeckName.ShouldBeNull();
            // NewDeckIcon resets to SelectedIcon (not null) so the next save still has a default icon
            _viewModel.NewDeckIcon.ShouldBe(_viewModel.SelectedIcon);
        }

        [Test]
        public async Task AppearingAsync_LoadsAllArchetypesAndTags()
        {
            Trainer trainer = new Trainer { Id = 1, Name = "Ash" };
            List<Archetype> archetypes = new List<Archetype> { new() { Id = 1, Name = "Fire" }, new() { Id = 2, Name = "Water" } };
            List<Tags> tags = new List<Tags> { new() { Id = 1, Name = "Aggro" } };

            _mockConnectionFactory.Trainers.GetActiveAsync().Returns(Task.FromResult<Trainer?>(trainer));
            _mockConnectionFactory.Trainers.GetAllAsync().Returns(Task.FromResult(new List<Trainer> { trainer }));
            _mockConnectionFactory.Archetypes.GetAllAsync().Returns(Task.FromResult(archetypes));
            _mockConnectionFactory.Tags.GetAllAsync().Returns(Task.FromResult(tags));

            await _viewModel.AppearingAsync();

            _viewModel.AllArchetypes.Count.ShouldBe(2);
            _viewModel.AllTags.Count.ShouldBe(1);
        }

        [Test]
        public async Task DeleteArchetypeAsync_CallsDeleteAndRefreshesList()
        {
            Trainer trainer = new Trainer { Id = 1, Name = "Ash" };
            Archetype archetype = new Archetype { Id = 5, Name = "Fire" };
            _mockConnectionFactory.Trainers.GetActiveAsync().Returns(Task.FromResult<Trainer?>(trainer));
            _mockConnectionFactory.Trainers.GetAllAsync().Returns(Task.FromResult(new List<Trainer> { trainer }));
            _mockConnectionFactory.Archetypes.GetAllAsync().Returns(Task.FromResult(new List<Archetype> { archetype }));
            _mockConnectionFactory.Tags.GetAllAsync().Returns(Task.FromResult(new List<Tags>()));
            _mockConnectionFactory.Archetypes.DeleteAsync(archetype).Returns(Task.FromResult(1));

            await _viewModel.AppearingAsync();
            await _viewModel.DeleteArchetypeAsync(archetype);

            _ = _mockConnectionFactory.Archetypes.Received(1).DeleteAsync(archetype);
        }

        [Test]
        public async Task DeleteArchetypeAsync_AfterDelete_RefreshesAllArchetypes()
        {
            Trainer trainer = new Trainer { Id = 1, Name = "Ash" };
            Archetype archetype = new Archetype { Id = 5, Name = "Fire" };
            _mockConnectionFactory.Trainers.GetActiveAsync().Returns(Task.FromResult<Trainer?>(trainer));
            _mockConnectionFactory.Trainers.GetAllAsync().Returns(Task.FromResult(new List<Trainer> { trainer }));
            _mockConnectionFactory.Archetypes.GetAllAsync()
                .Returns(Task.FromResult(new List<Archetype> { archetype }),
                         Task.FromResult(new List<Archetype>()));
            _mockConnectionFactory.Tags.GetAllAsync().Returns(Task.FromResult(new List<Tags>()));
            _mockConnectionFactory.Archetypes.DeleteAsync(archetype).Returns(Task.FromResult(1));

            await _viewModel.AppearingAsync();
            await _viewModel.DeleteArchetypeAsync(archetype);

            _viewModel.AllArchetypes.Count.ShouldBe(0);
        }

        [Test]
        public async Task DeleteTagAsync_CallsDeleteAndRefreshesList()
        {
            Trainer trainer = new Trainer { Id = 1, Name = "Ash" };
            Tags tag = new Tags { Id = 3, Name = "Lucky" };
            _mockConnectionFactory.Trainers.GetActiveAsync().Returns(Task.FromResult<Trainer?>(trainer));
            _mockConnectionFactory.Trainers.GetAllAsync().Returns(Task.FromResult(new List<Trainer> { trainer }));
            _mockConnectionFactory.Archetypes.GetAllAsync().Returns(Task.FromResult(new List<Archetype>()));
            _mockConnectionFactory.Tags.GetAllAsync().Returns(Task.FromResult(new List<Tags> { tag }));
            _mockConnectionFactory.Tags.DeleteAsync(tag).Returns(Task.FromResult(1));

            await _viewModel.AppearingAsync();
            await _viewModel.DeleteTagAsync(tag);

            _ = _mockConnectionFactory.Tags.Received(1).DeleteAsync(tag);
        }

        [Test]
        public async Task DeleteTagAsync_AfterDelete_RefreshesAllTags()
        {
            Trainer trainer = new Trainer { Id = 1, Name = "Ash" };
            Tags tag = new Tags { Id = 3, Name = "Lucky" };
            _mockConnectionFactory.Trainers.GetActiveAsync().Returns(Task.FromResult<Trainer?>(trainer));
            _mockConnectionFactory.Trainers.GetAllAsync().Returns(Task.FromResult(new List<Trainer> { trainer }));
            _mockConnectionFactory.Archetypes.GetAllAsync().Returns(Task.FromResult(new List<Archetype>()));
            _mockConnectionFactory.Tags.GetAllAsync()
                .Returns(Task.FromResult(new List<Tags> { tag }),
                         Task.FromResult(new List<Tags>()));
            _mockConnectionFactory.Tags.DeleteAsync(tag).Returns(Task.FromResult(1));

            await _viewModel.AppearingAsync();
            await _viewModel.DeleteTagAsync(tag);

            _viewModel.AllTags.Count.ShouldBe(0);
        }

        [Test]
        public async Task SaveArchetypeAsync_OnSuccess_RefreshesAllArchetypes()
        {
            Trainer trainer = new Trainer { Id = 7, Name = "Gary" };
            Archetype saved = new Archetype { Id = 10, Name = "Charizard" };
            _mockConnectionFactory.Trainers.GetActiveAsync().Returns(Task.FromResult<Trainer?>(trainer));
            _mockConnectionFactory.Trainers.GetAllAsync().Returns(Task.FromResult(new List<Trainer> { trainer }));
            _mockConnectionFactory.Archetypes.GetAllAsync()
                .Returns(Task.FromResult(new List<Archetype>()),
                         Task.FromResult(new List<Archetype> { saved }));
            _mockConnectionFactory.Tags.GetAllAsync().Returns(Task.FromResult(new List<Tags>()));
            _mockConnectionFactory.Archetypes.SaveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<uint>())
                .Returns(Task.FromResult(1));

            await _viewModel.AppearingAsync();
            _viewModel.NewDeckName = "Charizard";
            _viewModel.NewDeckIcon = "charizard.png";

            await _viewModel.SaveArchetypeAsync();

            _viewModel.AllArchetypes.Count.ShouldBe(1);
        }

        [Test]
        public async Task SaveTagAsync_OnSuccess_RefreshesAllTags()
        {
            Trainer trainer = new Trainer { Id = 3, Name = "Misty" };
            Tags saved = new Tags { Id = 5, Name = "Aggro" };
            _mockConnectionFactory.Trainers.GetActiveAsync().Returns(Task.FromResult<Trainer?>(trainer));
            _mockConnectionFactory.Trainers.GetAllAsync().Returns(Task.FromResult(new List<Trainer> { trainer }));
            _mockConnectionFactory.Archetypes.GetAllAsync().Returns(Task.FromResult(new List<Archetype>()));
            _mockConnectionFactory.Tags.GetAllAsync()
                .Returns(Task.FromResult(new List<Tags>()),
                         Task.FromResult(new List<Tags> { saved }));
            _mockConnectionFactory.Tags.SaveAsync(Arg.Any<string>(), Arg.Any<uint>())
                .Returns(Task.FromResult(1));

            await _viewModel.AppearingAsync();
            _viewModel.TagInput = "Aggro";

            await _viewModel.SaveTagAsync();

            _viewModel.AllTags.Count.ShouldBe(1);
        }

        // ---------------------------------------------------------------------------
        // OnSelectedSwitchTrainerChanged
        // ---------------------------------------------------------------------------

        [Test]
        public async Task OnSelectedSwitchTrainerChanged_DifferentTrainer_CallsSwitchService()
        {
            // Load an active trainer so _trainer is set
            Trainer current = new Trainer { Id = 1, Name = "Ash" };
            Trainer next = new Trainer { Id = 2, Name = "Misty" };
            _mockConnectionFactory.Trainers.GetActiveAsync().Returns(Task.FromResult<Trainer?>(current));
            _mockConnectionFactory.Trainers.GetAllAsync()
                .Returns(Task.FromResult(new List<Trainer> { current, next }));
            _mockSwitchService.SwitchToAsync(Arg.Any<Trainer>()).Returns(Task.CompletedTask);
            await _viewModel.AppearingAsync();

            // Setting SelectedSwitchTrainer fires OnSelectedSwitchTrainerChanged
            _viewModel.SelectedSwitchTrainer = next;

            // Give the fire-and-forget a moment to call SwitchToAsync
            await Task.Delay(50);
            await _mockSwitchService.Received(1).SwitchToAsync(next);
        }

        [Test]
        public async Task OnSelectedSwitchTrainerChanged_SameTrainer_DoesNotCallSwitchService()
        {
            Trainer trainer = new Trainer { Id = 3, Name = "Brock" };
            _mockConnectionFactory.Trainers.GetActiveAsync().Returns(Task.FromResult<Trainer?>(trainer));
            _mockConnectionFactory.Trainers.GetAllAsync()
                .Returns(Task.FromResult(new List<Trainer> { trainer }));
            await _viewModel.AppearingAsync();

            _viewModel.SelectedSwitchTrainer = trainer;

            await Task.Delay(50);
            await _mockSwitchService.DidNotReceive().SwitchToAsync(Arg.Any<Trainer>());
        }

        [Test]
        public async Task OnSelectedSwitchTrainerChanged_NullValue_DoesNotCallSwitchService()
        {
            Trainer trainer = new Trainer { Id = 3, Name = "Brock" };
            _mockConnectionFactory.Trainers.GetActiveAsync().Returns(Task.FromResult<Trainer?>(trainer));
            _mockConnectionFactory.Trainers.GetAllAsync()
                .Returns(Task.FromResult(new List<Trainer> { trainer }));
            await _viewModel.AppearingAsync();

            _viewModel.SelectedSwitchTrainer = null;

            await Task.Delay(50);
            await _mockSwitchService.DidNotReceive().SwitchToAsync(Arg.Any<Trainer>());
        }

        // ---------------------------------------------------------------------------
        // AppearingAsync — ActiveTrainer from switch service (no DB call needed)
        // ---------------------------------------------------------------------------

        [Test]
        public async Task AppearingAsync_ActiveTrainerFromSwitchService_DoesNotCallGetActiveAsync()
        {
            Trainer trainer = new Trainer { Id = 10, Name = "Lance" };
            _mockSwitchService.ActiveTrainer.Returns(trainer);
            _mockConnectionFactory.Trainers.GetAllAsync()
                .Returns(Task.FromResult(new List<Trainer> { trainer }));

            await _viewModel.AppearingAsync();

            _ = _mockConnectionFactory.Trainers.DidNotReceive().GetActiveAsync();
            _viewModel.TrainerName.ShouldBe("Lance");
        }

        // ---------------------------------------------------------------------------
        // DeleteTrainerFromListAsync — non-active trainer (no Shell dialog needed here
        // because Shell.Current is null in unit tests, but we can test the DB path
        // via a confirmed=false mock that makes the method return early)
        // ---------------------------------------------------------------------------

        [Test]
        public async Task DeleteArchetypeAsync_ThrowsException_DoesNotRethrow()
        {
            Archetype archetype = new Archetype { Id = 5, Name = "Fire" };
            _mockConnectionFactory.Archetypes.DeleteAsync(archetype)
                .Returns(Task.FromException<int>(new InvalidOperationException("DB error")));

            await Should.NotThrowAsync(() => _viewModel.DeleteArchetypeAsync(archetype));
        }

        [Test]
        public async Task DeleteTagAsync_ThrowsException_DoesNotRethrow()
        {
            Tags tag = new Tags { Id = 3, Name = "Lucky" };
            _mockConnectionFactory.Tags.DeleteAsync(tag)
                .Returns(Task.FromException<int>(new InvalidOperationException("DB error")));

            await Should.NotThrowAsync(() => _viewModel.DeleteTagAsync(tag));
        }

        [Test]
        public async Task SaveTrainerAsync_ThrowsException_DoesNotRethrow()
        {
            _viewModel.NameInput = "Ash";
            _mockConnectionFactory.Trainers.SaveAsync("Ash")
                .Returns(Task.FromException<int>(new InvalidOperationException("DB error")));

            await Should.NotThrowAsync(() => _viewModel.SaveTrainerAsync());
        }

        [Test]
        public async Task SaveTrainerAsync_SaveSucceedsButTrainerNotFoundAfterSave_DoesNotCallSwitchService()
        {
            // Save returns 1 but GetByNameAsync returns null → early return, no SwitchToAsync
            _viewModel.NameInput = "Ghost";
            _mockConnectionFactory.Trainers.SaveAsync("Ghost").Returns(Task.FromResult(1));
            _mockConnectionFactory.Trainers.GetByNameAsync("Ghost")
                .Returns(Task.FromResult<Trainer?>(null));

            await _viewModel.SaveTrainerAsync();

            await _mockSwitchService.DidNotReceive().SwitchToAsync(Arg.Any<Trainer>());
        }

        [Test]
        public async Task DeleteArchetypeAsync_AffectedIsZero_DoesNotRefreshArchetypeList()
        {
            Trainer trainer = new Trainer { Id = 1, Name = "Ash" };
            Archetype archetype = new Archetype { Id = 5, Name = "Fire" };
            _mockConnectionFactory.Trainers.GetActiveAsync().Returns(Task.FromResult<Trainer?>(trainer));
            _mockConnectionFactory.Trainers.GetAllAsync().Returns(Task.FromResult(new List<Trainer> { trainer }));
            _mockConnectionFactory.Archetypes.GetAllAsync()
                .Returns(Task.FromResult(new List<Archetype> { archetype }));
            _mockConnectionFactory.Tags.GetAllAsync().Returns(Task.FromResult(new List<Tags>()));
            _mockConnectionFactory.Archetypes.DeleteAsync(archetype).Returns(Task.FromResult(0));

            await _viewModel.AppearingAsync();
            _viewModel.AllArchetypes.Count.ShouldBe(1); // seeded above

            await _viewModel.DeleteArchetypeAsync(archetype);

            // GetAllAsync called once (AppearingAsync) but NOT a second time after delete=0
            _ = _mockConnectionFactory.Archetypes.Received(1).GetAllAsync();
        }

        [Test]
        public async Task DeleteTagAsync_AffectedIsZero_DoesNotRefreshTagList()
        {
            Trainer trainer = new Trainer { Id = 1, Name = "Ash" };
            Tags tag = new Tags { Id = 3, Name = "Lucky" };
            _mockConnectionFactory.Trainers.GetActiveAsync().Returns(Task.FromResult<Trainer?>(trainer));
            _mockConnectionFactory.Trainers.GetAllAsync().Returns(Task.FromResult(new List<Trainer> { trainer }));
            _mockConnectionFactory.Archetypes.GetAllAsync().Returns(Task.FromResult(new List<Archetype>()));
            _mockConnectionFactory.Tags.GetAllAsync().Returns(Task.FromResult(new List<Tags> { tag }));
            _mockConnectionFactory.Tags.DeleteAsync(tag).Returns(Task.FromResult(0));

            await _viewModel.AppearingAsync();

            await _viewModel.DeleteTagAsync(tag);

            // GetAllAsync called once (AppearingAsync) but NOT a second time after delete=0
            _ = _mockConnectionFactory.Tags.Received(1).GetAllAsync();
        }

        [Test]
        public async Task SaveTagAsync_ThrowsException_DoesNotRethrow()
        {
            Trainer trainer = new Trainer { Id = 3, Name = "Misty" };
            _mockConnectionFactory.Trainers.GetActiveAsync().Returns(Task.FromResult<Trainer?>(trainer));
            _mockConnectionFactory.Trainers.GetAllAsync().Returns(Task.FromResult(new List<Trainer> { trainer }));
            _mockConnectionFactory.Tags.SaveAsync(Arg.Any<string>(), Arg.Any<uint>())
                .Returns(Task.FromException<int>(new InvalidOperationException("DB error")));

            await _viewModel.AppearingAsync();
            _viewModel.TagInput = "Aggro";

            await Should.NotThrowAsync(() => _viewModel.SaveTagAsync());
        }

        [Test]
        public async Task SaveArchetypeAsync_ThrowsException_DoesNotRethrow()
        {
            Trainer trainer = new Trainer { Id = 7, Name = "Gary" };
            _mockConnectionFactory.Trainers.GetActiveAsync().Returns(Task.FromResult<Trainer?>(trainer));
            _mockConnectionFactory.Trainers.GetAllAsync().Returns(Task.FromResult(new List<Trainer> { trainer }));
            _mockConnectionFactory.Archetypes.SaveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<uint>())
                .Returns(Task.FromException<int>(new InvalidOperationException("DB error")));

            await _viewModel.AppearingAsync();
            _viewModel.NewDeckName = "Charizard";
            _viewModel.NewDeckIcon = "charizard.png";

            await Should.NotThrowAsync(() => _viewModel.SaveArchetypeAsync());
        }

        // ---------------------------------------------------------------------------
        // ToDisplayName
        // ---------------------------------------------------------------------------

        [TestCase("ball_icon.png", "Ball Icon")]
        [TestCase("charizard_fire.png", "Charizard Fire")]
        [TestCase("single.png", "Single")]
        [TestCase("no_extension", "No Extension")]
        public void ToDisplayName_FilenameVariants_ReturnsTitleCased(string filename, string expected)
        {
            OptionsPageViewModel.ToDisplayName(filename).ShouldBe(expected);
        }
    }
}
