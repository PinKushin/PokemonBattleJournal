namespace PokemonBattleJournal
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(CardDetailsPage), typeof(CardDetailsPage));
        }
    }
}
