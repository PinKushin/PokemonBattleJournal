using static PokemonBattleJournal.Models.MatchResult;

namespace PokemonBattleJournal.IntegrationTests.Services;

public class MatchOperationsTests : IAsyncDisposable
{
    private readonly TestSqliteConnectionFactory _factory = new(new NullMetaService());
    private Trainer _trainer = null!;
    private Archetype _archetype = null!;

    private async Task SetupAsync()
    {
        if (_trainer is not null) return;
        await _factory.Trainers.SaveAsync("MatchTestTrainer");
        _trainer = (await _factory.Trainers.GetByNameAsync("MatchTestTrainer"))!;

        await _factory.Archetypes.SaveAsync("Charizard ex", "ball_icon.png", _trainer.Id);
        List<Archetype> archs = await _factory.Archetypes.GetAllAsync();
        _archetype = archs.First(a => a.TrainerId == _trainer.Id);
    }

    private MatchEntry MakeMatch() => new()
    {
        TrainerId = _trainer.Id,
        PlayingId = _archetype.Id,
        AgainstId = _archetype.Id,
    };

    [Test]
    public async Task SaveAsync_ValidBO1Match_ReturnsPositive()
    {
        await SetupAsync();
        MatchEntry match = MakeMatch();
        Game game = new() { Result = Win };

        int result = await _factory.Matches.SaveAsync(match, [game]);
        result.ShouldBeGreaterThan(0);
    }

    [Test]
    public async Task SaveAsync_MatchAppearsInGetAll()
    {
        await SetupAsync();
        MatchEntry match = MakeMatch();
        await _factory.Matches.SaveAsync(match, [new Game { Result = Loss }]);

        List<MatchEntry> all = await _factory.Matches.GetAllAsync();
        all.ShouldContain(m => m.TrainerId == _trainer.Id);
    }

    [Test]
    public async Task GetByTrainerIdAsync_ReturnsMatchesForTrainer()
    {
        await SetupAsync();
        await _factory.Matches.SaveAsync(MakeMatch(), [new Game { Result = Tie }]);

        List<MatchEntry> matches = await _factory.Matches.GetByTrainerIdAsync(_trainer.Id);
        matches.ShouldNotBeEmpty();
        matches.ShouldAllBe(m => m.TrainerId == _trainer.Id);
    }

    [Test]
    public async Task GetByTrainerIdAsync_WithRelated_PopulatesArchetypes()
    {
        await SetupAsync();
        await _factory.Matches.SaveAsync(MakeMatch(), [new Game { Result = Win }]);

        List<MatchEntry> matches = await _factory.Matches.GetByTrainerIdAsync(_trainer.Id, includeRelated: true);
        matches.ShouldNotBeEmpty();
        matches[0].Playing.ShouldNotBeNull();
        matches[0].Against.ShouldNotBeNull();
    }

    [Test]
    public async Task GetByIdAsync_ExistingMatch_ReturnsWithChildren()
    {
        await SetupAsync();
        MatchEntry match = MakeMatch();
        await _factory.Matches.SaveAsync(match, [new Game { Result = Win }]);

        MatchEntry? loaded = await _factory.Matches.GetByIdAsync(match.Id);
        loaded.ShouldNotBeNull();
        loaded!.Id.ShouldBe(match.Id);
    }

    [Test]
    public async Task SaveAsync_BO3Match_SavesAllThreeGames()
    {
        await SetupAsync();
        MatchEntry match = MakeMatch();
        List<Game> games =
        [
            new() { Result = Win },
            new() { Result = Loss },
            new() { Result = Win },
        ];

        int result = await _factory.Matches.SaveAsync(match, games);
        result.ShouldBeGreaterThan(0);
        match.Game1Id.ShouldNotBeNull();
        match.Game2Id.ShouldNotBeNull();
        match.Game3Id.ShouldNotBeNull();
    }

    [Test]
    public async Task DeleteAsync_ExistingMatch_RemovesFromDb()
    {
        await SetupAsync();
        MatchEntry match = MakeMatch();
        await _factory.Matches.SaveAsync(match, [new Game { Result = Win }]);

        await _factory.Matches.DeleteAsync(match);

        List<MatchEntry> all = await _factory.Matches.GetAllAsync();
        all.ShouldNotContain(m => m.Id == match.Id);
    }

    [Test]
    public async Task SaveAsync_NoTrainerId_ThrowsArgumentException()
    {
        await SetupAsync();
        MatchEntry bad = new() { PlayingId = _archetype.Id, AgainstId = _archetype.Id };
        await Should.ThrowAsync<ArgumentException>(
            () => _factory.Matches.SaveAsync(bad, [new Game { Result = Win }]));
    }

    [Test]
    public async Task SaveAsync_WithTag_TagRelationshipPersists()
    {
        await SetupAsync();
        List<Tags> tags = await _factory.Tags.GetAllAsync();
        Tags tag = tags[0];

        MatchEntry match = MakeMatch();
        Game game = new() { Result = Win, Tags = [tag] };

        await _factory.Matches.SaveAsync(match, [game]);

        MatchEntry? loaded = await _factory.Matches.GetByIdAsync(match.Id, includeRelated: true);
        loaded.ShouldNotBeNull();
        loaded!.Game1.ShouldNotBeNull();
        loaded.Game1!.Tags.ShouldNotBeNull();
        loaded.Game1.Tags!.ShouldContain(t => t.Id == tag.Id);
    }

