namespace UITests
{
    [SetUpFixture]
    public class AppiumSetup
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

        // CI runners only surface artifact-uploaded log files after the job finishes (or
        // times out) — no way to see progress mid-hang. Mirroring to the console gives live
        // visibility in the Actions log stream while a run is in progress (e.g. confirming
        // exactly which numbered setup step a stuck job is stuck on).
        private static readonly bool IsCi = Environment.GetEnvironmentVariable("CI") == "true";

        private static void Log(string message)
        {
            string line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
            if (IsCi) { Console.WriteLine($"[AppiumSetup] {line}"); Console.Out.Flush(); }
            File.AppendAllText(SetupLogPath, line + Environment.NewLine);
        }

        private static void PerfLog(string message)
        {
            string line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
            if (IsCi) { Console.WriteLine($"[PerfLog] {line}"); Console.Out.Flush(); }
            try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "UITests.PerfLog.txt"), line + Environment.NewLine); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        [OneTimeSetUp]
        public void RunBeforeAnyTests()
        {
            var setupTimer = System.Diagnostics.Stopwatch.StartNew();
            Log("START AppiumSetup.RunBeforeAnyTests");
            
            File.WriteAllText(SetupLogPath, $"=== AppiumSetup start {DateTime.Now:O} ==={Environment.NewLine}");
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "UITests.NavLog.txt"),
                $"=== Nav log start {DateTime.Now:O} ==={Environment.NewLine}");

            Log("1. EnsureAndroidToolsInPath");
            var step1Timer = System.Diagnostics.Stopwatch.StartNew();
            EnsureAndroidToolsInPath();
            step1Timer.Stop();
            Log($"1. EnsureAndroidToolsInPath done ({step1Timer.ElapsedMilliseconds}ms)");

            // CI: reactivecircus/android-emulator-runner boots the emulator before this
            // process starts and tears it down after — AppiumSetup must not manage the
            // lifecycle there. EnsureEmulatorRunning's AVD-name probe (`adb emu avd name`)
            // can catch the freshly-booted device in a transient "offline" state, read
            // garbage, conclude the right AVD isn't running, and launch a SECOND emulator
            // on the same AVD/port — observed on CI as persistent "adb: device offline"
            // at driver creation. Locally the check is what boots the AVD at all — keep it.
            if (IsCi)
            {
                Log("2. EnsureEmulatorRunning skipped (CI — emulator-runner owns the lifecycle)");
                WaitForEmulatorBoot(timeoutSeconds: 120);
            }
            else
            {
                Log("2. EnsureEmulatorRunning");
                var step2Timer = System.Diagnostics.Stopwatch.StartNew();
                EnsureEmulatorRunning();
                step2Timer.Stop();
                Log($"2. EnsureEmulatorRunning done ({step2Timer.ElapsedMilliseconds}ms)");
            }

            Log("3. StartAppiumLocalServer");
            var step3Timer = System.Diagnostics.Stopwatch.StartNew();
            AppiumServerHelper.StartAppiumLocalServer();
            step3Timer.Stop();
            Log($"3. StartAppiumLocalServer done ({step3Timer.ElapsedMilliseconds}ms)");

            // Default = true (safe path: force-stop + wipe .db3 only) so local Test Explorer
            // runs work without any env setup — the VS-deployed Fast Deployment overrides
            // survive the run. CI opts into the full rebuild + pm clear path by setting
            // ANDROID_USE_INSTALLED=0 in ui-tests-android.yml.
            bool useInstalled =
                Environment.GetEnvironmentVariable("ANDROID_USE_INSTALLED") != "0";

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
                    var step4Timer = System.Diagnostics.Stopwatch.StartNew();
                    string apkPath = BuildAndroidApk();
                    step4Timer.Stop();
                    Log($"4. BuildAndroidApk done: {apkPath} ({step4Timer.ElapsedMilliseconds}ms)");
                    PerfLog($"[{DateTime.Now:HH:mm:ss.fff}] BuildAndroidApk completed ({step4Timer.ElapsedMilliseconds}ms)");

                    // Install manually via adb — do NOT set androidOptions.App.
                    // When App is set, Appium takes ownership of the lifecycle and uninstalls on Quit().
                    // Without App, Appium launches the already-installed package by AppPackage/AppActivity.
                    Log("4b. Installing APK via adb");
                    var step4bTimer = System.Diagnostics.Stopwatch.StartNew();
                    RunAdb($"install -r \"{apkPath}\"", timeoutMs: 120_000);
                    step4bTimer.Stop();
                    Log($"4b. APK installed ({step4bTimer.ElapsedMilliseconds}ms)");

                    androidOptions.AddAdditionalAppiumOption("noReset", true);
                    androidOptions.AddAdditionalAppiumOption("skipDeviceInitialization", true);
                }

            androidOptions.AddAdditionalAppiumOption(AndroidMobileCapabilityType.AppPackage, AppPackage);
            androidOptions.AddAdditionalAppiumOption(AndroidMobileCapabilityType.AppActivity, $"{AppPackage}.MainActivity");
            androidOptions.AddAdditionalAppiumOption("appWaitActivity", $"{AppPackage}.MainActivity");
            androidOptions.AddAdditionalAppiumOption("appWaitDuration", 60_000);

            if (useInstalled)
            {
                // VS Fast Deployment stores assemblies in .__override__/ inside app data.
                // pm clear would wipe them and crash the app on next launch.
                // Instead: force-stop then delete only the SQLite DB for a clean seed state.
                Log("4d. force-stop + wipe DB (VS fast-deploy safe)");
                RunAdb($"shell am force-stop {AppPackage}", timeoutMs: 5_000);
                RunAdb($"shell rm -f /data/data/{AppPackage}/files/*.db3", timeoutMs: 5_000);
                Log("4d. wipe DB done");
            }
            else
            {
                // EmbedAssembliesIntoApk=true build — assemblies are in the APK, not .__override__/.
                // pm clear is safe and gives the cleanest possible reset.
                Log("4d. pm clear app data");
                RunAdb($"shell pm clear {AppPackage}", timeoutMs: 10_000);
                Log("4d. pm clear done");
            }

            Log("5. new AndroidDriver");
            var step5Timer = System.Diagnostics.Stopwatch.StartNew();
            // Driver-creation retry: UIAutomator2's session init shells settings commands
            // via adb, and on a freshly-booted CI emulator adbd can be starved enough to
            // report "device offline" transiently — observed twice (runs 30984076303,
            // 30989085956) killing an entire fixture from OneTimeSetUp. WaitForEmulatorBoot
            // between attempts polls until adb responds sanely again.
            const int driverAttempts = 3;
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    driver = new AndroidDriver(
                        new Uri("http://127.0.0.1:4723/"),
                        androidOptions,
                        TimeSpan.FromMinutes(5));
                    break;
                }
                catch (OpenQA.Selenium.WebDriverException ex) when (attempt < driverAttempts)
                {
                    Log($"5. AndroidDriver attempt {attempt} failed: {ex.GetType().Name}: {ex.Message.Split('\n')[0]} — re-checking boot, retrying");
                    WaitForEmulatorBoot(timeoutSeconds: 60);
                }
            }
            step5Timer.Stop();
            Log($"5. AndroidDriver created ({step5Timer.ElapsedMilliseconds}ms)");
            PerfLog($"[{DateTime.Now:HH:mm:ss.fff}] AndroidDriver instantiated ({step5Timer.ElapsedMilliseconds}ms)");
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);

            Log("6. WaitForActivity");
            var step6Timer = System.Diagnostics.Stopwatch.StartNew();
            WaitForActivity($"{AppPackage}.MainActivity", timeoutSeconds: 60);
            step6Timer.Stop();
            Log($"6. WaitForActivity done ({step6Timer.ElapsedMilliseconds}ms)");
            PerfLog($"[{DateTime.Now:HH:mm:ss.fff}] AndroidWaitForActivity completed ({step6Timer.ElapsedMilliseconds}ms)");

            // 7. App-ready gate: the activity being resumed does not mean the Shell is
            // composed. On a fresh CI emulator the first launch runs DebugDataSeeder
            // (blocking, on startup) plus a possible Limitless meta fetch before the first
            // page renders — observed on CI as the flyout hamburger staying out of the UIA
            // tree for 30s+ (every AboutPageTests nav retry exhausted at exactly 3×10s on
            // one run, while the identical job passed in 1s on others). Give the cold
            // start its runway ONCE here, so per-nav retries can stay tight.
            Log("7. WaitForAppReady (flyout hamburger in UIA tree)");
            var step7Timer = System.Diagnostics.Stopwatch.StartNew();
            var readyDeadline = DateTime.UtcNow.AddSeconds(90);
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(2);
            try
            {
                bool ready = false;
                while (DateTime.UtcNow < readyDeadline)
                {
                    if (driver.FindElements(MobileBy.AccessibilityId("Open navigation drawer")).Count > 0)
                    {
                        ready = true;
                        break;
                    }

                    // A system ANR dialog (observed: launcher/"Quickstep isn't responding"
                    // during the boot storm) owns the entire accessibility tree while our
                    // app renders underneath — no element of ours is reachable until it's
                    // gone. hide_error_dialogs=1 (set in the workflow) only prevents FUTURE
                    // dialogs; one already showing must be dismissed here. AOSP's ANR
                    // dialog exposes its Wait button as android:id/aerr_wait.
                    var anrWait = driver.FindElements(MobileBy.AndroidUIAutomator(
                        "new UiSelector().resourceId(\"android:id/aerr_wait\")"));
                    if (anrWait.Count > 0)
                    {
                        Log("7. WaitForAppReady: ANR dialog present — clicking Wait to dismiss");
                        try { anrWait[0].Click(); }
                        catch (OpenQA.Selenium.WebDriverException ex)
                        {
                            // Dialog may vanish between find and click — keep polling either way.
                            Log($"7. WaitForAppReady: ANR dismiss click failed: {ex.GetType().Name}");
                        }
                    }
                }
                step7Timer.Stop();
                if (!ready)
                {
                    // Diagnostic dump before failing: one CI instance (run 30986172378,
                    // ReadJournalPageTests) had the app fully rendered — MainPage composed,
                    // Limitless fetched, trainer loaded per logcat — yet "Open navigation
                    // drawer" returned "Found zero matches" in 4ms for 90 straight seconds,
                    // while the identical APK exposed it instantly on 4 sibling emulators.
                    // Capture what the toolbar/tree actually contains so the next occurrence
                    // is diagnosable instead of guessable.
                    try
                    {
                        var descs = driver.FindElements(MobileBy.AndroidUIAutomator(
                            "new UiSelector().descriptionMatches(\".+\")"));
                        Log($"7. GATE TIMEOUT: {descs.Count} elements with content-desc:");
                        foreach (var el in descs.Take(25))
                        {
                            try { Log($"     desc='{el.GetDomAttribute("content-desc")}' class='{el.GetDomAttribute("className")}'"); }
                            catch (OpenQA.Selenium.StaleElementReferenceException) { Log("     <stale>"); }
                        }
                        string src = driver.PageSource ?? "";
                        Log($"7. GATE TIMEOUT: PageSource first 4000 chars:\n{src[..Math.Min(4000, src.Length)]}");
                    }
                    catch (OpenQA.Selenium.WebDriverException ex)
                    {
                        Log($"7. GATE TIMEOUT: diagnostic dump failed: {ex.GetType().Name}: {ex.Message.Split('\n')[0]}");
                    }
                    throw new InvalidOperationException(
                        $"App not ready: flyout hamburger never appeared within 90s of activity start ({step7Timer.ElapsedMilliseconds}ms elapsed).");
                }
                Log($"7. WaitForAppReady done ({step7Timer.ElapsedMilliseconds}ms)");
                PerfLog($"[{DateTime.Now:HH:mm:ss.fff}] WaitForAppReady completed ({step7Timer.ElapsedMilliseconds}ms)");
            }
            finally
            {
                driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);
            }
        }

        [OneTimeTearDown]
        public void RunAfterAllTests()
        {
            var cleanupTimer = System.Diagnostics.Stopwatch.StartNew();

            // Pull the in-app ComboBoxControl popup lifecycle log before force-stopping.
            // App writes to FileSystem.CacheDirectory; use run-as to read from app-private
            // cache dir. Empty output = OnTapped never fired (popup click not registering).
            Log("Tearing down: Pull combobox_popup.log");
            try
            {
                string destPath = Path.Combine(Path.GetTempPath(), "UITests.PopupLog.txt");
                if (File.Exists(destPath)) File.Delete(destPath);
                string cat = RunAdb($"shell run-as {AppPackage} cat /data/data/{AppPackage}/cache/combobox_popup.log", timeoutMs: 5_000);
                if (!string.IsNullOrWhiteSpace(cat))
                {
                    File.WriteAllText(destPath, cat);
                    Log($"Tearing down: Pulled combobox_popup.log ({cat.Length} chars) → {destPath}");
                }
                else Log("Tearing down: combobox_popup.log empty or missing on device");
            }
            catch (Exception ex) { Log($"Tearing down: pull popup log failed: {ex.GetType().Name}: {ex.Message}"); }

            Log("Tearing down: Force-stop app");
            var stopAppTimer = System.Diagnostics.Stopwatch.StartNew();
            RunAdb($"shell am force-stop {AppPackage}", timeoutMs: 5_000);
            stopAppTimer.Stop();
            Log($"Tearing down: Force-stop app done ({stopAppTimer.ElapsedMilliseconds}ms)");
            
            Log("Tearing down: Quit driver");
            var quitDriverTimer = System.Diagnostics.Stopwatch.StartNew();
            driver?.Quit();
            quitDriverTimer.Stop();
            Log($"Tearing down: Quit driver done ({quitDriverTimer.ElapsedMilliseconds}ms)");
            
            Log("Tearing down: Dispose AppiumServer");
            AppiumServerHelper.DisposeAppiumLocalServer();
            
            // CI: leave the emulator alive — the emulator-runner action's own Terminate
            // Emulator step handles it. Killing it here made every adb call between our
            // teardown and the action's (the workflow's logcat capture, the action's own
            // `adb emu kill`) hit a dead/offline device — the source of the
            // "- waiting for device -" job hangs.
            if (IsCi)
            {
                Log("Tearing down: Shutdown emulator skipped (CI — emulator-runner owns the lifecycle)");
            }
            else
            {
                Log("Tearing down: Shutdown emulator");
                var shutdownEmulatorTimer = System.Diagnostics.Stopwatch.StartNew();
                ShutdownEmulator();
                shutdownEmulatorTimer.Stop();
                Log($"Tearing down: Shutdown emulator done ({shutdownEmulatorTimer.ElapsedMilliseconds}ms)");
            }
            
            cleanupTimer.Stop();
            PerfLog($"[{DateTime.Now:HH:mm:ss.fff}] Android AppiumSetup cleanup complete ({cleanupTimer.ElapsedMilliseconds}ms)");
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
