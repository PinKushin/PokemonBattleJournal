using System.Text;
using Microsoft.Extensions.Logging;
using PokemonBattleJournal.Scraper.Interfaces;
using PokemonBattleJournal.Scraper.Models;
using PokemonBattleJournal.Services.Import;
using SQLite;

namespace PokemonBattleJournal.Tests.Services
{
    public class TrainerHillImportServiceIntegrationTests
    {
        private InMemorySqliteConnectionFactory _factory = null!;
        private TrainerHillImportService _sut = null!;
        private uint _trainerId;

        [SetUp]
        public async Task SetUp()
        {
            _factory = new InMemorySqliteConnectionFactory();

            ILimitlessMetaService emptyLimitless = Substitute.For<ILimitlessMetaService>();
            emptyLimitless.GetTopDecksAsync(Arg.Any<int>()).Returns([]);

            _sut = new TrainerHillImportService(
                _factory,
                Substitute.For<ILogger<TrainerHillImportService>>(),
                emptyLimitless);
            _ = await _factory.GetDatabaseAsync();

            SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();
            Trainer trainer = new() { Name = "ImportTestTrainer", IsActive = true };
            _ = await db.InsertAsync(trainer);
            _trainerId = (uint)trainer.Id;
        }

        [TearDown]
        public async Task TearDown() => await _factory.CloseAndDeleteAsync();

        private static Stream ToStream(string json) =>
            new MemoryStream(Encoding.UTF8.GetBytes(json));

        // ---------------------------------------------------------------------------
        // Happy paths
        // ---------------------------------------------------------------------------

        [Test]
        public async Task ImportAsync_BO1Entry_ImportsOneMatch()
        {
            const string json = """
                [{"playing":"other","against":"other","time":"2026-07-18 19:08:53","result":"Win",
                  "game1":{"result":"Win","turn":1,"tags":[],"notes":null}}]
                """;

            (int imported, List<string> errors) = await _sut.ImportAsync(ToStream(json), _trainerId);

            imported.ShouldBe(1);
            errors.ShouldBeEmpty();
            SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();
            (await db.Table<MatchEntry>().CountAsync()).ShouldBe(1);
        }

        [Test]
        public async Task ImportAsync_BO3Entry_ImportsWithThreeGames()
        {
            const string json = """
                [{"playing":"other","against":"other","time":"2026-07-27 19:45:24","result":"Tie",
                  "game1":{"result":"Win","turn":"2","tags":[],"notes":null},
                  "game2":{"result":"Loss","turn":"1","tags":[],"notes":null},
                  "game3":{"result":"Tie","turn":1,"tags":[],"notes":null}}]
                """;

            (int imported, List<string> errors) = await _sut.ImportAsync(ToStream(json), _trainerId);

            imported.ShouldBe(1);
            errors.ShouldBeEmpty();
            SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();
            (await db.Table<Game>().CountAsync()).ShouldBe(3);
        }

        [Test]
        public async Task ImportAsync_NewSlug_CreatesArchetypes()
        {
            const string json = """
                [{"playing":"dragapult-dusknoir","against":"festival-lead","time":"2026-07-27 19:45:24","result":"Win",
                  "game1":{"result":"Win","turn":1,"tags":[],"notes":null}}]
                """;

            await _sut.ImportAsync(ToStream(json), _trainerId);

            SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();
            (await db.Table<Archetype>().Where(a => a.Name == "Dragapult Dusknoir").CountAsync()).ShouldBe(1);
            (await db.Table<Archetype>().Where(a => a.Name == "Festival Lead").CountAsync()).ShouldBe(1);
        }

        [Test]
        public async Task ImportAsync_GameWithTags_CreatesTags()
        {
            const string json = """
                [{"playing":"other","against":"other","time":"2026-07-18 19:08:53","result":"Win",
                  "game1":{"result":"Win","turn":1,"tags":["Lucky","Slow start"],"notes":null}}]
                """;

            await _sut.ImportAsync(ToStream(json), _trainerId);

            SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();
            (await db.Table<Tags>().Where(t => t.Name == "Lucky").CountAsync()).ShouldBe(1);
            (await db.Table<Tags>().Where(t => t.Name == "Slow start").CountAsync()).ShouldBe(1);
        }

        [Test]
        public async Task ImportAsync_SameTagInTwoEntries_NotDuplicated()
        {
            const string json = """
                [
                  {"playing":"other","against":"other","time":"2026-07-18 19:08:53","result":"Win",
                   "game1":{"result":"Win","turn":1,"tags":["Lucky"],"notes":null}},
                  {"playing":"other","against":"other","time":"2026-07-19 10:00:00","result":"Loss",
                   "game1":{"result":"Loss","turn":2,"tags":["Lucky"],"notes":null}}
                ]
                """;

            await _sut.ImportAsync(ToStream(json), _trainerId);

            SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();
            (await db.Table<Tags>().Where(t => t.Name == "Lucky").CountAsync()).ShouldBe(1);
        }

