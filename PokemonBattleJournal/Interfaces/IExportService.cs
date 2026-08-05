namespace PokemonBattleJournal.Interfaces
{
    /// <summary>
    /// Serializes stored matches to JSON. Two formats, differing in fidelity — see the
    /// individual methods.
    /// </summary>
    public interface IExportService
    {
        /// <summary>
        /// Exports one trainer's matches in TrainerHill's own format: a bare JSON array with
        /// archetype <em>slugs</em>.
        /// </summary>
        /// <remarks>
        /// For interop with TrainerHill, so it matches their shape rather than ours. Slugs are
        /// lossy — recovering the original archetype name on import needs the Limitless meta
        /// list. Use <see cref="ExportBackupAsync"/> when the file is meant to be restored.
        /// TrainerHill has no multi-profile concept, so this is always a single trainer.
        /// </remarks>
        Task<string> ExportTrainerHillAsync(uint trainerId);

        /// <summary>
        /// Exports one or more trainers in the app's own backup envelope, with archetype and tag
        /// names written verbatim so a restore never depends on network lookups.
        /// </summary>
        /// <param name="trainerIds">Trainers to include; null or empty exports every trainer.</param>
        Task<string> ExportBackupAsync(IReadOnlyList<uint>? trainerIds = null);
    }
}
