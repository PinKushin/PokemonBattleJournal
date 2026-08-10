using PokemonBattleJournal.Interfaces;

namespace PokemonBattleJournal.Tests.ViewModels
{
    /// <summary>
    /// Loading-gate contract: every page VM exposes a named IsBusy* flag that is
    /// true while its async data load is in flight and false the moment it settles —
    /// including on the error path. UI tests sync on the bound sentinel Label
    /// (AutomationId "Busy_*") instead of polling arbitrary elements.
    /// </summary>
    public class LoadingGateTests
    {
        // ---------------------------------------------------------------
        // ReadJournalPageViewModel — IsBusyMatchHistory
        // ---------------------------------------------------------------

        private static ReadJournalPageViewModel CreateReadJournalVm(
            out ISqliteConnectionFactory factory)
        {
            factory = Substitute.For<ISqliteConnectionFactory>();
            factory.Trainers.Returns(Substitute.For<ITrainerOperations>());
            factory.Matches.Returns(Substitute.For<IMatchOperations>());
            return new ReadJournalPageViewModel(
                Substitute.For<ILogger<ReadJournalPageViewModel>>(),
                factory,
                Substitute.For<ITrainerSwitchService>(), Substitute.For<IErrorHandler>());
        }

        [Test]
        public async Task ReadJournal_AppearingAsync_IsBusyMatchHistory_TrueDuringLoad_FalseAfter()
        {
            ReadJournalPageViewModel vm = CreateReadJournalVm(out ISqliteConnectionFactory factory);
            TaskCompletionSource<List<MatchEntry>> gate = new TaskCompletionSource<List<MatchEntry>>();
            factory.Trainers.GetActiveAsync()
                .Returns(Task.FromResult<Trainer?>(new Trainer { Id = 1, Name = "Test" }));
            factory.Matches.GetByTrainerIdAsync(1, Arg.Any<bool>())
                .Returns(_ => gate.Task);

            Task appearing = vm.AppearingAsync();

            vm.IsBusyMatchHistory.ShouldBeTrue("busy flag must be up while the DB load is in flight");

            gate.SetResult([new MatchEntry { Result = MatchResult.Win }]);
            await appearing;

            vm.IsBusyMatchHistory.ShouldBeFalse("busy flag must clear when the load completes");
        }

        [Test]
        public async Task ReadJournal_AppearingAsync_DatabaseThrows_ClearsBusy()
        {
            ReadJournalPageViewModel vm = CreateReadJournalVm(out ISqliteConnectionFactory factory);
            factory.Trainers.GetActiveAsync()
                .Returns(Task.FromException<Trainer?>(new InvalidOperationException("boom")));

            await vm.AppearingAsync();

            vm.IsBusyMatchHistory.ShouldBeFalse("busy flag must clear even when the load throws");
        }

        // ---------------------------------------------------------------
        // TrainerPageViewModel — IsBusyChartData
        // ---------------------------------------------------------------

        private static TrainerPageViewModel CreateTrainerVm(
            out ISqliteConnectionFactory factory,
            out IMatchAnalysisService analysis)
        {
            factory = Substitute.For<ISqliteConnectionFactory>();
            factory.Trainers.Returns(Substitute.For<ITrainerOperations>());
            factory.Matches.Returns(Substitute.For<IMatchOperations>());
            analysis = Substitute.For<IMatchAnalysisService>();
            return new TrainerPageViewModel(
                Substitute.For<ILogger<TrainerPageViewModel>>(),
                factory,
                analysis,
                Substitute.For<ITrainerSwitchService>(), Substitute.For<IErrorHandler>());
        }

        [Test]
        public async Task Trainer_AppearingAsync_IsBusyChartData_TrueDuringLoad_FalseAfter()
        {
            TrainerPageViewModel vm = CreateTrainerVm(out ISqliteConnectionFactory factory, out _);
            TaskCompletionSource<List<MatchEntry>> gate = new TaskCompletionSource<List<MatchEntry>>();
            factory.Trainers.GetActiveAsync()
                .Returns(Task.FromResult<Trainer?>(new Trainer { Id = 1, Name = "Test" }));
            factory.Matches.GetByTrainerIdAsync(1, Arg.Any<bool>())
                .Returns(_ => gate.Task);

            Task appearing = vm.AppearingAsync();

            vm.IsBusyChartData.ShouldBeTrue("busy flag must be up while match data + charts load");

            gate.SetResult([]);
            await appearing;

            vm.IsBusyChartData.ShouldBeFalse("busy flag must clear after charts settle");
        }

