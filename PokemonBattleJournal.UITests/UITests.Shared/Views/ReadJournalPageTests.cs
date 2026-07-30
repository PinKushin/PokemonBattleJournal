namespace UITests
{
    public partial class ReadJournalPageTests : BaseTest
    {
        [OneTimeSetUp]
        public void SetUp() => NavigateTo("Read Journal");

        private AppiumElement FindReadJournalElement(string id) => FindUIElement(id);

        [Test]
        public void ReadJournalPage_Loads_PageVisible()
        {
            FindReadJournalElement("ReadJournalPage").ShouldNotBeNull();
        }

        [Test]
        public void ReadJournalPage_Title_Displayed()
        {
            FindReadJournalElement("ReadJournalTitle").ShouldNotBeNull();
        }

        [Test]
        public void ReadJournalPage_MatchHistoryList_Displayed()
        {
            FindReadJournalElement("MatchHistoryList").ShouldNotBeNull();
        }

        [Test]
        public void ReadJournalPage_HasSeededMatches()
        {

            // Find the first seeded match row — AutomationId is bound to match Id.
            // SeedTestData seeds 3 matches so at least MatchRow_1 must exist.
            // Failure here means seeding failed or data didn't load for the active trainer.
            AppiumElement firstRow = App is WindowsDriver
                ? App.FindElements(MobileBy.XPath("//*[contains(@AutomationId,'MatchRow_')]"))
                    .FirstOrDefault()
                    ?? throw new Exception("No MatchRow_ elements found — ReadJournal loaded empty")
                : App.FindElement(MobileBy.AndroidUIAutomator(
                    "new UiSelector().resourceIdMatches(\"com.PinKushin.PokemonBattleJournal:id/MatchRow_.*\")"));

            firstRow.ShouldNotBeNull();
        }

        [Test]
        public async Task ReadJournalPage_SelectMatch_ShowsDetail()
        {

            // Find and click the first match row by its bound AutomationId.
            // Do NOT catch NoSuchElementException — empty list is a real failure (seed broken).
            AppiumElement firstRow = App is WindowsDriver
                ? App.FindElements(MobileBy.XPath("//*[contains(@AutomationId,'MatchRow_')]"))
                    .FirstOrDefault()
                    ?? throw new Exception("No match rows found — seeded data missing")
                : App.FindElement(MobileBy.AndroidUIAutomator(
                    "new UiSelector().resourceIdMatches(\"com.PinKushin.PokemonBattleJournal:id/MatchRow_.*\")"));

            firstRow.Click();
            await Task.Delay(500);

            FindReadJournalElement("PlayingNameLabel").ShouldNotBeNull();
        }
    }
}
