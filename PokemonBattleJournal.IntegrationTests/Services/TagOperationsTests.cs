
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

    [Test]
    public async Task GetAllAsync_EmptyDb_ReturnsDefaultTags()
    {
        List<Tags> tags = await _factory.Tags.GetAllAsync();
        // TagOperations seeds 8 defaults when table is empty
        tags.Count.ShouldBeGreaterThanOrEqualTo(8);
    }

    [Test]
    public async Task SaveAsync_ValidTag_ReturnsOne()
    {
        await EnsureTrainerAsync();
        int result = await _factory.Tags.SaveAsync("Aggressive", _trainerId);
        result.ShouldBe(1);
    }

    [Test]
    public async Task SaveAsync_Tag_AppearsInGetAll()
    {
        await EnsureTrainerAsync();
        await _factory.Tags.SaveAsync("Control", _trainerId);
        List<Tags> tags = await _factory.Tags.GetAllAsync();
        tags.ShouldContain(t => t.Name == "Control");
    }

    [Test]
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

    [Test]
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

    [Test]
    public async Task GetAllAsync_WithExistingTags_DoesNotReseed()
    {
        await EnsureTrainerAsync();
        await _factory.Tags.SaveAsync("CustomTag", _trainerId);

        // Call twice — second call should NOT re-seed defaults since table is non-empty
        List<Tags> first = await _factory.Tags.GetAllAsync();
        List<Tags> second = await _factory.Tags.GetAllAsync();

        second.Count.ShouldBe(first.Count);
        second.ShouldContain(t => t.Name == "CustomTag");
    }

    [Test]
    public async Task SaveAsync_EmptyName_ThrowsArgumentException()
    {
        await EnsureTrainerAsync();
        await Should.ThrowAsync<ArgumentException>(
            () => _factory.Tags.SaveAsync("", _trainerId));
    }

    [Test]
    public async Task SaveAsync_ZeroTrainerId_ThrowsArgumentException()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _factory.Tags.SaveAsync("SpeedRun", 0));
    }

    [Test]
    public async Task GetByIdAsync_NonExistentId_ReturnsNull()
    {
        Tags? result = await _factory.Tags.GetByIdAsync(99999);
        result.ShouldBeNull();
    }

    [Test]
    public async Task DeleteAsync_ZeroId_ThrowsArgumentException()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _factory.Tags.DeleteAsync(new Tags { Name = "Ghost" }));
    }

    [Test]
    public async Task DeleteAsync_TagUsedInGame_CascadesTagGameRelationship()
    {
        await EnsureTrainerAsync();

        await _factory.Trainers.SaveAsync("TagCascadeTrainer");
        Trainer trainer = (await _factory.Trainers.GetByNameAsync("TagCascadeTrainer"))!;
        await _factory.Archetypes.SaveAsync("Pikachu", "ball_icon.png", trainer.Id);
        List<Archetype> archs = await _factory.Archetypes.GetAllAsync();
        Archetype arch = archs.First(a => a.TrainerId == trainer.Id);

        await _factory.Tags.SaveAsync("CascadeTag", trainer.Id);
        List<Tags> allTags = await _factory.Tags.GetAllAsync();
        Tags cascadeTag = allTags.First(t => t.Name == "CascadeTag");

        MatchEntry match = new() { TrainerId = trainer.Id, PlayingId = arch.Id, AgainstId = arch.Id };
        Game game = new() { Result = MatchResult.Win, Tags = [cascadeTag] };
        await _factory.Matches.SaveAsync(match, [game]);

        await _factory.Tags.DeleteAsync(cascadeTag);

        Tags? deleted = await _factory.Tags.GetByIdAsync(cascadeTag.Id);
        deleted.ShouldBeNull();
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();
}
