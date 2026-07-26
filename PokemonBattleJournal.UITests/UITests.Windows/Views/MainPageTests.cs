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

            // Act
            BOSwitch.Click();
            await Task.Delay(500).WaitAsync(cancellationToken);
            string toggledOn = BOSwitch.GetAttribute("Toggle.ToggleState");
            BOSwitch.Click();
            await Task.Delay(500).WaitAsync(cancellationToken);
            string toggledOff = BOSwitch.GetAttribute("Toggle.ToggleState");

            // Assert
            _ = BOSwitch.ShouldNotBeNull();
            BOSwitch.Displayed.ShouldBeTrue();
            BOSwitch.Enabled.ShouldBeTrue();
            toggledOn.ShouldBe("1");
            toggledOff.ShouldBe("0");
        }
    }
}
