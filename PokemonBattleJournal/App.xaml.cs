namespace PokemonBattleJournal
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
            // It can also be defined as an Object Initializer
            //return new Window() { Page = new AppShell() };
        }
    }
}