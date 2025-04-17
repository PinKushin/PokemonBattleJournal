namespace PokemonBattleJournal.ViewModels
{
    public partial class OptionsPageViewModel : ObservableObject
    {
        private readonly ISqliteConnectionFactory _connection;
        private static readonly SemaphoreSlim _semaphore = new(1, 1);
        private Trainer? _trainer;
        private readonly ILogger<OptionsPageViewModel> _logger;
        public OptionsPageViewModel(ILogger<OptionsPageViewModel> logger, ISqliteConnectionFactory connection)
        {
            _connection = connection;
            _logger = logger;
        }

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
        public partial string FileConfirmMessage { get; set; } = $"Delete {PreferencesHelper.GetSetting("TrainerName")}'s Trainer File?";

        [RelayCommand]
        public async Task AppearingAsync()
        {
            _logger.LogInformation("OptionsPageViewModel appearing");
            _logger.LogInformation("Current Trainer Name: {TrainerName}", TrainerName);
            try
            {
                IconCollection = await PopulateIconCollectionAsync();
                _trainer = await _connection.Trainers.GetByNameAsync(TrainerName);
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
                await _semaphore.WaitAsync();
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
            finally
            {
                _ = _semaphore.Release();
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
        private static async Task<List<string>> PopulateIconCollectionAsync()
        {
            string? imageName;
            List<string> iconCollection = [];
            try
            {
                await _semaphore.WaitAsync();
                await using Stream fileStream = await FileSystem.Current.OpenAppPackageFileAsync("icon_file_names.txt");
                using StreamReader reader = new(fileStream);
                while (!reader.EndOfStream)
                {
                    imageName = await reader.ReadLineAsync();
                    iconCollection.Add(imageName!);
                }
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
    }
}