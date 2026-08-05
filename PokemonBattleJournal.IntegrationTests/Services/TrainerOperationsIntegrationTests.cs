
namespace PokemonBattleJournal.IntegrationTests.Services
{
    public class TrainerOperationsIntegrationTests
    {
        private TestSqliteConnectionFactory _factory = null!;
        private TrainerOperations _sut = null!;

        [SetUp]
        public async Task SetUp()
        {
            _factory = new TestSqliteConnectionFactory();
            _sut = new TrainerOperations(_factory, Substitute.For<ILogger>(), Substitute.For<IErrorHandler>());
            _ = await _factory.GetDatabaseAsync();
        }

        [TearDown]
        public async Task TearDown()
        {
            await _factory.CloseAndDeleteAsync();
        }

        [Test]
        public async Task SaveAsync_NewTrainer_PersistsToDatabase()
        {
            int affected = await _sut.SaveAsync("Ash");

            affected.ShouldBeGreaterThan(0);
            List<Trainer> all = await _sut.GetAllAsync();
            all.ShouldContain(t => t.Name == "Ash");
        }



        [Test]
        public async Task GetByNameAsync_AfterSave_ReturnsCorrectTrainer()
        {
            _ = await _sut.SaveAsync("Gary");

            Trainer? found = await _sut.GetByNameAsync("Gary");

            found.ShouldNotBeNull();
            found!.Name.ShouldBe("Gary");
        }

        [Test]
        public async Task GetActiveAsync_AfterSetActive_ReturnsActiveTrainer()
        {
            _ = await _sut.SaveAsync("Giovanni");
            Trainer? trainer = await _sut.GetByNameAsync("Giovanni");
            trainer.ShouldNotBeNull();
            await _sut.SetActiveAsync(trainer!);

            Trainer? active = await _sut.GetActiveAsync();

            active.ShouldNotBeNull();
            active!.Name.ShouldBe("Giovanni");
        }

        [Test]
        public async Task SetActiveAsync_SwitchBetweenTrainers_OnlyOneActive()
        {
            _ = await _sut.SaveAsync("TrainerA");
            _ = await _sut.SaveAsync("TrainerB");
            Trainer? trainerA = await _sut.GetByNameAsync("TrainerA");
            Trainer? trainerB = await _sut.GetByNameAsync("TrainerB");
            trainerA.ShouldNotBeNull();
            trainerB.ShouldNotBeNull();

            await _sut.SetActiveAsync(trainerA!);
            await _sut.SetActiveAsync(trainerB!);

            Trainer? active = await _sut.GetActiveAsync();
            active.ShouldNotBeNull();
            active!.Name.ShouldBe("TrainerB");

            List<Trainer> all = await _sut.GetAllAsync();
            all.Count(t => t.IsActive).ShouldBe(1);
        }

        [Test]
        public async Task DeleteAsync_AfterSave_RemovesTrainer()
        {
            _ = await _sut.SaveAsync("Erika");
            Trainer? trainer = await _sut.GetByNameAsync("Erika");
            trainer.ShouldNotBeNull();

            int deleted = await _sut.DeleteAsync(trainer!);

            deleted.ShouldBeGreaterThan(0);
            List<Trainer> remaining = await _sut.GetAllAsync();
            remaining.ShouldNotContain(t => t.Name == "Erika");
        }

        // ---------------------------------------------------------------------------
        // Additional guard and miss cases
        // ---------------------------------------------------------------------------

        [Test]
        public async Task GetByNameAsync_NonExistentName_ReturnsNull()
        {
            Trainer? found = await _sut.GetByNameAsync("Nobody");
            found.ShouldBeNull();
        }


        [Test]
        public async Task GetAllAsync_EmptyDatabase_ReturnsEmptyList()
        {
            List<Trainer> all = await _sut.GetAllAsync();
            all.ShouldBeEmpty();
        }

        // ---------------------------------------------------------------------------
        // GetByIdAsync
        // ---------------------------------------------------------------------------

        [Test]
        public async Task GetByIdAsync_ExistingTrainer_ReturnsTrainer()
        {
            _ = await _sut.SaveAsync("Oak");
            Trainer? byName = await _sut.GetByNameAsync("Oak");
            byName.ShouldNotBeNull();

            Trainer? byId = await _sut.GetByIdAsync(byName!.Id);

            byId.ShouldNotBeNull();
            byId!.Name.ShouldBe("Oak");
            byId.Id.ShouldBe(byName.Id);
        }


        private sealed class TestSqliteConnectionFactory : SqliteConnectionFactory
        {
            private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"pbj_trainer_test_{Guid.NewGuid():N}.db3");

            public TestSqliteConnectionFactory()
                : base(Substitute.For<ILogger<SqliteConnectionFactory>>(), Substitute.For<ILimitlessMetaService>(), Substitute.For<IErrorHandler>())
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
