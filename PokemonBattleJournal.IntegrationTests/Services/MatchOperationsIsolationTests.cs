using PokemonBattleJournal.IntegrationTests.Infrastructure;

namespace PokemonBattleJournal.IntegrationTests.Services;

/// <summary>
/// Reads that must return one trainer's matches and nobody else's.
/// </summary>
/// <remarks>
/// Every existing MatchOperations test uses a single trainer, so the filters in
/// <c>GetByTrainerIdAsync</c> have never been asked to EXCLUDE anything. From one trainer's
/// point of view, returning every match in the database is indistinguishable from returning
/// theirs — the same blind spot that let the trainer deletion cascade go unproven.
///
/// This is the read-side counterpart. A dropped filter here does not destroy data, it shows
/// one person another person's match history, which on a shared device is worse than a crash
/// because nothing looks wrong.
/// </remarks>
public class MatchOperationsIsolationTests
{
    private TestSqliteConnectionFactory _factory = null!;
    private uint _mineId;
    private uint _theirsId;
    private uint _archetypeId;

    [SetUp]
    public async Task SetUp()
    {
        ILimitlessMetaService meta = Substitute.For<ILimitlessMetaService>();
        meta.GetTopDecksAsync(Arg.Any<int>()).Returns([]);
        _factory = new TestSqliteConnectionFactory(meta);

        SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();

        Trainer mine = new() { Name = "Mine", IsActive = true };
        _ = await db.InsertAsync(mine);
        _mineId = mine.Id;

        Trainer theirs = new() { Name = "Theirs" };
        _ = await db.InsertAsync(theirs);
        _theirsId = theirs.Id;

        Archetype archetype = new() { Name = "Other", ImagePath = "substitute.png" };
        _ = await db.InsertAsync(archetype);
        _archetypeId = archetype.Id;
    }

    [TearDown]
    public async Task TearDown() => await _factory.DisposeAsync();

    private async Task SaveMatchForAsync(uint trainerId, string note, params Tags[] tags)
    {
        MatchEntry match = new()
        {
            TrainerId = trainerId,
            PlayingId = _archetypeId,
            AgainstId = _archetypeId,
            Result = MatchResult.Win,
            StartTime = DateTime.UtcNow.AddMinutes(-trainerId),
            EndTime = DateTime.UtcNow,
            DatePlayed = DateTime.UtcNow.Date,
        };
        Game game = new() { Result = MatchResult.Win, Turn = 1, Notes = note, Tags = [.. tags] };
        (await _factory.Matches.SaveAsync(match, [game]))
            .ShouldBeGreaterThan(0, "seeding must succeed or the test proves nothing");
    }

    [Test]
    public async Task GetByTrainerIdAsync_ReturnsOnlyThatTrainersMatches()
    {
        await SaveMatchForAsync(_mineId, "mine");
        await SaveMatchForAsync(_theirsId, "theirs");

        List<MatchEntry> mine = await _factory.Matches.GetByTrainerIdAsync(_mineId);

        mine.Count.ShouldBe(1, "the other trainer's match must not appear");
        mine[0].TrainerId.ShouldBe(_mineId);
        mine[0].Game1!.Notes.ShouldBe("mine");
    }

    [Test]
    public async Task GetByTrainerIdAsync_ForATrainerWithNothing_ReturnsEmptyWhileOthersHaveMatches()
    {
        // Distinct from the existing empty-DATABASE test. There, everything is empty and a
        // missing filter looks identical to a working one. Here the database is not empty, so
        // returning anything at all is a failure.
        await SaveMatchForAsync(_theirsId, "theirs");

        (await _factory.Matches.GetByTrainerIdAsync(_mineId)).ShouldBeEmpty();
    }

    [Test]
    public async Task GetAllAsync_IsTheOneThatCrossesTrainers()
    {
        // Pins the distinction the two methods exist for. If GetByTrainerIdAsync lost its
        // filter it would simply become this, and no single-trainer test could tell.
        await SaveMatchForAsync(_mineId, "mine");
        await SaveMatchForAsync(_theirsId, "theirs");

        (await _factory.Matches.GetAllAsync()).Count.ShouldBe(2);
    }

    [Test]
    public async Task SaveAsync_WithTags_WritesTheTagRelationshipsAndTheyLoadBack()
    {
        // Reaches the tag-relationship half of VerifyDataIntegrityAsync, which a match saved
        // without tags skips entirely.
        SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();
        Tags bricked = new() { Name = "bricked", TrainerId = _mineId };
        _ = await db.InsertAsync(bricked);

        await SaveMatchForAsync(_mineId, "mine", bricked);

        List<MatchEntry> mine = await _factory.Matches.GetByTrainerIdAsync(_mineId);
        mine.ShouldHaveSingleItem();
        mine[0].Game1!.Tags.ShouldNotBeNull().ShouldHaveSingleItem().Name.ShouldBe("bricked");

        (await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM TagGame")).ShouldBe(1);
    }

    [Test]
    public async Task GetByTrainerIdAsync_WithIncludeRelatedFalse_StillOnlyReturnsThatTrainer()
    {
        // The cheap path skips loading games; it must not also skip the filter.
        await SaveMatchForAsync(_mineId, "mine");
        await SaveMatchForAsync(_theirsId, "theirs");

        List<MatchEntry> mine = await _factory.Matches.GetByTrainerIdAsync(_mineId, includeRelated: false);

        mine.ShouldHaveSingleItem().TrainerId.ShouldBe(_mineId);
    }
}
