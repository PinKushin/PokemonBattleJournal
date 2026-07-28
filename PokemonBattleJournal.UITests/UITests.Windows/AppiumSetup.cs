namespace UITests
{
    public class AppiumSetup : IDisposable
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

        public AppiumSetup()
        {
            RunBeforeAnyTests();
        }

        private static readonly string UiTestSentinelPath =
            Path.Combine(Path.GetTempPath(), "PokemonBattleJournal.uitest");

        public void RunBeforeAnyTests()
        {
            // Signal the app to suppress the first-boot prompt (avoids XamlRoot crash)
            File.WriteAllText(UiTestSentinelPath, "1");

            // Port 4724 so Windows and Android Appium servers don't conflict when the full suite runs in parallel
            AppiumServerHelper.StartAppiumLocalServer(port: 4724);

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
                // Attach to VS-launched app — skip build
                windowsOptions.AddAdditionalAppiumOption(
                    "appium:appTopLevelWindow",
                    "0x" + runningProc!.MainWindowHandle.ToString("X"));
            }
            else
            {
                // Kill any leftover crash remnants
                foreach (var proc in System.Diagnostics.Process.GetProcessesByName("PokemonBattleJournal"))
                    try { proc.Kill(true); proc.WaitForExit(5_000); } catch { }

                string exePath = BuildWindowsApp();
                // No pre-wipe — app seeds UITestTrainer in #if DEBUG if absent (idempotent)
                windowsOptions.App = exePath;
            }

            driver = new WindowsDriver(new Uri("http://127.0.0.1:4724/"), windowsOptions);
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(15);
        }

        public void Dispose()
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
