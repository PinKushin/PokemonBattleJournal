namespace UITests
{
    public partial class TrainerPageTests : BaseTest
    {
        [Fact]
        public void TrainerPage_StatsLabels_Displayed()
        {
            NavigateTo("Trainer's Profile");

            AppiumElement welcomeLabel = FindUIElement("TrainerWelcomeLabel");
            AppiumElement winRateLabel = FindUIElement("WinRateLabel");

            welcomeLabel.Displayed.ShouldBeTrue();
            winRateLabel.Displayed.ShouldBeTrue();
        }
    }
}
