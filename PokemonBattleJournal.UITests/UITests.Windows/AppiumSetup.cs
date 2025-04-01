namespace UITests
{
    public class AppiumSetup : IDisposable
    {
        private static AppiumDriver? driver;

        public static AppiumDriver App => driver ?? throw new NullReferenceException("AppiumDriver is null");
        public AppiumSetup()
        {
            RunBeforeAnyTests();
        }

        public void RunBeforeAnyTests()
        {
            // If you started an Appium server manually, make sure to comment out the next line
            // This line starts a local Appium server for you as part of the test run
            AppiumServerHelper.StartAppiumLocalServer();
            var windowsOptions = new AppiumOptions
            {
                // Specify windows as the driver, typically don't need to change this
                AutomationName = "windows",
                // Always Windows for Windows
                PlatformName = "Windows",
                DeviceName = "WindowsPC",
                // The identifier of the deployed application to test
                App = @"D:\source\PinKushin\PokemonBattleJournal\PokemonBattleJournal\bin\Debug\net9.0-windows10.0.19041.0\win10-x64\PokemonBattleJournal.exe" //Always update the path on new machine or repartition of OS
            };

            // Note there are many more options that you can use to influence the app under test according to your needs

            driver = new WindowsDriver(windowsOptions);
        }

        public void Dispose()
        {
            driver?.Quit();
            // If an Appium server was started locally above, make sure we clean it up here
            AppiumServerHelper.DisposeAppiumLocalServer();
        }
    }
}
