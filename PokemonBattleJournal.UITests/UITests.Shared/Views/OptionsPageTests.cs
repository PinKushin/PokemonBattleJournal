namespace UITests
{
    public partial class OptionsPageTests : BaseTest
    {
        [OneTimeSetUp]
        public void SetUp()
        {
            NavigateTo("Options");
            // Sync on trainers + archetypes + tags load before any element queries.
            WaitUntilBusyGone("Busy_ArchetypeList");
        }

        // ---------------------------------------------------------------------------
        // Cleanup helpers — only called by tests that create data
        // ---------------------------------------------------------------------------

        // Both use the SCROLLING click. TryClickIfPresent does a plain lookup, and UiAutomator
        // only exposes on-screen elements — so once the list grew past a screenful the newly
        // created row sat below the fold, the lookup missed, TryClickIfPresent returned false,
        // and the row was never deleted. Self-worsening: every failed cleanup made the list
        // longer and the next cleanup more likely to fail. It stayed invisible because the
        // Android DB wipe was also broken (see project_android_seeder_persistent_db), so the
        // leftovers looked like ordinary persistence rather than failed cleanup.
        // OptionsPage_DeleteArchetype_RemovesFromList always used ScrollIntoViewAndClick and
        // always worked — that contrast is what identified this.
        private void DeleteCreatedArchetype(string name) => DeleteCreatedRow($"DeleteArchetype_{name}");

        private void DeleteCreatedTag(string name) => DeleteCreatedRow($"DeleteTag_{name}");

        private void DeleteCreatedRow(string automationId)
        {
            try
            {
                ScrollIntoViewAndClick(automationId);
                if (!WaitUntilRemoved(automationId))
                    PerfLog($"Cleanup: '{automationId}' still present after delete click — next run starts with stale data");
            }
            catch (OpenQA.Selenium.NoSuchElementException)
            {
                // Genuinely absent: the test failed before creating the row. Not an error, but
                // say so — a silent miss here is indistinguishable from a failed delete.
                PerfLog($"Cleanup: '{automationId}' not found — nothing to delete");
            }
        }

        // ---------------------------------------------------------------------------
        // Tests
        // ---------------------------------------------------------------------------

        [Test]
        public void OptionsPage_Loads_PageVisible()
        {
            FindUIElement("SaveTrainerNameButton").ShouldNotBeNull();
        }

        [Test]
        public void OptionsPage_ArchetypeNameInput_AcceptsText()
        {
            AppiumElement input = FindUIElement("ArchetypeNameInput");
            input.Clear();
            input.SendKeys("TestDeck");
            input.Text.ShouldEndWith("TestDeck");
            input.Clear();
        }

        [Test]
        public void OptionsPage_ArchetypeIconPicker_Displayed()
        {
            FindUIElement("ArchetypeIconPicker").ShouldNotBeNull();
        }

        [Test]
        public void OptionsPage_SaveArchetypeButton_Displayed()
        {
            FindUIElement("SaveArchetypeButton").ShouldNotBeNull();
        }

        [Test]
        public void OptionsPage_SaveArchetype_WithName_ClearsInput()
        {
            string deckName = $"UITestDeck-{DateTime.Now:HHmmss}";
            try
            {
                AppiumElement input = FindUIElement("ArchetypeNameInput");
                input.Clear();
                input.SendKeys(deckName);

                FindUIElement("SaveArchetypeButton").Click();

                // Poll for new row — proves async save completed before checking input.
                FindUIElement($"DeleteArchetype_{deckName}").ShouldNotBeNull();

                FindUIElement("ArchetypeNameInput").Text.ShouldNotContain(deckName);
            }
            finally { DeleteCreatedArchetype(deckName); }
        }

        [Test]
        public void OptionsPage_ArchetypeList_ShowsSeededItems()
        {
            FindUIElement("DeleteArchetype_Other").ShouldNotBeNull();
        }

        [Test]
        public void OptionsPage_ArchetypeList_ShowsDebugSeededArchetypes()
        {
            // Limitless archetypes are seeded on startup. Names differ by platform (live vs offline).
            // Check for multiple archetypes (Other is always 1; Limitless adds more).
            int count = App is WindowsDriver
                ? App.FindElements(MobileBy.XPath("//*[contains(@AutomationId,'DeleteArchetype_')]")).Count
                : App.FindElements(MobileBy.AndroidUIAutomator(
                    "new UiSelector().resourceIdMatches(\"com.PinKushin.PokemonBattleJournal:id/DeleteArchetype_.*\")")).Count;
            (count > 1).ShouldBeTrue($"Only {count} archetype row(s) found — Limitless seeding may have failed");
        }

        [Test]
        public void OptionsPage_TagList_ShowsDebugSeededTags()
        {
            // Debug seed inserts "Lucky" and "Early Start" for UITestTrainer.
            FindUIElement("DeleteTag_Lucky").ShouldNotBeNull();
            FindUIElement("DeleteTag_Early Start").ShouldNotBeNull();
        }

        [Test]
        public void OptionsPage_TagInput_Displayed()
        {
            FindUIElement("TagInput").ShouldNotBeNull();
        }

        [Test]
        public void OptionsPage_SaveTag_WithName_Saves()
        {
            string tagName = $"UITestTag-{DateTime.Now:HHmmss}";
            try
            {
                AppiumElement tagInput = FindUIElement("TagInput");
                tagInput.Clear();
                tagInput.SendKeys(tagName);

                FindUIElement("SaveTagButton").Click();

                // Poll for new row — proves async save completed.
                FindUIElement($"DeleteTag_{tagName}").ShouldNotBeNull();
            }
            finally { DeleteCreatedTag(tagName); }
        }

        [Test]
        public void OptionsPage_TrainerSwitchPicker_Displayed()
        {
            FindUIElement("TrainerSwitchPicker").ShouldNotBeNull();
        }

        [Test]
        public void OptionsPage_SaveAllButton_Displayed()
        {
            FindUIElement("SaveAllButton").ShouldNotBeNull();
        }

        [Test]
        public void OptionsPage_TrainerNameInput_Displayed()
        {
            FindUIElement("TrainerNameInput").ShouldNotBeNull();
        }

        [Test]
        public void OptionsPage_SectionHeadings_Displayed()
        {
            FindUIElement("TrainerSectionHeading").ShouldNotBeNull();
            FindUIElement("ArchetypeSectionHeading").ShouldNotBeNull();
            FindUIElement("TagSectionHeading").ShouldNotBeNull();
            FindUIElement("SwitchTrainerHeading").ShouldNotBeNull();
        }

        [Test]
        public void OptionsPage_ArchetypeList_Container_Displayed()
        {
            FindUIElement("ArchetypeList").ShouldNotBeNull();
        }

        [Test]
        public void OptionsPage_TagList_Container_Displayed()
        {
            FindUIElement("TagList").ShouldNotBeNull();
        }

        [Test]
        public void OptionsPage_ArchetypeItem_Other_Displayed()
        {
            // ArchetypeItem_{Name} is the row container in the archetype list.
            FindUIElement("ArchetypeItem_Other").ShouldNotBeNull();
        }

        [Test]
        public void OptionsPage_TagItem_Lucky_Displayed()
        {
            // TagItem_{Name} is the row container in the tag list.
            FindUIElement("TagItem_Lucky").ShouldNotBeNull();
        }

        [Test]
        public void OptionsPage_DeleteTrainer_Displayed()
        {
            FindUIElement("DeleteTrainer").ShouldNotBeNull();
        }

        [Test]
        public void OptionsPage_DeleteArchetype_RemovesFromList()
        {
            string deckName = $"DelTestDeck-{DateTime.Now:HHmmss}";

            AppiumElement input = FindUIElement("ArchetypeNameInput");
            input.Clear();
            input.SendKeys(deckName);
            FindUIElement("SaveArchetypeButton").Click();
            // Sync on the save + AllArchetypes rebind completing before checking the row —
            // otherwise the CollectionView rebind races the presence check.
            WaitUntilBusyGone("Busy_Mutating");

            FindUIElement($"DeleteArchetype_{deckName}").ShouldNotBeNull();

            ScrollIntoViewAndClick($"DeleteArchetype_{deckName}");
            // Sync on the delete + rebind completing — same race, opposite direction.
            WaitUntilBusyGone("Busy_Mutating");

            // Verify gone — polled, phantom-element tolerant as a second line of defense
            // (WinAppDriver can still return a stale node for a frame after the gate clears).
            WaitUntilRemoved($"DeleteArchetype_{deckName}")
                .ShouldBeTrue("Archetype row should be removed after delete");
        }

        [Test]
        public void OptionsPage_DeleteTag_RemovesFromList()
        {
            string tagName = $"DelTestTag-{DateTime.Now:HHmmss}";

            AppiumElement tagInput = FindUIElement("TagInput");
            tagInput.Clear();
            tagInput.SendKeys(tagName);
            FindUIElement("SaveTagButton").Click();
            // Sync on the save + AllTags rebind completing before checking the row.
            WaitUntilBusyGone("Busy_Mutating");

            FindUIElement($"DeleteTag_{tagName}").ShouldNotBeNull();

            ScrollIntoViewAndClick($"DeleteTag_{tagName}");
            // Sync on the delete + rebind completing.
            WaitUntilBusyGone("Busy_Mutating");

            // Verify gone — polled, phantom-element tolerant as a second line of defense.
            WaitUntilRemoved($"DeleteTag_{tagName}")
                .ShouldBeTrue("Tag row should be removed after delete");
        }

        // ---------------------------------------------------------------------------
        // Export
        //
        // Presence and accessibility only — these deliberately do NOT click. Both export
        // commands end in FileSaver, which raises the operating system's own save dialog.
        // That dialog is not part of the app's accessibility tree, Appium cannot dismiss it
        // reliably, and a native modal left open owns the foreground for every test that
        // follows. The serialization behind the buttons is covered by ExportServiceTests and
        // ExportServiceIntegrationTests, which need no UI at all; what only a UI test can
        // establish is that the controls exist, carry their automation ids, and are reachable
        // on a page long enough to need scrolling.
        // ---------------------------------------------------------------------------

        [Test]
        public void OptionsPage_ExportSectionHeading_Visible()
        {
            FindUIElement("ExportSectionHeading").ShouldNotBeNull();
        }

        // ---------------------------------------------------------------------------
        // Loading indicator
        //
        // Real operations here finish in tens of milliseconds, so a test that clicked Save and
        // looked for the spinner would be racing the database and would usually lose. The
        // DEBUG-only toggle holds the busy gate open indefinitely instead, which makes this
        // deterministic and doubles as the way to eyeball the animation while developing.
        // ---------------------------------------------------------------------------

        [Test]
        public void OptionsPage_LoadingIndicator_ShownWhileBusyAndHiddenAfter()
        {
            FindUIElement("SimulateLoadingButton").Click();
            try
            {
                IsElementPresent("LoadingIndicatorHost")
                    .ShouldBeTrue("the spinner must appear while a busy gate is held open");
            }
            finally
            {
                // Leaving the gate open would strand every later test behind a
                // WaitUntilBusyGone("Busy_Mutating") that never completes.
                FindUIElement("SimulateLoadingButton").Click();
            }

            WaitUntilRemoved("LoadingIndicatorHost")
                .ShouldBeTrue("the spinner must disappear once the work is done");
        }

        [Test]
        public void OptionsPage_ExportTrainerHillButton_Visible()
        {
            FindUIElement("ExportTrainerHillButton").ShouldNotBeNull();
        }

        [Test]
        public void OptionsPage_ExportBackupButton_Visible()
        {
            FindUIElement("ExportBackupButton").ShouldNotBeNull();
        }
    }
}
