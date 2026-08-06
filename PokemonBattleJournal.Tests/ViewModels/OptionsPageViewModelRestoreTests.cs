using PokemonBattleJournal.Services.Restore;
using PokemonBattleJournal.Tests.Fixtures;

namespace PokemonBattleJournal.Tests.ViewModels
{
    /// <summary>
    /// The status line a restore leaves behind, and the command that produces it.
    /// </summary>
    /// <remarks>
    /// Weighted towards the wording rather than the plumbing on purpose. A restore is the one
    /// operation a user runs when something has already gone wrong, and the only evidence they
    /// get that it worked is this sentence — there is no modal (see project_error_handler_di)
    /// and the matches themselves live on another page.
    ///
    /// The import status message previously reported the *error* count using the word
    /// "skipped", which reads as "these were already here" when it meant "these failed":
    /// opposite meanings, and the reassuring one was shown for the alarming case. These tests
    /// exist so that cannot come back on the restore path.
    /// </remarks>
    public class OptionsPageViewModelRestoreTests
    {
        private OptionsPageViewModel _viewModel = null!;
        private IRestoreService _restoreService = null!;
        private IErrorHandler _errorHandler = null!;
        private RecordingLogger<OptionsPageViewModel> _logger = null!;
        private ISqliteConnectionFactory _factory = null!;

        [SetUp]
        public void SetUp()
        {
            _logger = new RecordingLogger<OptionsPageViewModel>();
            _restoreService = Substitute.For<IRestoreService>();
            _errorHandler = Substitute.For<IErrorHandler>();

            ISqliteConnectionFactory factory = Substitute.For<ISqliteConnectionFactory>();
            _factory = factory;
            factory.Trainers.Returns(Substitute.For<ITrainerOperations>());
            factory.Tags.Returns(Substitute.For<ITagOperations>());
            factory.Archetypes.Returns(Substitute.For<IArchetypeOperations>());
            factory.Matches.Returns(Substitute.For<IMatchOperations>());

            // Unstubbed NSubstitute calls hand back null where the operations layer returns an
            // empty list, and the reload path assigns straight into the bound collections. See
            // feedback_mock_returns_null_not_empty.
            factory.Trainers.GetAllAsync().Returns([]);
            factory.Archetypes.GetAllAsync().Returns([]);
            factory.Tags.GetAllAsync().Returns([]);

            ITrainerSwitchService switchService = Substitute.For<ITrainerSwitchService>();
            var mainPageVm = new MainPageViewModel(
                Substitute.For<ILogger<MainPageViewModel>>(),
                factory,
                Substitute.For<IMatchResultsCalculatorFactory>(),
                switchService, Substitute.For<IErrorHandler>());
            var shellVm = new AppShellViewModel(
                switchService, mainPageVm, Substitute.For<ILogger<AppShellViewModel>>());

            _viewModel = new OptionsPageViewModel(
                _logger, factory, switchService, shellVm,
                Substitute.For<ITrainerHillImportService>(), Substitute.For<IExportService>(),
                _restoreService, _errorHandler);
        }

        [Test]
        public void DescribeRestore_FreshDatabase_ReportsMatchesAndTheTrainerItCreated()
        {
            string message = OptionsPageViewModel.DescribeRestore(new RestoreResult
            {
                TrainersCreated = 1,
                MatchesInserted = 12,
            });

            message.ShouldBe("Restored 12 matches, 1 trainer added");
        }

        [Test]
        public void DescribeRestore_OneMatch_ReadsAsSingular()
        {
            string message = OptionsPageViewModel.DescribeRestore(new RestoreResult { MatchesInserted = 1 });

            message.ShouldBe("Restored 1 match");
        }

