using System.Text;

namespace PokemonBattleJournal.ViewModels
{
    public partial class MainPageViewModel : ObservableObject
    {
        private readonly ILogger<MainPageViewModel> _logger;
        private readonly ISqliteConnectionFactory _connection;
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private readonly Lock _lock = new();
        private Trainer? _trainer;
        private readonly IMatchResultsCalculatorFactory _calculatorFactory;
        private readonly ITrainerSwitchService _switchService;

        public bool HasUnsavedData =>
            PlayerSelected != null ||
            RivalSelected != null ||
            !string.IsNullOrWhiteSpace(UserNoteInput) ||
            TagsSelected?.Count > 0;

        public MainPageViewModel(ILogger<MainPageViewModel> logger, ISqliteConnectionFactory connection, IMatchResultsCalculatorFactory calculatorFactory, ITrainerSwitchService switchService)
        {
            _logger = logger;
            _connection = connection;
            _calculatorFactory = calculatorFactory;
            _switchService = switchService;
            _switchService.TrainerChanged += OnTrainerChanged;


            _logger.LogInformation("Created Main Page ViewModel{this}", this);
            WelcomeMsg = $"Welcome {TrainerName}";
        }

        private void OnTrainerChanged(object? sender, Trainer trainer)
        {
            MainThreadHelper.BeginInvokeOnMainThread(async () =>
            {
                TrainerName = trainer.Name ?? string.Empty;
                WelcomeMsg = $"Welcome {TrainerName}";
                ResetForm();
                await AppearingAsync();
            });
        }

        private void ResetForm()
        {
            PlayerSelected = null;
            RivalSelected = null;
            UserNoteInput = string.Empty;
            UserNoteInput2 = string.Empty;
            UserNoteInput3 = string.Empty;
            TagsSelected = null;
            Match2TagsSelected = null;
            Match3TagsSelected = null;
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
        public partial Archetype? RivalSelected { get; set; }

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
                Match2TagsSelected = null;
                Match3TagsSelected = null;
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
        public partial List<MatchResult> PossibleResults { get; set; } = [.. Enum.GetValues<MatchResult>().Cast<MatchResult>()];

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
        public partial List<Tags>? TagCollection { get; set; }

        [ObservableProperty]
        public partial IList<object>? TagsSelected { get; set; }

        [ObservableProperty]
        public partial IList<object>? Match2TagsSelected { get; set; }

        [ObservableProperty]
        public partial IList<object>? Match3TagsSelected { get; set; }

        [ObservableProperty]
        public partial bool? IsToggled { get; set; }

        /// <summary>
        /// Update displayed time on UI
        /// </summary>


        /// <summary>
        /// Load Archetypes and Tags when page appears
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        public async Task AppearingAsync()
        {
            _logger.LogInformation("Appearing: {Time}", DateTime.Now);
            StartTime = DateTime.Now.TimeOfDay;
            EndTime = DateTime.Now.AddMinutes(5).TimeOfDay;

            try
            {
                await _semaphore.WaitAsync();
                _trainer = _switchService.ActiveTrainer
                    ?? await _connection.Trainers.GetActiveAsync();

                // First boot or fresh install: no trainers exist — ask for a name
                if (_trainer is null && Shell.Current is not null)
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
                TagCollection = await _connection.Tags.GetAllAsync();

            }
            catch (Exception ex)
            {
                ModalErrorHandler modalErrorHandler = new();
                modalErrorHandler.HandleError(ex);
                _logger.LogError(ex, "Error Loading ViewModel");
            }
            finally
            {
                _ = _semaphore.Release();
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

            // Time range validation
            if (EndTime < StartTime)
            {
                _ = validationMessages.AppendLine("End time cannot be before start time");
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
                _logger.LogError("Trainer not found: {TrainerName}", TrainerName);
                return 0;
            }
            try
            {
                await _semaphore.WaitAsync();
                DateTime startTimestamp = DateTime.Now;

                _logger.LogInformation("Starting match save process for trainer {TrainerId} ({TrainerName})",
                    _trainer.Id, _trainer.Name);

                IMatchResultCalculator calc = _calculatorFactory.GetCalculator(BO3Toggle);

                _logger.LogDebug("Creating match entry with Playing={PlayingId} ({PlayingName}), Against={AgainstId} ({AgainstName})",
                    PlayerSelected?.Id, PlayerSelected?.Name, RivalSelected?.Id, RivalSelected?.Name);

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
                    Tags = TagsSelected?.OfType<Tags>().ToList() ?? [], // Allow null to mean no tags
                    Turn = FirstCheck ? 1u : 2u,
                    Notes = UserNoteInput
                };
                _logger.LogDebug("Saving Game1 Tags: {@Tags}, from {@TagsSelect}", game1.Tags, TagsSelected);
                games.Add(game1);

                if (BO3Toggle)
                {
                    Game game2 = new()
                    {
                        Result = Result2,
                        Tags = Match2TagsSelected?.OfType<Tags>().ToList() ?? [], // Allow null to mean no tags
                        Turn = FirstCheck2 ? 1u : 2u,
                        Notes = UserNoteInput2
                    };
                    _logger.LogDebug("Saving Game2 Tags: {@Tags}", game2.Tags);
                    games.Add(game2);

                    Game game3 = new()
                    {
                        Result = Result3,
                        Tags = Match3TagsSelected?.OfType<Tags>().ToList() ?? [], // Allow null to mean no tags
                        Turn = FirstCheck3 ? 1u : 2u,
                        Notes = UserNoteInput3
                    };
                    _logger.LogDebug("Saving Game3 Tags: {@Tags}", game3.Tags);
                    games.Add(game3);
                }
                // Calculate overall match result
                matchEntry.Result = calc.CalculateResult(Result, Result2, Result3);
                _logger.LogInformation("Overall match result calculated: {Result}", matchEntry.Result);

                _logger.LogInformation("Saving match entry and {GameCount} games to database...", games.Count);
                int result = await _connection.Matches.SaveAsync(matchEntry, games);

                double elapsedMs = (DateTime.Now - startTimestamp).TotalMilliseconds;

                if (result > 0)
                {
                    SavedFileDisplay = $"Saved: Match at {DateTimeOffset.Now}";
                    _logger.LogInformation("Match saved successfully in {ElapsedMs}ms", elapsedMs);
                    _logger.LogInformation("Match details: Playing={Playing} Against={Against}, Result={Result}",
                        matchEntry.Playing?.Name, matchEntry.Against?.Name, matchEntry.Result);
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
                ModalErrorHandler modalErrorHandler = new();
                modalErrorHandler.HandleError(ex);
                _logger.LogError(ex, "Invalid data when saving match");
                return 0;
            }
            catch (SQLiteException ex)
            {
                SavedFileDisplay = "Save Failed: Database Error";
                ValidationMessage = $"Database error: {ex.Message}";
                HasValidationErrors = true;
                ModalErrorHandler modalErrorHandler = new();
                modalErrorHandler.HandleError(ex);
                _logger.LogError(ex, "Database error when saving match");
                return 0;
            }
            catch (Exception ex)
            {
                SavedFileDisplay = "Save Failed: Unexpected Error";
                ValidationMessage = $"An unexpected error occurred: {ex.Message}";
                HasValidationErrors = true;
                ModalErrorHandler modalErrorHandler = new();
                modalErrorHandler.HandleError(ex);
                _logger.LogError(ex, "Error saving match");
                return 0;
            }
            finally
            {
                _ = _semaphore.Release();
            }
        }
    }
}