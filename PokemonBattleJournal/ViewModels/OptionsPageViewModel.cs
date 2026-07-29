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
        public partial Trainer? SelectedSwitchTrainer { get; set; }

        partial void OnSelectedSwitchTrainerChanged(Trainer? value)
        {
            if (value is null || value.Id == (_trainer?.Id ?? 0))
                return;
            _ = SwitchTrainerAsync(value);
        }

        [ObservableProperty]
        public partial string Title { get; set; } = "Options";

        [ObservableProperty]
        public partial string TrainerName { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string? NameInput { get; set; }

        [ObservableProperty]
        public partial string? TagInput { get; set; }

        [ObservableProperty]
        public partial string? NewDeckName { get; set; }

        [ObservableProperty]
        public partial string? NewDeckIcon { get; set; } = "ball_icon.png";

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
        public partial List<Archetype> AllArchetypes { get; set; } = [];

        [ObservableProperty]
        public partial List<Tags> AllTags { get; set; } = [];

        [ObservableProperty]
        public partial string FileConfirmMessage { get; set; } = "Delete Trainer File?";

        [RelayCommand]
        public async Task AppearingAsync()
        {
            _logger.LogInformation("OptionsPageViewModel appearing");
            try
            {
                IconCollection = await PopulateIconCollectionAsync();
                _trainer = _switchService.ActiveTrainer ?? await _connection.Trainers.GetActiveAsync();
                TrainerName = _trainer?.Name ?? string.Empty;
                Title = $"{TrainerName}'s Options";
                _logger.LogInformation("Current Trainer Name: {TrainerName}", TrainerName);
                AllTrainers = await _connection.Trainers.GetAllAsync();
                SelectedSwitchTrainer = AllTrainers.FirstOrDefault(t => t.Id == (_trainer?.Id ?? 0));
                AllArchetypes = await _connection.Archetypes.GetAllAsync();
                AllTags = await _connection.Tags.GetAllAsync();
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
            bool confirmed = await Shell.Current.DisplayAlertAsync(
                "Delete Trainer",
                $"Delete '{trainer.Name}' and all their match data?",
                "Delete", "Cancel");
            if (!confirmed)
                return;

            bool deletedActive = trainer.Id == (_trainer?.Id ?? 0);
            try
            {
                await _semaphore.WaitAsync();
                _ = await _connection.Trainers.DeleteAsync(trainer);
                AllTrainers = await _connection.Trainers.GetAllAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting trainer {TrainerName}", trainer.Name);
                ModalErrorHandler modalErrorHandler = new();
                modalErrorHandler.HandleError(ex);
                return;
            }
            finally
            {
                _ = _semaphore.Release();
            }

            if (deletedActive)
            {
                _trainer = null;
                TrainerName = string.Empty;
                await HandleNoActiveTrainerAsync();
            }

            AllTrainers = await _connection.Trainers.GetAllAsync();
            await _shellVm.LoadAsync();
        }

        [RelayCommand]
        public async Task SaveTrainerAsync()
        {
            if (NameInput is null)
            {
                return;
            }

            TrainerName = NameInput;
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
                await _switchService.SwitchToAsync(_trainer);
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
                int affected = await _connection.Tags.SaveAsync(TagInput, _trainer.Id);
                if (affected == 0)
                {
                    _logger.LogInformation("Tag not saved: {TagInput}", TagInput);
                    return;
                }
                _logger.LogInformation("Tag saved: {TagInput}", TagInput);
                AllTags = await _connection.Tags.GetAllAsync();
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
                int affected = await _connection.Archetypes.SaveAsync(NewDeckName, NewDeckIcon, _trainer.Id);
                if (affected == 0)
                {
                    _logger.LogInformation("Archetype not saved: {DeckName} {DeckIcon}", NewDeckName, NewDeckIcon);
                    return;
                }
                _logger.LogInformation("Archetype saved: {DeckName} {DeckIcon}", NewDeckName, NewDeckIcon);
                AllArchetypes = await _connection.Archetypes.GetAllAsync();
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
                NewDeckIcon = SelectedIcon; // reset to current icon selection (default: ball_icon.png)
                _ = _semaphore.Release();
            }
        }

        [RelayCommand]
        public async Task DeleteArchetypeAsync(Archetype archetype)
        {
            try
            {
                await _semaphore.WaitAsync();
                int affected = await _connection.Archetypes.DeleteAsync(archetype);
                if (affected == 0)
                {
                    _logger.LogInformation("Archetype not deleted: {Name}", archetype.Name);
                    return;
                }
                _logger.LogInformation("Archetype deleted: {Name}", archetype.Name);
                AllArchetypes = await _connection.Archetypes.GetAllAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting Archetype: {Name}", archetype.Name);
                ModalErrorHandler modalErrorHandler = new();
                modalErrorHandler.HandleError(ex);
            }
            finally
            {
                _ = _semaphore.Release();
            }
        }

        [RelayCommand]
        public async Task DeleteTagAsync(Tags tag)
        {
            try
            {
                await _semaphore.WaitAsync();
                int affected = await _connection.Tags.DeleteAsync(tag);
                if (affected == 0)
                {
                    _logger.LogInformation("Tag not deleted: {Name}", tag.Name);
                    return;
                }
                _logger.LogInformation("Tag deleted: {Name}", tag.Name);
                AllTags = await _connection.Tags.GetAllAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting Tag: {Name}", tag.Name);
                ModalErrorHandler modalErrorHandler = new();
                modalErrorHandler.HandleError(ex);
            }
            finally
            {
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
            if (_trainer is null) return;

            try
            {
                await _semaphore.WaitAsync();
                _ = await _connection.Trainers.DeleteAsync(_trainer);
                _trainer = null;
                TrainerName = string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting Trainer: {TrainerName}", TrainerName);
                ModalErrorHandler modalErrorHandler = new();
                modalErrorHandler.HandleError(ex);
                return;
            }
            finally
            {
                _ = _semaphore.Release();
            }

            await HandleNoActiveTrainerAsync();
            AllTrainers = await _connection.Trainers.GetAllAsync();
            Title = _trainer is not null ? $"{TrainerName}'s Options" : "Options";
            await _shellVm.LoadAsync();
        }

        // Called after the active trainer is deleted. Offers the user a choice:
        // switch to an existing account, create a new one, or continue as guest
        // (guest = null active trainer; MainPage will re-prompt on next visit).
        private async Task HandleNoActiveTrainerAsync()
        {
            if (Shell.Current is null) return; // unit test environment

            List<Trainer> remaining = await _connection.Trainers.GetAllAsync();

            string[] options = remaining.Count > 0
                ? [.. remaining.Select(t => t.Name ?? "Unknown"), "Create New Account"]
                : ["Create New Account"];

            string? choice = await Shell.Current.DisplayActionSheetAsync(
                "Choose an account", "Continue as Guest", null, options);

            if (string.IsNullOrEmpty(choice) || choice == "Continue as Guest")
                return; // guest — MainPage prompt will fire next time

            if (choice == "Create New Account")
            {
                await PromptAndCreateTrainerAsync();
                return;
            }

            Trainer? picked = remaining.FirstOrDefault(t => t.Name == choice);
            if (picked is not null)
            {
                await _switchService.SwitchToAsync(picked);
                _trainer = picked;
                TrainerName = picked.Name ?? string.Empty;
            }
        }

        private async Task PromptAndCreateTrainerAsync()
        {
            string? name = await Shell.Current.DisplayPromptAsync(
                "New Account", "Enter your trainer name",
                accept: "Save", cancel: "Skip",
                placeholder: "Trainer name", maxLength: 50);

            if (string.IsNullOrWhiteSpace(name)) return;

            await _connection.Trainers.SaveAsync(name);
            Trainer? created = await _connection.Trainers.GetByNameAsync(name);
            if (created is null) return;

            await _switchService.SwitchToAsync(created);
            _trainer = created;
            TrainerName = created.Name ?? string.Empty;
            _shellVm.OnTrainerCreated(created);
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