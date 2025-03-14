using Microsoft.Extensions.Logging;

namespace PokemonBattleJournal.ViewModels
{
    public partial class ReadJournalPageViewModel : ObservableObject
    {
        private readonly ILogger<ReadJournalPageViewModel> _logger;
        private readonly SqliteConnectionFactory _connection;
        //private readonly Trainer _trainer;
        public ReadJournalPageViewModel(ILogger<ReadJournalPageViewModel> logger, SqliteConnectionFactory connection)
        {
            WelcomeMsg = $"{TrainerName}'s Journal";
            _logger = logger;
            _connection = connection;
            //_trainer = _connection.GetTrainerByNameAsync(TrainerName).Result;
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
            var trainer = await _connection.GetTrainerByNameAsync(TrainerName);
            if (trainer == null)
                return;
            MatchHistory = await _connection.GetMatchEntriesByTrainerIdAsync(trainer.Id);
        }

        [RelayCommand]
        public void LoadMatch()
        {
            if (SelectedMatch == null)
            {
                PlayingIconSource = "ball_icon.png";
                AgainstIconSource = "ball_icon.png";
                return;
            }
            OverallResult = SelectedMatch.Result;
            ResultGame1 = SelectedMatch.Game1.Result;
            SelectedNote = SelectedMatch.Game1.Notes;
            TagsSelectedGame1 = SelectedMatch.Game1.Tags;
            if (SelectedMatch.Game2 is not null)
            {
                ResultGame2 = SelectedMatch.Game2.Result;
                SelectedNote2 = SelectedMatch.Game2.Notes;
                TagsSelectedGame2 = SelectedMatch.Game2.Tags;
            }
            else
            {
                TagsSelectedGame2 = null;
            }

            if (SelectedMatch.Game3 is not null)
            {
                ResultGame3 = SelectedMatch.Game3.Result;
                SelectedNote3 = SelectedMatch.Game3.Notes;
                TagsSelectedGame3 = SelectedMatch.Game3.Tags;
            }
            else
            {
                TagsSelectedGame3 = null;
            }
            PlayingName = SelectedMatch.Playing?.Name;
            AgainstName = SelectedMatch.Against?.Name;
            PlayingIconSource = SelectedMatch.Playing?.ImagePath;
            AgainstIconSource = SelectedMatch.Against?.ImagePath;
        }
    }
}