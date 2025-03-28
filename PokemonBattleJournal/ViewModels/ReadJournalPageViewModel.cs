namespace PokemonBattleJournal.ViewModels
{
    public partial class ReadJournalPageViewModel : ObservableObject
    {
        private readonly ILogger<ReadJournalPageViewModel> _logger;
        private readonly SqliteConnectionFactory _connection;
        private static readonly SemaphoreSlim _semaphore = new(1, 1);

        public ReadJournalPageViewModel(ILogger<ReadJournalPageViewModel> logger, SqliteConnectionFactory connection)
        {
            WelcomeMsg = $"{TrainerName}'s Journal";
            _logger = logger;
            _connection = connection;
            _logger.LogInformation("ReadJournalPageViewModel created");

        }


        [ObservableProperty]
        public partial string TrainerName { get; set; } = PreferencesHelper.GetSetting("TrainerName");

        [ObservableProperty]
        public partial string WelcomeMsg { get; set; }

        [ObservableProperty]
        public partial string? SelectedNote { get; set; } = "Select Match";

        [ObservableProperty]
        public partial string? SelectedNote2 { get; set; } = "Select Match";

        [ObservableProperty]
        public partial string? SelectedNote3 { get; set; } = "Select Match";

        [ObservableProperty]
        public partial MatchEntry? SelectedMatch { get; set; }

        [ObservableProperty]
        public partial Game? SelectedGame { get; set; }

        [ObservableProperty]
        public partial Game? Game1 { get; set; }

        [ObservableProperty]
        public partial Game? Game2 { get; set; }

        [ObservableProperty]
        public partial Game? Game3 { get; set; }

        [ObservableProperty]
        public partial MatchResult? ResultGame1 { get; set; }

        [ObservableProperty]
        public partial MatchResult? ResultGame2 { get; set; }

        [ObservableProperty]
        public partial MatchResult? ResultGame3 { get; set; }

        [ObservableProperty]
        public partial MatchResult? OverallResult { get; set; }

        [ObservableProperty]
        public partial string? PlayingName { get; set; } = "other";

        [ObservableProperty]
        public partial string? AgainstName { get; set; } = "other";

        [ObservableProperty]
        public partial string? PlayingIconSource { get; set; } = "ball_icon.png";

        [ObservableProperty]
        public partial string? AgainstIconSource { get; set; } = "ball_icon.png";

        [ObservableProperty]
        public partial List<Tags>? TagsSelectedGame1 { get; set; }

        [ObservableProperty]
        public partial List<Tags>? TagsSelectedGame2 { get; set; }

        [ObservableProperty]
        public partial List<Tags>? TagsSelectedGame3 { get; set; }

        [ObservableProperty]
        public partial List<MatchEntry>? MatchHistory { get; set; }



        [RelayCommand]
        public async Task AppearingAsync()
        {
            _logger.LogInformation("ReadJournalPageViewModel appearing");
            try
            {
                await _semaphore.WaitAsync();
                var trainer = await _connection.GetTrainerByNameAsync(TrainerName);
                if (trainer == null)
                {
                    _logger.LogInformation("Trainer not found: {TrainerName}", TrainerName);
                    return;
                }
                _logger.LogInformation("Loading matches for trainer: {TrainerId} {TrainerName}", trainer.Id, trainer.Name);
                var matches = await _connection.GetMatchEntriesByTrainerIdAsync(trainer.Id);

                if (matches.Count < 1 || matches is null)
                {
                    _logger.LogInformation("No matches found for trainer: {TrainerId} {TrainerName}", trainer.Id, trainer.Name);
                    MatchHistory = [];
                    return;
                }
#if DEBUG
                foreach (var match in matches)
                {
                    _logger.LogInformation("Match loaded: ID={Id}, Playing={@Playing}, Against={@Against}",
                        match.Id, match.Playing, match.Against);
                }
#endif

                MatchHistory = matches;
                _logger.LogInformation("Loaded {Count} matches", matches.Count);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AppearingAsync");
                ModalErrorHandler errorHandler = new ModalErrorHandler();
                errorHandler.HandleError(ex);
                return;
            }
            finally
            {
                _semaphore.Release();
            }


        }

        [RelayCommand]
        public void LoadMatch()
        {
            try
            {
                if (SelectedMatch == null)
                {
                    _logger.LogWarning("No match selected");
                    ResetDisplay();
                    return;
                }

                _logger.LogInformation("Loading match: {@SelectedMatch}", SelectedMatch);

                OverallResult = SelectedMatch.Result;
                PlayingName = SelectedMatch.Playing?.Name ?? "Unknown";
                AgainstName = SelectedMatch.Against?.Name ?? "Unknown";
                PlayingIconSource = SelectedMatch.Playing?.ImagePath ?? "ball_icon.png";
                AgainstIconSource = SelectedMatch.Against?.ImagePath ?? "ball_icon.png";

                LoadGameDetails();

                _logger.LogInformation("Match loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading match details");
                ModalErrorHandler errorHandler = new();
                errorHandler.HandleError(ex);
                ResetDisplay();
            }
        }

        private void LoadGameDetails()
        {
            if (SelectedMatch?.Game1 != null)
            {
                ResultGame1 = SelectedMatch.Game1.Result;
                SelectedNote = SelectedMatch.Game1.Notes;
                TagsSelectedGame1 = SelectedMatch.Game1.Tags;
                _logger.LogDebug("Game 1 loaded: {Result}", ResultGame1);
            }

            if (SelectedMatch?.Game2 != null)
            {
                ResultGame2 = SelectedMatch.Game2.Result;
                SelectedNote2 = SelectedMatch.Game2.Notes;
                TagsSelectedGame2 = SelectedMatch.Game2.Tags;
                _logger.LogDebug("Game 2 loaded: {Result}", ResultGame2);
            }
            else
            {
                ResultGame2 = null;
                SelectedNote2 = null;
                TagsSelectedGame2 = null;
            }

            if (SelectedMatch?.Game3 != null)
            {
                ResultGame3 = SelectedMatch.Game3.Result;
                SelectedNote3 = SelectedMatch.Game3.Notes;
                TagsSelectedGame3 = SelectedMatch.Game3.Tags;
                _logger.LogDebug("Game 3 loaded: {Result}", ResultGame3);
            }
            else
            {
                ResultGame3 = null;
                SelectedNote3 = null;
                TagsSelectedGame3 = null;
            }
        }

        private void ResetDisplay()
        {
            PlayingIconSource = "ball_icon.png";
            AgainstIconSource = "ball_icon.png";
            PlayingName = "other";
            AgainstName = "other";
            SelectedNote = "Select Match";
            SelectedNote2 = "Select Match";
            SelectedNote3 = "Select Match";
            TagsSelectedGame1 = null;
            TagsSelectedGame2 = null;
            TagsSelectedGame3 = null;
            ResultGame1 = null;
            ResultGame2 = null;
            ResultGame3 = null;
            OverallResult = null;
        }
    }
}