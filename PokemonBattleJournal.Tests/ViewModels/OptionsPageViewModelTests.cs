namespace PokemonBattleJournal.Tests.ViewModels
{
#pragma warning disable IDE0058 // Expression value is never used
    public class OptionsPageViewModelTests
    {
        private readonly OptionsPageViewModel _viewModel;
        public OptionsPageViewModelTests()
        {
            //SUT
            _viewModel = new OptionsPageViewModel();

        }

        [Fact]
        public void OptionsPageViewModel_UpdateTrainerName_TrainerNameChanged()
        {
            // Arrange
            string nameInput = "TestName";
            // Act
            _viewModel.NameInput = nameInput;
            _viewModel.UpdateTrainerName();

            // Assert
            _viewModel.TrainerName.ShouldBe(nameInput);
            _viewModel.NameInput.ShouldBeNull();
            _viewModel.Title.ShouldBe($"{nameInput}'s Options");
        }

        [Fact]
        public void OptionsPageViewModel_UpdateTrainerName_TrainerNameUnchanged()
        {//naming convention = ClassName__MethodName_ExpectedResult

            // Arrange
            string? nameInput = null;
            string prevName = _viewModel.TrainerName;
            // Act
            _viewModel.NameInput = nameInput;

            // Assert
            _viewModel.TrainerName.ShouldNotBe(nameInput);
            _viewModel.TrainerName.ShouldBe(prevName);
            _viewModel.NameInput.ShouldBeNull();
        }

        [Fact]
        public async Task OptionsPageViewModel_AppearingAsync_IconCollectionPopulated()
        {
            // Arrange
            // Act
            await _viewModel.AppearingAsync();
            // Assert
            _viewModel.IconCollection.ShouldNotBeNull();
            _viewModel.IconCollection.ShouldBeAssignableTo<List<string>>();
        }
    }
}
