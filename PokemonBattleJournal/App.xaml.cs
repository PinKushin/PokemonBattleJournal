using System.Globalization;

namespace PokemonBattleJournal
{

    public partial class App : Application
    {
        private readonly ILogger<App> _logger;
        private readonly AppShell _shell;

        public App(ILogger<App> logger, AppShell shell, ISqliteConnectionFactory factory)
        {
            _logger = logger;
            _shell = shell;
            InitializeComponent();
#if DEBUG
            Task.Run(() => DevSeed.DebugDataSeeder.SeedAsync(factory, _logger)).GetAwaiter().GetResult();
#endif
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            Window window = new(_shell);
            ApplyWindowSizeOverride(window);
            return window;
        }

        /// <summary>
        /// Sizes the window from <c>PBJ_WINDOW_SIZE</c> (e.g. <c>754x512</c>) when it is set.
        /// </summary>
        /// <remarks>
        /// For looking at the app the way CI sees it. The UI tests already honour
        /// <c>UITEST_WINDOW_SIZE</c>, but that only helps a test run — there was no way to just
        /// launch the app at CI's geometry and look at it, which is how the off-window click
        /// problem was found (docs/memory/project_windows_mainpage_click_flake.md).
        ///
        /// CI's Windows window is 754x512:
        /// <code>dotnet run --project PokemonBattleJournal/PokemonBattleJournal.csproj -f net10.0-windows10.0.19041.0</code>
        /// with <c>PBJ_WINDOW_SIZE=754x512</c> set.
        ///
        /// Unset means unchanged, so this costs nothing in normal use. Deliberately not
        /// Debug-only: reproducing a CI geometry against a Release build is exactly when it
        /// would be needed, and an env var nobody sets cannot affect a user.
        /// </remarks>
        private void ApplyWindowSizeOverride(Window window)
        {
            string? requested = Environment.GetEnvironmentVariable("PBJ_WINDOW_SIZE");
            if (string.IsNullOrWhiteSpace(requested))
                return;

            string[] parts = requested.Split('x', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2
                || !double.TryParse(parts[0], NumberStyles.Number, CultureInfo.InvariantCulture, out double width)
                || !double.TryParse(parts[1], NumberStyles.Number, CultureInfo.InvariantCulture, out double height)
                || width <= 0 || height <= 0)
            {
                // Surfaced rather than ignored: a typo here would otherwise look like the
                // override silently not working, and the whole point is trusting the geometry.
                _logger.LogWarning("PBJ_WINDOW_SIZE '{Requested}' is not in WIDTHxHEIGHT form — window size unchanged", requested);
                return;
            }

            window.Width = width;
            window.Height = height;
            window.X = 0;
            window.Y = 0;
            _logger.LogInformation("PBJ_WINDOW_SIZE applied: {Width}x{Height} at (0,0)", width, height);
        }
    }
}