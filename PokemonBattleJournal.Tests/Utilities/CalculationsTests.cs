using PokemonBattleJournal.Utilities;

namespace PokemonBattleJournal.Tests.Utilities
{
    public class CalculationsTests
    {


        public CalculationsTests()
        {

        }

        [Fact]
        public void CalculateOverallResult_ReturnsMatchResultOfTie()
        {
            // Arrange
            MatchResult? result1 = MatchResult.Tie;
            MatchResult? result2 = MatchResult.Win;
            MatchResult? result3 = MatchResult.Loss;

            // Act
            var result = Calculations.CalculateOverallResult(
                result1,
                result2,
                result3);

            // Assert
            result.ShouldBe(MatchResult.Tie);
        }

        [Fact]
        public void CalculateOverallResult_ReturnsMatchResultOfTieSimpleCase()
        {
            // Arrange
            MatchResult? result1 = MatchResult.Tie;
            MatchResult? result2 = MatchResult.Tie;
            MatchResult? result3 = MatchResult.Loss;

            // Act
            var result = Calculations.CalculateOverallResult(
                result1,
                result2,
                result3);

            // Assert
            result.ShouldBe(MatchResult.Tie);
        }

        [Fact]
        public void CalculateOverallResult_ReturnsMatchResultOfWin()
        {
            // Arrange
            MatchResult? result1 = MatchResult.Win;
            MatchResult? result2 = MatchResult.Win;
            MatchResult? result3 = MatchResult.Loss;

            // Act
            var result = Calculations.CalculateOverallResult(
                result1,
                result2,
                result3);

            // Assert
            result.ShouldBe(MatchResult.Win);
        }

        [Fact]
        public void CalculateOverallResult_ReturnsMatchResultOfLoss()
        {
            // Arrange
            MatchResult? result1 = MatchResult.Win;
            MatchResult? result2 = MatchResult.Loss;
            MatchResult? result3 = MatchResult.Loss;

            // Act
            var result = Calculations.CalculateOverallResult(
                result1,
                result2,
                result3);

            // Assert
            result.ShouldBe(MatchResult.Loss);
        }

        [Fact]
        public void CalculateOverallResult_HandlesNullByReturningMatchResultOfLoss()
        {
            // Arrange
            MatchResult? result1 = null;
            MatchResult? result2 = null;
            MatchResult? result3 = null;

            // Act
            var result = Calculations.CalculateOverallResult(
                result1,
                result2,
                result3);

            // Assert
            result.ShouldBe(MatchResult.Loss);
        }
    }
}
