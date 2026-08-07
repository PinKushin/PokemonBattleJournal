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
        /// Largest backup this will read, in bytes.
        /// </summary>
        /// <remarks>
        /// Declared here rather than inside the implementation because callers need it too. The
        /// service can only check a size once the file is already a string in memory, so a caller
        /// holding a stream should refuse an oversized file before reading it — and both must
        /// refuse at the same number, or one of them is decoration.
        /// </remarks>
        public const int MaxBackupBytes = 8 * 1024 * 1024;

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
