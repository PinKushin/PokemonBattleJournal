namespace UITests
{
    public partial class MainPageTests : BaseTest
    {
        [Fact]
        public void MainPage_BOSwitch_DisplayedAndToggled()
        {
            NavigateTo("Journal Entry");
            AppiumElement boSwitch = FindUIElement("BOSwitch");
            AppiumElement statusLabel = FindUIElement("BO3StatusLabel");

            // Ensure starting state is BO1
            if (statusLabel.Text == "Best of 3")
                boSwitch.Click();

            try
            {
                boSwitch.Click();
                string toggledOn = FindUIElement("BO3StatusLabel").Text;

                boSwitch.Click();
                string toggledOff = FindUIElement("BO3StatusLabel").Text;

                boSwitch.ShouldNotBeNull();
                boSwitch.Displayed.ShouldBeTrue();
                boSwitch.Enabled.ShouldBeTrue();
                toggledOn.ShouldBe("Best of 3");
                toggledOff.ShouldBe("Best of 1");
            }
            finally
            {
                // Always leave BO3 off so subsequent tests see clean BO1 state
                AppiumElement label = FindUIElement("BO3StatusLabel");
                if (label.Text == "Best of 3")
                    FindUIElement("BOSwitch").Click();
            }
        }
    }
}
