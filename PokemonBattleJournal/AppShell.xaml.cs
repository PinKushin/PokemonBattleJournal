namespace PokemonBattleJournal
{
    public partial class AppShell : Shell
    {
        public AppShell(AppShellViewModel vm)
        {
            InitializeComponent();
            BindingContext = vm;
            Loaded += async (_, _) => await vm.LoadAsync();
        }
    }
}