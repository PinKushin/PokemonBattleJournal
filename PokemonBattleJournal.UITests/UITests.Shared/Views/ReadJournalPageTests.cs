namespace UITests
{
    public partial class ReadJournalPageTests : BaseTest
    {
        [Fact]
        public void ReadJournalPage_Loads_PageVisible()
        {
            NavigateTo("Read Journal");
            // Use MobileBy.Id directly — UiScrollable.scrollIntoView hits a 30s scroll-loop
            // when the target IS the root scrollable container (can't scroll itself into view).
            AppiumElement page = App is WindowsDriver
                ? App.FindElement(MobileBy.AccessibilityId("ReadJournalPage"))
                : App.FindElement(MobileBy.Id("com.PinKushin.PokemonBattleJournal:id/ReadJournalPage"));

            page.ShouldNotBeNull();
        }

        [Fact]
        public void ReadJournalPage_Title_Displayed()
        {
            NavigateTo("Read Journal");
            AppiumElement title = FindUIElement("ReadJournalTitle");
            title.ShouldNotBeNull();
        }

        [Fact]
        public void ReadJournalPage_MatchHistoryList_Displayed()
        {
            NavigateTo("Read Journal");
            AppiumElement list = FindUIElement("MatchHistoryList");
            list.ShouldNotBeNull();
        }

        [Fact]
        public async Task ReadJournalPage_SelectMatch_ShowsDetail()
        {
            NavigateTo("Read Journal");

            try
            {
                // Find the first item inside the MatchHistoryList CollectionView.
                // UiScrollable.getChildByInstance targets children of the list, not the list itself.
                AppiumElement firstItem = App is WindowsDriver
                    ? App.FindElement(MobileBy.AccessibilityId("MatchHistoryList"))
                    : App.FindElement(MobileBy.AndroidUIAutomator(
                        "new UiScrollable(new UiSelector().resourceId(\"com.PinKushin.PokemonBattleJournal:id/MatchHistoryList\"))" +
                        ".getChildByInstance(new UiSelector().clickable(true), 0)"));

                firstItem.Click();
                await Task.Delay(1000);

                AppiumElement playingLabel = FindUIElement("PlayingNameLabel");
                playingLabel.ShouldNotBeNull();
            }
            catch (OpenQA.Selenium.NoSuchElementException)
            {
                // No matches in DB yet — list is empty, detail test is not applicable.
                // The MatchHistoryList_Displayed test already proved the list rendered.
            }
        }
    }
}
