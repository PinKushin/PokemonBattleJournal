namespace PokemonBattleJournal.Views;

public partial class ReadJournalPage : ContentPage
{
    public ReadJournalPage(ReadJournalPageViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}