using Microsoft.Extensions.Logging;
using PokemonBattleJournal.Scraper.Interfaces;
using PokemonBattleJournal.Scraper.Models;

namespace PokemonBattleJournal.Scraper.Services;

public class LimitlessMetaService : ILimitlessMetaService
{
    private const string DecksUrl = "https://limitlesstcg.com/decks";

    private readonly IMetaDeckFetcher _fetcher;
    private readonly IMetaDeckParser _parser;
    private readonly ILogger<LimitlessMetaService> _logger;

    public LimitlessMetaService(IMetaDeckFetcher fetcher, IMetaDeckParser parser, ILogger<LimitlessMetaService> logger)
    {
        _fetcher = fetcher;
        _parser = parser;
        _logger = logger;
    }

    public async Task<List<MetaDeck>> GetTopDecksAsync(int count = 10)
    {
        // Skip network call during UI test runs — the sentinel file is written by AppiumSetup.
        // UI tests only need the "Other" default archetype; live meta data is irrelevant there.
        if (File.Exists(Path.Combine(Path.GetTempPath(), "PokemonBattleJournal.uitest")))
        {
            _logger.LogInformation("GetTopDecksAsync: UI test sentinel detected — skipping network fetch");
            return [];
        }

        string html = await _fetcher.FetchAsync(DecksUrl);
        if (string.IsNullOrWhiteSpace(html))
        {
            _logger.LogInformation("GetTopDecksAsync: empty response — returning empty list (offline?)");
            return [];
        }

        return _parser.Parse(html, count);
    }
}
