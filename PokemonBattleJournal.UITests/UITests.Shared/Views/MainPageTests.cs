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
            userEntry.Text.ShouldBe("Hello World");

        }

        [Fact]
        public void MainPage_BallIcon_DisplayedOnPage()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            AppiumElement BallIconPng = FindUIElement("ball_icon.png");
            sw.Stop();

            _ = BallIconPng.ShouldNotBeNull();
            BallIconPng.Displayed.ShouldBeTrue();
            BallIconPng.Enabled.ShouldBeTrue();
            sw.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5));
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
