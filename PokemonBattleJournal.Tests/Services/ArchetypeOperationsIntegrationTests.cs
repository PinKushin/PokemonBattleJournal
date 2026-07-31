using SQLite;

namespace PokemonBattleJournal.Tests.Services
{
    public class ArchetypeOperationsIntegrationTests
    {
        private TestSqliteConnectionFactory _factory = null!;
        private ArchetypeOperations _sut = null!;

        [SetUp]
        public async Task SetUp()
        {
            _factory = new TestSqliteConnectionFactory();
            var metaService = Substitute.For<ILimitlessMetaService>();
            metaService.GetTopDecksAsync(Arg.Any<int>())
                .Returns(Task.FromResult(new List<PokemonBattleJournal.Scraper.Models.MetaDeck>()));
            _sut = new ArchetypeOperations(_factory, Substitute.For<ILogger>(), metaService);
            _ = await _factory.GetDatabaseAsync();
        }

        [TearDown]
        public async Task TearDown()
        {
            await _factory.CloseAndDeleteAsync();
        }

        private async Task<uint> SeedTrainerAsync(string name = "TestTrainer")
        {
            SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();
            Trainer trainer = new() { Name = name, IsActive = true };
            _ = await db.InsertAsync(trainer);
            return (uint)trainer.Id;
        }

        [Test]
        public async Task SaveAsync_NewArchetype_PersistsToDatabase()
        {
            uint trainerId = await SeedTrainerAsync();

            int affected = await _sut.SaveAsync("Charizard", "charizard.png", trainerId);

            affected.ShouldBeGreaterThan(0);
            List<Archetype> archetypes = await _sut.GetAllAsync();
            archetypes.ShouldContain(a => a.Name == "Charizard");
        }

        [Test]
        public async Task SaveAsync_DuplicateName_ReturnsZero()
        {
            uint trainerId = await SeedTrainerAsync();
            _ = await _sut.SaveAsync("Pikachu", "pikachu.png", trainerId);

            int affected = await _sut.SaveAsync("Pikachu", "pikachu.png", trainerId);

            affected.ShouldBe(0);
        }

        [Test]
        public async Task GetAllAsync_AfterSave_ReturnsArchetype()
        {
            uint trainerId = await SeedTrainerAsync();
            _ = await _sut.SaveAsync("Mewtwo", "mewtwo.png", trainerId);

            List<Archetype> archetypes = await _sut.GetAllAsync();

            archetypes.ShouldNotBeEmpty();
            archetypes.ShouldContain(a => a.Name == "Mewtwo");
        }

        [Test]
        public async Task DeleteAsync_AfterSave_RemovesArchetype()
        {
            uint trainerId = await SeedTrainerAsync();
            _ = await _sut.SaveAsync("Blastoise", "blastoise.png", trainerId);
            List<Archetype> all = await _sut.GetAllAsync();
            Archetype toDelete = all.First(a => a.Name == "Blastoise");

            int deleted = await _sut.DeleteAsync(toDelete);

            deleted.ShouldBeGreaterThan(0);
            List<Archetype> remaining = await _sut.GetAllAsync();
            remaining.ShouldNotContain(a => a.Name == "Blastoise");
        }

        [Test]
        public async Task GetByIdAsync_AfterSave_ReturnsCorrectArchetype()
        {
            uint trainerId = await SeedTrainerAsync();
            _ = await _sut.SaveAsync("Venusaur", "venusaur.png", trainerId);
            List<Archetype> all = await _sut.GetAllAsync();
            Archetype saved = all.First(a => a.Name == "Venusaur");

            Archetype? found = await _sut.GetByIdAsync((uint)saved.Id);

            found.ShouldNotBeNull();
            found!.Name.ShouldBe("Venusaur");
            found.ImagePath.ShouldBe("venusaur.png");
        }

        [Test]
        public async Task SaveAsync_StoresImagePath()
        {
            uint trainerId = await SeedTrainerAsync();
            _ = await _sut.SaveAsync("Snorlax", "snorlax.png", trainerId);

            List<Archetype> archetypes = await _sut.GetAllAsync();
            Archetype? saved = archetypes.FirstOrDefault(a => a.Name == "Snorlax");

            saved.ShouldNotBeNull();
            saved!.ImagePath.ShouldBe("snorlax.png");
        }

        private sealed class TestSqliteConnectionFactory : SqliteConnectionFactory
        {
            private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"pbj_archetype_test_{Guid.NewGuid():N}.db3");

            public TestSqliteConnectionFactory()
                : base(Substitute.For<ILogger<SqliteConnectionFactory>>(), Substitute.For<ILimitlessMetaService>())
            { }

            protected override string GetDbPath() => _dbPath;

            public async Task CloseAndDeleteAsync()
            {
                SQLiteAsyncConnection db = await GetDatabaseAsync();
                await db.CloseAsync();
                if (File.Exists(_dbPath))
                    File.Delete(_dbPath);
            }
        }
    }
}
