namespace UITests
{
    [SetUpFixture]
    public class AppiumSetup
    {
        private static AppiumDriver? driver;
        private bool _attachedToExisting;

        public static AppiumDriver App
        {
            get
            {
                return driver ?? throw new NullReferenceException("AppiumDriver is null");
            }
        }

        private static readonly string UiTestSentinelPath =
            Path.Combine(Path.GetTempPath(), "PokemonBattleJournal.uitest");

        // CI runners only surface artifact-uploaded log files after the job finishes (or
        // times out) — no way to see progress mid-hang. Mirroring to the console gives live
        // visibility in the Actions log stream while a run is in progress.
        private static readonly bool IsCi = Environment.GetEnvironmentVariable("CI") == "true";

        private static void Log(string message)
        {
            string line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
            if (IsCi) { Console.WriteLine($"[AppiumSetup] {line}"); Console.Out.Flush(); }
            try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "UITests.Windows.setup.log"), line + Environment.NewLine); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
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
            string logPrefix = $"[{DateTime.Now:HH:mm:ss.fff}]";
            
            // Signal the app to suppress the first-boot prompt (avoids XamlRoot crash)
            File.WriteAllText(UiTestSentinelPath, "1");
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "UITests.NavLog.txt"),
                $"=== Nav log start {DateTime.Now:O} ==={Environment.NewLine}");
            PerfLog($"{logPrefix} START Windows AppiumSetup");

            // Port 4724 so Windows and Android Appium servers don't conflict when the full suite runs in parallel
            Log("1. StartAppiumLocalServer");
            AppiumServerHelper.StartAppiumLocalServer(port: 4724);
            PerfLog($"{logPrefix} AppiumServer started ({setupTimer.ElapsedMilliseconds}ms)");

            var runningProc = System.Diagnostics.Process.GetProcessesByName("PokemonBattleJournal")
                .FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero);

            _attachedToExisting = runningProc != null;

            AppiumOptions windowsOptions = new()
            {
                AutomationName = "windows",
                PlatformName = "Windows",
                DeviceName = "WindowsPC",
            };

            if (_attachedToExisting)
            {
                Log("2. Attach to VS-launched app — skip build");
                // Attach to VS-launched app — skip build
                windowsOptions.AddAdditionalAppiumOption(
                    "appium:appTopLevelWindow",
                    "0x" + runningProc!.MainWindowHandle.ToString("X"));
            }
            else
            {
                Log("2. Kill crash remnants");
                // Kill any leftover crash remnants
                foreach (var proc in System.Diagnostics.Process.GetProcessesByName("PokemonBattleJournal"))
                    try { proc.Kill(true); proc.WaitForExit(5_000); } catch { }

                Log("3. WipeAppDatabase");
                WipeAppDatabase();

                Log("4. BuildWindowsApp");
                var buildTimer = System.Diagnostics.Stopwatch.StartNew();
                string exePath = BuildWindowsApp();
                buildTimer.Stop();
                Log($"4. BuildWindowsApp done ({buildTimer.ElapsedMilliseconds}ms)");
                PerfLog($"{logPrefix} BuildWindowsApp completed ({buildTimer.ElapsedMilliseconds}ms)");
                windowsOptions.App = exePath;
            }

            Log("5. new WindowsDriver");
            var driverTimer = System.Diagnostics.Stopwatch.StartNew();
            Exception? lastEx = null;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    driver = new WindowsDriver(new Uri("http://127.0.0.1:4724/"), windowsOptions);
                    lastEx = null;
                    break;
                }
                catch (Exception ex) when (attempt < 2)
                {
                    lastEx = ex;
                    Log($"4. WindowsDriver attempt {attempt + 1} failed: {ex.Message} — retrying in 5s...");
                    Task.Delay(5_000).Wait();
                }
            }
            if (lastEx != null) throw new InvalidOperationException("Failed to create WindowsDriver after 3 attempts", lastEx);
            driverTimer.Stop();
            Log($"5. WindowsDriver created ({driverTimer.ElapsedMilliseconds}ms)");
            PerfLog($"{logPrefix} WindowsDriver instantiated ({driverTimer.ElapsedMilliseconds}ms)");
            
            driver!.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);

            // Wait for Shell to be fully rendered before any test runs.
            // FindElement blocks until the element appears (up to 15s implicit wait).
            // The hamburger/OK menu is the first interactive UIA element — if it's visible
            // the Shell nav is wired up and NavigateTo will succeed immediately.
            Log("6. Wait for Shell");
            var shellWaitTimer = System.Diagnostics.Stopwatch.StartNew();
            driver.FindElement(MobileBy.AccessibilityId("OK"));
            shellWaitTimer.Stop();
            Log($"6. Shell ready ({shellWaitTimer.ElapsedMilliseconds}ms)");
            PerfLog($"{logPrefix} Shell ready ({shellWaitTimer.ElapsedMilliseconds}ms)");
            
            setupTimer.Stop();
            PerfLog($"{logPrefix} Windows AppiumSetup complete ({setupTimer.ElapsedMilliseconds}ms)");
        }

        [OneTimeTearDown]
        public void RunAfterAllTests()
        {
            driver?.Quit();
            if (!_attachedToExisting)
            {
                foreach (var proc in System.Diagnostics.Process.GetProcessesByName("PokemonBattleJournal"))
                    try { proc.Kill(); } catch { }
            }
            AppiumServerHelper.DisposeAppiumLocalServer();
            // Delete UITestTrainer (and its matches) after app stops — preserves manually-added trainers
            CleanupTestTrainer();
            // Remove sentinel so normal debug runs see the first-boot prompt
            try { File.Delete(UiTestSentinelPath); } catch { }
        }

        private static IEnumerable<string> FindFiles(string root, string pattern)
        {
            IEnumerable<string> entries;
            try { entries = Directory.EnumerateFiles(root, pattern); }
            catch { yield break; }
            foreach (string f in entries) yield return f;

            IEnumerable<string> dirs;
            try { dirs = Directory.EnumerateDirectories(root); }
            catch { yield break; }
            foreach (string dir in dirs)
                foreach (string f in FindFiles(dir, pattern))
                    yield return f;
        }

        private static void WipeAppDatabase()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string? dbPath = FindFiles(localAppData, "PokemonBattleJournal.db3").FirstOrDefault();
            if (dbPath == null) return;
            try
            {
                File.Delete(dbPath);
                Log($"WipeAppDatabase: deleted {dbPath}");
            }
            catch (Exception ex)
            {
                Log($"WipeAppDatabase failed: {ex.Message}");
            }
        }

        private static void CleanupTestTrainer()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string? dbPath = FindFiles(localAppData, "PokemonBattleJournal.db3").FirstOrDefault();
            if (dbPath == null) return;
            try
            {
                SQLitePCL.Batteries_V2.Init();
                using var conn = new SQLite.SQLiteConnection(dbPath);
                int trainerId = conn.ExecuteScalar<int>(
                    "SELECT COALESCE(Id, 0) FROM Trainer WHERE Name = 'UITestTrainer'");
                if (trainerId == 0) return;

                // Delete games linked to UITestTrainer's matches
                conn.Execute(
                    "DELETE FROM TagGame WHERE GameId IN (" +
                    "SELECT Game1Id FROM MatchEntry WHERE TrainerId = ? AND Game1Id IS NOT NULL UNION ALL " +
                    "SELECT Game2Id FROM MatchEntry WHERE TrainerId = ? AND Game2Id IS NOT NULL UNION ALL " +
                    "SELECT Game3Id FROM MatchEntry WHERE TrainerId = ? AND Game3Id IS NOT NULL)",
                    trainerId, trainerId, trainerId);
                conn.Execute(
                    "DELETE FROM Game WHERE Id IN (" +
                    "SELECT Game1Id FROM MatchEntry WHERE TrainerId = ? AND Game1Id IS NOT NULL UNION ALL " +
                    "SELECT Game2Id FROM MatchEntry WHERE TrainerId = ? AND Game2Id IS NOT NULL UNION ALL " +
                    "SELECT Game3Id FROM MatchEntry WHERE TrainerId = ? AND Game3Id IS NOT NULL)",
                    trainerId, trainerId, trainerId);
                conn.Execute("DELETE FROM MatchEntry WHERE TrainerId = ?", trainerId);
                conn.Execute("DELETE FROM Archetype WHERE TrainerId = ?", trainerId);
                conn.Execute("DELETE FROM Tags WHERE TrainerId = ?", trainerId);
                conn.Execute("DELETE FROM Trainer WHERE Id = ?", trainerId);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[CleanupTestTrainer] {ex.Message}");
            }
        }

        // AppContext.BaseDirectory = …\UITests.Windows\bin\<Config>\net10.0\
        // 5 hops up lands at repo root
        private static string RepoRoot => Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

        private static string BuildWindowsApp()
        {
            string repoRoot = RepoRoot;
            string project = Path.Combine(repoRoot, "PokemonBattleJournal", "PokemonBattleJournal.csproj");
#if DEBUG
            const string config = "Debug";
#else
            const string config = "Release";
#endif
            string framework = "net10.0-windows10.0.19041.0";
            string rid = "win-x64";

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"build \"{project}\" -f {framework} -c {config}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = repoRoot,
            };

            using var proc = System.Diagnostics.Process.Start(psi)!;
            // Read stdout and stderr concurrently — reading after WaitForExit deadlocks when buffers fill
            var stdoutTask = Task.Run(() => proc.StandardOutput.ReadToEnd());
            var stderrTask = Task.Run(() => proc.StandardError.ReadToEnd());
            bool exited = proc.WaitForExit(300_000); // 5-minute cap
            string stderr = stderrTask.Result;
            if (!exited)
                throw new TimeoutException("Windows build timed out after 5 minutes.");

            if (proc.ExitCode != 0)
                throw new InvalidOperationException($"Windows build failed (exit {proc.ExitCode}):\n{stderr}");

            string exePath = Path.Combine(repoRoot, "PokemonBattleJournal", "bin", config, framework, rid, "PokemonBattleJournal.exe");

            if (!File.Exists(exePath))
                throw new FileNotFoundException($"Built exe not found at expected path: {exePath}");

            return exePath;
        }
    }
}