        [Test]
        public async Task Trainer_AppearingAsync_NoTrainer_ClearsBusy()
        {
            TrainerPageViewModel vm = CreateTrainerVm(out ISqliteConnectionFactory factory, out _);
            factory.Trainers.GetActiveAsync().Returns(Task.FromResult<Trainer?>(null));

            await vm.AppearingAsync();

            vm.IsBusyChartData.ShouldBeFalse("busy flag must clear on the early-return path");
        }

        // ---------------------------------------------------------------
        // MainPageViewModel — IsBusyArchetypeList
        // ---------------------------------------------------------------

        private static MainPageViewModel CreateMainVm(out ISqliteConnectionFactory factory)
        {
            factory = Substitute.For<ISqliteConnectionFactory>();
            factory.Trainers.Returns(Substitute.For<ITrainerOperations>());
            factory.Matches.Returns(Substitute.For<IMatchOperations>());
            factory.Archetypes.Returns(Substitute.For<IArchetypeOperations>());
            factory.Tags.Returns(Substitute.For<ITagOperations>());
            return new MainPageViewModel(
                Substitute.For<ILogger<MainPageViewModel>>(),
                factory,
                Substitute.For<IMatchResultsCalculatorFactory>(),
                Substitute.For<ITrainerSwitchService>(), Substitute.For<IErrorHandler>());
        }

        [Test]
        public async Task Main_AppearingAsync_IsBusyArchetypeList_TrueDuringLoad_FalseAfter()
        {
            MainPageViewModel vm = CreateMainVm(out ISqliteConnectionFactory factory);
            TaskCompletionSource<List<Archetype>> gate = new TaskCompletionSource<List<Archetype>>();
            factory.Trainers.GetActiveAsync()
                .Returns(Task.FromResult<Trainer?>(new Trainer { Id = 1, Name = "Test" }));
            factory.Archetypes.GetAllAsync().Returns(_ => gate.Task);
            factory.Tags.GetAllAsync().Returns(Task.FromResult(new List<Tags>()));

            Task appearing = vm.AppearingAsync();

            vm.IsBusyArchetypeList.ShouldBeTrue("busy flag must be up while archetypes load");

            gate.SetResult([new Archetype { Name = "Other" }]);
            await appearing;

            vm.IsBusyArchetypeList.ShouldBeFalse("busy flag must clear when archetypes settle");
        }

        [Test]
        public async Task Main_AppearingAsync_ArchetypeLoadThrows_ClearsBusy()
        {
            MainPageViewModel vm = CreateMainVm(out ISqliteConnectionFactory factory);
            factory.Trainers.GetActiveAsync()
                .Returns(Task.FromResult<Trainer?>(new Trainer { Id = 1, Name = "Test" }));
            factory.Archetypes.GetAllAsync()
                .Returns(Task.FromException<List<Archetype>>(new InvalidOperationException("boom")));

            await vm.AppearingAsync();

            vm.IsBusyArchetypeList.ShouldBeFalse("busy flag must clear even when the load throws");
        }

        // ---------------------------------------------------------------
        // MainPageViewModel — IsBusyMutating (SaveMatchAsync)
        // ---------------------------------------------------------------

