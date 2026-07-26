namespace UITests
{
    public partial class TrainerPageTests : BaseTest
    {
        [Fact]
        public void TrainerPage_StatsLabels_Displayed()
        {
            NavigateTo("Trainer's Profile");

            AppiumElement winRateLabel = FindUIElement("WinRateLabel");

            winRateLabel.Displayed.ShouldBeTrue();
        }
    }
}
