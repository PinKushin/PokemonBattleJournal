using System.Globalization;
using System.Text;
using System.Text.Json;
using PokemonBattleJournal.IntegrationTests.Infrastructure;
using PokemonBattleJournal.Services.Export;
using PokemonBattleJournal.Services.Import;

namespace PokemonBattleJournal.IntegrationTests.Services;

/// <summary>
/// Export against a real SQLite database, including a full export → import round trip.
/// </summary>
/// <remarks>
/// The unit tests mock the operations layer, so they can only prove the serializer emits what
/// it was handed. These prove the parts that only real persistence exercises: that related
/// data (archetypes, games, tags) is actually loaded rather than left null by a missing
/// includeRelated, and that re-importing an export reconstructs the same matches — including
/// creating the archetype and tag rows it needs.
/// </remarks>
public class ExportServiceIntegrationTests
{
    private TestSqliteConnectionFactory _factory = null!;
    private ExportService _sut = null!;
    private uint _trainerId;
    private uint _dragapultId;
    private uint _otherId;

    [SetUp]
    public async Task SetUp()
    {
        _factory = new TestSqliteConnectionFactory(Substitute.For<ILimitlessMetaService>());
        _sut = new ExportService(_factory, NullLogger<ExportService>.Instance);

        SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();

        Trainer trainer = new() { Name = "Ash", IsActive = true };
        _ = await db.InsertAsync(trainer);
        _trainerId = trainer.Id;

        Archetype dragapult = new() { Name = "Dragapult ex / Dusknoir", ImagePath = "dragapult.png" };
        _ = await db.InsertAsync(dragapult);
        _dragapultId = dragapult.Id;

        Archetype other = new() { Name = "Other", ImagePath = "substitute.png" };
        _ = await db.InsertAsync(other);
        _otherId = other.Id;
    }

    [TearDown]
    public async Task TearDown() => await _factory.DisposeAsync();

    private async Task SaveMatchAsync(MatchResult result, params Game[] games)
    {
        MatchEntry match = new()
        {
            TrainerId = _trainerId,
            PlayingId = _dragapultId,
            AgainstId = _otherId,
            Result = result,
            DatePlayed = new DateTime(2026, 7, 27, 19, 45, 24, DateTimeKind.Utc),
            StartTime = new DateTime(2026, 7, 27, 19, 45, 24, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 7, 27, 20, 5, 0, DateTimeKind.Utc),
        };
        int affected = await _factory.Matches.SaveAsync(match, [.. games]);
        affected.ShouldBeGreaterThan(0, "seeding a match must succeed or the test proves nothing");
    }

    [Test]
    public async Task ExportTrainerHillAsync_RealMatch_IncludesRelatedArchetypesAndTags()
    {
        List<Tags> tags = await _factory.Tags.GetAllAsync();
        Tags tag = tags[0];
        await SaveMatchAsync(MatchResult.Win,
            new Game { Result = MatchResult.Win, Turn = 1, Notes = "clean start", Tags = [tag] });

        using JsonDocument doc = JsonDocument.Parse(await _sut.ExportTrainerHillAsync(_trainerId));

        JsonElement entry = doc.RootElement[0];
        // Would be empty if GetByTrainerIdAsync had not loaded the related archetype rows.
        entry.GetProperty("playing").GetString().ShouldBe("dragapult-ex-dusknoir");
        entry.GetProperty("game1").GetProperty("notes").GetString().ShouldBe("clean start");
        entry.GetProperty("game1").GetProperty("tags")[0].GetString().ShouldBe(tag.Name);
    }

    [Test]
    public async Task ExportTrainerHillAsync_BO3Match_WritesThreeGames()
    {
        await SaveMatchAsync(MatchResult.Win,
            new Game { Result = MatchResult.Win, Turn = 1 },
            new Game { Result = MatchResult.Loss, Turn = 2 },
            new Game { Result = MatchResult.Win, Turn = 1 });

        using JsonDocument doc = JsonDocument.Parse(await _sut.ExportTrainerHillAsync(_trainerId));

        JsonElement entry = doc.RootElement[0];
        entry.GetProperty("game2").GetProperty("result").GetString().ShouldBe("Loss");
        entry.GetProperty("game3").GetProperty("result").GetString().ShouldBe("Win");
    }

