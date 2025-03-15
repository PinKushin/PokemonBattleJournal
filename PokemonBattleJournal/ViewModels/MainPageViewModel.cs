using System.Globalization;
using Microsoft.Extensions.Logging;

namespace PokemonBattleJournal.ViewModels;

public partial class MainPageViewModel : ObservableObject
{
    private readonly ILogger<MainPageViewModel> _logger;
    private readonly IDispatcherTimer? _timer;
    private readonly SqliteConnectionFactory _connection;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly Lock _lock = new();
    private static Trainer? _trainer;


    public MainPageViewModel(ILogger<MainPageViewModel> logger, SqliteConnectionFactory connection)
    {
        _logger = logger;
        _connection = connection;

        //Timer to update displayed time
        if (Application.Current != null)
        {
            _timer = Application.Current.Dispatcher.CreateTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += UpdateTime;
        }

        _logger.LogInformation("Created Main Page ViewModel{this}", this);
        WelcomeMsg = $"Welcome {TrainerName}";
        lock (_lock)
        {
            connection.GetTagsAsync().ContinueWith(tags =>
            {
                TagCollection = tags.Result;
            });
        }
    }

    //Convert date-time to string that can be used in the UI
    [ObservableProperty]
    public partial string CurrentDateTimeDisplay { get; set; } =
        DateTime.Now.ToLocalTime().ToString("T", CultureInfo.InvariantCulture);

