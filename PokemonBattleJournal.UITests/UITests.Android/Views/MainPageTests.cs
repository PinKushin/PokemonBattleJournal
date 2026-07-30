namespace UITests
{
    public partial class MainPageTests : BaseTest
    {
        [Test]
        public void MainPage_BOSwitch_DisplayedAndToggled()
        {
            AppiumElement boSwitch = FindUIElement("BOSwitch");
            AppiumElement statusLabel = FindUIElement("BO3StatusLabel");

            // Ensure starting state is BO1
            if (statusLabel.Text == "Best of 3")
                boSwitch.Click();

            boSwitch.Click();
            // Sync on label change — no Task.Delay
            string toggledOn = FindUIElement("BO3StatusLabel").Text;

            boSwitch.Click();
            string toggledOff = FindUIElement("BO3StatusLabel").Text;

            boSwitch.ShouldNotBeNull();
            boSwitch.Displayed.ShouldBeTrue();
            boSwitch.Enabled.ShouldBeTrue();
            toggledOn.ShouldBe("Best of 3");
            toggledOff.ShouldBe("Best of 1");
        }
    }
}
