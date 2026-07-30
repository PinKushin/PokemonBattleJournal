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
            _switchService.TrainerChanged += (_, trainer) =>
            {
                _suppressSelectionChanged = true;
                SelectedTrainer = Trainers.FirstOrDefault(t => t.Id == trainer.Id);
                _suppressSelectionChanged = false;
                IsTrainerMenuOpen = false;
            };
        }

        [ObservableProperty]
        public partial bool IsTrainerMenuOpen { get; set; }

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
                await _switchService.InitializeAsync();
                var all = await _switchService.GetAllTrainersAsync();
                _suppressSelectionChanged = true;
                Trainers = new ObservableCollection<Trainer>(all);
                SelectedTrainer = Trainers.FirstOrDefault(t => t.Id == (_switchService.ActiveTrainer?.Id ?? 0))
                    ?? Trainers.FirstOrDefault();
                _suppressSelectionChanged = false;
                // If no trainer was flagged active, persist the fallback choice
                if (_switchService.ActiveTrainer is null && SelectedTrainer is not null)
                    await _switchService.SwitchToAsync(SelectedTrainer);
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
                bool confirmed = await Shell.Current.DisplayAlertAsync(
                    "Switch Trainer",
                    "You have unsaved match data. Switch anyway?",
                    "Switch", "Cancel");

                if (!confirmed)
                {
                    _suppressSelectionChanged = true;
                    SelectedTrainer = Trainers.FirstOrDefault(t => t.Id == (_switchService.ActiveTrainer?.Id ?? 0));
                    _suppressSelectionChanged = false;
                    return;
                }
            }

            await _switchService.SwitchToAsync(trainer);
        }

        [RelayCommand]
        public void ToggleTrainerMenu() => IsTrainerMenuOpen = !IsTrainerMenuOpen;

        [RelayCommand]
        public async Task SelectTrainerAsync(Trainer trainer)
        {
            if (trainer is null || trainer.Id == (SelectedTrainer?.Id ?? 0))
            {
                IsTrainerMenuOpen = false;
                return;
            }
            await SwitchTrainerAsync(trainer);
        }

        [RelayCommand]
        public async Task NavigateAsync(string route)
        {
            await Shell.Current.GoToAsync(route);
            Shell.Current.FlyoutIsPresented = false;
        }

        public void OnTrainerCreated(Trainer trainer)
        {
            if (Trainers.Any(t => t.Id == trainer.Id))
                return;
            Trainers.Add(trainer);
        }
    }
}
