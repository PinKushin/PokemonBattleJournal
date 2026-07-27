using PokemonBattleJournal.Scraper.Models;
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

    [Fact]
    public void Parse_ValidHtml_ReturnsRequestedCount()
    {
        string html = BuildHtml(12);
        List<MetaDeck> result = _parser.Parse(html, 10);
        result.Count.ShouldBe(10);
    }

    [Fact]
    public void Parse_ValidHtml_ExtractsDeckName()
    {
        List<MetaDeck> result = _parser.Parse(DragapultHtml, 10);
        result[0].Name.ShouldBe("Dragapult ex");
    }

    [Fact]
    public void Parse_ValidHtml_ExtractsImageUrl()
    {
        List<MetaDeck> result = _parser.Parse(DragapultHtml, 10);
        result[0].ImageUrl.ShouldContain("dragapult");
    }

    [Fact]
    public void Parse_EmptyTable_ReturnsEmptyList()
    {
        string html = "<table><tbody></tbody></table>";
        List<MetaDeck> result = _parser.Parse(html, 10);
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_CountGreaterThanRows_ReturnsAllRows()
    {
        string html = BuildHtml(3);
        List<MetaDeck> result = _parser.Parse(html, 10);
        result.Count.ShouldBe(3);
    }

    [Fact]
    public void Parse_EmptyHtml_ReturnsEmptyList()
    {
        List<MetaDeck> result = _parser.Parse(string.Empty, 10);
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_WhitespaceOnlyHtml_ReturnsEmptyList()
    {
        List<MetaDeck> result = _parser.Parse("   \t\n  ", 10);
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_RowMissingAnchor_SkipsRow()
    {
        string html =
            "<table><tbody>" +
            "<tr><td>1</td><td><img class=\"pokemon\" src=\"img.png\"></td><td>no link here</td></tr>" +
            "</tbody></table>";
        List<MetaDeck> result = _parser.Parse(html, 10);
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_RowMissingImage_SkipsRow()
    {
        string html =
            "<table><tbody>" +
            "<tr><td>1</td><td></td><td><a href=\"/decks/1\">Charizard <span class=\"annotation\">ex</span></a></td></tr>" +
            "</tbody></table>";
        List<MetaDeck> result = _parser.Parse(html, 10);
        result.ShouldBeEmpty();
    }

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
    public void Parse_CountZero_ReturnsEmptyList()
    {
        string html = BuildHtml(5);
        List<MetaDeck> result = _parser.Parse(html, 0);
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_TableWithNoTbody_ReturnsEmptyList()
    {
        // QuerySelectorAll("table tbody tr") finds nothing without tbody
        string html = "<table><tr><td>1</td></tr></table>";
        List<MetaDeck> result = _parser.Parse(html, 10);
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_NullHtml_ReturnsEmptyList()
    {
        List<MetaDeck> result = _parser.Parse(null!, 10);
        result.ShouldBeEmpty();
    }
}
