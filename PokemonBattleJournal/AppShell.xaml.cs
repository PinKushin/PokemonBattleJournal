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

        /// <summary>
        /// The Shell's own view model, exposed as a typed property so XAML can bind through it.
        /// </summary>
        /// <remarks>
        /// Bindings that reach the Shell's context from inside a typed DataTemplate used to be written
        /// as <c>Path=BindingContext.SomeCommand</c>. <c>BindingContext</c> is declared as
        /// <c>object</c>, and inside a template the compiler resolves the path against the ITEM type,
        /// so it emitted XC0045 ("Property BindingContext not found on ...Trainer") and silently fell
        /// back to reflection-based binding. Going through a typed property lets the binding compile.
        /// </remarks>
        public AppShellViewModel? ViewModel => BindingContext as AppShellViewModel;
    }
}