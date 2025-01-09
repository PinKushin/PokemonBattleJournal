namespace PokemonBattleJournal.Views;

public partial class CardDetailsPage : ContentPage
{
    public CardDetailsPage(CardDetailsPageViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;
	}
}