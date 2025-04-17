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
            Logger<FirstStartPageViewModel> logger = new(new LoggerFactory());
            if (TrainerNameInput != null && Application.Current != null)
            {
                PreferencesHelper.SetSetting("FirstStart", "false");
                PreferencesHelper.SetSetting("TrainerName", TrainerNameInput);
                Application.Current.Windows[0].Page = new AppShell();
            }
            logger.LogInformation("Created Trainer: {Name}", TrainerNameInput);
        }
    }
}