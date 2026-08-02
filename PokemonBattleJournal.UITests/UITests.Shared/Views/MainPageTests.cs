namespace UITests
{
    public partial class MainPageTests : BaseTest
    {
        [OneTimeSetUp]
        public void SetUp()
        {
            NavigateTo("Journal Entry");
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
            if (!TryClickIfPresent("Game1Tab"))
            {
                // Game1Tab may not exist if BO3 was toggled off — log for CI diagnosis.
                TestContext.WriteLine("WARN: ResetGame1Tab — Game1Tab not found; next test may inherit dirty state.");
            }
        }

        // UiScrollable scrollForward() moves toward the end of the list; scrollToBeginning()
        // moves back to the top/left edge. On Windows we use keyboard navigation to return
        // the ScrollView to the top because the page itself does not expose a scroll helper.
        private void ScrollPageToTop()
        {
            if (App is AndroidDriver)
            {
                try
                {
                    App.FindElement(MobileBy.AndroidUIAutomator(
                        "new UiScrollable(new UiSelector().scrollable(true).instance(0)).scrollToBeginning(100)"));
                }
                catch { }
                return;
            }

            if (App is not WindowsDriver) return;

            try
            {
                App.FindElement(MobileBy.AccessibilityId("MainPageScrollView")).SendKeys(OpenQA.Selenium.Keys.Home);
            }
            catch { }
        }

        private void CloseWindowsPickers(params string[] ids)
        {
            if (App is not WindowsDriver) return;
            foreach (string id in ids)
            {
                try
                {
                    App.FindElement(MobileBy.AccessibilityId(id)).SendKeys(OpenQA.Selenium.Keys.Escape);
                }
                catch (OpenQA.Selenium.NoSuchElementException) { }
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
        private void EnsureBO3On()
        {
            AppiumElement label = FindUIElement("BO3StatusLabel");
            if (label.Text != "Best of 3")
                FindUIElement("BOSwitch").Click();
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

        [Test]
        public void MainPage_UserNoteInput_ShowTextEntry()
        {
            try
            {
                AppiumElement userEntry = FindUIElement("UserNoteInput");
                userEntry.Click();
                userEntry.SendKeys("Hello World");
                userEntry.ShouldNotBeNull();
                // Re-fetch element so WinAppDriver returns current Value, not cached state.
                FindUIElement("UserNoteInput").Text.ShouldContain("Hello World");
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
                    picker1.Click();
                    App.FindElement(MobileBy.AndroidUIAutomator("new UiSelector().text(\"Tie\")")).Click();
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
                    picker2.Click();
                    App.FindElement(MobileBy.AndroidUIAutomator("new UiSelector().text(\"Win\")")).Click();
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
                    picker1.Click();
                    App.FindElement(MobileBy.AndroidUIAutomator("new UiSelector().text(\"Win\")")).Click();
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
                    picker2.Click();
                    App.FindElement(MobileBy.AndroidUIAutomator("new UiSelector().text(\"Loss\")")).Click();
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
            }
            finally
            {
                CloseWindowsPickers("PossibleResultsPicker", "PossibleResultsPicker2");
                ResetGame1Tab();
                ResetBOSwitch();
            }
        }

        [Test]
        public void MainPage_SaveMatch_WithResult_Saves()
        {
            try
            {
                AppiumElement resultPicker = FindUIElement("PossibleResultsPicker");
                if (App is not WindowsDriver)
                {
                    resultPicker.Click();
                    App.FindElement(MobileBy.AndroidUIAutomator("new UiSelector().text(\"Win\")")).Click();
                }
                else
                    SelectWindowsPickerItem(resultPicker, "Win");

                FindUIElement("SaveMatchButton").Click();
                FindUIElement("SaveMatchButton").ShouldNotBeNull();
            }
            finally { CloseWindowsPickers("PossibleResultsPicker"); }
        }
    }
}
