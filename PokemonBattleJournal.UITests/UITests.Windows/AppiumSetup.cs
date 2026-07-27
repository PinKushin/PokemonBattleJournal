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
        }

        public void Dispose()
        {
            driver?.Quit();
            AppiumServerHelper.DisposeAppiumLocalServer();
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
