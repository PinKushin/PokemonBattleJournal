namespace PokemonBattleJournal.ViewModels
{
    public partial class FirstStartPageViewModel : ObservableObject
    {
        private readonly ISqliteConnectionFactory _connection;
        private readonly ITrainerSwitchService _switchService;

        public FirstStartPageViewModel(ISqliteConnectionFactory connection, ITrainerSwitchService switchService)
        {
            _connection = connection;
            _switchService = switchService;
        }

        [ObservableProperty]
        public partial string? TrainerNameInput { get; set; }

        [RelayCommand]
        public async Task SaveTrainerName()
        {
            if (TrainerNameInput is null || Application.Current is null)
                return;

            PreferencesHelper.SetSetting("FirstStart", "false");

            await _connection.Trainers.SaveAsync(TrainerNameInput);
            Trainer? trainer = await _connection.Trainers.GetByNameAsync(TrainerNameInput);
            if (trainer != null)
                await _switchService.SwitchToAsync(trainer);

            Application.Current.Windows[0].Page = IPlatformApplication.Current!.Services.GetRequiredService<AppShell>();
        }
    }
}
