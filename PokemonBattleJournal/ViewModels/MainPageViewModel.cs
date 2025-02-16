using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace PokemonBattleJournal.ViewModels
{
	public partial class MainPageViewModel : ObservableObject
	{
		private readonly ILogger<MainPageViewModel> _logger;
		private readonly IDispatcherTimer? _timer;

		public MainPageViewModel(ILogger<MainPageViewModel> logger)
		{
			_logger = logger;

			//Timer to update displayed time
			if (Application.Current != null)
			{
				_timer = Application.Current.Dispatcher.CreateTimer();
				_timer.Interval = TimeSpan.FromSeconds(1);
				_timer.Tick += UpdateTime;
			}

			Archetypes = DataPopulationHelper.PopulateArchetypes();

			TagCollection.Add("Early Start");
			TagCollection.Add("Behind Early");
			TagCollection.Add("Donked Rival");
			TagCollection.Add("Got Donked");
			TagCollection.Add("Lucky");
			TagCollection.Add("Unlucky");
			TagCollection.Add("Never Punished");
			TagCollection.Add("Punished");

			WelcomeMsg = $"Welcome {TrainerName}";
			// get default file path
			string? filePath = FileHelper.GetAppDataPath() + $@"\{TrainerName}.json";
			// create file if it doesn't exist
			if (!FileHelper.Exists(filePath))
			{
				FileHelper.CreateFile(filePath);
			}
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

		[RelayCommand]
		public void Appearing()
		{
			if (_timer is not null)
				_timer.Start();
		}

		[RelayCommand]
		public void Disappearing()
		{
			if (_timer is not null)
				_timer.Stop();
		}

		//Convert date-time to string that can be used in the UI
		[ObservableProperty]
		public partial string? CurrentDateTimeDisplay { get; set; } = DateTime.Now.ToString();

		[ObservableProperty]
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
		public partial DateTimeOffset? StartTime { get; set; } = DateTimeOffset.Now;

		[ObservableProperty]
		public partial DateTimeOffset? EndTime { get; set; } = DateTimeOffset.Now;

		[ObservableProperty]
		public partial DateTimeOffset DatePlayed { get; set; } = DateTimeOffset.Now;

		[ObservableProperty]
		public partial ObservableCollection<Archetype> Archetypes { get; set; }

		[ObservableProperty]
		public partial bool BO3Toggle { get; set; }

		[ObservableProperty]
		public partial bool FirstCheck { get; set; }

		[ObservableProperty]
		public partial bool FirstCheck2 { get; set; }

		[ObservableProperty]
		public partial bool FirstCheck3 { get; set; }

		[ObservableProperty]
		public partial List<string> PossibleResults { get; set; } = new() { "Win", "Loss", "Tie" };

		[ObservableProperty]
		public partial string Result { get; set; } = "";

		[ObservableProperty]
		public partial string Result2 { get; set; } = "";

		[ObservableProperty]
		public partial string Result3 { get; set; } = "";

		//Tags
		[ObservableProperty]
		public partial ObservableCollection<string> TagCollection { get; set; } = new();

		[ObservableProperty]
		public partial IList<string>? TagsSelected { get; set; }

		[ObservableProperty]
		public partial IList<string>? Match2TagsSelected { get; set; }

		[ObservableProperty]
		public partial IList<string>? Match3TagsSelected { get; set; }

		[ObservableProperty]
		public partial bool? IsToggled { get; set; }

		/// <summary>
		/// Verify, Serialize, and Save Match Data
		/// </summary>
		[RelayCommand]
		public async Task SaveFile()
		{
			// get default file path
			string filePath = FileHelper.GetAppDataPath() + $@"\{TrainerName}.json";
			// create file if it doesn't exist
			if (!FileHelper.Exists(filePath))
			{
				FileHelper.CreateFile(filePath);
			}

			try
			{
				MatchEntry matchEntry = CreateMatchEntry();
				//Read File from Disk throws error if file doesn't exist so it was created above
				string? saveFile = await FileHelper.ReadFileAsync(filePath);
				//Deserialize file to add the new match or create an empty list of matches if no matches exist
				List<MatchEntry> matchList = JsonConvert.DeserializeObject<List<MatchEntry>>(saveFile) ?? [];

				//add match to list
				matchList.Add(matchEntry);
				//serialize data with the new match appended to memory
				saveFile = JsonConvert.SerializeObject(matchList, Formatting.Indented);
				//write serialized data to file
				await FileHelper.WriteFileAsync(filePath, saveFile);
				SavedFileDisplay = $"Saved: Match at {DateTimeOffset.Now}";
			}
			catch (Exception ex)
			{
				SavedFileDisplay = $"No File Saved";
				ModalErrorHandler modalErrorHandler = new ModalErrorHandler();
				modalErrorHandler.HandleError(ex);
				_logger.LogError(ex, "Error Saving File");
				return;
			}
		}

		public MatchEntry CreateMatchEntry()
		{
			var matchEntry = new MatchEntry
			{
				//add user inputs to match entry
				Playing = PlayerSelected ?? new("Other", "ball_icon.png"),
				Against = RivalSelected ?? new("Other", "ball_icon.png"),
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
			if (UserNoteInput != null)
				matchEntry.Game1.Notes = UserNoteInput;
			if (TagsSelected is not null)
			{
				matchEntry.Game1.Tags = new();
				matchEntry.Game1.Tags.AddRange(from string tag in TagsSelected
											   select tag);
			}

			if (!BO3Toggle)
			{
				matchEntry.Result = Result;
			}
			else
			{
				uint _wins = 0;
				uint _draws = 0;
				matchEntry.Game2 = new Game();
				matchEntry.Game3 = new Game();
				matchEntry.Game2.Result = Result2;
				switch (Result)
				{
					case "Win":
						_wins++;
						break;

					case "Tie":
						_draws++;
						break;

					default:
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

					default:
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

					default:
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
				else if (_draws == 1 && _wins == 1)
				{
					matchEntry.Result = "Tie";
				}
				else
				{
					matchEntry.Result = "Loss";
				}

				if (UserNoteInput2 != null)
					matchEntry.Game2.Notes = UserNoteInput2;
				if (UserNoteInput3 != null)
					matchEntry.Game3.Notes = UserNoteInput3;

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

				if (Match2TagsSelected is not null)
				{
					matchEntry.Game2.Tags = new();
					matchEntry.Game2.Tags.AddRange(from string tag in Match2TagsSelected
												   select tag);
				}

				if (Match3TagsSelected != null)
				{
					matchEntry.Game3.Tags = new();
					matchEntry.Game3.Tags.AddRange(from string tag in Match3TagsSelected
												   select tag);
				}
			}
			//Clear Inputs
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
			StartTime = new();
			EndTime = new();
			DatePlayed = new();
			Result = "";
			Result2 = "";
			Result3 = "";
			BO3Toggle = false;
			return matchEntry;
		}
	}
}