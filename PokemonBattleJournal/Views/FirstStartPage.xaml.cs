namespace PokemonBattleJournal.Views;

public partial class FirstStartPage : ContentPage
{
    public FirstStartPage(FirstStartPageViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}