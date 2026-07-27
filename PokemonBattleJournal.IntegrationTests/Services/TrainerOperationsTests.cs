using PokemonBattleJournal.Models;
using static PokemonBattleJournal.Models.MatchResult;

namespace PokemonBattleJournal.IntegrationTests.Services;

public class TrainerOperationsTests : IAsyncDisposable
{
    private readonly TestSqliteConnectionFactory _factory = new(new NullMetaService());

    [Fact]
    public async Task SaveAsync_ValidName_ReturnsOne()
    {
        int result = await _factory.Trainers.SaveAsync("Ash");
        result.ShouldBe(1);
    }

    [Fact]
    public async Task SaveAsync_DuplicateName_ReturnsZero()
    {
        await _factory.Trainers.SaveAsync("Misty");
        int result = await _factory.Trainers.SaveAsync("Misty");
        result.ShouldBe(0);
    }

    [Fact]
    public async Task GetAllAsync_AfterSave_ReturnsTrainer()
    {
        await _factory.Trainers.SaveAsync("Brock");
        List<Trainer> all = await _factory.Trainers.GetAllAsync();
        all.ShouldContain(t => t.Name == "Brock");
    }

    [Fact]
    public async Task GetByNameAsync_ExistingName_ReturnsTrainer()
    {
        await _factory.Trainers.SaveAsync("Gary");
        Trainer? found = await _factory.Trainers.GetByNameAsync("Gary");
        found.ShouldNotBeNull();
        found!.Name.ShouldBe("Gary");
    }

    [Fact]
    public async Task GetByNameAsync_MissingName_ReturnsNull()
    {
        Trainer? found = await _factory.Trainers.GetByNameAsync("Nobody");
        found.ShouldBeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsTrainer()
    {
        await _factory.Trainers.SaveAsync("Erika");
        Trainer? erika = await _factory.Trainers.GetByNameAsync("Erika");
        Trainer? found = await _factory.Trainers.GetByIdAsync(erika!.Id);
        found.ShouldNotBeNull();
        found!.Name.ShouldBe("Erika");
    }

    [Fact]
    public async Task DeleteAsync_ExistingTrainer_RemovesFromDb()
    {
        await _factory.Trainers.SaveAsync("Giovanni");
        Trainer? giovanni = await _factory.Trainers.GetByNameAsync("Giovanni");
        await _factory.Trainers.DeleteAsync(giovanni!);

        Trainer? after = await _factory.Trainers.GetByNameAsync("Giovanni");
        after.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteAsync_WithRelatedMatches_CleansUpAll()
    {
        // Save trainer + archetype + a match so cascading delete is exercised
        await _factory.Trainers.SaveAsync("Surge");
        Trainer surge = (await _factory.Trainers.GetByNameAsync("Surge"))!;

        await _factory.Archetypes.SaveAsync("PikachuEx", "ball_icon.png", surge.Id);
        List<Archetype> archetypes = await _factory.Archetypes.GetAllAsync();
        Archetype arch = archetypes.First(a => a.TrainerId == surge.Id);

        MatchEntry match = new() { TrainerId = surge.Id, PlayingId = arch.Id, AgainstId = arch.Id };
        Game game = new() { Result = Win };
        await _factory.Matches.SaveAsync(match, [game]);

        await _factory.Trainers.DeleteAsync(surge);

        List<Trainer> remaining = await _factory.Trainers.GetAllAsync();
        remaining.ShouldNotContain(t => t.Id == surge.Id);
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();
}
