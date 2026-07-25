namespace PokemonBattleJournal.Tests.ViewModels
{
    /// <summary>
    /// Pins the public surface of FirstStartPageViewModel to what FirstStartPage.xaml binds to.
    /// Rename or remove a bound member → this test breaks. Update both together.
    /// </summary>
    public class FirstStartPageViewModelContractTests
    {
        private static readonly Type _vm = typeof(FirstStartPageViewModel);

        [Theory]
        [InlineData("TrainerNameInput")]
        public void FirstStartPageViewModel_HasProperty(string name) =>
            _vm.GetProperty(name).ShouldNotBeNull($"FirstStartPage.xaml binds to '{name}' but it was not found on FirstStartPageViewModel");

        [Theory]
        [InlineData("SaveTrainerNameCommand")]
        public void FirstStartPageViewModel_HasCommand(string name) =>
            _vm.GetProperty(name).ShouldNotBeNull($"FirstStartPage.xaml binds to Command '{name}' but it was not found on FirstStartPageViewModel");
    }
}