        private static MainPageViewModel CreateMainVmReadyToSave(
            out ISqliteConnectionFactory factory, out IMatchResultsCalculatorFactory calculatorFactory)
        {
            factory = Substitute.For<ISqliteConnectionFactory>();
            factory.Trainers.Returns(Substitute.For<ITrainerOperations>());
            factory.Matches.Returns(Substitute.For<IMatchOperations>());
            factory.Archetypes.Returns(Substitute.For<IArchetypeOperations>());
            factory.Tags.Returns(Substitute.For<ITagOperations>());
            calculatorFactory = Substitute.For<IMatchResultsCalculatorFactory>();
            IMatchResultCalculator calculator = Substitute.For<IMatchResultCalculator>();
            calculator.CalculateResult(Arg.Any<MatchResult?>(), Arg.Any<MatchResult?>(), Arg.Any<MatchResult?>())
                .Returns(MatchResult.Win);
            calculatorFactory.GetCalculator(Arg.Any<bool>()).Returns(calculator);
            factory.Trainers.GetActiveAsync()
                .Returns(Task.FromResult<Trainer?>(new Trainer { Id = 1, Name = "Test" }));

            MainPageViewModel vm = new MainPageViewModel(
                Substitute.For<ILogger<MainPageViewModel>>(),
                factory,
                calculatorFactory,
                Substitute.For<ITrainerSwitchService>(), Substitute.For<IErrorHandler>());
            vm.TrainerName = "Test";
            vm.PlayerSelected = new Archetype { Id = 1, Name = "Fire" };
            vm.RivalSelected = new Archetype { Id = 2, Name = "Water" };
            vm.Result = MatchResult.Win;
            return vm;
        }

        [Test]
        public async Task Main_SaveMatchAsync_IsBusyMutating_TrueDuringSave_FalseAfter()
        {
            MainPageViewModel vm = CreateMainVmReadyToSave(out ISqliteConnectionFactory factory, out _);
            TaskCompletionSource<int> gate = new TaskCompletionSource<int>();
            factory.Matches.SaveAsync(Arg.Any<MatchEntry>(), Arg.Any<List<Game>>()).Returns(_ => gate.Task);

            Task saving = vm.SaveMatchAsync();

            vm.IsBusyMutating.ShouldBeTrue("busy flag must be up while the match save is in flight");

            gate.SetResult(1);
            await saving;

            vm.IsBusyMutating.ShouldBeFalse("busy flag must clear when the save settles");
        }

        [Test]
        public async Task Main_SaveMatchAsync_SaveThrows_ClearsBusyMutating()
        {
            MainPageViewModel vm = CreateMainVmReadyToSave(out ISqliteConnectionFactory factory, out _);
            factory.Matches.SaveAsync(Arg.Any<MatchEntry>(), Arg.Any<List<Game>>())
                .Returns(Task.FromException<int>(new InvalidOperationException("boom")));

            await vm.SaveMatchAsync();

            vm.IsBusyMutating.ShouldBeFalse("busy flag must clear even when the save throws");
        }

        [Test]
        public async Task Main_SaveMatchAsync_ValidationFails_NeverSetsBusyMutating()
        {
            // No PlayerSelected/RivalSelected/Result — fails validation before the gate.
            MainPageViewModel vm = CreateMainVm(out _);

            await vm.SaveMatchAsync();

            vm.IsBusyMutating.ShouldBeFalse("busy flag must never flip for a save that never reaches the DB");
        }

        // ---------------------------------------------------------------
        // OptionsPageViewModel — IsBusyArchetypeList
        // ---------------------------------------------------------------

        private static OptionsPageViewModel CreateOptionsVm(out ISqliteConnectionFactory factory)
        {
            factory = Substitute.For<ISqliteConnectionFactory>();
            factory.Trainers.Returns(Substitute.For<ITrainerOperations>());
            factory.Matches.Returns(Substitute.For<IMatchOperations>());
            factory.Archetypes.Returns(Substitute.For<IArchetypeOperations>());
            factory.Tags.Returns(Substitute.For<ITagOperations>());
            ITrainerSwitchService switchService = Substitute.For<ITrainerSwitchService>();
            MainPageViewModel mainVm = new MainPageViewModel(
                Substitute.For<ILogger<MainPageViewModel>>(),
                factory,
                Substitute.For<IMatchResultsCalculatorFactory>(),
                switchService, Substitute.For<IErrorHandler>());
            AppShellViewModel shellVm = new AppShellViewModel(
                switchService,
                mainVm,
                Substitute.For<ILogger<AppShellViewModel>>());
            return new OptionsPageViewModel(
                Substitute.For<ILogger<OptionsPageViewModel>>(),
                factory,
                switchService,
                shellVm,
                Substitute.For<ITrainerHillImportService>(), Substitute.For<IExportService>(), Substitute.For<IRestoreService>(), Substitute.For<IErrorHandler>(),
                new PokemonBattleJournal.Logging.SentryPerformanceMonitor());
        }

