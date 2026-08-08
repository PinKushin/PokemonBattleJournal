using PokemonBattleJournal.IntegrationTests.Infrastructure;

namespace PokemonBattleJournal.IntegrationTests.Services;

/// <summary>
/// Deleting a trainer, when that trainer actually has data.
/// </summary>
/// <remarks>
/// <c>DeleteAsync</c> was the largest uncovered block in Core — 85 NoCoverage mutants in
/// TrainerOperations, most of them here — despite an existing
/// <c>DeleteAsync_AfterSave_RemovesTrainer</c> test that passes. That test deletes a trainer
/// with NO matches, archetypes or tags, so every foreach body is skipped and
/// <c>if (match.Game1Id.HasValue)</c> is never evaluated. Seed data that never reaches the
/// branch — shape #1 in feedback_tests_that_cannot_fail — which reads as covered and proves
/// nothing about the cascade.
///
/// The test that matters most here is the isolation one. Every query in the cascade is
/// filtered by TrainerId, and dropping any of those filters deletes EVERY trainer's data while
/// leaving a single-trainer test perfectly green.
/// </remarks>
public class TrainerDeletionCascadeTests
{
    private TestSqliteConnectionFactory _factory = null!;
    private uint _victimId;
    private uint _bystanderId;
    private uint _archetypeId;

    [SetUp]
    public async Task SetUp()
    {
        ILimitlessMetaService meta = Substitute.For<ILimitlessMetaService>();
        meta.GetTopDecksAsync(Arg.Any<int>()).Returns([]);
        _factory = new TestSqliteConnectionFactory(meta);

        SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();

        Trainer victim = new() { Name = "Victim", IsActive = true };
        _ = await db.InsertAsync(victim);
        _victimId = victim.Id;

        Trainer bystander = new() { Name = "Bystander" };
        _ = await db.InsertAsync(bystander);
        _bystanderId = bystander.Id;

        Archetype shared = new() { Name = "Other", ImagePath = "substitute.png" };
        _ = await db.InsertAsync(shared);
        _archetypeId = shared.Id;
    }

    [TearDown]
    public async Task TearDown() => await _factory.DisposeAsync();

    /// <summary>Gives a trainer one match, one archetype and one tag, with the tag linked.</summary>
    private async Task SeedDataForAsync(uint trainerId, string suffix, bool bo3 = false)
    {
        SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();

        Archetype owned = new() { Name = $"Deck {suffix}", ImagePath = "x.png", TrainerId = trainerId };
        _ = await db.InsertAsync(owned);

        Tags tag = new() { Name = $"tag {suffix}", TrainerId = trainerId };
        _ = await db.InsertAsync(tag);

        List<Game> games = [new Game { Result = MatchResult.Win, Turn = 1, Tags = [tag] }];
        if (bo3)
        {
            games.Add(new Game { Result = MatchResult.Loss, Turn = 2, Tags = [tag] });
            games.Add(new Game { Result = MatchResult.Win, Turn = 1, Tags = [tag] });
        }

        MatchEntry match = new()
        {
            TrainerId = trainerId,
            PlayingId = _archetypeId,
            AgainstId = _archetypeId,
            Result = MatchResult.Win,
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddMinutes(20),
            DatePlayed = DateTime.UtcNow.Date,
        };
        (await _factory.Matches.SaveAsync(match, games))
            .ShouldBeGreaterThan(0, "seeding must succeed or the test proves nothing");
    }

    private async Task<int> CountAsync(string sql, params object[] args) =>
        await (await _factory.GetDatabaseAsync()).ExecuteScalarAsync<int>(sql, args);

    [Test]
    public async Task DeleteAsync_RemovesTheTrainersMatchesArchetypesAndTags()
    {
        await SeedDataForAsync(_victimId, "v");
        Trainer victim = (await _factory.Trainers.GetByIdAsync(_victimId))!;

        _ = await _factory.Trainers.DeleteAsync(victim);

        (await CountAsync("SELECT COUNT(*) FROM Trainer WHERE Id = ?", _victimId)).ShouldBe(0);
        (await CountAsync("SELECT COUNT(*) FROM MatchEntry WHERE TrainerId = ?", _victimId)).ShouldBe(0);
        (await CountAsync("SELECT COUNT(*) FROM Archetype WHERE TrainerId = ?", _victimId)).ShouldBe(0);
        (await CountAsync("SELECT COUNT(*) FROM Tags WHERE TrainerId = ?", _victimId)).ShouldBe(0);
    }

