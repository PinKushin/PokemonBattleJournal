using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using Newtonsoft.Json;

namespace PokemonBattleJournal.ViewModels
{
    public partial class ReadJournalPageViewModel : ObservableObject
    {
        // get default file path
        private static readonly string filePath = FileHelper.GetAppDataPath() + $"\\{PreferencesHelper.GetSetting("TrainerName")}.json";
        [ObservableProperty]
        public partial string TrainerName { get; set; } = PreferencesHelper.GetSetting("TrainerName");
        [ObservableProperty]
        public partial string WelcomeMsg { get; set; }
        [ObservableProperty]
        public partial string? SelectedNote { get; set; } = "Select Match";
        [ObservableProperty]
        public partial MatchEntry? SelectedMatch { get; set; }
        [ObservableProperty]
        public partial string NoteHeaderIconSource { get; set; } = "ball_icon.png";
        [ObservableProperty]
        public partial ObservableCollection<MatchEntry> MatchHistory { get; set; } = new();

        public ReadJournalPageViewModel()
        {
            WelcomeMsg = $"{TrainerName}'s Journal";
            LoadJournalAsync();

        }

        private async void LoadJournalAsync()
        {
            if (FileHelper.Exists(filePath))
            {
                //Read File from Disk throws error if file doesn't exist so it was checked above
                var saveFile = await File.ReadAllTextAsync(filePath);
                //Deserialize file or create an empty list of matches if no matches exist
                MatchHistory = JsonConvert.DeserializeObject<ObservableCollection<MatchEntry>>(saveFile)
                    ?? [];

            }

        }

        [RelayCommand]
        public void LoadMatch()
        {
            if (SelectedMatch == null)
            {
                NoteHeaderIconSource = "ball_icon.png";
                return;
            }

            SelectedNote = SelectedMatch.Game1.Notes;
            
            NoteHeaderIconSource = $"{SelectedMatch.Playing.Replace(" ", "_")}.png";
        }
    }
}
