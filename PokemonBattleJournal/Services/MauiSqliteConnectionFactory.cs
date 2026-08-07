using PokemonBattleJournal.Scraper.Interfaces;

namespace PokemonBattleJournal.Services
{
    /// <summary>
    /// The app head's <see cref="SqliteConnectionFactory"/>: the one that knows where the
    /// database file goes on a real device.
    /// </summary>
    /// <remarks>
    /// This class is the entire reason the base class is abstract. Resolving an app-data
    /// directory needs MAUI's <c>FileSystem</c> (via <see cref="FileHelper.GetAppDataPath"/>),
    /// and <c>PokemonBattleJournal.Core</c> cannot reference MAUI — that is what makes it
    /// mutation-testable, since Stryker cannot recompile the MAUI head at all.
    ///
    /// So the path question moves up here rather than being answered with a platform call
    /// buried in a shared service. Everything else about the connection — the flags, the table
    /// creation order, the semaphore pairing — stays in Core where tests can reach it.
    /// </remarks>
    public sealed class MauiSqliteConnectionFactory : SqliteConnectionFactory
    {
        public MauiSqliteConnectionFactory(
            ILogger logger,
            ILimitlessMetaService metaService,
            IErrorHandler errorHandler)
            : base(logger, metaService, errorHandler)
        {
        }

        /// <summary>
        /// The app data directory plus the database filename — the behaviour the old
        /// <c>Constants.DatabasePath</c> property had, moved to the one place that can perform it.
        /// </summary>
        protected override string GetDbPath() =>
            Path.Combine(FileHelper.GetAppDataPath(), Constants.DatabaseFilename);
    }
}
