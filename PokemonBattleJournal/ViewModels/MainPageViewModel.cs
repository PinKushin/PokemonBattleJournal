using System.Collections.ObjectModel;
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

			_logger.LogInformation("Created ViewModel");
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
				CurrentDateTimeDisplay = $"{DateTime.Now.ToLocalTime()}";
			});
		}

		[RelayCommand]
		public async Task AppearingAsync()
		{
			_logger.LogInformation("Appearing: {Time}", DateTime.Now);
			try
			{
				_timer?.Start();
				Archetypes = await DataPopulationHelper.PopulateArchetypesAsync();
				TagCollection = await DataPopulationHelper.PopulateTagsAsync();
				_logger.LogInformation("{@ArcheTypes}", Archetypes);
			}
			catch (Exception ex)
			{
				ModalErrorHandler modalErrorHandler = new ModalErrorHandler();
				modalErrorHandler.HandleError(ex);
				_logger.LogError(ex, "Error Loading ViewModel");
			}
		}

		[RelayCommand]
		public void Disappearing()
		{
			_logger.LogInformation("Disappering: {Time}", DateTime.Now);
			_timer?.Stop();
		}

		//Convert date-time to string that can be used in the UI
		[ObservableProperty]
		public partial string? CurrentDateTimeDisplay { get; set; } = DateTime.Now.ToLocalTime().ToString();

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
		public partial DateTime StartTime { get; set; } = DateTime.Now.ToLocalTime();

		[ObservableProperty]
		public partial DateTime EndTime { get; set; } = DateTime.Now.ToLocalTime();

		[ObservableProperty]
		public partial DateTime DatePlayed { get; set; } = DateTime.Now.ToLocalTime();

		[ObservableProperty]
		public partial ObservableCollection<Archetype>? Archetypes { get; set; }

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
		public partial IList<object>? TagCollection { get; set; }

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
		public async Task SaveFileAsync()
		{
			// get default file path
			string filePath = FileHelper.GetAppDataPath() + $@"\{TrainerName}.json";
			_logger.LogInformation("FilePath: {FilePath}", filePath);
			// create file if it doesn't exist
			if (!FileHelper.Exists(filePath))
			{
				FileHelper.CreateFile(filePath);
				_logger.LogInformation("File Created: {FilePath}", filePath);
			}

			try
			{
				MatchEntry matchEntry = CreateMatchEntry();
				_logger.LogInformation("Match Created: {@Match}", matchEntry);
				//Read File from Disk throws error if file doesn't exist so it was created above
				string? saveFile = await FileHelper.ReadFileAsync(filePath);
				_logger.LogInformation("Read Save File: {@Save}", saveFile);
				//Deserialize file to add the new match or create an empty list of matches if no matches exist
				List<MatchEntry> matchList = JsonConvert.DeserializeObject<List<MatchEntry>>(saveFile) ?? [];
				//add match to list
				matchList.Add(matchEntry);
				//serialize data with the new match appended to memory
				saveFile = JsonConvert.SerializeObject(matchList, Formatting.Indented);
				_logger.LogInformation("Save File Updated: {@Save}", saveFile);
				//write serialized data to file
				await FileHelper.WriteFileAsync(filePath, saveFile);
				SavedFileDisplay = $"Saved: Match at {DateTimeOffset.Now}";
				_logger.LogInformation("Saved file: {@SaveFile} at {FilePath}", saveFile, filePath);
			}
			catch (Exception ex)
			{
				SavedFileDisplay = $"No File Saved";
				ModalErrorHandler modalErrorHandler = new ModalErrorHandler();
				modalErrorHandler.HandleError(ex);
				_logger.LogError(ex, "Error Saving File at {FilePath}", filePath);
				return;
			}
		}

		public MatchEntry CreateMatchEntry()
		{
			_logger.LogInformation("Creating Match Entry...");
			_logger.LogInformation("Tags Selected: {Tags}", TagsSelected);
			MatchEntry matchEntry = new()
			{
				//add user inputs to match entry
				Playing = PlayerSelected ?? new("Other", "ball_icon.png"),
				Against = RivalSelected ?? new("Other", "ball_icon.png"),
				Time = DatePlayed,
				DatePlayed = DatePlayed,
				StartTime = StartTime,
				EndTime = EndTime,
				Game1 = new Game()
			};
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

			matchEntry.Game1.Tags = TagsSelected;

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
				matchEntry.Game2.Tags = Match2TagsSelected;
				matchEntry.Game3.Tags = Match3TagsSelected;
				matchEntry.Game2.Result = Result2;
				matchEntry.Game3.Result = Result3;
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
			StartTime = new DateTime(DateTime.Now.Ticks, DateTimeKind.Local);
			EndTime = new DateTime(DateTime.Now.Ticks, DateTimeKind.Local);
			DatePlayed = new DateTime(DateTime.Now.Ticks, DateTimeKind.Local);
			Result = "";
			Result2 = "";
			Result3 = "";
			BO3Toggle = false;
			_logger.LogInformation("Match Creation Complete");
			return matchEntry;
		}
	}
}