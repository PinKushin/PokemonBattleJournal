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
            string toggledOn = BOSwitch.GetAttribute("checked");
            BOSwitch.Click();
            await Task.Delay(500).WaitAsync(cancellationToken); // Wait for the click to register 
            string toggledOff = BOSwitch.GetAttribute("checked");
            // Assert
            _ = BOSwitch.ShouldNotBeNull();
            BOSwitch.Displayed.ShouldBeTrue();
            BOSwitch.Enabled.ShouldBeTrue();
            toggledOn.ShouldBe("true"); // "1" for toggled on
            toggledOff.ShouldBe("false");// "0" for toggled off

        }
    }
}
