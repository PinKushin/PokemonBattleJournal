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

        protected override Window CreateWindow(IActivationState? activationState) => new(_shell);
    }
}