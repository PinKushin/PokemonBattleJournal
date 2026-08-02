using SQLite;

namespace PokemonBattleJournal.Tests.Services
{
    public class TagOperationsIntegrationTests
    {
        private TestSqliteConnectionFactory _factory = null!;
        private TagOperations _sut = null!;

        [SetUp]
        public async Task SetUp()
        {
            _factory = new TestSqliteConnectionFactory();
            _sut = new TagOperations(_factory, Substitute.For<ILogger>());
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
        public async Task SaveAsync_NewTag_PersistsToDatabase()
        {
            uint trainerId = await SeedTrainerAsync();

            int affected = await _sut.SaveAsync("Aggro", trainerId);

            affected.ShouldBeGreaterThan(0);
            List<Tags> tags = await _sut.GetAllAsync();
            tags.ShouldContain(t => t.Name == "Aggro");
        }

        [Test]
        public async Task SaveAsync_DuplicateTag_ReturnsZero()
        {
            uint trainerId = await SeedTrainerAsync();
            _ = await _sut.SaveAsync("Control", trainerId);

            int affected = await _sut.SaveAsync("Control", trainerId);

            affected.ShouldBe(0);
        }

        [Test]
        public async Task GetAllAsync_AfterSave_ReturnsTag()
        {
            uint trainerId = await SeedTrainerAsync();
            _ = await _sut.SaveAsync("Midrange", trainerId);

            List<Tags> tags = await _sut.GetAllAsync();

            tags.ShouldNotBeEmpty();
            tags.ShouldContain(t => t.Name == "Midrange");
        }

        [Test]
        public async Task DeleteAsync_AfterSave_RemovesTag()
        {
            uint trainerId = await SeedTrainerAsync();
            _ = await _sut.SaveAsync("Tempo", trainerId);
            List<Tags> tags = await _sut.GetAllAsync();
            Tags toDelete = tags.First(t => t.Name == "Tempo");

            int deleted = await _sut.DeleteAsync(toDelete);

            deleted.ShouldBeGreaterThan(0);
            List<Tags> remaining = await _sut.GetAllAsync();
            remaining.ShouldNotContain(t => t.Name == "Tempo");
        }

        [Test]
        public async Task GetByIdAsync_AfterSave_ReturnsCorrectTag()
        {
            uint trainerId = await SeedTrainerAsync();
            _ = await _sut.SaveAsync("Stall", trainerId);
            List<Tags> tags = await _sut.GetAllAsync();
            Tags saved = tags.First(t => t.Name == "Stall");

            Tags? found = await _sut.GetByIdAsync((uint)saved.Id);

            found.ShouldNotBeNull();
            found!.Name.ShouldBe("Stall");
        }

        [Test]
        public async Task GetByIdAsync_NonExistentId_ReturnsNull()
        {
            Tags? found = await _sut.GetByIdAsync(99999);
            found.ShouldBeNull();
        }

        [Test]
        public async Task GetAllAsync_EmptyDatabase_SeedsDefaultTags()
        {
            // TagOperations.GetAllAsync seeds 8 default tags when the table is empty
            List<Tags> tags = await _sut.GetAllAsync();
            tags.ShouldNotBeEmpty();
            tags.ShouldContain(t => t.Name == "Lucky");
        }

        [Test]
        public async Task DeleteAsync_ZeroId_ThrowsArgumentException()
        {
            await Should.ThrowAsync<ArgumentException>(() => _sut.DeleteAsync(new Tags { Id = 0, Name = "Ghost" }));
        }

        [Test]
        public async Task SaveAsync_EmptyTagName_ThrowsArgumentException()
        {
            uint trainerId = await SeedTrainerAsync();
            await Should.ThrowAsync<ArgumentException>(() => _sut.SaveAsync("", trainerId));
        }

        [Test]
        public async Task SaveAsync_ZeroTrainerId_ThrowsArgumentException()
        {
            await Should.ThrowAsync<ArgumentException>(() => _sut.SaveAsync("Valid", 0));
        }

        private sealed class TestSqliteConnectionFactory : SqliteConnectionFactory
        {
            private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"pbj_tag_test_{Guid.NewGuid():N}.db3");

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
