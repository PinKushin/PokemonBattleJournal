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
            // 1. Put emulator/adb on PATH so Appium can find them
            EnsureAndroidToolsInPath();

            // 2. Start Appium server (must exist before creating a driver)
            AppiumServerHelper.StartAppiumLocalServer();

            AppiumOptions androidOptions = new()
            {
                AutomationName = "UIAutomator2",
                PlatformName = "Android",
            };

            androidOptions.AddAdditionalAppiumOption(MobileCapabilityType.NoReset, "true");
            androidOptions.AddAdditionalAppiumOption(AndroidMobileCapabilityType.AppPackage, "com.PinKushin.PokemonBattleJournal");
            androidOptions.AddAdditionalAppiumOption(AndroidMobileCapabilityType.AppActivity, "com.PinKushin.PokemonBattleJournal.MainActivity");

            // 3. avd causes UIAutomator2 to boot the emulator if it is not already running.
            //    EnsureAndroidToolsInPath() ensures emulator.exe is discoverable.
            androidOptions.AddAdditionalAppiumOption("avd", "pixel_7_-_api_35");
            androidOptions.AddAdditionalAppiumOption("avdLaunchTimeout", 180_000);
            androidOptions.AddAdditionalAppiumOption("avdReadyTimeout", 180_000);

            // 4. Create driver — this triggers Appium to boot the emulator if needed
            driver = new AndroidDriver(androidOptions);
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(15);

            // 5. Emulator is now up — deploy the latest build then relaunch
            DeployToAndroid();

            // 6. Relaunch app via adb so Appium session tracks the fresh instance
            var adb = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "adb",
                Arguments = "shell am start -n com.PinKushin.PokemonBattleJournal/com.PinKushin.PokemonBattleJournal.MainActivity",
                UseShellExecute = false,
                RedirectStandardOutput = true,
            };
            using var launch = System.Diagnostics.Process.Start(adb);
            launch?.WaitForExit(10_000);

            // 7. Give the app time to finish launching
            Task.Delay(3000).Wait();
        }

        public void Dispose()
        {
            driver?.Quit();
            AppiumServerHelper.DisposeAppiumLocalServer();
            ShutdownEmulator();
        }

        private static void ShutdownEmulator()
        {
            try
            {
                // adb emu kill gracefully shuts down the AVD that Appium booted
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "adb",
                    Arguments = "emu kill",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using var proc = System.Diagnostics.Process.Start(psi);
                proc?.WaitForExit(10_000);
            }
            catch
            {
                // Best effort — don't fail the test run if shutdown fails
            }
        }

        private static void EnsureAndroidToolsInPath()
        {
            string androidHome = Environment.GetEnvironmentVariable("ANDROID_HOME")
                ?? Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT")
                ?? string.Empty;

            if (string.IsNullOrEmpty(androidHome)) return;

            string emulatorDir = Path.Combine(androidHome, "emulator");
            string platformToolsDir = Path.Combine(androidHome, "platform-tools");
            string currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

            List<string> additions = [];
            if (!currentPath.Contains(emulatorDir)) additions.Add(emulatorDir);
            if (!currentPath.Contains(platformToolsDir)) additions.Add(platformToolsDir);

            if (additions.Count > 0)
            {
                Environment.SetEnvironmentVariable("PATH",
                    string.Join(Path.PathSeparator.ToString(), [.. additions, currentPath]));
            }
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