using PokemonBattleJournal.ViewModels;
using System;
using Xunit;

namespace PokemonBattleJournal.Tests.ViewModels
{
    public class MainPageViewModelTests
    {
        [Fact]
        public void UpdateTrainerName_StateUnderTest_ExpectedBehavior()
        {
            // Arrange
            var viewModel = new MainPageViewModel();
            string? trainerName = null;

            // Act
            viewModel.UpdateTrainerName(
                trainerName);

            // Assert
            Assert.True(false);
        }
    }
}
