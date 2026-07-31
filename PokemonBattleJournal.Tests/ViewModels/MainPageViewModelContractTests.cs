namespace PokemonBattleJournal.Tests.ViewModels
{
    /// <summary>
    /// Pins the public surface of MainPageViewModel to what MainPage.xaml binds to.
    /// Rename or remove a bound member → this test breaks. Update both together.
    /// </summary>
    public class MainPageViewModelContractTests
    {
        private static readonly Type _vm = typeof(MainPageViewModel);

        [Test]
        [TestCase("WelcomeMsg")]
        [TestCase("Archetypes")]
        [TestCase("PlayerSelected")]
        [TestCase("RivalSelected")]
        [TestCase("StartTime")]
        [TestCase("EndTime")]
        [TestCase("DatePlayed")]
        [TestCase("TagCollection")]
        [TestCase("TagsSelected")]
        [TestCase("UserNoteInput")]
        [TestCase("FirstCheck")]
        [TestCase("PossibleResults")]
        [TestCase("Result")]
        [TestCase("SavedFileDisplay")]
        [TestCase("Match2TagsSelected")]
        [TestCase("UserNoteInput2")]
        [TestCase("FirstCheck2")]
        [TestCase("Result2")]
        [TestCase("Match3TagsSelected")]
        [TestCase("UserNoteInput3")]
        [TestCase("FirstCheck3")]
        [TestCase("Result3")]
        [TestCase("ShowGame3")]
        [TestCase("IsGame1Selected")]
        [TestCase("IsGame2Selected")]
        [TestCase("IsGame3Selected")]
        [TestCase("HasValidationErrors")]
        [TestCase("ValidationMessage")]
        public void MainPageViewModel_HasProperty(string name) =>
            _vm.GetProperty(name).ShouldNotBeNull($"MainPage.xaml binds to '{name}' but it was not found on MainPageViewModel");

        [Test]
        [TestCase("AppearingCommand")]
        [TestCase("DisappearingCommand")]
        [TestCase("SaveMatchCommand")]
        [TestCase("BO3Toggle")]
        [TestCase("SelectGame1Command")]
        [TestCase("SelectGame2Command")]
        [TestCase("SelectGame3Command")]
        [TestCase("ToggleBO3Command")]
        [TestCase("ToggleFirstCheckCommand")]
        [TestCase("ToggleFirstCheck2Command")]
        [TestCase("ToggleFirstCheck3Command")]
        public void MainPageViewModel_HasCommand(string name) =>
            _vm.GetProperty(name).ShouldNotBeNull($"MainPage.xaml binds to Command '{name}' but it was not found on MainPageViewModel");
    }
}
