using PokemonBattleJournal.IntegrationTests.Infrastructure;
using PokemonBattleJournal.Services.Export;
using PokemonBattleJournal.Services.Restore;

namespace PokemonBattleJournal.IntegrationTests.Services;

/// <summary>
/// Restore against a real SQLite database, driven by real export output.
/// </summary>
/// <remarks>
/// Every test round-trips through <see cref="ExportService"/> rather than hand-writing JSON.
/// A restore's whole job is reading what the export wrote, so a hand-built fixture would let
/// the two drift apart and still pass — which is exactly how the missing startTime went
/// unnoticed until it was looked for deliberately.
/// </remarks>
public class RestoreServiceIntegrationTests
{
    private TestSqliteConnectionFactory _factory = null!;
    private ExportService _export = null!;
    private RestoreService _sut = null!;
    private uint _trainerId;
    private uint _dragapultId;
    private uint _otherId;

    [SetUp]
    public async Task SetUp()
    {
        // Must return an empty list, not an unstubbed null: ArchetypeOperations.GetAllAsync
        // faults on null and returns [] from its catch, which surfaces as archetypes silently
        // missing rather than as an error. See project_integration_test_isolation.
        ILimitlessMetaService meta = Substitute.For<ILimitlessMetaService>();
        meta.GetTopDecksAsync(Arg.Any<int>()).Returns([]);

        _factory = new TestSqliteConnectionFactory(meta);
        _export = new ExportService(_factory, NullLogger<ExportService>.Instance);
        _sut = new RestoreService(_factory, NullLogger<RestoreService>.Instance, new PokemonBattleJournal.Logging.SentryPerformanceMonitor());

        SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();

        Trainer trainer = new() { Name = "Ash", IsActive = true };
        _ = await db.InsertAsync(trainer);
        _trainerId = trainer.Id;

        Archetype dragapult = new() { Name = "Dragapult ex / Dusknoir", ImagePath = "dragapult.png", ImagePath2 = "dusknoir.png" };
        _ = await db.InsertAsync(dragapult);
        _dragapultId = dragapult.Id;

        Archetype other = new() { Name = "Other", ImagePath = "substitute.png" };
        _ = await db.InsertAsync(other);
        _otherId = other.Id;
    }

    [TearDown]
    public async Task TearDown() => await _factory.DisposeAsync();

    private async Task SeedMatchAsync(DateTime startTime, params Game[] games)
    {
        MatchEntry match = new()
        {
            TrainerId = _trainerId,
            PlayingId = _dragapultId,
            AgainstId = _otherId,
            Result = MatchResult.Win,
            DatePlayed = startTime.Date,
            StartTime = startTime,
            EndTime = startTime.AddMinutes(20),
        };
        (await _factory.Matches.SaveAsync(match, [.. games]))
            .ShouldBeGreaterThan(0, "seeding a match must succeed or the test proves nothing");
    }

    /// <summary>
    /// Restoring into a database that already holds the data must change nothing.
    /// </summary>
    /// <remarks>
    /// The common real case: a user restores a backup onto the machine it came from. Before
    /// duplicate detection existed, re-importing a file inserted every entry a second time,
    /// so this is the behaviour most worth pinning.
    /// </remarks>
    [Test]
    public async Task RestoreBackupAsync_SameDatabase_SkipsEverythingAsAlreadyPresent()
    {
        await SeedMatchAsync(new DateTime(2026, 7, 27, 19, 45, 24, DateTimeKind.Utc),
            new Game { Result = MatchResult.Win, Turn = 1, Notes = "clean start" });
        string backup = await _export.ExportBackupAsync();

        RestoreResult result = await _sut.RestoreBackupAsync(backup);

        result.MatchesInserted.ShouldBe(0);
        result.MatchesSkippedIdentical.ShouldBe(1, "the match is already present and identical");
        result.Conflicts.ShouldBeEmpty();
        result.Errors.ShouldBeEmpty();
        result.TrainersMerged.ShouldBe(1, "the trainer exists, so it must be merged rather than duplicated");
        result.TrainersCreated.ShouldBe(0);

        (await _factory.Matches.GetByTrainerIdAsync(_trainerId)).Count
            .ShouldBe(1, "restoring onto the same database must not duplicate matches");
        (await _factory.Trainers.GetAllAsync()).Count(t => t.Name == "Ash")
            .ShouldBe(1, "a second trainer of the same name would split the history");
    }

    /// <summary>
    /// Restoring into an empty database must rebuild the data, timings included.
    /// </summary>
    [Test]
    public async Task RestoreBackupAsync_EmptyDatabase_RebuildsMatchWithTimings()
    {
        DateTime start = new(2026, 7, 27, 19, 45, 24, DateTimeKind.Utc);
        await SeedMatchAsync(start, new Game { Result = MatchResult.Win, Turn = 1, Notes = "clean start" });
        string backup = await _export.ExportBackupAsync();

        // Wipe the matches, keeping the trainer — the shape of "I deleted something by mistake".
        foreach (MatchEntry match in await _factory.Matches.GetByTrainerIdAsync(_trainerId))
        {
            _ = await _factory.Matches.DeleteAsync(match);
        }
        (await _factory.Matches.GetByTrainerIdAsync(_trainerId)).ShouldBeEmpty();

        RestoreResult result = await _sut.RestoreBackupAsync(backup);

        result.MatchesInserted.ShouldBe(1);
        result.Errors.ShouldBeEmpty();

        MatchEntry restored = (await _factory.Matches.GetByTrainerIdAsync(_trainerId)).Single();
        restored.StartTime.ShouldBe(start, "start time drives CalculateAverageMatchDuration");
        restored.EndTime.ShouldBe(start.AddMinutes(20), "end time drives CalculateAverageMatchDuration");
        restored.Result.ShouldBe(MatchResult.Win);
        restored.Game1!.Notes.ShouldBe("clean start");
    }

