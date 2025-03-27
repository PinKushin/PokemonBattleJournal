namespace PokemonBattleJournal.ViewModels
{
    public partial class OptionsPageViewModel : ObservableObject
    {
        private readonly SqliteConnectionFactory _connection;
        private static readonly SemaphoreSlim _semaphore = new(1, 1);
        private Trainer? _trainer;
        public OptionsPageViewModel(SqliteConnectionFactory connection)
        {
            _connection = connection;
            //PopulateIconCollectionAsync().ContinueWith(icons => IconCollection = icons.Result);
            //_connection.GetTrainerByNameAsync(TrainerName).ContinueWith(trainer => _trainer = trainer.Result);
        }

        [ObservableProperty]
        public partial string Title { get; set; } = $"{PreferencesHelper.GetSetting("Trainer Name")}'s Options";

        [ObservableProperty]
        public partial string TrainerName { get; set; } = PreferencesHelper.GetSetting("Trainer Name");

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

        private bool _initialized;
        [RelayCommand]
        public async Task AppearingAsync()
        {
            if (!_initialized)
            {
                IconCollection = await PopulateIconCollectionAsync();
                _initialized = true;
            }
            _trainer = await _connection.GetTrainerByNameAsync(TrainerName);
        }

        [RelayCommand]
        public async Task SaveTrainerAsync()
        {
            if (NameInput is null)
                return;

            TrainerName = NameInput;
            PreferencesHelper.SetSetting("TrainerName", NameInput);
            await _connection.SaveTrainerAsync(NameInput);
            _trainer = await _connection.GetTrainerByNameAsync(NameInput);
            NameInput = null;
            Title = $"{TrainerName}'s Options";
        }

        [RelayCommand]
        public async Task SaveTagAsync()
        {
            if (TagInput is null)
                return;
            if (_trainer is null)
            {
                return;
            }
            await _connection.SaveTagAsync(TagInput, _trainer.Id);
            TagInput = null;
        }

        [RelayCommand]
        public async Task SaveArchetypeAsync()
        {
            if (NewDeckName is null || NewDeckIcon is null || _trainer is null)
                return;
            await _connection.SaveArchetypeAsync(NewDeckName, NewDeckIcon, _trainer.Id);
            NewDeckName = null;
            NewDeckIcon = null;
        }

        [RelayCommand]
        public async Task SaveAllAsync()
        {
            await SaveTrainerAsync();
            await SaveTagAsync();
            await SaveArchetypeAsync();
        }
        //TODO: create method to delete trainer file
        public async Task DeleteTrainerFileAsync()
        {
            if (_trainer is null)
            {
                return;
            }
            await _connection.DeleteTrainerAsync(_trainer);
        }

        //Icon Collection File Reader
        private static async Task<List<string>> PopulateIconCollectionAsync()
        {
            string? imageName;
            List<string> iconCollection = [];
            try
            {
                using Stream fileStream = await FileSystem.Current.OpenAppPackageFileAsync("icon_file_names.txt");
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
        }
    }
}