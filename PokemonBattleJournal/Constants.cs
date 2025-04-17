namespace PokemonBattleJournal
{
    public static class Constants
    {
        public const string DatabaseFilename = "PokemonBattleJournal.db3";

        public const SQLite.SQLiteOpenFlags Flags =
            SQLite.SQLiteOpenFlags.ReadWrite |
            SQLite.SQLiteOpenFlags.Create |
            SQLite.SQLiteOpenFlags.SharedCache;

        public static string DatabasePath
        {
            get
            {
                return Path.Combine(FileHelper.GetAppDataPath(), DatabaseFilename);
            }
        }
    }
}
