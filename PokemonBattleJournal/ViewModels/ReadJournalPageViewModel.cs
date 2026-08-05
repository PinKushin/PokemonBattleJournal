using PokemonBattleJournal.Utilities;

namespace PokemonBattleJournal.ViewModels
{
    public partial class ReadJournalPageViewModel : ObservableObject
    {
        private readonly ILogger<ReadJournalPageViewModel> _logger;
        private readonly ISqliteConnectionFactory _connection;
        private readonly ITrainerSwitchService _switchService;
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public ReadJournalPageViewModel(ILogger<ReadJournalPageViewModel> logger, ISqliteConnectionFactory connection, ITrainerSwitchService switchService)
        {
            WelcomeMsg = $"{TrainerName}'s Journal";
            _logger = logger;
            _connection = connection;
            _switchService = switchService;
            _switchService.TrainerChanged += OnTrainerChanged;
            _logger.LogInformation("ReadJournalPageViewModel created");
        }

        private void OnTrainerChanged(object? sender, Trainer trainer)
        {
            MainThreadHelper.BeginInvokeOnMainThread(() =>
            {
                TrainerName = trainer.Name ?? string.Empty;
                WelcomeMsg = $"{TrainerName}'s Journal";
                AppearingAsync().FireAndForgetSafeAsync(logger: _logger);
            });
        }


        [ObservableProperty]
        public partial string TrainerName { get; set; } = string.Empty;

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
        public partial string PlayingIconSource { get; set; } = "substitute.png";

        [ObservableProperty]
        public partial string? PlayingIconSource2 { get; set; }

        [ObservableProperty]
        public partial bool HasPlayingIcon2 { get; set; }

        [ObservableProperty]
        public partial string AgainstIconSource { get; set; } = "substitute.png";

        [ObservableProperty]
        public partial string? AgainstIconSource2 { get; set; }

        [ObservableProperty]
        public partial bool HasAgainstIcon2 { get; set; }

        [ObservableProperty]
        public partial List<Tags>? TagsSelectedGame1 { get; set; }

        [ObservableProperty]
        public partial List<Tags>? TagsSelectedGame2 { get; set; }

        [ObservableProperty]
        public partial List<Tags>? TagsSelectedGame3 { get; set; }

        [ObservableProperty]
        public partial bool HasGame1Tags { get; set; }

        [ObservableProperty]
        public partial bool HasGame2Tags { get; set; }

        [ObservableProperty]
        public partial bool HasGame3Tags { get; set; }

        [ObservableProperty]
        public partial string Game1TagsInfo { get; set; } = "No tags";

        [ObservableProperty]
        public partial string Game2TagsInfo { get; set; } = "No tags";

        [ObservableProperty]
        public partial string Game3TagsInfo { get; set; } = "No tags";

        [ObservableProperty]
        public partial List<MatchEntry>? MatchHistory { get; set; }

        /// <summary>
        /// Loading gate: true while the match-history load is in flight. Bound to the
        /// hidden Busy_MatchHistory sentinel Label so UI tests can sync on load completion
        /// instead of polling arbitrary elements.
        /// </summary>
        [ObservableProperty]
        public partial bool IsBusyMatchHistory { get; set; }

        [RelayCommand]
        public async Task AppearingAsync()
        {
            _logger.LogInformation("ReadJournalPageViewModel appearing");
            IsBusyMatchHistory = true;
            try
            {
                await _semaphore.WaitAsync();
                Trainer? trainer = _switchService.ActiveTrainer ?? await _connection.Trainers.GetActiveAsync();
                if (trainer == null)
                {
                    _logger.LogInformation("Trainer not found: {TrainerName}", TrainerName);
                    return;
                }
                TrainerName = trainer.Name ?? TrainerName;
                WelcomeMsg = $"{TrainerName}'s Journal";
                _logger.LogInformation("Loading matches for trainer: {TrainerId} {TrainerName}", trainer.Id, trainer.Name);
                List<MatchEntry>? matches = await _connection.Matches.GetByTrainerIdAsync(trainer.Id, includeRelated: true);

                if (matches.Count < 1 || matches is null)
                {
                    _logger.LogInformation("No matches found for trainer: {TrainerId} {TrainerName}", trainer.Id, trainer.Name);
                    MatchHistory = [];
                    return;
                }
#if DEBUG
                foreach (MatchEntry match in matches)
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
                ModalErrorHandler errorHandler = new();
                errorHandler.HandleError(ex);
                return;
            }
            finally
            {
                _semaphore.Release();
                IsBusyMatchHistory = false;
            }
        }

