namespace UITests
{
    public partial class ReadJournalPageTests : BaseTest
    {
        [OneTimeSetUp]
        public void SetUp()
        {
            NavigateTo("Read Journal");
            // Sync on the match-history load completing before any element queries —
            // otherwise every UiAutomator call waits out the busy UI thread.
            WaitUntilBusyGone("Busy_MatchHistory");
        }

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

        /// <summary>
        /// Data-presence assertion: the journal actually lists seeded matches, not just an
        /// empty list control.
        /// </summary>
        /// <remarks>
        /// <para><b>Order(1) is load-bearing on Android.</b> NUnit otherwise runs this fixture
        /// alphabetically, which puts both BO3 tests ahead of this one, and both leave a match's
        /// detail panel open. MatchHistoryList is a virtualized CollectionView, so once its rows
        /// are pushed out of the viewport they leave the accessibility tree entirely and a bare
        /// resourceIdMatches finds nothing.</para>
        ///
        /// <para>That made a data-presence test depend on the height of a panel below it: adding
        /// the game 2/3 note editors (~400px) broke it, on a change that touched nothing about
        /// the match list. It passed in isolation and failed in the fixture — the signature of an
        /// order dependence.</para>
        ///
        /// <para>Two compensating fixes were tried and neither worked, so do not reach for them
        /// again. Scrolling back to the top does not re-realize the rows. Calling NavigateTo does
        /// nothing at all here — it no-ops when already on the page. Running first, on a freshly
        /// loaded page, is also what this test actually means: "the journal lists seeded matches
        /// when you open it".</para>
        /// </remarks>
        [Test]
        [Order(1)]
        public void ReadJournalPage_HasSeededMatches()
        {
            // Body was a verbatim copy of FindFirstMatchRow's. Two copies kept in step by hand,
            // which is how a fix to one silently misses the other.
            FindFirstMatchRow().ShouldNotBeNull("ReadJournal loaded with no match rows — seeded data missing");
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

        /// <summary>
        /// Game 2 and 3 notes must actually reach the screen.
        /// </summary>
        /// <remarks>
        /// The ViewModel has computed <c>SelectedNote2</c> and <c>SelectedNote3</c> since the
        /// page was written, and nothing was ever bound to them — the notes existed in the
        /// database, survived export and restore, and could not be read anywhere in the app
        /// (B-08). This is the test that would have caught it: the tag views next door were
        /// asserted, the notes beside them were not.
        /// </remarks>
        [Test]
        public void ReadJournalPage_BO3Match_ShowsGame2And3Notes()
        {
            EnsureBO3MatchDetailOpen();
            FindReadJournalElement("SelectedMatchNotes2").ShouldNotBeNull();
            FindReadJournalElement("SelectedMatchNotes3").ShouldNotBeNull();
        }
    }
}
