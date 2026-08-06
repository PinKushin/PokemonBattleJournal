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

        /// <summary>
        /// Safety net for a stuck loading indicator, which poisons every later test in the
        /// fixture rather than failing one.
        /// </summary>
        /// <remarks>
        /// This is a deliberate exception to "no blanket TearDown" (see
        /// docs/memory/feedback_uitest_cleanup_pattern.md). The usual objection is cost, and it
        /// does not apply: <c>IsElementPresent</c> runs with a zero implicit wait, so when
        /// nothing is stuck — the normal case — this is a single near-instant query.
        ///
        /// The failure it guards is severe and non-local. If the simulated-loading toggle is
        /// left on, the spinner animates at 60fps, the Android UI thread never goes idle, and
        /// every following UiAutomator call burns its full ~20s waitForIdle budget. On CI run
        /// 31063011084 that turned one stuck toggle into eight failed tests, none of which had
        /// anything to do with loading.
        /// </remarks>
        [TearDown]
        public void ClearStuckLoadingIndicator()
        {
            bool stuck;
            try
            {
                stuck = IsElementPresent("LoadingIndicatorHost");
            }
            catch (InvalidOperationException)
            {
                // WinAppDriver phantom-element race: an element caught mid-removal comes back
                // with an empty id and IsElementPresent throws rather than reporting false.
                // WaitUntilRemoved already swallows this exact case. Mid-removal means it is
                // on its way out, which is precisely the state we do not need to fix.
                return;
            }

            if (!stuck)
            {
                return;
            }

            PerfLog("TearDown: loading indicator still showing — clearing it so it cannot starve the UI thread");
            try
            {
                ClickSimulateLoadingUntil("LoadingIndicatorHost", shouldBePresent: false);
            }
            catch (OpenQA.Selenium.NoSuchElementException ex)
            {
                // Say so loudly rather than letting the next test fail on an unrelated lookup.
                PerfLog($"TearDown: FAILED to clear the loading indicator — later tests in this fixture will likely fail. {ex.Message}");
            }
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
            ClickSimulateLoadingUntil("LoadingIndicatorHost", shouldBePresent: true);
            try
            {
                IsElementPresent("LoadingIndicatorHost")
                    .ShouldBeTrue("the spinner must appear while a busy gate is held open");
            }
            finally
            {
                ClickSimulateLoadingUntil("LoadingIndicatorHost", shouldBePresent: false);
            }

            IsElementPresent("LoadingIndicatorHost")
                .ShouldBeFalse("the spinner must disappear once the work is done");
        }

        /// <summary>
        /// Clicks the simulated-loading toggle until the indicator reaches the wanted state.
        /// </summary>
        /// <remarks>
        /// A plain click is not safe here, and getting it wrong poisons the whole fixture rather
        /// than failing one test.
        ///
        /// Android clicks silently miss MAUI gesture handlers (see
        /// docs/memory/feedback_android_flaky_tap_retry.md). If the click that turns the toggle
        /// OFF misses, the busy gate stays open, the spinner keeps animating at 60fps, and the
        /// UI thread never goes idle again — so every following UiAutomator call burns its full
        /// ~20s waitForIdle budget and every later test in the fixture fails on lookup. That is
        /// exactly what happened on CI (run 31063011084): this test passed, then all eight tests
        /// alphabetically after it failed with STAGE3_FAIL after ~22s, the signature from
        /// docs/memory/project_readjournal_android_slow.md.
        ///
        /// So the toggle is verified against the indicator's actual visibility and retried,
        /// rather than assumed to have landed.
        /// </remarks>
        private void ClickSimulateLoadingUntil(string indicatorId, bool shouldBePresent, int attempts = 3)
        {
            for (int attempt = 1; attempt <= attempts; attempt++)
            {
                FindUIElement("SimulateLoadingButton").Click();

                bool reached = shouldBePresent
                    ? IsElementPresent(indicatorId)
                    : WaitUntilRemoved(indicatorId);

                if (reached)
                {
                    if (attempt > 1)
                        PerfLog($"SimulateLoading toggle to present={shouldBePresent} took {attempt} clicks");
                    return;
                }

                PerfLog($"SimulateLoading toggle attempt {attempt} did not reach present={shouldBePresent}, retrying");
            }

            throw new OpenQA.Selenium.NoSuchElementException(
                $"SimulateLoadingButton never drove '{indicatorId}' to present={shouldBePresent} " +
                $"after {attempts} clicks. Leaving the gate open starves the Android UI thread " +
                "and fails every later test in this fixture.");
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
