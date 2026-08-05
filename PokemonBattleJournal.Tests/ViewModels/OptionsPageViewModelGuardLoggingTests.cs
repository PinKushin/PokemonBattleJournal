using PokemonBattleJournal.Tests.Fixtures;

namespace PokemonBattleJournal.Tests.ViewModels
{
    /// <summary>
    /// Every user-initiated save command has a guard that declines to act when its inputs are
    /// incomplete. Those guards must say so.
    /// </summary>
    /// <remarks>
    /// Written after a CI failure that cost a logcat dig to diagnose: on Android,
    /// OptionsPage_SaveTag_WithName_Saves failed because the text never landed in TagInput
    /// after a scrollIntoView, so <c>TagInput</c> was still null and <c>SaveTagAsync</c>
    /// returned at its guard. The app logged nothing at all — neither "Tag saved" nor "Tag
    /// not saved" — so from the outside a broken interaction was indistinguishable from a
    /// working app that had simply been told to do nothing. The eight test failures that
    /// followed were a cascade off that one silent return.
    ///
    /// These assert on message content, not just "something was logged", because the value is
    /// entirely in being able to tell WHICH input was missing when reading a log after the fact.
    /// </remarks>
    public class OptionsPageViewModelGuardLoggingTests
    {
        private OptionsPageViewModel _viewModel = null!;
        private ISqliteConnectionFactory _mockConnectionFactory = null!;
        private RecordingLogger<OptionsPageViewModel> _logger = null!;

        [SetUp]
        public void SetUp()
        {
            _logger = new RecordingLogger<OptionsPageViewModel>();
            _mockConnectionFactory = Substitute.For<ISqliteConnectionFactory>();
            ITrainerSwitchService switchService = Substitute.For<ITrainerSwitchService>();

            _mockConnectionFactory.Trainers.Returns(Substitute.For<ITrainerOperations>());
            _mockConnectionFactory.Tags.Returns(Substitute.For<ITagOperations>());
            _mockConnectionFactory.Archetypes.Returns(Substitute.For<IArchetypeOperations>());
            _mockConnectionFactory.Matches.Returns(Substitute.For<IMatchOperations>());

            var mainPageVm = new MainPageViewModel(
                Substitute.For<ILogger<MainPageViewModel>>(),
                _mockConnectionFactory,
                Substitute.For<IMatchResultsCalculatorFactory>(),
                switchService);
            var shellVm = new AppShellViewModel(
                switchService,
                mainPageVm,
                Substitute.For<ILogger<AppShellViewModel>>());

            _viewModel = new OptionsPageViewModel(
                _logger, _mockConnectionFactory, switchService, shellVm,
                Substitute.For<ITrainerHillImportService>());
        }

        private void ShouldHaveWarned(string containing)
        {
            _logger.EntriesMatching(LogLevel.Warning, containing)
                .ShouldNotBeEmpty($"expected a Warning containing '{containing}'. Logged:{Environment.NewLine}{_logger.Dump()}");
        }

        [Test]
        public async Task SaveTagAsync_NullInput_LogsWarning()
        {
            _viewModel.TagInput = null;

            await _viewModel.SaveTagAsync();

            ShouldHaveWarned("tag");
        }

        [Test]
        public async Task SaveTagAsync_NoActiveTrainer_LogsWarning()
        {
            // TagInput is supplied, so the only reason to decline is the missing trainer —
            // and the message must make that distinction, not just say "cannot save".
            _viewModel.TagInput = "Lucky";

            await _viewModel.SaveTagAsync();

            ShouldHaveWarned("trainer");
        }

        [Test]
        public async Task SaveTrainerAsync_NullInput_LogsWarning()
        {
            _viewModel.NameInput = null;

            await _viewModel.SaveTrainerAsync();

            ShouldHaveWarned("name");
        }

        [Test]
        public async Task SaveArchetypeAsync_NullName_LogsWarning()
        {
            _viewModel.NewDeckName = null;

            await _viewModel.SaveArchetypeAsync();

            ShouldHaveWarned("name");
        }

        [Test]
        public async Task SaveArchetypeAsync_NullIcon_LogsWarning()
        {
            // NewDeckIcon is pre-initialised to ball_icon.png precisely so this guard does not
            // fire silently (see project_options_vm_bugs_fixed); null it explicitly to reach it.
            _viewModel.NewDeckName = "Gholdengo";
            _viewModel.NewDeckIcon = null;

            await _viewModel.SaveArchetypeAsync();

            ShouldHaveWarned("icon");
        }

        [Test]
        public async Task GuardedSave_DoesNotLogAsError()
        {
            // Declining incomplete input is expected operation, not a fault. Warning is the
            // right level: visible in logs and in Sentry breadcrumbs, without raising an
            // error event for what is usually just an empty field.
            _viewModel.TagInput = null;

            await _viewModel.SaveTagAsync();

            _logger.Entries.ShouldNotContain(
                e => e.Level >= LogLevel.Error,
                $"guards should warn, not error. Logged:{Environment.NewLine}{_logger.Dump()}");
        }
    }
}
