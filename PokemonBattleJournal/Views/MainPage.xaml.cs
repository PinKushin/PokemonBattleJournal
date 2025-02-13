namespace PokemonBattleJournal.Views
{
    public partial class MainPage : ContentPage
    {
        public MainPage(MainPageViewModel vm)
        {
            InitializeComponent();
            BindingContext = vm;
			if (!OperatingSystem.IsWindows())
			{
				//RightColumn.Rotation = 90;
			}
        }

		private void OpenTimePickers(object sender, EventArgs e)
		{
			
			EndTimePicker.IsOpen = true;
			StartTimePicker.IsOpen = true;
		}

		private void OpenDatePlayedPicker(object sender, EventArgs e)
		{
			DatePlayedPicker.IsOpen = true;
		}
	}

}
