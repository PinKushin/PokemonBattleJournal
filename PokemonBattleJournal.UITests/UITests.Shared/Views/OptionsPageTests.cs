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
            // Key on the RAW GATE, never on the indicator.
            //
            // The indicator deliberately lingers for MinimumVisibleDuration (500ms) after the
            // gate clears, so straight after any mutating test it is legitimately still on
            // screen. An earlier version of this teardown checked the indicator, read that
            // perfectly normal state as "stuck", and clicked the toggle — which turns
            // IsBusyMutating ON. With retries it could leave it on, and CI run on e8edb16 duly
            // failed nearly the whole fixture. The safety net caused the exact failure it was
            // added to prevent.
            //
            // Busy_Mutating is bound to IsBusyMutating, which is precisely what the toggle
            // flips, so it identifies a stuck toggle without false positives. Page loading
            // raises IsBusyArchetypeList and a different sentinel, so it cannot be confused
            // with one.
            bool stuck;
            try
            {
                stuck = IsElementPresent("Busy_Mutating");
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

                ClickElement("SaveArchetypeButton");

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

                ClickElement("SaveTagButton");

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
            ClickElement("SaveArchetypeButton");
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
            ClickElement("SaveTagButton");
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

        /// <summary>
        /// Largest movement treated as measurement noise rather than displacement. Layout
        /// rounding differs by a pixel between platforms; the inline indicator moved content by
        /// well over a hundred, so nothing about this threshold is delicate.
        /// </summary>
        private const int AllowedShiftPixels = 2;

        /// <summary>
        /// The spinner must not occupy layout space, so no element's position depends on
        /// whether it happens to be on screen.
        /// </summary>
        /// <remarks>
        /// This is the property that matters for the whole test suite, not just for looks. When
        /// the indicator was an inline sibling it pushed roughly 130px of OptionsPage down every
        /// time it appeared, which means every element below it had two possible positions
        /// depending on timing. Android's UiScrollable only scrolls down
        /// (docs/memory/feedback_uiscrollable_direction.md), so content displaced upward past the
        /// scroll position is simply unreachable — a lookup failure with no visible cause.
        ///
        /// SimulateLoadingButton is the probe because it sits directly below where the indicator
        /// used to be, so it moves first if anything does.
        ///
        /// BOTH reads are taken AFTER a click, which is load-bearing. Measuring before the first
        /// click compares two different scroll offsets: WinAppDriver scrolls an off-screen
        /// element into view in order to click it, so if an alphabetically earlier test left the
        /// page scrolled, the button starts above the viewport and the first click pulls it back
        /// down. That produced a 132px "shift" (y=-52 to y=80) in a full-suite run while the
        /// isolated run passed, because in isolation the page was already at the top. Clicking
        /// first pins the scroll offset, so what remains is layout.
        ///
        /// This also covers InputTransparent implicitly: the overlay is drawn over the whole
        /// page, so if it swallowed input the toggle-off click could never land and this test
        /// would fail inside ClickSimulateLoadingUntil rather than on the assertion.
        /// </remarks>
        [Test]
        public void OptionsPage_LoadingIndicator_DoesNotDisplacePageContent()
        {
            int during;
            try
            {
                ClickSimulateLoadingUntil("LoadingIndicatorHost", shouldBePresent: true);
                during = FindUIElement("SimulateLoadingButton").Location.Y;
            }
            finally
            {
                ClickSimulateLoadingUntil("LoadingIndicatorHost", shouldBePresent: false);
            }

            int after = FindUIElement("SimulateLoadingButton").Location.Y;

            int shift = Math.Abs(during - after);
            PerfLog($"Loading indicator displaced SimulateLoadingButton by {shift}px (y {during} with spinner -> {after} without)");

            shift.ShouldBeLessThanOrEqualTo(
                AllowedShiftPixels,
                $"the spinner must render as an overlay and take no layout space, but showing it " +
                $"moved SimulateLoadingButton {shift}px (y={during} with the spinner up, y={after} " +
                "without). Content that changes position depending on whether the spinner is up " +
                "is unreachable to Android's scroll-down-only element lookup.");
        }

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
                ClickElement("SimulateLoadingButton");

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

        // ---------------------------------------------------------------------------
        // Restore
        //
        // Presence only, for the same reason as the export tests above: the command opens the
        // operating system's file picker, which is outside the app's accessibility tree and
        // owns the foreground until something dismisses it. The restore itself is covered by
        // RestoreServiceIntegrationTests and OptionsPageRestoreIntegrationTests, neither of
        // which needs a UI. What only a UI test establishes is that the control exists, carries
        // its automation id, and is reachable at the bottom of a page that needs scrolling.
        // ---------------------------------------------------------------------------

        [Test]
        public void OptionsPage_RestoreSectionHeading_Visible()
        {
            FindUIElement("RestoreSectionHeading").ShouldNotBeNull();
        }

        [Test]
        public void OptionsPage_RestoreBackupButton_Visible()
        {
            FindUIElement("RestoreBackupButton").ShouldNotBeNull();
        }
    }
}
