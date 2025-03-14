using Microsoft.Extensions.Logging;

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
                "MzY3NjYwMkAzMjM4MmUzMDJlMzBuWkMvdUxhYkNPWERMQndYazNyU1gzWnVoN29Zb2dxU1AxQTk2K0k3aXNFPQ=="
                );
            _logger = logger;
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            if (PreferencesHelper.GetSetting("FirstStart") != "false")
                return new Window(new FirstStartPage(new FirstStartPageViewModel()));
            return new Window(new AppShell());
        }
    }
}