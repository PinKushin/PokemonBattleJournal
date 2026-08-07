namespace PokemonBattleJournal
{
    /// <summary>
    /// Database constants that carry no platform dependency.
    /// </summary>
    /// <remarks>
    /// The <c>DatabasePath</c> property that used to live here did not move with them. It was
    /// built from <c>FileHelper.GetAppDataPath()</c>, which reaches MAUI's <c>FileSystem</c>
    /// and <c>DeviceInfo</c>, and that is exactly the kind of dependency this project exists to
    /// keep out. Resolving the path is now the app head's job, in
    /// <c>MauiSqliteConnectionFactory</c> — see the abstract <c>GetDbPath()</c> on
    /// <see cref="Services.SqliteConnectionFactory"/>.
    /// </remarks>
    public static class Constants
    {
        public const string DatabaseFilename = "PokemonBattleJournal.db3";

        public const SQLite.SQLiteOpenFlags Flags =
            SQLite.SQLiteOpenFlags.ReadWrite |
            SQLite.SQLiteOpenFlags.Create |
            SQLite.SQLiteOpenFlags.SharedCache;
    }
}
