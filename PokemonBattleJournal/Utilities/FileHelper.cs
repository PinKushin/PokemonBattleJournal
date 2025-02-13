using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace PokemonBattleJournal.Utilities
{
    public static class FileHelper
    {
        public static string GetAppDataPath()
        {
            //This is a wrapper for supporting unit tests
            if (DeviceInfo.Platform == DevicePlatform.Unknown)
            {              
                return "Test Response";
            }
            var filePath = FileSystem.Current.AppDataDirectory;
            return filePath;
        }

        public static bool Exists(string filePath)
        {
            //This is a wrapper for supporting unit tests
            if (DeviceInfo.Platform == DevicePlatform.Unknown)
            {
                return true;
            }
            if (!File.Exists(filePath)) return false;
            return true;
        }
        public static void CreateFile(string filePath)
        {
            //This is a wrapper for supporting unit tests
            if (DeviceInfo.Platform == DevicePlatform.Unknown)
            {
                return;
            }
			File.Create(filePath);
        }

        public static void DeleteFile(string filePath)
        {
            //This is a wrapper for supporting unit tests
            if (DeviceInfo.Platform == DevicePlatform.Unknown)
            {
                return;
            }
            File.Delete(filePath);
        }

        public static async Task<string> ReadFileAsync(string filePath) 
        {
            //This is a wrapper for supporting unit tests
            if (DeviceInfo.Platform == DevicePlatform.Unknown)
            {
                return "Test Response";
            }
            return await File.ReadAllTextAsync(filePath);
        }
        public static async Task WriteFileAsync(string filePath, string saveFile)
        {
            //This is a wrapper for supporting unit tests
            if (DeviceInfo.Platform == DevicePlatform.Unknown)
            {
                return;
            }
            await File.WriteAllTextAsync(filePath, saveFile);
            return;
			;
        }

    }
}
