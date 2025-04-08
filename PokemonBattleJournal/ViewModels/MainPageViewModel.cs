namespace PokemonBattleJournal.ViewModels
{
    using System.Globalization;
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

        [ObservableProperty]
        public partial DateTime DatePlayed { get; set; } = DateTime.Now.ToLocalTime();

        [ObservableProperty]
        public partial List<Archetype>? Archetypes { get; set; }

        [ObservableProperty]
        public partial bool BO3Toggle { get; set; }

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
                    await _connection.Trainers.SaveAsync(TrainerName);
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
                _semaphore.Release();
            }
        }

        [RelayCommand]
        private void Disappearing()
        {
            _logger.LogInformation("Disappearing: {Time}", DateTime.Now);
            _timer?.Stop();
        }

        /// <summary>
        /// Verify, Serialize, and Save Match Data
        /// </summary>
        [RelayCommand]
        public async Task<int> SaveMatchAsync()
        {
            _logger.LogInformation("Saving File...");
            _trainer = await _connection.Trainers.GetByNameAsync(TrainerName);
            if (PlayerSelected == null || RivalSelected == null || TrainerName == null || _trainer == null)
            {
                throw new InvalidOperationException("Required fields are missing.");
            }

            try
            {
                await _semaphore.WaitAsync();
                var calc = _calculatorFactory.GetCalculator(BO3Toggle);
                var matchEntry = new MatchEntry()
                {
                    // Add user inputs to match entry
                    TrainerId = _trainer.Id,
                    PlayingId = PlayerSelected.Id,
                    Playing = PlayerSelected,
                    AgainstId = RivalSelected.Id,
                    Against = RivalSelected,
                    DatePlayed = DatePlayed,
                    StartTime = StartTime,
                    EndTime = EndTime,
                };

                var games = new List<Game>();
                var game1 = new Game()
                {
                    Result = Result,
                    Tags = TagsSelected?.Cast<Tags>().ToList(),
                    Turn = FirstCheck ? 1u : 2u,
                    Notes = UserNoteInput
                };
                games.Add(game1);


                if (BO3Toggle)
                {
                    var game2 = new Game
                    {
                        Result = Result2,
                        Tags = Match2TagsSelected?.Cast<Tags>().ToList(),
                        Turn = FirstCheck2 ? 1u : 2u,
                        Notes = UserNoteInput2
                    };
                    games.Add(game2);

                    var game3 = new Game
                    {
                        Result = Result3,
                        Tags = Match3TagsSelected?.Cast<Tags>().ToList(),
                        Turn = FirstCheck3 ? 1u : 2u,
                        Notes = UserNoteInput3
                    };
                    games.Add(game3);
                }
                matchEntry.Result = calc.CalculateResult(Result, Result2, Result3);

                var result = await _connection.Matches.SaveAsync(matchEntry, games);
                if (result > 0)
                {
                    SavedFileDisplay = $"Saved: Match at {DateTimeOffset.Now}";
                    _logger.LogInformation("Match Created: {@Match}", matchEntry);
                    _logger.LogInformation("Playing: {Playing} Against: {Against}", matchEntry.Playing.Name, matchEntry.Against.Name);
                    _logger.LogInformation("{@Count} - Games Created", games.Count);
                    _logger.LogInformation("{@Game1}", games[0]);
                    if (games.Count > 1)
                    {
                        _logger.LogInformation("{@Game2}/n{Game3}", games[1], games[2]);
                    }
                    return result;
                }

                SavedFileDisplay = "Failed to save match";
                _logger.LogInformation("Failed Saving Match with no exceptions");
                return result;
            }
            catch (Exception ex)
            {
                SavedFileDisplay = "No File Saved";
                ModalErrorHandler modalErrorHandler = new();
                modalErrorHandler.HandleError(ex);
                _logger.LogError(ex, "Error Saving Match");
                return 0;
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}