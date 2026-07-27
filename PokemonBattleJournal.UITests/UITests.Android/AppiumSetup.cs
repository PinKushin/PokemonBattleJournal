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

            // 3. avd causes UIAutomator2 to boot the emulator if not already running
            androidOptions.AddAdditionalAppiumOption("avd", "pixel_7_-_api_35");
            androidOptions.AddAdditionalAppiumOption("avdLaunchTimeout", 180_000);
            androidOptions.AddAdditionalAppiumOption("avdReadyTimeout", 180_000);
            // Tell Appium to wait up to 60s for MainActivity to be in the foreground
            androidOptions.AddAdditionalAppiumOption("appWaitActivity", "com.PinKushin.PokemonBattleJournal.MainActivity");
            androidOptions.AddAdditionalAppiumOption("appWaitDuration", 60_000);

            // 4. Create driver — triggers Appium to boot the emulator if needed.
            // Cold boot takes 2-3 min; default 60s command timeout is too short.
            driver = new AndroidDriver(
                new Uri("http://127.0.0.1:4723/"),
                androidOptions,
                TimeSpan.FromMinutes(5));
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(15);

            // 5. Poll until Android reports fully booted (sys.boot_completed = 1)
            WaitForEmulatorBoot(timeoutSeconds: 180);

            // 6. Deploy the latest build now that the emulator is fully ready
            DeployToAndroid();

            // 7. Relaunch so Appium session tracks the freshly deployed instance
            RunAdb("shell am start -n com.PinKushin.PokemonBattleJournal/com.PinKushin.PokemonBattleJournal.MainActivity",
                   timeoutMs: 10_000);

            // 8. Wait for the activity to be foregrounded before tests start
            WaitForActivity("com.PinKushin.PokemonBattleJournal.MainActivity", timeoutSeconds: 60);

            // 9. Seed known test matches so ReadJournal and TrainerPage tests always have data
            SeedTestData();
        }

        public void Dispose()
        {
            CleanSeedData();
            driver?.Quit();
            AppiumServerHelper.DisposeAppiumLocalServer();
            ShutdownEmulator();
        }

        private static void SeedTestData()
        {
            // Save 3 matches via the UI so ReadJournal and TrainerPage always have data.
            // Notes contain "UITestSeed" so CleanSeedData can target exactly these rows.
            try
            {
                for (int i = 1; i <= 3; i++)
                {
                    // Ensure we're on Journal Entry
                    var menu = driver!.FindElement(MobileBy.AccessibilityId("Open navigation drawer"));
                    menu.Click();
                    Thread.Sleep(500);
                    var item = driver.FindElement(MobileBy.AndroidUIAutomator("new UiSelector().text(\"Journal Entry\")"));
                    item.Click();
                    Thread.Sleep(800);

                    // Select "Win" from result picker
                    var resultPicker = driver.FindElement(MobileBy.AndroidUIAutomator(
                        "new UiScrollable(new UiSelector().scrollable(true).instance(0))" +
                        ".scrollIntoView(new UiSelector().resourceId(\"com.PinKushin.PokemonBattleJournal:id/PossibleResultsPicker\"))"));
                    resultPicker.Click();
                    Thread.Sleep(500);
                    var winOption = driver.FindElement(MobileBy.AndroidUIAutomator("new UiSelector().text(\"Win\")"));
                    winOption.Click();
                    Thread.Sleep(300);

                    // Type seed note so we can identify and delete these rows later
                    var noteInput = driver.FindElement(MobileBy.AndroidUIAutomator(
                        "new UiScrollable(new UiSelector().scrollable(true).instance(0))" +
                        ".scrollIntoView(new UiSelector().resourceId(\"com.PinKushin.PokemonBattleJournal:id/UserNoteInput\"))"));
                    noteInput.SendKeys($"UITestSeed-{i}");
                    Thread.Sleep(200);

                    // Save
                    var saveBtn = driver.FindElement(MobileBy.AndroidUIAutomator(
                        "new UiScrollable(new UiSelector().scrollable(true).instance(0))" +
                        ".scrollIntoView(new UiSelector().resourceId(\"com.PinKushin.PokemonBattleJournal:id/SaveMatchButton\"))"));
                    saveBtn.Click();
                    Thread.Sleep(800);
                }
            }
            catch
            {
                // Seed failure is non-fatal — tests degrade gracefully without data
            }
        }

        private static void CleanSeedData()
        {
            // Delete seeded test matches from the app's SQLite DB via adb.
            // Works on debug APKs via run-as (no root required).
            try
            {
                const string pkg = "com.PinKushin.PokemonBattleJournal";
                const string db = "files/PokemonBattleJournal.db3";
                string sql = "DELETE FROM MatchEntry WHERE Notes LIKE '%UITestSeed%';";
                RunAdb($"shell run-as {pkg} sqlite3 {db} \"{sql}\"", timeoutMs: 10_000);
            }
            catch
            {
                // Best effort — leftover seed rows don't break production
            }
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

        private static void WaitForEmulatorBoot(int timeoutSeconds)
        {
            var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
            while (DateTime.UtcNow < deadline)
            {
                string output = RunAdb("shell getprop sys.boot_completed", timeoutMs: 5_000);
                if (output.Trim() == "1") return;
                Task.Delay(2000).Wait();
            }
            throw new TimeoutException($"Emulator did not finish booting within {timeoutSeconds}s");
        }

        private static void WaitForActivity(string activity, int timeoutSeconds)
        {
            var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
            while (DateTime.UtcNow < deadline)
            {
                string output = RunAdb("shell dumpsys activity activities", timeoutMs: 5_000);
                if (output.Contains(activity)) return;
                Task.Delay(2000).Wait();
            }
            // Non-fatal — tests will fail naturally if app isn't up
        }

        private static string RunAdb(string arguments, int timeoutMs)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "adb",
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using var proc = System.Diagnostics.Process.Start(psi)!;
                proc.WaitForExit(timeoutMs);
                return proc.StandardOutput.ReadToEnd();
            }
            catch { return string.Empty; }
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