namespace UITests
{
    public partial class MainPageTests : BaseTest
    {
        // No constructor navigation — WinAppDriver/Appium starts the app on MainPage already.
        // Tests that need a specific Shell page navigate explicitly.

        // The SeedTestData step in AppiumSetup is the integration test for first-boot:
        // it installs a fresh APK (no DB), launches the app, dismisses the Welcome prompt
        // by typing "UITestTrainer" and clicking Save, then verifies the main page becomes
        // interactive by successfully seeding 3 matches. If the prompt flow were broken,
        // every subsequent test in this suite would fail because the nav drawer would be
        // unreachable. This test documents that contract explicitly.
        [Fact]
        public void MainPage_AfterFirstBoot_TrainerNameSet()
        {
            AppiumElement welcomeLabel = FindUIElement("WelcomeMsg");
            welcomeLabel.ShouldNotBeNull();
            welcomeLabel.Text.ShouldContain("UITestTrainer");
        }

        [Fact]
        public void MainPage_UserNoteInput_ShowTextEntry()
        {
            // Editor (EditText) gets resource-id from AutomationId on Android — use FindUIElement.
            // SemanticProperties.Description on EditText maps to hint, not content-desc.
            AppiumElement userEntry = FindUIElement("UserNoteInput");
            try
            {
                userEntry.SendKeys("Hello World");
                userEntry.ShouldNotBeNull();
                userEntry.Text.ShouldEndWith("Hello World");
            }
            finally
            {
                // Clear so later tests don't see stale note text
                try { userEntry.Clear(); } catch (OpenQA.Selenium.NoSuchElementException) { }
            }
        }

        [Fact]
        public void MainPage_BallIcon_DisplayedOnPage()
        {
            AppiumElement BallIconPng = FindUIElement("ball_icon.png");
            BallIconPng.ShouldNotBeNull();
        }

        [Fact]
        public void MainPage_Pickers_DisplayedAndEnabled()
        {
            AppiumElement startPicker = FindUIElement("StartTimePicker");
            AppiumElement endPicker = FindUIElement("EndTimePicker");
            AppiumElement datePicker = FindUIElement("DatePlayedPicker");

            startPicker.Enabled.ShouldBeTrue();
            endPicker.Enabled.ShouldBeTrue();
            datePicker.Enabled.ShouldBeTrue();
        }

        [Fact]
        public void MainPage_TagsView_Displayed()
        {
            AppiumElement tagsView = FindUIElement("TagsView");
            tagsView.ShouldNotBeNull();
        }

        [Fact]
        public void MainPage_BOSwitch_ShowsBO3Fields()
        {
            AppiumElement boSwitch = FindUIElement("BOSwitch");
            try
            {
                boSwitch.Click();
                FindUIElement("BO3GamesLayout").ShouldNotBeNull();
            }
            finally
            {
                // Disable BO3 regardless of pass/fail so next test starts clean
                try { FindUIElement("BOSwitch").Click(); } catch (OpenQA.Selenium.NoSuchElementException) { }
            }
        }

        [Fact]
        public void MainPage_PlayerArchetype_Displayed()
        {
            AppiumElement picker = FindUIElement("PlayerArchetype");
            picker.ShouldNotBeNull();
        }

        [Fact]
        public void MainPage_RivalArchetype_Displayed()
        {
            AppiumElement picker = FindUIElement("RivalArchetype");
            picker.ShouldNotBeNull();
        }

        [Fact]
        public void MainPage_BO3StatusLabel_Displayed()
        {
            AppiumElement label = FindUIElement("BO3StatusLabel");
            label.ShouldNotBeNull();
        }

        [Fact]
        public void MainPage_ResultPicker_Displayed()
        {
            AppiumElement resultPicker = FindUIElement("PossibleResultsPicker");
            resultPicker.ShouldNotBeNull();
        }

        [Fact]
        public void MainPage_FirstCheck_Displayed()
        {
            AppiumElement firstCheck = FindUIElement("FirstCheck");
            firstCheck.ShouldNotBeNull();
        }

