using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;

namespace PokemonBattleJournal.ViewModels
{
    public partial class TrainerPageViewModel : ObservableObject
    {
        // get default file path
        private static readonly string filePath = FileHelper.GetAppDataPath() + $"\\{PreferencesHelper.GetSetting("TrainerName")}.json";
        [ObservableProperty]
        public partial string TrainerName { get; set; } = PreferencesHelper.GetSetting("TrainerName");
        [ObservableProperty]
        public partial string WelcomeMsg { get; set; }
        [ObservableProperty]
        public partial int Wins { get; set; } = 0;
        [ObservableProperty]
        public partial int Losses { get; set; } = 0;
        [ObservableProperty]
        public partial int Ties { get; set; } = 0;
        [ObservableProperty]
        public partial double WinAverage { get; set; } = 0;

        public TrainerPageViewModel()
        {
            WelcomeMsg = $"{TrainerName}'s Profile";
            LoadMatches();
        }

		private void LoadMatches()
        {
            // create file if it doesn't exist
            if (!FileHelper.Exists(filePath))
            {
                FileHelper.CreateFile(filePath);
            }
            //throws error if file doesn't exist so it was created above
            string? saveFile = File.ReadAllText(filePath);
			//deserialize file to add the new match or create an empty list of matches if no matches exist
			List<MatchEntry> matchList = JsonConvert.DeserializeObject<List<MatchEntry>>(saveFile)
                ?? [];
            //Tally up the wins, losses, and ties
            foreach (MatchEntry match in matchList)
            {
                if (match.Result == "Win")
                {
                    Wins++;
                }
                else if (match.Result == "Tie")
                {
                    Ties++;
                }
                else
                {
                    Losses++;
                }
            }
            if (Wins + Losses + Ties == 0)
            {
                WinAverage = 0;
            }
            else
            {
                WinAverage = ((Wins + (0.5 * Ties)) / (Wins + Losses + Ties)) * 100;
            }

        }
    }
}
