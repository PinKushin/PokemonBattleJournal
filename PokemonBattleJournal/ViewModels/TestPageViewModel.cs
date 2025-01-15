using System;
using System.Collections.Generic;
using System.IO.Enumeration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PokemonBattleJournal.ViewModels
{
    public partial class TestPageViewModel: ObservableObject
    {
        // get default file path
        private static string filePath = FileHelper.GetAppDataPath() + $"\\{PreferencesHelper.GetSetting("TrainerName")}.json";
        [ObservableProperty]
        public partial string FileSuccessMessage { get; set; } = $"Delete {PreferencesHelper.GetSetting("TrainerName")}'s Trainer File?";
        
        [RelayCommand]
        public void DeleteFile()
        {

            // Delete Trainer Match file if it exists
            if (FileHelper.Exists(filePath))
            {
                FileHelper.DeleteFile(filePath);
                FileSuccessMessage = "File Deleted";
            }
            else
            {
                FileSuccessMessage = "File Not Found";
            }
        }
    }
}
