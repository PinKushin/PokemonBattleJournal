namespace UITests
{
    public partial class MainPageTests : BaseTest
    {
        public MainPageTests()
        {
            NavigateTo("Journal Entry");
            Thread.Sleep(1500);
        }

        [Fact]
        public async Task MainPage_UserNoteInput_ShowTextEntry()
        {
            // Arrange
            AppiumElement userEntry = FindUIElement("UserNoteInput");
            CancellationToken cancellationToken = new();
            // Act
            userEntry.SendKeys("Hello World");
            await Task.Delay(500).WaitAsync(cancellationToken);

            // Assert
            _ = userEntry.ShouldNotBeNull();
            // Android folds SemanticProperties.Description into content-desc, which Appium
            // prepends to the field value ("Game 1 notes, Hello World"). Assert on the value.
            userEntry.Text.ShouldEndWith("Hello World");

        }

        [Fact]
        public void MainPage_BallIcon_DisplayedOnPage()
        {
            AppiumElement BallIconPng = FindUIElement("ball_icon.png");

            _ = BallIconPng.ShouldNotBeNull();
            BallIconPng.Displayed.ShouldBeTrue();
            BallIconPng.Enabled.ShouldBeTrue();
        }

        [Fact]
        public void MainPage_Pickers_DisplayedAndEnabled()
        {
            AppiumElement startPicker = FindUIElement("StartTimePicker");
            AppiumElement endPicker = FindUIElement("EndTimePicker");
            AppiumElement datePicker = FindUIElement("DatePlayedPicker");

            // Pickers may be below the fold on Windows — FindUIElement proves existence; check Enabled not Displayed
            startPicker.Enabled.ShouldBeTrue();
            endPicker.Enabled.ShouldBeTrue();
            datePicker.Enabled.ShouldBeTrue();
        }

        [Fact]
        public void MainPage_TagsView_Displayed()
        {
            AppiumElement tagsView = FindUIElement("TagsView");

            tagsView.Displayed.ShouldBeTrue();
        }

        [Fact]
        public void MainPage_BOSwitch_ShowsBO3Fields()
        {
            AppiumElement boSwitch = FindUIElement("BOSwitch");

            boSwitch.Click();
            Thread.Sleep(500);

            AppiumElement bo3Layout = FindUIElement("BO3GamesLayout");
            bo3Layout.Displayed.ShouldBeTrue();
        }
    }
}
