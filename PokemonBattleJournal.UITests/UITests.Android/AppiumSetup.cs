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

                // Install manually via adb — do NOT set androidOptions.App.
                // When App is set, Appium takes ownership of the lifecycle and uninstalls on Quit().
                // Without App, Appium launches the already-installed package by AppPackage/AppActivity.
                Log("4b. Installing APK via adb");
                RunAdb($"install -r \"{apkPath}\"", timeoutMs: 120_000);
                Log("4b. APK installed");

                androidOptions.AddAdditionalAppiumOption("noReset", true);
                androidOptions.AddAdditionalAppiumOption("skipDeviceInitialization", true);
            }

            androidOptions.AddAdditionalAppiumOption(AndroidMobileCapabilityType.AppPackage, AppPackage);
            androidOptions.AddAdditionalAppiumOption(AndroidMobileCapabilityType.AppActivity, $"{AppPackage}.MainActivity");
            androidOptions.AddAdditionalAppiumOption("appWaitActivity", $"{AppPackage}.MainActivity");
            androidOptions.AddAdditionalAppiumOption("appWaitDuration", 60_000);

            // pm clear wipes all app data so seed always starts from a known state.
            // Safe with EmbedAssembliesIntoApk=true — assemblies are in the APK, not .__override__/.
            Log("4d. pm clear app data");
            RunAdb($"shell pm clear {AppPackage}", timeoutMs: 10_000);
            Log("4d. pm clear done");

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
        }

        public void Dispose()
        {
            // Force-stop the app before closing the session so it doesn't stay running
            RunAdb($"shell am force-stop {AppPackage}", timeoutMs: 5_000);
            driver?.Quit();
            AppiumServerHelper.DisposeAppiumLocalServer();
            ShutdownEmulator();
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

            // Skip rebuild only if:
            //   (a) APK exists AND was produced by this test runner (sentinel present), AND
            //   (b) no source file is newer than the APK.
            // Sentinel prevents a VS fast-deploy APK (different build flags/state) from being reused.
            string sentinelPath = apkPath + ".uitest";
            if (File.Exists(apkPath) && File.Exists(sentinelPath))
            {
                DateTime apkTime = File.GetLastWriteTimeUtc(apkPath);
                string srcDir = Path.Combine(repoRoot, "PokemonBattleJournal");
                bool anyNewer = Directory.EnumerateFiles(srcDir, "*", SearchOption.AllDirectories)
                    .Where(f => f.EndsWith(".cs") || f.EndsWith(".xaml") || f.EndsWith(".csproj"))
                    .Any(f => File.GetLastWriteTimeUtc(f) > apkTime);
                if (!anyNewer)
                {
                    Log($"4. BuildAndroidApk skipped — APK is fresh and test-built ({apkPath})");
                    return apkPath;
                }
            }

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                // EmbedAssembliesIntoApk required: Appium installs via adb but never pushes
                // fast-deploy assemblies to files/.__override__/. Without this flag, monodroid
                // aborts "No assemblies found" on launch. Not set in .csproj so VS stays unaffected.
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
            string stdout = stdoutTask.Result;
            string stderr = stderrTask.Result;
            if (!exited)
                throw new TimeoutException("Android build timed out after 10 minutes.");
            if (proc.ExitCode != 0)
                throw new InvalidOperationException($"Android build failed (exit {proc.ExitCode}):\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");

            if (!File.Exists(apkPath))
                throw new FileNotFoundException($"APK not found at expected path: {apkPath}");

            // Mark this APK as test-built so the skip check won't confuse it with a VS fast-deploy build.
            File.WriteAllText(sentinelPath, DateTime.UtcNow.ToString("O"));

            return apkPath;
        }
    }
}
