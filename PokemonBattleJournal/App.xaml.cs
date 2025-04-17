namespace PokemonBattleJournal
{

    public partial class App : Application
    {
        private readonly ILogger<App> _logger;

        public App(ILogger<App> logger)
        {
            //Register Syncfusion license
            Syncfusion
                .Licensing
                .SyncfusionLicenseProvider
                .RegisterLicense(
                "Ngo9BigBOggjHTQxAR8/V1NNaF5cXmBCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdmWXxceHVSRGZdVE11VkRWYUA="
                );
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