using System.Text;
using PokemonBattleJournal.Utilities;

namespace PokemonBattleJournal.ViewModels
{
    public partial class MainPageViewModel : ObservableObject
    {
        private readonly ILogger<MainPageViewModel> _logger;
        private readonly IErrorHandler _errorHandler;
        private readonly ISqliteConnectionFactory _connection;
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private Trainer? _trainer;
        private readonly IMatchResultsCalculatorFactory _calculatorFactory;
        private readonly ITrainerSwitchService _switchService;

        public bool HasUnsavedData =>
            PlayerSelected != null ||
            RivalSelected != null ||
            !string.IsNullOrWhiteSpace(UserNoteInput) ||
            Game1TagCollection?.Any(t => t.IsSelected) == true;

        public MainPageViewModel(ILogger<MainPageViewModel> logger, ISqliteConnectionFactory connection, IMatchResultsCalculatorFactory calculatorFactory, ITrainerSwitchService switchService, IErrorHandler errorHandler)
        {
            _logger = logger;
            _errorHandler = errorHandler;
            _connection = connection;
            _calculatorFactory = calculatorFactory;
            _switchService = switchService;
            _switchService.TrainerChanged += OnTrainerChanged;


            _logger.LogInformation("Created Main Page ViewModel");
            WelcomeMsg = $"Welcome {TrainerName}";
        }

        private void OnTrainerChanged(object? sender, Trainer trainer)
        {
            MainThreadHelper.BeginInvokeOnMainThread(() =>
            {
                TrainerName = trainer.Name ?? string.Empty;
                WelcomeMsg = $"Welcome {TrainerName}";
                ResetForm();
                _ = AppearingAsync().FireAndForgetSafeAsync(logger: _logger);
            });
        }

        private void ResetForm()
        {
            PlayerSelected = null;
            RivalSelected = null;
            UserNoteInput = string.Empty;
            UserNoteInput2 = string.Empty;
            UserNoteInput3 = string.Empty;
            foreach (TagViewModel t in Game1TagCollection ?? []) t.IsSelected = false;
            foreach (TagViewModel t in Game2TagCollection ?? []) t.IsSelected = false;
            foreach (TagViewModel t in Game3TagCollection ?? []) t.IsSelected = false;
            Result = default;
            Result2 = default;
            Result3 = default;
            FirstCheck = false;
            FirstCheck2 = false;
            FirstCheck3 = false;
            BO3Toggle = false;
        }


