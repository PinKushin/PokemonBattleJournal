using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PokemonBattleJournal.Utilities
{
    internal class PreferencesHelper
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
