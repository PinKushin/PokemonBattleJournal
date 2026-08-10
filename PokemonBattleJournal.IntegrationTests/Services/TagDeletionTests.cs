using PokemonBattleJournal.IntegrationTests.Infrastructure;

namespace PokemonBattleJournal.IntegrationTests.Services;

/// <summary>
/// Deleting a tag that games are still using, and the guards on either side of it.
/// </summary>
/// <remarks>
/// TagOperations carried 23 survivors and 20 uncovered mutants. The survivors are the more
/// interesting half: the code runs and nothing looks at the result. In particular
/// <c>affected += relationshipsDeleted</c> survived being mutated to a subtraction, which means
/// no test had ever asked what the returned number counts.
///
/// A tag is not owned by one game — TagGame is many-to-many — so deleting one has to clean up
/// links from every game that used it while leaving those games alone. That is the part worth
/// proving.
/// </remarks>
public class TagDeletionTests
{
    private TestSqliteConnectionFactory _factory = null!;
    private uint _trainerId;
    private uint _archetypeId;

    /// <summary>
    /// Distinguishes the StartTime of each seeded match.
    /// </summary>
    /// <remarks>
    /// A counter, not <c>Random.Shared</c>. Two reasons, and the analyser warning (CA5394) is the
    /// less important one. Random makes the test's input vary run to run, so a failure is not
    /// reproducible from the test alone; and it can COLLIDE, which matters specifically here
    /// because <c>MatchDuplicateKey</c> keys on StartTime — two seeded matches landing on the same
    /// second would be treated as duplicates and silently change what the test measures.
    /// A counter is deterministic and collision-free by construction.
    /// </remarks>
    private int _seededMatchCount;

    [SetUp]
    public async Task SetUp()
    {
        ILimitlessMetaService meta = Substitute.For<ILimitlessMetaService>();
        meta.GetTopDecksAsync(Arg.Any<int>()).Returns([]);
        _factory = new TestSqliteConnectionFactory(meta);

        SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();

        Trainer trainer = new() { Name = "Ash", IsActive = true };
        _ = await db.InsertAsync(trainer);
        _trainerId = trainer.Id;

        Archetype archetype = new() { Name = "Other", ImagePath = "substitute.png" };
        _ = await db.InsertAsync(archetype);
        _archetypeId = archetype.Id;
    }

    [TearDown]
    public async Task TearDown() => await _factory.DisposeAsync();

    private async Task<Tags> InsertTagAsync(string name)
    {
        SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();
        Tags tag = new() { Name = name, TrainerId = _trainerId };
        _ = await db.InsertAsync(tag);
        return tag;
    }

    /// <summary>Saves a match whose single game carries the given tags.</summary>
    private async Task SaveMatchWithAsync(params Tags[] tags)
    {
        MatchEntry match = new()
        {
            TrainerId = _trainerId,
            PlayingId = _archetypeId,
            AgainstId = _archetypeId,
            Result = MatchResult.Win,
            StartTime = DateTime.UtcNow.AddSeconds(-(++_seededMatchCount)),
            EndTime = DateTime.UtcNow,
            DatePlayed = DateTime.UtcNow.Date,
        };
        Game game = new() { Result = MatchResult.Win, Turn = 1, Tags = [.. tags] };
        (await _factory.Matches.SaveAsync(match, [game]))
            .ShouldBeGreaterThan(0, "seeding must succeed or the test proves nothing");
    }

    private async Task<int> CountAsync(string sql, params object[] args) =>
        await (await _factory.GetDatabaseAsync()).ExecuteScalarAsync<int>(sql, args);

    [Test]
    public async Task DeleteAsync_ATagUsedByGames_RemovesEveryLinkToIt()
    {
        Tags bricked = await InsertTagAsync("bricked");
        await SaveMatchWithAsync(bricked);
        await SaveMatchWithAsync(bricked);
        (await CountAsync("SELECT COUNT(*) FROM TagGame WHERE TagId = ?", bricked.Id)).ShouldBe(2);

        _ = await _factory.Tags.DeleteAsync(bricked);

        (await CountAsync("SELECT COUNT(*) FROM Tags WHERE Id = ?", bricked.Id)).ShouldBe(0);
        (await CountAsync("SELECT COUNT(*) FROM TagGame WHERE TagId = ?", bricked.Id)).ShouldBe(0);
    }

    [Test]
    public async Task DeleteAsync_CountsTheLinksItRemovedAsWellAsTheTag()
    {
        // The return value nobody had looked at. Mutating `affected += relationshipsDeleted` to a
        // subtraction survived every existing test, so the number could have meant anything.
        Tags bricked = await InsertTagAsync("bricked");
        await SaveMatchWithAsync(bricked);
        await SaveMatchWithAsync(bricked);

        int affected = await _factory.Tags.DeleteAsync(bricked);

        affected.ShouldBe(3, "two TagGame rows plus the tag itself");
    }

    [Test]
    public async Task DeleteAsync_AnUnusedTag_AffectsOnlyTheTagRow()
    {
        // The other side of `if (tagGameCount > 0)`. With no links the branch is skipped and the
        // count must be exactly one.
        Tags unused = await InsertTagAsync("never used");

        int affected = await _factory.Tags.DeleteAsync(unused);

        affected.ShouldBe(1);
    }

    [Test]
    public async Task DeleteAsync_LeavesTheGamesAndOtherTagsAlone()
    {
        // Deleting a tag must not take the match with it. TagGame is many-to-many, so a filter
        // dropped here would strip every game's tags rather than one tag's links.
        Tags bricked = await InsertTagAsync("bricked");
        Tags donked = await InsertTagAsync("donked");
        await SaveMatchWithAsync(bricked, donked);

        _ = await _factory.Tags.DeleteAsync(bricked);

        (await CountAsync("SELECT COUNT(*) FROM Game")).ShouldBe(1, "the game itself must survive");
        (await CountAsync("SELECT COUNT(*) FROM Tags WHERE Id = ?", donked.Id)).ShouldBe(1);
        (await CountAsync("SELECT COUNT(*) FROM TagGame WHERE TagId = ?", donked.Id))
            .ShouldBe(1, "the other tag's link must be untouched");
    }

    [Test]
    public async Task SaveAsync_AnEmptyOrWhitespaceName_IsRejected()
    {
        // Throws rather than returning 0, unlike the restore guards. Pinned because the
        // difference is real: a caller that ignores the return value still gets stopped here.
        _ = await Should.ThrowAsync<ArgumentException>(async () => await _factory.Tags.SaveAsync("", _trainerId));
        _ = await Should.ThrowAsync<ArgumentException>(async () => await _factory.Tags.SaveAsync("   ", _trainerId));
    }

    [Test]
    public async Task SaveAsync_WithoutATrainer_IsRejected() =>
        _ = await Should.ThrowAsync<ArgumentException>(async () => await _factory.Tags.SaveAsync("orphan", 0));

    [Test]
    public async Task DeleteAsync_ATagWithNoId_IsRejected() =>
        // Id 0 means the tag was never persisted; deleting by it would match nothing and report
        // success.
        _ = await Should.ThrowAsync<ArgumentException>(
            async () => await _factory.Tags.DeleteAsync(new Tags { Name = "ghost", TrainerId = _trainerId }));
}
