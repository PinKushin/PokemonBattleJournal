namespace PokemonBattleJournal.Tests.Utilities
{
    public class CalculationsTests
    {
        [Fact]
        public void CalculateWinRate_EmptyList_ReturnsZero()
        {
            // Arrange
            List<MatchEntry> matches = [];

            // Act
            double result = Calculations.CalculateWinRate(matches, out uint wins, out uint losses, out uint ties);

            // Assert
            result.ShouldBe(0);
            wins.ShouldBe(0u);
            losses.ShouldBe(0u);
            ties.ShouldBe(0u);
        }

        [Fact]
        public void CalculateWinRate_AllWins_Returns100()
        {
            // Arrange
            List<MatchEntry> matches =
            [
                new() { Result = MatchResult.Win },
                new() { Result = MatchResult.Win },
                new() { Result = MatchResult.Win }
            ];

            // Act
            double result = Calculations.CalculateWinRate(matches, out uint wins, out uint losses, out uint ties);

            // Assert
            result.ShouldBe(100);
            wins.ShouldBe(3u);
            losses.ShouldBe(0u);
            ties.ShouldBe(0u);
        }

        [Fact]
        public void CalculateWinRate_AllTies_ReturnsZero()
        {
            // Arrange
            List<MatchEntry> matches =
            [
                new() { Result = MatchResult.Tie },
                new() { Result = MatchResult.Tie }
            ];

            // Act
            double result = Calculations.CalculateWinRate(matches, out uint wins, out uint losses, out uint ties);

            // Assert — ties count as zero
            result.ShouldBe(0);
            wins.ShouldBe(0u);
            losses.ShouldBe(0u);
            ties.ShouldBe(2u);
        }

        [Fact]
        public void CalculateWinRate_MixedResults_ReturnsCorrectRate()
        {
            // Arrange — 2 wins, 1 loss, 1 tie = 2 / 4 * 100 = 50
            List<MatchEntry> matches =
            [
                new() { Result = MatchResult.Win },
                new() { Result = MatchResult.Win },
                new() { Result = MatchResult.Loss },
                new() { Result = MatchResult.Tie }
            ];

            // Act
            double result = Calculations.CalculateWinRate(matches, out uint wins, out uint losses, out uint ties);

            // Assert
            result.ShouldBe(50);
            wins.ShouldBe(2u);
            losses.ShouldBe(1u);
            ties.ShouldBe(1u);
        }
    }
}
