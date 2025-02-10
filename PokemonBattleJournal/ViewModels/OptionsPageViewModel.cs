using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        public OptionsPageViewModel()
        {
            PopulateIconCollection();
        }

        //Update/Save Trainer Name to preferences and update displays
        [ObservableProperty]
        public partial string? NameInput { get; set; }

        [RelayCommand]
        public void UpdateTrainerName()
        {
            if (NameInput == null) return;
            //Cannot use Preferences raw with unit testing
            PreferencesHelper.SetSetting("TrainerName", NameInput);
            TrainerName = NameInput;
            NameInput = null;
            Title = $"{TrainerName}'s Options";
        }

        //Update Tags
        [ObservableProperty]
        public partial string? TagInput { get; set; }

        //TODO: Create a way to save a new tag to tag list

        //Update Decks
        [ObservableProperty]
        public partial string? NewDeckName { get; set; }
        [ObservableProperty]
        public partial string? NewDeckIcon { get; set; }
        [ObservableProperty]
        public partial List<string> IconCollection { get; set; } = new List<string>();
        [ObservableProperty]
        public partial string? SelectedIcon { get; set; }

        private async void PopulateIconCollection()
        {
            string? imageName;
            try
            {
                using Stream fileStream = await FileSystem.Current.OpenAppPackageFileAsync("icon_file_names.txt");
                using StreamReader reader = new StreamReader(fileStream);
                while (!reader.EndOfStream)
                {
                    imageName = await reader.ReadLineAsync();
                    IconCollection.Add(imageName!);
                }
            }
            catch (Exception ex)
            {
                ModalErrorHandler modalErrorHandler = new ModalErrorHandler();
                modalErrorHandler.HandleError(ex);
                return;
            }
            
        }

        //TODO: Create a way to save a new deck for selection on MainPage

        //Save all options
        //TODO: create method to save all options at once

        //Delete trainer's save
        //Get default file path
        private static readonly string filePath = FileHelper.GetAppDataPath() + $"\\{PreferencesHelper.GetSetting("TrainerName")}.json";
        [ObservableProperty]
        public partial string FileConfirmMessage { get; set; } = $"Delete {PreferencesHelper.GetSetting("TrainerName")}'s Trainer File?";

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
