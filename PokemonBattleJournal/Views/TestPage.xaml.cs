namespace PokemonBattleJournal.Views;

public partial class TestPage : ContentPage
{
	public TestPage(TestPageViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;
	}
}