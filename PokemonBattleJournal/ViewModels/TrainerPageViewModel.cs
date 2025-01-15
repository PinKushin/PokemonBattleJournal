using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;

namespace PokemonBattleJournal.ViewModels
{
    public partial class TrainerPageViewModel : ObservableObject
    {
        // get default file path
        private static string filePath = FileHelper.GetAppDataPath() + $"\\{PreferencesHelper.GetSetting("TrainerName")}.json";
        [ObservableProperty]
        public partial string TrainerName { get; set; } = PreferencesHelper.GetSetting("TrainerName");
        [ObservableProperty]
        public partial string WelcomeMsg { get; set; }
        [ObservableProperty]
        public partial int Wins { get; set; } = 0;
        [ObservableProperty]
        public partial int Losses { get; set; } = 0;
        [ObservableProperty]
        public partial int Draws { get; set; } = 0;
        [ObservableProperty]
        public partial double WinAverage { get; set; } = 0;

        public TrainerPageViewModel()
        {
            WelcomeMsg = $"{TrainerName}'s Profile";
            LoadMatchesAsync();
        }


        private async void LoadMatchesAsync()
        {
            // create file if it doesn't exist
            if (!File.Exists(filePath))
            {
                File.Create(filePath);
            }
            //throws error if file doesn't exist so it was created above
            var saveFile = await File.ReadAllTextAsync(filePath);
            //deserialize file to add the new match or create an empty list of matches if no matches exist
            var matchList = JsonConvert.DeserializeObject<List<MatchEntry>>(saveFile)
                ?? [];
            //Tally up the wins, losses, and ties
            foreach (var match in matchList)
            {
                if (match.Result == "Win")
                {
                    Wins++;
                }
                else if (match.Result == "Draw")
                {
                    Draws++;
                }
                else
                {
                    Losses++;
                }
            }
            if (Wins + Losses + Draws == 0)
            {
                WinAverage = 0;
            }
            else
            {
                WinAverage = ((Wins + (0.5 * Draws)) / (Wins + Losses + Draws)) * 100;
            }

        }
    }
}
