using PokemonBattleJournal.IntegrationTests.Infrastructure;

namespace PokemonBattleJournal.IntegrationTests.Services;

/// <summary>
/// How a scraped deck name becomes a local sprite file, and how the second icon gets stored.
/// </summary>
/// <remarks>
/// ArchetypeOperations had the highest survivor count in Core — 37 mutants that RUN and that
/// nothing asserts on. The interesting ones are here: the name-to-filename fallback used when a
/// CDN url resolves to nothing, and the dual-icon branches.
///
/// The naming rule matters beyond this file. MAUI asset names must use underscores while the
/// CDN uses hyphens, so a mutation to the whitespace-to-underscore replace produces a filename
/// that silently resolves to no image — see project_sprite_naming_constraint. Nothing was
/// pinning it.
///
/// Driven through GetAllAsync rather than the private helper directly: that exercises the
/// upsert branches at the same time, and testing a private method would mean widening its
/// accessibility for the benefit of a test.
/// </remarks>
public class ArchetypeSpriteResolutionTests
{
    private TestSqliteConnectionFactory _factory = null!;
    private ILimitlessMetaService _meta = null!;

    [SetUp]
    public void SetUp()
    {
        _meta = Substitute.For<ILimitlessMetaService>();
        _meta.GetTopDecksAsync(Arg.Any<int>()).Returns([]);
        _factory = new TestSqliteConnectionFactory(_meta);
    }

    [TearDown]
    public async Task TearDown() => await _factory.DisposeAsync();

    /// <summary>Empty ImageUrl forces the name-based fallback rather than URL resolution.</summary>
    private void MetaReturns(params MetaDeck[] decks) =>
        _meta.GetTopDecksAsync(Arg.Any<int>()).Returns([.. decks]);

    private async Task<Archetype?> ArchetypeNamedAsync(string name) =>
        (await _factory.Archetypes.GetAllAsync()).FirstOrDefault(a => a.Name == name);

    [Test]
    public async Task GetAllAsync_ASpacedName_BecomesAnUnderscoredFilename()
    {
        // The constraint that makes this worth a test: MAUI asset names use underscores, the CDN
        // uses hyphens. Mutating the replacement produces a filename that resolves to no image
        // and shows a blank icon rather than throwing.
        MetaReturns(new MetaDeck("Raging Bolt", string.Empty));

        (await ArchetypeNamedAsync("Raging Bolt"))!.ImagePath.ShouldBe("raging_bolt.png");
    }

    [Test]
    public async Task GetAllAsync_ATrailingCardSuffix_IsStrippedFromTheFilename()
    {
        // "Dragapult ex" and "Dragapult" are the same Pokemon and share one sprite.
        MetaReturns(new MetaDeck("Dragapult ex", string.Empty));

        (await ArchetypeNamedAsync("Dragapult ex"))!.ImagePath.ShouldBe("dragapult.png");
    }

    [Test]
    public async Task GetAllAsync_SuffixStrippingIgnoresCase()
    {
        // The input has to be a casing the alternation does NOT list. It spells out ex|EX and
        // tera|Tera, so those strip with or without IgnoreCase and prove nothing — an earlier
        // version of this test used them and survived removing the flag entirely. "gx" is
        // listed only as GX, so it is stripped by the flag alone.
        MetaReturns(new MetaDeck("Pikachu gx", string.Empty), new MetaDeck("Mewtwo vstar", string.Empty));

        (await ArchetypeNamedAsync("Pikachu gx"))!.ImagePath.ShouldBe("pikachu.png");
        (await ArchetypeNamedAsync("Mewtwo vstar"))!.ImagePath.ShouldBe("mewtwo.png");
    }

    [Test]
    public async Task GetAllAsync_BackfillsASecondIconOntoARowThatPredatesIt()
    {
        // The branch a fresh row cannot reach: the INSERT already writes ImagePath2, so the
        // backfill UPDATE only matters for a row stored before dual icons existed — an app
        // upgrade, or a TrainerHill import that created the archetype by name alone.
        SQLiteAsyncConnection db = await _factory.GetDatabaseAsync();
        _ = await db.InsertAsync(new Archetype
        {
            Name = "Dragapult ex / Dusknoir",
            ImagePath = "dragapult.png",
            ImagePath2 = null,
        });

        MetaReturns(new MetaDeck("Dragapult ex / Dusknoir", string.Empty, "https://example.invalid/dusknoir.png"));

        Archetype archetype = (await ArchetypeNamedAsync("Dragapult ex / Dusknoir"))!;
        archetype.ImagePath2.ShouldNotBeNullOrEmpty(
            "an existing row must gain the second icon rather than keep a blank one forever");
    }

    [Test]
    public async Task GetAllAsync_AMultiPokemonName_UsesTheFirstOne()
    {
        // Split on & and /, first part wins — the primary icon is the deck's headline Pokemon.
        MetaReturns(new MetaDeck("Dragapult ex / Dusknoir", string.Empty));

        (await ArchetypeNamedAsync("Dragapult ex / Dusknoir"))!.ImagePath.ShouldBe("dragapult.png");
    }

    [Test]
    public async Task GetAllAsync_ASecondaryImage_IsStoredAsTheSecondIcon()
    {
        // The dual-icon branch. Without a secondary url the whole path is skipped, which is why
        // it survived every mutation.
        MetaReturns(new MetaDeck("Dragapult ex / Dusknoir", string.Empty, "https://example.invalid/dusknoir.png"));

        Archetype archetype = (await ArchetypeNamedAsync("Dragapult ex / Dusknoir"))!;
        archetype.ImagePath.ShouldBe("dragapult.png");
        archetype.ImagePath2.ShouldNotBeNullOrEmpty("a deck with two icons must keep the second");
    }

    [Test]
    public async Task GetAllAsync_NoSecondaryImage_LeavesTheSecondIconEmpty()
    {
        // The other side of the same branch: a single-Pokemon deck must not invent one.
        MetaReturns(new MetaDeck("Miraidon", string.Empty));

        (await ArchetypeNamedAsync("Miraidon"))!.ImagePath2.ShouldBeNullOrEmpty();
    }

    [Test]
    public async Task GetAllAsync_OfflineWithAnEmptyDatabase_SeedsUsableDefaults()
    {
        // The fallback a user actually meets on a first launch with no network. Asserting the
        // whole hardcoded list would be a change-detector test, so this pins the two properties
        // that matter: the catch-all exists, and the dual-icon default survives seeding.
        _meta.GetTopDecksAsync(Arg.Any<int>()).Returns([]);

        List<Archetype> all = await _factory.Archetypes.GetAllAsync();

        all.Count.ShouldBeGreaterThan(1, "an offline first launch must still offer decks to pick");
        all.ShouldContain(a => a.Name == "Other", "the catch-all is what unknown decks fall back to");
        Archetype dragapult = all.First(a => a.Name.StartsWith("Dragapult", StringComparison.Ordinal));
        dragapult.ImagePath2.ShouldNotBeNullOrEmpty("the seeded dual-icon deck must keep both icons");
    }

    [Test]
    public async Task GetAllAsync_AlwaysProvidesTheOtherCatchAll()
    {
        // Even when the scrape succeeds and returns decks that do not include it.
        MetaReturns(new MetaDeck("Miraidon", string.Empty));

        (await _factory.Archetypes.GetAllAsync())
            .ShouldContain(a => a.Name == "Other");
    }
}
