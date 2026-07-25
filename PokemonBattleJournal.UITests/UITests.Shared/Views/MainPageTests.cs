namespace UITests
{
    public partial class MainPageTests : BaseTest
    {
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
            userEntry.Text.ShouldBe("Hello World");

        }

        [Fact]
        public void MainPage_BallIcon_DisplayedOnPage()
        {
            // Arrange
            AppiumElement BallIconPng = FindUIElement("ball_icon.png");
            // Act
            // Assert

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

            startPicker.Displayed.ShouldBeTrue();
            startPicker.Enabled.ShouldBeTrue();
            endPicker.Displayed.ShouldBeTrue();
            endPicker.Enabled.ShouldBeTrue();
            datePicker.Displayed.ShouldBeTrue();
            datePicker.Enabled.ShouldBeTrue();
        }

        [Fact]
        public void MainPage_TagsView_Displayed()
        {
            AppiumElement tagsView = FindUIElement("TagsView");

            tagsView.Displayed.ShouldBeTrue();
        }
    }
}
