namespace PokemonBattleJournal.Utilities
{
    public static class PreferencesHelper
    {
        public static string GetSetting(string key)
        {
            //This is a wrapper for supporting unit tests
            if (DeviceInfo.Platform == DevicePlatform.Unknown)
            {
                return "Trainer";
            }
            return Preferences.Get(key, "Trainer");
        }

        public static void SetSetting(string key, string value)
        {
            //This is a wrapper for supporting unit tests
            if (DeviceInfo.Platform == DevicePlatform.Unknown)
            {
                return;
            }
            Preferences.Set(key, value);
        }

        public static uint GetTrainerId()
        {
            if (DeviceInfo.Platform == DevicePlatform.Unknown)
                return 0u;
            return (uint)Preferences.Get("TrainerId", 0);
        }

        public static void SetTrainerId(uint id)
        {
            if (DeviceInfo.Platform == DevicePlatform.Unknown)
                return;
            Preferences.Set("TrainerId", (int)id);
        }
    }
}