    /// <summary>
    /// A trainer named in the backup but absent here is created, not silently dropped.
    /// </summary>
    [Test]
    public async Task RestoreBackupAsync_UnknownTrainer_CreatesThatTrainer()
    {
        await SeedMatchAsync(new DateTime(2026, 7, 27, 19, 45, 24, DateTimeKind.Utc),
            new Game { Result = MatchResult.Win, Turn = 1 });
        string backup = (await _export.ExportBackupAsync()).Replace("\"Ash\"", "\"Misty\"");

        RestoreResult result = await _sut.RestoreBackupAsync(backup);

        result.TrainersCreated.ShouldBe(1);
        Trainer? misty = await _factory.Trainers.GetByNameAsync("Misty");
        misty.ShouldNotBeNull();
        (await _factory.Matches.GetByTrainerIdAsync(misty.Id)).Count.ShouldBe(1);
    }

    /// <summary>
    /// A user's chosen archetype icon survives a restore into a database that never had it.
    /// </summary>
    [Test]
    public async Task RestoreBackupAsync_CustomArchetype_RestoresIconsAndOwner()
    {
        SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();
        _ = await db.InsertAsync(new Archetype
        {
            Name = "Pinku's Brew",
            ImagePath = "pikachu.png",
            ImagePath2 = "mimikyu.png",
            TrainerId = _trainerId,
        });
        string backup = await _export.ExportBackupAsync();

        _ = await db.ExecuteAsync("DELETE FROM Archetype WHERE Name = ?", "Pinku's Brew");

        RestoreResult result = await _sut.RestoreBackupAsync(backup);
        result.Errors.ShouldBeEmpty();

        Archetype restored = (await _factory.Archetypes.GetAllAsync())
            .Single(a => a.Name == "Pinku's Brew");
        restored.ImagePath.ShouldBe("pikachu.png", "guessing an icon from a name cannot recover a chosen one");
        restored.ImagePath2.ShouldBe("mimikyu.png");
        restored.TrainerId.ShouldBe(_trainerId, "a custom archetype belongs to the trainer who made it");
    }

    /// <summary>
    /// A same-key match whose data differs is reported, never merged or overwritten.
    /// </summary>
    /// <remarks>
    /// The key includes AgainstId, which identifies a *deck* rather than a person, so two
    /// different opponents on the same deck in the same minute collide. Overwriting on a key
    /// hit would therefore destroy a real match, which is why this reports instead.
    /// </remarks>
    [Test]
    public async Task RestoreBackupAsync_SameKeyDifferentNotes_ReportsConflictAndChangesNothing()
    {
        DateTime start = new(2026, 7, 27, 19, 45, 24, DateTimeKind.Utc);
        await SeedMatchAsync(start, new Game { Result = MatchResult.Win, Turn = 1, Notes = "original note" });
        string backup = (await _export.ExportBackupAsync()).Replace("original note", "different note");

        RestoreResult result = await _sut.RestoreBackupAsync(backup);

        result.MatchesInserted.ShouldBe(0, "a conflict must not insert a second copy");
        result.MatchesSkippedIdentical.ShouldBe(0);
        result.Conflicts.Count.ShouldBe(1);
        result.Conflicts[0].TrainerName.ShouldBe("Ash");
        result.Conflicts[0].Description.ShouldContain("notes");

        MatchEntry untouched = (await _factory.Matches.GetByTrainerIdAsync(_trainerId)).Single();
        untouched.Game1!.Notes.ShouldBe("original note", "the existing match must be left exactly as it was");
    }

    [Test]
    public async Task RestoreBackupAsync_NewerFormatVersion_RefusesRatherThanGuessing()
    {
        await SeedMatchAsync(new DateTime(2026, 7, 27, 19, 45, 24, DateTimeKind.Utc),
            new Game { Result = MatchResult.Win, Turn = 1 });
        string backup = (await _export.ExportBackupAsync()).Replace("\"version\": 1", "\"version\": 99");

        RestoreResult result = await _sut.RestoreBackupAsync(backup);

        result.Errors.ShouldNotBeEmpty();
        result.MatchesInserted.ShouldBe(0, "a partial restore is worse than none — the user believes their data is back");
    }

    [Test]
    public async Task RestoreBackupAsync_Garbage_ReportsAnErrorInsteadOfThrowing()
    {
        RestoreResult result = await _sut.RestoreBackupAsync("{ not json at all");

        result.Errors.ShouldNotBeEmpty();
        result.MatchesInserted.ShouldBe(0);
    }
}
