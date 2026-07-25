using PokemonBattleJournal.Scraper.Models;

namespace PokemonBattleJournal.Scraper.Interfaces;

public interface IMetaDeckParser
{
    /// <summary>
    /// Parses deck rows from raw HTML.
    /// Returns up to <paramref name="count"/> decks in ranked order.
    /// </summary>
    List<MetaDeck> Parse(string html, int count);
}