        [Test]
        public async Task Options_AppearingAsync_IsBusyArchetypeList_TrueDuringLoad_FalseAfter()
        {
            OptionsPageViewModel vm = CreateOptionsVm(out ISqliteConnectionFactory factory);
            TaskCompletionSource<List<Trainer>> gate = new TaskCompletionSource<List<Trainer>>();
            factory.Trainers.GetAllAsync().Returns(_ => gate.Task);
            factory.Archetypes.GetAllAsync().Returns(Task.FromResult(new List<Archetype>()));
            factory.Tags.GetAllAsync().Returns(Task.FromResult(new List<Tags>()));

            Task appearing = vm.AppearingAsync();

            vm.IsBusyArchetypeList.ShouldBeTrue("busy flag must be up while options data loads");

            gate.SetResult([]);
            await appearing;

            vm.IsBusyArchetypeList.ShouldBeFalse("busy flag must clear when options data settles");
        }

        [Test]
        public async Task Options_AppearingAsync_LoadThrows_ClearsBusy()
        {
            OptionsPageViewModel vm = CreateOptionsVm(out ISqliteConnectionFactory factory);
            factory.Trainers.GetAllAsync()
                .Returns(Task.FromException<List<Trainer>>(new InvalidOperationException("boom")));

            await vm.AppearingAsync();

            vm.IsBusyArchetypeList.ShouldBeFalse("busy flag must clear even when the load throws");
        }

        // ---------------------------------------------------------------
        // OptionsPageViewModel — IsBusyMutating (Save/Delete Archetype/Tag)
        // ---------------------------------------------------------------

        private static async Task<(OptionsPageViewModel Vm, ISqliteConnectionFactory Factory)> CreateOptionsVmWithActiveTrainerAsync()
        {
            ISqliteConnectionFactory factory = Substitute.For<ISqliteConnectionFactory>();
            factory.Trainers.Returns(Substitute.For<ITrainerOperations>());
            factory.Matches.Returns(Substitute.For<IMatchOperations>());
            factory.Archetypes.Returns(Substitute.For<IArchetypeOperations>());
            factory.Tags.Returns(Substitute.For<ITagOperations>());
            ITrainerSwitchService switchService = Substitute.For<ITrainerSwitchService>();
            switchService.ActiveTrainer.Returns(new Trainer { Id = 1, Name = "Test" });
            factory.Trainers.GetAllAsync().Returns(Task.FromResult(new List<Trainer>()));
            factory.Archetypes.GetAllAsync().Returns(Task.FromResult(new List<Archetype>()));
            factory.Tags.GetAllAsync().Returns(Task.FromResult(new List<Tags>()));

            MainPageViewModel mainVm = new MainPageViewModel(
                Substitute.For<ILogger<MainPageViewModel>>(),
                factory,
                Substitute.For<IMatchResultsCalculatorFactory>(),
                switchService, Substitute.For<IErrorHandler>());
            AppShellViewModel shellVm = new AppShellViewModel(
                switchService,
                mainVm,
                Substitute.For<ILogger<AppShellViewModel>>());
            OptionsPageViewModel vm = new OptionsPageViewModel(
                Substitute.For<ILogger<OptionsPageViewModel>>(),
                factory,
                switchService,
                shellVm,
                Substitute.For<ITrainerHillImportService>(), Substitute.For<IExportService>(), Substitute.For<IRestoreService>(), Substitute.For<IErrorHandler>(),
                new PokemonBattleJournal.Logging.SentryPerformanceMonitor());

            // AppearingAsync sets the private _trainer field from ActiveTrainer — required
            // before Save/Delete commands will run their body instead of early-returning.
            await vm.AppearingAsync();
            return (vm, factory);
        }

