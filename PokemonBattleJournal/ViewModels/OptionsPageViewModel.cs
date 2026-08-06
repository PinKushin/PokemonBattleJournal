using System.Text;
using CommunityToolkit.Maui.Storage;

namespace PokemonBattleJournal.ViewModels
{
    public partial class OptionsPageViewModel : ObservableObject
    {
        private readonly ISqliteConnectionFactory _connection;
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private Trainer? _trainer;
        private readonly ILogger<OptionsPageViewModel> _logger;
        private readonly IErrorHandler _errorHandler;
        private readonly ITrainerSwitchService _switchService;
        private readonly AppShellViewModel _shellVm;
        private readonly ITrainerHillImportService _importService;
        private readonly IExportService _exportService;

        public OptionsPageViewModel(ILogger<OptionsPageViewModel> logger, ISqliteConnectionFactory connection, ITrainerSwitchService switchService, AppShellViewModel shellVm, ITrainerHillImportService importService, IExportService exportService, IErrorHandler errorHandler)
        {
            _connection = connection;
            _logger = logger;
            _errorHandler = errorHandler;
            _switchService = switchService;
            _shellVm = shellVm;
            _importService = importService;
            _exportService = exportService;
        }

        [ObservableProperty]
        public partial List<Trainer> AllTrainers { get; set; } = [];

        [ObservableProperty]
        public partial Trainer? SelectedSwitchTrainer { get; set; }

        partial void OnSelectedSwitchTrainerChanged(Trainer? value)
        {
            if (value is null || value.Id == (_trainer?.Id ?? 0))
                return;
            SwitchTrainerCommand.Execute(value);
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

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasImportStatus))]
        public partial string ImportStatusMessage { get; set; } = string.Empty;

        public bool HasImportStatus => !string.IsNullOrEmpty(ImportStatusMessage);

        [RelayCommand]
        public async Task ImportFromTrainerHillAsync()
        {
            if (_trainer is null)
            {
                _logger.LogWarning("Import not started: no active trainer");
                return;
            }

            try
            {
                FileResult? file = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Select TrainerHill battle log",
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.WinUI, [".json"] },
                        { DevicePlatform.Android, ["application/json"] },
                        { DevicePlatform.iOS, ["public.json"] },
                        { DevicePlatform.MacCatalyst, ["public.json"] },
                    })
                });

                if (file is null)
                    return;

                // Gate starts AFTER the file picker returns — the picker is user browsing
                // time, not app processing time, and would otherwise leave Busy_Mutating
                // visible for however long the user takes to pick a file.
                IsBusyMutating = true;
                try
                {
                    await using Stream stream = await file.OpenReadAsync();
                    (int imported, List<string> errors) = await _importService.ImportAsync(stream, _trainer.Id);

                    ImportStatusMessage = errors.Count > 0
                        ? $"Imported {imported}, {errors.Count} skipped"
                        : $"Imported {imported} matches";

                    _logger.LogInformation("TrainerHill import: {Imported} imported, {Errors} errors", imported, errors.Count);

                    if (errors.Count > 0)
                        _logger.LogWarning("TrainerHill import errors: {Errors}", string.Join("; ", errors));
                }
                finally
                {
                    IsBusyMutating = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during TrainerHill import");
                ImportStatusMessage = "Import failed";
                _errorHandler.HandleError(ex);
            }
        }

        /// <summary>
        /// True in Debug builds only. Bound to the visibility of the loading-indicator toggle
        /// so the affordance simply is not there in Release.
        /// </summary>
        public static bool IsDebugBuild =>
#if DEBUG
            true;
#else
            false;
