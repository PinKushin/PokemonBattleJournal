
namespace PokemonBattleJournal.IntegrationTests.Services;

public class TrainerSwitchServiceTests : IAsyncDisposable
{
    private readonly TestSqliteConnectionFactory _factory = new(new NullMetaService());

    private TrainerSwitchService MakeService() =>
        new(_factory, NullLogger<TrainerSwitchService>.Instance);

    [Test]
    public async Task GetAllTrainersAsync_ReturnsAllTrainers()
    {
        await _factory.Trainers.SaveAsync("Eevee");
        await _factory.Trainers.SaveAsync("Flareon");

        TrainerSwitchService sut = MakeService();
        List<Trainer> all = await sut.GetAllTrainersAsync();

        all.ShouldContain(t => t.Name == "Eevee");
        all.ShouldContain(t => t.Name == "Flareon");
    }

    [Test]
    public async Task InitializeAsync_WithActiveTrainer_SetsActiveTrainer()
    {
        await _factory.Trainers.SaveAsync("Vaporeon");
        Trainer vaporeon = (await _factory.Trainers.GetByNameAsync("Vaporeon"))!;
        await _factory.Trainers.SetActiveAsync(vaporeon);

        TrainerSwitchService sut = MakeService();
        await sut.InitializeAsync();

        sut.ActiveTrainer.ShouldNotBeNull();
        sut.ActiveTrainer!.Name.ShouldBe("Vaporeon");
    }

    [Test]
    public async Task InitializeAsync_NoActiveTrainer_ActiveTrainerIsNull()
    {
        TrainerSwitchService sut = MakeService();
        await sut.InitializeAsync();

        sut.ActiveTrainer.ShouldBeNull();
    }

    [Test]
    public async Task SwitchToAsync_SetsActiveTrainer()
    {
        await _factory.Trainers.SaveAsync("Jolteon");
        Trainer jolteon = (await _factory.Trainers.GetByNameAsync("Jolteon"))!;

        TrainerSwitchService sut = MakeService();
        await sut.SwitchToAsync(jolteon);

        sut.ActiveTrainer.ShouldNotBeNull();
        sut.ActiveTrainer!.Name.ShouldBe("Jolteon");
    }

    [Test]
    public async Task SwitchToAsync_FiresTrainerChangedEvent()
    {
        await _factory.Trainers.SaveAsync("Espeon");
        Trainer espeon = (await _factory.Trainers.GetByNameAsync("Espeon"))!;

        TrainerSwitchService sut = MakeService();
        Trainer? eventTrainer = null;
        sut.TrainerChanged += (_, t) => eventTrainer = t;

        await sut.SwitchToAsync(espeon);

        eventTrainer.ShouldNotBeNull();
        eventTrainer!.Name.ShouldBe("Espeon");
    }

    [Test]
    public async Task SwitchToAsync_UpdatesDbActiveFlag()
    {
        await _factory.Trainers.SaveAsync("Umbreon");
        await _factory.Trainers.SaveAsync("Sylveon");
        Trainer umbreon = (await _factory.Trainers.GetByNameAsync("Umbreon"))!;
        Trainer sylveon = (await _factory.Trainers.GetByNameAsync("Sylveon"))!;

        TrainerSwitchService sut = MakeService();
        await sut.SwitchToAsync(umbreon);
        await sut.SwitchToAsync(sylveon);

        Trainer? active = await _factory.Trainers.GetActiveAsync();
        active!.Name.ShouldBe("Sylveon");
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();
}
