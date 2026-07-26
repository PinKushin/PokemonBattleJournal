namespace UITests
{
    public partial class MainPageTests : BaseTest
    {

        [Fact]
        public async Task MainPage_BOSwitch_DisplayedAndToggled()
        {
            // Arrange
            AppiumElement BOSwitch = FindUIElement("BOSwitch");
            CancellationToken cancellationToken = new();

            // Ensure starting state is off
            if (BOSwitch.GetAttribute("checked") == "true")
            {
                BOSwitch.Click();
                await Task.Delay(500).WaitAsync(cancellationToken);
            }

            // Act
            BOSwitch.Click();
            await Task.Delay(500).WaitAsync(cancellationToken);
            string toggledOn = BOSwitch.GetAttribute("checked");
            BOSwitch.Click();
            await Task.Delay(500).WaitAsync(cancellationToken);
            string toggledOff = BOSwitch.GetAttribute("checked");

            // Assert
            _ = BOSwitch.ShouldNotBeNull();
            BOSwitch.Displayed.ShouldBeTrue();
            BOSwitch.Enabled.ShouldBeTrue();
            toggledOn.ShouldBe("true");
            toggledOff.ShouldBe("false");
        }
    }
}
