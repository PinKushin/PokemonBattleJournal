namespace PokemonBattleJournal.Tests.Services
{
    public class MatchResultCalculatorFactoryTests
    {
        public MatchResultCalculatorFactoryTests()
        {

        }

        private MatchResultCalculatorFactory CreateFactory()
        {
            return new MatchResultCalculatorFactory();
        }

        [Fact]
        public void MatchResultCalculatorFactory_GetCalculator_ReturnsABO1Calculator()
        {
            // Arrange
            MatchResultCalculatorFactory factory = CreateFactory();
            bool isBestOf3 = false;


            // Act
            Interfaces.IMatchResultCalculator calculator = factory.GetCalculator(
                isBestOf3);

            // Assert
            _ = calculator.ShouldBeOfType<BO1ResultCalculator>();
        }
        [Fact]
        public void GetCalculator_ReturnsABO3Calculator()
        {
            // Arrange
            MatchResultCalculatorFactory factory = CreateFactory();
            bool isBestOf3 = true;


            // Act
            Interfaces.IMatchResultCalculator calculator = factory.GetCalculator(
                isBestOf3);

            // Assert
            _ = calculator.ShouldBeOfType<BO3ResultCalculator>();
        }

    }
}
