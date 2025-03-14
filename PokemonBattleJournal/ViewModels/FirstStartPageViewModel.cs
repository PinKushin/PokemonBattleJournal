using Microsoft.Extensions.Logging;

namespace PokemonBattleJournal.ViewModels
{
    public partial class FirstStartPageViewModel : ObservableObject
    {
        public FirstStartPageViewModel()
        { }

        [ObservableProperty]
        public partial string? TrainerNameInput { get; set; }

        [RelayCommand]
        public void SaveTrainerName()
        {
            var logger = new Logger<FirstStartPageViewModel>(new LoggerFactory());
            logger.LogInformation("Created Trainer: {Name}", TrainerNameInput);
            if (TrainerNameInput != null && Application.Current != null)
            {
                PreferencesHelper.SetSetting("FirstStart", "false");
                PreferencesHelper.SetSetting("TrainerName", TrainerNameInput);
                Application.Current.Windows[0].Page = new AppShell();
            }
        }
    }
}