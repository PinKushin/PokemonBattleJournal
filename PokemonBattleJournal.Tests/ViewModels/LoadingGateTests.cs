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
                Substitute.For<ITrainerSwitchService>());
        }

        [Test]
        public async Task ReadJournal_AppearingAsync_IsBusyMatchHistory_TrueDuringLoad_FalseAfter()
        {
            ReadJournalPageViewModel vm = CreateReadJournalVm(out ISqliteConnectionFactory factory);
            var gate = new TaskCompletionSource<List<MatchEntry>>();
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
                Substitute.For<ITrainerSwitchService>());
        }

        [Test]
        public async Task Trainer_AppearingAsync_IsBusyChartData_TrueDuringLoad_FalseAfter()
        {
            TrainerPageViewModel vm = CreateTrainerVm(out ISqliteConnectionFactory factory, out _);
            var gate = new TaskCompletionSource<List<MatchEntry>>();
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
                Substitute.For<ITrainerSwitchService>());
        }

        [Test]
        public async Task Main_AppearingAsync_IsBusyArchetypeList_TrueDuringLoad_FalseAfter()
        {
            MainPageViewModel vm = CreateMainVm(out ISqliteConnectionFactory factory);
            var gate = new TaskCompletionSource<List<Archetype>>();
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
            var mainVm = new MainPageViewModel(
                Substitute.For<ILogger<MainPageViewModel>>(),
                factory,
                Substitute.For<IMatchResultsCalculatorFactory>(),
                switchService);
            var shellVm = new AppShellViewModel(
                switchService,
                mainVm,
                Substitute.For<ILogger<AppShellViewModel>>());
            return new OptionsPageViewModel(
                Substitute.For<ILogger<OptionsPageViewModel>>(),
                factory,
                switchService,
                shellVm,
                Substitute.For<ITrainerHillImportService>());
        }

        [Test]
        public async Task Options_AppearingAsync_IsBusyArchetypeList_TrueDuringLoad_FalseAfter()
        {
            OptionsPageViewModel vm = CreateOptionsVm(out ISqliteConnectionFactory factory);
            var gate = new TaskCompletionSource<List<Trainer>>();
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

            var mainVm = new MainPageViewModel(
                Substitute.For<ILogger<MainPageViewModel>>(),
                factory,
                Substitute.For<IMatchResultsCalculatorFactory>(),
                switchService);
            var shellVm = new AppShellViewModel(
                switchService,
                mainVm,
                Substitute.For<ILogger<AppShellViewModel>>());
            var vm = new OptionsPageViewModel(
                Substitute.For<ILogger<OptionsPageViewModel>>(),
                factory,
                switchService,
                shellVm,
                Substitute.For<ITrainerHillImportService>());

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
            var gate = new TaskCompletionSource<int>();
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
            var archetype = new Archetype { Id = 1, Name = "Test Deck" };
            var gate = new TaskCompletionSource<int>();
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
            var gate = new TaskCompletionSource<int>();
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
            var tag = new Tags { Id = 1, Name = "Test Tag" };
            var gate = new TaskCompletionSource<int>();
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
    }
}
