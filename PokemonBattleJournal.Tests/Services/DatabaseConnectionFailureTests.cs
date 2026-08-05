using PokemonBattleJournal.Tests.Fixtures;

namespace PokemonBattleJournal.Tests.Services
{
    /// <summary>
    /// Every database operation must survive the connection itself failing, not just the query.
    /// </summary>
    /// <remarks>
    /// Opening the connection is the single most likely thing to fail on a real device — a
    /// corrupt or locked .db3, a revoked storage permission, a full disk. These are unit tests
    /// rather than integration tests because the failure is injected at the
    /// <see cref="ISqliteConnectionFactory"/> seam; no SQLite file is involved.
    ///
    /// Each case also asserts the lock is left at rest. Releasing a semaphore that was never
    /// acquired throws <c>SemaphoreFullException</c> from the <c>finally</c>, which would both
    /// mask the real error and replace the method's return value.
    /// </remarks>
    public class DatabaseConnectionFailureTests
    {
        private static readonly InvalidOperationException Boom = new("database unavailable");

        private SemaphoreSlim _gate = null!;
        private IErrorHandler _errorHandler = null!;
        private RecordingLogger<DatabaseConnectionFailureTests> _logger = null!;

        private TagOperations _tags = null!;
        private TrainerOperations _trainers = null!;
        private MatchOperations _matches = null!;
        private ArchetypeOperations _archetypes = null!;

        [SetUp]
        public void SetUp()
        {
            _gate = new SemaphoreSlim(1, 1);
            _errorHandler = Substitute.For<IErrorHandler>();
            _logger = new RecordingLogger<DatabaseConnectionFailureTests>();

            ISqliteConnectionFactory factory = Substitute.For<ISqliteConnectionFactory>();
            factory.GetDatabaseAsync().Returns(Task.FromException<SQLiteAsyncConnection>(Boom));
            factory.GetLock().Returns(_gate);

            ILimitlessMetaService metaService = Substitute.For<ILimitlessMetaService>();
            metaService.GetTopDecksAsync(Arg.Any<int>()).Returns(Task.FromResult(new List<MetaDeck>()));

            _tags = new TagOperations(factory, _logger, _errorHandler);
            _trainers = new TrainerOperations(factory, _logger, _errorHandler);
            _matches = new MatchOperations(factory, _logger, _errorHandler);
            _archetypes = new ArchetypeOperations(factory, _logger, metaService, _errorHandler);
        }

        [TearDown]
        public void TearDown() => _gate.Dispose();

        /// <summary>
        /// Operations that surface the failure to the user through <see cref="IErrorHandler"/>.
        /// </summary>
        private static IEnumerable<TestCaseData> SurfacingOperations()
        {
            yield return Case("TagOperations.GetByIdAsync", t => t._tags.GetByIdAsync(1));
            yield return Case("TagOperations.SaveAsync", t => t._tags.SaveAsync("Lucky", 1));
            yield return Case("TagOperations.DeleteAsync", t => t._tags.DeleteAsync(new Tags { Id = 1 }));

            yield return Case("TrainerOperations.GetAllAsync", t => t._trainers.GetAllAsync());
            yield return Case("TrainerOperations.GetByNameAsync", t => t._trainers.GetByNameAsync("Ash"));
            yield return Case("TrainerOperations.GetByIdAsync", t => t._trainers.GetByIdAsync(1));
            yield return Case("TrainerOperations.DeleteAsync", t => t._trainers.DeleteAsync(new Trainer { Id = 1 }));
            yield return Case("TrainerOperations.SaveAsync", t => t._trainers.SaveAsync("Ash"));

            yield return Case("MatchOperations.GetAllAsync", t => t._matches.GetAllAsync());
            yield return Case("MatchOperations.SaveAsync", t => t._matches.SaveAsync(
                new MatchEntry { TrainerId = 1, PlayingId = 1, AgainstId = 1 }, [new Game()]));
            yield return Case("MatchOperations.GetByIdAsync", t => t._matches.GetByIdAsync(1));
            yield return Case("MatchOperations.GetByTrainerIdAsync", t => t._matches.GetByTrainerIdAsync(1));
            yield return Case("MatchOperations.DeleteAsync", t => t._matches.DeleteAsync(new MatchEntry { Id = 1 }));

            yield return Case("ArchetypeOperations.GetByIdAsync", t => t._archetypes.GetByIdAsync(1));
            yield return Case("ArchetypeOperations.SaveAsync", t => t._archetypes.SaveAsync("Regidrago", "regidrago.png", 1));
            yield return Case("ArchetypeOperations.DeleteAsync", t => t._archetypes.DeleteAsync(new Archetype { Id = 1 }));
        }

        /// <summary>
        /// Operations that log without invoking <see cref="IErrorHandler"/> on purpose: they run
        /// from AppearingAsync, and a ContentDialog raised before the page's XamlRoot is composed
        /// crashes WinUI (0xc000027b).
        /// </summary>
        private static IEnumerable<TestCaseData> LogOnlyOperations()
        {
            yield return Case("TagOperations.GetAllAsync", t => t._tags.GetAllAsync());
            yield return Case("ArchetypeOperations.GetAllAsync", t => t._archetypes.GetAllAsync());
            yield return Case("TrainerOperations.GetActiveAsync", t => t._trainers.GetActiveAsync());
            yield return Case("TrainerOperations.SetActiveAsync", t => t._trainers.SetActiveAsync(new Trainer { Id = 1 }));
        }

        private static TestCaseData Case(string name, Func<DatabaseConnectionFailureTests, Task> invoke) =>
            new TestCaseData(invoke).SetName($"{{m}}({name})");

        [TestCaseSource(nameof(SurfacingOperations))]
        public async Task Operation_ConnectionFails_ReportsToErrorHandler(
            Func<DatabaseConnectionFailureTests, Task> invoke)
        {
            await invoke(this);

            _errorHandler.Received(1).HandleError(Boom);
            AssertLoggedAndLockReleased();
        }

        [TestCaseSource(nameof(LogOnlyOperations))]
        public async Task Operation_ConnectionFails_LogsWithoutErrorHandler(
            Func<DatabaseConnectionFailureTests, Task> invoke)
        {
            await invoke(this);

            _errorHandler.DidNotReceive().HandleError(Arg.Any<Exception>());
            AssertLoggedAndLockReleased();
        }

        private void AssertLoggedAndLockReleased()
        {
            _logger.Entries.ShouldContain(
                e => e.Level == LogLevel.Error && e.Exception == Boom,
                $"Connection failure was swallowed without an Error log. Logged:{Environment.NewLine}{_logger.Dump()}");

            _gate.CurrentCount.ShouldBe(1, "the lock was never acquired, so nothing should have released it");
        }
    }
}