        [ObservableProperty]
        public partial string TrainerName { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string WelcomeMsg { get; set; }

        [ObservableProperty]
        public partial string? SavedFileDisplay { get; set; } = "Save File";

        [ObservableProperty]
        public partial string? ValidationMessage { get; set; }

        [ObservableProperty]
        public partial bool HasValidationErrors { get; set; }
        //Match Info and Notes
        [ObservableProperty]
        public partial Archetype? PlayerSelected { get; set; }

        [ObservableProperty]
        public partial bool IsPlayerArchetype2Visible { get; set; }

        partial void OnPlayerSelectedChanged(Archetype? value) =>
            IsPlayerArchetype2Visible = !string.IsNullOrEmpty(value?.ImagePath2);

        [ObservableProperty]
        public partial Archetype? RivalSelected { get; set; }

        [ObservableProperty]
        public partial bool IsRivalArchetype2Visible { get; set; }

        partial void OnRivalSelectedChanged(Archetype? value) =>
            IsRivalArchetype2Visible = !string.IsNullOrEmpty(value?.ImagePath2);

        [ObservableProperty]
        public partial string? UserNoteInput { get; set; }

        [ObservableProperty]
        public partial string? UserNoteInput2 { get; set; }

        [ObservableProperty]
        public partial string? UserNoteInput3 { get; set; }

        [ObservableProperty]
        public partial TimeSpan StartTime { get; set; } = DateTime.Now.TimeOfDay;

        [ObservableProperty]
        public partial TimeSpan EndTime { get; set; } = DateTime.Now.AddMinutes(5).TimeOfDay;

        partial void OnStartTimeChanged(TimeSpan value)
        {
            if (EndTime < value)
            {
                EndTime = value;
            }
        }

        partial void OnEndTimeChanged(TimeSpan value)
        {
            if (value < StartTime)
            {
                EndTime = StartTime;
            }
        }
        [ObservableProperty]
        public partial DateTime DatePlayed { get; set; } = DateTime.Now.ToLocalTime();

        [ObservableProperty]
        public partial List<Archetype>? Archetypes { get; set; }

        [ObservableProperty]
        public partial bool BO3Toggle { get; set; }

        partial void OnBO3ToggleChanged(bool value)
        {
            if (!value)
            {
                Result2 = null;
                Result3 = null;
                foreach (var t in Game2TagCollection ?? []) t.IsSelected = false;
                foreach (var t in Game3TagCollection ?? []) t.IsSelected = false;
                UserNoteInput2 = null;
                UserNoteInput3 = null;
                IsGame1Selected = true;
                IsGame2Selected = false;
                IsGame3Selected = false;
            }
            OnPropertyChanged(nameof(ShowGame3));
        }

        [ObservableProperty]
        public partial bool IsGame1Selected { get; set; } = true;

        [ObservableProperty]
        public partial bool IsGame2Selected { get; set; }

        [ObservableProperty]
        public partial bool IsGame3Selected { get; set; }

        [RelayCommand]
        private void SelectGame1()
        {
            IsGame1Selected = true;
            IsGame2Selected = false;
            IsGame3Selected = false;
        }

        [RelayCommand]
        private void SelectGame2()
        {
            IsGame1Selected = false;
            IsGame2Selected = true;
            IsGame3Selected = false;
        }

        [RelayCommand]
        private void SelectGame3()
        {
            IsGame1Selected = false;
            IsGame2Selected = false;
            IsGame3Selected = true;
        }

        [ObservableProperty]
        public partial bool FirstCheck { get; set; }

        [RelayCommand]
        private void ToggleFirstCheck() => FirstCheck = !FirstCheck;

        [ObservableProperty]
        public partial bool FirstCheck2 { get; set; }

        [RelayCommand]
        private void ToggleFirstCheck2() => FirstCheck2 = !FirstCheck2;

        [ObservableProperty]
        public partial bool FirstCheck3 { get; set; }

        [RelayCommand]
        private void ToggleFirstCheck3() => FirstCheck3 = !FirstCheck3;

        [ObservableProperty]
        public partial List<MatchResult> PossibleResults { get; set; } = [.. Enum.GetValues<MatchResult>()];

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowGame3))]
        public partial MatchResult? Result { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowGame3))]
        public partial MatchResult? Result2 { get; set; }

        [ObservableProperty]
        public partial MatchResult? Result3 { get; set; }

        // Game 3 needed when winner can't be determined: split result OR either game is a tie
        public bool ShowGame3 =>
            BO3Toggle && Result != null && Result2 != null &&
            ((Result == MatchResult.Win && Result2 == MatchResult.Loss) ||
             (Result == MatchResult.Loss && Result2 == MatchResult.Win) ||
             Result == MatchResult.Tie || Result2 == MatchResult.Tie);

        [RelayCommand]
        private void ToggleBO3() => BO3Toggle = !BO3Toggle;

        //Tags
        [ObservableProperty]
        public partial ObservableCollection<TagViewModel>? Game1TagCollection { get; set; }

        [ObservableProperty]
        public partial ObservableCollection<TagViewModel>? Game2TagCollection { get; set; }

        [ObservableProperty]
        public partial ObservableCollection<TagViewModel>? Game3TagCollection { get; set; }

        [ObservableProperty]
        public partial bool? IsToggled { get; set; }

        /// <summary>
        /// Update displayed time on UI
        /// </summary>


        /// <summary>
        /// Load Archetypes and Tags when page appears
        /// </summary>
        /// <returns></returns>
        /// <summary>
        /// Loading gate: true while archetypes + tags load. Bound to the hidden
        /// Busy_ArchetypeList sentinel Label for UI test sync.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsAnyBusy))]
        public partial bool IsBusyArchetypeList { get; set; }

        [RelayCommand]
        public async Task AppearingAsync()
        {
            _logger.LogInformation("Appearing: {Time}", DateTime.Now);
            StartTime = DateTime.Now.TimeOfDay;
            EndTime = DateTime.Now.AddMinutes(5).TimeOfDay;

            IsBusyArchetypeList = true;
            try
            {
                await _semaphore.WaitAsync();
                _trainer = _switchService.ActiveTrainer
                    ?? await _connection.Trainers.GetActiveAsync();

                // First boot or fresh install: no trainers exist — ask for a name
                // Suppressed when UI tests are running (sentinel file present) to avoid
                // ContentDialog.ShowAsync crashing before the WinUI XamlRoot is ready
                bool isUiTestRun = File.Exists(
                    Path.Combine(Path.GetTempPath(), "PokemonBattleJournal.uitest"));
                if (_trainer is null && Shell.Current is not null && !isUiTestRun)
                {
                    string? name = await Shell.Current.DisplayPromptAsync(
                        "Welcome",
                        "Enter your trainer name to get started",
                        accept: "Save",
                        cancel: null,
                        placeholder: "Trainer name",
                        maxLength: 50);

                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        await _connection.Trainers.SaveAsync(name);
                        Trainer? created = await _connection.Trainers.GetByNameAsync(name);
                        if (created is not null)
                        {
                            await _switchService.SwitchToAsync(created);
                            _trainer = created;
                        }
                    }
                }

                TrainerName = _trainer?.Name ?? TrainerName;
                WelcomeMsg = $"Welcome {TrainerName}";
                Archetypes = await _connection.Archetypes.GetAllAsync();
                List<Tags> allTags = await _connection.Tags.GetAllAsync();
                Game1TagCollection = new ObservableCollection<TagViewModel>(allTags.Select(t => new TagViewModel(t)));
                Game2TagCollection = new ObservableCollection<TagViewModel>(allTags.Select(t => new TagViewModel(t)));
                Game3TagCollection = new ObservableCollection<TagViewModel>(allTags.Select(t => new TagViewModel(t)));

            }
            catch (Exception ex)
            {
                _errorHandler.HandleError(ex);
                _logger.LogError(ex, "Error Loading ViewModel");
            }
            finally
            {
                _ = _semaphore.Release();
                IsBusyArchetypeList = false;
            }
        }

        [RelayCommand]
        private void Disappearing()
        {
            _logger.LogInformation("Disappearing: {Time}", DateTime.Now);
        }

        /// <summary>
        /// Validates match data before saving
        /// </summary>
        /// <returns>Tuple containing validation status and message</returns>
        private (bool IsValid, string Message) ValidateMatchData()
        {
            StringBuilder validationMessages = new();
            bool isValid = true;

            // Check required fields
            if (PlayerSelected == null)
            {
                _ = validationMessages.AppendLine("Player archetype is required");
                isValid = false;
            }

            if (RivalSelected == null)
            {
                _ = validationMessages.AppendLine("Rival archetype is required");
                isValid = false;
            }

            if (Result == null)
            {
                _ = validationMessages.AppendLine("Game 1 result is required");
                isValid = false;
            }

            // BO3 specific validations
            if (BO3Toggle)
            {
                if (Result2 == null)
                {
                    _ = validationMessages.AppendLine("Game 2 result is required for Best of 3");
                    isValid = false;
                }

                // Game 3 required when match winner can't be determined (split or both tied)
                if (Result != null && Result2 != null && Result3 == null && ShowGame3)
                {
                    _ = validationMessages.AppendLine("Game 3 result is required (results are split or both tied)");
                    isValid = false;
                }
            }

            return (isValid, validationMessages.ToString());
        }

        /// <summary>
        /// Loading gate: true while SaveMatchAsync is validating/writing/resetting the
        /// form. Bound to the hidden Busy_Mutating sentinel Label. Local Windows UI runs
        /// showed save-then-assert races on slower boxes; this gives tests a real signal
        /// to wait on instead of the button-text poll already in place.
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

        /// <summary>
        /// Verify, Serialize, and Save Match Data
        /// </summary>
        [RelayCommand]
        public async Task<int> SaveMatchAsync()
        {
            _logger.LogInformation("Attempting to save match data...");

            // Validate match data first
            (bool isValid, string message) = ValidateMatchData();
            if (!isValid)
            {
                ValidationMessage = message;
                HasValidationErrors = true;
                _logger.LogWarning("Match validation failed: {ValidationMessage}", message);
                return 0;
            }

            HasValidationErrors = false;
            ValidationMessage = null;

            // Get trainer
            _trainer = _switchService.ActiveTrainer ?? await _connection.Trainers.GetActiveAsync();
            if (TrainerName == null || _trainer == null)
            {
                ValidationMessage = "Trainer not found. Please create a trainer profile first.";
                HasValidationErrors = true;
                _logger.LogError("Trainer not found: name is {NameLength} chars", TrainerName?.Length ?? 0);
                return 0;
            }

            IsBusyMutating = true;
            try
            {
                await _semaphore.WaitAsync();
                DateTime startTimestamp = DateTime.UtcNow;

                _logger.LogInformation("Starting match save process for trainer {TrainerId}", _trainer.Id);

                IMatchResultCalculator calc = _calculatorFactory.GetCalculator(BO3Toggle);

                _logger.LogDebug("Creating match entry with Playing={PlayingId}, Against={AgainstId}",
                    PlayerSelected?.Id, RivalSelected?.Id);

                MatchEntry matchEntry = new()
                {
                    // Add user inputs to match entry
                    TrainerId = _trainer.Id,
                    PlayingId = PlayerSelected!.Id,
                    Playing = PlayerSelected,
                    AgainstId = RivalSelected!.Id,
                    Against = RivalSelected,
                    DatePlayed = DatePlayed,
                    StartTime = DatePlayed.Date + StartTime,
                    EndTime = DatePlayed.Date + EndTime,
                };
                List<Game> games = [];
                Game game1 = new()
                {
                    Result = Result,
                    Tags = Game1TagCollection?.Where(t => t.IsSelected).Select(t => t.Model).ToList() ?? [],
                    Turn = FirstCheck ? 1u : 2u,
                    Notes = UserNoteInput
                };
                _logger.LogDebug("Saving Game1 with {TagCount} tags", game1.Tags?.Count ?? 0);
                games.Add(game1);

                if (BO3Toggle)
                {
                    Game game2 = new()
                    {
                        Result = Result2,
                        Tags = Game2TagCollection?.Where(t => t.IsSelected).Select(t => t.Model).ToList() ?? [],
                        Turn = FirstCheck2 ? 1u : 2u,
                        Notes = UserNoteInput2
                    };
                    _logger.LogDebug("Saving Game2 with {TagCount} tags", game2.Tags?.Count ?? 0);
                    games.Add(game2);

                    Game game3 = new()
                    {
                        Result = Result3,
                        Tags = Game3TagCollection?.Where(t => t.IsSelected).Select(t => t.Model).ToList() ?? [],
                        Turn = FirstCheck3 ? 1u : 2u,
                        Notes = UserNoteInput3
                    };
                    _logger.LogDebug("Saving Game3 with {TagCount} tags", game3.Tags?.Count ?? 0);
                    games.Add(game3);
                }
                // Calculate overall match result
                matchEntry.Result = calc.CalculateResult(Result, Result2, Result3);
                _logger.LogInformation("Overall match result calculated: {Result}", matchEntry.Result);

                _logger.LogInformation("Saving match entry and {GameCount} games to database...", games.Count);
                int result = await _connection.Matches.SaveAsync(matchEntry, games);

                double elapsedMs = (DateTime.UtcNow - startTimestamp).TotalMilliseconds;

                if (result > 0)
                {
                    SavedFileDisplay = $"Saved: Match at {DateTimeOffset.Now}";
                    _logger.LogInformation("Match saved successfully in {ElapsedMs}ms", elapsedMs);
                    _logger.LogInformation("Match details: Playing={PlayingId}, Against={AgainstId}, Result={Result}",
                        matchEntry.PlayingId, matchEntry.AgainstId, matchEntry.Result);
                    _logger.LogDebug("Created {GameCount} games for match {MatchId}", games.Count, matchEntry.Id);

                    HasValidationErrors = false;
                    ValidationMessage = null;
                    ResetForm();

                    return result;
                }

                SavedFileDisplay = "Failed to save match";
                ValidationMessage = "Database operation completed but no records were affected.";
                HasValidationErrors = true;
                _logger.LogWarning("Match save operation completed but no records were affected");
                return result;
            }
            catch (ArgumentException ex)
            {
                SavedFileDisplay = "Save Failed: Invalid Data";
                ValidationMessage = $"Invalid data: {ex.Message}";
                HasValidationErrors = true;
                _errorHandler.HandleError(ex);
                _logger.LogError(ex, "Invalid data when saving match");
                return 0;
            }
            catch (SQLiteException ex)
            {
                SavedFileDisplay = "Save Failed: Database Error";
                ValidationMessage = $"Database error: {ex.Message}";
                HasValidationErrors = true;
                _errorHandler.HandleError(ex);
                _logger.LogError(ex, "Database error when saving match");
                return 0;
            }
            catch (Exception ex)
            {
                SavedFileDisplay = "Save Failed: Unexpected Error";
                ValidationMessage = $"An unexpected error occurred: {ex.Message}";
                HasValidationErrors = true;
                _errorHandler.HandleError(ex);
                _logger.LogError(ex, "Error saving match");
                return 0;
            }
            finally
            {
                _ = _semaphore.Release();
                IsBusyMutating = false;
            }
        }
    }
}