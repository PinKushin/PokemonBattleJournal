using PokemonBattleJournal.Tests.Fixtures;

namespace PokemonBattleJournal.Tests.Services
{
    /// <summary>
    /// Every database operation must survive the connection itself failing, not just the query —
    /// and must do so whichever category of failure arrives.
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
    ///
    /// <para>
    /// The operations catch three exception types separately — <c>ArgumentException</c>,
    /// <c>SQLiteException</c> and a general <c>Exception</c> — so that a bad-input failure and a
    /// storage failure are distinguishable in a crash report rather than arriving as one
    /// undifferentiated error. Only the general one used to be exercised: injecting an
    /// <c>InvalidOperationException</c> falls past the two typed handlers into the last one, so
    /// 45 mutants across the typed catch bodies were reported by Stryker as NoCoverage — never
    /// executed by any test at all. Running every operation under all three categories is what
    /// reaches them.
    /// </para>
    /// </remarks>
    public class DatabaseConnectionFailureTests
    {
        /// <summary>Which <c>catch</c> clause the injected failure is meant to land in.</summary>
        public enum FailureKind
        {
            /// <summary>Lands in <c>catch (ArgumentException)</c>.</summary>
            InvalidData,

            /// <summary>Lands in <c>catch (SQLiteException)</c>.</summary>
            Database,

            /// <summary>Falls past both typed handlers into <c>catch (Exception)</c>.</summary>
            Unexpected,
        }

        private SemaphoreSlim _gate = null!;
        private ISqliteConnectionFactory _factory = null!;
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

            _factory = Substitute.For<ISqliteConnectionFactory>();
            _factory.GetLock().Returns(_gate);

            ILimitlessMetaService metaService = Substitute.For<ILimitlessMetaService>();
            metaService.GetTopDecksAsync(Arg.Any<int>()).Returns(Task.FromResult(new List<MetaDeck>()));

            _tags = new TagOperations(_factory, _logger, _errorHandler);
            _trainers = new TrainerOperations(_factory, _logger, _errorHandler);
            _matches = new MatchOperations(_factory, _logger, _errorHandler);
            _archetypes = new ArchetypeOperations(_factory, _logger, metaService, _errorHandler);
        }

        [TearDown]
        public void TearDown() => _gate.Dispose();

        /// <summary>
        /// Arms the factory to fail with an exception of the requested category and returns the
        /// instance, so the assertions can demand that exact object rather than merely its type.
        /// </summary>
        private Exception Arm(FailureKind kind)
        {
            Exception failure = kind switch
            {
                // A real ArgumentException from this layer looks like a column/type mismatch.
                FailureKind.InvalidData => new ArgumentException("no such column: Nmae"),

                // SQLiteException has no public constructor; New is the supported factory.
                FailureKind.Database => SQLiteException.New(
                    SQLite3.Result.Corrupt, "database disk image is malformed"),

                FailureKind.Unexpected => new InvalidOperationException("database unavailable"),

                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "unhandled failure kind"),
            };

