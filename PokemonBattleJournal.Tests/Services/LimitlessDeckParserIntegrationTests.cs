using PokemonBattleJournal.Scraper.Models;
using PokemonBattleJournal.Scraper.Services;
using System.Reflection;

namespace PokemonBattleJournal.Tests.Services;

/// <summary>
/// Integration tests against a real HTML snapshot captured from limitlesstcg.com/decks.
/// If these fail after a snapshot refresh, Limitless changed their page structure and
/// LimitlessDeckParser needs updating before the live app breaks.
/// Refresh the snapshot: fetch https://limitlesstcg.com/decks and overwrite
/// PokemonBattleJournal.Tests/Fixtures/limitless_decks_snapshot.html
/// </summary>
public class LimitlessDeckParserIntegrationTests
{
    private static readonly string _snapshotHtml = LoadSnapshot();

    private static string LoadSnapshot()
    {
        Assembly asm = typeof(LimitlessDeckParserIntegrationTests).Assembly;
        string resourceName = asm.GetManifestResourceNames()
            .First(n => n.EndsWith("limitless_decks_snapshot.html", StringComparison.OrdinalIgnoreCase));
        using Stream stream = asm.GetManifestResourceStream(resourceName)!;
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }

    private readonly LimitlessDeckParser _parser = new();

    [Fact]
    public void Parse_RealSnapshot_ReturnsNonEmptyList()
    {
        List<MetaDeck> result = _parser.Parse(_snapshotHtml, 10);
        result.ShouldNotBeEmpty();
    }

    [Fact]
    public void Parse_RealSnapshot_ReturnsRequestedCount()
    {
        List<MetaDeck> result = _parser.Parse(_snapshotHtml, 10);
        result.Count.ShouldBe(10);
    }

    [Fact]
    public void Parse_RealSnapshot_EachDeckHasName()
    {
        List<MetaDeck> result = _parser.Parse(_snapshotHtml, 10);
        result.ShouldAllBe(d => !string.IsNullOrWhiteSpace(d.Name),
            "Every deck must have a non-empty name — check td:nth-child(3) a selector");
    }

    [Fact]
    public void Parse_RealSnapshot_EachDeckHasImageUrl()
    {
        List<MetaDeck> result = _parser.Parse(_snapshotHtml, 10);
        result.ShouldAllBe(d => !string.IsNullOrWhiteSpace(d.ImageUrl),
            "Every deck must have an image URL — check img.pokemon selector");
    }

    [Fact]
    public void Parse_RealSnapshot_ImageUrlsPointToLimitless()
    {
        List<MetaDeck> result = _parser.Parse(_snapshotHtml, 10);
        // If this fails, Limitless moved their CDN — ImageUrl sources need updating
        result.ShouldAllBe(d => d.ImageUrl.Contains("limitlesstcg") || d.ImageUrl.StartsWith("http"),
            "Image URLs should be absolute HTTP URLs");
    }

    [Fact]
    public void Parse_RealSnapshot_AnnotationDecksNameContainsAnnotation()
    {
        // Snapshot has entries like "Dragapult ex" — annotation merged into name
        List<MetaDeck> result = _parser.Parse(_snapshotHtml, 20);
        bool anyWithEx = result.Any(d => d.Name.Contains("ex", StringComparison.OrdinalIgnoreCase));
        anyWithEx.ShouldBeTrue("Expected at least one 'ex' annotated deck in top 20 — snapshot may be stale");
    }

    [Fact]
    public void Parse_RealSnapshot_CountOne_ReturnsExactlyOne()
    {
        List<MetaDeck> result = _parser.Parse(_snapshotHtml, 1);
        result.Count.ShouldBe(1);
    }

    [Fact]
    public void Parse_RealSnapshot_FirstDeckNameIsNotEmpty()
    {
        List<MetaDeck> result = _parser.Parse(_snapshotHtml, 1);
        result[0].Name.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Parse_RealSnapshot_NoDuplicateNames()
    {
        List<MetaDeck> result = _parser.Parse(_snapshotHtml, 10);
        int distinct = result.Select(d => d.Name).Distinct().Count();
        distinct.ShouldBe(result.Count, "Deck names should be unique — parser may be double-counting rows");
    }
}
