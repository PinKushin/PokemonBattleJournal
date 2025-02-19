using System.Collections.ObjectModel;
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
		public partial string? SelectedNote2 { get; set; } = "Select Match";

		[ObservableProperty]
		public partial string? SelectedNote3 { get; set; } = "Select Match";

		[ObservableProperty]
		public partial MatchEntry? SelectedMatch { get; set; }

		[ObservableProperty]
		public partial string? ResultGame1 { get; set; }

		[ObservableProperty]
		public partial string? ResultGame2 { get; set; }

		[ObservableProperty]
		public partial string? ResultGame3 { get; set; }

		[ObservableProperty]
		public partial string? OverallResult { get; set; }

		[ObservableProperty]
		public partial string? PlayingName { get; set; } = "other";

		[ObservableProperty]
		public partial string? AgainstName { get; set; } = "other";

		[ObservableProperty]
		public partial string? PlayingIconSource { get; set; } = "ball_icon.png";

		[ObservableProperty]
		public partial string? AgainstIconSource { get; set; } = "ball_icon.png";

		[ObservableProperty]
		public partial List<string>? TagsSelectedGame1 { get; set; }

		[ObservableProperty]
		public partial List<string>? TagsSelectedGame2 { get; set; }

		[ObservableProperty]
		public partial List<string>? TagsSelectedGame3 { get; set; }

		[ObservableProperty]
		public partial ObservableCollection<MatchEntry> MatchHistory { get; set; } = new();

		public ReadJournalPageViewModel()
		{
			WelcomeMsg = $"{TrainerName}'s Journal";
		}

		[RelayCommand]
		public async Task AppearingAsync()
		{
			MatchHistory = await LoadMatchHistoryAsync();
		}

		private static async Task<ObservableCollection<MatchEntry>> LoadMatchHistoryAsync()
		{
			ObservableCollection<MatchEntry> matchHistory = new();
			if (FileHelper.Exists(filePath))
			{
				//Read File from Disk throws error if file doesn't exist so it was checked above
				var saveFile = await FileHelper.ReadFileAsync(filePath);
				//Deserialize file or create an empty list of matches if no matches exist
				matchHistory = JsonConvert.DeserializeObject<ObservableCollection<MatchEntry>>(saveFile)
					?? [];
			}
			return matchHistory;
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

			if (SelectedMatch.Game2 != null)
			{
				ResultGame2 = SelectedMatch.Game2.Result;
				SelectedNote2 = SelectedMatch.Game2.Notes;
				TagsSelectedGame2 = SelectedMatch.Game2.Tags;
			}

			if (SelectedMatch.Game3 != null)
			{
				ResultGame2 = SelectedMatch.Game3.Result;
				SelectedNote2 = SelectedMatch.Game3.Notes;
				TagsSelectedGame2 = SelectedMatch.Game3.Tags;
			}
			PlayingName = SelectedMatch.Playing?.Name;
			AgainstName = SelectedMatch.Against?.Name;
			PlayingIconSource = SelectedMatch.Playing?.ImagePath;
			AgainstIconSource = SelectedMatch.Against?.ImagePath;
		}
	}
}