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

        private const string AvdName = "pixel_9_pro_xl_-_api_36_1";
        private const string AppPackage = "com.PinKushin.PokemonBattleJournal";

        public AppiumSetup()
        {
            // 1. Put emulator/adb on PATH so Appium can find them
            EnsureAndroidToolsInPath();

            // 2. Start Appium server
            AppiumServerHelper.StartAppiumLocalServer();

            // 3. Build APK (no -t:Install; Appium handles the adb install via the app capability)
            string apkPath = BuildAndroidApk();

            AppiumOptions androidOptions = new()
            {
                AutomationName = "UIAutomator2",
                PlatformName = "Android",
                App = apkPath,
            };

            // avd boots the emulator if not already running
            androidOptions.AddAdditionalAppiumOption("avd", AvdName);
            androidOptions.AddAdditionalAppiumOption("avdLaunchTimeout", 180_000);
            androidOptions.AddAdditionalAppiumOption("avdReadyTimeout", 180_000);
            androidOptions.AddAdditionalAppiumOption(AndroidMobileCapabilityType.AppPackage, AppPackage);
            androidOptions.AddAdditionalAppiumOption(AndroidMobileCapabilityType.AppActivity, $"{AppPackage}.MainActivity");
            androidOptions.AddAdditionalAppiumOption("appWaitActivity", $"{AppPackage}.MainActivity");
            androidOptions.AddAdditionalAppiumOption("appWaitDuration", 60_000);

            // 4. Create driver — Appium installs the APK and launches the app.
            // Fresh install guarantees a clean DB and preferences (no wipe step needed).
            driver = new AndroidDriver(
                new Uri("http://127.0.0.1:4723/"),
                androidOptions,
                TimeSpan.FromMinutes(5));
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(15);

            // 5. Poll until Android reports fully booted
            WaitForEmulatorBoot(timeoutSeconds: 180);

            // 6. Wait for the activity to be foregrounded before tests start
            WaitForActivity($"{AppPackage}.MainActivity", timeoutSeconds: 60);

            // 7. Seed known test matches so ReadJournal and TrainerPage tests always have data
            SeedTestData();
        }

        public void Dispose()
        {
            // Force-stop the app before closing the session so it doesn't stay running
            RunAdb($"shell am force-stop {AppPackage}", timeoutMs: 5_000);
            driver?.Quit();
            AppiumServerHelper.DisposeAppiumLocalServer();
            // Only kill the emulator in CI — locally the AVD stays alive for the next run
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")))
                ShutdownEmulator();
        }

        private static void SeedTestData()
        {
            // Save 3 matches via the UI so ReadJournal and TrainerPage always have data.
            try
            {
                // 1. Set a known trainer name so ReadJournal/TrainerPage always have a trainer
                var menu = driver!.FindElement(MobileBy.AccessibilityId("Open navigation drawer"));
                menu.Click();
                Thread.Sleep(500);
                var optionsItem = driver.FindElement(MobileBy.AndroidUIAutomator("new UiSelector().text(\"Options\")"));
                optionsItem.Click();
                Thread.Sleep(800);

                var trainerInput = driver.FindElement(MobileBy.AndroidUIAutomator(
                    "new UiScrollable(new UiSelector().scrollable(true).instance(0))" +
                    ".scrollIntoView(new UiSelector().resourceId(\"com.PinKushin.PokemonBattleJournal:id/TrainerNameInput\"))"));
                trainerInput.Clear();
                trainerInput.SendKeys("UITestTrainer");
                Thread.Sleep(300);

                var saveTrainerBtn = driver.FindElement(MobileBy.AndroidUIAutomator(
                    "new UiScrollable(new UiSelector().scrollable(true).instance(0))" +
                    ".scrollIntoView(new UiSelector().resourceId(\"com.PinKushin.PokemonBattleJournal:id/SaveTrainerNameButton\"))"));
                saveTrainerBtn.Click();
                Thread.Sleep(500);

                for (int i = 1; i <= 3; i++)
                {
                    // Ensure we're on Journal Entry
                    var navDrawer = driver!.FindElement(MobileBy.AccessibilityId("Open navigation drawer"));
                    navDrawer.Click();
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

                    // Type seed note
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

        private static void ShutdownEmulator()
        {
            try
            {
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
            catch { }
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

        // AppContext.BaseDirectory = …\UITests.Android\bin\<Config>\net10.0\
        // 5 hops up lands at repo root
        private static string RepoRoot => Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

        private static string BuildAndroidApk()
        {
            string repoRoot = RepoRoot;
            string project = Path.Combine(repoRoot, "PokemonBattleJournal", "PokemonBattleJournal.csproj");
#if DEBUG
            const string config = "Debug";
#else
            const string config = "Release";
#endif
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"build \"{project}\" -f net10.0-android -c {config}",
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
                throw new InvalidOperationException($"Android build failed (exit {proc.ExitCode}):\n{err}");
            }

            string apkPath = Path.Combine(repoRoot, "PokemonBattleJournal", "bin", config,
                "net10.0-android", "com.PinKushin.PokemonBattleJournal-Signed.apk");

            if (!File.Exists(apkPath))
                throw new FileNotFoundException($"APK not found at expected path: {apkPath}");

            return apkPath;
        }
    }
}
