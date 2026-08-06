using PokemonBattleJournal.Services.Restore;

namespace PokemonBattleJournal.Interfaces
{
    /// <summary>
    /// Reads a backup envelope written by <see cref="IExportService.ExportBackupAsync"/> back
    /// into the database.
    /// </summary>
    /// <remarks>
    /// Separate from <c>ITrainerHillImportService</c> because the two answer different
    /// questions. A TrainerHill file is somebody else's data going to whoever is importing it;
    /// a backup is this app's own data going back where it came from, so trainers come from the
    /// file and merge into an existing trainer of the same name rather than creating a second.
    /// </remarks>
    public interface IRestoreService
    {
        /// <summary>
        /// Restores a backup envelope.
        /// </summary>
        /// <param name="json">The envelope, as written by the backup export.</param>
        /// <returns>
        /// What happened, including entries skipped as already present and conflicts left for
        /// the user. Never throws for per-entry problems — those are collected in
        /// <see cref="RestoreResult.Errors"/> so one bad entry cannot abort the rest.
        /// </returns>
        Task<RestoreResult> RestoreBackupAsync(string json);
    }
}
