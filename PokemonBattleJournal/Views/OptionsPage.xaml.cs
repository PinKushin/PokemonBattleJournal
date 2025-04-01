namespace PokemonBattleJournal.Views;

public partial class OptionsPage : ContentPage
{
    public OptionsPage(OptionsPageViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}