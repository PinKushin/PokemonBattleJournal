using PokemonBattleJournal.Models;
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

    [Fact]
    public async Task SaveAsync_ValidBO1Match_ReturnsPositive()
    {
        await SetupAsync();
        MatchEntry match = MakeMatch();
        Game game = new() { Result = Win };

        int result = await _factory.Matches.SaveAsync(match, [game]);
        result.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task SaveAsync_MatchAppearsInGetAll()
    {
        await SetupAsync();
        MatchEntry match = MakeMatch();
        await _factory.Matches.SaveAsync(match, [new Game { Result = Loss }]);

        List<MatchEntry> all = await _factory.Matches.GetAllAsync();
        all.ShouldContain(m => m.TrainerId == _trainer.Id);
    }

    [Fact]
    public async Task GetByTrainerIdAsync_ReturnsMatchesForTrainer()
    {
        await SetupAsync();
        await _factory.Matches.SaveAsync(MakeMatch(), [new Game { Result = Tie }]);

        List<MatchEntry> matches = await _factory.Matches.GetByTrainerIdAsync(_trainer.Id);
        matches.ShouldNotBeEmpty();
        matches.ShouldAllBe(m => m.TrainerId == _trainer.Id);
    }

    [Fact]
    public async Task GetByTrainerIdAsync_WithRelated_PopulatesArchetypes()
    {
        await SetupAsync();
        await _factory.Matches.SaveAsync(MakeMatch(), [new Game { Result = Win }]);

        List<MatchEntry> matches = await _factory.Matches.GetByTrainerIdAsync(_trainer.Id, includeRelated: true);
        matches.ShouldNotBeEmpty();
        matches[0].Playing.ShouldNotBeNull();
        matches[0].Against.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ExistingMatch_ReturnsWithChildren()
    {
        await SetupAsync();
        MatchEntry match = MakeMatch();
        await _factory.Matches.SaveAsync(match, [new Game { Result = Win }]);

        MatchEntry? loaded = await _factory.Matches.GetByIdAsync(match.Id);
        loaded.ShouldNotBeNull();
        loaded!.Id.ShouldBe(match.Id);
    }

    [Fact]
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

    [Fact]
    public async Task DeleteAsync_ExistingMatch_RemovesFromDb()
    {
        await SetupAsync();
        MatchEntry match = MakeMatch();
        await _factory.Matches.SaveAsync(match, [new Game { Result = Win }]);

        await _factory.Matches.DeleteAsync(match);

        List<MatchEntry> all = await _factory.Matches.GetAllAsync();
        all.ShouldNotContain(m => m.Id == match.Id);
    }

    [Fact]
    public async Task SaveAsync_NoTrainerId_ThrowsArgumentException()
    {
        await SetupAsync();
        MatchEntry bad = new() { PlayingId = _archetype.Id, AgainstId = _archetype.Id };
        await Should.ThrowAsync<ArgumentException>(
            () => _factory.Matches.SaveAsync(bad, [new Game { Result = Win }]));
    }

    [Fact]
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

    public ValueTask DisposeAsync() => _factory.DisposeAsync();
}
