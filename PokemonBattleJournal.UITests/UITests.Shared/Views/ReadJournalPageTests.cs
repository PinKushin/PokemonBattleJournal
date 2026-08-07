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
                ClickElement(FindFirstMatchRow());
        }

        // Same guard for the BO3 detail: skip click if Game2TagsView is already showing.
        private void EnsureBO3MatchDetailOpen()
        {
            if (!IsElementPresent("Game2TagsView"))
                ClickElement(FindFirstMatchRow());
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
        /// A best-of-one match must not show empty game 2 and 3 note boxes.
        /// </summary>
        /// <remarks>
        /// <para>The negative half of <see cref="ReadJournalPage_BO3Match_ShowsGame2And3Notes"/>.
        /// Without it, deleting <c>IsVisible="{Binding HasGame2}"</c> from the XAML leaves every
        /// other test green while every BO1 match grows two empty editors — verified by removing
        /// those bindings and watching only this test fail.</para>
        ///
        /// <para>Targets the LAST row rather than an index. The seeder writes four matches — BO1
        /// at −14d and −8d, BO3 at −2d and +1d — and the list sorts newest-first, so the oldest
        /// row is always a BO1. An index is not safe: MainPageTests sorts ahead of this fixture
        /// and saves matches stamped ~now, which land between the BO3s in a full-suite run and
        /// are absent in a single-fixture run. Same order-dependence that broke HasSeededMatches.</para>
        /// </remarks>
        [Test]
        public void ReadJournalPage_BO1Match_HidesGame2And3Notes()
        {
            ClickElement(FindLastMatchRow());

            // Prove WHICH match is selected, not merely that a panel opened. Without this the
            // test's real claim is unverified: "no game 2 editor" is also true of a panel that
            // never rendered, and would be true if the click landed on nothing. Confirmed to
            // discriminate — pointed at the first row it fails with "read 'Seed-BO3b-G1'".
            // Contains, not StartsWith: on Android an Editor's .Text is its
            // SemanticProperties.Description followed by the value, so this reads
            // "Notes for game 1 of the selected match, Seed-BO1-1" there and bare "Seed-BO1-1"
            // on Windows. The BO3 test gets away with ShouldEndWith for the same reason — the
            // value is always the tail. Anchoring to the start only works on Windows.
            string game1Note = FindReadJournalElement("SelectedMatchNotes").Text;
            game1Note.ShouldContain("Seed-BO1-", Case.Sensitive,
                $"the oldest row must be a seeded best-of-one match, but game 1's note read '{game1Note}' — " +
                "if this shows a Seed-BO3 note the click selected the wrong row and the assertions below prove nothing");

            // IsElementPresent uses a zero implicit wait. A missing element costs ~6.8s at the
            // ambient wait and ~0 here, and there are two of them. See feedback_uitest_timeouts.
            IsElementPresent("SelectedMatchNotes2")
                .ShouldBeFalse("a best-of-one match has no game 2, so its note editor must stay hidden");
            IsElementPresent("SelectedMatchNotes3")
                .ShouldBeFalse("a best-of-one match has no game 3, so its note editor must stay hidden");
        }

        /// <summary>Oldest match row — always a seeded BO1. See the BO1 test for why not an index.</summary>
        private AppiumElement FindLastMatchRow()
        {
            System.Collections.ObjectModel.ReadOnlyCollection<AppiumElement> rows = App is WindowsDriver
                ? App.FindElements(MobileBy.XPath("//*[contains(@AutomationId,'MatchRow_')]"))
                : App.FindElements(MobileBy.AndroidUIAutomator(
                    "new UiSelector().resourceIdMatches(\"com.PinKushin.PokemonBattleJournal:id/MatchRow_.*\")"));

            rows.Count.ShouldBeGreaterThan(1,
                "need at least two match rows to have a BO1 distinct from the newest BO3 — seed missing?");

            return rows[^1];
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

            // Assert the TEXT, not just that the editors exist. "Element is present" would pass
            // with both boxes empty, or with all three bound to SelectedNote — and a value that
            // is computed but never reaches the screen is exactly the bug this feature fixes.
            // The seeder writes distinct notes per game (Seed-BO3{a,b}-G{1,2,3}), so matching on
            // the suffix proves each property reached its OWN editor without pinning which of
            // the two seeded BO3 matches sorts first.
            string note2 = FindReadJournalElement("SelectedMatchNotes2").Text;
            string note3 = FindReadJournalElement("SelectedMatchNotes3").Text;

            note2.ShouldEndWith("-G2", Case.Sensitive,
                $"game 2's editor must show game 2's note, not another game's. Showed: '{note2}'");
            note3.ShouldEndWith("-G3", Case.Sensitive,
                $"game 3's editor must show game 3's note, not another game's. Showed: '{note3}'");
        }
    }
}