        [Test]
        public async Task ImportAsync_StringTurn_ParsedCorrectly()
        {
            const string json = """
                [{"playing":"other","against":"other","time":"2026-07-18 19:08:53","result":"Win",
                  "game1":{"result":"Win","turn":"2","tags":[],"notes":null}}]
                """;

            await _sut.ImportAsync(ToStream(json), _trainerId);

            SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();
            Game? game = await db.Table<Game>().FirstOrDefaultAsync();
            game.ShouldNotBeNull();
            game!.Turn.ShouldBe(2u);
        }

        [Test]
        public async Task ImportAsync_MultipleEntries_AllImported()
        {
            const string json = """
                [
                  {"playing":"other","against":"other","time":"2026-07-18 19:08:53","result":"Win",
                   "game1":{"result":"Win","turn":1,"tags":[],"notes":null}},
                  {"playing":"other","against":"other","time":"2026-07-19 10:00:00","result":"Loss",
                   "game1":{"result":"Loss","turn":2,"tags":[],"notes":null}}
                ]
                """;

            (int imported, List<string> errors) = await _sut.ImportAsync(ToStream(json), _trainerId);

            imported.ShouldBe(2);
            errors.ShouldBeEmpty();
        }

        // ---------------------------------------------------------------------------
        // Error paths
        // ---------------------------------------------------------------------------

        [Test]
        public async Task ImportAsync_InvalidJson_ReturnsError()
        {
            (int imported, List<string> errors) = await _sut.ImportAsync(ToStream("not json at all"), _trainerId);

            imported.ShouldBe(0);
            errors.ShouldNotBeEmpty();
        }

        [Test]
        public async Task ImportAsync_UnknownResult_SkipsEntry()
        {
            const string json = """
                [{"playing":"other","against":"other","time":"2026-07-18 19:08:53","result":"Draw",
                  "game1":{"result":"Win","turn":1,"tags":[],"notes":null}}]
                """;

            (int imported, List<string> errors) = await _sut.ImportAsync(ToStream(json), _trainerId);

            imported.ShouldBe(0);
            errors.Count.ShouldBeGreaterThan(0);
        }

        [Test]
        public async Task ImportAsync_MissingGame1_SkipsEntry()
        {
            const string json = """
                [{"playing":"other","against":"other","time":"2026-07-18 19:08:53","result":"Win"}]
                """;

            (int imported, List<string> errors) = await _sut.ImportAsync(ToStream(json), _trainerId);

            imported.ShouldBe(0);
            errors.ShouldNotBeEmpty();
        }

        [Test]
        public async Task ImportAsync_OneValidOneBad_ImportedCountIsOne()
        {
            const string json = """
                [
                  {"playing":"other","against":"other","time":"2026-07-18 19:08:53","result":"Win",
                   "game1":{"result":"Win","turn":1,"tags":[],"notes":null}},
                  {"playing":"other","against":"other","time":"2026-07-19 10:00:00","result":"Bogus",
                   "game1":{"result":"Win","turn":1,"tags":[],"notes":null}}
                ]
                """;

            (int imported, List<string> errors) = await _sut.ImportAsync(ToStream(json), _trainerId);

            imported.ShouldBe(1);
            errors.ShouldNotBeEmpty();
        }

        [Test]
        public async Task ImportAsync_SlugMatchesLimitlessDeck_UsesLimitlessName()
        {
            ILimitlessMetaService metaService = Substitute.For<ILimitlessMetaService>();
            metaService.GetTopDecksAsync(Arg.Any<int>())
                .Returns([new MetaDeck("N's Zoroark", "some-url")]);
            TrainerHillImportService sut = new(
                _factory,
                Substitute.For<ILogger<TrainerHillImportService>>(),
                metaService);

            const string json = """
                [{"playing":"n-zoroark","against":"other","time":"2026-07-18 19:08:53","result":"Win",
                  "game1":{"result":"Win","turn":1,"tags":[],"notes":null}}]
                """;

            await sut.ImportAsync(ToStream(json), _trainerId);

            SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();
            (await db.Table<Archetype>().Where(a => a.Name == "N's Zoroark").CountAsync()).ShouldBe(1);
            (await db.Table<Archetype>().Where(a => a.Name == "N Zoroark").CountAsync()).ShouldBe(0);
        }

        // ---------------------------------------------------------------------------
        // Factory
        // ---------------------------------------------------------------------------

        private sealed class InMemorySqliteConnectionFactory : SqliteConnectionFactory
        {
            private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"pbj_test_{Guid.NewGuid():N}.db3");

            public InMemorySqliteConnectionFactory()
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
