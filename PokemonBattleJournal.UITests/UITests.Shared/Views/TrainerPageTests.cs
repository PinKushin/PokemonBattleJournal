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

            // TrainerPageViewModel loads stats asynchronously — poll until WinsLabel shows non-zero.
            // Seeding puts 3 Win matches in; zero means no active trainer or async load not complete.
            var wait = new OpenQA.Selenium.Support.UI.WebDriverWait(App, TimeSpan.FromSeconds(20));
            string winsText = wait.Until(_ =>
            {
                string text = FindUIElement("WinsLabel").Text;
                return int.TryParse(text, out int v) && v > 0 ? text : null;
            });
            int.TryParse(winsText, out int wins);
            wins.ShouldBeGreaterThan(0, $"WinsLabel shows '{winsText}' after 20s — TrainerPage has no data");
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
