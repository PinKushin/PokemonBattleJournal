namespace PokemonBattleJournal.Views
{
    public partial class MainPage : ContentPage
    {
        public MainPage(MainPageViewModel vm)
        {
            InitializeComponent();
            BindingContext = vm;
        }

        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);
            bool narrow = width < 560;
            SecondColDef.Width = narrow ? new GridLength(0) : GridLength.Star;
            Grid.SetColumn(RightColumn, narrow ? 0 : 1);
            Grid.SetRow(RightColumn, narrow ? 1 : 0);
            Grid.SetColumnSpan(LeftColumn, narrow ? 2 : 1);
            Grid.SetColumnSpan(RightColumn, narrow ? 2 : 1);
        }
    }
}
