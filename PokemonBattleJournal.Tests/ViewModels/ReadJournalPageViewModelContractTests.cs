namespace PokemonBattleJournal.Tests.ViewModels
{
    /// <summary>
    /// Pins the public surface of ReadJournalPageViewModel to what ReadJournalPage.xaml binds to.
    /// Rename or remove a bound member → this test breaks. Update both together.
    /// </summary>
    public class ReadJournalPageViewModelContractTests
    {
        private static readonly Type _vm = typeof(ReadJournalPageViewModel);

        [Test]
        [TestCase("WelcomeMsg")]
        [TestCase("MatchHistory")]
        [TestCase("SelectedMatch")]
        [TestCase("SelectedNote")]
        [TestCase("PlayingName")]
        [TestCase("PlayingIconSource")]
        [TestCase("AgainstName")]
        [TestCase("AgainstIconSource")]
        [TestCase("Game1TagsInfo")]
        [TestCase("Game2TagsInfo")]
        [TestCase("Game3TagsInfo")]
        [TestCase("HasGame1Tags")]
        [TestCase("HasGame2Tags")]
        [TestCase("HasGame3Tags")]
        [TestCase("TagsSelectedGame1")]
        [TestCase("TagsSelectedGame2")]
        [TestCase("TagsSelectedGame3")]
        public void ReadJournalPageViewModel_HasProperty(string name) =>
            _vm.GetProperty(name).ShouldNotBeNull($"ReadJournalPage.xaml binds to '{name}' but it was not found on ReadJournalPageViewModel");

        [Test]
        [TestCase("AppearingCommand")]
        [TestCase("LoadMatchCommand")]
        public void ReadJournalPageViewModel_HasCommand(string name) =>
            _vm.GetProperty(name).ShouldNotBeNull($"ReadJournalPage.xaml binds to Command '{name}' but it was not found on ReadJournalPageViewModel");
    }
}
