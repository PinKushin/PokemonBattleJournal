namespace PokemonBattleJournal.Utilities
{
    public static class MainThreadHelper
    {
        public static void BeginInvokeOnMainThread(Action action)
        {
            //This is a wrapper for supporting unit tests
            if (DeviceInfo.Platform == DevicePlatform.Unknown)
            {
                _ = Task.Run(action);
                return;
            }
            MainThread.BeginInvokeOnMainThread(action);
        }

        public static bool IsMainThread
        {
            get
            {
                //This is a wrapper for supporting unit tests
                if (DeviceInfo.Platform == DevicePlatform.Unknown)
                {
                    return true;
                }
                return MainThread.IsMainThread;
            }
        }
    }
}