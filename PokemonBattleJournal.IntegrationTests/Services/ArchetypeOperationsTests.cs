using Microsoft.Extensions.Logging.Abstractions;
using PokemonBattleJournal.Models;
using PokemonBattleJournal.Scraper.Interfaces;
using PokemonBattleJournal.Scraper.Models;
using PokemonBattleJournal.Services;

namespace PokemonBattleJournal.IntegrationTests.Services;

public class ArchetypeOperationsTests : IAsyncDisposable
{
    private TestSqliteConnectionFactory? _factory;

    [SetUp]
    public void SetUp()
    {
        _factory = new TestSqliteConnectionFactory(new NullMetaService());
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_factory != null)
        {
            await _factory.DisposeAsync();
            _factory = null;
        }
    }

    private async Task<Trainer> EnsureTrainerAsync()
    {
        ArgumentNullException.ThrowIfNull(_factory);
        await _factory.Trainers.SaveAsync("ArchetypeTestTrainer");
        return (await _factory.Trainers.GetByNameAsync("ArchetypeTestTrainer"))!;
    }

    private ArchetypeOperations CreateSut()
    {
        ArgumentNullException.ThrowIfNull(_factory);
        return new(_factory, NullLogger<ArchetypeOperations>.Instance, new NullMetaService());
    }

    [Test]
    public async Task GetAllAsync_EmptyDb_SeedsDefaultsAndIncludesOther()
    {
        ArchetypeOperations sut = CreateSut();
        List<Archetype> archetypes = await sut.GetAllAsync();

        archetypes.ShouldNotBeEmpty();
        archetypes.ShouldContain(a => a.Name == "Other");
    }

    [Test]
    public async Task GetAllAsync_EmptyDb_SeedsEightDefaultArchetypes()
    {
        ArchetypeOperations sut = CreateSut();
        List<Archetype> archetypes = await sut.GetAllAsync();

        // When meta service returns empty list, 8 hardcoded defaults should be seeded
        archetypes.Count.ShouldBeGreaterThanOrEqualTo(8);
    }

    [Test]
    public async Task SaveAsync_ValidArchetype_ReturnsOne()
    {
        Trainer trainer = await EnsureTrainerAsync();
        ArchetypeOperations sut = CreateSut();

        int result = await sut.SaveAsync("TestDeck", "test.png", trainer.Id);
        result.ShouldBe(1);
    }

    [Test]
    public async Task SaveAsync_EmptyName_ThrowsArgumentException()
    {
        Trainer trainer = await EnsureTrainerAsync();
        ArchetypeOperations sut = CreateSut();

        await Should.ThrowAsync<ArgumentException>(() => sut.SaveAsync("", "icon.png", trainer.Id));
    }

    [Test]
    public async Task SaveAsync_EmptyImagePath_ThrowsArgumentException()
    {
        Trainer trainer = await EnsureTrainerAsync();
        ArchetypeOperations sut = CreateSut();

        await Should.ThrowAsync<ArgumentException>(() => sut.SaveAsync("DeckName", "", trainer.Id));
    }

    [Test]
    public async Task SaveAsync_ZeroTrainerId_ThrowsArgumentException()
    {
        ArchetypeOperations sut = CreateSut();

        await Should.ThrowAsync<ArgumentException>(() => sut.SaveAsync("DeckName", "icon.png", 0));
    }

    [Test]
    public async Task SaveAsync_SavedArchetype_AppearsInGetAll()
    {
        Trainer trainer = await EnsureTrainerAsync();
        ArchetypeOperations sut = CreateSut();

        await sut.SaveAsync("SavedDeck", "saved.png", trainer.Id);
        List<Archetype> all = await sut.GetAllAsync();

        all.ShouldContain(a => a.Name == "SavedDeck" && a.ImagePath == "saved.png" && a.TrainerId == trainer.Id);
    }

    [Test]
    public async Task GetByIdAsync_ExistingArchetype_ReturnsArchetype()
    {
        Trainer trainer = await EnsureTrainerAsync();
        ArchetypeOperations sut = CreateSut();

        await sut.SaveAsync("FindableDeck", "find.png", trainer.Id);
        List<Archetype> all = await sut.GetAllAsync();
        Archetype saved = all.First(a => a.Name == "FindableDeck");

        Archetype? found = await sut.GetByIdAsync(saved.Id);
        found.ShouldNotBeNull();
        found!.Name.ShouldBe("FindableDeck");
    }

    [Test]
    public async Task GetByIdAsync_NonExistentId_ReturnsNull()
    {
        ArchetypeOperations sut = CreateSut();

        Archetype? found = await sut.GetByIdAsync(99999);
        found.ShouldBeNull();
    }

    [Test]
    public async Task DeleteAsync_ExistingArchetype_ReturnsOneAndRemoves()
    {
        Trainer trainer = await EnsureTrainerAsync();
        ArchetypeOperations sut = CreateSut();

        await sut.SaveAsync("DeletableDeck", "delete.png", trainer.Id);
        List<Archetype> all = await sut.GetAllAsync();
        Archetype toDelete = all.First(a => a.Name == "DeletableDeck");

        int result = await sut.DeleteAsync(toDelete);
        result.ShouldBe(1);

        List<Archetype> after = await sut.GetAllAsync();
        after.ShouldNotContain(a => a.Id == toDelete.Id);
    }

    [Test]
    public async Task DeleteAsync_ArchetypeUsedInMatch_ReturnsZeroAndDoesNotDelete()
    {
        Trainer trainer = await EnsureTrainerAsync();
        ArgumentNullException.ThrowIfNull(_factory);
        ArchetypeOperations sut = CreateSut();

        await sut.SaveAsync("UsedDeck", "used.png", trainer.Id);
        Archetype arch = (await sut.GetAllAsync()).First(a => a.Name == "UsedDeck");

        // Now save a match using this archetype
        var now = DateTime.Now;
        int saveResult = await _factory.Matches.SaveAsync(new MatchEntry
        {
            TrainerId = trainer.Id,
            PlayingId = arch.Id,
            AgainstId = arch.Id,
            StartTime = now,
            EndTime = now.AddMinutes(15),
            DatePlayed = now.Date,
        }, [new Game { Result = MatchResult.Win }]);

        // Verify the match was actually saved
        saveResult.ShouldBeGreaterThan(0);
        List<MatchEntry> allMatches = await _factory.Matches.GetByTrainerIdAsync(trainer.Id);
        allMatches.ShouldContain(m => m.PlayingId == arch.Id);

        // Delete should return 0 (failure) when archetype is used in a match
        int deleteResult = await sut.DeleteAsync(arch);
        deleteResult.ShouldBe(0);

        // Verify archetype still exists
        Archetype? stillExists = await sut.GetByIdAsync(arch.Id);
        stillExists.ShouldNotBeNull();
        stillExists.Name.ShouldBe("UsedDeck");
    }

    [Test]
    public async Task DeleteAsync_NullArchetype_ThrowsArgumentNullException()
    {
        ArchetypeOperations sut = CreateSut();

        await Should.ThrowAsync<ArgumentNullException>(() => sut.DeleteAsync(null!));
    }

    [Test]
    public async Task DeleteAsync_ZeroId_ThrowsArgumentException()
    {
        ArchetypeOperations sut = CreateSut();
        Archetype arch = new() { Name = "TestDeck", ImagePath = "test.png" };

        await Should.ThrowAsync<ArgumentException>(() => sut.DeleteAsync(arch));
    }

    [Test]
    public async Task GetAllAsync_WithMetaService_ReturnsDecks()
    {
        Trainer trainer = await EnsureTrainerAsync();
        var metaService = new FakeMetaService();
        ArgumentNullException.ThrowIfNull(_factory);
        var sut = new ArchetypeOperations(_factory, NullLogger<ArchetypeOperations>.Instance, metaService);

        List<Archetype> archetypes = await sut.GetAllAsync();

        archetypes.ShouldNotBeEmpty();
        // MetaDecks should be upserted
        archetypes.ShouldContain(a => a.Name == "Iron Valiant");
    }

    private sealed class FakeMetaService : ILimitlessMetaService
    {
        public Task<List<MetaDeck>> GetTopDecksAsync(int count = 10) =>
            Task.FromResult(new List<MetaDeck>
            {
                new("Iron Valiant", "iron-valiant.png"),
                new("Charizard ex", "charizard-ex.png"),
            });
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory != null)
        {
            await _factory.DisposeAsync();
        }
    }
}
