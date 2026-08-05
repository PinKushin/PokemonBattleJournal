
namespace PokemonBattleJournal.IntegrationTests.Infrastructure;

/// <summary>
/// Stub that returns an empty meta deck list — prevents network calls during DB integration tests.
/// </summary>
public sealed class NullMetaService : ILimitlessMetaService
{
    public Task<List<MetaDeck>> GetTopDecksAsync(int count = 10)
        => Task.FromResult(new List<MetaDeck>());
}