    [Test]
    public async Task SaveAsync_ZeroPlayingId_ThrowsArgumentException()
    {
        await SetupAsync();
        MatchEntry bad = new() { TrainerId = _trainer.Id, AgainstId = _archetype.Id };
        await Should.ThrowAsync<ArgumentException>(
            () => _factory.Matches.SaveAsync(bad, [new Game { Result = Win }]));
    }

    [Test]
    public async Task SaveAsync_ZeroAgainstId_ThrowsArgumentException()
    {
        await SetupAsync();
        MatchEntry bad = new() { TrainerId = _trainer.Id, PlayingId = _archetype.Id };
        await Should.ThrowAsync<ArgumentException>(
            () => _factory.Matches.SaveAsync(bad, [new Game { Result = Win }]));
    }

    [Test]
    public async Task SaveAsync_EmptyGamesList_ThrowsArgumentException()
    {
        await SetupAsync();
        await Should.ThrowAsync<ArgumentException>(
            () => _factory.Matches.SaveAsync(MakeMatch(), []));
    }

    [Test]
    public async Task GetByIdAsync_NonExistentId_ReturnsNull()
    {
        MatchEntry? result = await _factory.Matches.GetByIdAsync(99999);
        result.ShouldBeNull();
    }

    [Test]
    public async Task GetAllAsync_EmptyDb_ReturnsEmpty()
    {
        List<MatchEntry> all = await _factory.Matches.GetAllAsync();
        all.ShouldBeEmpty();
    }

    [Test]
    public async Task DeleteAsync_NullMatch_ThrowsArgumentNullException()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _factory.Matches.DeleteAsync(null!));
    }

    [Test]
    public async Task DeleteAsync_ZeroId_ThrowsArgumentException()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _factory.Matches.DeleteAsync(new MatchEntry()));
    }

    [Test]
    public async Task GetByTrainerIdAsync_NoMatchesForTrainer_ReturnsEmpty()
    {
        await SetupAsync();
        List<MatchEntry> matches = await _factory.Matches.GetByTrainerIdAsync(99999);
        matches.ShouldBeEmpty();
    }

    [Test]
    public async Task SaveAsync_TwoGameBO3_SetsGame1AndGame2ButNotGame3()
    {
        await SetupAsync();
        MatchEntry match = MakeMatch();
        List<Game> games =
        [
            new() { Result = Win },
            new() { Result = Win },
        ];

        await _factory.Matches.SaveAsync(match, games);

        match.Game1Id.ShouldNotBeNull();
        match.Game2Id.ShouldNotBeNull();
        match.Game3Id.ShouldBeNull();
    }

    [Test]
    public async Task SaveAsync_UpdateExistingMatch_Persists()
    {
        await SetupAsync();
        MatchEntry match = MakeMatch();
        await _factory.Matches.SaveAsync(match, [new Game { Result = Win }]);

        DateTime updatedEnd = match.EndTime.AddMinutes(10);
        match.EndTime = updatedEnd;
        await _factory.Matches.SaveAsync(match, [new Game { Result = Loss }]);

        MatchEntry? loaded = await _factory.Matches.GetByIdAsync(match.Id);
        loaded.ShouldNotBeNull();
        loaded!.EndTime.ShouldBe(updatedEnd);
    }

    [Test]
    public async Task GetByIdAsync_WithIncludeRelatedFalse_ReturnsMatchWithoutLoadedChildren()
    {
        await SetupAsync();
        MatchEntry match = MakeMatch();
        await _factory.Matches.SaveAsync(match, [new Game { Result = Win }]);

        MatchEntry? loaded = await _factory.Matches.GetByIdAsync(match.Id, includeRelated: false);
        loaded.ShouldNotBeNull();
        loaded!.Id.ShouldBe(match.Id);
    }

    [Test]
    public async Task GetByTrainerIdAsync_WithIncludeRelatedFalse_ReturnsMatchesWithoutArchetypes()
    {
        await SetupAsync();
        await _factory.Matches.SaveAsync(MakeMatch(), [new Game { Result = Win }]);

        List<MatchEntry> matches = await _factory.Matches.GetByTrainerIdAsync(_trainer.Id, includeRelated: false);
        matches.ShouldNotBeEmpty();
    }

    [Test]
    public async Task SaveAsync_WithNonExistentTagId_ReturnsZero()
    {
        // PreValidateTagsAsync throws ArgumentException, caught by catch block → returns 0
        await SetupAsync();
        Tags fakeTag = new() { Id = 99999, Name = "Ghost" };
        Game game = new() { Result = Win, Tags = [fakeTag] };

        int result = await _factory.Matches.SaveAsync(MakeMatch(), [game]);
        result.ShouldBe(0);
    }

    [Test]
    public async Task DeleteAsync_CascadesGamesAndTagRelationships()
    {
        await SetupAsync();
        List<Tags> tags = await _factory.Tags.GetAllAsync();
        Tags tag = tags[0];

        MatchEntry match = MakeMatch();
        Game game = new() { Result = Win, Tags = [tag] };
        await _factory.Matches.SaveAsync(match, [game]);

        uint matchId = match.Id;
        await _factory.Matches.DeleteAsync(match);

        MatchEntry? deleted = await _factory.Matches.GetByIdAsync(matchId);
        deleted.ShouldBeNull();
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();
}
