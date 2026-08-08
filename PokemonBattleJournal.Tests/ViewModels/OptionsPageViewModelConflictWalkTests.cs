using PokemonBattleJournal.Services.Restore;
using PokemonBattleJournal.Tests.Fixtures;

namespace PokemonBattleJournal.Tests.ViewModels
{
    /// <summary>
    /// Walking conflicts one match at a time, the way a rebase walks commits.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The review used to render every conflict at once inside a CollectionView. Two problems
    /// with that, and only one of them is cosmetic. A restore can carry any number of conflicted
    /// matches, so the panel grew without bound inside a page that already scrolls. And the
    /// buttons lived in a DataTemplate, so they were virtualised — realised, recycled and
    /// re-realised as rows scrolled, which is what produced the CI race where
    /// ApplyConflictsButton existed in the UIA tree carrying no invokable pattern.
    /// </para>
    /// <para>
    /// One match at a time fixes both: a single set of controls, built once and re-bound, and a
    /// panel of fixed height. The decision is per MATCH — SelectedResolution lives on the row,
    /// not the game — so a match's differing games still stack inside the one card. That is
    /// bounded at three by BO3, unlike the match count.
    /// </para>
    /// <para>
    /// Choices stay staged. Walking away from a match keeps its answer, and nothing reaches the
    /// database until Apply, which is the existing contract these must not break.
    /// </para>
    /// </remarks>
    public class OptionsPageViewModelConflictWalkTests
    {
        private OptionsPageViewModel _viewModel = null!;
        private IRestoreService _restoreService = null!;

        [SetUp]
        public void SetUp()
        {
            ISqliteConnectionFactory factory = Substitute.For<ISqliteConnectionFactory>();
            factory.Trainers.Returns(Substitute.For<ITrainerOperations>());
            factory.Tags.Returns(Substitute.For<ITagOperations>());
            factory.Archetypes.Returns(Substitute.For<IArchetypeOperations>());
            factory.Matches.Returns(Substitute.For<IMatchOperations>());
            factory.Trainers.GetAllAsync().Returns([]);
            factory.Archetypes.GetAllAsync().Returns([]);
            factory.Tags.GetAllAsync().Returns([]);

            _restoreService = Substitute.For<IRestoreService>();

            ITrainerSwitchService switchService = Substitute.For<ITrainerSwitchService>();
            MainPageViewModel mainPageVm = new(
                Substitute.For<ILogger<MainPageViewModel>>(), factory,
                Substitute.For<IMatchResultsCalculatorFactory>(), switchService,
                Substitute.For<IErrorHandler>());
            AppShellViewModel shellVm = new(
                switchService, mainPageVm, Substitute.For<ILogger<AppShellViewModel>>());

            _viewModel = new OptionsPageViewModel(
                new RecordingLogger<OptionsPageViewModel>(), factory, switchService, shellVm,
                Substitute.For<ITrainerHillImportService>(), Substitute.For<IExportService>(),
                _restoreService, Substitute.For<IErrorHandler>());
        }

        private static RestoreConflict Contradicting(uint id) => new()
        {
            TrainerName = "Ash",
            ExistingMatchId = id,
            StartTime = new DateTime(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc).AddMinutes(id),
            Description = $"match {id} differs",
            Games = [new ConflictGameDiff { Label = "Game 1", ExistingNotes = "mine", IncomingNotes = "theirs" }],
        };

        private async Task RestoreWithAsync(params RestoreConflict[] conflicts)
        {
            _restoreService.RestoreBackupAsync(Arg.Any<string>())
                .Returns(new RestoreResult { Conflicts = conflicts });
            await _viewModel.ApplyRestoreAsync("{}");
        }

        [Test]
        public async Task AfterRestore_TheWalkStartsAtTheFirstConflict()
        {
            await RestoreWithAsync(Contradicting(1), Contradicting(2), Contradicting(3));

            // Identity, not "is not null". A walk that silently started in the middle would pass
            // a null check and show the user the wrong match.
            _viewModel.CurrentConflict!.Conflict.ExistingMatchId.ShouldBe(1u);
            _viewModel.ConflictPositionLabel.ShouldBe("Match 1 of 3");
        }

        [Test]
        public async Task NextConflict_AdvancesOneMatch()
        {
            await RestoreWithAsync(Contradicting(1), Contradicting(2), Contradicting(3));

            _viewModel.NextConflictCommand.Execute(null);

            _viewModel.CurrentConflict!.Conflict.ExistingMatchId.ShouldBe(2u);
            _viewModel.ConflictPositionLabel.ShouldBe("Match 2 of 3");
        }

