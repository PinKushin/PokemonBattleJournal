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
            RunBeforeAnyTests();
        }

        public void RunBeforeAnyTests()
        {
            // Port 4724 so Windows and Android Appium servers don't conflict when the full suite runs in parallel
            AppiumServerHelper.StartAppiumLocalServer(port: 4724);

            string exePath = BuildWindowsApp();

            AppiumOptions windowsOptions = new()
            {
                AutomationName = "windows",
                PlatformName = "Windows",
                DeviceName = "WindowsPC",
                App = exePath,
            };

            driver = new WindowsDriver(new Uri("http://127.0.0.1:4724/"), windowsOptions);
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(15);

            SeedTestData();
        }

        public void Dispose()
        {
            CleanSeedData();
            driver?.Quit();
            AppiumServerHelper.DisposeAppiumLocalServer();
        }

        private static void SeedTestData()
        {
            // Set a known trainer name then seed 3 matches so ReadJournal/TrainerPage always have data.
            try
            {
                // 1. Navigate to Options and set trainer name
                var menu = driver!.FindElement(MobileBy.AccessibilityId("OK"));
                menu.Click();
                Thread.Sleep(500);
                var optionsItem = driver.FindElement(MobileBy.AccessibilityId("Options"));
                optionsItem.Click();
                Thread.Sleep(800);

                var trainerInput = driver.FindElement(MobileBy.AccessibilityId("TrainerNameInput"));
                trainerInput.Clear();
                trainerInput.SendKeys("UITestTrainer");
                Thread.Sleep(300);

                var saveTrainerBtn = driver.FindElement(MobileBy.AccessibilityId("SaveTrainerNameButton"));
                saveTrainerBtn.Click();
                Thread.Sleep(500);

                // 2. Seed 3 matches on Journal Entry page
                for (int i = 1; i <= 3; i++)
                {
                    menu = driver.FindElement(MobileBy.AccessibilityId("OK"));
                    menu.Click();
                    Thread.Sleep(500);
                    var journalItem = driver.FindElement(MobileBy.AccessibilityId("Journal Entry"));
                    journalItem.Click();
                    Thread.Sleep(800);

                    var resultPicker = driver.FindElement(MobileBy.AccessibilityId("PossibleResultsPicker"));
                    resultPicker.Click();
                    Thread.Sleep(500);
                    driver.FindElement(MobileBy.Name("Win")).Click();
                    Thread.Sleep(300);

                    var noteInput = driver.FindElement(MobileBy.AccessibilityId("UserNoteInput"));
                    noteInput.Clear();
                    noteInput.SendKeys($"UITestSeed-{i}");
                    Thread.Sleep(200);

                    var saveBtn = driver.FindElement(MobileBy.AccessibilityId("SaveMatchButton"));
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
            // Delete seeded rows from the SQLite DB directly (avoids needing sqlite3 CLI on Windows).
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string[] dbs = Directory.GetFiles(localAppData, "PokemonBattleJournal.db3", SearchOption.AllDirectories);
                string? dbPath = dbs.FirstOrDefault();
                if (dbPath == null) return;

                using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}");
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "DELETE FROM MatchEntry WHERE Notes LIKE '%UITestSeed%';";
                cmd.ExecuteNonQuery();
                cmd.CommandText = "DELETE FROM Tags WHERE Name LIKE 'UITestTag%';";
                cmd.ExecuteNonQuery();
            }
            catch
            {
                // Best effort — leftover rows don't break production
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
            proc.WaitForExit(300_000); // 5-minute cap

            if (proc.ExitCode != 0)
            {
                string err = proc.StandardError.ReadToEnd();
                throw new InvalidOperationException($"Windows build failed (exit {proc.ExitCode}):\n{err}");
            }

            string exePath = Path.Combine(repoRoot, "PokemonBattleJournal", "bin", config, framework, rid, "PokemonBattleJournal.exe");

            if (!File.Exists(exePath))
                throw new FileNotFoundException($"Built exe not found at expected path: {exePath}");

            return exePath;
        }
    }
}
