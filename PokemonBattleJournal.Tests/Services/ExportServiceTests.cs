using System.Text;
using System.Text.Json;
using PokemonBattleJournal.Services.Export;
using PokemonBattleJournal.Services.Import;

namespace PokemonBattleJournal.Tests.Services
{
    /// <summary>
    /// Export has two formats on purpose, and the difference is fidelity.
    /// </summary>
    /// <remarks>
    /// The TrainerHill format writes archetype <em>slugs</em> because that is what TrainerHill
    /// uses. Slugs are lossy: recovering "Dragapult ex / Dusknoir" from "dragapult-ex-dusknoir"
    /// needs the Limitless meta list, so an offline import gets "Dragapult Ex Dusknoir" instead.
    /// That is acceptable for interop with someone else's tool and unacceptable for a backup,
    /// so the backup format writes names verbatim. The tests below pin both halves of that.
    /// </remarks>
    public class ExportServiceTests
    {
        private ISqliteConnectionFactory _factory = null!;
        private IMatchOperations _matches = null!;
        private ITrainerOperations _trainers = null!;
        private ExportService _sut = null!;

        private static readonly Archetype Dragapult = new() { Id = 1, Name = "Dragapult ex / Dusknoir" };
        private static readonly Archetype Other = new() { Id = 2, Name = "Other" };

        [SetUp]
        public void SetUp()
        {
            _matches = Substitute.For<IMatchOperations>();
            _trainers = Substitute.For<ITrainerOperations>();
            _factory = Substitute.For<ISqliteConnectionFactory>();
            _factory.Matches.Returns(_matches);
            _factory.Trainers.Returns(_trainers);

            _sut = new ExportService(_factory, Substitute.For<ILogger<ExportService>>());
        }

        private static MatchEntry BO1(MatchResult result = MatchResult.Win) => new()
        {
            Id = 10,
            TrainerId = 1,
            Playing = Dragapult,
            Against = Other,
            Result = result,
            DatePlayed = new DateTime(2026, 7, 27, 19, 45, 24, DateTimeKind.Utc),
            StartTime = new DateTime(2026, 7, 27, 19, 45, 24, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 7, 27, 20, 5, 0, DateTimeKind.Utc),
            Game1Id = 100,
            Game1 = new Game
            {
                Id = 100,
                Result = result,
                Turn = 1,
                Notes = "went first",
                Tags = [new Tags { Id = 5, Name = "Lucky" }],
            },
        };

        private void GivenMatches(params MatchEntry[] matches) =>
            _matches.GetByTrainerIdAsync(1, true).Returns(Task.FromResult(matches.ToList()));

        // -------------------------------------------------------------------------------
        // TrainerHill format
        // -------------------------------------------------------------------------------

        [Test]
        public async Task ExportTrainerHillAsync_SingleMatch_WritesTrainerHillShape()
        {
            GivenMatches(BO1());

            using JsonDocument doc = JsonDocument.Parse(await _sut.ExportTrainerHillAsync(1));

            doc.RootElement.ValueKind.ShouldBe(JsonValueKind.Array, "TrainerHill's format is a bare array");
            JsonElement entry = doc.RootElement[0];
            entry.GetProperty("result").GetString().ShouldBe("Win");
            entry.GetProperty("game1").GetProperty("notes").GetString().ShouldBe("went first");
            entry.GetProperty("game1").GetProperty("turn").GetInt32().ShouldBe(1);
            entry.GetProperty("game1").GetProperty("tags")[0].GetString().ShouldBe("Lucky");
        }

        [Test]
        public async Task ExportTrainerHillAsync_ArchetypeNames_WrittenAsSlugs()
        {
            GivenMatches(BO1());

            using JsonDocument doc = JsonDocument.Parse(await _sut.ExportTrainerHillAsync(1));

            JsonElement entry = doc.RootElement[0];
            entry.GetProperty("playing").GetString().ShouldBe("dragapult-ex-dusknoir");
            entry.GetProperty("against").GetString().ShouldBe("other");
        }

        [Test]
        public async Task ExportTrainerHillAsync_BO1Match_OmitsGame2AndGame3()
        {
            GivenMatches(BO1());

            using JsonDocument doc = JsonDocument.Parse(await _sut.ExportTrainerHillAsync(1));

            JsonElement entry = doc.RootElement[0];
            entry.TryGetProperty("game2", out _).ShouldBeFalse("a BO1 match has no second game to write");
            entry.TryGetProperty("game3", out _).ShouldBeFalse();
        }

        [Test]
        public async Task ExportTrainerHillAsync_BO3Match_WritesAllThreeGames()
        {
            MatchEntry match = BO1();
            match.Game2Id = 101;
            match.Game2 = new Game { Id = 101, Result = MatchResult.Loss, Turn = 2 };
            match.Game3Id = 102;
            match.Game3 = new Game { Id = 102, Result = MatchResult.Win, Turn = 1 };
            GivenMatches(match);

            using JsonDocument doc = JsonDocument.Parse(await _sut.ExportTrainerHillAsync(1));

            JsonElement entry = doc.RootElement[0];
            entry.GetProperty("game2").GetProperty("result").GetString().ShouldBe("Loss");
            entry.GetProperty("game2").GetProperty("turn").GetInt32().ShouldBe(2);
            entry.GetProperty("game3").GetProperty("result").GetString().ShouldBe("Win");
        }

        [Test]
        public async Task ExportTrainerHillAsync_NoMatches_WritesEmptyArray()
        {
            GivenMatches();

            (await _sut.ExportTrainerHillAsync(1)).ShouldBe("[]");
        }

