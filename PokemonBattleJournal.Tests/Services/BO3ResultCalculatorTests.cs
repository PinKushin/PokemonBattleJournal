namespace PokemonBattleJournal.Tests.Services
{
    public class BO3ResultCalculatorTests
    {
        private BO3ResultCalculator _bO3ResultCalculator = null!;


        [SetUp]
        public void SetUp()
        {
            _bO3ResultCalculator = new();
        }

        [Test]
        public void CalculateResult_AllResultsNull_ThrowsException()
        {
            // Arrange
            MatchResult? result1 = null;
            MatchResult? result2 = null;
            MatchResult? result3 = null;

            // Act & Assert
            _ = Should.Throw<ArgumentNullException>(() =>
            {
                _ = _bO3ResultCalculator.CalculateResult(result1, result2, result3);
            });
        }

        [Test]
        public void CalculateResult_2ResultsNull_ThrowsException()
        {
            // Arrange
            MatchResult? result1 = MatchResult.Win;
            MatchResult? result2 = null;
            MatchResult? result3 = null;

            // Act & Assert
            _ = Should.Throw<ArgumentNullException>(() =>
            {
                _ = _bO3ResultCalculator.CalculateResult(result1, result2, result3);
            });
        }

        [Test]
        public void CalculateResult_TwoWins_ReturnsMatchResultOfWin()
        {
            // Arrange
            MatchResult? result1 = MatchResult.Win;
            MatchResult? result2 = MatchResult.Win;
            MatchResult? result3 = null;

            // Act
            MatchResult result = _bO3ResultCalculator.CalculateResult(
                result1,
                result2,
                result3);

            // Assert
            _ = result.ShouldBeOfType<MatchResult>();
            result.ShouldBe(MatchResult.Win);
        }

        [Test]
        public void CalculateResult_TieWinLoss_ReturnsMatchResultOfTie()
        {
            // Arrange
            MatchResult? result1 = MatchResult.Tie;
            MatchResult? result2 = MatchResult.Win;
            MatchResult? result3 = MatchResult.Loss;

            // Act
            MatchResult result = _bO3ResultCalculator.CalculateResult(
                result1,
                result2,
                result3);

            // Assert
            _ = result.ShouldBeOfType<MatchResult>();
            result.ShouldBe(MatchResult.Tie);
        }
        [Test]
        public void CalculateResult_TwoLosses_ReturnsMatchResultOfLoss()
        {
            // Arrange
            MatchResult? result1 = MatchResult.Tie;
            MatchResult? result2 = MatchResult.Loss;
            MatchResult? result3 = MatchResult.Loss;

            // Act
            MatchResult result = _bO3ResultCalculator.CalculateResult(
                result1,
                result2,
                result3);

            // Assert
            _ = result.ShouldBeOfType<MatchResult>();
            result.ShouldBe(MatchResult.Loss);
        }
        [Test]
        public void CalculateResult_TwoTiesOneLoss_ReturnsMatchResultOfLoss()
        {
            // Arrange
            MatchResult? result1 = MatchResult.Tie;
            MatchResult? result2 = MatchResult.Tie;
            MatchResult? result3 = MatchResult.Loss;
            // Act
            MatchResult result = _bO3ResultCalculator.CalculateResult(
                result1,
                result2,
                result3);
            // Assert
            _ = result.ShouldBeOfType<MatchResult>();
            result.ShouldBe(MatchResult.Loss);
        }
        [Test]
        public void CalculateResult_TwoTies_ReturnsMatchResultOfTie()
        {
            // Arrange
            MatchResult? result1 = MatchResult.Tie;
            MatchResult? result2 = MatchResult.Tie;
            MatchResult? result3 = null;
            // Act
            MatchResult result = _bO3ResultCalculator.CalculateResult(
                result1,
                result2,
                result3);
            // Assert
            _ = result.ShouldBeOfType<MatchResult>();
            result.ShouldBe(MatchResult.Tie);
        }
        [Test]
        public void CalculateResult_OneWinOneLoss_ReturnsMatchResultOfTie()
        {
            // Arrange
            MatchResult? result1 = MatchResult.Win;
            MatchResult? result2 = MatchResult.Loss;
            MatchResult? result3 = null;
            // Act
            MatchResult result = _bO3ResultCalculator.CalculateResult(
                result1,
                result2,
                result3);
            // Assert
            _ = result.ShouldBeOfType<MatchResult>();
            result.ShouldBe(MatchResult.Tie);
        }
        [Test]
        public void CalculateResult_TwoTiesAndOneWin_ReturnsMatchResultOfWin()
        {
            // Arrange
            MatchResult? result1 = MatchResult.Tie;
            MatchResult? result2 = MatchResult.Tie;
            MatchResult? result3 = MatchResult.Win;

            // Act
            MatchResult result = _bO3ResultCalculator.CalculateResult(result1, result2, result3);

            // Assert
            result.ShouldBe(MatchResult.Win);
        }
        [Test]
        public void CalculateResult_TwoWinsAndOneTie_ReturnsMatchResultOfWin()
        {
            // Arrange
            MatchResult? result1 = MatchResult.Win;
            MatchResult? result2 = MatchResult.Win;
            MatchResult? result3 = MatchResult.Tie;

            // Act
            MatchResult result = _bO3ResultCalculator.CalculateResult(result1, result2, result3);

            // Assert
            result.ShouldBe(MatchResult.Win);
        }

        [Test]
        public void CalculateResult_ThreeWins_ReturnsMatchResultOfWin()
        {
            // Arrange
            MatchResult? result1 = MatchResult.Win;
            MatchResult? result2 = MatchResult.Win;
            MatchResult? result3 = MatchResult.Win;

            // Act
            MatchResult result = _bO3ResultCalculator.CalculateResult(result1, result2, result3);

            // Assert
            result.ShouldBe(MatchResult.Win);
        }

        [Test]
        public void CalculateResult_ThreeLosses_ReturnsMatchResultOfLoss()
        {
            // Arrange
            MatchResult? result1 = MatchResult.Loss;
            MatchResult? result2 = MatchResult.Loss;
            MatchResult? result3 = MatchResult.Loss;

            // Act
            MatchResult result = _bO3ResultCalculator.CalculateResult(result1, result2, result3);

            // Assert
            result.ShouldBe(MatchResult.Loss);
        }

        [Test]
        public void CalculateResult_ThreeTies_ReturnsMatchResultOfTie()
        {
            // Arrange
            MatchResult? result1 = MatchResult.Tie;
            MatchResult? result2 = MatchResult.Tie;
            MatchResult? result3 = MatchResult.Tie;

            // Act
            MatchResult result = _bO3ResultCalculator.CalculateResult(result1, result2, result3);

            // Assert
            result.ShouldBe(MatchResult.Tie);
        }

        [Test]
        public void CalculateResult_WinLossLoss_ReturnsMatchResultOfLoss()
        {
            // Arrange
            MatchResult? result1 = MatchResult.Win;
            MatchResult? result2 = MatchResult.Loss;
            MatchResult? result3 = MatchResult.Loss;

            // Act
            MatchResult result = _bO3ResultCalculator.CalculateResult(result1, result2, result3);

            // Assert
            result.ShouldBe(MatchResult.Loss);
        }
    }
}