        [Test]
        public async Task PreviousConflict_GoesBackSoAChoiceCanBeRevised()
        {
            await RestoreWithAsync(Contradicting(1), Contradicting(2));

            _viewModel.NextConflictCommand.Execute(null);
            _viewModel.PreviousConflictCommand.Execute(null);

            _viewModel.CurrentConflict!.Conflict.ExistingMatchId.ShouldBe(1u);
        }

        [Test]
        public async Task AtTheLastConflict_ThereIsNoNext()
        {
            await RestoreWithAsync(Contradicting(1), Contradicting(2));

            _viewModel.HasNextConflict.ShouldBeTrue("two conflicts, sitting on the first");
            _viewModel.NextConflictCommand.Execute(null);
            _viewModel.HasNextConflict.ShouldBeFalse();
            _viewModel.HasPreviousConflict.ShouldBeTrue();
        }

        [Test]
        public async Task AtTheFirstConflict_ThereIsNoPrevious()
        {
            await RestoreWithAsync(Contradicting(1), Contradicting(2));

            _viewModel.HasPreviousConflict.ShouldBeFalse();
        }

        [Test]
        public async Task NextPastTheEnd_StaysOnTheLastRatherThanFallingOffIt()
        {
            await RestoreWithAsync(Contradicting(1), Contradicting(2));

            _viewModel.NextConflictCommand.Execute(null);
            _viewModel.NextConflictCommand.Execute(null);
            _viewModel.NextConflictCommand.Execute(null);

            // A clamp, not a wrap. Wrapping would silently send the user back to a match they
            // already answered and read as "nothing happened".
            _viewModel.CurrentConflict!.Conflict.ExistingMatchId.ShouldBe(2u);
        }

        [Test]
        public async Task AChoiceMadeOnOneMatch_SurvivesWalkingAwayAndBack()
        {
            await RestoreWithAsync(Contradicting(1), Contradicting(2));

            _viewModel.CurrentConflict!.ChooseReplaceCommand.Execute(null);
            _viewModel.NextConflictCommand.Execute(null);
            _viewModel.PreviousConflictCommand.Execute(null);

            _viewModel.CurrentConflict!.SelectedResolution.ShouldBe(ConflictResolution.Replace);
        }

        [Test]
        public async Task AnsweringOneMatchDoesNotAnswerItsNeighbour()
        {
            // The control. Without a second match, "set the current one" and "set them all" are
            // indistinguishable — the walk binds one row and it must not write through to others.
            await RestoreWithAsync(Contradicting(1), Contradicting(2));

            _viewModel.CurrentConflict!.ChooseKeepCommand.Execute(null);

            _viewModel.Conflicts[0].SelectedResolution.ShouldBe(ConflictResolution.Keep);
            _viewModel.Conflicts[1].SelectedResolution.ShouldBeNull("the bystander must stay unanswered");
        }

        [Test]
        public async Task ApplyingTheAnsweredMatches_LeavesTheWalkOnSomethingThatStillExists()
        {
            // Apply removes the answered rows from the collection. If the index is left pointing
            // past the end, CurrentConflict throws or goes null while HasConflicts is still true,
            // and the panel renders empty with no way out.
            await RestoreWithAsync(Contradicting(1), Contradicting(2), Contradicting(3));

            _viewModel.NextConflictCommand.Execute(null);
            _viewModel.NextConflictCommand.Execute(null);
            _viewModel.CurrentConflict!.ChooseKeepCommand.Execute(null);

            _restoreService.ApplyResolutionAsync(Arg.Any<RestoreConflict>(), Arg.Any<ConflictResolution>())
                .Returns(1);

            await _viewModel.ApplyConflictsAsync();

            _viewModel.Conflicts.Count.ShouldBe(2, "the two unanswered matches remain");
            _viewModel.HasConflicts.ShouldBeTrue();
            _viewModel.CurrentConflict.ShouldNotBeNull();
            _viewModel.ConflictPositionLabel.ShouldBe("Match 2 of 2");
        }

        [Test]
        public async Task WhenEveryConflictIsApplied_TheWalkEmptiesOut()
        {
            await RestoreWithAsync(Contradicting(1));

            _viewModel.CurrentConflict!.ChooseKeepCommand.Execute(null);
            _restoreService.ApplyResolutionAsync(Arg.Any<RestoreConflict>(), Arg.Any<ConflictResolution>())
                .Returns(1);

            await _viewModel.ApplyConflictsAsync();

            _viewModel.HasConflicts.ShouldBeFalse();
            _viewModel.CurrentConflict.ShouldBeNull();
            _viewModel.ConflictPositionLabel.ShouldBe(string.Empty);
        }
    }
}
