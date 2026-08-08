using PokemonBattleJournal.Services.Restore;
using PokemonBattleJournal.Tests.Fixtures;

namespace PokemonBattleJournal.Tests.ViewModels
{
    /// <summary>
    /// Staging and applying conflict decisions.
    /// </summary>
    /// <remarks>
    /// The rule these pin is that the app must never imply more was saved than was. Choosing a
    /// resolution writes nothing; only Apply writes, only for rows the user answered, and the
    /// status afterwards has to distinguish what went in from what is still waiting.
    /// </remarks>
    public class OptionsPageViewModelConflictTests
    {
        private OptionsPageViewModel _viewModel = null!;
        private IRestoreService _restoreService = null!;
        private RecordingLogger<OptionsPageViewModel> _logger = null!;

        [SetUp]
        public void SetUp()
        {
            _logger = new RecordingLogger<OptionsPageViewModel>();
            _restoreService = Substitute.For<IRestoreService>();

            ISqliteConnectionFactory factory = Substitute.For<ISqliteConnectionFactory>();
            factory.Trainers.Returns(Substitute.For<ITrainerOperations>());
            factory.Tags.Returns(Substitute.For<ITagOperations>());
            factory.Archetypes.Returns(Substitute.For<IArchetypeOperations>());
            factory.Matches.Returns(Substitute.For<IMatchOperations>());
            factory.Trainers.GetAllAsync().Returns([]);
            factory.Archetypes.GetAllAsync().Returns([]);
            factory.Tags.GetAllAsync().Returns([]);

            ITrainerSwitchService switchService = Substitute.For<ITrainerSwitchService>();
            MainPageViewModel mainPageVm = new(
                Substitute.For<ILogger<MainPageViewModel>>(), factory,
                Substitute.For<IMatchResultsCalculatorFactory>(), switchService,
                Substitute.For<IErrorHandler>());
            AppShellViewModel shellVm = new(
                switchService, mainPageVm, Substitute.For<ILogger<AppShellViewModel>>());

            _viewModel = new OptionsPageViewModel(
                _logger, factory, switchService, shellVm,
                Substitute.For<ITrainerHillImportService>(), Substitute.For<IExportService>(),
                _restoreService, Substitute.For<IErrorHandler>(),
                new PokemonBattleJournal.Logging.SentryPerformanceMonitor());
        }

        private static RestoreConflict Contradicting(uint id) => new()
        {
            TrainerName = "Ash",
            ExistingMatchId = id,
            StartTime = new DateTime(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc),
            Description = "game 1 notes differ",
            Games = [new ConflictGameDiff { Label = "Game 1", ExistingNotes = "mine", IncomingNotes = "theirs" }],
        };

        private static RestoreConflict Richer(uint id) => new()
        {
            TrainerName = "Ash",
            ExistingMatchId = id,
            StartTime = new DateTime(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc),
            Description = "game 1 notes differ",
            Games = [new ConflictGameDiff { Label = "Game 1", ExistingNotes = "", IncomingNotes = "theirs" }],
        };

        private async Task RestoreWithAsync(params RestoreConflict[] conflicts)
        {
            _restoreService.RestoreBackupAsync(Arg.Any<string>())
                .Returns(new RestoreResult { Conflicts = conflicts });
            await _viewModel.ApplyRestoreAsync("{}");
        }

        [Test]
        public async Task ApplyRestoreAsync_PublishesConflictsForReview()
        {
            await RestoreWithAsync(Contradicting(1), Contradicting(2));

            _viewModel.Conflicts.Count.ShouldBe(2);
            _viewModel.HasConflicts.ShouldBeTrue();
        }

        [Test]
        public async Task ApplyRestoreAsync_ARicherConflict_ArrivesPreSelectedAsAppend()
        {
            await RestoreWithAsync(Richer(1));

            _viewModel.Conflicts[0].SelectedResolution.ShouldBe(ConflictResolution.Append);
            _viewModel.Conflicts[0].WasSuggested.ShouldBeTrue();
        }

        [Test]
        public async Task ApplyRestoreAsync_AGenuineConflict_ArrivesUnanswered()
        {
            await RestoreWithAsync(Contradicting(1));

            _viewModel.Conflicts[0].SelectedResolution.ShouldBeNull();
            _viewModel.Conflicts[0].IsResolved.ShouldBeFalse();
        }

        [Test]
        public async Task ChoosingAResolution_WritesNothing()
        {
            // The whole point of staging. Selecting must not reach the service.
            await RestoreWithAsync(Contradicting(1));

            _viewModel.Conflicts[0].ChooseReplaceCommand.Execute(null);

            await _restoreService.DidNotReceive()
                .ApplyResolutionAsync(Arg.Any<RestoreConflict>(), Arg.Any<ConflictResolution>());
        }

