
namespace PokemonBattleJournal.IntegrationTests.Services
{
    public class ArchetypeOperationsIntegrationTests
    {
        private TestSqliteConnectionFactory _factory = null!;
        private ArchetypeOperations _sut = null!;

        [SetUp]
        public async Task SetUp()
        {
            _factory = new TestSqliteConnectionFactory();
            ILimitlessMetaService metaService = Substitute.For<ILimitlessMetaService>();
            metaService.GetTopDecksAsync(Arg.Any<int>())
                .Returns(Task.FromResult(new List<PokemonBattleJournal.Scraper.Models.MetaDeck>()));
            _sut = new ArchetypeOperations(_factory, Substitute.For<ILogger>(), metaService, Substitute.For<IErrorHandler>());
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
        public async Task GetAllAsync_OgerponBox_ResolvesToTealMaskSprite()
        {
            ILimitlessMetaService metaService = Substitute.For<ILimitlessMetaService>();
            metaService.GetTopDecksAsync(Arg.Any<int>())
                .Returns(Task.FromResult(new List<PokemonBattleJournal.Scraper.Models.MetaDeck>
                {
                    new("Ogerpon Box", "https://r2.limitlesstcg.net/pokemon/gen9/ogerpon.png"),
                }));
            ArchetypeOperations sut = new ArchetypeOperations(_factory, Substitute.For<ILogger>(), metaService, Substitute.For<IErrorHandler>());

            List<Archetype> archetypes = await sut.GetAllAsync();

            Archetype? saved = archetypes.FirstOrDefault(a => a.Name == "Ogerpon Box");
            saved.ShouldNotBeNull();
            saved!.ImagePath.ShouldBe("ogerpon_teal_mask.png");
        }

        [Test]
        public async Task GetAllAsync_OgerponWellspring_ResolvesToWellspringMaskSprite()
        {
            ILimitlessMetaService metaService = Substitute.For<ILimitlessMetaService>();
            metaService.GetTopDecksAsync(Arg.Any<int>())
                .Returns(Task.FromResult(new List<PokemonBattleJournal.Scraper.Models.MetaDeck>
                {
                    new("Ogerpon Meganium", "https://r2.limitlesstcg.net/pokemon/gen9/ogerpon.png",
                        "https://r2.limitlesstcg.net/pokemon/gen9/ogerpon-wellspring.png"),
                }));
            ArchetypeOperations sut = new ArchetypeOperations(_factory, Substitute.For<ILogger>(), metaService, Substitute.For<IErrorHandler>());

            List<Archetype> archetypes = await sut.GetAllAsync();

            Archetype? saved = archetypes.FirstOrDefault(a => a.Name == "Ogerpon Meganium");
            saved.ShouldNotBeNull();
            saved!.ImagePath2.ShouldBe("ogerpon_wellspring_mask.png");
        }

        [Test]
        public async Task GetAllAsync_WithMetaDecks_UpsertsMetaArchetypes()
        {
            // Re-create the SUT with a metaService that returns real decks
            ILimitlessMetaService metaService = Substitute.For<ILimitlessMetaService>();
            metaService.GetTopDecksAsync(Arg.Any<int>())
                .Returns(Task.FromResult(new List<PokemonBattleJournal.Scraper.Models.MetaDeck>
                {
                    new("Pikachu", "https://cdn.example.com/pikachu.png"),
                    new("Eevee", "https://cdn.example.com/eevee.png"),
                }));
            ArchetypeOperations sut = new ArchetypeOperations(_factory, Substitute.For<ILogger>(), metaService, Substitute.For<IErrorHandler>());

            List<Archetype> archetypes = await sut.GetAllAsync();

            archetypes.ShouldContain(a => a.Name == "Pikachu");
            archetypes.ShouldContain(a => a.Name == "Eevee");
        }

        [Test]
        public async Task GetAllAsync_OfflineEmptyDb_SeedsHardcodedDefaults()
        {
            // metaService returns empty (offline), DB is empty → hardcoded defaults are seeded
            List<Archetype> archetypes = await _sut.GetAllAsync();

            archetypes.ShouldNotBeEmpty();
            archetypes.ShouldContain(a => a.Name == "Other");
            archetypes.ShouldContain(a => a.Name == "Charizard");
        }

        // Every offline default, name AND sprite. Written to kill 14 surviving string mutants
        // found by Stryker on 2026-08-10: the existing offline tests assert "not empty",
        // "contains Other" and "Count >= 8", so every literal in the seed list could be
        // replaced with "" and all of them still passed.
        //
        // "Other" and "Dragapult ex / Dusknoir" are deliberately ABSENT from this list. Both
        // rows are also written unconditionally further down GetAllAsync, so mutating their
        // seed-list literals leaves the observable database identical — equivalent by
        // construction, and no assertion here can distinguish them. Killing those would mean
        // deleting the redundancy in the production code, which is a separate decision.
        [TestCase("Regidrago", "regidrago.png")]
        [TestCase("Charizard", "charizard.png")]
        [TestCase("Klawf", "klawf.png")]
        [TestCase("Snorlax Stall", "snorlax.png")]
        [TestCase("Raging Bolt", "raging_bolt.png")]
        [TestCase("Gardevoir", "gardevoir.png")]
        [TestCase("Miraidon", "miraidon.png")]
        public async Task GetAllAsync_OfflineEmptyDb_SeedsDefaultWithItsOwnSprite(string name, string sprite)
        {
            List<Archetype> archetypes = await _sut.GetAllAsync();

            Archetype? seeded = archetypes.FirstOrDefault(a => a.Name == name);
            seeded.ShouldNotBeNull($"offline seed is missing '{name}'");
            seeded!.ImagePath.ShouldBe(sprite);
        }

        // The dual-icon BACKFILL, which is a different code path from the dual-icon INSERT and
        // was completely untested. GetAllAsync_OgerponWellspring_... covers the insert: a NEW
        // row gets ImagePath2 from the same INSERT statement that creates it. The backfill only
        // runs when the row ALREADY EXISTS with ImagePath2 NULL — the case where a deck was
        // first seen without a second icon, or arrived via TrainerHill import.
        //
        // That distinction is why Stryker could delete the backfill and flip its guard with
        // both mutants surviving: with no pre-existing row, correct and broken code produce the
        // same database. The fix is the INPUT, not a stronger assertion.
        [Test]
        public async Task GetAllAsync_ExistingRowWithNoSecondIcon_BackfillsImagePath2()
        {
            SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();
            _ = await db.ExecuteAsync(
                "INSERT INTO Archetype (Name, ImagePath, ImagePath2) VALUES (?, ?, NULL)",
                "Ogerpon Meganium", "ogerpon_teal_mask.png");

            ILimitlessMetaService metaService = Substitute.For<ILimitlessMetaService>();
            metaService.GetTopDecksAsync(Arg.Any<int>())
                .Returns(Task.FromResult(new List<PokemonBattleJournal.Scraper.Models.MetaDeck>
                {
                    new("Ogerpon Meganium", "https://r2.limitlesstcg.net/pokemon/gen9/ogerpon.png",
                        "https://r2.limitlesstcg.net/pokemon/gen9/ogerpon-wellspring.png"),
                }));
            ArchetypeOperations sut = new(_factory, Substitute.For<ILogger>(), metaService, Substitute.For<IErrorHandler>());

            List<Archetype> archetypes = await sut.GetAllAsync();

            Archetype? backfilled = archetypes.FirstOrDefault(a => a.Name == "Ogerpon Meganium");
            backfilled.ShouldNotBeNull();
            backfilled!.ImagePath2.ShouldBe("ogerpon_wellspring_mask.png");
            // The control: the backfill must not disturb the icon that was already there.
            backfilled.ImagePath.ShouldBe("ogerpon_teal_mask.png");
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

        [Test]
        public async Task DeleteAsync_ArchetypeInUseByMatch_ThrowsInvalidOperationException()
        {
            uint trainerId = await SeedTrainerAsync();
            _ = await _sut.SaveAsync("Raichu", "raichu.png", trainerId);
            List<Archetype> all = await _sut.GetAllAsync();
            Archetype archetype = all.First(a => a.Name == "Raichu");

            // Use RunInTransactionAsync so the insert is committed on the same serialized queue
            // before DeleteAsync's RunInTransactionAsync reads the COUNT.
            SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();
            await db.RunInTransactionAsync(tran =>
            {
                tran.Execute(
                    "INSERT INTO MatchEntry (TrainerId, PlayingId, AgainstId, Result, DatePlayed, StartTime, EndTime) VALUES (?,?,?,?,?,?,?)",
                    trainerId, archetype.Id, archetype.Id,
                    (int)MatchResult.Win,
                    DateTime.UtcNow.Ticks, DateTime.UtcNow.Ticks, DateTime.UtcNow.AddMinutes(5).Ticks);
            });

            // DeleteAsync catches InvalidOperationException internally and returns 0 (does not rethrow)
            int result = await _sut.DeleteAsync(archetype);
            result.ShouldBe(0);
            // Archetype must still exist in the DB
            Archetype? still = await _sut.GetByIdAsync(archetype.Id);
            still.ShouldNotBeNull();
        }

        [Test]
        public async Task SaveAsync_WithSecondIcon_PersistsImagePath2()
        {
            uint trainerId = await SeedTrainerAsync();

            int affected = await _sut.SaveAsync("Charizard ex / Pidgeot ex", "charizard_ex.png", trainerId, "pidgeot_ex.png");

            affected.ShouldBeGreaterThan(0);
            List<Archetype> archetypes = await _sut.GetAllAsync();
            Archetype? saved = archetypes.FirstOrDefault(a => a.Name == "Charizard ex / Pidgeot ex");
            saved.ShouldNotBeNull();
            saved!.ImagePath2.ShouldBe("pidgeot_ex.png");
        }

        [Test]
        public async Task GetAllAsync_WithMetaDeckHavingSecondaryImage_PersistsImagePath2()
        {
            ILimitlessMetaService metaService = Substitute.For<ILimitlessMetaService>();
            metaService.GetTopDecksAsync(Arg.Any<int>())
                .Returns(Task.FromResult(new List<PokemonBattleJournal.Scraper.Models.MetaDeck>
                {
                    new("Charizard ex / Pidgeot ex", "https://cdn.example.com/charizard_ex.png", "https://cdn.example.com/pidgeot_ex.png"),
                }));
            ArchetypeOperations sut = new ArchetypeOperations(_factory, Substitute.For<ILogger>(), metaService, Substitute.For<IErrorHandler>());

            List<Archetype> archetypes = await sut.GetAllAsync();

            Archetype? saved = archetypes.FirstOrDefault(a => a.Name == "Charizard ex / Pidgeot ex");
            saved.ShouldNotBeNull();
            saved!.ImagePath2.ShouldBe("pidgeot_ex.png");
        }

        [Test]
        public async Task GetAllAsync_ArchetypeCreatedByImportWithSubstitute_GetsProperSpriteFromMeta()
        {
            // Simulate TrainerHill import creating archetype with substitute.png placeholder
            SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();
            await db.ExecuteAsync(
                "INSERT OR IGNORE INTO Archetype (Name, ImagePath) VALUES (?, ?)",
                "Ogerpon Box", "substitute.png");

            ILimitlessMetaService metaService = Substitute.For<ILimitlessMetaService>();
            metaService.GetTopDecksAsync(Arg.Any<int>())
                .Returns(Task.FromResult(new List<PokemonBattleJournal.Scraper.Models.MetaDeck>
                {
                    new("Ogerpon Box", "https://r2.limitlesstcg.net/pokemon/gen9/ogerpon.png"),
                }));
            ArchetypeOperations sut = new ArchetypeOperations(_factory, Substitute.For<ILogger>(), metaService, Substitute.For<IErrorHandler>());

            List<Archetype> archetypes = await sut.GetAllAsync();

            Archetype? saved = archetypes.FirstOrDefault(a => a.Name == "Ogerpon Box");
            saved.ShouldNotBeNull();
            saved!.ImagePath.ShouldBe("ogerpon_teal_mask.png");
        }




        private sealed class TestSqliteConnectionFactory : SqliteConnectionFactory
        {
            private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"pbj_archetype_test_{Guid.NewGuid():N}.db3");

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
