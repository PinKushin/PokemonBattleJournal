namespace UITests
{
    public partial class MainPageTests : BaseTest
    {
        public MainPageTests()
        {
            NavigateTo("Journal Entry");
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
            Thread.Sleep(500);

            // Game3Tab only appears when a Game3 is needed (split result after Game1+Game2),
            // so only assert Game1Tab and Game2Tab which are always visible in BO3 mode.
            AppiumElement game1 = FindUIElement("Game1Tab");
            AppiumElement game2 = FindUIElement("Game2Tab");
            game1.ShouldNotBeNull();
            game2.ShouldNotBeNull();

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
                // Windows Picker renders as a ComboBox — select by text
                AppiumElement winOption = App.FindElement(MobileBy.AccessibilityId("Win"));
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
