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

        private const string AvdName = "pixel_7_-_api_35";
        private const string AppPackage = "com.PinKushin.PokemonBattleJournal";

        private static readonly string SetupLogPath = Path.Combine(
            Path.GetTempPath(), "UITests.Android.setup.log");

        private static void Log(string message)
        {
            string line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
            File.AppendAllText(SetupLogPath, line + Environment.NewLine);
        }

        public AppiumSetup()
        {
            File.WriteAllText(SetupLogPath, $"=== AppiumSetup start {DateTime.Now:O} ==={Environment.NewLine}");

            Log("1. EnsureAndroidToolsInPath");
            EnsureAndroidToolsInPath();

            Log("2. EnsureEmulatorRunning");
            EnsureEmulatorRunning();
            Log("2. EnsureEmulatorRunning done");

            Log("3. StartAppiumLocalServer");
            AppiumServerHelper.StartAppiumLocalServer();
            Log("3. StartAppiumLocalServer done");

            bool useInstalled = !string.IsNullOrEmpty(
                Environment.GetEnvironmentVariable("ANDROID_USE_INSTALLED"));

            AppiumOptions androidOptions = new()
            {
                AutomationName = "UIAutomator2",
                PlatformName = "Android",
            };

            if (useInstalled)
            {
                Log("4. Skipping build — using app deployed by VS");
                androidOptions.AddAdditionalAppiumOption("noReset", true);
            }
            else
            {
                Log("4. BuildAndroidApk");
                string apkPath = BuildAndroidApk();
                Log($"4. BuildAndroidApk done: {apkPath}");
                androidOptions.App = apkPath;
            }

            androidOptions.AddAdditionalAppiumOption(AndroidMobileCapabilityType.AppPackage, AppPackage);
            androidOptions.AddAdditionalAppiumOption(AndroidMobileCapabilityType.AppActivity, $"{AppPackage}.MainActivity");
            androidOptions.AddAdditionalAppiumOption("appWaitActivity", $"{AppPackage}.MainActivity");
            androidOptions.AddAdditionalAppiumOption("appWaitDuration", 60_000);

            Log("5. new AndroidDriver");
            driver = new AndroidDriver(
                new Uri("http://127.0.0.1:4723/"),
                androidOptions,
                TimeSpan.FromMinutes(5));
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(15);
            Log("5. AndroidDriver created");

            Log("6. WaitForActivity");
            WaitForActivity($"{AppPackage}.MainActivity", timeoutSeconds: 60);
            Log("6. WaitForActivity done");

            Log("7. SeedTestData");
            SeedTestData();
            Log("7. SeedTestData done");
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
            // Fresh install starts on Journal Entry with the first-boot Welcome prompt blocking the UI.
            // Handle the prompt first, then seed matches. No need to visit Options — the prompt
            // creates the trainer directly.
            try
            {
                // 1. The Welcome prompt appears immediately: type trainer name and save
                // DisplayPromptAsync renders as an Android AlertDialog with a single EditText
                var promptInput = driver!.FindElement(MobileBy.AndroidUIAutomator(
                    "new UiSelector().className(\"android.widget.EditText\")"));
                promptInput.SendKeys("UITestTrainer");
                driver.FindElement(MobileBy.AndroidUIAutomator("new UiSelector().text(\"Save\")")).Click();
                // Wait for trainer creation to complete — SaveMatchButton appears when main page is ready
                driver.FindElement(MobileBy.AndroidUIAutomator(
                    "new UiScrollable(new UiSelector().scrollable(true).instance(0))" +
                    ".scrollIntoView(new UiSelector().resourceId(\"com.PinKushin.PokemonBattleJournal:id/SaveMatchButton\"))"));

                // 2. Now on Journal Entry — seed 3 matches. Save clears the form so no nav needed between iterations.
                for (int i = 1; i <= 3; i++)
                {
                    // Select "Win" from result picker
                    var resultPicker = driver.FindElement(MobileBy.AndroidUIAutomator(
                        "new UiScrollable(new UiSelector().scrollable(true).instance(0))" +
                        ".scrollIntoView(new UiSelector().resourceId(\"com.PinKushin.PokemonBattleJournal:id/PossibleResultsPicker\"))"));
                    resultPicker.Click();
                    driver.FindElement(MobileBy.AndroidUIAutomator("new UiSelector().text(\"Win\")")).Click();

                    // Select "Other" for player archetype — SaveMatchAsync rejects null PlayerSelected
                    driver.FindElement(MobileBy.AndroidUIAutomator(
                        "new UiScrollable(new UiSelector().scrollable(true).instance(0))" +
                        ".scrollIntoView(new UiSelector().resourceId(\"com.PinKushin.PokemonBattleJournal:id/PlayerArchetype\"))")).Click();
                    driver.FindElement(MobileBy.AndroidUIAutomator(
                        "new UiSelector().resourceId(\"com.PinKushin.PokemonBattleJournal:id/ArchetypeItem_Other\")")).Click();

                    // Select "Other" for rival archetype
                    driver.FindElement(MobileBy.AndroidUIAutomator(
                        "new UiScrollable(new UiSelector().scrollable(true).instance(0))" +
                        ".scrollIntoView(new UiSelector().resourceId(\"com.PinKushin.PokemonBattleJournal:id/RivalArchetype\"))")).Click();
                    driver.FindElement(MobileBy.AndroidUIAutomator(
                        "new UiSelector().resourceId(\"com.PinKushin.PokemonBattleJournal:id/ArchetypeItem_Other\")")).Click();

                    // Type seed note
                    var noteInput = driver.FindElement(MobileBy.AndroidUIAutomator(
                        "new UiScrollable(new UiSelector().scrollable(true).instance(0))" +
                        ".scrollIntoView(new UiSelector().resourceId(\"com.PinKushin.PokemonBattleJournal:id/UserNoteInput\"))"));
                    noteInput.SendKeys($"UITestSeed-{i}");

                    // Save — wait for completion by confirming SaveMatchButton reappears (form cleared)
                    driver.FindElement(MobileBy.AndroidUIAutomator(
                        "new UiScrollable(new UiSelector().scrollable(true).instance(0))" +
                        ".scrollIntoView(new UiSelector().resourceId(\"com.PinKushin.PokemonBattleJournal:id/SaveMatchButton\"))")).Click();
                    driver.FindElement(MobileBy.AndroidUIAutomator(
                        "new UiScrollable(new UiSelector().scrollable(true).instance(0))" +
                        ".scrollIntoView(new UiSelector().resourceId(\"com.PinKushin.PokemonBattleJournal:id/SaveMatchButton\"))"));
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"SeedTestData failed: {ex.Message}", ex);
            }
        }

        private static void EnsureEmulatorRunning()
        {
            string androidHome = Environment.GetEnvironmentVariable("ANDROID_HOME")
                ?? Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT")
                ?? string.Empty;
            string emulatorExe = string.IsNullOrEmpty(androidHome)
                ? "emulator"
                : Path.Combine(androidHome, "emulator", "emulator");

            // Check if the correct AVD is already running.
            string devices = RunAdb("devices", 5_000);
            bool correctAvdRunning = false;
            foreach (string line in devices.Split('\n'))
            {
                string serial = line.Split('\t')[0].Trim();
                if (!serial.StartsWith("emulator-")) continue;
                string avdName = RunAdb($"-s {serial} emu avd name", 5_000).Split('\n')[0].Trim();
                if (avdName == AvdName) { correctAvdRunning = true; break; }
            }

            if (!correctAvdRunning)
            {
                // Launch the correct AVD — fire-and-forget; emulator stays alive until killed.
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = emulatorExe,
                    Arguments = $"-avd {AvdName} -no-snapshot-load",
                    UseShellExecute = false,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    CreateNoWindow = true,
                };
                System.Diagnostics.Process.Start(psi);
            }

            // Wait until Android reports fully booted.
            WaitForEmulatorBoot(timeoutSeconds: 240);

            // Skip uninstall — same debug keystore every run, no signing conflict.
            // MSBuild incremental build handles changed files; adb install -r replaces in place.
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
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ShutdownEmulator] {ex.Message}");
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
                // Read concurrently — WaitForExit deadlocks if output buffer fills (e.g. dumpsys output)
                var stdoutTask = Task.Run(() => proc.StandardOutput.ReadToEnd());
                proc.WaitForExit(timeoutMs);
                return stdoutTask.Result;
            }
            catch (Exception ex)
            {
                // adb not found or timed out — caller treats empty string as "not available"
                Console.Error.WriteLine($"[RunAdb '{arguments}'] {ex.Message}");
                return string.Empty;
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
            string apkPath = Path.Combine(repoRoot, "PokemonBattleJournal", "bin", config,
                "net10.0-android", "com.PinKushin.PokemonBattleJournal-Signed.apk");

            // Skip rebuild if APK is newer than every source file — EmbedAssembliesIntoApk builds
            // are slow (~7-18 min cold); skipping saves time when nothing changed.
            if (File.Exists(apkPath))
            {
                DateTime apkTime = File.GetLastWriteTimeUtc(apkPath);
                string srcDir = Path.Combine(repoRoot, "PokemonBattleJournal");
                bool anyNewer = Directory.EnumerateFiles(srcDir, "*", SearchOption.AllDirectories)
                    .Where(f => f.EndsWith(".cs") || f.EndsWith(".xaml") || f.EndsWith(".csproj"))
                    .Any(f => File.GetLastWriteTimeUtc(f) > apkTime);
                if (!anyNewer)
                {
                    Log($"4. BuildAndroidApk skipped — APK is fresh ({apkPath})");
                    return apkPath;
                }
            }

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                // EmbedAssembliesIntoApk: Appium only does adb install, never pushes Fast Deployment
                // assemblies separately. Without this, monodroid aborts "No assemblies found".
                // Not set in the .csproj so VS debug/hot-reload stays unaffected.
                Arguments = $"build \"{project}\" -f net10.0-android -c {config} -p:EmbedAssembliesIntoApk=true",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = repoRoot,
            };

            using var proc = System.Diagnostics.Process.Start(psi)!;
            // Read stdout/stderr concurrently — WaitForExit blocks if output buffer fills (deadlock)
            var stdoutTask = Task.Run(() => proc.StandardOutput.ReadToEnd());
            var stderrTask = Task.Run(() => proc.StandardError.ReadToEnd());
            bool exited = proc.WaitForExit(600_000); // 10-minute cap
            string stderr = stderrTask.Result;
            if (!exited)
                throw new TimeoutException("Android build timed out after 10 minutes.");
            if (proc.ExitCode != 0)
                throw new InvalidOperationException($"Android build failed (exit {proc.ExitCode}):\n{stderr}");

            if (!File.Exists(apkPath))
                throw new FileNotFoundException($"APK not found at expected path: {apkPath}");

            return apkPath;
        }
    }
}
