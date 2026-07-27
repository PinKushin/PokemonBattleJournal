namespace UITests
{
    public partial class ReadJournalPageTests : BaseTest
    {
        [Fact]
        public void ReadJournalPage_Loads_PageVisible()
        {
            NavigateTo("Read Journal");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            // Use MobileBy.Id directly — UiScrollable.scrollIntoView hits a 30s scroll-loop
            // when the target IS the root scrollable container (can't scroll itself into view).
            AppiumElement page = App is WindowsDriver
                ? App.FindElement(MobileBy.AccessibilityId("ReadJournalPage"))
                : App.FindElement(MobileBy.Id("com.PinKushin.PokemonBattleJournal:id/ReadJournalPage"));
            sw.Stop();

            page.Displayed.ShouldBeTrue();
            // UIAutomator2 accessibility-tree refresh adds ~5-10s overhead on Android
            // regardless of actual render time. Threshold guards against regression, not perf.
            sw.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(15));
        }
    }
}
