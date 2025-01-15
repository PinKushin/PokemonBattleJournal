namespace PokemonBattleJournal.Views;

public partial class TrainerPage : ContentPage
{
	public TrainerPage(TrainerPageViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;
	}
}