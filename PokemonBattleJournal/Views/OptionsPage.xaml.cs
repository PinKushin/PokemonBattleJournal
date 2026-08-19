namespace PokemonBattleJournal.Views;

public partial class OptionsPage : ContentPage
{
    public OptionsPage(OptionsPageViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    /// <summary>
    /// The Shell's own view model, exposed as a typed property so XAML can bind through it.
    /// </summary>
    /// <remarks>
    /// Bindings that reach the Shell's context from inside a typed DataTemplate used to be written
    /// as <c>Path=BindingContext.SomeCommand</c>. <c>BindingContext</c> is declared as
    /// <c>object</c>, and inside a template the compiler resolves the path against the ITEM type,
    /// so it emitted XC0045 ("Property BindingContext not found on ...Archetype") and silently fell
    /// back to reflection-based binding. Going through a typed property lets the binding compile.
    /// </remarks>
    public OptionsPageViewModel? ViewModel => BindingContext as OptionsPageViewModel;
}