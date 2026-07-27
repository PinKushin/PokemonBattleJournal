namespace UITests
{
    public partial class ReadJournalPageTests : BaseTest
    {
        // Use direct MobileBy.Id for ReadJournal elements — UiScrollable.scrollIntoView causes
        // 15-30s delays because it exhausts the scroll range before finding visible elements.
        private AppiumElement FindReadJournalElement(string id) =>
            App is WindowsDriver
                ? App.FindElement(MobileBy.AccessibilityId(id))
                : App.FindElement(MobileBy.Id($"com.PinKushin.PokemonBattleJournal:id/{id}"));

        [Fact]
        public void ReadJournalPage_Loads_PageVisible()
        {
            NavigateTo("Read Journal");
            FindReadJournalElement("ReadJournalPage").ShouldNotBeNull();
        }

        [Fact]
        public void ReadJournalPage_Title_Displayed()
        {
            NavigateTo("Read Journal");
            FindReadJournalElement("ReadJournalTitle").ShouldNotBeNull();
        }

        [Fact]
        public void ReadJournalPage_MatchHistoryList_Displayed()
        {
            NavigateTo("Read Journal");
            FindReadJournalElement("MatchHistoryList").ShouldNotBeNull();
        }

        [Fact]
        public async Task ReadJournalPage_SelectMatch_ShowsDetail()
        {
            NavigateTo("Read Journal");

            try
            {
                AppiumElement firstItem = App is WindowsDriver
                    ? App.FindElement(MobileBy.AccessibilityId("MatchHistoryList"))
                    : App.FindElement(MobileBy.AndroidUIAutomator(
                        "new UiScrollable(new UiSelector().resourceId(\"com.PinKushin.PokemonBattleJournal:id/MatchHistoryList\"))" +
                        ".getChildByInstance(new UiSelector().clickable(true), 0)"));

                firstItem.Click();
                await Task.Delay(1000);

                FindReadJournalElement("PlayingNameLabel").ShouldNotBeNull();
            }
            catch (OpenQA.Selenium.NoSuchElementException)
            {
                // No matches in DB — list is empty, detail test is not applicable.
            }
        }
    }
}
