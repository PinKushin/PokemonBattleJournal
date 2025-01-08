using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;

namespace PokemonBattleJournal.ViewModels
{
    public partial class MainPageViewModel : ObservableObject
    {
        //public IFileSaver? fileSaver;
        //readonly CancellationTokenSource cancellationTokenSource = new();

        public MainPageViewModel(/*IFileSaver fileSaver*/)
        {
            //this.fileSaver = fileSaver;
            //Timer to update displayed time
            if (Application.Current != null)
            {
                var timer = Application.Current.Dispatcher.CreateTimer();
                timer.Interval = TimeSpan.FromSeconds(1);
                timer.Tick += UpdateTime;
                timer.Start();
            }

            DeckNames.Add("Regidrago");
            DeckNames.Add("Charizard");
            DeckNames.Add("Klawf");
            DeckNames.Add("Snorlax Stall");
            DeckNames.Add("Raging Bolt");
            DeckNames.Add("Gardevoir");
            DeckNames.Add("Miraidon");

            TagCollection.Add("Early Start");
            TagCollection.Add("Behind Early");
            TagCollection.Add("Donked Rival");
            TagCollection.Add("Got Donked");
            TagCollection.Add("Lucky");
            TagCollection.Add("Unlucky");
            TagCollection.Add("Never Punished");
            TagCollection.Add("Punished");

            PossibleResults.Add("Win");
            PossibleResults.Add("Lose");
            PossibleResults.Add("Draw");

            WelcomeMsg = $"Welcome {TrainerName}";

        }
        /// <summary>
        /// Update displayed time on UI
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UpdateTime(object? sender, EventArgs e)
        {
            MainThreadHelper.BeginInvokeOnMainThread(() =>
            {
                CurrentDateTimeDisplay = $"{DateTime.Now}";
            });

        }

        //Convert date-time to string that can be used in the UI
        [ObservableProperty]
        public partial string? CurrentDateTimeDisplay { get; set; } = DateTime.Now.ToString();
        [ObservableProperty]
        //Cannot use Preferences raw with unit testing need to create Interface and Dependency Inject it.
        public partial string TrainerName { get; set; } = "Trainer";//Preferences.Default.Get("TrainerName", "Trainer");
        [ObservableProperty]
        public partial string? NameInput { get; set; }
        [ObservableProperty]
        public partial string WelcomeMsg { get; set; }
        [ObservableProperty]
        public partial string? SavedTimeDisplay { get; set; } = "Save File";
        [ObservableProperty]
        public partial string? SavedFileDisplay { get; set; } = "Save File";

        //Match Info and Notes
        [ObservableProperty]
        public partial string PlayerSelected { get; set; } = "Other";
        [ObservableProperty]
        public partial string RivalSelected { get; set; } = "Other";
        [ObservableProperty]
        public partial string? UserNoteInput { get; set; }
        [ObservableProperty]
        public partial TimeSpan StartTime { get; set; } = new TimeSpan(0, 0, 0);
        [ObservableProperty]
        public partial TimeSpan EndTime { get; set; } = new TimeSpan(0, 0, 0);
        [ObservableProperty]
        public partial DateTime DatePlayed { get; set; } = DateTime.Now;
        [ObservableProperty]
        public partial ObservableCollection<string>? DeckNames { get; set; } = new();
        [ObservableProperty]
        public partial bool FirstCheck { get; set; }

        [ObservableProperty]
        public partial ObservableCollection<string>? PossibleResults { get; set; } = new();
        [ObservableProperty]
        public partial string Result { get; set; } = "";

        //Tags
        [ObservableProperty]
        public partial ObservableCollection<string>? TagCollection { get; set; } = new();
        [ObservableProperty]
        public partial IList<object>? TagsSelected { get; set; }

        /// <summary>
        /// Verify, Serialize, and Save Match Data
        /// </summary>
        [RelayCommand]
        public async Task SaveFile()
        {
            // get default file path
            var filePath = FileSystem.Current.AppDataDirectory + $"\\{TrainerName}.json";
            // create file if it doesn't exist
            if (!File.Exists(filePath))
            {
                File.Create(filePath);
            }

            try
            {
                //create blank match entry
                var matchEntry = new MatchEntry
                {
                    //add user inputs to match entry
                    PlayerSelected = PlayerSelected,
                    RivalSelected = RivalSelected,
                    DatePlayed = DatePlayed,
                    StartTime = StartTime,
                    EndTime = EndTime,
                    Result = Result,
                    FirstCheck = FirstCheck
                };
                if (TagsSelected != null)
                {
                    foreach (var tag in TagsSelected)
                    {
                        matchEntry.TagsSelected.Add(tag.ToString()!);
                    }
                }
                if (UserNoteInput != null) matchEntry.Note = UserNoteInput;

                //Read File from Disk throws error if file doesn't exist so it was created above
                var saveFile = await File.ReadAllTextAsync(filePath);
                //Deserialize file to add the new match or create an empty list of matches if no matches exist
                var matchList = JsonConvert.DeserializeObject<List<MatchEntry>>(saveFile)
                    ?? [];
                //add match to list
                matchList.Add(matchEntry);
                //serialize data with the new match appended to memory
                saveFile = JsonConvert.SerializeObject(matchList, Formatting.Indented);
                //write serialized data to file
                await File.WriteAllTextAsync(filePath, saveFile);

                //Clear Inputs
                SavedFileDisplay = $"Saved: Match at {CurrentDateTimeDisplay}";
                TagsSelected = null;
                FirstCheck = false;
                UserNoteInput = null;
                PlayerSelected = "Other";
                RivalSelected = "Other";
                StartTime = new TimeSpan();
                EndTime = new TimeSpan();
                Result = "";
            }
            catch (Exception)
            {

                SavedFileDisplay = $"No File Saved";
                return;
            }

        }

        //Update/Save Trainer Name to preferences and update displays
        [RelayCommand]
        public void UpdateTrainerName()
        {
        
            if (NameInput == null) return;
            //Cannot use Preferences raw with unit testing need to create Interface and Dependency Inject it.
            //Preferences.Default.Set("TrainerName", NameInput);
            TrainerName = NameInput;
            NameInput = null;
            WelcomeMsg = $"Welcome {TrainerName}";
        }

    }
}

