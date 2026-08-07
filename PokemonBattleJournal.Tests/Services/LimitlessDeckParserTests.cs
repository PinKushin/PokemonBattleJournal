using PokemonBattleJournal.Scraper.Services;

namespace PokemonBattleJournal.Tests.Services;

public class LimitlessDeckParserTests
{
    private static readonly LimitlessDeckParser _parser = new();

    private static string BuildHtml(int rowCount) =>
        "<table><tbody>" +
        string.Concat(Enumerable.Range(1, rowCount).Select(i =>
            $"<tr>" +
            $"<td>{i}</td>" +
            $"<td><img class=\"pokemon\" src=\"https://r2.limitlesstcg.net/pokemon/gen9/deck{i}.png\" alt=\"deck{i}\"></td>" +
            $"<td><a href=\"/decks/{i}\">Deck {i} <span class=\"annotation\">ex</span></a></td>" +
            $"<td>{i * 100}</td>" +
            $"<td>{i}.00%</td>" +
            $"</tr>")) +
        "</tbody></table>";

    private static string DragapultHtml =>
        "<table><tbody>" +
        "<tr>" +
        "<td>1</td>" +
        "<td><img class=\"pokemon\" src=\"https://r2.limitlesstcg.net/pokemon/gen9/dragapult.png\" alt=\"dragapult\"></td>" +
        "<td><a href=\"/decks/284\">Dragapult <span class=\"annotation\">ex</span></a></td>" +
        "<td>2227</td><td>49.22%</td>" +
        "</tr>" +
        "<tr>" +
        "<td>2</td>" +
        "<td><img class=\"pokemon\" src=\"https://r2.limitlesstcg.net/pokemon/gen9/zoroark.png\" alt=\"zoroark\"></td>" +
        "<td><a href=\"/decks/300\">N's Zoroark <span class=\"annotation\">ex</span></a></td>" +
        "<td>363</td><td>8.02%</td>" +
        "</tr>" +
        "</tbody></table>";

    [Test]
    public void Parse_ValidHtml_ReturnsRequestedCount()
    {
        string html = BuildHtml(12);
        List<MetaDeck> result = _parser.Parse(html, 10);
        result.Count.ShouldBe(10);
    }

    [Test]
    public void Parse_ValidHtml_ExtractsDeckName()
    {
        List<MetaDeck> result = _parser.Parse(DragapultHtml, 10);
        result[0].Name.ShouldBe("Dragapult ex");
    }

    [Test]
    public void Parse_ValidHtml_ExtractsImageUrl()
    {
        List<MetaDeck> result = _parser.Parse(DragapultHtml, 10);
        result[0].ImageUrl.ShouldContain("dragapult");
    }

    [Test]
    public void Parse_EmptyTable_ReturnsEmptyList()
    {
        string html = "<table><tbody></tbody></table>";
        List<MetaDeck> result = _parser.Parse(html, 10);
        result.ShouldBeEmpty();
    }

    [Test]
    public void Parse_CountGreaterThanRows_ReturnsAllRows()
    {
        string html = BuildHtml(3);
        List<MetaDeck> result = _parser.Parse(html, 10);
        result.Count.ShouldBe(3);
    }

    [Test]
    public void Parse_EmptyHtml_ReturnsEmptyList()
    {
        List<MetaDeck> result = _parser.Parse(string.Empty, 10);
        result.ShouldBeEmpty();
    }

    [Test]
    public void Parse_WhitespaceOnlyHtml_ReturnsEmptyList()
    {
        List<MetaDeck> result = _parser.Parse("   \t\n  ", 10);
        result.ShouldBeEmpty();
    }

    [Test]
    public void Parse_RowMissingAnchor_SkipsRow()
    {
        string html =
            "<table><tbody>" +
            "<tr><td>1</td><td><img class=\"pokemon\" src=\"img.png\"></td><td>no link here</td></tr>" +
            "</tbody></table>";
        List<MetaDeck> result = _parser.Parse(html, 10);
        result.ShouldBeEmpty();
    }

    [Test]
    public void Parse_RowMissingImage_SkipsRow()
    {
        string html =
            "<table><tbody>" +
            "<tr><td>1</td><td></td><td><a href=\"/decks/1\">Charizard <span class=\"annotation\">ex</span></a></td></tr>" +
            "</tbody></table>";
        List<MetaDeck> result = _parser.Parse(html, 10);
        result.ShouldBeEmpty();
    }

    [Test]
    public void Parse_AnchorWithNoAnnotation_UsesFullText()
    {
        string html =
            "<table><tbody>" +
            "<tr>" +
            "<td>1</td>" +
            "<td><img class=\"pokemon\" src=\"snorlax.png\"></td>" +
            "<td><a href=\"/decks/1\">Snorlax Stall</a></td>" +
            "</tr>" +
            "</tbody></table>";
        List<MetaDeck> result = _parser.Parse(html, 10);
        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("Snorlax Stall");
    }

    [Test]
    public void Parse_ImageWithNoSrc_SkipsRow()
    {
        string html =
            "<table><tbody>" +
            "<tr><td>1</td><td><img class=\"pokemon\"></td><td><a href=\"/decks/1\">Dragapult <span class=\"annotation\">ex</span></a></td></tr>" +
            "</tbody></table>";
        List<MetaDeck> result = _parser.Parse(html, 10);
        // img with no src returns empty string — empty imageUrl => row skipped
        result.ShouldBeEmpty();
    }

