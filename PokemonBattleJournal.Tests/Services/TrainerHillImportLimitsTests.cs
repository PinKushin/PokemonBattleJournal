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
            // The importer now indexes existing matches to skip duplicates. An unstubbed
            // NSubstitute call returns null, which the real ops layer never does.
            _matches.GetByTrainerIdAsync(Arg.Any<uint>(), Arg.Any<bool>()).Returns([]);

            // Any DB access at all is a failure in these tests — every limit must be enforced
            // before the first query. Making the connection throw proves that rather than
            // assuming it.
            _factory.GetDatabaseAsync().Returns(
                Task.FromException<SQLiteAsyncConnection>(
                    new InvalidOperationException("the import touched the database despite being over a limit")));

            ILimitlessMetaService meta = Substitute.For<ILimitlessMetaService>();
            meta.GetTopDecksAsync(Arg.Any<int>()).Returns(Task.FromResult(new List<MetaDeck>()));

            _sut = new TrainerHillImportService(
                _factory, Substitute.For<ILogger<TrainerHillImportService>>(), meta, new PokemonBattleJournal.Logging.SentryPerformanceMonitor());
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
            (int imported, _, List<string> errors) = await _sut.ImportAsync(stream, trainerId: 1);

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
            using OverstatedLengthStream huge = new OverstatedLengthStream(TrainerHillImportService.MaxBytes + 1);

            await AssertRejectedAsync(huge, "too large");
            huge.ReadWasAttempted.ShouldBeFalse("an over-limit stream must be rejected before it is read");
        }

        [Test]
        public async Task ImportAsync_NonSeekableStreamOverByteLimit_Rejects()
        {
            // Android content-provider streams are not seekable, so Length is unavailable and the
            // cap has to be enforced while reading.
            byte[] payload = Encoding.UTF8.GetBytes("[" + new string(' ', (int)TrainerHillImportService.MaxBytes) + "]");
            using NonSeekableStream stream = new NonSeekableStream(payload);

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
            (int imported, _, List<string> errors) = await _sut.ImportAsync(Json("[]"), trainerId: 1);

            imported.ShouldBe(0);
            errors.ShouldBeEmpty();
            await _matches.DidNotReceive().SaveAsync(Arg.Any<MatchEntry>(), Arg.Any<List<Game>>());
        }

        // The limit tests all push PAST a bound, so every one of them is satisfied by a check
        // that fires too eagerly. These land exactly ON the bounds, which is where `>` and `>=`
        // stop agreeing — Stryker survived swapping both of them (2026-08-10).

        [Test]
        public async Task ImportAsync_NonSeekableStreamExactlyAtByteLimit_IsAccepted()
        {
            // MaxBytes itself is legal; only more than MaxBytes is not. Rejecting here would
            // refuse a file the app claims to support, and no over-limit test can see it.
            //
            // This also covers the whole read loop's exit condition: with the accumulate check
            // inverted the very first chunk is refused, and with it widened to >= this exact
            // payload is refused.
            byte[] payload = Encoding.UTF8.GetBytes(
                "[" + new string(' ', (int)TrainerHillImportService.MaxBytes - 2) + "]");
            payload.Length.ShouldBe((int)TrainerHillImportService.MaxBytes, "the payload must sit exactly on the bound");
            using NonSeekableStream stream = new NonSeekableStream(payload);

            (int imported, _, List<string> errors) = await _sut.ImportAsync(stream, trainerId: 1);

            imported.ShouldBe(0);
            errors.ShouldBeEmpty($"a file of exactly MaxBytes must be accepted. Errors: {string.Join(" | ", errors)}");
        }

        // A non-seekable stream is buffered chunk by chunk, and nothing asserted that the buffer
        // actually receives the bytes — deleting the write left every existing test green,
        // because they all reject before reaching it.
        //
        // This is also the only test that runs the loop to EOF, which is what exposes its `> 0`
        // exit condition: widened to `>= 0` it spins forever on the zero-length read at EOF.
        // There is no [Timeout] here because NUnit's is unsupported on this TFM ("TargetFramework
        // doesn't support timeout on tests") — Stryker's own per-mutant timeout catches that one,
        // and a Timeout counts as killed.
        [Test]
        public async Task ImportAsync_NonSeekableStreamUnderLimit_BuffersTheWholeBody()
        {
            using NonSeekableStream stream = new NonSeekableStream(Encoding.UTF8.GetBytes("[]"));

            (int imported, _, List<string> errors) = await _sut.ImportAsync(stream, trainerId: 1);

            imported.ShouldBe(0);
            // If the buffered copy never received the bytes, the parse sees an empty document and
            // reports invalid JSON instead of an empty array.
            errors.ShouldBeEmpty($"the body was not buffered. Errors: {string.Join(" | ", errors)}");
        }

        [Test]
        public async Task ImportAsync_NotesExactlyAtLengthLimit_IsAccepted()
        {
            // Same boundary argument one level down: MaxNotesLength characters is allowed, and
            // the existing test only proves MaxNotesLength + 1 is not.
            string notes = new string('n', TrainerHillImportService.MaxNotesLength);

            (int imported, _, List<string> errors) = await _sut.ImportAsync(
                Json($"[{EntryJson(notes: notes)}]"), trainerId: 1);

            errors.ShouldNotContain(
                e => e.Contains("notes too long", StringComparison.OrdinalIgnoreCase),
                "notes of exactly MaxNotesLength must be accepted");
            // The import itself still fails at the database seam this fixture injects, which is
            // fine — the claim under test is about validation, and it is made by the ABSENCE of
            // the length error rather than by the import succeeding.
            imported.ShouldBe(0);
        }

        [Test]
        public async Task ImportAsync_OverlongDeckName_TruncatesItInTheError()
        {
            // The error text quotes the deck name so the user can find the entry in their own
            // file, which means an absurd name would otherwise be echoed back in full. The
            // truncation could be disabled entirely without any test noticing.
            string absurd = new string('x', TrainerHillImportService.MaxNameLength + 50);

            (_, _, List<string> errors) = await _sut.ImportAsync(
                Json($"[{EntryJson(playing: absurd)}]"), trainerId: 1);

            string reported = errors.ShouldHaveSingleItem();
            // Explicit Contains rather than ShouldContain: on a string the latter binds to the
            // IEnumerable<char> overload and compares characters, not substrings.
            reported.Contains('…', StringComparison.Ordinal)
                .ShouldBeTrue($"an overlong name must be shown truncated. Reported: {reported}");
            reported.Contains(absurd, StringComparison.Ordinal)
                .ShouldBeFalse("the untruncated name must never be echoed back");
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