            _factory.GetDatabaseAsync().Returns(Task.FromException<SQLiteAsyncConnection>(failure));
            return failure;
        }

        private static IEnumerable<(string Name, Func<DatabaseConnectionFailureTests, Task> Invoke)> SurfacingOps()
        {
            yield return ("TagOperations.GetByIdAsync", t => t._tags.GetByIdAsync(1));
            yield return ("TagOperations.SaveAsync", t => t._tags.SaveAsync("Lucky", 1));
            yield return ("TagOperations.DeleteAsync", t => t._tags.DeleteAsync(new Tags { Id = 1 }));

            yield return ("TrainerOperations.GetAllAsync", t => t._trainers.GetAllAsync());
            yield return ("TrainerOperations.GetByNameAsync", t => t._trainers.GetByNameAsync("Ash"));
            yield return ("TrainerOperations.GetByIdAsync", t => t._trainers.GetByIdAsync(1));
            yield return ("TrainerOperations.DeleteAsync", t => t._trainers.DeleteAsync(new Trainer { Id = 1 }));
            yield return ("TrainerOperations.SaveAsync", t => t._trainers.SaveAsync("Ash"));

            yield return ("MatchOperations.GetAllAsync", t => t._matches.GetAllAsync());
            yield return ("MatchOperations.SaveAsync", t => t._matches.SaveAsync(
                new MatchEntry { TrainerId = 1, PlayingId = 1, AgainstId = 1 }, [new Game()]));
            yield return ("MatchOperations.GetByIdAsync", t => t._matches.GetByIdAsync(1));
            yield return ("MatchOperations.GetByTrainerIdAsync", t => t._matches.GetByTrainerIdAsync(1));
            yield return ("MatchOperations.DeleteAsync", t => t._matches.DeleteAsync(new MatchEntry { Id = 1 }));

            yield return ("ArchetypeOperations.GetByIdAsync", t => t._archetypes.GetByIdAsync(1));
            yield return ("ArchetypeOperations.SaveAsync", t => t._archetypes.SaveAsync("Regidrago", "regidrago.png", 1));
            yield return ("ArchetypeOperations.DeleteAsync", t => t._archetypes.DeleteAsync(new Archetype { Id = 1 }));
        }

        /// <summary>
        /// Operations that log without invoking <see cref="IErrorHandler"/> on purpose: they run
        /// from AppearingAsync, and a ContentDialog raised before the page's XamlRoot is composed
        /// crashes WinUI (0xc000027b).
        /// </summary>
        private static IEnumerable<(string Name, Func<DatabaseConnectionFailureTests, Task> Invoke)> LogOnlyOps()
        {
            yield return ("TagOperations.GetAllAsync", t => t._tags.GetAllAsync());
            yield return ("ArchetypeOperations.GetAllAsync", t => t._archetypes.GetAllAsync());
            yield return ("TrainerOperations.GetActiveAsync", t => t._trainers.GetActiveAsync());
            yield return ("TrainerOperations.SetActiveAsync", t => t._trainers.SetActiveAsync(new Trainer { Id = 1 }));
        }

        /// <summary>Every operation crossed with every failure category.</summary>
        private static IEnumerable<TestCaseData> Cross(
            IEnumerable<(string Name, Func<DatabaseConnectionFailureTests, Task> Invoke)> ops)
        {
            foreach ((string name, Func<DatabaseConnectionFailureTests, Task> invoke) in ops)
            {
                foreach (FailureKind kind in Enum.GetValues<FailureKind>())
                {
                    yield return new TestCaseData(invoke, kind).SetName($"{{m}}({name}, {kind})");
                }
            }
        }

        private static IEnumerable<TestCaseData> SurfacingOperations() => Cross(SurfacingOps());

        private static IEnumerable<TestCaseData> LogOnlyOperations() => Cross(LogOnlyOps());

        [TestCaseSource(nameof(SurfacingOperations))]
        public async Task Operation_ConnectionFails_ReportsToErrorHandler(
            Func<DatabaseConnectionFailureTests, Task> invoke, FailureKind kind)
        {
            Exception failure = Arm(kind);

            await invoke(this);

            _errorHandler.Received(1).HandleError(failure);
            AssertLoggedAndLockReleased(failure);
        }

        [TestCaseSource(nameof(LogOnlyOperations))]
        public async Task Operation_ConnectionFails_LogsWithoutErrorHandler(
            Func<DatabaseConnectionFailureTests, Task> invoke, FailureKind kind)
        {
            Exception failure = Arm(kind);

            await invoke(this);

            _errorHandler.DidNotReceive().HandleError(Arg.Any<Exception>());
            AssertLoggedAndLockReleased(failure);
        }

        private void AssertLoggedAndLockReleased(Exception failure)
        {
            RecordingLogger<DatabaseConnectionFailureTests>.Entry entry =
                _logger.Entries.FirstOrDefault(e => e.Level == LogLevel.Error && e.Exception == failure)
                ?? throw new AssertionException(
                    $"Connection failure was swallowed without an Error log. Logged:{Environment.NewLine}{_logger.Dump()}");

            // The message, not just the level and the exception object. A handler whose template
            // is empty still produces an entry at Error carrying the exception, so asserting only
            // those two cannot tell a real log line from a blank one — and a blank line is what
            // reaches a crash report. Deliberately not asserting the wording: pinning that would
            // fail on every rephrase without detecting anything a reader would care about.
            entry.Message.ShouldNotBeNullOrWhiteSpace(
                $"logged at Error with no message. Logged:{Environment.NewLine}{_logger.Dump()}");

            _gate.CurrentCount.ShouldBe(1, "the lock was never acquired, so nothing should have released it");
        }
    }
}