        [Fact]
        public void MainPage_SaveMatchButton_Displayed()
        {
            AppiumElement saveButton = FindUIElement("SaveMatchButton");
            saveButton.ShouldNotBeNull();
        }

        [Fact]
        public void MainPage_BO3GameTabs_DisplayedWhenBO3Active()
        {
            try
            {
                FindUIElement("BOSwitch").Click();

                // Verify outer layout first so we know BO3 content is rendered
                FindUIElement("BO3GamesLayout").ShouldNotBeNull();

                // Game3Tab only appears with a split result — only check Game1Tab and Game2Tab.
                FindUIElement("Game1Tab").ShouldNotBeNull();
                FindUIElement("Game2Tab").ShouldNotBeNull();
            }
            finally
            {
                try { FindUIElement("BOSwitch").Click(); } catch (OpenQA.Selenium.NoSuchElementException) { }
            }
        }

        [Fact]
        public void MainPage_Game3Tab_ShowsWhenGame1IsTie()
        {
            try
            {
                FindUIElement("BOSwitch").Click();
                // Wait for BO3 layout to fully render before interacting with pickers
                FindUIElement("BO3GamesLayout");

                FindUIElement("PossibleResultsPicker").Click();
                if (App is WindowsDriver)
                    SelectWindowsPickerItem("Tie");
                else
                    App.FindElement(MobileBy.AndroidUIAutomator("new UiSelector().text(\"Tie\")")).Click();

                FindUIElement("Game2Tab").Click();

                FindUIElement("PossibleResultsPicker2").Click();
                if (App is WindowsDriver)
                    SelectWindowsPickerItem("Win");
                else
                    App.FindElement(MobileBy.AndroidUIAutomator("new UiSelector().text(\"Win\")")).Click();

                FindUIElement("Game3Tab").ShouldNotBeNull();
            }
            finally
            {
                // Dismiss any open picker dropdown, return to Game1Tab, disable BO3
                try
                {
                    if (App is WindowsDriver)
                        App.FindElement(MobileBy.AccessibilityId("PossibleResultsPicker")).SendKeys(OpenQA.Selenium.Keys.Escape);
                }
                catch (OpenQA.Selenium.NoSuchElementException) { }
                try
                {
                    if (App is WindowsDriver)
                        App.FindElement(MobileBy.AccessibilityId("PossibleResultsPicker2")).SendKeys(OpenQA.Selenium.Keys.Escape);
                }
                catch (OpenQA.Selenium.NoSuchElementException) { }
                try { FindUIElement("Game1Tab").Click(); } catch (OpenQA.Selenium.NoSuchElementException) { }
                try { FindUIElement("BOSwitch").Click(); } catch (OpenQA.Selenium.NoSuchElementException) { }
            }
        }

        [Fact]
        public void MainPage_SaveMatch_WithResult_Saves()
        {
            try
            {
                FindUIElement("PossibleResultsPicker").Click();

                // On Android the MAUI Picker opens a native AlertDialog — find "Win" by text.
                // On Windows, MAUI Picker may open its dropdown as a child popup window on CI —
                // SelectWindowsPickerItem checks all window handles before falling back to main window.
                if (App is not WindowsDriver)
                    App.FindElement(MobileBy.AndroidUIAutomator("new UiSelector().text(\"Win\")")).Click();
                else
                    SelectWindowsPickerItem("Win");
            }
            catch (OpenQA.Selenium.NoSuchElementException)
            {
                // Close picker before re-throwing so subsequent tests aren't left with an open popup
                try
                {
                    if (App is WindowsDriver)
                        App.FindElement(MobileBy.AccessibilityId("PossibleResultsPicker")).SendKeys(OpenQA.Selenium.Keys.Escape);
                }
                catch (OpenQA.Selenium.NoSuchElementException) { }
                throw;
            }

            FindUIElement("SaveMatchButton").Click();

            // Button still present means save didn't crash
            FindUIElement("SaveMatchButton").ShouldNotBeNull();
        }
    }
}
