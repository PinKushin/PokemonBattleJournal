namespace PokemonBattleJournal.Interfaces
{
    public interface ITrainerHillImportService
    {
        Task<(int Imported, int SkippedDuplicates, List<string> Errors)> ImportAsync(Stream jsonStream, uint trainerId);
    }
}
