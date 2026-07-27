namespace UITests
{
    public class AppiumSetup : IDisposable
    {
        private static AppiumDriver? driver;

        public static AppiumDriver App
        {
            get
            {
                return driver ?? throw new NullReferenceException("AppiumDriver is null");
            }
        }

        public AppiumSetup()
        {
            DeployToAndroid();
            AppiumServerHelper.StartAppiumLocalServer();
            AppiumOptions androidOptions = new()
            {
                // Specify UIAutomator2 as the driver, typically don't need to change this
                AutomationName = "UIAutomator2",
                // Always Android for Android
                PlatformName = "Android",

                // RELEASE BUILD SETUP
                // The full path to the .apk file
                // This only works with release builds because debug builds have fast deployment enabled
                // and Appium isn't compatible with fast deployment
                // App = Path.Join(TestContext.CurrentContext.TestDirectory, "../../../../MauiApp/bin/Release/net8.0-android/com.PinKushin.PokemonBattleJournal-Signed.apk"),
                // END RELEASE BUILD SETUP
            };

            // DEBUG BUILD SETUP
            // If you're running your tests against debug builds you'll need to set NoReset to true
            // otherwise appium will delete all the libraries used for Fast Deployment on Android
            // Release builds have Fast Deployment disabled
            // https://learn.microsoft.com/xamarin/android/deploy-test/building-apps/build-process#fast-deployment
            androidOptions.AddAdditionalAppiumOption(MobileCapabilityType.NoReset, "true");
            androidOptions.AddAdditionalAppiumOption(AndroidMobileCapabilityType.AppPackage, "com.PinKushin.PokemonBattleJournal");

            //Make sure to set [Register("com.PinKushin.PokemonBattleJournal.MainActivity")] on the MainActivity of your android application
            androidOptions.AddAdditionalAppiumOption(AndroidMobileCapabilityType.AppActivity, $"com.PinKushin.PokemonBattleJournal.MainActivity");
            // END DEBUG BUILD SETUP


            // Specifying the avd option will boot the emulator for you
            // make sure there is an emulator with the name below
            // If not specified, make sure you have an emulator booted
            androidOptions.AddAdditionalAppiumOption("avd", "pixel_7_-_api_35");

            // Note there are many more options that you can use to influence the app under test according to your needs

            driver = new AndroidDriver(androidOptions);
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(15);
            Task.Delay(2000).Wait(); // Wait for the app to load
        }

        public void Dispose()
        {
            driver?.Quit();
            AppiumServerHelper.DisposeAppiumLocalServer();
        }

        private static void DeployToAndroid()
        {
            // Build and fast-deploy the app to the running emulator before tests start.
            // This ensures the emulator always has the latest code without a manual deploy step.
            string repoRoot = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
            string project = Path.Combine(repoRoot, "PokemonBattleJournal", "PokemonBattleJournal.csproj");

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"build \"{project}\" -f net10.0-android -t:Install",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = repoRoot,
            };

            using var proc = System.Diagnostics.Process.Start(psi)!;
            proc.WaitForExit(180_000); // 3-minute cap

            if (proc.ExitCode != 0)
            {
                string err = proc.StandardError.ReadToEnd();
                throw new InvalidOperationException($"Android deploy failed (exit {proc.ExitCode}):\n{err}");
            }
        }
    }
}