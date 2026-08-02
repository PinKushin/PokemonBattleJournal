using PokemonBattleJournal.Models;
using static PokemonBattleJournal.Models.MatchResult;

namespace PokemonBattleJournal.IntegrationTests.Services;

[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class TrainerOperationsTests : IAsyncDisposable
{
    private readonly TestSqliteConnectionFactory _factory = new(new NullMetaService());

    [Test]
    public async Task SaveAsync_ValidName_ReturnsOne()
    {
        int result = await _factory.Trainers.SaveAsync("Ash");
        result.ShouldBe(1);
    }

    [Test]
    public async Task SaveAsync_DuplicateName_ReturnsZero()
    {
        await _factory.Trainers.SaveAsync("Misty");
        int result = await _factory.Trainers.SaveAsync("Misty");
        result.ShouldBe(0);
    }

    [Test]
    public async Task GetAllAsync_AfterSave_ReturnsTrainer()
    {
        await _factory.Trainers.SaveAsync("Brock");
        List<Trainer> all = await _factory.Trainers.GetAllAsync();
        all.ShouldContain(t => t.Name == "Brock");
    }

    [Test]
    public async Task GetByNameAsync_ExistingName_ReturnsTrainer()
    {
        await _factory.Trainers.SaveAsync("Gary");
        Trainer? found = await _factory.Trainers.GetByNameAsync("Gary");
        found.ShouldNotBeNull();
        found!.Name.ShouldBe("Gary");
    }

    [Test]
    public async Task GetByNameAsync_MissingName_ReturnsNull()
    {
        Trainer? found = await _factory.Trainers.GetByNameAsync("Nobody");
        found.ShouldBeNull();
    }

    [Test]
    public async Task GetByIdAsync_ExistingId_ReturnsTrainer()
    {
        await _factory.Trainers.SaveAsync("Erika");
        Trainer? erika = await _factory.Trainers.GetByNameAsync("Erika");
        Trainer? found = await _factory.Trainers.GetByIdAsync(erika!.Id);
        found.ShouldNotBeNull();
        found!.Name.ShouldBe("Erika");
    }

    [Test]
    public async Task DeleteAsync_ExistingTrainer_RemovesFromDb()
    {
        await _factory.Trainers.SaveAsync("Giovanni");
        Trainer? giovanni = await _factory.Trainers.GetByNameAsync("Giovanni");
        await _factory.Trainers.DeleteAsync(giovanni!);

        Trainer? after = await _factory.Trainers.GetByNameAsync("Giovanni");
        after.ShouldBeNull();
    }

    [Test]
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

    [Test]
    public async Task SaveAsync_EmptyName_ThrowsArgumentException()
    {
        await Should.ThrowAsync<ArgumentException>(() => _factory.Trainers.SaveAsync(""));
    }

    [Test]
    public async Task SaveAsync_WhitespaceName_ThrowsArgumentException()
    {
        await Should.ThrowAsync<ArgumentException>(() => _factory.Trainers.SaveAsync("   "));
    }

    [Test]
    public async Task GetByNameAsync_EmptyName_ThrowsArgumentException()
    {
        await Should.ThrowAsync<ArgumentException>(() => _factory.Trainers.GetByNameAsync(""));
    }

    [Test]
    public async Task GetByIdAsync_NonExistentId_ReturnsNull()
    {
        Trainer? result = await _factory.Trainers.GetByIdAsync(99999);
        result.ShouldBeNull();
    }

    [Test]
    public async Task GetAllAsync_EmptyDb_ReturnsEmptyList()
    {
        List<Trainer> all = await _factory.Trainers.GetAllAsync();
        all.ShouldBeEmpty();
    }

    [Test]
    public async Task DeleteAsync_NullTrainer_ThrowsArgumentNullException()
    {
        await Should.ThrowAsync<ArgumentNullException>(() => _factory.Trainers.DeleteAsync(null!));
    }

    [Test]
    public async Task DeleteAsync_ZeroId_ThrowsArgumentException()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _factory.Trainers.DeleteAsync(new Trainer { Name = "Ghost" }));
    }

    [Test]
    public async Task SetActiveAsync_OneOfTwoTrainers_OnlyOneIsActive()
    {
        await _factory.Trainers.SaveAsync("Red");
        await _factory.Trainers.SaveAsync("Blue");
        Trainer red = (await _factory.Trainers.GetByNameAsync("Red"))!;
        Trainer blue = (await _factory.Trainers.GetByNameAsync("Blue"))!;

        await _factory.Trainers.SetActiveAsync(red);
        await _factory.Trainers.SetActiveAsync(blue);

        List<Trainer> all = await _factory.Trainers.GetAllAsync();
        all.Count(t => t.IsActive).ShouldBe(1);
        all.Single(t => t.IsActive).Name.ShouldBe("Blue");
    }

    [Test]
    public async Task GetActiveAsync_AfterSetActive_ReturnsCorrectTrainer()
    {
        await _factory.Trainers.SaveAsync("Lance");
        Trainer lance = (await _factory.Trainers.GetByNameAsync("Lance"))!;

        await _factory.Trainers.SetActiveAsync(lance);

        Trainer? active = await _factory.Trainers.GetActiveAsync();
        active.ShouldNotBeNull();
        active!.Name.ShouldBe("Lance");
    }

    [Test]
    public async Task GetActiveAsync_NoActiveTrainer_ReturnsNull()
    {
        Trainer? active = await _factory.Trainers.GetActiveAsync();
        active.ShouldBeNull();
    }

    [Test]
    public async Task SetActiveAsync_SwitchingActive_PreviousTrainerBecomesInactive()
    {
        await _factory.Trainers.SaveAsync("Koga");
        await _factory.Trainers.SaveAsync("Sabrina");
        Trainer koga = (await _factory.Trainers.GetByNameAsync("Koga"))!;
        Trainer sabrina = (await _factory.Trainers.GetByNameAsync("Sabrina"))!;

        await _factory.Trainers.SetActiveAsync(koga);
        await _factory.Trainers.SetActiveAsync(sabrina);

        Trainer? active = await _factory.Trainers.GetActiveAsync();
        active!.Name.ShouldBe("Sabrina");

        Trainer? reloadedKoga = await _factory.Trainers.GetByNameAsync("Koga");
        reloadedKoga!.IsActive.ShouldBeFalse();
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();
}
