namespace PokemonBattleJournal.Tests.ViewModels
{
    public class MainPageViewModelTests
    {
        private readonly MainPageViewModel _viewModel;
        public MainPageViewModelTests() 
        {
            //SUT
            _viewModel = new MainPageViewModel();
        }
        //naming convention = ClassName__MethodName_ExpectedResult
        [Fact]
        public void MainPageViewModel_UpdateTrainerName_TrainerNameChanged()
        {
            // Arrange
            string nameInput = "TestName";
            // Act
            _viewModel.NameInput = nameInput;
            _viewModel.UpdateTrainerName();
            // Assert
            _viewModel.TrainerName.Should().Be(nameInput);
            _viewModel.WelcomeMsg.Should().Be($"Welcome {nameInput}");
            _viewModel.NameInput.Should().BeNull();
        }

        [Fact]
        public void MainPageViewModel_UpdateTrainerName_TrainerNameUnchanged()
        {//naming convention = ClassName__MethodName_ExpectedResult

            // Arrange
            string? nameInput = null;
            var prevName = _viewModel.TrainerName;
            // Act
            _viewModel.NameInput = nameInput;
            _viewModel.UpdateTrainerName();
            // Assert
            _viewModel.TrainerName.Should().NotBe(nameInput);
            _viewModel.TrainerName.Should().Be(prevName);
            _viewModel.WelcomeMsg.Should().Be($"Welcome {prevName}");
            _viewModel.NameInput.Should().BeNull();
        }
    }
}
