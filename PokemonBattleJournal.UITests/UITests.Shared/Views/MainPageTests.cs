namespace UITests
{
    public partial class MainPageTests : BaseTest
    {
        [OneTimeSetUp]
        public void SetUp()
        {
            NavigateTo("Journal Entry");
            // Sync on archetypes + tags load (incl. possible Limitless fetch) before tests run.
            WaitUntilBusyGone("Busy_ArchetypeList", timeoutMs: 15000);
            ScrollPageToTop();
        }

        [OneTimeTearDown]
        public void TearDown() => InvalidateCurrentPage();

        // ---------------------------------------------------------------------------
        // Cleanup helpers — each test that mutates state calls the relevant helper(s)
        // in a finally block. TryClickIfPresent is used for best-effort cleanup.
        // ---------------------------------------------------------------------------

        private void ResetBOSwitch()
        {
            ScrollPageToTop();
            try
            {
                AppiumElement label = FindUIElement("BO3StatusLabel");
                if (label.Text == "Best of 3")
                    TryClickIfPresent("BOSwitch");
            }
            catch (OpenQA.Selenium.NoSuchElementException) { }
        }

        private void ResetGame1Tab()
        {
            ScrollPageToTop();
            // Click-verify-retry. A single click is not enough: Appium taps sometimes never
            // reach the handler on Android (proved via popup lifecycle log — same disease as
            // OpenArchetypePopup). If Game1Tab isn't actually selected, the whole Game 1 panel
            // (IsVisible={Binding IsGame1Selected}) stays hidden → ResultPicker/TagsView/
            // UserNoteInput/WentFirstLabel vanish for downstream tests → cascade of 17-20 s
            // STAGE3 timeouts. Verify the panel marker (PossibleResultsPicker) is present
            // after each click; retry up to 3 times; fail loudly if it never comes back.
            // Verify by the OTHER panels' content leaving the tree — NOT by Game 1 content
            // appearing. UiAutomator only exposes elements inside the visible viewport; the
            // Game 1 picker may be off-screen at the current scroll position even when the
            // panel switch succeeded, which made an "is Game 1 content present" check loop
            // forever (observed 2026-08-04, run 3). UserNoteInput2/3 are in the viewport
            // region we just interacted with, so their disappearance is a reliable signal.
            for (int i = 0; i < 3; i++)
            {
                FindUIElement("Game1Tab").Click();
                var deadline = DateTime.UtcNow.AddMilliseconds(2500);
                while (DateTime.UtcNow < deadline)
                {
                    if (!IsElementPresent("UserNoteInput2") && !IsElementPresent("UserNoteInput3"))
                    {
                        PerfLog($"ResetGame1Tab: Game 2/3 panels gone on attempt {i + 1}");
                        return;
                    }
                }
                PerfLog($"ResetGame1Tab: attempt {i + 1} — Game 2/3 panel content still visible, retrying click");
            }
            throw new OpenQA.Selenium.NoSuchElementException(
                "ResetGame1Tab: Game 2/3 panel content still present after 3 clicks on Game1Tab.");
        }

        // UiScrollable scrollForward() moves toward the end of the list; scrollToBeginning()
        // moves back to the top/left edge. On Windows we use keyboard navigation to return
        // the ScrollView to the top because the page itself does not expose a scroll helper.
        private void ScrollPageToTop()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            if (App is AndroidDriver)
            {
                try
                {
                    App.FindElement(MobileBy.AndroidUIAutomator(
                        "new UiScrollable(new UiSelector().scrollable(true).instance(0)).scrollToBeginning(100)"));
                }
                catch { }
                if (sw.ElapsedMilliseconds > 750) PerfLog($"ScrollPageToTop (Android): {sw.ElapsedMilliseconds}ms");
                return;
            }

            if (App is not WindowsDriver) return;

            try
            {
                App.FindElement(MobileBy.AccessibilityId("MainPageScrollView")).SendKeys(OpenQA.Selenium.Keys.Home);
            }
            catch { }
            if (sw.ElapsedMilliseconds > 750) PerfLog($"ScrollPageToTop (Windows): {sw.ElapsedMilliseconds}ms");
        }

        private void CloseWindowsPickers(params string[] ids)
        {
            if (App is not WindowsDriver) return;
            foreach (string id in ids)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    App.FindElement(MobileBy.AccessibilityId(id)).SendKeys(OpenQA.Selenium.Keys.Escape);
                    PerfLog($"CloseWindowsPickers('{id}'): escaped in {sw.ElapsedMilliseconds}ms");
                }
                catch (OpenQA.Selenium.NoSuchElementException)
                {
                    PerfLog($"CloseWindowsPickers('{id}'): absent, cost {sw.ElapsedMilliseconds}ms");
                }
            }
        }

        private void ClearUserNoteInput()
        {
            try
            {
                if (App is AndroidDriver androidDriver)
                    androidDriver.HideKeyboard();
                App.FindElement(MobileBy.AccessibilityId("UserNoteInput")).Clear();
            }
            catch (OpenQA.Selenium.NoSuchElementException) { }
        }

        // Ensures BO3 is ON regardless of current state (safe to call when BO3 may already be on).
        // Call ScrollPageToTop() before this in tests where a prior test may have scrolled down,
        // so BO3StatusLabel/BOSwitch (near page top) are in view for FindUIElement.
        // Click-verify-retry: BOSwitch is a Border + TapGestureRecognizer — the same control
        // shape whose taps Appium sometimes drops on Android (proved via popup lifecycle log).
        // WaitUntilText returns silently on timeout, so a missed click used to fall through
        // looking like "label on but tab missing". Each attempt: click, wait for the label to
        // confirm, then wait for Game2Tab (the IsVisible-bound tab — Game1Tab is always
        // visible and proves nothing) to enter the tree.
        private void EnsureBO3On()
        {
            AppiumElement label = FindUIElement("BO3StatusLabel");
            if (label.Text == "Best of 3" && IsElementPresent("Game2Tab"))
                return;

            for (int i = 0; i < 3; i++)
            {
                if (GetBO3LabelTextSafe() != "Best of 3")
                {
                    FindUIElement("BOSwitch").Click();
                    WaitUntilText("BO3StatusLabel", "Best of 3", timeoutMs: 3000);
                }

                if (GetBO3LabelTextSafe() != "Best of 3")
                {
                    PerfLog($"EnsureBO3On: attempt {i + 1} — label did not flip, re-clicking BOSwitch");
                    continue;
                }

                var tabSw = System.Diagnostics.Stopwatch.StartNew();
                var tabDeadline = DateTime.UtcNow.AddMilliseconds(5000);
                while (DateTime.UtcNow < tabDeadline)
                {
                    if (IsElementPresent("Game2Tab"))
                    {
                        PerfLog($"EnsureBO3On: Game2Tab appeared in {tabSw.ElapsedMilliseconds}ms (attempt {i + 1})");
                        return;
                    }
                }
                PerfLog($"EnsureBO3On: attempt {i + 1} — label on but Game2Tab not in tree after {tabSw.ElapsedMilliseconds}ms");
            }
            throw new OpenQA.Selenium.NoSuchElementException(
                "EnsureBO3On: Game2Tab never appeared after 3 BOSwitch attempts.");
        }

        private string GetBO3LabelTextSafe()
        {
            try { return GetElementText("BO3StatusLabel"); }
            catch (OpenQA.Selenium.NoSuchElementException) { return ""; }
        }

        // Dismiss the archetype popup reliably on both platforms. On Android, if the Cancel
        // button click misses (content-desc reset after popup animation, or button off-screen),
        // fall back to the Android BACK keyevent via adb. WaitUntilGone syncs on the popup
        // actually leaving the UIA tree so the next test starts on a clean main page.
        private void DismissArchetypePopup()
        {
            PerfLog("DismissArchetypePopup: try TryClickIfPresent(ArchetypePopupCancel)");
            bool cancelClicked = TryClickIfPresent("ArchetypePopupCancel");
            PerfLog($"DismissArchetypePopup: cancelClicked={cancelClicked}");

            App.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
            var pollSw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var deadline = DateTime.UtcNow.AddMilliseconds(1500);
                while (DateTime.UtcNow < deadline)
                {
                    if (App.FindElements(MobileBy.AccessibilityId("ArchetypeSearchBar")).Count == 0)
                    {
                        PerfLog($"DismissArchetypePopup: OK — searchBar gone via Cancel in {pollSw.ElapsedMilliseconds}ms");
                        return;
                    }
                }
                PerfLog($"DismissArchetypePopup: searchBar STILL PRESENT after Cancel + {pollSw.ElapsedMilliseconds}ms poll → try BACK");
            }
            finally { App.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5); }

            // Cancel didn't dismiss (or the search bar's content-desc is stale). Use BACK.
            SendAndroidBack();

            App.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
            var backPollSw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var deadline = DateTime.UtcNow.AddMilliseconds(1500);
                while (DateTime.UtcNow < deadline)
                {
                    if (App.FindElements(MobileBy.AccessibilityId("ArchetypeSearchBar")).Count == 0)
                    {
                        PerfLog($"DismissArchetypePopup: OK — searchBar gone via BACK in {backPollSw.ElapsedMilliseconds}ms");
                        return;
                    }
                }
                PerfLog($"DismissArchetypePopup: FAIL — searchBar STILL PRESENT after BACK + {backPollSw.ElapsedMilliseconds}ms poll");
                DumpVisibleElements("DismissArchetypePopup FAIL");
            }
            finally { App.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5); }
        }

        // MAUI Picker on Android opens a native AlertDialog. The option list items are
        // AppCompat Buttons with text — findable via UiSelector().text(...).
        // Owns the picker click AND the item selection with retry: popup-lifecycle logging
        // proved Appium clicks sometimes never reach the tap handler (page still settling
        // from a scroll fling), so a single click + text lookup is inherently flaky.
        // Each attempt: click picker, wait up to 2.5s for the dialog item text, click it.
        private void SelectAndroidPickerItem(AppiumElement picker, string itemText, int attempts = 3)
        {
            PerfLog($"SelectAndroidPickerItem('{itemText}'): begin");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < attempts; i++)
            {
                picker.Click();
                App.Manage().Timeouts().ImplicitWait = TimeSpan.FromMilliseconds(2500);
                try
                {
                    var el = App.FindElement(MobileBy.AndroidUIAutomator(
                        $"new UiSelector().text(\"{itemText}\")"));
                    PerfLog($"SelectAndroidPickerItem('{itemText}'): dialog open on attempt {i + 1} ({sw.ElapsedMilliseconds}ms) — clicking item");
                    el.Click();
                    return;
                }
                catch (OpenQA.Selenium.NoSuchElementException)
                {
                    PerfLog($"SelectAndroidPickerItem('{itemText}'): attempt {i + 1} — dialog item not found, retrying picker click");
                }
                finally { App.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10); }
            }
            PerfLog($"SelectAndroidPickerItem('{itemText}'): FAIL after {attempts} attempts ({sw.ElapsedMilliseconds}ms)");
            DumpVisibleElements($"SelectAndroidPickerItem('{itemText}') FAIL");
            throw new OpenQA.Selenium.NoSuchElementException(
                $"Picker dialog item '{itemText}' not reachable after {attempts} picker clicks.");
        }

        // ---------------------------------------------------------------------------
        // Tests
        // ---------------------------------------------------------------------------

        [Test]
        public void MainPage_AfterFirstBoot_TrainerNameSet()
        {
            AppiumElement welcomeLabel = FindUIElement("WelcomeMsg");
            welcomeLabel.ShouldNotBeNull();
            welcomeLabel.Text.ShouldContain("UITestTrainer");
        }

        // [Order(1)]: on CI's software-rendered emulator, the host GL translator's
        // ColorBuffer pool drains over the course of this popup-heavy fixture
        // ("Failed to find ColorBuffer: NNN" in the emulator log), and whichever tests
        // run LAST lose their elements from the UIA tree — this test and WentFirstLabel
        // failed as the alphabetical tail three runs straight while 23 siblings passed.
        // Ordered tests run before unordered ones in NUnit, so the two victims now run
        // while the pool is fresh. Both are state-safe to run first: this one clears its
        // note in finally, WentFirstLabel is display-only.
        [Test, Order(1)]
        public void MainPage_UserNoteInput_ShowTextEntry()
        {
            // Type-verify-retry: SendKeys drops keystrokes on slow CI runners (Windows CI
            // observed "Hello " — the "World" never arrived) and Android CI throws
            // StaleElementReferenceException when the Editor re-renders between find and
            // type. Re-find the element every attempt (heals staleness) and verify the
            // full text landed before asserting.
            const string expected = "Hello World";
            try
            {
                string actual = "";
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        AppiumElement userEntry = FindUIElement("UserNoteInput");
                        // Windows-only focus click. On Android, logcat proved this click
                        // BACKGROUNDS THE APP (Window.Deactivated -> stopped 100ms after the
                        // click command, run 31001057545): the Editor sits at the bottom of
                        // the screen and the tap lands in the gesture-navigation home zone.
                        // UiAutomator2's SendKeys sets text directly without needing focus.
                        if (App is WindowsDriver)
                            userEntry.Click();
                        userEntry.Clear();
                        userEntry.SendKeys(expected);
                        // Android: the soft keyboard opened by SendKeys covers the lower
                        // half of the screen; elements under it (this Editor included, and
                        // WentFirstLabel in the next test) drop out of the visible UIA tree
                        // until it's dismissed — the source of the stale-then-unfindable
                        // tail-pair failures on CI. No-op on Windows.
                        DismissKeyboard();
                        // Poll instead of a single immediate read — SendKeys can return before
                        // the bound Text property finishes propagating back to the native
                        // control, which previously raced a same-instant Text read into "".
                        WaitUntilText("UserNoteInput", expected, timeoutMs: 3000);
                        // Re-fetch so the driver returns current value, not cached state.
                        actual = FindUIElement("UserNoteInput").Text;
                        if (actual.Contains(expected, StringComparison.OrdinalIgnoreCase))
                            break;
                        PerfLog($"UserNoteInput attempt {i + 1}: typed text incomplete ('{actual}') — retyping");
                    }
                    catch (OpenQA.Selenium.StaleElementReferenceException)
                    {
                        PerfLog($"UserNoteInput attempt {i + 1}: stale element — re-finding");
                    }
                }
                actual.ShouldContain(expected);
            }
            finally { ClearUserNoteInput(); }
        }

        [Test]
        public void MainPage_BallIcon_DisplayedOnPage()
        {
            FindUIElement("ball_icon.png").ShouldNotBeNull();
        }

        [Test]
        public void MainPage_Pickers_DisplayedAndEnabled()
        {
            FindUIElement("StartTimePicker").Enabled.ShouldBeTrue();
            FindUIElement("EndTimePicker").Enabled.ShouldBeTrue();
            FindUIElement("DatePlayedPicker").Enabled.ShouldBeTrue();
        }

        [Test]
        public void MainPage_TagsView_Displayed()
        {
            FindUIElement("TagsView").ShouldNotBeNull();
        }

        [Test]
        public void MainPage_BOSwitch_ShowsBO3Fields()
        {
            try
            {
                EnsureBO3On();
                FindUIElement("BO3GamesLayout").ShouldNotBeNull();
            }
            finally { ResetBOSwitch(); }
        }

        [Test]
        public void MainPage_PlayerArchetype_Displayed()
        {
            FindUIElement("PlayerArchetype").ShouldNotBeNull();
        }

        [Test]
        public void MainPage_RivalArchetype_Displayed()
        {
            FindUIElement("RivalArchetype").ShouldNotBeNull();
        }

        [Test]
        public void MainPage_BO3StatusLabel_Displayed()
        {
            FindUIElement("BO3StatusLabel").ShouldNotBeNull();
        }

        [Test]
        public void MainPage_ResultPicker_Displayed()
        {
            FindUIElement("PossibleResultsPicker").ShouldNotBeNull();
        }

        [Test]
        public void MainPage_FirstCheck_Displayed()
        {
            FindUIElement("FirstCheck").ShouldNotBeNull();
        }

        [Test]
        public void MainPage_SaveMatchButton_Displayed()
        {
            FindUIElement("SaveMatchButton").ShouldNotBeNull();
        }

        [Test]
        public void MainPage_BO3GameTabs_DisplayedWhenBO3Active()
        {
            try
            {
                EnsureBO3On();
                FindUIElement("BO3GamesLayout").ShouldNotBeNull();
                FindUIElement("Game1Tab").ShouldNotBeNull();
                FindUIElement("Game2Tab").ShouldNotBeNull();
            }
            finally { ResetBOSwitch(); }
        }

        [Test]
        public void MainPage_Game3Tab_ShowsWhenGame1IsTie()
        {
            try
            {
                ScrollPageToTop(); // prior tests may leave page scrolled; BO3 controls near top
                EnsureBO3On();
                FindUIElement("BO3GamesLayout");

                AppiumElement picker1 = FindUIElement("PossibleResultsPicker");
                if (App is not WindowsDriver)
                {
                    SelectAndroidPickerItem(picker1, "Tie");
                    ClickTab(FindUIElement("Game2Tab"));
                }
                else
                {
                    SelectWindowsPickerItem(picker1, "Tie");
                    ClickTab(FindUIElement("Game2Tab"));
                }

                // Wait for Game 2 panel to be active
                FindUIElement("UserNoteInput2");

                FindUIElement("FirstCheck2");

                AppiumElement picker2 = FindUIElement("PossibleResultsPicker2");
                if (App is not WindowsDriver)
                {
                    SelectAndroidPickerItem(picker2, "Win");
                }
                else
                {
                    SelectWindowsPickerItem(picker2, "Win");
                }

                FindUIElement("Game3Tab").ShouldNotBeNull();
            }
            finally
            {
                CloseWindowsPickers("PossibleResultsPicker", "PossibleResultsPicker2");
                ResetGame1Tab();
                ResetBOSwitch();
            }
        }

        [Test]
        public void MainPage_Game3Tab_ShowsGamePanel()
        {
            try
            {
                ScrollPageToTop(); // MainPage_FirstCheck_Displayed (prior alphabetically) scrolls down via stage-3
                EnsureBO3On();
                FindUIElement("BO3GamesLayout");

                AppiumElement picker1 = FindUIElement("PossibleResultsPicker");
                if (App is not WindowsDriver)
                {
                    SelectAndroidPickerItem(picker1, "Win");
                    ClickTab(FindUIElement("Game2Tab"));
                }
                else
                {
                    SelectWindowsPickerItem(picker1, "Win");
                    ClickTab(FindUIElement("Game2Tab"));
                }

                // Wait for Game 2 panel to be active
                FindUIElement("UserNoteInput2");

                FindUIElement("FirstCheck2");

                AppiumElement picker2 = FindUIElement("PossibleResultsPicker2");
                if (App is not WindowsDriver)
                {
                    SelectAndroidPickerItem(picker2, "Loss");
                    ClickTab(FindUIElement("Game3Tab"));
                }
                else
                {
                    SelectWindowsPickerItem(picker2, "Loss");
                    ClickTab(FindUIElement("Game3Tab"));
                }
                FindUIElement("Match3Tags").ShouldNotBeNull();
                FindUIElement("UserNoteInput3").ShouldNotBeNull();
                FindUIElement("WentFirstLabel3").ShouldNotBeNull();
                FindUIElement("PossibleResultsPicker3").ShouldNotBeNull();
                FindUIElement("FirstCheck3").ShouldNotBeNull();
            }
            finally
            {
                CloseWindowsPickers("PossibleResultsPicker", "PossibleResultsPicker2");
                ResetGame1Tab();
                ResetBOSwitch();
            }
        }

        [Test]
        public void MainPage_PageTitle_Displayed()
        {
            FindUIElement("PageTitle").ShouldNotBeNull();
        }

        [Test]
        public void MainPage_SectionHeadings_Displayed()
        {
            FindUIElement("SelectDecksHeading").ShouldNotBeNull();
            FindUIElement("MatchFormatHeading").ShouldNotBeNull();
            FindUIElement("StartTimeHeading").ShouldNotBeNull();
            FindUIElement("EndTimeHeading").ShouldNotBeNull();
            FindUIElement("DatePlayedHeading").ShouldNotBeNull();
        }

        // [Order(2)]: see MainPage_UserNoteInput_ShowTextEntry — second ColorBuffer victim.
        [Test, Order(2)]
        public void MainPage_WentFirstLabel_Displayed()
        {
            FindUIElement("WentFirstLabel").ShouldNotBeNull();
        }

        [Test]
        public void MainPage_SaveMatch_WithArchetypes_ShowsSavedText()
        {
            try
            {
                ScrollPageToTop();

                // Select player archetype — WaitUntilGone syncs on popup dismissal before opening rival.
                OpenArchetypePopup("PlayerArchetype");
                FindUIElement("ArchetypeItem_Other").Click();
                WaitUntilGone("ArchetypeItem_Other");

                // Select rival archetype
                OpenArchetypePopup("RivalArchetype");
                FindUIElement("ArchetypeItem_Other").Click();
                WaitUntilGone("ArchetypeItem_Other");

                // Select result
                AppiumElement resultPicker = FindUIElement("PossibleResultsPicker");
                if (App is not WindowsDriver)
                {
                    SelectAndroidPickerItem(resultPicker, "Win");
                }
                else
                    SelectWindowsPickerItem(resultPicker, "Win");

                FindUIElement("SaveMatchButton").Click();
                // Sync on the DB write settling before polling for the "Saved" text —
                // avoids racing the save on slower local/CI runs.
                WaitUntilBusyGone("Busy_Mutating");

                // SavedFileDisplay binding updates to "Saved: Match at …" on success.
                // SemanticProperties.Description is NOT set on the button so .Text reflects the bound value.
                var wait = new OpenQA.Selenium.Support.UI.WebDriverWait(App, TimeSpan.FromSeconds(5));
                string savedText = wait.Until(_ =>
                {
                    string text = FindUIElement("SaveMatchButton").Text;
                    return text.StartsWith("Saved") ? text : null;
                });
                savedText.ShouldStartWith("Saved");
            }
            finally { CloseWindowsPickers("PossibleResultsPicker"); }
        }

        [Test]
        public void MainPage_SaveMatch_WithResult_Saves()
        {
            try
            {
                AppiumElement resultPicker = FindUIElement("PossibleResultsPicker");
                if (App is not WindowsDriver)
                {
                    SelectAndroidPickerItem(resultPicker, "Win");
                }
                else
                    SelectWindowsPickerItem(resultPicker, "Win");

                FindUIElement("SaveMatchButton").Click();
                WaitUntilBusyGone("Busy_Mutating");
                FindUIElement("SaveMatchButton").ShouldNotBeNull();
            }
            finally { CloseWindowsPickers("PossibleResultsPicker"); }
        }

        [Test]
        public void MainPage_PlayerArchetype_DualIconDeck_ShowsBothIcons()
        {
            ScrollPageToTop();
            OpenArchetypePopup("PlayerArchetype");
            try
            {
                FindUIElement("ArchetypeItem_Dragapult ex / Dusknoir").Click();
                WaitUntilGone("ArchetypeItem_Dragapult ex / Dusknoir");
            }
            finally { DismissArchetypePopup(); }

            // Second icon should now be visible in the PlayerArchetype picker
            FindUIElement("PlayerArchetypeIcon2").ShouldNotBeNull();

            // Rival archetype still shows single-icon (Other/substitute) — second icon not visible
            App.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
            try
            {
                App.FindElements(MobileBy.AccessibilityId("RivalArchetypeIcon2")).Count.ShouldBe(0,
                    "Rival second icon should be hidden when a single-icon archetype is selected");
            }
            finally
            {
                App.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);
                // Reset PlayerArchetype back to Other so match-save tests stay clean
                ScrollPageToTop();
                OpenArchetypePopup("PlayerArchetype");
                try { FindUIElement("ArchetypeItem_Other").Click(); }
                finally { DismissArchetypePopup(); }
            }
        }

        [Test]
        public void MainPage_PlayerArchetype_Cancel_DismissesPopup()
        {
            ScrollPageToTop();
            OpenArchetypePopup("PlayerArchetype"); // opens and confirms popup content visible
            DismissPopupPlatform();
            WaitUntilGone("ArchetypeSearchBar", timeoutMs: 3000);
        }

        [Test]
        public void MainPage_RivalArchetype_Cancel_DismissesPopup()
        {
            ScrollPageToTop();
            WaitUntilGone("ArchetypeSearchBar"); // ensure no leftover popup from prior test
            OpenArchetypePopup("RivalArchetype"); // opens and confirms popup content visible
            DismissPopupPlatform();
            WaitUntilGone("ArchetypeSearchBar", timeoutMs: 3000);
        }

        // Windows: the ComboBoxPopup Cancel Button is the primary dismiss control and is
        // reachable via UIA. Android: CommunityToolkit Popup dismisses on outside-tap and
        // via BACK; the Cancel button often isn't in the UIA tree (MAUI popup Dialog window
        // vs main app scrollable). Use the platform's natural dismiss gesture.
        private void DismissPopupPlatform()
        {
            if (App is WindowsDriver)
                FindUIElement("ArchetypePopupCancel").Click();
            else
                SendAndroidBack();
        }

        // Opens the archetype popup with click-retry. In-app popup lifecycle logging proved
        // (2026-08-04, UITests.PopupLog.txt) that Appium's .Click() on the ComboBoxControl
        // sometimes never reaches the TapGestureRecognizer on Android — OnTapped simply
        // doesn't fire, most likely because the click lands while the page is still settling
        // from ScrollPageToTop's UiScrollable fling. The element is found (resource-id
        // resolves) but the tap misses. Retry the click until popup content actually appears.
        private void OpenArchetypePopup(string comboBoxId, int attempts = 3)
        {
            for (int i = 0; i < attempts; i++)
            {
                FindUIElement(comboBoxId).Click();

                // Poll for popup content (search bar) with a short per-attempt deadline.
                var deadline = DateTime.UtcNow.AddMilliseconds(2500);
                while (DateTime.UtcNow < deadline)
                {
                    if (IsElementPresent("ArchetypeSearchBar"))
                    {
                        PerfLog($"OpenArchetypePopup({comboBoxId}): popup open on attempt {i + 1}");
                        return;
                    }
                }
                PerfLog($"OpenArchetypePopup({comboBoxId}): attempt {i + 1} — popup did not appear, retrying click");
            }
            throw new OpenQA.Selenium.NoSuchElementException(
                $"Archetype popup did not open after {attempts} clicks on '{comboBoxId}'.");
        }

        [Test]
        public void MainPage_ArchetypePicker_Search_FiltersResults()
        {
            ScrollPageToTop();
            OpenArchetypePopup("PlayerArchetype");
            try
            {
                // Search bar appears inside the popup
                AppiumElement searchBar = FindUIElement("ArchetypeSearchBar");
                searchBar.ShouldNotBeNull();

                // "Other" is seeded — should survive a matching query
                searchBar.SendKeys("Other");
                FindUIElement("ArchetypeItem_Other").ShouldNotBeNull();

                // Clear and type a non-matching query — Other should disappear
                searchBar.Clear();
                searchBar.SendKeys("zzznomatch");
                App.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
                try
                {
                    bool found = App.FindElements(MobileBy.AccessibilityId("ArchetypeItem_Other")).Count > 0;
                    found.ShouldBeFalse("ArchetypeItem_Other should be filtered out by 'zzznomatch' query");
                }
                finally { App.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5); }
            }
            finally
            {
                // Always close popup so a failure here doesn't cascade to the next test class.
                DismissArchetypePopup();
            }
        }
    }
}