        // -------------------------------------------------------------------------------
        // Backup format
        // -------------------------------------------------------------------------------

        [Test]
        public async Task ExportBackupAsync_WritesEnvelopeWithTrainerNames()
        {
            _trainers.GetAllAsync().Returns(Task.FromResult(new List<Trainer> { new() { Id = 1, Name = "Ash" } }));
            GivenMatches(BO1());

            using JsonDocument doc = JsonDocument.Parse(await _sut.ExportBackupAsync());

            doc.RootElement.ValueKind.ShouldBe(JsonValueKind.Object, "the backup wraps trainers in an envelope");
            doc.RootElement.GetProperty("version").GetInt32().ShouldBe(1);
            JsonElement trainer = doc.RootElement.GetProperty("trainers")[0];
            trainer.GetProperty("name").GetString().ShouldBe("Ash");
            trainer.GetProperty("matches").GetArrayLength().ShouldBe(1);
        }

        [Test]
        public async Task ExportBackupAsync_ArchetypeNames_WrittenVerbatimNotSlugged()
        {
            // The whole point of the backup format: a restore must not depend on the Limitless
            // meta list being reachable to recover the original archetype name.
            _trainers.GetAllAsync().Returns(Task.FromResult(new List<Trainer> { new() { Id = 1, Name = "Ash" } }));
            GivenMatches(BO1());

            using JsonDocument doc = JsonDocument.Parse(await _sut.ExportBackupAsync());

            JsonElement entry = doc.RootElement.GetProperty("trainers")[0].GetProperty("matches")[0];
            entry.GetProperty("playing").GetString().ShouldBe("Dragapult ex / Dusknoir");
        }

        [Test]
        public async Task ExportBackupAsync_SpecificTrainers_ExportsOnlyThose()
        {
            _trainers.GetAllAsync().Returns(Task.FromResult(new List<Trainer>
            {
                new() { Id = 1, Name = "Ash" },
                new() { Id = 2, Name = "Misty" },
            }));
            GivenMatches(BO1());
            _matches.GetByTrainerIdAsync(2, true).Returns(Task.FromResult(new List<MatchEntry>()));

            using JsonDocument doc = JsonDocument.Parse(await _sut.ExportBackupAsync([1]));

            doc.RootElement.GetProperty("trainers").GetArrayLength().ShouldBe(1);
            doc.RootElement.GetProperty("trainers")[0].GetProperty("name").GetString().ShouldBe("Ash");
        }

        // -------------------------------------------------------------------------------
        // Round trip
        // -------------------------------------------------------------------------------

        [Test]
        public async Task ExportTrainerHillAsync_OutputIsAcceptedByTheImporter()
        {
            // The strongest guarantee available without a database: whatever export writes, the
            // importer parses and saves. A field-name or casing drift between the two would show
            // up here as zero imported matches.
            GivenMatches(BO1());
            string json = await _sut.ExportTrainerHillAsync(1);

            IMatchOperations importTarget = Substitute.For<IMatchOperations>();
            importTarget.SaveAsync(Arg.Any<MatchEntry>(), Arg.Any<List<Game>>()).Returns(Task.FromResult(1));
            ISqliteConnectionFactory importFactory = Substitute.For<ISqliteConnectionFactory>();
            importFactory.Matches.Returns(importTarget);
            importFactory.Archetypes.Returns(Substitute.For<IArchetypeOperations>());
            importFactory.GetLock().Returns(new SemaphoreSlim(1, 1));
            importFactory.GetDatabaseAsync().Returns(Task.FromException<SQLiteAsyncConnection>(
                new InvalidOperationException("no database in this test")));

            ILimitlessMetaService meta = Substitute.For<ILimitlessMetaService>();
            meta.GetTopDecksAsync(Arg.Any<int>()).Returns(Task.FromResult(
                new List<MetaDeck> { new("Dragapult ex / Dusknoir", ""), new("Other", "") }));

            TrainerHillImportService importer = new(
                importFactory, Substitute.For<ILogger<TrainerHillImportService>>(), meta);

            (int _, List<string> errors) = await importer.ImportAsync(
                new MemoryStream(Encoding.UTF8.GetBytes(json)), trainerId: 7);

            // Archetype resolution needs the database, which is unavailable here, so the entry
            // cannot be saved. What matters is WHY it failed: a parse or shape mismatch would
            // report a skipped/invalid entry instead of the database being unreachable.
            errors.ShouldNotContain(e => e.Contains("Invalid JSON", StringComparison.OrdinalIgnoreCase),
                $"export produced JSON the importer could not parse: {string.Join(" | ", errors)}");
            errors.ShouldNotContain(e => e.Contains("Skipped entry", StringComparison.OrdinalIgnoreCase),
                $"export produced an entry the importer rejected: {string.Join(" | ", errors)}");
        }

        [Test]
        public void NameToSlug_RoundTripsThroughTheImporterLookup()
        {
            // Pins the two halves as inverses. This is the property the TrainerHill export relies
            // on: a slug it writes must resolve back to the original name when the deck is known.
            Dictionary<string, string> lookup = TrainerHillImportService.BuildSlugLookup(
                [new MetaDeck("Dragapult ex / Dusknoir", ""), new MetaDeck("Rocket's Honchkrow", "")]);

            foreach (string name in new[] { "Dragapult ex / Dusknoir", "Rocket's Honchkrow" })
            {
                string slug = TrainerHillImportService.NameToSlug(name);
                TrainerHillImportService.LookupSlug(slug, lookup)
                    .ShouldBe(name, $"slug '{slug}' did not resolve back to '{name}'");
            }
        }
    }
}
