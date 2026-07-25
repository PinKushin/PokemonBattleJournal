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
}