    [ObservableProperty]
    private partial string TrainerName { get; set; } = PreferencesHelper.GetSetting("TrainerName");

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
    ///     Update displayed time on UI
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void UpdateTime(object? sender, EventArgs e)
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
    private async Task AppearingAsync()
    {
        _timer?.Start();
        _logger.LogInformation("Appearing: {Time}", DateTime.Now);
        await _semaphore.WaitAsync();
        try
        {
            _trainer = await _connection.GetTrainerByNameAsync(TrainerName);
            if (_trainer == null)
            {
                await _connection.SaveTrainerAsync(new Trainer() { Name = TrainerName });
                _trainer = await _connection.GetTrainerByNameAsync(TrainerName);
            }
            Archetypes = await _connection.GetArchetypesAsync();
            //TagCollection = await _connection.GetTagsAsync();
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
    ///     Verify, Serialize, and Save Match Data
    /// </summary>
    [RelayCommand]
    private async Task<int> SaveMatchAsync()
    {
        _logger.LogInformation("Saving File...");
        _trainer = await _connection.GetTrainerByNameAsync(TrainerName);
        if (PlayerSelected == null || RivalSelected == null || TrainerName == null || _trainer == null)
        {
            throw new InvalidOperationException("Required fields are missing.");
        }

        await _semaphore.WaitAsync();
        try
        {
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

                matchEntry.Result = Calculations.CalculateOverallResult(Result, Result2, Result3);
            }
            else
            {
                matchEntry.Result = Result;
            }

            var result = await _connection.SaveMatchEntryAsync(matchEntry, games);
            if (result > 0)
            {
                SavedFileDisplay = $"Saved: Match at {DateTimeOffset.Now}";
                _logger.LogInformation("Match Created: {@Match}", matchEntry);
                return result;
            }

            SavedFileDisplay = "Failed to save match";
            _logger.LogInformation("Failed Saving Match with no exceptions");
            return 0;
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

    //public async Task<MatchEntry> CreateMatchEntryAsync()
    //{
    //    _logger.LogInformation("Creating Match Entry...");
    //    _logger.LogInformation("Tags Selected: {Tags}", TagsSelected);
    //    _trainer = await _connection.GetTrainerByNameAsync(TrainerName);
    //    if (PlayerSelected == null || RivalSelected == null || TrainerName == null || _trainer == null)
    //    {
    //        throw new InvalidOperationException("Required fields are missing.");
    //    }

    //    MatchEntry matchEntry = new()
    //    {
    //        // Add user inputs to match entry
    //        TrainerId = _trainer.Id,
    //        PlayingId = PlayerSelected!.Id,
    //        Playing = PlayerSelected,
    //        AgainstId = RivalSelected!.Id,
    //        Against = RivalSelected,
    //        DatePlayed = DatePlayed,
    //        StartTime = StartTime,
    //        EndTime = EndTime,
    //        Game1 = new Game() { Result = Result },
    //    };
    //    matchEntry.Game1.MatchEntryId = matchEntry.Id;
    //    try
    //    {
    //        await Task.Run(() =>
    //        {
    //            if (FirstCheck)
    //                matchEntry.Game1.Turn = 1;
    //            else
    //                matchEntry.Game1.Turn = 2;
    //            if (UserNoteInput != null)
    //                matchEntry.Game1.Notes = UserNoteInput;

    //            matchEntry.Game1.Tags = TagsSelected as List<Tags>;

    //            if (!BO3Toggle)
    //            {
    //                matchEntry.Result = Result;
    //            }
    //            else
    //            {
    //                uint wins = 0;
    //                uint draws = 0;
    //                matchEntry.Game2 = new Game()
    //                {
    //                    Result = Result2,
    //                    MatchEntryId = matchEntry.Id
    //                };
    //                matchEntry.Game3 = new Game()
    //                {
    //                    Result = Result3,
    //                    MatchEntryId = matchEntry.Id
    //                };
    //                switch (Result)
    //                {
    //                    case MatchResult.Win:
    //                        wins++;
    //                        break;

    //                    case MatchResult.Tie:
    //                        draws++;
    //                        break;
    //                }

    //                switch (Result2)
    //                {
    //                    case MatchResult.Win:
    //                        wins++;
    //                        break;

    //                    case MatchResult.Tie:
    //                        draws++;
    //                        break;
    //                }

    //                switch (Result3)
    //                {
    //                    case MatchResult.Win:
    //                        wins++;
    //                        break;

    //                    case MatchResult.Tie:
    //                        draws++;
    //                        break;
    //                }

    //                switch (wins)
    //                {
    //                    case >= 2:
    //                        matchEntry.Result = MatchResult.Win;
    //                        break;
    //                    default:
    //                        switch (draws)
    //                        {
    //                            case >= 2:
    //                            case 1 when wins == 1:
    //                                matchEntry.Result = MatchResult.Tie;
    //                                break;
    //                            default:
    //                                matchEntry.Result = MatchResult.Loss;
    //                                break;
    //                        }

    //                        break;
    //                }

    //                if (UserNoteInput2 != null)
    //                    matchEntry.Game2.Notes = UserNoteInput2;
    //                if (UserNoteInput3 != null)
    //                    matchEntry.Game3.Notes = UserNoteInput3;

    //                if (FirstCheck2)
    //                    matchEntry.Game2.Turn = 1;
    //                else
    //                    matchEntry.Game2.Turn = 2;

    //                if (FirstCheck3)
    //                    matchEntry.Game3.Turn = 1;
    //                else
    //                    matchEntry.Game3.Turn = 2;
    //            }
    //        });
    //    }
    //    catch (Exception ex)
    //    {
    //        ModalErrorHandler modalErrorHandler = new();
    //        modalErrorHandler.HandleError(ex);
    //        _logger.LogError(ex, "Error Creating Match Entry");
    //    }
    //    finally
    //    {
    //        // Clear Inputs
    //        //TagsSelected = null;
    //        //Match2TagsSelected = null;
    //        //Match3TagsSelected = null;
    //        //FirstCheck = false;
    //        //FirstCheck2 = false;
    //        //FirstCheck3 = false;
    //        //UserNoteInput = null;
    //        //UserNoteInput2 = null;
    //        //UserNoteInput3 = null;
    //        //PlayerSelected = null;
    //        //RivalSelected = null;
    //        //StartTime = new DateTime(DateTime.Now.Ticks, DateTimeKind.Local);
    //        //EndTime = new DateTime(DateTime.Now.Ticks, DateTimeKind.Local);
    //        //DatePlayed = new DateTime(DateTime.Now.Ticks, DateTimeKind.Local);
    //        //Result = MatchResult.Tie;
    //        //Result2 = MatchResult.Tie;
    //        //Result3 = MatchResult.Tie;
    //        //BO3Toggle = false;
    //        //_logger.LogInformation("Cleared Inputs");
    //    }
    //    _logger.LogInformation("Match Creation Complete");
    //    return matchEntry;
    //}
    //}
}
