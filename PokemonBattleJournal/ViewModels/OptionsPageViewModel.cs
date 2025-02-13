using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PokemonBattleJournal.ViewModels
{
	public partial class OptionsPageViewModel : ObservableObject
	{
		[ObservableProperty]
		public partial string Title { get; set; } = $"{PreferencesHelper.GetSetting("Trainer Name")}'s Options";
		[ObservableProperty]
		public partial string TrainerName { get; set; } = PreferencesHelper.GetSetting("Trainer Name");
		[ObservableProperty]
		public partial string? NameInput { get; set; }
		[ObservableProperty]
		public partial string? TagInput { get; set; }
		[ObservableProperty]
		public partial string? NewDeckName { get; set; }
		[ObservableProperty]
		public partial string? NewDeckIcon { get; set; }
		[ObservableProperty]
		public partial List<string> IconCollection { get; set; } = new List<string>();
		[ObservableProperty]
		public partial string SelectedIcon { get; set; } = "ball_icon.png";
		[ObservableProperty]
		public partial string FileConfirmMessage { get; set; } = $"Delete {PreferencesHelper.GetSetting("TrainerName")}'s Trainer File?";
		private static readonly string filePath = FileHelper.GetAppDataPath() + $@"\{PreferencesHelper.GetSetting("TrainerName")}.json";
		private bool _initialized = false;

		public OptionsPageViewModel()
		{
			
		}

		[RelayCommand]
		public async Task AppearingAsync()
		{
			if (!_initialized)
			{
				IconCollection = await PopulateIconCollectionAsync();
				_initialized = true;
			}
		}

		//Update/Save Trainer Name to preferences and update displays
		[RelayCommand]
		public void UpdateTrainerName()
		{
			if (NameInput == null) return;
			
			TrainerName = NameInput;
			PreferencesHelper.SetSetting("TrainerName", NameInput);
			NameInput = null;
			Title = $"{TrainerName}'s Options";
		}

		//Update Tags
		

		//TODO: Create a way to save a new tag to tag list

		//Update Decks
		

		private async Task<List<string>> PopulateIconCollectionAsync()
		{
			string? imageName;
			List<string> iconCollection = new List<string>();
			try
			{
				using Stream fileStream = await FileSystem.Current.OpenAppPackageFileAsync("icon_file_names.txt");
				using StreamReader reader = new StreamReader(fileStream);
				while (!reader.EndOfStream)
				{
					imageName = await reader.ReadLineAsync();
					iconCollection.Add(imageName!);
				}
				return iconCollection;

			}
			catch (Exception ex)
			{
				ModalErrorHandler modalErrorHandler = new ModalErrorHandler();
				modalErrorHandler.HandleError(ex);
				return iconCollection;
			}
			
		}

		//TODO: Create a way to save a new deck for selection on MainPage
		//TODO: create method to save all options at once
		[RelayCommand]
		public void DeleteFile()
		{

			// Delete Trainer Match file if it exists
			if (FileHelper.Exists(filePath))
			{
				FileHelper.DeleteFile(filePath);
				FileConfirmMessage = "File Deleted";
			}
			else
			{
				FileConfirmMessage = "File Not Found";
			}
		}

	}
}
