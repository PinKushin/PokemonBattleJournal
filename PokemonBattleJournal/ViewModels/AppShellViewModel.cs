using System.Collections.ObjectModel;

namespace PokemonBattleJournal.ViewModels
{
    public partial class AppShellViewModel : ObservableObject
    {
        private readonly ITrainerSwitchService _switchService;
        private readonly MainPageViewModel _mainPageVm;
        private readonly ILogger<AppShellViewModel> _logger;
        private bool _suppressSelectionChanged;

        public AppShellViewModel(
            ITrainerSwitchService switchService,
            MainPageViewModel mainPageVm,
            ILogger<AppShellViewModel> logger)
        {
            _switchService = switchService;
            _mainPageVm = mainPageVm;
            _logger = logger;
        }

        [ObservableProperty]
        public partial ObservableCollection<Trainer> Trainers { get; set; } = [];

        [ObservableProperty]
        public partial Trainer? SelectedTrainer { get; set; }

        partial void OnSelectedTrainerChanged(Trainer? value)
        {
            if (_suppressSelectionChanged || value is null)
                return;
            _ = SwitchTrainerAsync(value);
        }

        [RelayCommand]
        public async Task LoadAsync()
        {
            try
            {
                var all = await _switchService.GetAllTrainersAsync();
                _suppressSelectionChanged = true;
                Trainers = new ObservableCollection<Trainer>(all);
                var activeId = PreferencesHelper.GetTrainerId();
                SelectedTrainer = Trainers.FirstOrDefault(t => t.Id == activeId)
                    ?? Trainers.FirstOrDefault();
                _suppressSelectionChanged = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading trainers for shell picker");
            }
        }

        private async Task SwitchTrainerAsync(Trainer trainer)
        {
            if (_mainPageVm.HasUnsavedData)
            {
                bool confirmed = await Shell.Current.DisplayAlert(
                    "Switch Trainer",
                    "You have unsaved match data. Switch anyway?",
                    "Switch", "Cancel");

                if (!confirmed)
                {
                    // Revert picker to current trainer without triggering the handler
                    _suppressSelectionChanged = true;
                    var activeId = PreferencesHelper.GetTrainerId();
                    SelectedTrainer = Trainers.FirstOrDefault(t => t.Id == activeId);
                    _suppressSelectionChanged = false;
                    return;
                }
            }

            await _switchService.SwitchToAsync(trainer);
        }

        public void OnTrainerCreated(Trainer trainer)
        {
            if (Trainers.Any(t => t.Id == trainer.Id))
                return;
            Trainers.Add(trainer);
        }
    }
}
