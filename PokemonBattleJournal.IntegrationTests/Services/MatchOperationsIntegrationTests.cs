
namespace PokemonBattleJournal.IntegrationTests.Services
{
    /// <summary>
    /// Integration tests for MatchOperations using a real in-memory SQLite database.
    /// These cover the end-to-end save/retrieve paths that UI tests exercise via picker
    /// interaction, providing a CI-stable regression layer independent of UI automation.
    /// </summary>
    public class MatchOperationsIntegrationTests
    {
        private InMemorySqliteConnectionFactory _factory = null!;
        private MatchOperations _sut = null!;

        [SetUp]
        public async Task SetUp()
        {
            _factory = new InMemorySqliteConnectionFactory();
            _sut = new MatchOperations(_factory, Substitute.For<ILogger>(), Substitute.For<IErrorHandler>());
            // Trigger table creation
            _ = await _factory.GetDatabaseAsync();
        }

        [TearDown]
        public async Task TearDown()
        {
            await _factory.CloseAndDeleteAsync();
        }

        // ---------------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------------

        private async Task<uint> SeedTrainerAsync(string name = "TestTrainer")
        {
            SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();
            Trainer trainer = new() { Name = name, IsActive = true };
            _ = await db.InsertAsync(trainer);
            return (uint)trainer.Id;
        }

        private async Task<uint> SeedArchetypeAsync(string name = "TestDeck")
        {
            SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();
            Archetype archetype = new() { Name = name, ImagePath = "ball_icon.png" };
            _ = await db.InsertAsync(archetype);
            return (uint)archetype.Id;
        }

        // ---------------------------------------------------------------------------
        // SaveAsync — happy paths
        // ---------------------------------------------------------------------------

        [Test]
        public async Task SaveAsync_WinResult_PersistsMatchToDatabase()
        {
            uint trainerId = await SeedTrainerAsync();
            uint archetypeId = await SeedArchetypeAsync();

            MatchEntry match = new()
            {
                TrainerId = trainerId,
                PlayingId = archetypeId,
                AgainstId = archetypeId,
                Result = MatchResult.Win,
                DatePlayed = DateTime.UtcNow,
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow.AddMinutes(20),
            };
            List<Game> games = [new Game { Result = MatchResult.Win, Turn = 1 }];

            int affected = await _sut.SaveAsync(match, games);

            affected.ShouldBeGreaterThan(0);
            SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();
            MatchEntry? saved = await db.FindAsync<MatchEntry>(match.Id);
            saved.ShouldNotBeNull();
            saved!.Result.ShouldBe(MatchResult.Win);
        }

        [Test]
        public async Task SaveAsync_LossResult_PersistsCorrectResult()
        {
            uint trainerId = await SeedTrainerAsync();
            uint archetypeId = await SeedArchetypeAsync();

            MatchEntry match = new()
            {
                TrainerId = trainerId,
                PlayingId = archetypeId,
                AgainstId = archetypeId,
                Result = MatchResult.Loss,
                DatePlayed = DateTime.UtcNow,
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow.AddMinutes(15),
            };
            List<Game> games = [new Game { Result = MatchResult.Loss, Turn = 1 }];

            int affected = await _sut.SaveAsync(match, games);

            affected.ShouldBeGreaterThan(0);
            SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();
            MatchEntry? saved = await db.FindAsync<MatchEntry>(match.Id);
            saved!.Result.ShouldBe(MatchResult.Loss);
        }

        [Test]
        public async Task SaveAsync_TieResult_PersistsCorrectResult()
        {
            uint trainerId = await SeedTrainerAsync();
            uint archetypeId = await SeedArchetypeAsync();

            MatchEntry match = new()
            {
                TrainerId = trainerId,
                PlayingId = archetypeId,
                AgainstId = archetypeId,
                Result = MatchResult.Tie,
                DatePlayed = DateTime.UtcNow,
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow.AddMinutes(30),
            };
            List<Game> games = [new Game { Result = MatchResult.Tie, Turn = 1 }];

            int affected = await _sut.SaveAsync(match, games);

            affected.ShouldBeGreaterThan(0);
            SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();
            MatchEntry? saved = await db.FindAsync<MatchEntry>(match.Id);
            saved!.Result.ShouldBe(MatchResult.Tie);
        }

