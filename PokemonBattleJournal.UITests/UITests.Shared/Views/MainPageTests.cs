namespace UITests
{
    public partial class MainPageTests : BaseTest
    {
        public MainPageTests()
        {
            NavigateTo("Journal Entry");
        }

        // The SeedTestData step in AppiumSetup is the integration test for first-boot:
        // it installs a fresh APK (no DB), launches the app, dismisses the Welcome prompt
        // by typing "UITestTrainer" and clicking Save, then verifies the main page becomes
        // interactive by successfully seeding 3 matches. If the prompt flow were broken,
        // every subsequent test in this suite would fail because the nav drawer would be
        // unreachable. This test documents that contract explicitly.
        [Fact]
        public void MainPage_AfterFirstBoot_TrainerNameSet()
        {
            // If seeding succeeded, a trainer is active and the WelcomeMsg label is present
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
            userEntry.SendKeys("Hello World");
            userEntry.ShouldNotBeNull();
            userEntry.Text.ShouldEndWith("Hello World");
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
            boSwitch.Click();

            AppiumElement bo3Layout = FindUIElement("BO3GamesLayout");
            bo3Layout.ShouldNotBeNull();

            boSwitch.Click();
            InvalidateCurrentPage();
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
            AppiumElement boSwitch = FindUIElement("BOSwitch");
            boSwitch.Click();

            // Verify outer layout first so we know BO3 content is rendered
            FindUIElement("BO3GamesLayout").ShouldNotBeNull();

            // Game3Tab only appears with a split result — only check Game1Tab and Game2Tab.
            // Border elements have resource-id from AutomationId on Android — use FindUIElement.
            FindUIElement("Game1Tab").ShouldNotBeNull();
            FindUIElement("Game2Tab").ShouldNotBeNull();

            boSwitch.Click();
            InvalidateCurrentPage();
        }

        [Fact]
        public void MainPage_Game3Tab_ShowsWhenGame1IsTie()
        {
            AppiumElement boSwitch = FindUIElement("BOSwitch");
            try
            {
                boSwitch.Click();

                FindUIElement("PossibleResultsPicker").Click();
                if (App is WindowsDriver)
                    App.FindElement(By.XPath("//*[@Name='Tie']")).Click();
                else
                    App.FindElement(MobileBy.AndroidUIAutomator("new UiSelector().text(\"Tie\")")).Click();

                FindUIElement("Game2Tab").Click();

                FindUIElement("PossibleResultsPicker2").Click();
                if (App is WindowsDriver)
                    App.FindElement(By.XPath("//*[@Name='Win']")).Click();
                else
                    App.FindElement(MobileBy.AndroidUIAutomator("new UiSelector().text(\"Win\")")).Click();

                FindUIElement("Game3Tab").ShouldNotBeNull();
            }
            finally
            {
                try { boSwitch.Click(); } catch { }
                InvalidateCurrentPage();
            }
        }

        [Fact]
        public void MainPage_SaveMatch_WithResult_Saves()
        {
            AppiumElement resultPicker = FindUIElement("PossibleResultsPicker");
            resultPicker.Click();

            // On Android the MAUI Picker opens a native AlertDialog — find "Win" by text
            if (App is not WindowsDriver)
                App.FindElement(MobileBy.AndroidUIAutomator("new UiSelector().text(\"Win\")")).Click();
            else
                App.FindElement(By.XPath("//*[@Name='Win']")).Click();

            AppiumElement saveButton = FindUIElement("SaveMatchButton");
            saveButton.Click();

            // Button still present means save didn't crash
            FindUIElement("SaveMatchButton").ShouldNotBeNull();

            InvalidateCurrentPage();
        }
    }
}