#endif

        /// <summary>
        /// Holds <see cref="IsBusyMutating"/> open so the loading indicator can be seen.
        /// </summary>
        /// <remarks>
        /// A toggle rather than a timed "simulate a slow operation", deliberately. A timed
        /// version would need a <c>Task.Delay</c>, which this project bans outside of tests, and
        /// it would give UI tests a window to race. Toggling holds the gate open until it is
        /// switched off, so the test is deterministic and the animation can be watched for as
        /// long as it takes to judge it.
        ///
        /// Debug-only in effect: the button that invokes it is bound to <see cref="IsDebugBuild"/>.
        /// </remarks>
        [RelayCommand]
        public void ToggleSimulatedLoading()
        {
            IsBusyMutating = !IsBusyMutating;
            _logger.LogInformation("Simulated loading toggled {State}", IsBusyMutating ? "on" : "off");
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasExportStatus))]
        public partial string ExportStatusMessage { get; set; } = string.Empty;

        public bool HasExportStatus => !string.IsNullOrEmpty(ExportStatusMessage);

        /// <summary>
        /// Exports the active trainer's matches in TrainerHill's format.
        /// </summary>
        [RelayCommand]
        public async Task ExportTrainerHillAsync()
        {
            if (_trainer is null)
            {
                _logger.LogWarning("Export not started: no active trainer");
                ExportStatusMessage = "No active trainer to export";
                return;
            }

            await SaveExportAsync(
                () => _exportService.ExportTrainerHillAsync(_trainer.Id),
                $"trainerhill-battle-log-{SanitizeForFileName(_trainer.Name)}-{DateTime.Now:yyyy-MM-dd}.json");
        }

        /// <summary>
        /// Exports every trainer in the app's own backup envelope.
        /// </summary>
        [RelayCommand]
        public async Task ExportBackupAsync() =>
            await SaveExportAsync(
                () => _exportService.ExportBackupAsync(),
                $"pbj-backup-{DateTime.Now:yyyy-MM-dd}.json");

        /// <summary>
        /// Serializes, then writes the result wherever the user chooses.
        /// </summary>
        /// <remarks>
        /// Shared by both export commands because the only difference between them is which
        /// serializer runs and what the file is called.
        ///
        /// The busy gate deliberately covers only the serialize step, not the save dialog:
        /// the dialog is user browsing time, and gating it would leave Busy_Mutating visible
        /// for as long as the user takes to choose a folder — the same reasoning as the import
        /// picker.
        /// </remarks>
        private async Task SaveExportAsync(Func<Task<string>> serialize, string suggestedFileName)
        {
            try
            {
                string json;
                IsBusyMutating = true;
                try
                {
                    json = await serialize();
                }
                finally
                {
                    IsBusyMutating = false;
                }

                using MemoryStream stream = new(Encoding.UTF8.GetBytes(json));
                FileSaverResult result = await FileSaver.Default.SaveAsync(suggestedFileName, stream);

                if (!result.IsSuccessful)
                {
                    // Cancelling the dialog surfaces here as an unsuccessful result, so this is
                    // the normal "changed my mind" path as well as the genuine failure path.
                    // Log at Information and say nothing alarming.
                    _logger.LogInformation(result.Exception, "Export not saved: {File}", suggestedFileName);
                    ExportStatusMessage = string.Empty;
                    return;
                }

                _logger.LogInformation("Exported to {Path}", result.FilePath);
                ExportStatusMessage = $"Exported to {Path.GetFileName(result.FilePath)}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during export of {File}", suggestedFileName);
                ExportStatusMessage = "Export failed";
                _errorHandler.HandleError(ex);
            }
        }

        /// <summary>Strips characters that are illegal in a file name on any target platform.</summary>
        private static string SanitizeForFileName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "trainer";

            // Path.GetInvalidFileNameChars() is platform-specific and Android's set is smaller
            // than Windows', so an export named on one platform could be unopenable on another.
            // An explicit allowlist keeps the suggested name portable.
            char[] cleaned = [.. name.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-')];
            return new string(cleaned).Trim('-');
        }

        /// <summary>
        /// Loading gate: true while trainers + archetypes + tags load. Bound to the
        /// hidden Busy_ArchetypeList sentinel Label for UI test sync.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsAnyBusy))]
        public partial bool IsBusyArchetypeList { get; set; }

        /// <summary>
        /// Loading gate: true while any Save/Delete/Import/Switch command is mutating the
        /// DB or reloading trainer-scoped state. Bound to the hidden Busy_Mutating
        /// sentinel Label. Without this, UI tests race the CollectionView rebind that
        /// follows a delete — WinAppDriver can return a phantom element whose ID is
        /// null/empty mid-rebind, surfacing as InvalidOperationException. Covers:
        /// SaveTrainerAsync, SwitchTrainerAsync, DeleteTrainerFromListAsync,
        /// DeleteTrainerFileAsync, SaveTagAsync, DeleteTagAsync, SaveArchetypeAsync,
        /// DeleteArchetypeAsync, ImportFromTrainerHillAsync.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsAnyBusy))]
        public partial bool IsBusyMutating { get; set; }

        /// <summary>
        /// True while EITHER gate is up. This is what the loading indicator binds to.
        /// </summary>
        /// <remarks>
        /// The page has a load gate and a mutate gate, and binding the spinner to one would
        /// leave the other operation with no feedback. The [NotifyPropertyChangedFor] on both
        /// inputs is load-bearing: without it the binding never updates and the spinner simply
        /// never appears, which no amount of correct XAML would reveal.
        /// </remarks>
        public bool IsAnyBusy => IsBusyArchetypeList || IsBusyMutating;

        [RelayCommand]
        public async Task AppearingAsync()
        {
            _logger.LogInformation("OptionsPageViewModel appearing");
            IsBusyArchetypeList = true;
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
                // Log only — no dialog from AppearingAsync. ContentDialog requires XamlRoot
                // which isn't set until the page is fully composed; calling it here crashes WinUI (0xc000027b).
                _logger.LogError(ex, "Error loading ViewModel: {TrainerName} {@IconCollection}", TrainerName, IconCollection);
            }
            finally
            {
                IsBusyArchetypeList = false;
            }
        }

        [RelayCommand]
        public async Task SwitchTrainerAsync(Trainer trainer)
        {
            if (trainer.Id == (_trainer?.Id ?? 0))
                return;

            IsBusyMutating = true;
            try
            {
                await _switchService.SwitchToAsync(trainer);
                _trainer = trainer;
                TrainerName = trainer.Name ?? string.Empty;
                Title = $"{TrainerName}'s Options";
                FileConfirmMessage = $"Delete {TrainerName}'s Trainer File?";
                await _shellVm.LoadAsync();
            }
            finally
            {
                IsBusyMutating = false;
            }
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
            IsBusyMutating = true;
            try
            {
                await _semaphore.WaitAsync();
                _ = await _connection.Trainers.DeleteAsync(trainer);
                AllTrainers = await _connection.Trainers.GetAllAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting trainer {TrainerName}", trainer.Name);
                _errorHandler.HandleError(ex);
                return;
            }
            finally
            {
                _ = _semaphore.Release();
                IsBusyMutating = false;
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
                _logger.LogWarning("Trainer not saved: name is empty");
                return;
            }

            TrainerName = NameInput;
            IsBusyMutating = true;
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
                _errorHandler.HandleError(ex);
            }
            finally
            {
                _ = _semaphore.Release();
                NameInput = null;
                Title = $"{TrainerName}'s Options";
                IsBusyMutating = false;
            }
        }
        [RelayCommand]
        public async Task SaveTagAsync()
        {
            // Named separately so a log reader can tell which input was missing. An empty
            // TagInput after a UI interaction means the text never reached the field; a null
            // trainer means the page was used before AppearingAsync resolved one.
            if (TagInput is null)
            {
                _logger.LogWarning("Tag not saved: tag name is empty");
                return;
            }

            if (_trainer is null)
            {
                _logger.LogWarning("Tag not saved: no active trainer (tag was {TagInput})", TagInput);
                return;
            }

            IsBusyMutating = true;
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
                _errorHandler.HandleError(ex);
            }
            finally
            {
                _ = _semaphore.Release();
                TagInput = null;
                IsBusyMutating = false;
            }
        }

        [RelayCommand]
        public async Task SaveArchetypeAsync()
        {
            if (NewDeckName is null)
            {
                _logger.LogWarning("Archetype not saved: deck name is empty");
                return;
            }

            if (NewDeckIcon is null)
            {
                _logger.LogWarning("Archetype not saved: no deck icon selected (deck was {NewDeckName})", NewDeckName);
                return;
            }

            if (_trainer is null)
            {
                _logger.LogWarning("Archetype not saved: no active trainer (deck was {NewDeckName})", NewDeckName);
                return;
            }

            IsBusyMutating = true;
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
                _errorHandler.HandleError(ex);
            }
            finally
            {
                NewDeckName = null;
                NewDeckIcon = SelectedIcon; // reset to current icon selection (default: ball_icon.png)
                _ = _semaphore.Release();
                IsBusyMutating = false;
            }
        }

        [RelayCommand]
        public async Task DeleteArchetypeAsync(Archetype archetype)
        {
            IsBusyMutating = true;
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
                _errorHandler.HandleError(ex);
            }
            finally
            {
                _ = _semaphore.Release();
                IsBusyMutating = false;
            }
        }

        [RelayCommand]
        public async Task DeleteTagAsync(Tags tag)
        {
            IsBusyMutating = true;
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
                _errorHandler.HandleError(ex);
            }
            finally
            {
                _ = _semaphore.Release();
                IsBusyMutating = false;
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
                _errorHandler.HandleError(ex);
            }
        }

        [RelayCommand]
        public async Task DeleteTrainerFileAsync()
        {
            if (_trainer is null)
            {
                _logger.LogWarning("Trainer file not deleted: no active trainer");
                return;
            }

            IsBusyMutating = true;
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
                _errorHandler.HandleError(ex);
                return;
            }
            finally
            {
                _ = _semaphore.Release();
                IsBusyMutating = false;
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
                // Log only — called from AppearingAsync before XamlRoot is composed; dialog would crash WinUI.
                _logger.LogError(ex, "Error loading icon collection from app package");
                return iconCollection;
            }
            finally
            {
                _ = _semaphore.Release();
            }
        }

        internal static string ToDisplayName(string filename)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(filename);
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo
                .ToTitleCase(name.Replace('_', ' '));
        }
    }
}