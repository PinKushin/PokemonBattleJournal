using PokemonBattleJournal.IntegrationTests.Infrastructure;
using PokemonBattleJournal.Services.Export;
using PokemonBattleJournal.Services.Restore;

namespace PokemonBattleJournal.IntegrationTests.Services;

/// <summary>
/// Applying a user's Keep/Append/Replace decision to a real conflicted match.
/// </summary>
/// <remarks>
/// Conflicts are produced the way a user produces them — export a match, edit the stored copy,
/// restore the file — rather than by hand-building a RestoreConflict. A hand-built one would
/// let the shape the service emits drift from the shape it accepts and still pass.
/// </remarks>
public class RestoreConflictResolutionTests
{
    private TestSqliteConnectionFactory _factory = null!;
    private ExportService _export = null!;
    private RestoreService _sut = null!;
    private uint _trainerId;
    private uint _playingId;
    private uint _againstId;

    [SetUp]
    public async Task SetUp()
    {
        // Unstubbed would return null, which ArchetypeOperations swallows into an empty list —
        // archetypes then go missing silently rather than failing. See
        // project_integration_test_isolation.
        ILimitlessMetaService meta = Substitute.For<ILimitlessMetaService>();
        meta.GetTopDecksAsync(Arg.Any<int>()).Returns([]);

        _factory = new TestSqliteConnectionFactory(meta);
        _export = new ExportService(_factory, NullLogger<ExportService>.Instance);
        _sut = new RestoreService(_factory, NullLogger<RestoreService>.Instance, new PokemonBattleJournal.Logging.SentryPerformanceMonitor());

        SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();

        Trainer trainer = new() { Name = "Ash", IsActive = true };
        _ = await db.InsertAsync(trainer);
        _trainerId = trainer.Id;

        Archetype playing = new() { Name = "Dragapult ex", ImagePath = "dragapult.png" };
        _ = await db.InsertAsync(playing);
        _playingId = playing.Id;

        Archetype against = new() { Name = "Other", ImagePath = "substitute.png" };
        _ = await db.InsertAsync(against);
        _againstId = against.Id;
    }

    [TearDown]
    public async Task TearDown() => await _factory.DisposeAsync();

    private static readonly DateTime Start = new(2026, 7, 27, 19, 45, 24, DateTimeKind.Utc);

    /// <summary>
    /// Seeds a match, exports it, then rewrites the stored note so a restore sees a conflict.
    /// </summary>
    private async Task<(RestoreConflict Conflict, uint MatchId)> ProduceConflictAsync(
        string storedNote, string backupNote)
    {
        MatchEntry match = new()
        {
            TrainerId = _trainerId,
            PlayingId = _playingId,
            AgainstId = _againstId,
            Result = MatchResult.Win,
            DatePlayed = Start.Date,
            StartTime = Start,
            EndTime = Start.AddMinutes(20),
        };
        Game game = new() { Result = MatchResult.Win, Turn = 1, Notes = backupNote };
        (await _factory.Matches.SaveAsync(match, [game]))
            .ShouldBeGreaterThan(0, "seeding must succeed or the test proves nothing");

        // The backup now holds backupNote.
        string backup = await _export.ExportBackupAsync();

        // Now the stored copy diverges — the user edited it after taking the backup.
        MatchEntry stored = (await _factory.Matches.GetByIdAsync(match.Id))!;
        stored.Game1!.Notes = storedNote;
        (await _factory.Matches.SaveAsync(stored, [stored.Game1!]))
            .ShouldBeGreaterThan(0, "rewriting the stored note must succeed");

        RestoreResult result = await _sut.RestoreBackupAsync(backup);
        result.Conflicts.Count.ShouldBe(1, "the edited note must produce exactly one conflict");
        return (result.Conflicts[0], match.Id);
    }

    private async Task<string?> StoredNoteAsync(uint matchId) =>
        (await _factory.Matches.GetByIdAsync(matchId))!.Game1!.Notes;

