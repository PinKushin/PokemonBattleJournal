namespace PokemonBattleJournal.ViewModels
{
    public partial class OptionsPageViewModel : ObservableObject
    {
        private readonly ISqliteConnectionFactory _connection;
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private Trainer? _trainer;
        private readonly ILogger<OptionsPageViewModel> _logger;
        private readonly ITrainerSwitchService _switchService;
        private readonly AppShellViewModel _shellVm;

        public OptionsPageViewModel(ILogger<OptionsPageViewModel> logger, ISqliteConnectionFactory connection, ITrainerSwitchService switchService, AppShellViewModel shellVm)
        {
            _connection = connection;
            _logger = logger;
            _switchService = switchService;
            _shellVm = shellVm;
        }

        [ObservableProperty]
        public partial List<Trainer> AllTrainers { get; set; } = [];

        [ObservableProperty]
        public partial string Title { get; set; } = $"{PreferencesHelper.GetSetting("TrainerName")}'s Options";

        [ObservableProperty]
        public partial string TrainerName { get; set; } = PreferencesHelper.GetSetting("TrainerName");

        [ObservableProperty]
        public partial string? NameInput { get; set; }

        [ObservableProperty]
        public partial string? TagInput { get; set; }

        [ObservableProperty]
        public partial string? NewDeckName { get; set; }

        [ObservableProperty]
        public partial string? NewDeckIcon { get; set; }

        [ObservableProperty]
        public partial List<string> IconCollection { get; set; } = new List<string>();

        [ObservableProperty]
        public partial string SelectedIcon { get; set; } = "ball_icon.png";

        [ObservableProperty]
        public partial List<IconItem> IconItems { get; set; } = [];

        [ObservableProperty]
        public partial IconItem? SelectedIconItem { get; set; }

        partial void OnSelectedIconItemChanged(IconItem? value)
        {
            SelectedIcon = value?.ImagePath ?? "ball_icon.png";
            NewDeckIcon = value?.ImagePath;
        }

        [ObservableProperty]
        public partial string FileConfirmMessage { get; set; } = $"Delete {PreferencesHelper.GetSetting("TrainerName")}'s Trainer File?";

        [RelayCommand]
        public async Task AppearingAsync()
        {
            _logger.LogInformation("OptionsPageViewModel appearing");
            TrainerName = PreferencesHelper.GetSetting("TrainerName");
            Title = $"{TrainerName}'s Options";
            _logger.LogInformation("Current Trainer Name: {TrainerName}", TrainerName);
            try
            {
                IconCollection = await PopulateIconCollectionAsync();
                var activeId = PreferencesHelper.GetTrainerId();
                _trainer = activeId > 0
                    ? await _connection.Trainers.GetByIdAsync(activeId)
                    : await _connection.Trainers.GetByNameAsync(TrainerName);
                AllTrainers = await _connection.Trainers.GetAllAsync();
                _logger.LogInformation("Trainer Loaded: {TrainerName}", TrainerName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading ViewModel: {TrainerName} {@IconCollection}", TrainerName, IconCollection);
                ModalErrorHandler modalErrorHandler = new();
                modalErrorHandler.HandleError(ex);
            }
        }

        [RelayCommand]
        public async Task SwitchTrainerAsync(Trainer trainer)
        {
            if (trainer.Id == (_trainer?.Id ?? 0))
                return;

            await _switchService.SwitchToAsync(trainer);
            _trainer = trainer;
            TrainerName = trainer.Name ?? string.Empty;
            Title = $"{TrainerName}'s Options";
            FileConfirmMessage = $"Delete {TrainerName}'s Trainer File?";
            await _shellVm.LoadAsync();
        }

        [RelayCommand]
        public async Task DeleteTrainerFromListAsync(Trainer trainer)
        {
            bool confirmed = await Shell.Current.DisplayAlert(
                "Delete Trainer",
                $"Delete '{trainer.Name}' and all their match data?",
                "Delete", "Cancel");
            if (!confirmed)
                return;

            try
            {
                await _semaphore.WaitAsync();
                _ = await _connection.Trainers.DeleteAsync(trainer);
                AllTrainers = await _connection.Trainers.GetAllAsync();
                await _shellVm.LoadAsync();

                // If we deleted the active trainer, switch to first available
                if (trainer.Id == (_trainer?.Id ?? 0) && AllTrainers.Count > 0)
                    await SwitchTrainerAsync(AllTrainers[0]);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting trainer {TrainerName}", trainer.Name);
                ModalErrorHandler modalErrorHandler = new();
                modalErrorHandler.HandleError(ex);
            }
            finally
            {
                _ = _semaphore.Release();
            }
        }

        [RelayCommand]
        public async Task SaveTrainerAsync()
        {
            if (NameInput is null)
            {
                return;
            }

            TrainerName = NameInput;
            PreferencesHelper.SetSetting("TrainerName", NameInput);
            try
            {
                await _semaphore.WaitAsync();
                int affected = await _connection.Trainers.SaveAsync(NameInput);
                if (affected == 0)
                {
                    _logger.LogInformation("Trainer not saved: {TrainerName}", TrainerName);
                    return;
                }
                _logger.LogInformation("Trainer saved: {TrainerName}", TrainerName);
                _trainer = await _connection.Trainers.GetByNameAsync(NameInput);
                if (_trainer is null)
                {
                    _logger.LogInformation("Trainer not found immediately after save: {TrainerName}", TrainerName);
                    return;
                }
                _logger.LogInformation("Trainer Loaded: {TrainerName}", TrainerName);
                PreferencesHelper.SetTrainerId(_trainer.Id);
                AllTrainers = await _connection.Trainers.GetAllAsync();
                _shellVm.OnTrainerCreated(_trainer);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving Trainer: {TrainerName}", TrainerName);
                ModalErrorHandler modalErrorHandler = new();
                modalErrorHandler.HandleError(ex);
            }
            finally
            {
                _ = _semaphore.Release();
                NameInput = null;
                Title = $"{TrainerName}'s Options";
            }
        }

        [RelayCommand]
        public async Task SaveTagAsync()
        {
            if (TagInput is null || _trainer is null)
            {
                return;
            }

            try
            {
                await _semaphore.WaitAsync();
                int affected = 0;
                _ = await _connection.Tags.SaveAsync(TagInput, _trainer.Id);
                if (affected == 0)
                {
                    _logger.LogInformation("Tag not saved: {TagInput}", TagInput);
                    return;
                }
                _logger.LogInformation("Tag saved: {TagInput}", TagInput);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving Tag: {TagInput}", TagInput);
                ModalErrorHandler modalErrorHandler = new();
                modalErrorHandler.HandleError(ex);
            }
            finally
            {
                _ = _semaphore.Release();
                TagInput = null;
            }
        }

        [RelayCommand]
        public async Task SaveArchetypeAsync()
        {
            if (NewDeckName is null || NewDeckIcon is null || _trainer is null)
            {
                return;
            }

            try
            {
                await _semaphore.WaitAsync();
                int affected = 0;
                _ = await _connection.Archetypes.SaveAsync(NewDeckName, NewDeckIcon, _trainer.Id);
                if (affected == 0)
                {
                    _logger.LogInformation("Archetype not saved: {DeckName} {DeckIcon}", NewDeckName, NewDeckIcon);
                    return;
                }
                _logger.LogInformation("Archetype saved: {DeckName} {DeckIcon}", NewDeckName, NewDeckIcon);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving Archetype: {DeckName} {DeckIcon}", NewDeckName, NewDeckIcon);
                ModalErrorHandler modalErrorHandler = new();
                modalErrorHandler.HandleError(ex);
            }
            finally
            {
                NewDeckName = null;
                NewDeckIcon = null;
                _ = _semaphore.Release();
            }
        }

        [RelayCommand]
        public async Task SaveAllAsync()
        {
            try
            {
                await SaveTrainerAsync();
                await SaveTagAsync();
                await SaveArchetypeAsync();
                _logger.LogInformation("All saved");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving all");
                ModalErrorHandler modalErrorHandler = new();
                modalErrorHandler.HandleError(ex);
            }
        }

        [RelayCommand]
        public async Task DeleteTrainerFileAsync()
        {
            if (_trainer is null)
            {
                return;
            }
            try
            {
                await _semaphore.WaitAsync();
                _ = await _connection.Trainers.DeleteAsync(_trainer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting Trainer: {TrainerName}", TrainerName);
                ModalErrorHandler modalErrorHandler = new();
                modalErrorHandler.HandleError(ex);
            }
            finally
            {
                _ = _semaphore.Release();
            }
        }

        //Icon name collection file reader
        private async Task<List<string>> PopulateIconCollectionAsync()
        {
            string? imageName;
            List<string> iconCollection = [];
            try
            {
                await _semaphore.WaitAsync();
                await using Stream fileStream = await FileSystem.Current.OpenAppPackageFileAsync("icon_file_names.txt");
                using StreamReader reader = new(fileStream);
                while ((imageName = await reader.ReadLineAsync()) is not null)
                {
                    iconCollection.Add(imageName);
                }
                IconItems = iconCollection
                    .Select(f => new IconItem(ToDisplayName(f), f))
                    .ToList();
                return iconCollection;
            }
            catch (Exception ex)
            {
                ModalErrorHandler modalErrorHandler = new();
                modalErrorHandler.HandleError(ex);
                return iconCollection;
            }
            finally
            {
                _ = _semaphore.Release();
            }
        }

        private static string ToDisplayName(string filename)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(filename);
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo
                .ToTitleCase(name.Replace('_', ' '));
        }
    }
}