namespace PokemonBattleJournal
{

    public partial class App : Application
    {
        private readonly ILogger<App> _logger;
        private readonly AppShell _shell;
        private readonly FirstStartPage _firstStartPage;

        public App(ILogger<App> logger, AppShell shell, FirstStartPage firstStartPage)
        {
            _logger = logger;
            _shell = shell;
            _firstStartPage = firstStartPage;
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            if (PreferencesHelper.GetSetting("FirstStart") != "false")
                return new Window(_firstStartPage);

            return new Window(_shell);
        }
    }
}