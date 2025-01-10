using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;

namespace PokemonBattleJournal.ViewModels
{
    public partial class MainPageViewModel : ObservableObject
    {
     
        public MainPageViewModel()
        {
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
            PossibleResults.Add("Tie");

            WelcomeMsg = $"Welcome {TrainerName}";

        }
        /// <summary>
        /// Update displayed time on UI
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void UpdateTime(object? sender, EventArgs e)
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
        public partial string TrainerName { get; set; } = PreferencesHelper.GetSetting("TrainerName");
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
        public partial string? UserNoteInput2 { get; set; }
        [ObservableProperty]
        public partial string? UserNoteInput3 { get; set; }
        [ObservableProperty]
        public partial TimeSpan StartTime { get; set; } = new TimeSpan(0, 0, 0);
        [ObservableProperty]
        public partial TimeSpan EndTime { get; set; } = new TimeSpan(0, 0, 0);
        [ObservableProperty]
        public partial DateTime DatePlayed { get; set; } = DateTime.Now;
        [ObservableProperty]
        public partial ObservableCollection<string>? DeckNames { get; set; } = new();


        [ObservableProperty]
        public partial bool BO3Toggle { get; set; }
        [ObservableProperty]
        public partial bool FirstCheck { get; set; }
        [ObservableProperty]
        public partial bool FirstCheck2 { get; set; }
        [ObservableProperty]
        public partial bool FirstCheck3 { get; set; }

        [ObservableProperty]
        public partial ObservableCollection<string>? PossibleResults { get; set; } = new();
        [ObservableProperty]
        public partial string Result { get; set; } = "";
        [ObservableProperty]
        public partial string Result2 { get; set; } = "";
        [ObservableProperty]
        public partial string Result3 { get; set; } = "";

        //Tags
        [ObservableProperty]
        public partial ObservableCollection<string>? TagCollection { get; set; } = new();
        [ObservableProperty]
        public partial IList<object>? TagsSelected { get; set; }
        [ObservableProperty]
        public partial IList<object>? Match2TagsSelected { get; set; }
        [ObservableProperty]
        public partial IList<object>? Match3TagsSelected { get; set; }
        [ObservableProperty]
        public partial bool? IsToggled { get; set; }

        /// <summary>
        /// Verify, Serialize, and Save Match Data
        /// </summary>
        [RelayCommand]
        public async Task SaveFile()
        {
            // get default file path
            var filePath = FileHelper.GetAppDataPath() + $"\\{TrainerName}.json";
            // create file if it doesn't exist
            if (!FileHelper.Exists(filePath))
            {
                FileHelper.CreateFile(filePath);
            }

            try
            {
                //create blank match entry
                var matchEntry = new MatchEntry
                {
                    //add user inputs to match entry
                    Playing = PlayerSelected,
                    Against = RivalSelected,
                    Time = DatePlayed,
                    DatePlayed = DatePlayed,
                    StartTime = StartTime,
                    EndTime = EndTime
                };

                matchEntry.Game1 = new Game();
                matchEntry.Game1.Result = Result;
                if (FirstCheck)
                {
                    matchEntry.Game1.Turn = 1;
                }
                else
                {
                    matchEntry.Game1.Turn = 2;
                }
                if (UserNoteInput != null) matchEntry.Game1.Notes = UserNoteInput;
                if (!BO3Toggle)
                {
                    matchEntry.Result = Result;
                }
                else
                {
                    int _wins = 0;
                    int _draws = 0;
                    matchEntry.Game2 = new Game();
                    matchEntry.Game3 = new Game();
                    matchEntry.Game2.Result = Result2;
                    switch (Result2)
                    {
                        case "Win":
                            _wins++;
                            break;

                        case "Tie":
                            _draws++;
                            break;
                    }
                    switch (Result2)
                    {
                        case "Win":
                            _wins++;
                            break;

                        case "Tie":
                            _draws++; 
                            break;
                    }
                    matchEntry.Game3.Result = Result3;
                    switch (Result3)
                    {
                        case "Win":
                            _wins++;
                            break;

                        case "Tie":
                            _draws++;
                            break;
                    }
                    
                    if (_wins >= 2)
                    {
                        matchEntry.Result = "Win";
                    }
                    else if (_draws >= 2)
                    {
                        matchEntry.Result = "Tie";
                    }
                    else
                    {
                        matchEntry.Result = "Loss";
                    }

                    if (UserNoteInput2 != null) matchEntry.Game2.Notes = UserNoteInput2;
                    if (UserNoteInput3 != null) matchEntry.Game3.Notes = UserNoteInput3;

                    if (FirstCheck2)
                    {
                        matchEntry.Game2.Turn = 1;
                    }
                    else
                    {
                        matchEntry.Game2.Turn = 2;
                    }

                    if (FirstCheck3)
                    {
                        matchEntry.Game3.Turn = 1;
                    }
                    else
                    {
                        matchEntry.Game3.Turn = 2;
                    }

                    if (Match2TagsSelected != null)
                    {
                        matchEntry.Game2.Tags = new();
                        foreach (var tag in Match2TagsSelected)
                        {
                            matchEntry.Game2.Tags.Add(tag.ToString()!);
                        }
                    }

                    if (Match3TagsSelected != null)
                    {
                        matchEntry.Game3.Tags = new();
                        foreach (var tag in Match3TagsSelected)
                        {
                            matchEntry.Game3.Tags.Add(tag.ToString()!);
                        }
                    }
                }
                //Read File from Disk throws error if file doesn't exist so it was created above
                var saveFile = await FileHelper.ReadFileAsync(filePath);
                //Deserialize file to add the new match or create an empty list of matches if no matches exist
                var matchList = JsonConvert.DeserializeObject<List<MatchEntry>>(saveFile)
                    ?? [];
                //add match to list
                matchList.Add(matchEntry);
                //serialize data with the new match appended to memory
                saveFile = JsonConvert.SerializeObject(matchList, Formatting.Indented);
                //write serialized data to file
                await FileHelper.WriteFileAsync(filePath, saveFile);

                //Clear Inputs
                SavedFileDisplay = $"Saved: Match at {CurrentDateTimeDisplay}";
                TagsSelected = null;
                Match2TagsSelected = null;
                Match3TagsSelected = null;
                FirstCheck = false;
                FirstCheck2 = false;
                FirstCheck3 = false;
                UserNoteInput = null;
                UserNoteInput2 = null;
                UserNoteInput3 = null;
                PlayerSelected = "Other";
                RivalSelected = "Other";
                StartTime = new TimeSpan();
                EndTime = new TimeSpan();
                Result = "";
                Result2 = "";
                Result3 = "";
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
            //Cannot use Preferences raw with unit testing
            PreferencesHelper.SetSetting("TrainerName", NameInput);
            TrainerName = NameInput;
            NameInput = null;
            WelcomeMsg = $"Welcome {TrainerName}";
        }

    }
}

