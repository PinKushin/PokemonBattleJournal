namespace PokemonBattleJournal.Views;

public partial class DeckBuilderPage : ContentPage
{
	public DeckBuilderPage(DeckBuilderPageViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;
	}
}