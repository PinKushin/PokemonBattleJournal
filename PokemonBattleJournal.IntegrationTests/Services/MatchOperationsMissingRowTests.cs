using PokemonBattleJournal.Services;

namespace PokemonBattleJournal.IntegrationTests.Services;

/// <summary>
/// Asking for a match that is not there.
/// </summary>
/// <remarks>
/// A missing row is a normal answer, not a fault. <c>GetWithChildrenAsync</c> disagrees — it
/// throws rather than returning null — and the catch around it calls
/// <see cref="IErrorHandler.HandleError"/>, which in the app puts a modal on screen.
///
/// That surfaced through the restore conflict UI: applying a decision for a match the user had
/// since deleted is an expected situation the feature has a code path for, and it popped an
/// error dialog anyway. It went unnoticed in tests because the shared integration factory uses
/// NullErrorHandler, which swallows without recording — so the call happened and nothing looked
/// wrong.
/// </remarks>
public class MatchOperationsMissingRowTests
{
    private sealed class RecordingFactory : SqliteConnectionFactory, IAsyncDisposable
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"pbjmiss_{Guid.NewGuid():N}.db3");

        public RecordingFactory(IErrorHandler errorHandler)
            : base(NullLogger<SqliteConnectionFactory>.Instance, Meta(), errorHandler)
        {
        }

        private static ILimitlessMetaService Meta()
        {
            ILimitlessMetaService meta = Substitute.For<ILimitlessMetaService>();
            meta.GetTopDecksAsync(Arg.Any<int>()).Returns([]);
            return meta;
        }

        protected override string GetDbPath() => _dbPath;

        public async ValueTask DisposeAsync()
        {
            await Task.Delay(100);
            try { File.Delete(_dbPath); } catch (IOException) { /* best effort on a file SQLite may still hold */ }
        }
    }

    private IErrorHandler _errorHandler = null!;
    private RecordingFactory _factory = null!;

    [SetUp]
    public void SetUp()
    {
        _errorHandler = Substitute.For<IErrorHandler>();
        _factory = new RecordingFactory(_errorHandler);
    }

    [TearDown]
    public async Task TearDown() => await _factory.DisposeAsync();

    [Test]
    public async Task GetByIdAsync_ForAMissingRow_ReturnsNull()
    {
        (await _factory.Matches.GetByIdAsync(uint.MaxValue)).ShouldBeNull();
    }

    [Test]
    public async Task GetByIdAsync_ForAMissingRow_DoesNotRaiseAnErrorToTheUser()
    {
        // The real assertion. Returning null while also showing a modal would satisfy the test
        // above and still be the bug.
        _ = await _factory.Matches.GetByIdAsync(uint.MaxValue);

        _errorHandler.DidNotReceive().HandleError(Arg.Any<Exception>());
    }

    [Test]
    public async Task GetByIdAsync_ForARowThatExists_StillReturnsIt()
    {
        // Guards the fix from becoming "always return null".
        SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();
        Trainer trainer = new() { Name = "Ash", IsActive = true };
        _ = await db.InsertAsync(trainer);
        Archetype archetype = new() { Name = "Other", ImagePath = "substitute.png" };
        _ = await db.InsertAsync(archetype);

        MatchEntry match = new()
        {
            TrainerId = trainer.Id,
            PlayingId = archetype.Id,
            AgainstId = archetype.Id,
            Result = MatchResult.Win,
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddMinutes(20),
            DatePlayed = DateTime.UtcNow.Date,
        };
        _ = await _factory.Matches.SaveAsync(match, [new Game { Result = MatchResult.Win, Turn = 1 }]);

        MatchEntry? found = await _factory.Matches.GetByIdAsync(match.Id);

        found.ShouldNotBeNull();
        found.Id.ShouldBe(match.Id);
        _errorHandler.DidNotReceive().HandleError(Arg.Any<Exception>());
    }
}