        [Test]
        public async Task SaveAsync_BO3Match_PersistsAllThreeGames()
        {
            uint trainerId = await SeedTrainerAsync();
            uint archetypeId = await SeedArchetypeAsync();

            MatchEntry match = new()
            {
                TrainerId = trainerId,
                PlayingId = archetypeId,
                AgainstId = archetypeId,
                Result = MatchResult.Win,
                DatePlayed = DateTime.UtcNow,
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow.AddMinutes(45),
            };
            List<Game> games =
            [
                new Game { Result = MatchResult.Tie, Turn = 1 },
                new Game { Result = MatchResult.Win, Turn = 2 },
                new Game { Result = MatchResult.Win, Turn = 1 },
            ];

            int affected = await _sut.SaveAsync(match, games);

            affected.ShouldBeGreaterThan(0);
            SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();
            MatchEntry? saved = await db.FindAsync<MatchEntry>(match.Id);
            saved.ShouldNotBeNull();
            saved!.Game1Id.ShouldNotBeNull();
            saved.Game2Id.ShouldNotBeNull();
            saved.Game3Id.ShouldNotBeNull();
        }

        [Test]
        public async Task SaveAsync_ThenGetByTrainerId_ReturnsMatch()
        {
            uint trainerId = await SeedTrainerAsync();
            uint archetypeId = await SeedArchetypeAsync();
            MatchEntry match = new()
            {
                TrainerId = trainerId,
                PlayingId = archetypeId,
                AgainstId = archetypeId,
                Result = MatchResult.Win,
                DatePlayed = DateTime.UtcNow,
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow.AddMinutes(20),
            };
            _ = await _sut.SaveAsync(match, [new Game { Result = MatchResult.Win, Turn = 1 }]);

            List<MatchEntry> matches = await _sut.GetByTrainerIdAsync(trainerId, includeRelated: false);

            matches.ShouldNotBeEmpty();
            matches[0].Result.ShouldBe(MatchResult.Win);
            matches[0].TrainerId.ShouldBe(trainerId);
        }

        [Test]
        public async Task SaveAsync_ThenDelete_RemovesMatchFromDatabase()
        {
            uint trainerId = await SeedTrainerAsync();
            uint archetypeId = await SeedArchetypeAsync();
            MatchEntry match = new()
            {
                TrainerId = trainerId,
                PlayingId = archetypeId,
                AgainstId = archetypeId,
                Result = MatchResult.Win,
                DatePlayed = DateTime.UtcNow,
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow.AddMinutes(20),
            };
            _ = await _sut.SaveAsync(match, [new Game { Result = MatchResult.Win, Turn = 1 }]);

            int deleted = await _sut.DeleteAsync(match);

            deleted.ShouldBeGreaterThan(0);
            SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();
            MatchEntry? gone = await db.FindAsync<MatchEntry>(match.Id);
            gone.ShouldBeNull();
        }

        // ---------------------------------------------------------------------------
        // GetAllAsync
        // ---------------------------------------------------------------------------

        [Test]
        public async Task GetAllAsync_EmptyDatabase_ReturnsEmptyList()
        {
            List<MatchEntry> all = await _sut.GetAllAsync();

            all.ShouldBeEmpty();
        }

        [Test]
        public async Task GetAllAsync_AfterSavingTwoMatches_ReturnsBoth()
        {
            uint trainerId = await SeedTrainerAsync();
            uint archetypeId = await SeedArchetypeAsync();
            MatchEntry match1 = new()
            {
                TrainerId = trainerId, PlayingId = archetypeId, AgainstId = archetypeId,
                Result = MatchResult.Win, DatePlayed = DateTime.UtcNow,
                StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow.AddMinutes(20),
            };
            MatchEntry match2 = new()
            {
                TrainerId = trainerId, PlayingId = archetypeId, AgainstId = archetypeId,
                Result = MatchResult.Loss, DatePlayed = DateTime.UtcNow,
                StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow.AddMinutes(15),
            };
            _ = await _sut.SaveAsync(match1, [new Game { Result = MatchResult.Win, Turn = 1 }]);
            _ = await _sut.SaveAsync(match2, [new Game { Result = MatchResult.Loss, Turn = 1 }]);

            List<MatchEntry> all = await _sut.GetAllAsync();

            all.Count.ShouldBe(2);
        }

