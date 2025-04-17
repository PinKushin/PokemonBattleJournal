using System.Globalization;
using System.Text;

namespace PokemonBattleJournal.ViewModels
{
    public partial class MainPageViewModel : ObservableObject
    {
        private readonly ILogger<MainPageViewModel> _logger;
        private readonly IDispatcherTimer? _timer;
        private readonly ISqliteConnectionFactory _connection;
        private static readonly SemaphoreSlim _semaphore = new(1, 1);
        private readonly Lock _lock = new();
        private static Trainer? _trainer;
        private readonly IMatchResultsCalculatorFactory _calculatorFactory;


        public MainPageViewModel(ILogger<MainPageViewModel> logger, ISqliteConnectionFactory connection, IMatchResultsCalculatorFactory calculatorFactory)
        {
            _logger = logger;
            _connection = connection;
            _calculatorFactory = calculatorFactory;

            //Timer to update displayed time
            if (Application.Current != null)
            {
                _timer = Application.Current.Dispatcher.CreateTimer();
                _timer.Interval = TimeSpan.FromSeconds(1);
                _timer.Tick += UpdateTime;
            }

            _logger.LogInformation("Created Main Page ViewModel{this}", this);
            WelcomeMsg = $"Welcome {TrainerName}";
        }

        //Convert date-time to string that can be used in the UI
        [ObservableProperty]
        public partial string CurrentDateTimeDisplay { get; set; } =
            DateTime.Now.ToLocalTime().ToString("T", CultureInfo.InvariantCulture);

        [ObservableProperty]
        public partial string TrainerName { get; set; } = PreferencesHelper.GetSetting("TrainerName");

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
        public partial DateTime StartTime { get; set; } = DateTime.Now.ToLocalTime();

        [ObservableProperty]
        public partial DateTime EndTime { get; set; } = DateTime.Now.ToLocalTime();

        partial void OnStartTimeChanged(DateTime value)
        {
            // Ensure EndTime is not before StartTime
            if (EndTime < value)
            {
                EndTime = value;
            }
        }

        partial void OnEndTimeChanged(DateTime value)
        {
            // Ensure EndTime is not before StartTime
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
            // Reset game 2 and 3 results if toggling off BO3
            if (!value)
            {
                Result2 = null;
                Result3 = null;
                Match2TagsSelected = null;
                Match3TagsSelected = null;
                UserNoteInput2 = null;
                UserNoteInput3 = null;
            }
        }
        [ObservableProperty]
        public partial bool FirstCheck { get; set; }

        [ObservableProperty]
        public partial bool FirstCheck2 { get; set; }

        [ObservableProperty]
        public partial bool FirstCheck3 { get; set; }

        [ObservableProperty]
        public partial List<MatchResult> PossibleResults { get; set; } = [.. Enum.GetValues<MatchResult>().Cast<MatchResult>()];

        [ObservableProperty]
        public partial MatchResult? Result { get; set; }

        [ObservableProperty]
        public partial MatchResult? Result2 { get; set; }

        [ObservableProperty]
        public partial MatchResult? Result3 { get; set; }

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
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void UpdateTime(object? sender, EventArgs e)
        {
            MainThreadHelper.BeginInvokeOnMainThread(() =>
            {
                CurrentDateTimeDisplay = $"{DateTime.Now.ToLocalTime().ToString("T", CultureInfo.InvariantCulture)}";
            });
        }

        /// <summary>
        /// Load Archetypes and Tags when page appears
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        public async Task AppearingAsync()
        {
            _timer?.Start();
            _logger.LogInformation("Appearing: {Time}", DateTime.Now);

            try
            {
                await _semaphore.WaitAsync();
                _trainer = await _connection.Trainers.GetByNameAsync(TrainerName);
                if (_trainer == null)
                {
                    _ = await _connection.Trainers.SaveAsync(TrainerName);
                    _trainer = await _connection.Trainers.GetByNameAsync(TrainerName);
                }
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
            _timer?.Stop();
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

                // Game 3 is required only if games 1 and 2 have different results
                if (Result != null && Result2 != null && Result != Result2 && Result3 == null)
                {
                    _ = validationMessages.AppendLine("Game 3 result is required when games 1 and 2 have different results");
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
            _trainer = await _connection.Trainers.GetByNameAsync(TrainerName);
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
                    StartTime = StartTime,
                    EndTime = EndTime,
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

                    // Clear validation state
                    HasValidationErrors = false;
                    ValidationMessage = null;

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