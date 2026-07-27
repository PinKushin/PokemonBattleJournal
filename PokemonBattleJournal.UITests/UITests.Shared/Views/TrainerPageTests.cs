namespace UITests
{
    public partial class TrainerPageTests : BaseTest
    {
        [Fact]
        public async Task TrainerPage_StatsLabels_Displayed()
        {
            NavigateTo("Trainer's Profile");
            // LiveCharts renders 8 charts — give them time before querying accessibility tree
            await Task.Delay(3000);
            AppiumElement winRateLabel = FindUIElement("WinRateLabel");
            winRateLabel.ShouldNotBeNull();
        }

        [Fact]
        public async Task TrainerPage_AllStatsLabels_Displayed()
        {
            NavigateTo("Trainer's Profile");
            await Task.Delay(3000);

            FindUIElement("WinsLabel").ShouldNotBeNull();
            FindUIElement("LossesLabel").ShouldNotBeNull();
            FindUIElement("TiesLabel").ShouldNotBeNull();
            FindUIElement("AverageMatchDurationLabel").ShouldNotBeNull();
            FindUIElement("StreakInfoLabel").ShouldNotBeNull();
        }

        [Fact]
        public async Task TrainerPage_Charts_Rendered()
        {
            NavigateTo("Trainer's Profile");
            // Charts need render time before Appium can find them
            await Task.Delay(5000);

            FindUIElement("MatchupMatrixChart").ShouldNotBeNull();
            FindUIElement("WinRateOverTimeChart").ShouldNotBeNull();
            FindUIElement("MostPlayedChart").ShouldNotBeNull();
            FindUIElement("ArchetypeWinRateChart").ShouldNotBeNull();
        }

        [Fact]
        public async Task TrainerPage_AllCharts_Rendered()
        {
            NavigateTo("Trainer's Profile");
            await Task.Delay(5000);

            FindUIElement("OpponentPerformanceChart").ShouldNotBeNull();
            FindUIElement("TagUsageChart").ShouldNotBeNull();
            FindUIElement("MatchLengthChart").ShouldNotBeNull();
            FindUIElement("FirstTurnChart").ShouldNotBeNull();
        }
    }
}
