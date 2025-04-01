namespace PokemonBattleJournal.Utilities
{
    public static class FileHelper
    {
        /// <summary>
        /// Get the path to the app data directory.
        /// </summary>
        /// <returns>returns file path string</returns>
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

        /// <summary>
        /// Check if a file exists.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns>returns true if file already exists</returns>
        public static bool Exists(string filePath)
        {
            //This is a wrapper for supporting unit tests
            if (DeviceInfo.Platform == DevicePlatform.Unknown)
            {
                return true;
            }
            return File.Exists(filePath);
        }

        /// <summary>
        /// Create a file.
        /// </summary>
        /// <param name="filePath">File Path</param>
        public static void CreateFile(string filePath)
        {
            //This is a wrapper for supporting unit tests
            if (DeviceInfo.Platform == DevicePlatform.Unknown)
            {
                return;
            }
            using var _ = File.Create(filePath);
        }

        /// <summary>
        /// Delete a file.
        /// </summary>
        /// <param name="filePath">File Path</param>
        public static void DeleteFile(string filePath)
        {
            //This is a wrapper for supporting unit tests
            if (DeviceInfo.Platform == DevicePlatform.Unknown)
            {
                return;
            }
            File.Delete(filePath);
        }

        /// <summary>
        /// Read a file as Text.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns>returns string of file contents</returns>
        public static async Task<string> ReadFileAsync(string filePath)
        {
            //This is a wrapper for supporting unit tests
            if (DeviceInfo.Platform == DevicePlatform.Unknown)
            {
                return "Test Response";
            }
            return await File.ReadAllTextAsync(filePath);
        }

        /// <summary>
        /// Write a file as Text.
        /// </summary>
        /// <param name="filePath">Path to Save</param>
        /// <param name="saveFile">Name of File</param>
        /// <returns>void</returns>
        public static async Task WriteFileAsync(string filePath, string saveFile)
        {
            //This is a wrapper for supporting unit tests
            if (DeviceInfo.Platform == DevicePlatform.Unknown)
            {
                return;
            }
            await File.WriteAllTextAsync(filePath, saveFile);
        }
    }
}