        [Test]
        public async Task Options_SaveArchetypeAsync_IsBusyMutating_TrueDuringSave_FalseAfter()
        {
            (OptionsPageViewModel vm, ISqliteConnectionFactory factory) = await CreateOptionsVmWithActiveTrainerAsync();
            vm.NewDeckName = "Test Deck";
            vm.NewDeckIcon = "ball_icon.png";
            TaskCompletionSource<int> gate = new TaskCompletionSource<int>();
            factory.Archetypes.SaveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<uint>())
                .Returns(_ => gate.Task);

            Task saving = vm.SaveArchetypeAsync();

            vm.IsBusyMutating.ShouldBeTrue("busy flag must be up while the archetype save is in flight");

            gate.SetResult(1);
            await saving;

            vm.IsBusyMutating.ShouldBeFalse("busy flag must clear when the save settles");
        }

        [Test]
        public async Task Options_DeleteArchetypeAsync_IsBusyMutating_TrueDuringDelete_FalseAfter()
        {
            (OptionsPageViewModel vm, ISqliteConnectionFactory factory) = await CreateOptionsVmWithActiveTrainerAsync();
            Archetype archetype = new Archetype { Id = 1, Name = "Test Deck" };
            TaskCompletionSource<int> gate = new TaskCompletionSource<int>();
            factory.Archetypes.DeleteAsync(archetype).Returns(_ => gate.Task);

            Task deleting = vm.DeleteArchetypeAsync(archetype);

            vm.IsBusyMutating.ShouldBeTrue("busy flag must be up while the archetype delete is in flight");

            gate.SetResult(1);
            await deleting;

            vm.IsBusyMutating.ShouldBeFalse("busy flag must clear when the delete settles");
        }

        [Test]
        public async Task Options_SaveTagAsync_IsBusyMutating_TrueDuringSave_FalseAfter()
        {
            (OptionsPageViewModel vm, ISqliteConnectionFactory factory) = await CreateOptionsVmWithActiveTrainerAsync();
            vm.TagInput = "Test Tag";
            TaskCompletionSource<int> gate = new TaskCompletionSource<int>();
            factory.Tags.SaveAsync(Arg.Any<string>(), Arg.Any<uint>()).Returns(_ => gate.Task);

            Task saving = vm.SaveTagAsync();

            vm.IsBusyMutating.ShouldBeTrue("busy flag must be up while the tag save is in flight");

            gate.SetResult(1);
            await saving;

            vm.IsBusyMutating.ShouldBeFalse("busy flag must clear when the save settles");
        }

        [Test]
        public async Task Options_DeleteTagAsync_IsBusyMutating_TrueDuringDelete_FalseAfter()
        {
            (OptionsPageViewModel vm, ISqliteConnectionFactory factory) = await CreateOptionsVmWithActiveTrainerAsync();
            Tags tag = new Tags { Id = 1, Name = "Test Tag" };
            TaskCompletionSource<int> gate = new TaskCompletionSource<int>();
            factory.Tags.DeleteAsync(tag).Returns(_ => gate.Task);

            Task deleting = vm.DeleteTagAsync(tag);

            vm.IsBusyMutating.ShouldBeTrue("busy flag must be up while the tag delete is in flight");

            gate.SetResult(1);
            await deleting;

            vm.IsBusyMutating.ShouldBeFalse("busy flag must clear when the delete settles");
        }

        [Test]
        public async Task Options_SaveArchetypeAsync_Throws_ClearsBusyMutating()
        {
            (OptionsPageViewModel vm, ISqliteConnectionFactory factory) = await CreateOptionsVmWithActiveTrainerAsync();
            vm.NewDeckName = "Test Deck";
            vm.NewDeckIcon = "ball_icon.png";
            factory.Archetypes.SaveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<uint>())
                .Returns(Task.FromException<int>(new InvalidOperationException("boom")));

            await vm.SaveArchetypeAsync();