        [RelayCommand]
        public void LoadMatch()
        {
            try
            {
                if (SelectedMatch == null)
                {
                    ResetDisplay();
                    return;
                }

                OverallResult = SelectedMatch.Result;
                PlayingName = SelectedMatch.Playing?.Name ?? "Unknown";
                AgainstName = SelectedMatch.Against?.Name ?? "Unknown";
                PlayingIconSource = SelectedMatch.Playing?.ImagePath ?? "substitute.png";
                PlayingIconSource2 = SelectedMatch.Playing?.ImagePath2;
                HasPlayingIcon2 = PlayingIconSource2 != null;
                AgainstIconSource = SelectedMatch.Against?.ImagePath ?? "substitute.png";
                AgainstIconSource2 = SelectedMatch.Against?.ImagePath2;
                HasAgainstIcon2 = AgainstIconSource2 != null;

                LoadGameDetails();
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
            TagsSelectedGame1 = null;
            TagsSelectedGame2 = null;
            TagsSelectedGame3 = null;
            HasGame1Tags = false;
            HasGame2Tags = false;
            HasGame3Tags = false;
            Game1TagsInfo = "No tags";
            Game2TagsInfo = "No tags";
            Game3TagsInfo = "No tags";

            if (SelectedMatch?.Game1 != null)
            {
                ResultGame1 = SelectedMatch.Game1.Result;
                SelectedNote = SelectedMatch.Game1.Notes;
                if (SelectedMatch.Game1.Tags?.Count > 0)
                {
                    TagsSelectedGame1 = [.. SelectedMatch.Game1.Tags];
                    HasGame1Tags = true;
                    Game1TagsInfo = $"Game 1: {TagsSelectedGame1.Count} tags";
                }
                else
                {
                    TagsSelectedGame1 = [];
                    Game1TagsInfo = "Game 1: No tags";
                }
            }
            else
            {
                ResultGame1 = null;
                SelectedNote = null;
                Game1TagsInfo = "Game 1: Not available";
            }

            if (SelectedMatch?.Game2 != null)
            {
                ResultGame2 = SelectedMatch.Game2.Result;
                SelectedNote2 = SelectedMatch.Game2.Notes;
                if (SelectedMatch.Game2.Tags?.Count > 0)
                {
                    TagsSelectedGame2 = [.. SelectedMatch.Game2.Tags];
                    HasGame2Tags = true;
                    Game2TagsInfo = $"Game 2: {TagsSelectedGame2.Count} tags";
                }
                else
                {
                    TagsSelectedGame2 = [];
                    Game2TagsInfo = "Game 2: No tags";
                }
            }
            else
            {
                ResultGame2 = null;
                SelectedNote2 = null;
                Game2TagsInfo = "Game 2: Not available";
            }

            if (SelectedMatch?.Game3 != null)
            {
                ResultGame3 = SelectedMatch.Game3.Result;
                SelectedNote3 = SelectedMatch.Game3.Notes;
                if (SelectedMatch.Game3.Tags?.Count > 0)
                {
                    TagsSelectedGame3 = [.. SelectedMatch.Game3.Tags];
                    HasGame3Tags = true;
                    Game3TagsInfo = $"Game 3: {TagsSelectedGame3.Count} tags";
                }
                else
                {
                    TagsSelectedGame3 = [];
                    Game3TagsInfo = "Game 3: No tags";
                }
            }
            else
            {
                ResultGame3 = null;
                SelectedNote3 = null;
                Game3TagsInfo = "Game 3: Not available";
            }
        }


        private void ResetDisplay()
        {
            PlayingIconSource = "substitute.png";
            PlayingIconSource2 = null;
            HasPlayingIcon2 = false;
            AgainstIconSource = "substitute.png";
            AgainstIconSource2 = null;
            HasAgainstIcon2 = false;
            PlayingName = "other";
            AgainstName = "other";
            SelectedNote = "Select Match";
            SelectedNote2 = "Select Match";
            SelectedNote3 = "Select Match";
            TagsSelectedGame1 = null;
            TagsSelectedGame2 = null;
            TagsSelectedGame3 = null;
            HasGame1Tags = false;
            HasGame2Tags = false;
            HasGame3Tags = false;
            Game1TagsInfo = "No tags";
            Game2TagsInfo = "No tags";
            Game3TagsInfo = "No tags";
            ResultGame1 = null;
            ResultGame2 = null;
            ResultGame3 = null;
            OverallResult = null;
        }
    }
}