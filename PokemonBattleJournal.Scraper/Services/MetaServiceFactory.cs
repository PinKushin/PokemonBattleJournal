using Microsoft.Extensions.Logging;
using PokemonBattleJournal.Scraper.Interfaces;

namespace PokemonBattleJournal.Scraper.Services;

public class MetaServiceFactory : IMetaServiceFactory
{
    private readonly IMetaDeckFetcher _fetcher;
    private readonly IMetaDeckParser _parser;
    private readonly ILogger<LimitlessMetaService> _logger;

    public MetaServiceFactory(IMetaDeckFetcher fetcher, IMetaDeckParser parser, ILogger<LimitlessMetaService> logger)
    {
        _fetcher = fetcher;
        _parser = parser;
        _logger = logger;
    }

    public ILimitlessMetaService Create() => new LimitlessMetaService(_fetcher, _parser, _logger);
}
