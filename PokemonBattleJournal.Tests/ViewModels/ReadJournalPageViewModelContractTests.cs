namespace PokemonBattleJournal.Tests.ViewModels
{
    /// <summary>
    /// Pins the public surface of ReadJournalPageViewModel to what ReadJournalPage.xaml binds to.
    /// Rename or remove a bound member → this test breaks. Update both together.
    /// </summary>
    public class ReadJournalPageViewModelContractTests
    {
        private static readonly Type _vm = typeof(ReadJournalPageViewModel);

        [Theory]
        [InlineData("WelcomeMsg")]
        [InlineData("MatchHistory")]
        [InlineData("SelectedMatch")]
        [InlineData("SelectedNote")]
        [InlineData("PlayingName")]
        [InlineData("PlayingIconSource")]
        [InlineData("AgainstName")]
        [InlineData("AgainstIconSource")]
        [InlineData("Game1TagsInfo")]
        [InlineData("Game2TagsInfo")]
        [InlineData("Game3TagsInfo")]
        [InlineData("HasGame1Tags")]
        [InlineData("HasGame2Tags")]
        [InlineData("HasGame3Tags")]
        [InlineData("TagsSelectedGame1")]
        [InlineData("TagsSelectedGame2")]
        [InlineData("TagsSelectedGame3")]
        public void ReadJournalPageViewModel_HasProperty(string name) =>
            _vm.GetProperty(name).ShouldNotBeNull($"ReadJournalPage.xaml binds to '{name}' but it was not found on ReadJournalPageViewModel");

        [Theory]
        [InlineData("AppearingCommand")]
        [InlineData("LoadMatchCommand")]
        public void ReadJournalPageViewModel_HasCommand(string name) =>
            _vm.GetProperty(name).ShouldNotBeNull($"ReadJournalPage.xaml binds to Command '{name}' but it was not found on ReadJournalPageViewModel");
    }
}
