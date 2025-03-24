namespace PokemonBattleJournal.ViewModels
{
    public partial class TrainerPageViewModel : ObservableObject
    {
        // get default file path
        //private static readonly string filePath = FileHelper.GetAppDataPath() + $"\\{PreferencesHelper.GetSetting("TrainerName")}.json";

        [ObservableProperty]
        public partial string TrainerName { get; set; } = PreferencesHelper.GetSetting("TrainerName");

        [ObservableProperty]
        public partial string WelcomeMsg { get; set; }

        [ObservableProperty]
        public partial uint Wins { get; set; } = 0;

        [ObservableProperty]
        public partial uint Losses { get; set; } = 0;

        [ObservableProperty]
        public partial uint Ties { get; set; } = 0;

        [ObservableProperty]
        public partial double WinAverage { get; set; } = 0;

        public TrainerPageViewModel()
        {
            WelcomeMsg = $"{TrainerName}'s Profile";
            //LoadMatches();
            //CalculateWinRate([]);
        }

        /// <summary>
        /// Calculate the average win rate of the trainer
        /// using ((Wins + (0.5 * Ties)) / TotalGames * 100
        /// </summary>
        /// <param name="matchList">List of Matches</param>
        public double CalculateWinRate(List<MatchEntry> matchList)
        {
            uint wins = 0;
            uint losses = 0;
            uint ties = 0;
            double winRate = 0;

            foreach (MatchEntry match in matchList)
            {
                switch (match.Result)
                {
                    case MatchResult.Win:
                        wins++;
                        break;
                    case MatchResult.Tie:
                        ties++;
                        break;
                    case null:
                        break;
                    default:
                        losses++;
                        break;
                }
            }
            Wins = wins;
            Losses = losses;
            Ties = ties;
            if (wins + losses + ties == 0)
            {
                winRate = 0;
            }
            else
            {
                winRate = ((wins + (0.5 * ties)) / (wins + losses + ties)) * 100;
            }

            return winRate;
        }
    }
}