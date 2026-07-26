namespace PokemonBattleJournal
{

    public partial class App : Application
    {
        private readonly ILogger<App> _logger;
        private readonly AppShell _shell;

        public App(ILogger<App> logger, AppShell shell)
        {
            _logger = logger;
            _shell = shell;
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            if (PreferencesHelper.GetSetting("FirstStart") != "false")
            {
                return new Window(new FirstStartPage(new FirstStartPageViewModel()));
            }

            return new Window(_shell);
        }
    }
}