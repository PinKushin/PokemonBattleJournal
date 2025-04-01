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
            userEntry.ShouldNotBeNull();
            userEntry.Text.ShouldBe("Hello World");

        }

        [Fact]
        public void MainPage_BallIcon_DisplayedOnPage()
        {
            // Arrange
            AppiumElement BallIconPng = FindUIElement("ball_icon.png");
            // Act
            // Assert

            BallIconPng.ShouldNotBeNull();
            BallIconPng.Displayed.ShouldBeTrue();
            BallIconPng.Enabled.ShouldBeTrue();
        }
    }
}
