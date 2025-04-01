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
            await Task.Delay(500).WaitAsync(cancellationToken); // Wait for the click to register 
            var toggledOn = BOSwitch.GetAttribute("Toggle.ToggleState");
            BOSwitch.Click();
            await Task.Delay(500).WaitAsync(cancellationToken); // Wait for the click to register 
            var toggledOff = BOSwitch.GetAttribute("Toggle.ToggleState");
            // Assert
            BOSwitch.ShouldNotBeNull();
            BOSwitch.Displayed.ShouldBeTrue();
            BOSwitch.Enabled.ShouldBeTrue();
            toggledOn.ShouldBe("1"); // "1" for toggled on
            toggledOff.ShouldBe("0");// "0" for toggled off

        }
    }
}
