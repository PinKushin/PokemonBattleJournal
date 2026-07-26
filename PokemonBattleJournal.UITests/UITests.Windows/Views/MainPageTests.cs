namespace UITests
{
    public partial class MainPageTests : BaseTest
    {

        [Fact]
        public async Task MainPage_BOSwitch_DisplayedAndToggled()
        {
            // Arrange — BOSwitch is a pokeball image toggle, not a Switch control
            AppiumElement BOSwitch = FindUIElement("BOSwitch");
            CancellationToken cancellationToken = new();

            _ = BOSwitch.ShouldNotBeNull();
            BOSwitch.Displayed.ShouldBeTrue();

            // Toggle BO3 on — BO3GamesLayout (Game 2 tab) should appear
            BOSwitch.Click();
            await Task.Delay(500).WaitAsync(cancellationToken);
            AppiumElement bo3Layout = FindUIElement("BO3GamesLayout");
            bo3Layout.Displayed.ShouldBeTrue();

            // Toggle BO3 off — BO3GamesLayout should disappear
            BOSwitch.Click();
            await Task.Delay(500).WaitAsync(cancellationToken);
            var hidden = App.FindElements(MobileBy.AccessibilityId("BO3GamesLayout"));
            hidden.ShouldBeEmpty();
        }
    }
}
