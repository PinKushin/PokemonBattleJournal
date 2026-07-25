namespace PokemonBattleJournal.Scraper.Interfaces;

public interface IMetaServiceFactory
{
    /// <summary>
    /// Creates the configured <see cref="ILimitlessMetaService"/> implementation.
    /// </summary>
    ILimitlessMetaService Create();
}
