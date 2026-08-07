namespace PokemonBattleJournal.Views
{
    public partial class MainPage : ContentPage
    {
        public MainPage(MainPageViewModel vm)
        {
            InitializeComponent();
            BindingContext = vm;
        }

        /// <summary>
        /// Width at or below which the two columns stack instead of sitting side by side.
        /// </summary>
        /// <remarks>
        /// Was 560, which is below what two columns actually need. Each side wants roughly
        /// 350-380px before its contents start clipping — the note editor stops shrinking
        /// gracefully, and the result picker and rival archetype picker clip on the right — so
        /// anything under ~800 total is already too tight for the side-by-side layout.
        ///
        /// This is also a test-stability fix, not only a cosmetic one. CI runs the app at
        /// 754x512 (logged as "App window: 754x512"), which cleared the old 560 threshold and
        /// so ran two-column at ~377px per side. A control clipped at the window edge can have
        /// its click coordinate fall OUTSIDE the window entirely: observed locally at that size,
        /// a click landed on the browser behind the app and brought it to the front. On CI there
        /// is nothing behind, so the same click hits the desktop and silently does nothing —
        /// which is the signature of the open MainPage click flake (dispatched, ~1000ms, no
        /// handler). See docs/memory/project_windows_mainpage_click_flake.md.
        ///
        /// Stacking at CI's width means nothing needs to be scrolled or clipped to be clicked,
        /// which removes the failure mode rather than compensating for it.
        /// </remarks>
        private const double StackColumnsBelowWidth = 800;

        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);
            bool narrow = width < StackColumnsBelowWidth;
            SecondColDef.Width = narrow ? new GridLength(0) : GridLength.Star;
            Grid.SetColumn(RightColumn, narrow ? 0 : 1);
            Grid.SetRow(RightColumn, narrow ? 1 : 0);
            Grid.SetColumnSpan(LeftColumn, narrow ? 2 : 1);
            Grid.SetColumnSpan(RightColumn, narrow ? 2 : 1);
        }
    }
}