    /// <summary>
    /// A backup must carry the match timings, because nothing else can reconstruct them.
    /// </summary>
    /// <remarks>
    /// <c>MatchEntry</c> stores <c>StartTime</c>, <c>EndTime</c> and <c>DatePlayed</c>
    /// separately, and two statistics are computed from the first two —
    /// <c>CalculateAverageMatchDuration</c> and <c>CalculateWinRateByMatchLength</c>. The
    /// backup wrote only <c>DatePlayed</c>, so restoring one would silently produce
    /// zero-length matches and corrupt both, with no error anywhere to explain it.
    ///
    /// This is a backup, not an interchange format: losing data it could have kept is the one
    /// thing it must not do.
    /// </remarks>
    [Test]
    public async Task ExportBackupAsync_RealMatch_PreservesStartAndEndTime()
    {
        await SaveMatchAsync(MatchResult.Win, new Game { Result = MatchResult.Win, Turn = 1 });

        using JsonDocument doc = JsonDocument.Parse(await _sut.ExportBackupAsync());

        JsonElement match = doc.RootElement.GetProperty("trainers")
            .EnumerateArray().Single(t => t.GetProperty("name").GetString() == "Ash")
            .GetProperty("matches")[0];

        match.TryGetProperty("startTime", out JsonElement start)
            .ShouldBeTrue("a backup that drops startTime cannot restore match duration");
        match.TryGetProperty("endTime", out JsonElement end)
            .ShouldBeTrue("a backup that drops endTime cannot restore match duration");

        DateTime.Parse(start.GetString()!, CultureInfo.InvariantCulture)
            .ShouldBe(new DateTime(2026, 7, 27, 19, 45, 24, DateTimeKind.Utc));
        DateTime.Parse(end.GetString()!, CultureInfo.InvariantCulture)
            .ShouldBe(new DateTime(2026, 7, 27, 20, 5, 0, DateTimeKind.Utc));
    }

    /// <summary>
    /// The TrainerHill export must stay exactly what TrainerHill emits.
    /// </summary>
    /// <remarks>
    /// The two formats share <c>ExportEntry</c>, so the timing fields added for backups would
    /// otherwise leak into the interchange format. That file is meant to be handed to
    /// TrainerHill, and its value is being indistinguishable from one of theirs.
    /// </remarks>
    /// <summary>
    /// <c>time</c> must carry <c>StartTime</c>, not <c>DatePlayed</c>.
    /// </summary>
    /// <remarks>
    /// TrainerHill's schema has a single time field, so this export cannot be lossless — but it
    /// can pick the better of the two. <c>DatePlayed</c> is the weak one: it comes from a date
    /// picker and sits at midnight, so exporting it throws away the time of day for no reason.
    /// <c>StartTime</c> carries the same date plus real precision.
    ///
    /// It also matters for duplicate detection, which keys on <c>StartTime</c> — a re-imported
    /// file whose time field was midnight would never match the row it came from.
    /// </remarks>
    [Test]
    public async Task ExportTrainerHillAsync_RealMatch_WritesStartTimeNotDatePlayed()
    {
        MatchEntry match = new()
        {
            TrainerId = _trainerId,
            PlayingId = _dragapultId,
            AgainstId = _otherId,
            Result = MatchResult.Win,
            // What a date picker leaves behind: midnight, no time of day.
            DatePlayed = new DateTime(2026, 7, 27, 0, 0, 0, DateTimeKind.Utc),
            StartTime = new DateTime(2026, 7, 27, 19, 45, 24, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 7, 27, 20, 5, 0, DateTimeKind.Utc),
        };
        (await _factory.Matches.SaveAsync(match, [new Game { Result = MatchResult.Win, Turn = 1 }]))
            .ShouldBeGreaterThan(0, "seeding a match must succeed or the test proves nothing");

        using JsonDocument doc = JsonDocument.Parse(await _sut.ExportTrainerHillAsync(_trainerId));

        DateTime written = DateTime.Parse(doc.RootElement[0].GetProperty("time").GetString()!, CultureInfo.InvariantCulture);
        written.TimeOfDay.ShouldNotBe(TimeSpan.Zero, "exporting DatePlayed throws away the time of day");
        written.ShouldBe(new DateTime(2026, 7, 27, 19, 45, 24, DateTimeKind.Utc));
    }

