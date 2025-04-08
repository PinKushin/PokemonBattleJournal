namespace PokemonBattleJournal.Tests.Services
{
    public class BO1ResultCalculatorTests
    {
        private readonly BO1ResultCalculator _bO1ResultCalculator;


        public BO1ResultCalculatorTests()
        {
            //SUT
            _bO1ResultCalculator = new();
        }

        [Fact]
        public void CalculateResult_NullInput_ThrowsException()
        {
            // Arrange
            MatchResult? result1 = null;

            // Act & Assert
            Should.Throw<ArgumentNullException>(() =>
            {
                _bO1ResultCalculator.CalculateResult(result1);
            });
        }

        [Fact]
        public void CalculateResult_WinInput_ReturnMatchResultOfWin()
        {
            // Arrange
            MatchResult? result1 = MatchResult.Win;

            // Act
            var result = _bO1ResultCalculator.CalculateResult(result1);

            // Assert
            result.ShouldBeOfType<MatchResult>();
            result.ShouldBe(MatchResult.Win);
        }

        [Fact]
        public void CalculateResult_LossInput_ReturnMatchResultOfLoss()
        {
            // Arrange
            MatchResult? result1 = MatchResult.Loss;

            // Act
            var result = _bO1ResultCalculator.CalculateResult(result1);

            // Assert
            result.ShouldBeOfType<MatchResult>();
            result.ShouldBe(MatchResult.Loss);
        }
    }
}
