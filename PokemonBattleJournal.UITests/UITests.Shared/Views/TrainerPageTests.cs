using System.Globalization;

namespace UITests
{
    public partial class TrainerPageTests : BaseTest
    {
        [OneTimeSetUp]
        public void SetUp()
        {
            NavigateTo("Trainer's Profile");
            // Sync on match load + chart computation before any element queries.
            WaitUntilBusyGone("Busy_ChartData", timeoutMs: 15000);
        }

        /// <summary>
        /// The four stat labels must agree with each other under the canonical win-rate formula.
        /// </summary>
        /// <remarks>
        /// <para>Every other test here checks one label in isolation for a non-zero value. That
        /// catches a stat that failed to load; it cannot catch a stat that loaded a wrong number,
        /// which is the likelier defect and completely invisible today. A win rate computed over
        /// the wrong denominator, ties not counted as half, or a label bound to the neighbouring
        /// property all pass every existing assertion.</para>
        ///
        /// <para>Asserts the relationship rather than absolute values, deliberately. MainPageTests
        /// sorts before this fixture and saves matches, so the counts differ between a full-suite
        /// run and a single-fixture run — any hard-coded total would be correct in one and wrong
        /// in the other. The invariant holds for every possible seed.</para>
        ///
        /// <para>Formula from <c>Utilities/Calculations.cs</c>, which CLAUDE.md names canonical:
        /// <c>(wins + 0.5 * ties) / total * 100</c>. This pins the rendered UI to it, so stats
        /// code drifting from the documented formula fails here rather than shipping.</para>
        /// </remarks>
        [Test]
        public void TrainerPage_WinRate_AgreesWithWinsLossesAndTies()
        {
            // Read after the busy gate in SetUp, so all four reflect the same completed load.
            int wins = ReadIntLabel("WinsLabel");
            int losses = ReadIntLabel("LossesLabel");
            int ties = ReadIntLabel("TiesLabel");

            int total = wins + losses + ties;
            total.ShouldBeGreaterThan(0, "the seeded trainer must have matches, or this proves nothing");

            string winRateText = FindUIElement("WinRateLabel").Text;
            double shown = double.Parse(winRateText.TrimEnd('%'), CultureInfo.InvariantCulture);
            double expected = (wins + (0.5 * ties)) / total * 100;

            // Tolerance covers the label's F1 rounding only.
            shown.ShouldBe(expected, 0.05,
                $"win rate must equal (wins + 0.5*ties)/total*100. " +
                $"Showed '{winRateText}' with {wins}W/{losses}L/{ties}T");
        }

        /// <summary>Reads a stat label as an integer, failing with the actual text when it is not one.</summary>
        private int ReadIntLabel(string automationId)
        {
            string text = FindUIElement(automationId).Text;
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                throw new AssertionException($"'{automationId}' should show a whole number but showed '{text}'");

            return value;
        }

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
        public void TrainerPage_AllEightCharts_Rendered()
        {
            // FindUIElement implicit wait polls until each chart element appears after LiveCharts renders.
            FindUIElement("MatchupMatrixChart").ShouldNotBeNull();
            FindUIElement("WinRateOverTimeChart").ShouldNotBeNull();
            FindUIElement("MostPlayedChart").ShouldNotBeNull();
            FindUIElement("ArchetypeWinRateChart").ShouldNotBeNull();
            FindUIElement("OpponentPerformanceChart").ShouldNotBeNull();
            FindUIElement("TagUsageChart").ShouldNotBeNull();
            FindUIElement("MatchLengthChart").ShouldNotBeNull();
            FindUIElement("FirstTurnChart").ShouldNotBeNull();
        }

        [Test]
        public void TrainerPage_LossesLabel_ShowsNonZero()
        {
            // Seeding now includes 2 Loss matches; zero means seed or async load failed.
            var wait = new OpenQA.Selenium.Support.UI.WebDriverWait(App, TimeSpan.FromSeconds(20));
            string lossesText = wait.Until(_ =>
            {
                string text = FindUIElement("LossesLabel").Text;
                return int.TryParse(text, out int v) && v > 0 ? text : null;
            });
            int.TryParse(lossesText, out int losses);
            losses.ShouldBeGreaterThan(0, $"LossesLabel shows '{lossesText}' — expected seeded losses");
        }

        [Test]
        public void TrainerPage_TiesLabel_ShowsNonZero()
        {
            // Seeding includes 1 Tie match.
            var wait = new OpenQA.Selenium.Support.UI.WebDriverWait(App, TimeSpan.FromSeconds(20));
            string tiesText = wait.Until(_ =>
            {
                string text = FindUIElement("TiesLabel").Text;
                return int.TryParse(text, out int v) && v > 0 ? text : null;
            });
            int.TryParse(tiesText, out int ties);
            ties.ShouldBeGreaterThan(0, $"TiesLabel shows '{tiesText}' — expected seeded ties");
        }

        [Test]
        public void TrainerPage_WinRateLabel_ShowsValue()
        {
            var wait = new OpenQA.Selenium.Support.UI.WebDriverWait(App, TimeSpan.FromSeconds(10));
            string winRate = wait.Until(_ =>
            {
                string text = FindUIElement("WinRateLabel").Text;
                return !string.IsNullOrEmpty(text) ? text : null;
            });
            winRate.ShouldNotBeNullOrEmpty();
        }

        [Test]
        public void TrainerPage_AverageMatchDuration_ShowsValue()
        {
            // Seeded matches have distinct start/end times so duration is non-zero.
            var wait = new OpenQA.Selenium.Support.UI.WebDriverWait(App, TimeSpan.FromSeconds(10));
            string text = wait.Until(_ =>
            {
                string t = FindUIElement("AverageMatchDurationLabel").Text;
                return !string.IsNullOrEmpty(t) ? t : null;
            });
            text.ShouldNotBeNullOrEmpty();
        }

        [Test]
        public void TrainerPage_StreakInfo_ShowsValue()
        {
            var wait = new OpenQA.Selenium.Support.UI.WebDriverWait(App, TimeSpan.FromSeconds(10));
            string text = wait.Until(_ =>
            {
                string t = FindUIElement("StreakInfoLabel").Text;
                return !string.IsNullOrEmpty(t) ? t : null;
            });
            text.ShouldNotBeNullOrEmpty();
        }

        [Test]
        public void TrainerPage_StatNameLabels_Displayed()
        {
            FindUIElement("WinRateStatName").ShouldNotBeNull();
            FindUIElement("WinsStatName").ShouldNotBeNull();
            FindUIElement("LossesStatName").ShouldNotBeNull();
            FindUIElement("TiesStatName").ShouldNotBeNull();
        }

        [Test]
        public void TrainerPage_ChartHeadings_Displayed()
        {
            FindUIElement("MatchupMatrixHeading").ShouldNotBeNull();
            FindUIElement("WinRateOverTimeHeading").ShouldNotBeNull();
            FindUIElement("MostPlayedHeading").ShouldNotBeNull();
            FindUIElement("ArchetypeWinRateHeading").ShouldNotBeNull();
            FindUIElement("OpponentPerformanceHeading").ShouldNotBeNull();
            FindUIElement("TagUsageHeading").ShouldNotBeNull();
            FindUIElement("MatchLengthHeading").ShouldNotBeNull();
            FindUIElement("FirstTurnHeading").ShouldNotBeNull();
        }

    }
}
