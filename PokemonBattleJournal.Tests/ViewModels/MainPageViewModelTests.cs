namespace PokemonBattleJournal.Tests.ViewModels
{
    public class MainPageViewModelTests
    {
        [Fact]
        public void MainPageViewmodel_UpdateTrainerName_TrainerNameChanged()
        {
            // Arrange
            var viewModel = new MainPageViewModel();
            string? nameInput = "TestName";
            

            // Act
            viewModel.NameInput = nameInput;
            viewModel.UpdateTrainerName();

            // Assert
            viewModel.TrainerName.Should().Be(nameInput);
        }
    }
}