        /// <summary>
        /// Restoring a backup onto the machine it came from is the common case, and it must not
        /// look like a failure.
        /// </summary>
        [Test]
        public void DescribeRestore_EverythingAlreadyPresent_SaysSoWithoutSoundingLikeAnError()
        {
            string message = OptionsPageViewModel.DescribeRestore(new RestoreResult
            {
                TrainersMerged = 1,
                MatchesSkippedIdentical = 40,
            });

            message.ShouldBe("Restored 0 matches, 40 already present");
        }

        /// <summary>
        /// Duplicates and failures are different things and must never share a word.
        /// </summary>
        [Test]
        public void DescribeRestore_DuplicatesAndErrors_CountsThemSeparately()
        {
            string message = OptionsPageViewModel.DescribeRestore(new RestoreResult
            {
                MatchesInserted = 3,
                MatchesSkippedIdentical = 1,
                Errors = ["entry 4: unparsable result", "entry 9: no game 1"],
            });

            message.ShouldBe("Restored 3 matches, 1 already present, 2 failed");
        }

        /// <summary>
        /// Conflicts are left in the file, not applied. Saying only "restored N" would report a
        /// partial restore as a complete one — the conflict resolution UI is a separate branch,
        /// so until it lands the count is the user's only signal that anything is outstanding.
        /// </summary>
        [Test]
        public void DescribeRestore_Conflicts_SaysTheyWereNotApplied()
        {
            string message = OptionsPageViewModel.DescribeRestore(new RestoreResult
            {
                MatchesInserted = 5,
                Conflicts =
                [
                    new RestoreConflict
                    {
                        TrainerName = "Ash",
                        ExistingMatchId = 3,
                        StartTime = new DateTime(2026, 7, 27, 19, 45, 0, DateTimeKind.Utc),
                        Description = "notes differ",
                    },
                ],
            });

            message.ShouldBe("Restored 5 matches, 1 needs review (not applied)");
        }

        /// <summary>
        /// A file the service refused outright must show why.
        /// </summary>
        /// <remarks>
        /// The whole-file rejections — wrong version, too large, unparsable — come back as a
        /// result with every count at zero and one human-readable reason. Rendering that as
        /// "Restored 0 matches, 1 failed" would hide the only sentence that tells the user
        /// whether to pick a different file or update the app.
        /// </remarks>
        [Test]
        public void DescribeRestore_WholeFileRejected_ShowsTheReasonInsteadOfACount()
        {
            string message = OptionsPageViewModel.DescribeRestore(new RestoreResult
            {
                Errors = ["This backup was written by a newer version of the app."],
            });

            message.ShouldBe("This backup was written by a newer version of the app.");
        }

        [Test]
        public void DescribeRestore_NothingInTheFile_SaysNothingToRestore()
        {
            string message = OptionsPageViewModel.DescribeRestore(new RestoreResult());

            message.ShouldBe("Backup contained no matches");
        }

        [Test]
        public async Task ApplyRestoreAsync_Success_PublishesTheStatusAndLowersTheBusyGate()
        {
            _restoreService.RestoreBackupAsync(Arg.Any<string>())
                .Returns(new RestoreResult { MatchesInserted = 2, TrainersCreated = 1 });

            await _viewModel.ApplyRestoreAsync("{}");

            _viewModel.RestoreStatusMessage.ShouldBe("Restored 2 matches, 1 trainer added");
            _viewModel.HasRestoreStatus.ShouldBeTrue();
            _viewModel.IsBusyMutating.ShouldBeFalse("the gate must come down once the restore returns");
        }

        [Test]
        public async Task ApplyRestoreAsync_ServiceThrows_ReportsFailureAndSurfacesTheError()
        {
            _restoreService.RestoreBackupAsync(Arg.Any<string>())
                .Returns<Task<RestoreResult>>(_ => throw new InvalidOperationException("db is gone"));

            await _viewModel.ApplyRestoreAsync("{}");

            _viewModel.RestoreStatusMessage.ShouldBe("Restore failed");
            _viewModel.IsBusyMutating.ShouldBeFalse("a thrown restore must not leave the gate up forever");
            _errorHandler.Received(1).HandleError(Arg.Any<Exception>());
            _logger.EntriesMatching(LogLevel.Error, "restore")
                .ShouldNotBeEmpty($"a failed restore must be logged. Logged:{Environment.NewLine}{_logger.Dump()}");
        }

