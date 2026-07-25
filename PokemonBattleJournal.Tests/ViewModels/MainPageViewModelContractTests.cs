namespace PokemonBattleJournal.Tests.ViewModels
{
    /// <summary>
    /// Pins the public surface of MainPageViewModel to what MainPage.xaml binds to.
    /// Rename or remove a bound member → this test breaks. Update both together.
    /// </summary>
    public class MainPageViewModelContractTests
    {
        private static readonly Type _vm = typeof(MainPageViewModel);

        [Theory]
        [InlineData("WelcomeMsg")]
        [InlineData("Archetypes")]
        [InlineData("PlayerSelected")]
        [InlineData("RivalSelected")]
        [InlineData("StartTime")]
        [InlineData("EndTime")]
        [InlineData("DatePlayed")]
        [InlineData("CurrentDateTimeDisplay")]
        [InlineData("TagCollection")]
        [InlineData("TagsSelected")]
        [InlineData("UserNoteInput")]
        [InlineData("FirstCheck")]
        [InlineData("PossibleResults")]
        [InlineData("Result")]
        [InlineData("SavedFileDisplay")]
        [InlineData("Match2TagsSelected")]
        [InlineData("UserNoteInput2")]
        [InlineData("FirstCheck2")]
        [InlineData("Result2")]
        [InlineData("Match3TagsSelected")]
        [InlineData("UserNoteInput3")]
        [InlineData("FirstCheck3")]
        [InlineData("Result3")]
        [InlineData("ShowGame3")]
        [InlineData("IsGame1Selected")]
        [InlineData("IsGame2Selected")]
        [InlineData("IsGame3Selected")]
        [InlineData("HasValidationErrors")]
        [InlineData("ValidationMessage")]
        public void MainPageViewModel_HasProperty(string name) =>
            _vm.GetProperty(name).ShouldNotBeNull($"MainPage.xaml binds to '{name}' but it was not found on MainPageViewModel");

        [Theory]
        [InlineData("AppearingCommand")]
        [InlineData("DisappearingCommand")]
        [InlineData("SaveMatchCommand")]
        [InlineData("BO3Toggle")]
        [InlineData("SelectGame1Command")]
        [InlineData("SelectGame2Command")]
        [InlineData("SelectGame3Command")]
        [InlineData("ToggleBO3Command")]
        public void MainPageViewModel_HasCommand(string name) =>
            _vm.GetProperty(name).ShouldNotBeNull($"MainPage.xaml binds to Command '{name}' but it was not found on MainPageViewModel");
    }
}