    [Test]
    public async Task DeleteAsync_RemovesTheGamesAndTagLinksBehindThoseMatches()
    {
        // The cascade the old test could not reach: games and TagGame rows are deleted by raw
        // SQL keyed on Game1Id/Game2Id/Game3Id, and with no matches seeded that loop never ran.
        await SeedDataForAsync(_victimId, "v");
        int gamesBefore = await CountAsync("SELECT COUNT(*) FROM Game");
        gamesBefore.ShouldBeGreaterThan(0, "the fixture must actually create a game");

        Trainer victim = (await _factory.Trainers.GetByIdAsync(_victimId))!;
        _ = await _factory.Trainers.DeleteAsync(victim);

        (await CountAsync("SELECT COUNT(*) FROM Game")).ShouldBe(0, "orphaned games would accumulate forever");
        (await CountAsync("SELECT COUNT(*) FROM TagGame")).ShouldBe(0, "orphaned tag links would too");
    }

    [Test]
    public async Task DeleteAsync_ABo3Match_RemovesAllThreeGames()
    {
        // Game2Id and Game3Id have their own branches, and a BO1-only fixture leaves both
        // permanently false.
        await SeedDataForAsync(_victimId, "v", bo3: true);
        (await CountAsync("SELECT COUNT(*) FROM Game")).ShouldBe(3);

        Trainer victim = (await _factory.Trainers.GetByIdAsync(_victimId))!;
        _ = await _factory.Trainers.DeleteAsync(victim);

        (await CountAsync("SELECT COUNT(*) FROM Game")).ShouldBe(0);
    }

    [Test]
    public async Task DeleteAsync_LeavesEveryOtherTrainersDataAlone()
    {
        // The one that matters. Every query in the cascade is filtered by TrainerId; drop any
        // one of those filters and this deletes the whole database while a single-trainer test
        // stays green.
        await SeedDataForAsync(_victimId, "v");
        await SeedDataForAsync(_bystanderId, "b");

        Trainer victim = (await _factory.Trainers.GetByIdAsync(_victimId))!;
        _ = await _factory.Trainers.DeleteAsync(victim);

        (await CountAsync("SELECT COUNT(*) FROM Trainer WHERE Id = ?", _bystanderId)).ShouldBe(1);
        (await CountAsync("SELECT COUNT(*) FROM MatchEntry WHERE TrainerId = ?", _bystanderId)).ShouldBe(1);
        (await CountAsync("SELECT COUNT(*) FROM Archetype WHERE TrainerId = ?", _bystanderId)).ShouldBe(1);
        (await CountAsync("SELECT COUNT(*) FROM Tags WHERE TrainerId = ?", _bystanderId)).ShouldBe(1);
        (await CountAsync("SELECT COUNT(*) FROM Game")).ShouldBe(1, "the bystander's game must survive");
        (await CountAsync("SELECT COUNT(*) FROM TagGame")).ShouldBe(1, "and so must its tag link");
    }

    [Test]
    public async Task DeleteAsync_ATrainerThatIsNotThere_ReportsNothingDeleted()
    {
        // Reaches the guard that throws ArgumentException and the catch that turns it into a 0.
        Trainer ghost = new() { Id = uint.MaxValue, Name = "Ghost" };

        int affected = await _factory.Trainers.DeleteAsync(ghost);

        affected.ShouldBe(0);
        (await CountAsync("SELECT COUNT(*) FROM Trainer")).ShouldBe(2, "nothing else may be touched");
    }

    [Test]
    public async Task SaveAsync_ANameThatAlreadyExists_IsRefused()
    {
        // The duplicate guard inside the transaction. Two trainers of the same name would split
        // a history in two with no way to tell them apart in the picker.
        int affected = await _factory.Trainers.SaveAsync("Victim");

        affected.ShouldBe(0);
        (await CountAsync("SELECT COUNT(*) FROM Trainer WHERE Name = ?", "Victim")).ShouldBe(1);
    }
}
