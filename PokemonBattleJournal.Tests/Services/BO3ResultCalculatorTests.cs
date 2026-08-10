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
        // Game 1's loss must actually be counted. CalculateResult_OneWinOneLoss_... already
        // covers a win and a loss, but it puts the loss in game TWO, so the `losses++` for
        // game one never decides anything — deleting it, or turning it into `losses--`, left
        // every existing test passing (Stryker, 2026-08-10).
        //
        // Loss-then-Win is the input where correct and broken differ: correct gives 1-1 and a
        // Tie, a missing increment gives 1-0 and a Win, and a decrement gives 1 to -1, also a
        // Win. The assertion did not need strengthening; the condition did.
        [Test]
        public void CalculateResult_LossThenWin_ReturnsMatchResultOfTie()
        {
            // Arrange
            MatchResult? result1 = MatchResult.Loss;
            MatchResult? result2 = MatchResult.Win;
            MatchResult? result3 = null;
            // Act
            MatchResult result = _bO3ResultCalculator.CalculateResult(
                result1,
                result2,
                result3);
            // Assert
            result.ShouldBe(MatchResult.Tie);
        }

        // The guard is three OR'd clauses and only ONE of them was ever exercised:
        // CalculateResult_2ResultsNull_... passes (Win, null, null), which the third clause
        // decides, so mutations to the first two clauses could not change the outcome.
        // These cover the missing positions.
        [TestCase(null, null, MatchResult.Win, TestName = "CalculateResult_OnlyGame3Present_Throws")]
        [TestCase(null, MatchResult.Win, null, TestName = "CalculateResult_OnlyGame2Present_Throws")]
        public void CalculateResult_FewerThanTwoGames_ThrowsArgumentNullException(
            MatchResult? result1, MatchResult? result2, MatchResult? result3)
        {
            ArgumentNullException ex = Should.Throw<ArgumentNullException>(() =>
                _bO3ResultCalculator.CalculateResult(result1, result2, result3));

            // The message is part of the contract — without asserting it, replacing the string
            // with "" survived.
            ex.Message.ShouldContain("Need results for at least 2 games");
        }

        // The control for the guard: exactly two games present must NOT throw, in every
        // position. These kill the logical mutations that widen the guard (&& becoming ||),
        // which no "it throws" test can detect — a broadened guard throws MORE, not less.
        [TestCase(null, MatchResult.Win, MatchResult.Loss, TestName = "CalculateResult_Games2And3Present_DoesNotThrow")]
        [TestCase(MatchResult.Win, null, MatchResult.Loss, TestName = "CalculateResult_Games1And3Present_DoesNotThrow")]
        public void CalculateResult_ExactlyTwoGames_ReturnsTieWithoutThrowing(
            MatchResult? result1, MatchResult? result2, MatchResult? result3)
        {
            MatchResult result = _bO3ResultCalculator.CalculateResult(result1, result2, result3);

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
