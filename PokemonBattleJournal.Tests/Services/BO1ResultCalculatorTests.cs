namespace PokemonBattleJournal.Tests.Services
{
    public class BO1ResultCalculatorTests
    {
        private BO1ResultCalculator _bO1ResultCalculator = null!;


        [SetUp]
        public void SetUp()
        {
            //SUT
            _bO1ResultCalculator = new();
        }

        [Test]
        public void CalculateResult_NullInput_ThrowsException()
        {
            // Arrange
            MatchResult? result1 = null;

            // Act & Assert
            _ = Should.Throw<ArgumentNullException>(() =>
            {
                _ = _bO1ResultCalculator.CalculateResult(result1);
            });
        }

        [Test]
        public void CalculateResult_WinInput_ReturnMatchResultOfWin()
        {
            // Arrange
            MatchResult? result1 = MatchResult.Win;

            // Act
            MatchResult result = _bO1ResultCalculator.CalculateResult(result1);

            // Assert
            _ = result.ShouldBeOfType<MatchResult>();
            result.ShouldBe(MatchResult.Win);
        }

        [Test]
        public void CalculateResult_LossInput_ReturnMatchResultOfLoss()
        {
            // Arrange
            MatchResult? result1 = MatchResult.Loss;

            // Act
            MatchResult result = _bO1ResultCalculator.CalculateResult(result1);

            // Assert
            _ = result.ShouldBeOfType<MatchResult>();
            result.ShouldBe(MatchResult.Loss);
        }

        [Test]
        public void CalculateResult_TieInput_ReturnMatchResultOfTie()
        {
            // Arrange
            MatchResult? result1 = MatchResult.Tie;

            // Act
            MatchResult result = _bO1ResultCalculator.CalculateResult(result1);

            // Assert
            _ = result.ShouldBeOfType<MatchResult>();
            result.ShouldBe(MatchResult.Tie);
        }
    }
}
