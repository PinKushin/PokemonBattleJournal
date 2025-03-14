using System.Globalization;
using Microsoft.Extensions.Logging;

namespace PokemonBattleJournal.ViewModels;

public partial class MainPageViewModel : ObservableObject
{
    private readonly ILogger<MainPageViewModel> _logger;
    private readonly IDispatcherTimer? _timer;
    private readonly SqliteConnectionFactory _connection;
    //private readonly Trainer _trainer;

    public MainPageViewModel(ILogger<MainPageViewModel> logger, SqliteConnectionFactory connection)
    {
        _logger = logger;
        _connection = connection;
        //_trainer = _connection.GetTrainerByNameAsync(TrainerName).Result;
        //if (_trainer == null)
        //{
        //    _connection.SaveTrainerAsync(new Trainer() { Name = TrainerName }).RunSynchronously();
        //    _trainer = _connection.GetTrainerByNameAsync(TrainerName).Result;
        //}

        //Timer to update displayed time
        if (Application.Current != null)
        {
            _timer = Application.Current.Dispatcher.CreateTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += UpdateTime;
        }

        _logger.LogInformation("Created Main Page ViewModel{this}", this);
        WelcomeMsg = $"Welcome {TrainerName}";
        connection.GetTagsAsync().ContinueWith(tags =>
        {
            TagCollection = tags.Result;
        });
        //connection.GetArchetypesAsync().ContinueWith(archetypes =>
        //{
        //    Archetypes = archetypes.Result;
        //});
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

    private Trainer? trainer;
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
        //await _connection.SaveArchetypeAsync(new Archetype() { Name = "Regidrago", ImagePath = "regidrago.png" });
        //await DataPopulationHelper.PopulateArchetypesAsync(_connection);
        _timer?.Start();
        _logger.LogInformation("Appearing: {Time}", DateTime.Now);
        try
        {
            trainer = await _connection.GetTrainerByNameAsync(TrainerName);
            if (trainer == null)
            {
                await _connection.SaveTrainerAsync(new Trainer() { Name = TrainerName });
                trainer = await _connection.GetTrainerByNameAsync(TrainerName);
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
    private async Task SaveMatchAsync()
    {
        _logger.LogInformation("Saving File...");

        try
        {
            //Game game1 = new()
            //{
            //    Turn = (uint) (FirstCheck ? 1 : 2),
            //    Result = Result,
            //    Notes = UserNoteInput,
            //    Tags = TagsSelected as List<Tags>
            //};
            //await _connection.SaveGameAsync(game1);

            //Game game2 = new()
            //{
            //    Turn = (uint) (FirstCheck2 ? 1 : 2),
            //    Result = Result2,
            //    Notes = UserNoteInput2,
            //    Tags = Match2TagsSelected as List<Tags>
            //};
            //await _connection.SaveGameAsync(game2);
            //Game game3 = new()
            //{
            //    Turn = (uint) (FirstCheck3 ? 1 : 2),
            //    Result = Result3,
            //    Notes = UserNoteInput3,
            //    Tags = Match3TagsSelected as List<Tags>
            //};
            //await _connection.SaveGameAsync(game3);

            //MatchEntry matchEntry = new() {
            //    TrainerId = trainer!.Id,
            //    PlayingId = PlayerSelected!.Id,
            //    Playing = PlayerSelected,
            //    AgainstId = RivalSelected!.Id,
            //    Against = RivalSelected,
            //    DatePlayed = DatePlayed,
            //    StartTime = StartTime,
            //    EndTime = EndTime,
            //    Game1Id = game1.Id,
            //    Game1 = game1,
            //};
            var matchEntry = await CreateMatchEntryAsync();
            await _connection.SaveMatchEntryAsync(matchEntry);
            _logger.LogInformation("Match Created: {@Match}", matchEntry);
            SavedFileDisplay = $"Saved: Match at {DateTimeOffset.Now}";
            _logger.LogInformation("{SavedFileDisplay}", SavedFileDisplay);
        }
        catch (Exception ex)
        {
            SavedFileDisplay = "No File Saved";
            ModalErrorHandler modalErrorHandler = new();
            modalErrorHandler.HandleError(ex);
            _logger.LogError(ex, "Error Saving Match");
        }
    }

    public async Task<MatchEntry> CreateMatchEntryAsync()
    {
        _logger.LogInformation("Creating Match Entry...");
        _logger.LogInformation("Tags Selected: {Tags}", TagsSelected);
        var trainer = await _connection.GetTrainerByNameAsync(TrainerName);
        if (PlayerSelected == null || RivalSelected == null || TrainerName == null || trainer == null)
        {
            throw new InvalidOperationException("Required fields are missing.");
        }

        MatchEntry matchEntry = new()
        {
            // Add user inputs to match entry
            TrainerId = trainer.Id,
            PlayingId = PlayerSelected!.Id,
            Playing = PlayerSelected,
            AgainstId = RivalSelected!.Id,
            Against = RivalSelected,
            DatePlayed = DatePlayed,
            StartTime = StartTime,
            EndTime = EndTime,
            Game1 = new Game() { Result = Result },
        };
        matchEntry.Game1.MatchEntryId = matchEntry.Id;
        try
        {
            await Task.Run(() =>
            {
                if (FirstCheck)
                    matchEntry.Game1.Turn = 1;
                else
                    matchEntry.Game1.Turn = 2;
                if (UserNoteInput != null)
                    matchEntry.Game1.Notes = UserNoteInput;

                matchEntry.Game1.Tags = TagsSelected as List<Tags>;

                if (!BO3Toggle)
                {
                    matchEntry.Result = Result;
                }
                else
                {
                    uint wins = 0;
                    uint draws = 0;
                    matchEntry.Game2 = new Game()
                    {
                        Result = Result2,
                        MatchEntryId = matchEntry.Id
                    };
                    matchEntry.Game3 = new Game()
                    {
                        Result = Result3,
                        MatchEntryId = matchEntry.Id
                    };
                    switch (Result)
                    {
                        case MatchResult.Win:
                            wins++;
                            break;

                        case MatchResult.Tie:
                            draws++;
                            break;
                    }

                    switch (Result2)
                    {
                        case MatchResult.Win:
                            wins++;
                            break;

                        case MatchResult.Tie:
                            draws++;
                            break;
                    }

                    switch (Result3)
                    {
                        case MatchResult.Win:
                            wins++;
                            break;

                        case MatchResult.Tie:
                            draws++;
                            break;
                    }

                    switch (wins)
                    {
                        case >= 2:
                            matchEntry.Result = MatchResult.Win;
                            break;
                        default:
                            switch (draws)
                            {
                                case >= 2:
                                case 1 when wins == 1:
                                    matchEntry.Result = MatchResult.Tie;
                                    break;
                                default:
                                    matchEntry.Result = MatchResult.Loss;
                                    break;
                            }

                            break;
                    }

                    if (UserNoteInput2 != null)
                        matchEntry.Game2.Notes = UserNoteInput2;
                    if (UserNoteInput3 != null)
                        matchEntry.Game3.Notes = UserNoteInput3;

                    if (FirstCheck2)
                        matchEntry.Game2.Turn = 1;
                    else
                        matchEntry.Game2.Turn = 2;

                    if (FirstCheck3)
                        matchEntry.Game3.Turn = 1;
                    else
                        matchEntry.Game3.Turn = 2;
                }
            });
        }
        catch (Exception ex)
        {
            ModalErrorHandler modalErrorHandler = new();
            modalErrorHandler.HandleError(ex);
            _logger.LogError(ex, "Error Creating Match Entry");
        }
        finally
        {
            // Clear Inputs
            TagsSelected = null;
            Match2TagsSelected = null;
            Match3TagsSelected = null;
            FirstCheck = false;
            FirstCheck2 = false;
            FirstCheck3 = false;
            UserNoteInput = null;
            UserNoteInput2 = null;
            UserNoteInput3 = null;
            PlayerSelected = null;
            RivalSelected = null;
            StartTime = new DateTime(DateTime.Now.Ticks, DateTimeKind.Local);
            EndTime = new DateTime(DateTime.Now.Ticks, DateTimeKind.Local);
            DatePlayed = new DateTime(DateTime.Now.Ticks, DateTimeKind.Local);
            Result = MatchResult.Tie;
            Result2 = MatchResult.Tie;
            Result3 = MatchResult.Tie;
            BO3Toggle = false;
            _logger.LogInformation("Cleared Inputs");
        }
        _logger.LogInformation("Match Creation Complete");
        return matchEntry;
    }
}
