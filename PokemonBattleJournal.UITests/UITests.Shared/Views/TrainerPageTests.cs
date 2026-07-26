namespace UITests
{
    public partial class TrainerPageTests : BaseTest
    {
        [Fact]
        public void TrainerPage_StatsLabels_Displayed()
        {
            var (winRateLabel, elapsed) = MeasurePageLoad("Trainer's Profile", "WinRateLabel");

            winRateLabel.Displayed.ShouldBeTrue();
            elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5));
        }
    }
}
