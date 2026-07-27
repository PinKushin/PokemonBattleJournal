namespace UITests
{
    public partial class TrainerPageTests : BaseTest
    {
        [Fact]
        public async Task TrainerPage_StatsLabels_Displayed()
        {
            NavigateTo("Trainer's Profile");
            // TrainerPage renders 8 LiveCharts charts — give them time to settle
            // before Appium queries the accessibility tree to avoid session timeout
            await Task.Delay(3000);
            AppiumElement winRateLabel = FindUIElement("WinRateLabel");

            winRateLabel.Displayed.ShouldBeTrue();
        }
    }
}
