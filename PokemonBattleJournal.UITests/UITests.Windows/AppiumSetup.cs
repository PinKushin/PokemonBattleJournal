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
            // Kill any leftover app process from a previous crash before touching files or starting servers
            foreach (var proc in System.Diagnostics.Process.GetProcessesByName("PokemonBattleJournal"))
                try { proc.Kill(true); proc.WaitForExit(5_000); } catch { }

            // Port 4724 so Windows and Android Appium servers don't conflict when the full suite runs in parallel
            AppiumServerHelper.StartAppiumLocalServer(port: 4724);

            string exePath = BuildWindowsApp();

            // Wipe the DB before launch so every run starts with a clean slate
            WipeAppData();

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
            driver?.Quit();
            // Force-kill the MAUI exe in case WinAppDriver left it running
            foreach (var proc in System.Diagnostics.Process.GetProcessesByName("PokemonBattleJournal"))
            {
                try { proc.Kill(); } catch { }
            }
            AppiumServerHelper.DisposeAppiumLocalServer();
        }

        private static void WipeAppData()
        {
            // Delete the SQLite DB and MAUI preferences before the app launches so every run starts clean.
            // DB is in {LocalAppData}\...\Data\PokemonBattleJournal.db3
            // Preferences are in {LocalAppData}\...\Settings\preferences.dat (MAUI unpackaged Windows)
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string[] dbs = Directory.GetFiles(localAppData, "PokemonBattleJournal.db3", SearchOption.AllDirectories);
                foreach (string db in dbs)
                {
                    File.Delete(db);
                    // preferences.dat lives in a Settings/ sibling of the Data/ folder
                    string? dataDir = Path.GetDirectoryName(db);
                    string? appRoot = Path.GetDirectoryName(dataDir);
                    string prefsFile = Path.Combine(appRoot ?? "", "Settings", "preferences.dat");
                    if (File.Exists(prefsFile)) File.Delete(prefsFile);
                }
            }
            catch
            {
                // Best effort — if delete fails the app will just open with existing data
            }
        }

        private static void SeedTestData()
        {
            // Fresh install starts on Journal Entry with the first-boot Welcome prompt blocking the UI.
            // Handle the prompt first, then seed matches. No need to visit Options — the prompt
            // creates the trainer directly.
            try
            {
                // 1. The Welcome prompt appears immediately: type trainer name and save.
                // On WinUI DisplayPromptAsync renders as a ContentDialog; the Entry is a TextBox.
                var promptInput = driver!.FindElement(MobileBy.ClassName("TextBox"));
                promptInput.SendKeys("UITestTrainer");
                Thread.Sleep(300);
                driver.FindElement(MobileBy.Name("Save")).Click();
                Thread.Sleep(800);

                // 2. Now on Journal Entry — seed 3 matches
                for (int i = 1; i <= 3; i++)
                {
                    if (i > 1)
                    {
                        // Navigate back to Journal Entry after previous save
                        driver.FindElement(MobileBy.AccessibilityId("OK")).Click();
                        Thread.Sleep(500);
                        driver.FindElement(MobileBy.AccessibilityId("Journal Entry")).Click();
                        Thread.Sleep(800);
                    }

                    var resultPicker = driver.FindElement(MobileBy.AccessibilityId("PossibleResultsPicker"));
                    resultPicker.Click();
                    Thread.Sleep(500);
                    driver.FindElement(MobileBy.Name("Win")).Click();
                    Thread.Sleep(300);

                    var noteInput = driver.FindElement(MobileBy.AccessibilityId("UserNoteInput"));
                    noteInput.Clear();
                    noteInput.SendKeys($"UITestSeed-{i}");
                    Thread.Sleep(200);

                    driver.FindElement(MobileBy.AccessibilityId("SaveMatchButton")).Click();
                    Thread.Sleep(800);
                }
            }
            catch
            {
                // Seed failure is non-fatal — tests degrade gracefully without data
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
