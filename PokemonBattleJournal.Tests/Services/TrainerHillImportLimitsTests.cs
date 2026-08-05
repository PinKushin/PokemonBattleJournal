using System.Text;
using System.Text.Json;
using PokemonBattleJournal.Services.Import;

namespace PokemonBattleJournal.Tests.Services
{
    /// <summary>
    /// The import reads a file the user chose, so a hostile or merely enormous document must be
    /// refused before it reaches the database.
    /// </summary>
    /// <remarks>
    /// Every case asserts <c>SaveAsync</c> was never called, not just that the return value looks
    /// right. Rejecting the document but having already written archetype or tag rows would be a
    /// silent failure: those rows persist after the import "fails".
    /// </remarks>
    public class TrainerHillImportLimitsTests
    {
        private ISqliteConnectionFactory _factory = null!;
        private IMatchOperations _matches = null!;
        private TrainerHillImportService _sut = null!;

        [SetUp]
        public void SetUp()
        {
            _matches = Substitute.For<IMatchOperations>();
            _factory = Substitute.For<ISqliteConnectionFactory>();
            _factory.Matches.Returns(_matches);

            // Any DB access at all is a failure in these tests — every limit must be enforced
            // before the first query. Making the connection throw proves that rather than
            // assuming it.
            _factory.GetDatabaseAsync().Returns(
                Task.FromException<SQLiteAsyncConnection>(
                    new InvalidOperationException("the import touched the database despite being over a limit")));

            ILimitlessMetaService meta = Substitute.For<ILimitlessMetaService>();
            meta.GetTopDecksAsync(Arg.Any<int>()).Returns(Task.FromResult(new List<MetaDeck>()));

            _sut = new TrainerHillImportService(
                _factory, Substitute.For<ILogger<TrainerHillImportService>>(), meta);
        }

        private static MemoryStream Json(string json) => new(Encoding.UTF8.GetBytes(json));

        private static string EntryJson(string playing = "other", string against = "other", string? notes = null)
        {
            string notesJson = notes is null ? "null" : JsonSerializer.Serialize(notes);
            return "{\"playing\":" + JsonSerializer.Serialize(playing)
                + ",\"against\":" + JsonSerializer.Serialize(against)
                + ",\"time\":\"2026-07-27 19:45:24\",\"result\":\"Win\""
                + ",\"game1\":{\"result\":\"Win\",\"turn\":1,\"tags\":[],\"notes\":" + notesJson + "}}";
        }

        private async Task AssertRejectedAsync(Stream stream, string expectedErrorFragment)
        {
            (int imported, List<string> errors) = await _sut.ImportAsync(stream, trainerId: 1);

            imported.ShouldBe(0);
            errors.ShouldContain(
                e => e.Contains(expectedErrorFragment, StringComparison.OrdinalIgnoreCase),
                $"No error mentioned '{expectedErrorFragment}'. Errors: {string.Join(" | ", errors)}");
            await _matches.DidNotReceive().SaveAsync(Arg.Any<MatchEntry>(), Arg.Any<List<Game>>());
        }

        [Test]
        public async Task ImportAsync_StreamOverByteLimit_RejectsWithoutReading()
        {
            // Reports an over-limit Length without allocating it — the seekable path must decide
            // from Length alone and never read the body.
            using var huge = new OverstatedLengthStream(TrainerHillImportService.MaxBytes + 1);

            await AssertRejectedAsync(huge, "too large");
            huge.ReadWasAttempted.ShouldBeFalse("an over-limit stream must be rejected before it is read");
        }

        [Test]
        public async Task ImportAsync_NonSeekableStreamOverByteLimit_Rejects()
        {
            // Android content-provider streams are not seekable, so Length is unavailable and the
            // cap has to be enforced while reading.
            byte[] payload = Encoding.UTF8.GetBytes("[" + new string(' ', (int)TrainerHillImportService.MaxBytes) + "]");
            using var stream = new NonSeekableStream(payload);

            await AssertRejectedAsync(stream, "too large");
        }

        [Test]
        public async Task ImportAsync_DeeplyNestedJson_Rejects()
        {
            // Nesting depth is bounded well below System.Text.Json's default of 64; the real
            // format is only array > entry > game > tags deep.
            string nested = new string('[', 200) + new string(']', 200);

            await AssertRejectedAsync(Json(nested), "json");
        }

        [Test]
        public async Task ImportAsync_TooManyEntries_RejectsEntireDocument()
        {
            // Refuses the whole document rather than importing the first N. A partial match log
            // is worse than none: the user cannot tell which entries are missing.
            string entries = string.Join(",",
                Enumerable.Repeat(EntryJson(), TrainerHillImportService.MaxEntries + 1));

            await AssertRejectedAsync(Json($"[{entries}]"), "too many entries");
        }

        [Test]
        public async Task ImportAsync_ArchetypeNameOverLengthLimit_SkipsEntry()
        {
            // Archetype names create DB rows on import, so an unbounded name is persistent junk
            // rather than a transient parse cost.
            string longSlug = new('a', TrainerHillImportService.MaxNameLength + 1);

            await AssertRejectedAsync(Json($"[{EntryJson(playing: longSlug)}]"), "name too long");
        }

        [Test]
        public async Task ImportAsync_NotesOverLengthLimit_SkipsEntry()
        {
            string longNotes = new('n', TrainerHillImportService.MaxNotesLength + 1);

            await AssertRejectedAsync(Json($"[{EntryJson(notes: longNotes)}]"), "notes too long");
        }

        [Test]
        public async Task ImportAsync_EmptyArray_ImportsNothingWithoutError()
        {
            (int imported, List<string> errors) = await _sut.ImportAsync(Json("[]"), trainerId: 1);

            imported.ShouldBe(0);
            errors.ShouldBeEmpty();
            await _matches.DidNotReceive().SaveAsync(Arg.Any<MatchEntry>(), Arg.Any<List<Game>>());
        }

        /// <summary>Reports a Length it does not have, and records whether anything read it.</summary>
        private sealed class OverstatedLengthStream : MemoryStream
        {
            private readonly long _reportedLength;

            public OverstatedLengthStream(long reportedLength) => _reportedLength = reportedLength;

            public bool ReadWasAttempted { get; private set; }

            public override long Length => _reportedLength;

            public override int Read(byte[] buffer, int offset, int count)
            {
                ReadWasAttempted = true;
                return base.Read(buffer, offset, count);
            }

            public override int Read(Span<byte> buffer)
            {
                ReadWasAttempted = true;
                return base.Read(buffer);
            }
        }

        /// <summary>Mimics a stream whose Length cannot be queried, as on Android.</summary>
        private sealed class NonSeekableStream(byte[] data) : Stream
        {
            private readonly MemoryStream _inner = new(data);

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position
            {
                get => _inner.Position;
                set => throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
            public override int Read(Span<byte> buffer) => _inner.Read(buffer);
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                if (disposing) _inner.Dispose();
                base.Dispose(disposing);
            }
        }
    }
}
