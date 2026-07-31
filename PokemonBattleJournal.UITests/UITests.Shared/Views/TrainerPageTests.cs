namespace UITests
{
    public partial class TrainerPageTests : BaseTest
    {
        [OneTimeSetUp]
        public void SetUp() => NavigateTo("Trainer's Profile");

        [Test]
        public void TrainerPage_StatsLabels_Displayed()
        {
            FindUIElement("WinRateLabel").ShouldNotBeNull();
        }

        [Test]
        public void TrainerPage_AllStatsLabels_Displayed()
        {
            FindUIElement("WinsLabel").ShouldNotBeNull();
            FindUIElement("LossesLabel").ShouldNotBeNull();
            FindUIElement("TiesLabel").ShouldNotBeNull();
            FindUIElement("AverageMatchDurationLabel").ShouldNotBeNull();
            FindUIElement("StreakInfoLabel").ShouldNotBeNull();
        }

        [Test]
        public void TrainerPage_HasSeededData()
        {

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

        [Test]
        public void TrainerPage_Charts_Rendered()
        {
            // FindUIElement implicit wait polls until each chart element appears after LiveCharts renders.
            FindUIElement("MatchupMatrixChart").ShouldNotBeNull();
            FindUIElement("WinRateOverTimeChart").ShouldNotBeNull();
            FindUIElement("MostPlayedChart").ShouldNotBeNull();
            FindUIElement("ArchetypeWinRateChart").ShouldNotBeNull();
        }

        [Test]
        public void TrainerPage_AllCharts_Rendered()
        {
            FindUIElement("OpponentPerformanceChart").ShouldNotBeNull();
            FindUIElement("TagUsageChart").ShouldNotBeNull();
            FindUIElement("MatchLengthChart").ShouldNotBeNull();
            FindUIElement("FirstTurnChart").ShouldNotBeNull();
        }
    }
}
