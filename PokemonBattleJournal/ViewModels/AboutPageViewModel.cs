namespace PokemonBattleJournal.ViewModels
{
    public partial class AboutPageViewModel : ObservableObject
    {
        private readonly ILogger<AboutPageViewModel> _logger;
        public AboutPageViewModel(ILogger<AboutPageViewModel> logger)
        {
            _logger = logger;
        }
    }
}