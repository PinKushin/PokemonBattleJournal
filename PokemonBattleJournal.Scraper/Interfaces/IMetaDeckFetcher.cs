namespace PokemonBattleJournal.Scraper.Interfaces;

public interface IMetaDeckFetcher
{
    /// <summary>
    /// Fetches raw HTML from the given URL.
    /// Returns an empty string on network failure (offline-safe).
    /// </summary>
    Task<string> FetchAsync(string url);
}