        /// <summary>
        /// Per-entry errors are reported in the status line, but they are also the only place the
        /// detail exists — the status line carries a count, so the log must carry the text.
        /// </summary>
        [Test]
        public async Task ApplyRestoreAsync_PerEntryErrors_LogsTheDetailBehindTheCount()
        {
            _restoreService.RestoreBackupAsync(Arg.Any<string>())
                .Returns(new RestoreResult { MatchesInserted = 1, Errors = ["entry 4: unparsable result"] });

            await _viewModel.ApplyRestoreAsync("{}");

            _logger.EntriesMatching(LogLevel.Warning, "unparsable result")
                .ShouldNotBeEmpty($"the failing entry's reason must reach the log. Logged:{Environment.NewLine}{_logger.Dump()}");
        }

        /// <summary>
        /// A restore writes trainers, archetypes and tags straight into the database underneath
        /// the page that is currently displaying them. Those lists have to be re-read.
        /// </summary>
        /// <remarks>
        /// The case this protects is the one restore exists for: a fresh install, no trainer, the
        /// user restores a backup — and the trainer picker on the very page they are standing on
        /// still shows nothing, because <c>AllTrainers</c> was loaded by AppearingAsync before
        /// the data existed. It looks exactly like a restore that silently did nothing.
        /// </remarks>
        [Test]
        public async Task ApplyRestoreAsync_DataApplied_RereadsTheListsThePageIsShowing()
        {
            _restoreService.RestoreBackupAsync(Arg.Any<string>())
                .Returns(new RestoreResult { TrainersCreated = 1, MatchesInserted = 4 });
            _factory.Trainers.GetAllAsync().Returns([new Trainer { Name = "Ash" }]);
            _factory.Archetypes.GetAllAsync().Returns([new Archetype { Name = "Dragapult ex" }]);
            _factory.Tags.GetAllAsync().Returns([new Tags { Name = "League" }]);

            await _viewModel.ApplyRestoreAsync("{}");

            _viewModel.AllTrainers.ShouldHaveSingleItem().Name.ShouldBe("Ash");
            _viewModel.AllArchetypes.ShouldHaveSingleItem().Name.ShouldBe("Dragapult ex");
            _viewModel.AllTags.ShouldHaveSingleItem().Name.ShouldBe("League");
        }

        /// <summary>
        /// A refused file changed nothing, so there is nothing to re-read.
        /// </summary>
        [Test]
        public async Task ApplyRestoreAsync_NothingApplied_DoesNotReloadAnything()
        {
            _restoreService.RestoreBackupAsync(Arg.Any<string>())
                .Returns(new RestoreResult { Errors = ["This backup was written by a newer version of the app."] });

            await _viewModel.ApplyRestoreAsync("{}");

            await _factory.Trainers.DidNotReceive().GetAllAsync();
            await _factory.Archetypes.DidNotReceive().GetAllAsync();
            await _factory.Tags.DidNotReceive().GetAllAsync();
        }

        /// <summary>
        /// Everything already present is a no-op for the data, so it is a no-op for the lists.
        /// </summary>
        [Test]
        public async Task ApplyRestoreAsync_EverythingAlreadyPresent_DoesNotReloadAnything()
        {
            _restoreService.RestoreBackupAsync(Arg.Any<string>())
                .Returns(new RestoreResult { TrainersMerged = 1, MatchesSkippedIdentical = 40 });

            await _viewModel.ApplyRestoreAsync("{}");

            await _factory.Trainers.DidNotReceive().GetAllAsync();
        }
    }
}