    [Test]
    public async Task ExportTrainerHillAsync_RealMatch_OmitsBackupOnlyTimings()
    {
        await SaveMatchAsync(MatchResult.Win, new Game { Result = MatchResult.Win, Turn = 1 });

        using JsonDocument doc = JsonDocument.Parse(await _sut.ExportTrainerHillAsync(_trainerId));

        JsonElement entry = doc.RootElement[0];
        entry.TryGetProperty("startTime", out _).ShouldBeFalse("TrainerHill's format has no startTime");
        entry.TryGetProperty("endTime", out _).ShouldBeFalse("TrainerHill's format has no endTime");
        entry.TryGetProperty("time", out _).ShouldBeTrue("TrainerHill's format keys the match on time");
    }

    [Test]
    public async Task ExportBackupAsync_RealDatabase_WritesArchetypeNameVerbatim()
    {
        await SaveMatchAsync(MatchResult.Loss, new Game { Result = MatchResult.Loss, Turn = 2 });

        using JsonDocument doc = JsonDocument.Parse(await _sut.ExportBackupAsync());

        JsonElement trainer = doc.RootElement.GetProperty("trainers")
            .EnumerateArray().Single(t => t.GetProperty("name").GetString() == "Ash");
        trainer.GetProperty("matches")[0].GetProperty("playing").GetString()
            .ShouldBe("Dragapult ex / Dusknoir", "a backup must not depend on slug resolution");
    }

    [Test]
    public async Task ExportThenImport_ReconstructsTheMatchForAnotherTrainer()
    {
        // The end-to-end guarantee: a file this app writes is a file this app can read back.
        List<Tags> tags = await _factory.Tags.GetAllAsync();
        await SaveMatchAsync(MatchResult.Win,
            new Game { Result = MatchResult.Win, Turn = 1, Notes = "round trip", Tags = [tags[0]] },
            new Game { Result = MatchResult.Loss, Turn = 2 });

        string json = await _sut.ExportTrainerHillAsync(_trainerId);

        SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();
        Trainer restored = new() { Name = "Restored" };
        _ = await db.InsertAsync(restored);

        ILimitlessMetaService meta = Substitute.For<ILimitlessMetaService>();
        meta.GetTopDecksAsync(Arg.Any<int>()).Returns(Task.FromResult(new List<MetaDeck>
        {
            new("Dragapult ex / Dusknoir", ""),
            new("Other", ""),
        }));
        TrainerHillImportService importer = new(
            _factory, NullLogger<TrainerHillImportService>.Instance, meta);

        (int imported, List<string> errors) = await importer.ImportAsync(
            new MemoryStream(Encoding.UTF8.GetBytes(json)), restored.Id);

        errors.ShouldBeEmpty($"round trip reported errors: {string.Join(" | ", errors)}");
        imported.ShouldBe(1);

        List<MatchEntry> restoredMatches = await _factory.Matches.GetByTrainerIdAsync(restored.Id, includeRelated: true);
        restoredMatches.Count.ShouldBe(1);
        MatchEntry match = restoredMatches[0];
        match.Result.ShouldBe(MatchResult.Win);
        match.Playing!.Name.ShouldBe("Dragapult ex / Dusknoir", "the slug must resolve back to the original name");
        match.Game1!.Notes.ShouldBe("round trip");
        match.Game1.Turn.ShouldBe(1u);
        match.Game2!.Result.ShouldBe(MatchResult.Loss);
        match.Game3.ShouldBeNull("the source match had two games, so the restore must not invent a third");
    }

    [Test]
    public async Task ExportTrainerHillAsync_TrainerWithNoMatches_WritesEmptyArray()
    {
        (await _sut.ExportTrainerHillAsync(_trainerId)).ShouldBe("[]");
    }
}
