namespace PokemonBattleJournal.Tests.ViewModels
{
    /// <summary>
    /// Pins the public surface of TrainerPageViewModel to what TrainerPage.xaml binds to.
    /// Rename or remove a bound member → this test breaks. Update both together.
    /// </summary>
    public class TrainerPageViewModelContractTests
    {
        private static readonly Type _vm = typeof(TrainerPageViewModel);

        [Test]
        [TestCase("WelcomeMsg")]
        [TestCase("WinAverage")]
        [TestCase("Wins")]
        [TestCase("Losses")]
        [TestCase("Ties")]
        [TestCase("AverageMatchDuration")]
        [TestCase("StreakInfo")]
        // Matchup heatmap
        [TestCase("MatchupHeatSeries")]
        [TestCase("MatchupXAxes")]
        [TestCase("MatchupYAxes")]
        // Bar charts
        [TestCase("MostPlayedSeries")]
        [TestCase("MostPlayedYAxes")]
        [TestCase("ArchetypeWinRateSeries")]
        [TestCase("ArchetypeWinRateYAxes")]
        [TestCase("TagUsageSeries")]
        [TestCase("TagUsageYAxes")]
        [TestCase("OpponentSeries")]
        [TestCase("OpponentYAxes")]
        [TestCase("MatchLengthSeries")]
        [TestCase("MatchLengthYAxes")]
        [TestCase("FirstTurnSeries")]
        [TestCase("FirstTurnYAxes")]
        // Line chart
        [TestCase("WinRateOverTimeSeries")]
        [TestCase("WinRateTimeXAxes")]
        public void TrainerPageViewModel_HasProperty(string name) =>
            _vm.GetProperty(name).ShouldNotBeNull($"TrainerPage.xaml binds to '{name}' but it was not found on TrainerPageViewModel");

        [Test]
        [TestCase("AppearingCommand")]
        public void TrainerPageViewModel_HasCommand(string name) =>
            _vm.GetProperty(name).ShouldNotBeNull($"TrainerPage.xaml binds to Command '{name}' but it was not found on TrainerPageViewModel");
    }
}
