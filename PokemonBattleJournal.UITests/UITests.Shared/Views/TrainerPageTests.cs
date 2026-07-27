namespace UITests
{
    public partial class TrainerPageTests : BaseTest
    {
        [Fact]
        public void TrainerPage_StatsLabels_Displayed()
        {
            NavigateTo("Trainer's Profile");
            FindUIElement("WinRateLabel").ShouldNotBeNull();
        }

        [Fact]
        public void TrainerPage_AllStatsLabels_Displayed()
        {
            NavigateTo("Trainer's Profile");
            FindUIElement("WinsLabel").ShouldNotBeNull();
            FindUIElement("LossesLabel").ShouldNotBeNull();
            FindUIElement("TiesLabel").ShouldNotBeNull();
            FindUIElement("AverageMatchDurationLabel").ShouldNotBeNull();
            FindUIElement("StreakInfoLabel").ShouldNotBeNull();
        }

        [Fact]
        public void TrainerPage_HasSeededData()
        {
            NavigateTo("Trainer's Profile");

            // WinsLabel must be non-zero — seeding puts 3 Win matches in.
            // Failure here means TrainerPage loaded with no active trainer or DB is empty.
            AppiumElement winsLabel = FindUIElement("WinsLabel");
            winsLabel.ShouldNotBeNull();
            string winsText = winsLabel.Text;
            int.TryParse(winsText, out int wins);
            wins.ShouldBeGreaterThan(0, $"WinsLabel shows '{winsText}' — TrainerPage has no data");
        }

        [Fact]
        public async Task TrainerPage_Charts_Rendered()
        {
            NavigateTo("Trainer's Profile");
            await Task.Delay(1000); // allow LiveCharts to finish initial render

            FindUIElement("MatchupMatrixChart").ShouldNotBeNull();
            FindUIElement("WinRateOverTimeChart").ShouldNotBeNull();
            FindUIElement("MostPlayedChart").ShouldNotBeNull();
            FindUIElement("ArchetypeWinRateChart").ShouldNotBeNull();
        }

        [Fact]
        public async Task TrainerPage_AllCharts_Rendered()
        {
            NavigateTo("Trainer's Profile");
            await Task.Delay(1000);

            FindUIElement("OpponentPerformanceChart").ShouldNotBeNull();
            FindUIElement("TagUsageChart").ShouldNotBeNull();
            FindUIElement("MatchLengthChart").ShouldNotBeNull();
            FindUIElement("FirstTurnChart").ShouldNotBeNull();
        }
    }
}
