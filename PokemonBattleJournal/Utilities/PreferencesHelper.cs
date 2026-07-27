namespace PokemonBattleJournal.Utilities
{
    public static class PreferencesHelper
    {
        public static string GetSetting(string key)
        {
            if (DeviceInfo.Platform == DevicePlatform.Unknown)
                return "false";
            return Preferences.Get(key, "false");
        }

        public static void SetSetting(string key, string value)
        {
            if (DeviceInfo.Platform == DevicePlatform.Unknown)
                return;
            Preferences.Set(key, value);
        }
    }
}