    [Test]
    public void Parse_MultiWordAnnotation_FormatsNameCorrectly()
    {
        string html =
            "<table><tbody>" +
            "<tr>" +
            "<td>1</td>" +
            "<td><img class=\"pokemon\" src=\"zoroark.png\"></td>" +
            "<td><a href=\"/decks/300\">N's Zoroark <span class=\"annotation\">ex</span></a></td>" +
            "</tr>" +
            "</tbody></table>";
        List<MetaDeck> result = _parser.Parse(html, 10);
        result[0].Name.ShouldBe("N's Zoroark ex");
    }

    [Test]
    public void Parse_CountZero_ReturnsEmptyList()
    {
        string html = BuildHtml(5);
        List<MetaDeck> result = _parser.Parse(html, 0);
        result.ShouldBeEmpty();
    }

    [Test]
    public void Parse_TableWithNoTbody_ReturnsEmptyList()
    {
        // QuerySelectorAll("table tbody tr") finds nothing without tbody
        string html = "<table><tr><td>1</td></tr></table>";
        List<MetaDeck> result = _parser.Parse(html, 10);
        result.ShouldBeEmpty();
    }

    // ---------------------------------------------------------------------------
    // Gaps found by mutation testing (Stryker, 2026-08-07). Every case above uses
    // rows with exactly ONE image and tidy anchor text, so two behaviours were
    // never exercised and mutating them changed nothing any test could see.
    // ---------------------------------------------------------------------------

    /// <summary>
    /// A row with two sprites must yield the second as SecondaryImageUrl.
    /// </summary>
    /// <remarks>
    /// Dual-icon decks are a real feature (Archetype.ImagePath2, the ComboBox and ReadJournal
    /// both render it), and no parser test had a two-image row — so
    /// <c>imgs.Length > 1 ? imgs[1]... : null</c> was never true and three mutants survived on
    /// that line, including replacing the whole expression with null.
    /// </remarks>
    [Test]
    public void Parse_RowWithTwoImages_ExtractsSecondaryImageUrl()
    {
        const string html =
            "<table><tbody><tr>" +
            "<td>1</td>" +
            "<td>" +
            "<img class=\"pokemon\" src=\"https://r2.limitlesstcg.net/pokemon/gen9/dragapult.png\" alt=\"dragapult\">" +
            "<img class=\"pokemon\" src=\"https://r2.limitlesstcg.net/pokemon/gen9/dusknoir.png\" alt=\"dusknoir\">" +
            "</td>" +
            "<td><a href=\"/decks/284\">Dragapult <span class=\"annotation\">ex</span></a></td>" +
            "<td>2227</td><td>49.22%</td>" +
            "</tr></tbody></table>";

        List<MetaDeck> decks = new LimitlessDeckParser().Parse(html, 10);

        decks.ShouldHaveSingleItem();
        decks[0].ImageUrl.ShouldEndWith("dragapult.png");
        decks[0].SecondaryImageUrl.ShouldNotBeNull("a two-sprite row must expose the second sprite");
        decks[0].SecondaryImageUrl!.ShouldEndWith("dusknoir.png");
    }

    /// <summary>
    /// A single-sprite row must leave SecondaryImageUrl null — the other side of the boundary.
    /// </summary>
    [Test]
    public void Parse_RowWithOneImage_LeavesSecondaryImageUrlNull()
    {
        List<MetaDeck> decks = new LimitlessDeckParser().Parse(DragapultHtml, 1);

        decks.ShouldHaveSingleItem();
        decks[0].SecondaryImageUrl.ShouldBeNull("a one-sprite row has no second icon to report");
    }

    /// <summary>
    /// The annotation branch exists to normalise spacing, and that is all it does.
    /// </summary>
    /// <remarks>
    /// For tidy markup the branch is a no-op: the anchor's TextContent already reads
    /// "Dragapult ex", so rebuilding it from base + annotation returns the same string, and
    /// mutating the branch away changed nothing. It only earns its keep when the markup has
    /// irregular whitespace — which is exactly what a scraped page produces.
    /// </remarks>
    [Test]
    public void Parse_AnnotationWithIrregularWhitespace_NormalisesTheName()
    {
        const string html =
            "<table><tbody><tr>" +
            "<td>1</td>" +
            "<td><img class=\"pokemon\" src=\"https://r2.limitlesstcg.net/pokemon/gen9/dragapult.png\" alt=\"dragapult\"></td>" +
            "<td><a href=\"/decks/284\">Dragapult    <span class=\"annotation\">ex</span></a></td>" +
            "<td>2227</td><td>49.22%</td>" +
            "</tr></tbody></table>";

        List<MetaDeck> decks = new LimitlessDeckParser().Parse(html, 1);

        decks.ShouldHaveSingleItem();
        decks[0].Name.ShouldBe("Dragapult ex",
            "the annotation branch collapses the gap between base name and annotation; " +
            "without it the raw anchor text keeps the run of spaces");
    }

    [Test]
    public void Parse_NullHtml_ReturnsEmptyList()
    {
        List<MetaDeck> result = _parser.Parse(null!, 10);
        result.ShouldBeEmpty();
    }
}
