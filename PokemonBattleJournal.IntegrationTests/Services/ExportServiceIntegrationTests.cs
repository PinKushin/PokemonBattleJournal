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
