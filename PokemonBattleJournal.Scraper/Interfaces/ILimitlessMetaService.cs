using PokemonBattleJournal.Scraper.Models;

namespace PokemonBattleJournal.Scraper.Interfaces;

public interface ILimitlessMetaService
{
    /// <summary>
    /// Returns the top <paramref name="count"/> meta decks from Limitless TCG.
    /// Returns an empty list when offline or on parse failure.
    /// </summary>
    Task<List<MetaDeck>> GetTopDecksAsync(int count = 10);
}
