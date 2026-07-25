namespace PokemonBattleJournal.Tests.ViewModels
{
    /// <summary>
    /// Pins the public surface of TrainerPageViewModel to what TrainerPage.xaml binds to.
    /// Rename or remove a bound member → this test breaks. Update both together.
    /// </summary>
    public class TrainerPageViewModelContractTests
    {
        private static readonly Type _vm = typeof(TrainerPageViewModel);

        [Theory]
        [InlineData("WelcomeMsg")]
        [InlineData("WinAverage")]
        [InlineData("Wins")]
        [InlineData("Losses")]
        [InlineData("Ties")]
        [InlineData("AverageMatchDuration")]
        [InlineData("StreakInfo")]
        // Matchup heatmap
        [InlineData("MatchupHeatSeries")]
        [InlineData("MatchupXAxes")]
        [InlineData("MatchupYAxes")]
        // Bar charts
        [InlineData("MostPlayedSeries")]
        [InlineData("MostPlayedYAxes")]
        [InlineData("ArchetypeWinRateSeries")]
        [InlineData("ArchetypeWinRateYAxes")]
        [InlineData("TagUsageSeries")]
        [InlineData("TagUsageYAxes")]
        [InlineData("OpponentSeries")]
        [InlineData("OpponentYAxes")]
        [InlineData("MatchLengthSeries")]
        [InlineData("MatchLengthYAxes")]
        [InlineData("FirstTurnSeries")]
        [InlineData("FirstTurnYAxes")]
        // Line chart
        [InlineData("WinRateOverTimeSeries")]
        [InlineData("WinRateTimeXAxes")]
        public void TrainerPageViewModel_HasProperty(string name) =>
            _vm.GetProperty(name).ShouldNotBeNull($"TrainerPage.xaml binds to '{name}' but it was not found on TrainerPageViewModel");

        [Theory]
        [InlineData("AppearingCommand")]
        public void TrainerPageViewModel_HasCommand(string name) =>
            _vm.GetProperty(name).ShouldNotBeNull($"TrainerPage.xaml binds to Command '{name}' but it was not found on TrainerPageViewModel");
    }
}