        [Test]
        public async Task ApplyConflictsAsync_AppliesOnlyTheAnsweredRows()
        {
            await RestoreWithAsync(Contradicting(1), Contradicting(2));
            _restoreService.ApplyResolutionAsync(Arg.Any<RestoreConflict>(), Arg.Any<ConflictResolution>())
                .Returns(1);
            _viewModel.Conflicts[0].ChooseReplaceCommand.Execute(null);

            await _viewModel.ApplyConflictsAsync();

            await _restoreService.Received(1)
                .ApplyResolutionAsync(Arg.Is<RestoreConflict>(c => c != null && c.ExistingMatchId == 1), ConflictResolution.Replace);
            await _restoreService.DidNotReceive()
                .ApplyResolutionAsync(Arg.Is<RestoreConflict>(c => c != null && c.ExistingMatchId == 2), Arg.Any<ConflictResolution>());
        }

        [Test]
        public async Task ApplyConflictsAsync_RemovesAppliedRowsAndKeepsTheRest()
        {
            await RestoreWithAsync(Contradicting(1), Contradicting(2));
            _restoreService.ApplyResolutionAsync(Arg.Any<RestoreConflict>(), Arg.Any<ConflictResolution>())
                .Returns(1);
            _viewModel.Conflicts[0].ChooseKeepCommand.Execute(null);

            await _viewModel.ApplyConflictsAsync();

            _viewModel.Conflicts.Count.ShouldBe(1, "the unanswered row still needs a decision");
            _viewModel.Conflicts[0].Conflict.ExistingMatchId.ShouldBe(2u);
        }

        [Test]
        public async Task ApplyConflictsAsync_StatusSaysWhatWasAppliedAndWhatIsLeft()
        {
            await RestoreWithAsync(Contradicting(1), Contradicting(2), Contradicting(3));
            _restoreService.ApplyResolutionAsync(Arg.Any<RestoreConflict>(), Arg.Any<ConflictResolution>())
                .Returns(1);
            _viewModel.Conflicts[0].ChooseReplaceCommand.Execute(null);
            _viewModel.Conflicts[1].ChooseKeepCommand.Execute(null);

            await _viewModel.ApplyConflictsAsync();

            _viewModel.RestoreStatusMessage.ShouldContain("2 applied");
            _viewModel.RestoreStatusMessage.ShouldContain("1 still needs review");
        }

        [Test]
        public async Task ApplyConflictsAsync_WithNothingAnswered_DoesNotTouchTheService()
        {
            await RestoreWithAsync(Contradicting(1));

            await _viewModel.ApplyConflictsAsync();

            await _restoreService.DidNotReceive()
                .ApplyResolutionAsync(Arg.Any<RestoreConflict>(), Arg.Any<ConflictResolution>());
            _viewModel.Conflicts.Count.ShouldBe(1);
        }

        [Test]
        public async Task ApplyRestoreAsync_WithNoConflicts_LeavesTheListEmpty()
        {
            _restoreService.RestoreBackupAsync(Arg.Any<string>())
                .Returns(new RestoreResult { MatchesInserted = 3 });

            await _viewModel.ApplyRestoreAsync("{}");

            _viewModel.Conflicts.ShouldBeEmpty();
            _viewModel.HasConflicts.ShouldBeFalse();
        }

        [Test]
        public void SeedSampleConflicts_ProducesOneAnsweredAndOneUnansweredRow()
        {
            // Debug-only affordance. The conflict section is invisible until a conflict exists,
            // so without this a UI test would have to perform a whole export/edit/restore cycle
            // through a file picker Appium cannot drive.
            _viewModel.SeedSampleConflictsCommand.Execute(null);

            _viewModel.Conflicts.Count.ShouldBe(2);
            _viewModel.HasConflicts.ShouldBeTrue();
            _viewModel.Conflicts.Count(c => c.IsResolved)
                .ShouldBe(1, "the richer sample must arrive pre-selected and the contradicting one blank");
        }

        [Test]
        public void SeedSampleConflicts_UsesMatchIdsThatCannotExist()
        {
            // The samples are applied through the real service. Pointing them at ids no row can
            // hold means Apply reports nothing done instead of rewriting somebody's match.
            _viewModel.SeedSampleConflictsCommand.Execute(null);

            _viewModel.Conflicts.ShouldAllBe(c => c.Conflict.ExistingMatchId > uint.MaxValue - 10);
        }

        [Test]
        public async Task ApplyRestoreAsync_Twice_DoesNotAccumulateStaleConflicts()
        {
            // Restoring a second file must replace the outstanding list, not append to it —
            // otherwise rows referring to the first file linger with no way to tell them apart.
            await RestoreWithAsync(Contradicting(1), Contradicting(2));
            await RestoreWithAsync(Contradicting(3));

            _viewModel.Conflicts.Count.ShouldBe(1);
            _viewModel.Conflicts[0].Conflict.ExistingMatchId.ShouldBe(3u);
        }
    }
}
