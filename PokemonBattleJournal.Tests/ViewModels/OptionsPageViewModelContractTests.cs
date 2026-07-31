namespace PokemonBattleJournal.Tests.ViewModels
{
    /// <summary>
    /// Pins the public surface of OptionsPageViewModel to what OptionsPage.xaml binds to.
    /// Rename or remove a bound member → this test breaks. Update both together.
    /// </summary>
    public class OptionsPageViewModelContractTests
    {
        private static readonly Type _vm = typeof(OptionsPageViewModel);

        [Test]
        [TestCase("Title")]
        [TestCase("NameInput")]
        [TestCase("NewDeckName")]
        [TestCase("SelectedIcon")]
        [TestCase("IconCollection")]
        [TestCase("IconItems")]
        [TestCase("SelectedIconItem")]
        [TestCase("TagInput")]
        public void OptionsPageViewModel_HasProperty(string name) =>
            _vm.GetProperty(name).ShouldNotBeNull($"OptionsPage.xaml binds to '{name}' but it was not found on OptionsPageViewModel");

        [Test]
        [TestCase("AppearingCommand")]
        [TestCase("SaveTrainerCommand")]
        [TestCase("SaveArchetypeCommand")]
        [TestCase("SaveTagCommand")]
        [TestCase("SaveAllCommand")]
        [TestCase("DeleteTrainerFileCommand")]
        public void OptionsPageViewModel_HasCommand(string name) =>
            _vm.GetProperty(name).ShouldNotBeNull($"OptionsPage.xaml binds to Command '{name}' but it was not found on OptionsPageViewModel");
    }
}