    [Test]
    public async Task ApplyResolutionAsync_Keep_WritesNothing()
    {
        (RestoreConflict conflict, uint matchId) = await ProduceConflictAsync("mine", "theirs");

        int affected = await _sut.ApplyResolutionAsync(conflict, ConflictResolution.Keep);

        affected.ShouldBe(0, "Keep means the stored match is already what the user wants");
        (await StoredNoteAsync(matchId)).ShouldBe("mine");
    }

    [Test]
    public async Task ApplyResolutionAsync_Replace_TakesTheBackupsNote()
    {
        (RestoreConflict conflict, uint matchId) = await ProduceConflictAsync("mine", "theirs");

        int affected = await _sut.ApplyResolutionAsync(conflict, ConflictResolution.Replace);

        affected.ShouldBeGreaterThan(0);
        (await StoredNoteAsync(matchId)).ShouldBe("theirs");
    }

    [Test]
    public async Task ApplyResolutionAsync_Append_KeepsBothNotes()
    {
        (RestoreConflict conflict, uint matchId) = await ProduceConflictAsync("mine", "theirs");

        int affected = await _sut.ApplyResolutionAsync(conflict, ConflictResolution.Append);

        affected.ShouldBeGreaterThan(0);
        (await StoredNoteAsync(matchId))
            .ShouldBe("mine" + ConflictResolver.NoteSeparator + "theirs");
    }

    [Test]
    public async Task ApplyResolutionAsync_LeavesTheRestOfTheMatchAlone()
    {
        // Resolution edits games. Everything else about the match — its timings, its archetypes,
        // its result — must survive untouched, or a note decision quietly rewrites the row.
        (RestoreConflict conflict, uint matchId) = await ProduceConflictAsync("mine", "theirs");

        _ = await _sut.ApplyResolutionAsync(conflict, ConflictResolution.Replace);

        MatchEntry after = (await _factory.Matches.GetByIdAsync(matchId))!;
        after.StartTime.ShouldBe(Start);
        after.EndTime.ShouldBe(Start.AddMinutes(20));
        after.PlayingId.ShouldBe(_playingId);
        after.AgainstId.ShouldBe(_againstId);
        after.Result.ShouldBe(MatchResult.Win);
    }

    [Test]
    public async Task ApplyResolutionAsync_ForAMatchThatNoLongerExists_ReportsNothingDone()
    {
        // The user deletes the match from the journal while the conflict list is on screen.
        // Staging means an Apply can arrive against a row that is gone, and that must not throw.
        (RestoreConflict conflict, uint matchId) = await ProduceConflictAsync("mine", "theirs");
        _ = await _factory.Matches.DeleteAsync((await _factory.Matches.GetByIdAsync(matchId))!);

        int affected = await _sut.ApplyResolutionAsync(conflict, ConflictResolution.Replace);

        affected.ShouldBe(0);
    }

    [Test]
    public async Task ApplyResolutionAsync_DoesNotResurrectTheConflictOnASecondRestore()
    {
        // After resolving, restoring the same file again must see the match as identical rather
        // than conflicting — otherwise the user answers the same question forever.
        MatchEntry match = new()
        {
            TrainerId = _trainerId,
            PlayingId = _playingId,
            AgainstId = _againstId,
            Result = MatchResult.Win,
            DatePlayed = Start.Date,
            StartTime = Start,
            EndTime = Start.AddMinutes(20),
        };
        Game game = new() { Result = MatchResult.Win, Turn = 1, Notes = "theirs" };
        _ = await _factory.Matches.SaveAsync(match, [game]);
        string backup = await _export.ExportBackupAsync();

        MatchEntry stored = (await _factory.Matches.GetByIdAsync(match.Id))!;
        stored.Game1!.Notes = "mine";
        _ = await _factory.Matches.SaveAsync(stored, [stored.Game1!]);

        RestoreResult first = await _sut.RestoreBackupAsync(backup);
        _ = await _sut.ApplyResolutionAsync(first.Conflicts[0], ConflictResolution.Replace);

        RestoreResult second = await _sut.RestoreBackupAsync(backup);

        second.Conflicts.ShouldBeEmpty("the stored note now matches the backup");
        second.MatchesSkippedIdentical.ShouldBe(1);
    }
}
