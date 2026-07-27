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
        public async Task MainPage_UserNoteInput_ShowTextEntry()
        {
            AppiumElement userEntry = FindUIElement("UserNoteInput");
            CancellationToken cancellationToken = new();
            userEntry.SendKeys("Hello World");
            await Task.Delay(500).WaitAsync(cancellationToken);

            _ = userEntry.ShouldNotBeNull();
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
            Thread.Sleep(500);

            AppiumElement bo3Layout = FindUIElement("BO3GamesLayout");
            bo3Layout.ShouldNotBeNull();

            boSwitch.Click();
            Thread.Sleep(300);
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
            Thread.Sleep(1000); // allow BO3 content to animate in

            // Verify outer layout first so we know BO3 content is rendered
            FindUIElement("BO3GamesLayout").ShouldNotBeNull();

            // Game3Tab only appears with a split result — only check Game1Tab and Game2Tab.
            // Tab elements live inside a TabBar and aren't found via UiScrollable; use direct id lookup.
            if (App is WindowsDriver)
            {
                App.FindElement(MobileBy.AccessibilityId("Game1Tab")).ShouldNotBeNull();
                App.FindElement(MobileBy.AccessibilityId("Game2Tab")).ShouldNotBeNull();
            }
            else
            {
                App.FindElement(MobileBy.Id("com.PinKushin.PokemonBattleJournal:id/Game1Tab")).ShouldNotBeNull();
                App.FindElement(MobileBy.Id("com.PinKushin.PokemonBattleJournal:id/Game2Tab")).ShouldNotBeNull();
            }

            boSwitch.Click();
            Thread.Sleep(300);
            InvalidateCurrentPage();
        }

        [Fact]
        public async Task MainPage_SaveMatch_WithResult_Saves()
        {
            // Select "Win" from the result picker
            AppiumElement resultPicker = FindUIElement("PossibleResultsPicker");
            resultPicker.Click();
            Thread.Sleep(500);

            // On Android the MAUI Picker opens a native AlertDialog — find "Win" by text
            if (App is not WindowsDriver)
            {
                AppiumElement winOption = App.FindElement(
                    MobileBy.AndroidUIAutomator("new UiSelector().text(\"Win\")"));
                winOption.Click();
            }
            else
            {
                // Windows MAUI Picker popup items are found by Name (displayed text), not AutomationId
                AppiumElement winOption = App.FindElement(MobileBy.Name("Win"));
                winOption.Click();
            }

            await Task.Delay(300);

            AppiumElement saveButton = FindUIElement("SaveMatchButton");
            saveButton.Click();
            await Task.Delay(1000);

            // Button still present means save didn't crash
            AppiumElement saveButtonAfter = FindUIElement("SaveMatchButton");
            saveButtonAfter.ShouldNotBeNull();

            InvalidateCurrentPage();
        }
    }
}
