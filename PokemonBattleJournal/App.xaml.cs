namespace PokemonBattleJournal
{

    public partial class App : Application
    {
        private readonly ILogger<App> _logger;

        public App(ILogger<App> logger)
        {
            _logger = logger;
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            if (PreferencesHelper.GetSetting("FirstStart") != "false")
            {
                return new Window(new FirstStartPage(new FirstStartPageViewModel()));
            }

            return new Window(new AppShell());
        }
    }
}