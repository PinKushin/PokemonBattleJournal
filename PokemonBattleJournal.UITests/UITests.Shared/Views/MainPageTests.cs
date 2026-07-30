namespace UITests
{
    public partial class MainPageTests : BaseTest
    {
        [OneTimeSetUp]
        public void SetUp() => NavigateTo("Journal Entry");

        [TearDown]
        public void AfterEach()
        {
            // Use 0ms timeout for all teardown finds — elements that don't exist
            // (e.g. Game1Tab when BO3 is off) fail instantly instead of exhausting
            // the full 3s+10s+10s Android lookup chain.
            App.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
            try
            {
                // Close any open Windows dropdowns first — open pickers block tab clicks and switch toggles.
                if (App is WindowsDriver)
                {
                    foreach (string id in new[] { "PossibleResultsPicker", "PossibleResultsPicker2", "PossibleResultsPicker3" })
                    {
                        try { App.FindElement(MobileBy.AccessibilityId(id)).SendKeys(OpenQA.Selenium.Keys.Escape); }
                        catch (OpenQA.Selenium.NoSuchElementException) { }
                    }
                }

                // Return to Game1 tab if a BO3 tab test left us on Game2/Game3.
                try { App.FindElement(MobileBy.AccessibilityId("Game1Tab")).Click(); }
                catch (OpenQA.Selenium.NoSuchElementException) { }

                // Turn off BO3 if a test left the switch on.
                try
                {
                    AppiumElement label = App.FindElement(MobileBy.AccessibilityId("BO3StatusLabel"));
                    if (label.Text == "Best of 3")
                        App.FindElement(MobileBy.AccessibilityId("BOSwitch")).Click();
                }
                catch (OpenQA.Selenium.NoSuchElementException) { }

                // Hide soft keyboard then clear note input.
                try
                {
                    if (App is AndroidDriver androidDriver)
                        androidDriver.HideKeyboard();
                    App.FindElement(MobileBy.AccessibilityId("UserNoteInput")).Clear();
                }
                catch (OpenQA.Selenium.NoSuchElementException) { }
            }
            finally
            {
                App.Manage().Timeouts().ImplicitWait = App is WindowsDriver
                    ? TimeSpan.FromSeconds(5)
                    : TimeSpan.FromSeconds(10);
            }
        }

        [OneTimeTearDown]
        public void TearDown() => InvalidateCurrentPage();

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
            AppiumElement userEntry = FindUIElement("UserNoteInput");
            userEntry.Click();
            userEntry.SendKeys("Hello World");
            userEntry.ShouldNotBeNull();
            // Re-fetch element so WinAppDriver returns current Value, not cached state.
            AppiumElement refetched = FindUIElement("UserNoteInput");
            refetched.Text.ShouldContain("Hello World");
        }

        [Test]
        public void MainPage_BallIcon_DisplayedOnPage()
        {
            AppiumElement BallIconPng = FindUIElement("ball_icon.png");
            BallIconPng.ShouldNotBeNull();
        }

        [Test]
        public void MainPage_Pickers_DisplayedAndEnabled()
        {
            AppiumElement startPicker = FindUIElement("StartTimePicker");
            AppiumElement endPicker = FindUIElement("EndTimePicker");
            AppiumElement datePicker = FindUIElement("DatePlayedPicker");

            startPicker.Enabled.ShouldBeTrue();
            endPicker.Enabled.ShouldBeTrue();
            datePicker.Enabled.ShouldBeTrue();
        }

        [Test]
        public void MainPage_TagsView_Displayed()
        {
            AppiumElement tagsView = FindUIElement("TagsView");
            tagsView.ShouldNotBeNull();
        }

        [Test]
        public void MainPage_BOSwitch_ShowsBO3Fields()
        {
            FindUIElement("BOSwitch").Click();
            FindUIElement("BO3GamesLayout").ShouldNotBeNull();
        }

        [Test]
        public void MainPage_PlayerArchetype_Displayed()
        {
            AppiumElement picker = FindUIElement("PlayerArchetype");
            picker.ShouldNotBeNull();
        }

        [Test]
        public void MainPage_RivalArchetype_Displayed()
        {
            AppiumElement picker = FindUIElement("RivalArchetype");
            picker.ShouldNotBeNull();
        }

        [Test]
        public void MainPage_BO3StatusLabel_Displayed()
        {
            AppiumElement label = FindUIElement("BO3StatusLabel");
            label.ShouldNotBeNull();
        }

        [Test]
        public void MainPage_ResultPicker_Displayed()
        {
            AppiumElement resultPicker = FindUIElement("PossibleResultsPicker");
            resultPicker.ShouldNotBeNull();
        }

        [Test]
        public void MainPage_FirstCheck_Displayed()
        {
            AppiumElement firstCheck = FindUIElement("FirstCheck");
            firstCheck.ShouldNotBeNull();
        }

        [Test]
        public void MainPage_SaveMatchButton_Displayed()
        {
            AppiumElement saveButton = FindUIElement("SaveMatchButton");
            saveButton.ShouldNotBeNull();
        }

        [Test]
        public void MainPage_BO3GameTabs_DisplayedWhenBO3Active()
        {
            FindUIElement("BOSwitch").Click();
            FindUIElement("BO3GamesLayout").ShouldNotBeNull();
            FindUIElement("Game1Tab").ShouldNotBeNull();
            FindUIElement("Game2Tab").ShouldNotBeNull();
        }

        [Test]
        public void MainPage_Game3Tab_ShowsWhenGame1IsTie()
        {
            FindUIElement("BOSwitch").Click();
            FindUIElement("BO3GamesLayout");

            AppiumElement picker1 = FindUIElement("PossibleResultsPicker");
            if (App is not WindowsDriver)
            {
                picker1.Click();
                App.FindElement(MobileBy.AndroidUIAutomator("new UiSelector().text(\"Tie\")")).Click();
            }
            else
                SelectWindowsPickerItem(picker1, "Tie");

            ClickTab(FindUIElement("Game2Tab"));
            // Sync on Game 2 panel visibility — TapGestureRecognizer fires async command.
            FindUIElement("FirstCheck2");

            AppiumElement picker2 = FindUIElement("PossibleResultsPicker2");
            if (App is not WindowsDriver)
            {
                picker2.Click();
                App.FindElement(MobileBy.AndroidUIAutomator("new UiSelector().text(\"Win\")")).Click();
            }
            else
                SelectWindowsPickerItem(picker2, "Win");

            FindUIElement("Game3Tab").ShouldNotBeNull();
        }

        [Test]
        public void MainPage_Game3Tab_ShowsGamePanel()
        {
            FindUIElement("BOSwitch").Click();
            FindUIElement("BO3GamesLayout");

            AppiumElement picker1 = FindUIElement("PossibleResultsPicker");
            if (App is not WindowsDriver)
            {
                picker1.Click();
                App.FindElement(MobileBy.AndroidUIAutomator("new UiSelector().text(\"Win\")")).Click();
            }
            else
                SelectWindowsPickerItem(picker1, "Win");

            ClickTab(FindUIElement("Game2Tab"));
            // Sync on Game 2 panel visibility — TapGestureRecognizer fires async command.
            FindUIElement("FirstCheck2");

            AppiumElement picker2 = FindUIElement("PossibleResultsPicker2");
            if (App is not WindowsDriver)
            {
                picker2.Click();
                App.FindElement(MobileBy.AndroidUIAutomator("new UiSelector().text(\"Loss\")")).Click();
            }
            else
                SelectWindowsPickerItem(picker2, "Loss");

            ClickTab(FindUIElement("Game3Tab"));
            FindUIElement("Match3Tags").ShouldNotBeNull();
            FindUIElement("UserNoteInput3").ShouldNotBeNull();
            FindUIElement("WentFirstLabel3").ShouldNotBeNull();
            FindUIElement("PossibleResultsPicker3").ShouldNotBeNull();
        }

        [Test]
        public void MainPage_SaveMatch_WithResult_Saves()
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
    }
}
