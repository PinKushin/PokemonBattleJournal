namespace UITests
{
    public partial class MainPageTests : BaseTest
    {

        [Fact]
        public async Task MainPage_BOSwitch_DisplayedAndToggled()
        {
            // Arrange
            AppiumElement BOSwitch = FindUIElement("BOSwitch");
            AppiumElement statusLabel = FindUIElement("BO3StatusLabel");
            CancellationToken cancellationToken = new();

            // Ensure starting state is off
            if (statusLabel.Text == "Best of 3")
            {
                BOSwitch.Click();
                await Task.Delay(500).WaitAsync(cancellationToken);
            }

            // Act
            BOSwitch.Click();
            await Task.Delay(500).WaitAsync(cancellationToken);
            string toggledOn = statusLabel.Text;
            BOSwitch.Click();
            await Task.Delay(500).WaitAsync(cancellationToken);
            string toggledOff = statusLabel.Text;

            // Assert
            _ = BOSwitch.ShouldNotBeNull();
            BOSwitch.Displayed.ShouldBeTrue();
            BOSwitch.Enabled.ShouldBeTrue();
            toggledOn.ShouldBe("Best of 3");
            toggledOff.ShouldBe("Best of 1");
        }
    }
}
