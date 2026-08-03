namespace PokemonBattleJournal.Interfaces
{
    public interface ITrainerHillImportService
    {
        Task<(int Imported, List<string> Errors)> ImportAsync(Stream jsonStream, uint trainerId);
    }
}