        // ---------------------------------------------------------------------------
        // GetByIdAsync
        // ---------------------------------------------------------------------------

        [Test]
        public async Task GetByIdAsync_ExistingEntry_ReturnsMatchWithRelatedData()
        {
            uint trainerId = await SeedTrainerAsync();
            uint archetypeId = await SeedArchetypeAsync();
            MatchEntry match = new()
            {
                TrainerId = trainerId, PlayingId = archetypeId, AgainstId = archetypeId,
                Result = MatchResult.Win, DatePlayed = DateTime.UtcNow,
                StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow.AddMinutes(20),
            };
            _ = await _sut.SaveAsync(match, [new Game { Result = MatchResult.Win, Turn = 1 }]);

            MatchEntry? found = await _sut.GetByIdAsync(match.Id);

            found.ShouldNotBeNull();
            found!.Id.ShouldBe(match.Id);
            found.Result.ShouldBe(MatchResult.Win);
        }


        [Test]
        public async Task GetByIdAsync_WithIncludeRelatedFalse_StillReturnsMatch()
        {
            // GetWithChildrenAsync(id, recursive:true) loads children regardless of includeRelated.
            // includeRelated=false only skips the extra LoadRelatedDataAsync pass (archetype FKs).
            // So the match is still returned; Playing is populated via GetWithChildrenAsync.
            uint trainerId = await SeedTrainerAsync();
            uint archetypeId = await SeedArchetypeAsync();
            MatchEntry match = new()
            {
                TrainerId = trainerId, PlayingId = archetypeId, AgainstId = archetypeId,
                Result = MatchResult.Tie, DatePlayed = DateTime.UtcNow,
                StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow.AddMinutes(10),
            };
            _ = await _sut.SaveAsync(match, [new Game { Result = MatchResult.Tie, Turn = 1 }]);

            MatchEntry? found = await _sut.GetByIdAsync(match.Id, includeRelated: false);

            found.ShouldNotBeNull();
            found!.Id.ShouldBe(match.Id);
            found.Result.ShouldBe(MatchResult.Tie);
        }

        // ---------------------------------------------------------------------------
        // DeleteAsync
        // ---------------------------------------------------------------------------

        [Test]
        public async Task DeleteAsync_ExistingMatch_RemovesFromDatabase()
        {
            uint trainerId = await SeedTrainerAsync();
            uint archetypeId = await SeedArchetypeAsync();
            MatchEntry match = new()
            {
                TrainerId = trainerId, PlayingId = archetypeId, AgainstId = archetypeId,
                Result = MatchResult.Win, DatePlayed = DateTime.UtcNow,
                StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow.AddMinutes(5),
            };
            _ = await _sut.SaveAsync(match, [new Game { Result = MatchResult.Win, Turn = 1 }]);

            int deleted = await _sut.DeleteAsync(match);

            deleted.ShouldBeGreaterThan(0);
            MatchEntry? found = await _sut.GetByIdAsync(match.Id);
            found.ShouldBeNull();
        }

        [Test]
        public async Task GetByTrainerIdAsync_EmptyDatabase_ReturnsEmptyList()
        {
            List<MatchEntry> matches = await _sut.GetByTrainerIdAsync(999);
            matches.ShouldBeEmpty();
        }

