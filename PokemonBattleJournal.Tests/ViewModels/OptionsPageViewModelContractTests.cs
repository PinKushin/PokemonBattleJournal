namespace PokemonBattleJournal.Tests.ViewModels
{
    /// <summary>
    /// Pins the public surface of OptionsPageViewModel to what OptionsPage.xaml binds to.
    /// Rename or remove a bound member → this test breaks. Update both together.
    /// </summary>
    public class OptionsPageViewModelContractTests
    {
        private static readonly Type _vm = typeof(OptionsPageViewModel);

        [Theory]
        [InlineData("Title")]
        [InlineData("NameInput")]
        [InlineData("NewDeckName")]
        [InlineData("SelectedIcon")]
        [InlineData("IconCollection")]
        [InlineData("IconItems")]
        [InlineData("SelectedIconItem")]
        [InlineData("TagInput")]
        public void OptionsPageViewModel_HasProperty(string name) =>
            _vm.GetProperty(name).ShouldNotBeNull($"OptionsPage.xaml binds to '{name}' but it was not found on OptionsPageViewModel");

        [Theory]
        [InlineData("AppearingCommand")]
        [InlineData("SaveTrainerCommand")]
        [InlineData("SaveArchetypeCommand")]
        [InlineData("SaveTagCommand")]
        [InlineData("SaveAllCommand")]
        [InlineData("DeleteTrainerFileCommand")]
        public void OptionsPageViewModel_HasCommand(string name) =>
            _vm.GetProperty(name).ShouldNotBeNull($"OptionsPage.xaml binds to Command '{name}' but it was not found on OptionsPageViewModel");
    }
}
