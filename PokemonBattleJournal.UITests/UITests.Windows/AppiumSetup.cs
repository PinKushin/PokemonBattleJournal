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

        public void RunBeforeAnyTests()
        {
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
                // Attach to VS-launched app — no wipe, no build
                windowsOptions.AddAdditionalAppiumOption(
                    "appium:appTopLevelWindow",
                    "0x" + runningProc!.MainWindowHandle.ToString("X"));
            }
            else
            {
                // Kill any leftover crash remnants before touching files
                foreach (var proc in System.Diagnostics.Process.GetProcessesByName("PokemonBattleJournal"))
                    try { proc.Kill(true); proc.WaitForExit(5_000); } catch { }

                string exePath = BuildWindowsApp();

                // Wipe the DB before launch so every run starts with a clean slate
                WipeAppData();

                windowsOptions.App = exePath;
            }

            driver = new WindowsDriver(new Uri("http://127.0.0.1:4724/"), windowsOptions);
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(15);

            SeedTestData();
        }

        public void Dispose()
        {
            driver?.Quit();
            if (!_attachedToExisting)
            {
                // Only kill if we launched it — don't kill the debugger session
                foreach (var proc in System.Diagnostics.Process.GetProcessesByName("PokemonBattleJournal"))
                {
                    try { proc.Kill(); } catch { }
                }
            }
            AppiumServerHelper.DisposeAppiumLocalServer();
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

        private static void WipeAppData()
        {
            // Delete the SQLite DB and MAUI preferences before the app launches so every run starts clean.
            // DB is in {LocalAppData}\...\Data\PokemonBattleJournal.db3
            // Preferences are in {LocalAppData}\...\Settings\preferences.dat (MAUI unpackaged Windows)
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            foreach (string db in FindFiles(localAppData, "PokemonBattleJournal.db3"))
            {
                try
                {
                    File.Delete(db);
                    string? dataDir = Path.GetDirectoryName(db);
                    string? appRoot = Path.GetDirectoryName(dataDir);
                    string prefsFile = Path.Combine(appRoot ?? "", "Settings", "preferences.dat");
                    if (File.Exists(prefsFile)) File.Delete(prefsFile);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[WipeAppData] Could not delete {db}: {ex.Message}");
                }
            }
        }

        private static void SeedTestData()
        {
            // Fresh install starts on Journal Entry with the first-boot Welcome prompt blocking the UI.
            // Handle the prompt first, then seed matches. No need to visit Options — the prompt
            // creates the trainer directly.
            try
            {
                // 1. Handle the Welcome prompt if it appears (first clean boot only).
                //    Use a short timeout to detect the dialog without stalling if trainer already exists.
                driver!.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(3);
                try
                {
                    // Confirm the ContentDialog is present by finding its Save button
                    AppiumElement saveBtn = driver.FindElement(MobileBy.Name("Save"));

                    // The dialog's TextBox has no AutomationId — all our form inputs do.
                    // Filter to the unnamed TextBox so we don't type into UserNoteInput behind the dialog.
                    var allBoxes = driver.FindElements(MobileBy.ClassName("TextBox"));
                    AppiumElement dialogBox = allBoxes
                        .FirstOrDefault(b => string.IsNullOrEmpty(b.GetAttribute("AutomationId")))
                        ?? allBoxes.First();
                    dialogBox.SendKeys("UITestTrainer");
                    saveBtn.Click();
                    // Wait for the trainer to be created before proceeding — find a known MainPage element
                    driver.FindElement(MobileBy.AccessibilityId("SaveMatchButton"));
                }
                catch (OpenQA.Selenium.NoSuchElementException)
                {
                    // No welcome dialog — trainer already exists from a previous run, continue seeding
                }
                finally
                {
                    driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(15);
                }

                // 2. Now on Journal Entry — seed 3 matches. Save clears the form so no nav needed between iterations.
                for (int i = 1; i <= 3; i++)
                {
                    var resultPicker = driver.FindElement(MobileBy.AccessibilityId("PossibleResultsPicker"));
                    resultPicker.Click();
                    driver.FindElement(MobileBy.Name("Win")).Click();

                    // Select "Other" for player archetype — SaveMatchAsync rejects null PlayerSelected
                    driver.FindElement(MobileBy.AccessibilityId("PlayerArchetype")).Click();
                    driver.FindElement(MobileBy.AccessibilityId("ArchetypeItem_Other")).Click();

                    // Select "Other" for rival archetype
                    driver.FindElement(MobileBy.AccessibilityId("RivalArchetype")).Click();
                    driver.FindElement(MobileBy.AccessibilityId("ArchetypeItem_Other")).Click();

                    var noteInput = driver.FindElement(MobileBy.AccessibilityId("UserNoteInput"));
                    noteInput.Clear();
                    noteInput.SendKeys($"UITestSeed-{i}");

                    driver.FindElement(MobileBy.AccessibilityId("SaveMatchButton")).Click();
                    // Wait for save to complete — form clears and SaveMatchButton reappears
                    driver.FindElement(MobileBy.AccessibilityId("SaveMatchButton"));
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"SeedTestData failed: {ex.Message}", ex);
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
