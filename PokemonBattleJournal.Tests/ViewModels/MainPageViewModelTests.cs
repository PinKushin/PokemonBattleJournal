namespace PokemonBattleJournal.Tests.ViewModels
{
    public class MainPageViewModelTests
    {
        [Fact]
        public void MainPageViewModel_UpdateTrainerName_TrainerNameChanged()
        {
            // Arrange
            var viewModel = new MainPageViewModel();
            string nameInput = "TestName";
            // Act
            viewModel.NameInput = nameInput;
            viewModel.UpdateTrainerName();
            // Assert
            viewModel.TrainerName.Should().Be(nameInput);
            viewModel.WelcomeMsg.Should().Be($"Welcome {nameInput}");
            viewModel.NameInput.Should().BeNull();
        }
        [Fact]
        public void MainPageViewModel_UpdateTrainerName_TrainerNameUnchanged()
        {
            // Arrange
            var viewModel = new MainPageViewModel();
            string? nameInput = null;
            var prevName = viewModel.TrainerName;
            // Act
            viewModel.NameInput = nameInput;
            viewModel.UpdateTrainerName();
            // Assert
            viewModel.TrainerName.Should().NotBe(nameInput);
            viewModel.TrainerName.Should().Be(prevName);
            viewModel.WelcomeMsg.Should().Be($"Welcome {prevName}");
        }
    }
}
