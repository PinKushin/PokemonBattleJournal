using PokemonBattleJournal.Services.Import;
using PokemonBattleJournal.Services.Restore;
using PokemonBattleJournal.Tests.Fixtures;

namespace PokemonBattleJournal.Tests.Services
{
    /// <summary>
    /// Import and restore each wrap their whole body in a span, and each ends with
    /// <c>catch (Exception) { span.SetFailed(); throw; }</c>. This pins that clause.
    /// </summary>
    /// <remarks>
    /// Stryker reported those two catch bodies as NoCoverage in both mutation-box runs: every
    /// existing test drives these services down paths that either succeed or collect errors and
    /// return, so nothing ever threw past the body and the handler was never entered.
    ///
    /// <para>
    /// It matters more than a log line would. A span that is never marked failed is not merely
    /// missing detail — it is recorded as a SUCCESSFUL operation of that duration. An import that
    /// crashed would appear in tracing as an import that worked, which is worse than no span at
    /// all, and the error channel would not correct it because the exception is rethrown to a
    /// caller that may itself only log.
    /// </para>
    ///
    /// <para>
    /// Each test carries a control: the same service on a path that does not throw must NOT mark
    /// the span failed. Without it, an implementation that called <c>SetFailed</c> unconditionally
    /// would pass, and "marks failure" and "marks everything" would be indistinguishable.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class SpanFailureOnThrowTests
    {
        private static readonly InvalidOperationException Boom = new("database unavailable");

        private ITimedSpan _span = null!;
        private IPerformanceMonitor _monitor = null!;
        private ISqliteConnectionFactory _factory = null!;

        [SetUp]
        public void SetUp()
        {
            _span = Substitute.For<ITimedSpan>();
            _monitor = Substitute.For<IPerformanceMonitor>();
            _monitor.StartSpan(Arg.Any<string>(), Arg.Any<string>()).Returns(_span);
            _factory = Substitute.For<ISqliteConnectionFactory>();
        }

        private RestoreService BuildRestore() =>
            new(_factory, new RecordingLogger<RestoreService>(), _monitor);

        private TrainerHillImportService BuildImport()
        {
            ILimitlessMetaService meta = Substitute.For<ILimitlessMetaService>();
            meta.GetTopDecksAsync(Arg.Any<int>()).Returns(Task.FromResult(new List<MetaDeck>()));
            return new TrainerHillImportService(
                _factory, new RecordingLogger<TrainerHillImportService>(), meta, _monitor);
        }

        /// <summary>A valid envelope, so the failure comes from the database and not a guard.</summary>
        private const string OneTrainerBackup =
            """{"version":1,"exportedUtc":"2026-08-11T00:00:00Z","archetypes":[],"trainers":[{"name":"Ash","matches":[]}]}""";

        [Test]
        public async Task RestoreBackupAsync_DatabaseThrows_MarksTheSpanFailedAndRethrows()
        {
            // Thrown from the first thing the core asks the database for, so the exception
            // travels the whole body rather than being handled by an inner catch.
            _factory.Trainers.GetByNameAsync(Arg.Any<string>())
                .Returns(Task.FromException<Trainer?>(Boom));

            RestoreService sut = BuildRestore();

            InvalidOperationException thrown = await Should.ThrowAsync<InvalidOperationException>(
                async () => await sut.RestoreBackupAsync(OneTrainerBackup));

            thrown.ShouldBeSameAs(Boom, "the original exception must reach the caller, not a wrapper");
            _span.Received(1).SetFailed();
        }

        [Test]
        public async Task RestoreBackupAsync_RefusedByAGuard_MarksTheSpanFailedButDoesNotThrow()
        {
            // The control, and it is not the obvious one. A refusal DOES mark the span failed —
            // deliberately, because a restore that returns errors has not worked either — so
            // "SetFailed was called" cannot by itself distinguish the catch clause from the guard
            // path. What separates them is that a guard RETURNS a result and the catch RETHROWS.
            RestoreService sut = BuildRestore();

            RestoreResult result = await sut.RestoreBackupAsync(string.Empty);

            result.Errors.ShouldNotBeEmpty();
            result.MatchesInserted.ShouldBe(0);
            _ = _factory.DidNotReceive().GetDatabaseAsync();
        }

        [Test]
        public async Task ImportAsync_TheFileCannotBeRead_MarksTheSpanFailedAndRethrows()
        {
            // A database failure does NOT reach the outer handler — ImportCoreAsync catches those
            // per entry and returns them as errors. The reachable route is the stream itself
            // failing mid-read, which is what a disconnected share or a revoked permission looks
            // like, and which no parse or per-entry handler covers.
            TrainerHillImportService sut = BuildImport();
            using ThrowingStream json = new(Boom);

            InvalidOperationException thrown = await Should.ThrowAsync<InvalidOperationException>(
                async () => await sut.ImportAsync(json, trainerId: 1));

            thrown.ShouldBeSameAs(Boom, "the original exception must reach the caller, not a wrapper");
            _span.Received(1).SetFailed();
        }

        /// <summary>A stream that fails on first read, standing in for I/O dying mid-import.</summary>
        private sealed class ThrowingStream(Exception failure) : Stream
        {
            private readonly Exception _failure = failure;

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count) => throw _failure;

            public override ValueTask<int> ReadAsync(
                Memory<byte> buffer, CancellationToken cancellationToken = default) => throw _failure;

            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
