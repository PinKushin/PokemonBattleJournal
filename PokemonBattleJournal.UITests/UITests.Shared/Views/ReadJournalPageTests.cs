namespace UITests
{
    public partial class ReadJournalPageTests : BaseTest
    {
        [OneTimeSetUp]
        public void SetUp() => NavigateTo("Read Journal");

        private AppiumElement FindReadJournalElement(string id) => FindUIElement(id);

        // Returns the first match row — the BO3 Loss is seeded with DatePlayed=UtcNow+1d so it
        // always sorts first (newest-first) regardless of BO1 matches added by other tests mid-run.
        private AppiumElement FindFirstMatchRow() =>
            App is WindowsDriver
                ? App.FindElements(MobileBy.XPath("//*[contains(@AutomationId,'MatchRow_')]"))
                    .FirstOrDefault()
                    ?? throw new Exception("No match rows found — seeded data missing")
                : App.FindElement(MobileBy.AndroidUIAutomator(
                    "new UiSelector().resourceIdMatches(\"com.PinKushin.PokemonBattleJournal:id/MatchRow_.*\")"));

        // Opens the detail panel for the first match row without toggling.
        // NUnit runs tests alphabetically; BO3Match opens the row first, then SelectMatch_* tests
        // would re-click and collapse it. This helper skips the click when detail is already visible.
        // IsElementPresent uses resource-id on Android (not AccessibilityId/content-desc) so the
        // check is correct across platforms.
        private void EnsureMatchDetailOpen()
        {
            if (!IsElementPresent("PlayingNameLabel"))
                FindFirstMatchRow().Click();
        }

        // Same guard for the BO3 detail: skip click if Game2TagsView is already showing.
        private void EnsureBO3MatchDetailOpen()
        {
            if (!IsElementPresent("Game2TagsView"))
                FindFirstMatchRow().Click();
        }

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
            EnsureMatchDetailOpen();
            FindReadJournalElement("PlayingNameLabel").ShouldNotBeNull();
        }

        [Test]
        public void ReadJournalPage_SelectMatch_ShowsArchetypeNames()
        {
            EnsureMatchDetailOpen();

            string playingName = FindReadJournalElement("PlayingNameLabel").Text;
            string againstName = FindReadJournalElement("AgainstNameLabel").Text;

            playingName.ShouldNotBeNullOrEmpty("PlayingNameLabel was empty — archetype not loaded");
            againstName.ShouldNotBeNullOrEmpty("AgainstNameLabel was empty — archetype not loaded");
        }

        [Test]
        public void ReadJournalPage_SelectMatch_ShowsDetailCard()
        {
            EnsureMatchDetailOpen();
            FindReadJournalElement("SelectedMatchCard").ShouldNotBeNull();
        }

        [Test]
        public void ReadJournalPage_SelectMatch_ShowsIcons()
        {
            EnsureMatchDetailOpen();
            FindReadJournalElement("PlayingIcon").ShouldNotBeNull();
            FindReadJournalElement("AgainstIcon").ShouldNotBeNull();
        }

        [Test]
        public void ReadJournalPage_SelectMatch_ShowsNotesEditor()
        {
            EnsureMatchDetailOpen();
            FindReadJournalElement("SelectedMatchNotes").ShouldNotBeNull();
        }

        [Test]
        public void ReadJournalPage_SelectMatch_ShowsGame1TagsView()
        {
            EnsureMatchDetailOpen();
            FindReadJournalElement("Game1TagsView").ShouldNotBeNull();
        }

        [Test]
        public void ReadJournalPage_BO3Match_ShowsGame2And3TagViews()
        {
            // BO3 Loss seeded at UtcNow+1d — always row 0 (newest-first sort).
            EnsureBO3MatchDetailOpen();
            FindReadJournalElement("Game2TagsView").ShouldNotBeNull();
            FindReadJournalElement("Game3TagsView").ShouldNotBeNull();
        }
    }
}
