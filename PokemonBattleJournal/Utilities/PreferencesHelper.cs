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
	}
}