            vm.IsBusyMutating.ShouldBeFalse("busy flag must clear even when the save throws");
        }

        [Test]
        public async Task Options_SwitchTrainerAsync_IsBusyMutating_FalseAfterSwitchSettles()
        {
            // ITrainerSwitchService is injected before OptionsPageViewModel construction
            // (see CreateOptionsVmWithActiveTrainerAsync), so there's no seam left to gate
            // SwitchToAsync mid-flight for this test — it only proves the flag clears
            // cleanly on the ordinary path. The TrueDuring/FalseAfter shape is covered by
            // Save/Delete Archetype/Tag and DeleteTrainerFileAsync above.
            (OptionsPageViewModel vm, ISqliteConnectionFactory _) = await CreateOptionsVmWithActiveTrainerAsync();
            Trainer otherTrainer = new Trainer { Id = 2, Name = "Other" };

            await vm.SwitchTrainerAsync(otherTrainer);

            vm.IsBusyMutating.ShouldBeFalse("busy flag must be back down once the switch settles");
        }

        [Test]
        public async Task Options_DeleteTrainerFileAsync_IsBusyMutating_TrueDuringDelete_FalseAfter()
        {
            (OptionsPageViewModel vm, ISqliteConnectionFactory factory) = await CreateOptionsVmWithActiveTrainerAsync();
            TaskCompletionSource<int> gate = new TaskCompletionSource<int>();
            factory.Trainers.DeleteAsync(Arg.Any<Trainer>()).Returns(_ => gate.Task);
            factory.Trainers.GetAllAsync().Returns(Task.FromResult(new List<Trainer>()));

            Task deleting = vm.DeleteTrainerFileAsync();

            vm.IsBusyMutating.ShouldBeTrue("busy flag must be up while the trainer file delete is in flight");

            gate.SetResult(1);
            await deleting;

            vm.IsBusyMutating.ShouldBeFalse("busy flag must clear when the delete settles");
        }

        // ---------------------------------------------------------------------------
        // IsAnyBusy — what the loading indicator binds to on pages with more than one gate
        //
        // MainPage and OptionsPage each have a load gate and a mutate gate. Binding the spinner
        // to one of them would leave the other operation with no feedback, so they expose a
        // combined signal. It must also RAISE a change notification when either input flips,
        // otherwise the binding never updates and the spinner silently never appears — a
        // failure that no amount of correct XAML would reveal.
        // ---------------------------------------------------------------------------

        [Test]
        public void Main_IsAnyBusy_TrueWhenEitherGateIsUp()
        {
            MainPageViewModel vm = CreateMainVm(out _);
            vm.IsAnyBusy.ShouldBeFalse();

            vm.IsBusyArchetypeList = true;
            vm.IsAnyBusy.ShouldBeTrue("the archetype load must show the indicator");

            vm.IsBusyArchetypeList = false;
            vm.IsBusyMutating = true;
            vm.IsAnyBusy.ShouldBeTrue("saving a match must show the indicator");

            vm.IsBusyMutating = false;
            vm.IsAnyBusy.ShouldBeFalse();
        }

        [Test]
        public void Main_IsAnyBusy_RaisesChangeNotificationForEachGate()
        {
            MainPageViewModel vm = CreateMainVm(out _);
            List<string?> changed = [];
            vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            vm.IsBusyArchetypeList = true;
            changed.ShouldContain(nameof(vm.IsAnyBusy), "without this the binding never updates");

            changed.Clear();
            vm.IsBusyMutating = true;
            changed.ShouldContain(nameof(vm.IsAnyBusy));
        }

        [Test]
        public void Options_IsAnyBusy_TrueWhenEitherGateIsUp()
        {
            OptionsPageViewModel vm = CreateOptionsVm(out _);
            vm.IsAnyBusy.ShouldBeFalse();

            vm.IsBusyArchetypeList = true;
            vm.IsAnyBusy.ShouldBeTrue();

            vm.IsBusyArchetypeList = false;
            vm.IsBusyMutating = true;
            vm.IsAnyBusy.ShouldBeTrue();
        }

        [Test]
        public void Options_IsAnyBusy_RaisesChangeNotificationForEachGate()
        {
            OptionsPageViewModel vm = CreateOptionsVm(out _);
            List<string?> changed = [];
            vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            vm.IsBusyArchetypeList = true;
            changed.ShouldContain(nameof(vm.IsAnyBusy));

            changed.Clear();
            vm.IsBusyMutating = true;
            changed.ShouldContain(nameof(vm.IsAnyBusy));
        }
    }
}
