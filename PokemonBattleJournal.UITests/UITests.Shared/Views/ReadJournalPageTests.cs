namespace UITests
{
    public partial class ReadJournalPageTests : BaseTest
    {
        [OneTimeSetUp]
        public void SetUp() => NavigateTo("Read Journal");

        private AppiumElement FindReadJournalElement(string id) => FindUIElement(id);

        // Returns the first match row — any match, used for single-game detail tests.
        private AppiumElement FindFirstMatchRow() =>
            App is WindowsDriver
                ? App.FindElements(MobileBy.XPath("//*[contains(@AutomationId,'MatchRow_')]"))
                    .FirstOrDefault()
                    ?? throw new Exception("No match rows found — seeded data missing")
                : App.FindElement(MobileBy.AndroidUIAutomator(
                    "new UiSelector().resourceIdMatches(\"com.PinKushin.PokemonBattleJournal:id/MatchRow_.*\")"));

        // Returns the last match row — the BO3 Loss (3 games) seeded last.
        private AppiumElement FindLastMatchRow() =>
            App is WindowsDriver
                ? App.FindElements(MobileBy.XPath("//*[contains(@AutomationId,'MatchRow_')]"))
                    .LastOrDefault()
                    ?? throw new Exception("No match rows found — seeded data missing")
                : App.FindElements(MobileBy.XPath("//*[contains(@resource-id,'MatchRow_')]"))
                    .LastOrDefault()
                    ?? throw new Exception("No match rows found — seeded data missing");

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
        public void ReadJournalPage_SelectMatch_ShowsDetail()
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

            // Poll for detail panel — implicit wait blocks until PlayingNameLabel appears.
            FindReadJournalElement("PlayingNameLabel").ShouldNotBeNull();
        }

        [Test]
        public void ReadJournalPage_SelectMatch_ShowsArchetypeNames()
        {
            FindFirstMatchRow().Click();

            // With diverse seed, archetypes are non-empty strings (e.g. "Other", "Charizard").
            string playingName = FindReadJournalElement("PlayingNameLabel").Text;
            string againstName = FindReadJournalElement("AgainstNameLabel").Text;

            playingName.ShouldNotBeNullOrEmpty("PlayingNameLabel was empty — archetype not loaded");
            againstName.ShouldNotBeNullOrEmpty("AgainstNameLabel was empty — archetype not loaded");
        }

        [Test]
        public void ReadJournalPage_SelectMatch_ShowsDetailCard()
        {
            FindFirstMatchRow().Click();
            FindReadJournalElement("SelectedMatchCard").ShouldNotBeNull();
        }

        [Test]
        public void ReadJournalPage_SelectMatch_ShowsIcons()
        {
            FindFirstMatchRow().Click();
            FindReadJournalElement("PlayingIcon").ShouldNotBeNull();
            FindReadJournalElement("AgainstIcon").ShouldNotBeNull();
        }

        [Test]
        public void ReadJournalPage_SelectMatch_ShowsNotesEditor()
        {
            FindFirstMatchRow().Click();
            FindReadJournalElement("SelectedMatchNotes").ShouldNotBeNull();
        }

        [Test]
        public void ReadJournalPage_SelectMatch_ShowsGame1TagsView()
        {
            FindFirstMatchRow().Click();
            // Game1TagsView is always visible; shows either tags or "No Tags for Game 1" empty view.
            FindReadJournalElement("Game1TagsView").ShouldNotBeNull();
        }

        [Test]
        public void ReadJournalPage_BO3Match_ShowsGame2And3TagViews()
        {
            // Last seeded match is BO3 Loss (Regidrago vs Miraidon, 3 games).
            // Game2TagsView and Game3TagsView are only visible for matches with Game2/Game3.
            FindLastMatchRow().Click();
            FindReadJournalElement("Game2TagsView").ShouldNotBeNull();
            FindReadJournalElement("Game3TagsView").ShouldNotBeNull();
        }
    }
}
