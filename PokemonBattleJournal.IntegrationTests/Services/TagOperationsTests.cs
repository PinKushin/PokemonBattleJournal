using PokemonBattleJournal.Models;

namespace PokemonBattleJournal.IntegrationTests.Services;

public class TagOperationsTests : IAsyncDisposable
{
    private readonly TestSqliteConnectionFactory _factory = new(new NullMetaService());
    private uint _trainerId;

    private async Task EnsureTrainerAsync()
    {
        if (_trainerId != 0) return;
        await _factory.Trainers.SaveAsync("TagTestTrainer");
        Trainer t = (await _factory.Trainers.GetByNameAsync("TagTestTrainer"))!;
        _trainerId = t.Id;
    }

    [Fact]
    public async Task GetAllAsync_EmptyDb_ReturnsDefaultTags()
    {
        List<Tags> tags = await _factory.Tags.GetAllAsync();
        // TagOperations seeds 8 defaults when table is empty
        tags.Count.ShouldBeGreaterThanOrEqualTo(8);
    }

    [Fact]
    public async Task SaveAsync_ValidTag_ReturnsOne()
    {
        await EnsureTrainerAsync();
        int result = await _factory.Tags.SaveAsync("Aggressive", _trainerId);
        result.ShouldBe(1);
    }

    [Fact]
    public async Task SaveAsync_Tag_AppearsInGetAll()
    {
        await EnsureTrainerAsync();
        await _factory.Tags.SaveAsync("Control", _trainerId);
        List<Tags> tags = await _factory.Tags.GetAllAsync();
        tags.ShouldContain(t => t.Name == "Control");
    }

    [Fact]
    public async Task GetByIdAsync_ExistingTag_ReturnsTag()
    {
        await EnsureTrainerAsync();
        await _factory.Tags.SaveAsync("Tempo", _trainerId);
        List<Tags> all = await _factory.Tags.GetAllAsync();
        Tags tempo = all.First(t => t.Name == "Tempo");

        Tags? found = await _factory.Tags.GetByIdAsync(tempo.Id);
        found.ShouldNotBeNull();
        found!.Name.ShouldBe("Tempo");
    }

    [Fact]
    public async Task DeleteAsync_ExistingTag_RemovesIt()
    {
        await EnsureTrainerAsync();
        await _factory.Tags.SaveAsync("Stall", _trainerId);
        List<Tags> all = await _factory.Tags.GetAllAsync();
        Tags stall = all.First(t => t.Name == "Stall");

        await _factory.Tags.DeleteAsync(stall);

        Tags? after = await _factory.Tags.GetByIdAsync(stall.Id);
        after.ShouldBeNull();
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();
}
