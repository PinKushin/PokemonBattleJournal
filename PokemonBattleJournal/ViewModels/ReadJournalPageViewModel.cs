namespace PokemonBattleJournal.ViewModels
{
    public partial class ReadJournalPageViewModel : ObservableObject
    {
        private readonly ILogger<ReadJournalPageViewModel> _logger;
        private readonly ISqliteConnectionFactory _connection;
        private static readonly SemaphoreSlim _semaphore = new(1, 1);

        public ReadJournalPageViewModel(ILogger<ReadJournalPageViewModel> logger, ISqliteConnectionFactory connection)
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



        [RelayCommand]
        public async Task AppearingAsync()
        {
            _logger.LogInformation("ReadJournalPageViewModel appearing");
            try
            {
                await _semaphore.WaitAsync();
                Trainer? trainer = await _connection.Trainers.GetByNameAsync(TrainerName);
                if (trainer == null)
                {
                    _logger.LogInformation("Trainer not found: {TrainerName}", TrainerName);
                    return;
                }
                _logger.LogInformation("Loading matches for trainer: {TrainerId} {TrainerName}", trainer.Id, trainer.Name);
                List<MatchEntry>? matches = await _connection.Matches.GetByTrainerIdAsync(trainer.Id);

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

                DateTime startTime = DateTime.Now;
                _logger.LogDebug("Loading match ID={MatchId} started at {StartTime}",
                    SelectedMatch.Id, startTime.ToString("HH:mm:ss.fff"));

                // Log detailed information about tags in each game
                LogTagDetails(SelectedMatch);

                OverallResult = SelectedMatch.Result;
                PlayingName = SelectedMatch.Playing?.Name ?? "Unknown";
                AgainstName = SelectedMatch.Against?.Name ?? "Unknown";
                PlayingIconSource = SelectedMatch.Playing?.ImagePath ?? "ball_icon.png";
                AgainstIconSource = SelectedMatch.Against?.ImagePath ?? "ball_icon.png";

                DateTime detailsStartTime = DateTime.Now;
                LoadGameDetails();
                TimeSpan detailsLoadTime = DateTime.Now - detailsStartTime;
                _logger.LogDebug("Game details loading took {LoadTimeMs}ms", detailsLoadTime.TotalMilliseconds);

                // Log final verification of tags after loading
                TimeSpan totalLoadTime = DateTime.Now - startTime;
                _logger.LogInformation("Match loaded successfully - Total load time: {TotalLoadTimeMs}ms",
                    totalLoadTime.TotalMilliseconds);

                // Verify final state of tags
                VerifyFinalTagState();
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
            _logger.LogDebug("Starting LoadGameDetails - Loading game details for match: {MatchId}", SelectedMatch?.Id);

            // Reset all tag collections first to ensure clean state
            TagsSelectedGame1 = null;
            TagsSelectedGame2 = null;
            TagsSelectedGame3 = null;

            // Reset tag availability flags
            HasGame1Tags = false;
            HasGame2Tags = false;
            HasGame3Tags = false;

            // Reset tag info strings
            Game1TagsInfo = "No tags";
            Game2TagsInfo = "No tags";
            Game3TagsInfo = "No tags";

            if (SelectedMatch?.Game1 != null)
            {
                ResultGame1 = SelectedMatch.Game1.Result;
                SelectedNote = SelectedMatch.Game1.Notes;

                // Log the source tags before assignment
                _logger.LogDebug("Game 1 source tags count: {Count}", SelectedMatch.Game1.Tags?.Count ?? 0);
                if (SelectedMatch.Game1.Tags != null && SelectedMatch.Game1.Tags.Count > 0)
                {
                    // Create a new list to ensure proper property change notification
                    TagsSelectedGame1 = [.. SelectedMatch.Game1.Tags];
                    HasGame1Tags = TagsSelectedGame1.Count > 0;
                    Game1TagsInfo = $"Game 1: {TagsSelectedGame1.Count} tags";
                    _logger.LogDebug("Game 1 tags assigned: {Count} tags", TagsSelectedGame1.Count);

                    // Log individual tags for debugging
                    foreach (Tags tag in TagsSelectedGame1)
                    {
                        _logger.LogDebug("Game 1 tag: {TagId} - {TagName}", tag.Id, tag.Name);
                    }
                }
                else
                {
                    _logger.LogDebug("Game 1 has no tags");
                    TagsSelectedGame1 = [];
                    HasGame1Tags = false;
                    Game1TagsInfo = "Game 1: No tags";
                }

                _logger.LogDebug("Game 1 loaded: {Result}, Tags count: {TagCount}",
                                ResultGame1, TagsSelectedGame1?.Count ?? 0);
            }
            else
            {
                ResultGame1 = null;
                SelectedNote = null;
                TagsSelectedGame1 = null;
                HasGame1Tags = false;
                Game1TagsInfo = "Game 1: Not available";
                _logger.LogDebug("Game 1 is null, no details to load");
            }

            if (SelectedMatch?.Game2 != null)
            {
                ResultGame2 = SelectedMatch.Game2.Result;
                SelectedNote2 = SelectedMatch.Game2.Notes;

                // Log the source tags before assignment
                _logger.LogDebug("Game 2 source tags count: {Count}", SelectedMatch.Game2.Tags?.Count ?? 0);
                if (SelectedMatch.Game2.Tags != null && SelectedMatch.Game2.Tags.Count > 0)
                {
                    // Create a new list to ensure proper property change notification
                    TagsSelectedGame2 = [.. SelectedMatch.Game2.Tags];
                    HasGame2Tags = TagsSelectedGame2.Count > 0;
                    Game2TagsInfo = $"Game 2: {TagsSelectedGame2.Count} tags";
                    _logger.LogDebug("Game 2 tags assigned: {Count} tags", TagsSelectedGame2.Count);

                    // Log individual tags for debugging
                    foreach (Tags tag in TagsSelectedGame2)
                    {
                        _logger.LogDebug("Game 2 tag: {TagId} - {TagName}", tag.Id, tag.Name);
                    }
                }
                else
                {
                    _logger.LogDebug("Game 2 has no tags");
                    TagsSelectedGame2 = [];
                    HasGame2Tags = false;
                    Game2TagsInfo = "Game 2: No tags";
                }

                _logger.LogDebug("Game 2 loaded: {Result}, Tags count: {TagCount}",
                    ResultGame2, TagsSelectedGame2?.Count ?? 0);
            }
            else
            {
                ResultGame2 = null;
                SelectedNote2 = null;
                TagsSelectedGame2 = null;
                HasGame2Tags = false;
                Game2TagsInfo = "Game 2: Not available";
                _logger.LogDebug("Game 2 is null, no details to load");
            }

            if (SelectedMatch?.Game3 != null)
            {
                ResultGame3 = SelectedMatch.Game3.Result;
                SelectedNote3 = SelectedMatch.Game3.Notes;

                // Log the source tags before assignment
                _logger.LogDebug("Game 3 source tags count: {Count}", SelectedMatch.Game3.Tags?.Count ?? 0);
                if (SelectedMatch.Game3.Tags != null && SelectedMatch.Game3.Tags.Count > 0)
                {
                    // Create a new list to ensure proper property change notification
                    TagsSelectedGame3 = [.. SelectedMatch.Game3.Tags];
                    HasGame3Tags = TagsSelectedGame3.Count > 0;
                    Game3TagsInfo = $"Game 3: {TagsSelectedGame3.Count} tags";
                    _logger.LogDebug("Game 3 tags assigned: {Count} tags", TagsSelectedGame3.Count);

                    // Log individual tags for debugging
                    foreach (Tags tag in TagsSelectedGame3)
                    {
                        _logger.LogDebug("Game 3 tag: {TagId} - {TagName}", tag.Id, tag.Name);
                    }
                }
                else
                {
                    _logger.LogDebug("Game 3 has no tags");
                    TagsSelectedGame3 = [];
                    HasGame3Tags = false;
                    Game3TagsInfo = "Game 3: No tags";
                }

                _logger.LogDebug("Game 3 loaded: {Result}, Tags count: {TagCount}",
                    ResultGame3, TagsSelectedGame3?.Count ?? 0);
            }
            else
            {
                ResultGame3 = null;
                SelectedNote3 = null;
                TagsSelectedGame3 = null;
                HasGame3Tags = false;
                Game3TagsInfo = "Game 3: Not available";
                _logger.LogDebug("Game 3 is null, no details to load");
            }

            // Log final state of tag collections after loading all games
            _logger.LogDebug("Final tag counts after loading game details - Game1: {Game1Count}, Game2: {Game2Count}, Game3: {Game3Count}",
                TagsSelectedGame1?.Count ?? 0,
                TagsSelectedGame2?.Count ?? 0,
                TagsSelectedGame3?.Count ?? 0);

            // Log availability flags
            _logger.LogDebug("Tag availability flags - HasGame1Tags: {HasGame1Tags}, HasGame2Tags: {HasGame2Tags}, HasGame3Tags: {HasGame3Tags}",
                HasGame1Tags, HasGame2Tags, HasGame3Tags);
        }


        private void ResetDisplay()
        {
            _logger.LogDebug("Resetting display");
            PlayingIconSource = "ball_icon.png";
            AgainstIconSource = "ball_icon.png";
            PlayingName = "other";
            AgainstName = "other";
            SelectedNote = "Select Match";
            SelectedNote2 = "Select Match";
            SelectedNote3 = "Select Match";

            // Explicitly set each tag collection to null or empty to trigger property changed notifications
            TagsSelectedGame1 = null;
            TagsSelectedGame2 = null;
            TagsSelectedGame3 = null;

            // Reset tag availability flags
            HasGame1Tags = false;
            HasGame2Tags = false;
            HasGame3Tags = false;

            // Reset tag info strings
            Game1TagsInfo = "No tags";
            Game2TagsInfo = "No tags";
            Game3TagsInfo = "No tags";

            ResultGame1 = null;
            ResultGame2 = null;
            ResultGame3 = null;
            OverallResult = null;
            _logger.LogDebug("Display reset complete");
        }

        private void LogTagDetails(MatchEntry match)
        {
            _logger.LogDebug("Raw tag details for match ID={MatchId}:", match.Id);

            if (match.Game1 != null)
            {
                _logger.LogDebug("Game 1 exists - Has Tags collection: {HasTagsCollection}, Tag count: {TagCount}",
                    match.Game1.Tags != null,
                    match.Game1.Tags?.Count ?? 0);

                if (match.Game1.Tags != null && match.Game1.Tags.Count > 0)
                {
                    foreach (Tags tag in match.Game1.Tags)
                    {
                        _logger.LogDebug("  > Game 1 raw Tag: ID={TagId}, Name={TagName}, HashCode={HashCode}",
                            tag.Id, tag.Name, tag.GetHashCode());
                    }
                }
            }
            else
            {
                _logger.LogDebug("Game 1 does not exist in this match");
            }

            if (match.Game2 != null)
            {
                _logger.LogDebug("Game 2 exists - Has Tags collection: {HasTagsCollection}, Tag count: {TagCount}",
                    match.Game2.Tags != null,
                    match.Game2.Tags?.Count ?? 0);

                if (match.Game2.Tags != null && match.Game2.Tags.Count > 0)
                {
                    foreach (Tags tag in match.Game2.Tags)
                    {
                        _logger.LogDebug("  > Game 2 raw Tag: ID={TagId}, Name={TagName}, HashCode={HashCode}",
                            tag.Id, tag.Name, tag.GetHashCode());
                    }
                }
            }
            else
            {
                _logger.LogDebug("Game 2 does not exist in this match");
            }

            if (match.Game3 != null)
            {
                _logger.LogDebug("Game 3 exists - Has Tags collection: {HasTagsCollection}, Tag count: {TagCount}",
                    match.Game3.Tags != null,
                    match.Game3.Tags?.Count ?? 0);

                if (match.Game3.Tags != null && match.Game3.Tags.Count > 0)
                {
                    foreach (Tags tag in match.Game3.Tags)
                    {
                        _logger.LogDebug("  > Game 3 raw Tag: ID={TagId}, Name={TagName}, HashCode={HashCode}",
                            tag.Id, tag.Name, tag.GetHashCode());
                    }
                }
            }
            else
            {
                _logger.LogDebug("Game 3 does not exist in this match");
            }
        }

        private void VerifyFinalTagState()
        {
            _logger.LogDebug("VERIFICATION - Final tag state after loading:");
            _logger.LogDebug("Game 1 tags - IsNull: {IsNull}, Count: {Count}, HasGame1Tags: {HasGame1Tags}",
                TagsSelectedGame1 == null,
                TagsSelectedGame1?.Count ?? -1,
                HasGame1Tags);

            _logger.LogDebug("Game 2 tags - IsNull: {IsNull}, Count: {Count}, HasGame2Tags: {HasGame2Tags}",
                TagsSelectedGame2 == null,
                TagsSelectedGame2?.Count ?? -1,
                HasGame2Tags);

            _logger.LogDebug("Game 3 tags - IsNull: {IsNull}, Count: {Count}, HasGame3Tags: {HasGame3Tags}",
                TagsSelectedGame3 == null,
                TagsSelectedGame3?.Count ?? -1,
                HasGame3Tags);
        }
    }
}