        /// <summary>
        /// A genuine three-game match must come back with all three, ids intact.
        /// </summary>
        /// <remarks>
        /// Mirrors DebugDataSeeder's BO3 shape exactly, including tags on games 1 and 3 but not
        /// game 2, because that is the row ReadJournal's BO3 UI test opens.
        /// </remarks>
        [Test]
        public async Task GetByTrainerIdAsync_BO3Match_KeepsAllThreeGamesAndTheirIds()
        {
            uint trainerId = await SeedTrainerAsync();
            uint archetypeId = await SeedArchetypeAsync();
            List<Tags> tags = await _factory.Tags.GetAllAsync();
            MatchEntry match = new()
            {
                TrainerId = trainerId,
                PlayingId = archetypeId,
                AgainstId = archetypeId,
                Result = MatchResult.Loss,
            };
            _ = await _sut.SaveAsync(match,
            [
                new Game { Result = MatchResult.Loss, Turn = 2, Notes = "G1", Tags = [tags[0]] },
                new Game { Result = MatchResult.Win, Turn = 1, Notes = "G2" },
                new Game { Result = MatchResult.Loss, Turn = 2, Notes = "G3", Tags = [tags[1]] },
            ]);

            List<MatchEntry> loaded = await _sut.GetByTrainerIdAsync(trainerId, includeRelated: true);

            loaded.Count.ShouldBe(1);
            MatchEntry entry = loaded[0];
            entry.Game2Id.ShouldNotBeNull("the second game's foreign key must survive loading");
            entry.Game3Id.ShouldNotBeNull("the third game's foreign key must survive loading");
            entry.Game2.ShouldNotBeNull();
            entry.Game3.ShouldNotBeNull();
            entry.Game2!.Notes.ShouldBe("G2");
            entry.Game3!.Notes.ShouldBe("G3");
        }

        /// <summary>
        /// A best-of-one match must come back with exactly one game.
        /// </summary>
        /// <remarks>
        /// Regression for a phantom-game bug found 2026-08-05 while round-tripping an export.
        /// <c>MatchEntry</c> declares three <c>[OneToOne]</c> Game properties backed by three
        /// separate <c>[ForeignKey(typeof(Game))]</c> columns, and SQLite-Net-Extensions cannot
        /// work out which foreign key feeds which property — so <c>GetWithChildrenAsync</c>
        /// filled all three from the same row. <c>LoadRelatedDataAsync</c> only assigned the
        /// slots that had an id, leaving the phantoms in place for every BO1 match loaded with
        /// related data. ReadJournal renders game details from exactly this call.
        /// </remarks>
        [Test]
        public async Task GetByTrainerIdAsync_BO1Match_HasNoPhantomSecondOrThirdGame()
        {
            uint trainerId = await SeedTrainerAsync();
            uint archetypeId = await SeedArchetypeAsync();
            MatchEntry match = new()
            {
                TrainerId = trainerId,
                PlayingId = archetypeId,
                AgainstId = archetypeId,
                Result = MatchResult.Win,
            };
            _ = await _sut.SaveAsync(match, [new Game { Result = MatchResult.Win, Turn = 1 }]);

            List<MatchEntry> loaded = await _sut.GetByTrainerIdAsync(trainerId, includeRelated: true);

            loaded.Count.ShouldBe(1);
            loaded[0].Game1.ShouldNotBeNull();
            loaded[0].Game2.ShouldBeNull("a best-of-one match has no second game");
            loaded[0].Game3.ShouldBeNull("a best-of-one match has no third game");
        }

        [Test]
        public async Task SaveAsync_ZeroTrainerId_ThrowsArgumentException()
        {
            MatchEntry match = new()
            {
                TrainerId = 0, PlayingId = 1, AgainstId = 1,
                Result = MatchResult.Win, DatePlayed = DateTime.UtcNow,
                StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow.AddMinutes(5),
            };
            await Should.ThrowAsync<ArgumentException>(() =>
                _sut.SaveAsync(match, [new Game { Result = MatchResult.Win, Turn = 1 }]));
        }

        // ---------------------------------------------------------------------------
        // Inner factory — overrides GetDbPath() to use an isolated in-memory database
        // ---------------------------------------------------------------------------

        private sealed class InMemorySqliteConnectionFactory : SqliteConnectionFactory
        {
            private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"pbj_test_{Guid.NewGuid():N}.db3");

            public InMemorySqliteConnectionFactory()
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
